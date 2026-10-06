using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MarkView.Controls;
using MarkView.Models;
using MarkView.Services;
using MarkView.ViewModels;

namespace MarkView.Views;

/// <summary>
/// View glue only: pane layout per view mode, window placement, full screen, drag &amp; drop,
/// find routing (editor vs preview) and the formatting shortcuts.
/// </summary>
public partial class MainWindow : Window
{
    public static readonly RoutedUICommand FindNextCommand = new("Occurrence suivante", nameof(FindNextCommand), typeof(MainWindow));
    public static readonly RoutedUICommand FindPreviousCommand = new("Occurrence précédente", nameof(FindPreviousCommand), typeof(MainWindow));

    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private bool _closeConfirmed;
    private WindowState _stateBeforeFullScreen;

    public MainWindow(MainViewModel viewModel, ISettingsService settingsService, IThemeService themeService)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _themeService = themeService;
        InitializeComponent();
        DataContext = viewModel;
        RegisterFormattingShortcuts();
        RestorePlacement();

        viewModel.Settings.PropertyChanged += OnSettingsChanged;
        viewModel.PropertyChanged += OnViewModelChanged;
        ApplyViewMode();
        Loaded += OnLoaded;
    }

    /// <summary>Paths passed on the command line, opened once the window is loaded.</summary>
    public IReadOnlyList<string> StartupPaths { get; set; } = [];

    private AppSettings Settings => _settingsService.Settings;

    /// <summary>Called when a second MarkView process forwarded its command line.</summary>
    public async Task OpenFromOtherInstanceAsync(string[] paths)
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        // Topmost toggle forces the window in front of the launching app.
        Topmost = true;
        Topmost = false;
        Focus();
        await _viewModel.OpenFilesAsync(paths);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _themeService.ApplyTitleBar(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync(StartupPaths);
        Editor.FocusEditor();
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed)
        {
            SavePlacement();
            _viewModel.SaveSession();
            _settingsService.Save();
            return;
        }

        e.Cancel = true;
        if (await _viewModel.ConfirmCloseAllAsync())
        {
            _closeConfirmed = true;
            // Deferred: the await may have completed synchronously, and Close() is not allowed while closing.
            _ = Dispatcher.BeginInvoke(Close);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Settings.PropertyChanged -= OnSettingsChanged;
        _viewModel.PropertyChanged -= OnViewModelChanged;
        Preview.Dispose();
        base.OnClosed(e);
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.ViewMode))
        {
            ApplyViewMode();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsFullScreen))
        {
            ApplyFullScreen(_viewModel.IsFullScreen);
        }
    }

    // ---- View modes -------------------------------------------------------------------------

    private void ApplyViewMode()
    {
        var mode = Settings.ViewMode;
        var showEditor = mode != ViewMode.PreviewOnly;
        var showPreview = mode != ViewMode.EditorOnly;
        var ratio = Math.Clamp(Settings.SplitterRatio, 0.1, 0.9);

        Editor.Visibility = showEditor ? Visibility.Visible : Visibility.Collapsed;
        Preview.Visibility = showPreview ? Visibility.Visible : Visibility.Collapsed;
        Splitter.Visibility = showEditor && showPreview ? Visibility.Visible : Visibility.Collapsed;
        EditorColumn.Width = showEditor ? new GridLength(showPreview ? ratio : 1, GridUnitType.Star) : new GridLength(0);
        PreviewColumn.Width = showPreview ? new GridLength(showEditor ? 1 - ratio : 1, GridUnitType.Star) : new GridLength(0);

        if (showEditor && IsLoaded)
        {
            Editor.FocusEditor();
        }
    }

    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        var total = EditorColumn.ActualWidth + PreviewColumn.ActualWidth;
        if (total > 0)
        {
            Settings.SplitterRatio = EditorColumn.ActualWidth / total;
        }
    }

    // ---- Find routing -----------------------------------------------------------------------

    private bool PreviewHasFind => Settings.ViewMode == ViewMode.PreviewOnly;

    private void OnFind(object sender, ExecutedRoutedEventArgs e)
    {
        if (PreviewHasFind)
        {
            Preview.ShowFind();
        }
        else
        {
            Editor.ShowFind();
        }
    }

    private void OnReplace(object sender, ExecutedRoutedEventArgs e)
    {
        if (!PreviewHasFind)
        {
            Editor.ShowReplace();
        }
    }

    private void OnFindNext(object sender, ExecutedRoutedEventArgs e)
    {
        if (PreviewHasFind)
        {
            Preview.FindNext();
        }
        else
        {
            Editor.FindNext();
        }
    }

    private void OnFindPrevious(object sender, ExecutedRoutedEventArgs e)
    {
        if (PreviewHasFind)
        {
            Preview.FindPrevious();
        }
        else
        {
            Editor.FindPrevious();
        }
    }

    // ---- Formatting shortcuts ---------------------------------------------------------------

    private void RegisterFormattingShortcuts()
    {
        (RoutedCommand Command, Key Key, ModifierKeys Modifiers)[] shortcuts =
        [
            (EditorCommands.Bold, Key.B, ModifierKeys.Control),
            (EditorCommands.Italic, Key.I, ModifierKeys.Control),
            (EditorCommands.Strikethrough, Key.X, ModifierKeys.Control | ModifierKeys.Shift),
            (EditorCommands.InlineCode, Key.E, ModifierKeys.Control),
            (EditorCommands.Heading1, Key.D1, ModifierKeys.Control | ModifierKeys.Alt),
            (EditorCommands.Heading2, Key.D2, ModifierKeys.Control | ModifierKeys.Alt),
            (EditorCommands.Heading3, Key.D3, ModifierKeys.Control | ModifierKeys.Alt),
            (EditorCommands.Heading1, Key.NumPad1, ModifierKeys.Control | ModifierKeys.Alt),
            (EditorCommands.Heading2, Key.NumPad2, ModifierKeys.Control | ModifierKeys.Alt),
            (EditorCommands.Heading3, Key.NumPad3, ModifierKeys.Control | ModifierKeys.Alt),
            (EditorCommands.BulletList, Key.D8, ModifierKeys.Control | ModifierKeys.Shift),
            (EditorCommands.NumberedList, Key.D7, ModifierKeys.Control | ModifierKeys.Shift),
            (EditorCommands.TaskList, Key.C, ModifierKeys.Control | ModifierKeys.Shift),
            (EditorCommands.Quote, Key.Q, ModifierKeys.Control | ModifierKeys.Shift),
            (EditorCommands.Link, Key.K, ModifierKeys.Control),
            (EditorCommands.Image, Key.I, ModifierKeys.Control | ModifierKeys.Shift),
            (EditorCommands.CodeBlock, Key.K, ModifierKeys.Control | ModifierKeys.Shift),
        ];

        foreach (var (command, key, modifiers) in shortcuts)
        {
            InputBindings.Add(new KeyBinding(command, new AltGrSafeKeyGesture(key, modifiers)) { CommandTarget = Editor });
        }
    }

    /// <summary>
    /// AltGr is reported as Ctrl+Alt: without this, typing "#" or "~" on an AZERTY keyboard (AltGr+3, AltGr+2)
    /// would trigger the Ctrl+Alt+digit heading shortcuts.
    /// </summary>
    private sealed class AltGrSafeKeyGesture(Key key, ModifierKeys modifiers) : KeyGesture(key, modifiers)
    {
        public override bool Matches(object targetElement, InputEventArgs inputEventArgs) =>
            !Keyboard.IsKeyDown(Key.RightAlt) && base.Matches(targetElement, inputEventArgs);
    }

    // ---- Tabs, drag & drop ------------------------------------------------------------------

    private void OnTabHeaderMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is FrameworkElement { DataContext: DocumentViewModel document })
        {
            _viewModel.CloseDocumentCommand.Execute(document);
            e.Handled = true;
        }
    }

    protected override void OnPreviewDragOver(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }

        base.OnPreviewDragOver(e);
    }

    protected override async void OnPreviewDrop(DragEventArgs e)
    {
        base.OnPreviewDrop(e);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            e.Handled = true;
            await _viewModel.OpenFilesAsync(files);
        }
    }

    // ---- Full screen & placement ------------------------------------------------------------

    private void ApplyFullScreen(bool fullScreen)
    {
        if (fullScreen)
        {
            _stateBeforeFullScreen = WindowState;
            WindowStyle = WindowStyle.None;
            // Going through Normal makes a maximized borderless window cover the taskbar.
            WindowState = WindowState.Normal;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _stateBeforeFullScreen;
        }
    }

    private void RestorePlacement()
    {
        var s = Settings;
        Width = Math.Max(MinWidth, s.WindowWidth);
        Height = Math.Max(MinHeight, s.WindowHeight);

        // Note: checks against the virtual-screen bounding box; a window placed in the gap of an
        // L-shaped multi-monitor layout would pass. Use MonitorFromRect if that ever matters.
        var visible = !double.IsNaN(s.WindowLeft) && !double.IsNaN(s.WindowTop)
            && s.WindowLeft + 100 < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && s.WindowLeft + Width - 100 > SystemParameters.VirtualScreenLeft
            && s.WindowTop >= SystemParameters.VirtualScreenTop - 10
            && s.WindowTop + 50 < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

        if (visible)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = s.WindowLeft;
            Top = s.WindowTop;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (s.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SavePlacement()
    {
        if (_viewModel.IsFullScreen)
        {
            ApplyFullScreen(false);
        }

        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        var s = Settings;
        s.WindowLeft = bounds.Left;
        s.WindowTop = bounds.Top;
        s.WindowWidth = bounds.Width;
        s.WindowHeight = bounds.Height;
        s.WindowMaximized = WindowState == WindowState.Maximized;
    }
}
