using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using MarkView.Services;
using MarkView.ViewModels;

namespace MarkView.Controls;

/// <summary>WebView2 wrapper rendering the HTML preview.</summary>
/// <remarks>
/// preview.html is loaded once from a virtual host; every later change (content, theme, font size, scroll sync,
/// find) goes through <c>window.markview.*</c> calls made with ExecuteScriptAsync.
/// </remarks>
public partial class PreviewControl : UserControl, IDisposable
{
    /// <summary>HTML fragment produced by IMarkdownRenderer (injected into the page via script).</summary>
    public static readonly DependencyProperty HtmlProperty = DependencyProperty.Register(
        nameof(Html), typeof(string), typeof(PreviewControl), new PropertyMetadata(string.Empty, (d, _) => ((PreviewControl)d).OnHtmlChanged()));

    /// <summary>Folder of the current document: base for relative images and links (null for unsaved documents).</summary>
    public static readonly DependencyProperty DocumentDirectoryProperty = DependencyProperty.Register(
        nameof(DocumentDirectory), typeof(string), typeof(PreviewControl), new PropertyMetadata(null, (d, _) => ((PreviewControl)d).OnDocumentDirectoryChanged()));

    public static readonly DependencyProperty IsDarkThemeProperty = DependencyProperty.Register(
        nameof(IsDarkTheme), typeof(bool), typeof(PreviewControl), new PropertyMetadata(false, (d, _) => ((PreviewControl)d).OnIsDarkThemeChanged()));

    /// <summary>Base font size of the preview in CSS pixels (at 100 % zoom).</summary>
    public static readonly DependencyProperty PreviewFontSizeProperty = DependencyProperty.Register(
        nameof(PreviewFontSize), typeof(double), typeof(PreviewControl), new PropertyMetadata(16.0, (d, _) => ((PreviewControl)d).OnPreviewFontSizeChanged()));

    /// <summary>Shared zoom factor; Ctrl+wheel in the preview updates it (two-way by default).</summary>
    public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(
        nameof(ZoomFactor), typeof(double), typeof(PreviewControl), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((PreviewControl)d).OnZoomFactorChanged()));

    /// <summary>0-based source line the preview should scroll to (when <see cref="IsSyncEnabled"/>).</summary>
    public static readonly DependencyProperty SyncLineProperty = DependencyProperty.Register(
        nameof(SyncLine), typeof(int), typeof(PreviewControl), new PropertyMetadata(0, (d, _) => ((PreviewControl)d).OnSyncChanged()));

    public static readonly DependencyProperty IsSyncEnabledProperty = DependencyProperty.Register(
        nameof(IsSyncEnabled), typeof(bool), typeof(PreviewControl), new PropertyMetadata(false, (d, _) => ((PreviewControl)d).OnSyncChanged()));

    /// <summary>Executed with the full path of a local .md file clicked in the preview.</summary>
    public static readonly DependencyProperty OpenFileCommandProperty = DependencyProperty.Register(
        nameof(OpenFileCommand), typeof(ICommand), typeof(PreviewControl), new PropertyMetadata(null));

    private const string HostSuffix = ".markview.example";
    private const string AppHost = "app" + HostSuffix;
    private const string AppOrigin = "https://" + AppHost + "/";
    private const string PageUrl = AppOrigin + "preview.html";
    private const string DocumentOrigin = "https://doc" + HostSuffix + "/";
    private const double ZoomStep = 0.1;

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown", ".mdown", ".txt" };

    private static readonly Dictionary<string, string> MediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
        [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon",
        [".avif"] = "image/avif",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg",
        [".wav"] = "audio/wav",
    };

    // The JSON goes into a script, never into HTML: no need to escape HTML-sensitive characters.
    private static readonly JsonSerializerOptions ScriptJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly System.Drawing.Color LightBackground = System.Drawing.Color.FromArgb(0xFF, 0xFF, 0xFF);
    private static readonly System.Drawing.Color DarkBackground = System.Drawing.Color.FromArgb(0x0D, 0x11, 0x17);

    private CoreWebView2? _core;
    private bool _isInitStarted;
    private bool _isPageReady;
    private bool _isPushingHtml;
    private bool _isDisposed;
    private string? _pendingHtml;

    public PreviewControl()
    {
        InitializeComponent();
    }

    public string Html { get => (string)GetValue(HtmlProperty); set => SetValue(HtmlProperty, value); }

    public string? DocumentDirectory { get => (string?)GetValue(DocumentDirectoryProperty); set => SetValue(DocumentDirectoryProperty, value); }

    public bool IsDarkTheme { get => (bool)GetValue(IsDarkThemeProperty); set => SetValue(IsDarkThemeProperty, value); }

    public double PreviewFontSize { get => (double)GetValue(PreviewFontSizeProperty); set => SetValue(PreviewFontSizeProperty, value); }

    public double ZoomFactor { get => (double)GetValue(ZoomFactorProperty); set => SetValue(ZoomFactorProperty, value); }

    public int SyncLine { get => (int)GetValue(SyncLineProperty); set => SetValue(SyncLineProperty, value); }

    public bool IsSyncEnabled { get => (bool)GetValue(IsSyncEnabledProperty); set => SetValue(IsSyncEnabledProperty, value); }

    public ICommand? OpenFileCommand { get => (ICommand?)GetValue(OpenFileCommandProperty); set => SetValue(OpenFileCommandProperty, value); }

    /// <summary>Opens a find-in-page UI (Ctrl+F in preview-only mode).</summary>
    public void ShowFind()
    {
        FindBar.Visibility = Visibility.Visible;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    /// <summary>F3 in preview-only mode.</summary>
    public void FindNext() => FindOrShow(backwards: false);

    /// <summary>Shift+F3 in preview-only mode.</summary>
    public void FindPrevious() => FindOrShow(backwards: true);

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _isPageReady = false;
        _core = null;
        WebView.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---- Initialization ---------------------------------------------------------------------

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitStarted)
        {
            return;
        }

        _isInitStarted = true;
        try
        {
            if (!WebViewEnvironment.IsRuntimeAvailable())
            {
                ShowError("Le runtime WebView2 est introuvable : l'aperçu est indisponible, mais l'éditeur reste utilisable.");
                return;
            }

            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Preview initialization");
            ShowError("L'aperçu n'a pas pu démarrer (WebView2), mais l'éditeur reste utilisable. Réinstaller le runtime WebView2 peut résoudre le problème.");
        }
    }

    private async Task InitializeWebViewAsync()
    {
        // Set before the first paint to avoid a white flash in the dark theme.
        WebView.DefaultBackgroundColor = IsDarkTheme ? DarkBackground : LightBackground;
        var assetsFolder = await PreviewAssets.GetFolderAsync();
        await WebView.EnsureCoreWebView2Async(await WebViewEnvironment.GetAsync());
        if (_isDisposed)
        {
            return;
        }

        var core = WebView.CoreWebView2;
        var settings = core.Settings;
        settings.AreDefaultContextMenusEnabled = false;
#if !DEBUG
        settings.AreDevToolsEnabled = false;
#endif
        settings.IsZoomControlEnabled = false;
        settings.IsStatusBarEnabled = false;
        // Ctrl+F, Ctrl+P, F3... are re-raised as WPF key events and reach the window's KeyBindings.
        settings.AreBrowserAcceleratorKeysEnabled = false;

        core.SetVirtualHostNameToFolderMapping(AppHost, assetsFolder, CoreWebView2HostResourceAccessKind.DenyCors);
        core.AddWebResourceRequestedFilter(DocumentOrigin + "*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnDocumentResourceRequested;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.WebMessageReceived += OnWebMessageReceived;
        _core = core;
        WebView.ZoomFactor = ZoomFactor;
        core.Navigate(IsDarkTheme ? PageUrl + "?theme=dark" : PageUrl);
    }

    private void ShowError(string message)
    {
        WebView.Visibility = Visibility.Collapsed;
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void OnDownloadRuntimeClick(object sender, RoutedEventArgs e) => ShellOpen(WebViewEnvironment.RuntimeDownloadUrl);

    // ---- Page state -------------------------------------------------------------------------

    private async Task OnPageReadyAsync()
    {
        _isPageReady = true;
        await RunScriptAsync(
            $"markview.setTheme({Json(IsDarkTheme)});" +
            $"markview.setBase({Json(GetBaseHref())});" +
            $"markview.setFontSize({Json(PreviewFontSize)});" +
            $"markview.sync({Json(EffectiveSyncLine)});");
        _pendingHtml = Html;
        await PushHtmlAsync();
    }

    private void OnHtmlChanged()
    {
        _pendingHtml = Html;
        _ = PushHtmlAsync();
    }

    /// <summary>Sends the latest HTML; updates arriving while a push is in flight collapse into the next one.</summary>
    private async Task PushHtmlAsync()
    {
        if (!_isPageReady || _isPushingHtml)
        {
            return;
        }

        _isPushingHtml = true;
        try
        {
            while (_pendingHtml is { } html && _isPageReady)
            {
                _pendingHtml = null;
                await RunScriptAsync($"markview.setContent({Json(html)})");
            }
        }
        finally
        {
            _isPushingHtml = false;
        }
    }

    private void OnDocumentDirectoryChanged()
    {
        if (_isPageReady)
        {
            _ = RunScriptAsync($"markview.setBase({Json(GetBaseHref())})");
        }
    }

    private void OnIsDarkThemeChanged()
    {
        if (_isDisposed)
        {
            return;
        }

        WebView.DefaultBackgroundColor = IsDarkTheme ? DarkBackground : LightBackground;
        _ = RunScriptAsync($"markview.setTheme({Json(IsDarkTheme)})");
    }

    private void OnPreviewFontSizeChanged() => _ = RunScriptAsync($"markview.setFontSize({Json(PreviewFontSize)})");

    private void OnZoomFactorChanged()
    {
        if (!_isDisposed)
        {
            WebView.ZoomFactor = ZoomFactor;
        }
    }

    private int EffectiveSyncLine => IsSyncEnabled ? SyncLine : -1;

    private void OnSyncChanged() => _ = RunScriptAsync($"markview.sync({Json(EffectiveSyncLine)})");

    /// <summary>
    /// Base URL of the document folder on the document host: its path is the full local path
    /// (<c>C:\a\b</c> → <c>/C%3A/a/b/</c>, <c>\\srv\share</c> → <c>/UNC/srv/share/</c>), so "../img.png" resolves and two
    /// folders never share cached images. Served by <see cref="OnDocumentResourceRequested"/>: a virtual host mapping
    /// added after the page has loaded would only take effect after a reload.
    /// </summary>
    private string? GetBaseHref()
    {
        if (string.IsNullOrEmpty(DocumentDirectory))
        {
            return null;
        }

        var folder = Path.GetFullPath(DocumentDirectory);
        var segments = folder.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var prefix = folder.StartsWith(@"\\", StringComparison.Ordinal) ? "UNC/" : string.Empty;
        return DocumentOrigin + prefix + string.Concat(segments.Select(segment => Uri.EscapeDataString(segment) + "/"));
    }

    /// <summary>Serves local images and media referenced by the document (see <see cref="GetBaseHref"/>).</summary>
    private void OnDocumentResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var segments = new Uri(e.Request.Uri).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToList();
            var path = segments.Count > 1 && segments[0] == "UNC"
                ? @"\\" + string.Join('\\', segments.Skip(1))
                : string.Join('\\', segments);

            // Only media types are served: the page can display local pictures, never read other files.
            if (_core is not null && MediaTypes.TryGetValue(Path.GetExtension(path), out var mediaType) && File.Exists(path))
            {
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                e.Response = _core.Environment.CreateWebResourceResponse(stream, 200, "OK", $"Content-Type: {mediaType}");
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or UriFormatException)
        {
            Log.Error(ex, "Preview resource");
        }

        e.Response = _core?.Environment.CreateWebResourceResponse(null, 404, "Not Found", string.Empty);
    }

    private async Task<string?> RunScriptAsync(string script)
    {
        if (_core is null || !_isPageReady)
        {
            return null;
        }

        try
        {
            return await _core.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Preview script");
            return null;
        }
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, ScriptJson);

    // ---- Messages, links and navigation -----------------------------------------------------

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            if (!e.Source.StartsWith(AppOrigin, StringComparison.Ordinal))
            {
                return;
            }

            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "ready":
                    await OnPageReadyAsync();
                    break;
                case "link":
                    OpenLink(root.GetProperty("href").GetString() ?? string.Empty);
                    break;
                case "zoom":
                    var zoom = Math.Round(ZoomFactor + (root.GetProperty("delta").GetInt32() * ZoomStep), 2);
                    SetCurrentValue(ZoomFactorProperty, Math.Clamp(zoom, MainViewModel.MinZoom, MainViewModel.MaxZoom));
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Preview message");
        }
    }

    /// <summary>Raw href of a clicked link (in-page anchors are handled by the page itself).</summary>
    private void OpenLink(string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            if (uri.Scheme is "http" or "https" or "mailto")
            {
                ShellOpen(uri.AbsoluteUri);
            }

            return;
        }

        var path = uri?.LocalPath ?? ResolveRelativePath(href);
        if (path is null)
        {
            return;
        }

        if (IsMarkdownFile(path))
        {
            OpenMarkdown(path);
        }
        else if (File.Exists(path) || Directory.Exists(path))
        {
            ShellOpen(path);
        }
    }

    private string? ResolveRelativePath(string href)
    {
        if (string.IsNullOrEmpty(DocumentDirectory))
        {
            return null;
        }

        var end = href.IndexOfAny(['#', '?']);
        var relative = Uri.UnescapeDataString(end < 0 ? href : href[..end]);
        return relative.Length == 0 ? null : Path.GetFullPath(Path.Combine(DocumentDirectory, relative));
    }

    private static bool IsMarkdownFile(string path) => MarkdownExtensions.Contains(Path.GetExtension(path)) && File.Exists(path);

    private void OpenMarkdown(string path)
    {
        if (OpenFileCommand?.CanExecute(path) == true)
        {
            OpenFileCommand.Execute(path);
        }
    }

    /// <summary>Safety net: the page never navigates; a dropped .md file opens in a new tab instead.</summary>
    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith(PageUrl, StringComparison.Ordinal))
        {
            _isPageReady = false;
            return;
        }

        e.Cancel = true;
        try
        {
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.IsFile && IsMarkdownFile(uri.LocalPath))
            {
                OpenMarkdown(uri.LocalPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Preview navigation");
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            ShellOpen(uri.AbsoluteUri);
        }
    }

    private static void ShellOpen(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Opening {target}");
        }
    }

    // ---- Find in page -----------------------------------------------------------------------

    private void FindOrShow(bool backwards)
    {
        if (FindBox.Text.Length == 0)
        {
            ShowFind();
        }
        else
        {
            _ = FindAsync(backwards, restart: false);
        }
    }

    /// <param name="restart">Search again from the start of the current match (the term was edited).</param>
    private async Task FindAsync(bool backwards, bool restart)
    {
        var term = FindBox.Text;
        if (term.Length == 0)
        {
            FindStatus.Text = string.Empty;
            return;
        }

        var found = await RunScriptAsync($"markview.find({Json(term)},{Json(backwards)},{Json(restart)})");
        FindStatus.Text = found == "false" ? "Aucun résultat" : string.Empty;
    }

    private void OnFindBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = FindAsync(backwards: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), restart: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFind();
            e.Handled = true;
        }
    }

    private void OnFindBoxTextChanged(object sender, TextChangedEventArgs e) => _ = FindAsync(backwards: false, restart: true);

    private void OnFindPreviousClick(object sender, RoutedEventArgs e) => _ = FindAsync(backwards: true, restart: false);

    private void OnFindNextClick(object sender, RoutedEventArgs e) => _ = FindAsync(backwards: false, restart: false);

    private void OnFindCloseClick(object sender, RoutedEventArgs e) => CloseFind();

    private void CloseFind()
    {
        FindBar.Visibility = Visibility.Collapsed;
        FindStatus.Text = string.Empty;
        WebView.Focus();
    }
}
