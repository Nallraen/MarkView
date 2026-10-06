using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarkView.Models;
using MarkView.Services;

namespace MarkView.ViewModels;

/// <summary>
/// Main window state. Split in partial files:
/// MainViewModel.cs (documents, view modes, zoom, live render), MainViewModel.Files.cs (file commands, session),
/// MainViewModel.Export.cs (export, print, settings, theme).
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    public const double MinZoom = 0.5;
    public const double MaxZoom = 3.0;
    private const double ZoomStep = 0.1;

    private readonly IMarkdownRenderer _renderer;
    private readonly IFileService _fileService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IExportService _exportService;
    private readonly IRecentFilesService _recentFiles;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _renderTimer;
    private int _renderVersion;

    public MainViewModel(
        IMarkdownRenderer renderer,
        IFileService fileService,
        ISettingsService settingsService,
        IThemeService themeService,
        IExportService exportService,
        IRecentFilesService recentFiles,
        IDialogService dialogs)
    {
        _renderer = renderer;
        _fileService = fileService;
        _settingsService = settingsService;
        _themeService = themeService;
        _exportService = exportService;
        _recentFiles = recentFiles;
        _dialogs = dialogs;

        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _renderTimer.Tick += OnRenderTimerTick;
        _themeService.ThemeChanged += OnThemeChanged;
        _isDarkTheme = _themeService.IsDark;
        Settings.PropertyChanged += OnSettingsChanged;
        InitializeFiles();
    }

    public AppSettings Settings => _settingsService.Settings;

    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyCanExecuteChangedFor(nameof(ExportHtmlCommand), nameof(ExportPdfCommand), nameof(PrintCommand), nameof(CopyHtmlCommand))]
    private DocumentViewModel? _activeDocument;

    [ObservableProperty] private bool _isDarkTheme;

    /// <summary>Rendered HTML fragment of the active document (debounced).</summary>
    [ObservableProperty] private string _previewHtml = string.Empty;

    [ObservableProperty] private int _wordCount;

    [ObservableProperty] private int _charCount;

    /// <summary>Shared zoom of editor and preview (1 = 100 %).</summary>
    [ObservableProperty] private double _zoomFactor = 1.0;

    [ObservableProperty] private bool _isFullScreen;

    public string WindowTitle => ActiveDocument is null ? "MarkView" : $"{ActiveDocument.DisplayName} – MarkView";

    public string ViewModeLabel => Settings.ViewMode switch
    {
        ViewMode.EditorOnly => "Édition seule",
        ViewMode.PreviewOnly => "Aperçu seul",
        _ => "Côte à côte",
    };

    /// <summary>Editor → preview scroll sync is only meaningful when both panes are visible.</summary>
    public bool IsScrollSyncActive => Settings.SyncScroll && Settings.ViewMode == ViewMode.SideBySide;

    /// <summary>Called once by the window after loading: restores the session, opens command-line files.</summary>
    public async Task InitializeAsync(IEnumerable<string> commandLinePaths)
    {
        await RestoreSessionAsync();
        await OpenFilesAsync(commandLinePaths);
        if (Documents.Count == 0)
        {
            NewDocument();
        }
    }

    [RelayCommand]
    private void SetViewMode(ViewMode mode) => Settings.ViewMode = mode;

    /// <summary>A local Markdown link was clicked in the preview.</summary>
    [RelayCommand]
    private Task OpenFileFromPreviewAsync(string path) => OpenFilesAsync([path]);

    [RelayCommand]
    private void ZoomIn() => ZoomFactor = Math.Min(MaxZoom, Math.Round(ZoomFactor + ZoomStep, 2));

    [RelayCommand]
    private void ZoomOut() => ZoomFactor = Math.Max(MinZoom, Math.Round(ZoomFactor - ZoomStep, 2));

    [RelayCommand]
    private void ZoomReset() => ZoomFactor = 1.0;

    [RelayCommand]
    private void NextTab() => MoveTab(1);

    [RelayCommand]
    private void PreviousTab() => MoveTab(-1);

    [RelayCommand]
    private void ToggleFullScreen() => IsFullScreen = !IsFullScreen;

    private void MoveTab(int delta)
    {
        if (Documents.Count < 2 || ActiveDocument is null)
        {
            return;
        }

        var index = Documents.IndexOf(ActiveDocument);
        ActiveDocument = Documents[(index + delta + Documents.Count) % Documents.Count];
    }

    partial void OnZoomFactorChanged(double value)
    {
        var clamped = Math.Clamp(value, MinZoom, MaxZoom);
        if (clamped != value)
        {
            ZoomFactor = clamped;
        }
    }

    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Document.TextChanged -= OnActiveTextChanged;
            oldValue.PropertyChanged -= OnActiveDocumentPropertyChanged;
        }

        if (newValue is not null)
        {
            newValue.Document.TextChanged += OnActiveTextChanged;
            newValue.PropertyChanged += OnActiveDocumentPropertyChanged;
        }

        _renderTimer.Stop();
        _ = RenderAsync();
    }

    private void OnActiveDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.DisplayName))
        {
            OnPropertyChanged(nameof(WindowTitle));
        }
    }

    private void OnActiveTextChanged(object? sender, EventArgs e)
    {
        _renderTimer.Stop();
        _renderTimer.Start();
    }

    private async void OnRenderTimerTick(object? sender, EventArgs e)
    {
        _renderTimer.Stop();
        await RenderAsync();
    }

    private async Task RenderAsync()
    {
        var document = ActiveDocument;
        var version = ++_renderVersion;
        if (document is null)
        {
            PreviewHtml = string.Empty;
            WordCount = CharCount = 0;
            return;
        }

        // A snapshot is immutable and safe to read from a worker thread.
        var snapshot = document.Document.CreateSnapshot();
        try
        {
            var (html, words, chars) = await Task.Run(() =>
            {
                var text = snapshot.Text;
                return (_renderer.RenderBody(text), CountWords(text), CountChars(text));
            });

            if (version != _renderVersion)
            {
                return;
            }

            PreviewHtml = html;
            WordCount = words;
            CharCount = chars;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Markdown rendering failed");
        }
    }

    internal static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }

    /// <summary>Characters excluding line breaks.</summary>
    internal static int CountChars(string text) => text.Length - text.Count(c => c is '\r' or '\n');

    private void OnThemeChanged(object? sender, EventArgs e) => IsDarkTheme = _themeService.IsDark;

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.ViewMode) or nameof(AppSettings.SyncScroll))
        {
            OnPropertyChanged(nameof(ViewModeLabel));
            OnPropertyChanged(nameof(IsScrollSyncActive));
        }
    }

    public void Dispose()
    {
        _renderTimer.Stop();
        _themeService.ThemeChanged -= OnThemeChanged;
        Settings.PropertyChanged -= OnSettingsChanged;
        DisposeFiles();
        GC.SuppressFinalize(this);
    }
}
