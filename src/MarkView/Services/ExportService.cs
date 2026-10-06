using System.Globalization;
using System.IO;
using System.Net;
using System.Printing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace MarkView.Services;

/// <summary>Standalone HTML, PDF (WebView2), printing and clipboard export, always in the light theme.</summary>
public sealed partial class ExportService(IMarkdownRenderer renderer) : IExportService
{
    private const double A4Width = 8.27;
    private const double A4Height = 11.69;
    private const double PageMargin = 0.6;

    private const string LayoutCss =
        "body{margin:0;background:#fff}" +
        ".markdown-body{box-sizing:border-box;max-width:980px;margin:0 auto;padding:45px}" +
        ".markdown-body pre code.hljs{padding:0}" +
        "@media (max-width:767px){.markdown-body{padding:15px}}" +
        "@media print{.markdown-body{max-width:none;padding:0}}";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly string Styles =
        ReadResource("Preview/github-markdown-light.css") + "\n" + ReadResource("Preview/hljs-github.min.css") + "\n" + LayoutCss;

    private static readonly string HighlightScript = ReadResource("Preview/highlight.min.js");

    private static readonly Dictionary<string, string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
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
    };

    public string BuildStandaloneHtml(string markdown, string title, string? documentPath, bool embedImages)
    {
        var body = renderer.RenderBody(markdown);
        if (embedImages)
        {
            body = EmbedImages(body, documentPath is null ? null : Path.GetDirectoryName(documentPath));
        }

        // Same rule as the preview: raw HTML is kept, but only our own (nonce-tagged) scripts may run.
        // Besides security, this keeps an injected alert() from blocking the off-screen PDF/print render.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        return new StringBuilder(body.Length + Styles.Length + HighlightScript.Length + 768)
            .Append("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; script-src 'nonce-").Append(nonce)
            .Append("'; style-src 'unsafe-inline'; img-src 'self' * data: file:; media-src 'self' * data: file:; font-src data:\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n")
            .Append("<style>\n").Append(Styles).Append("\n</style>\n</head>\n<body>\n")
            .Append("<article class=\"markdown-body\">\n").Append(body).Append("\n</article>\n")
            .Append("<script nonce=\"").Append(nonce).Append("\">\n").Append(HighlightScript).Append("\n</script>\n")
            .Append("<script nonce=\"").Append(nonce).Append("\">hljs.highlightAll();</script>\n</body>\n</html>\n")
            .ToString();
    }

    public async Task ExportHtmlAsync(string markdown, string title, string? documentPath, string targetPath, bool embedImages)
    {
        var html = await Task.Run(() => BuildStandaloneHtml(markdown, title, documentPath, embedImages)).ConfigureAwait(false);
        await File.WriteAllTextAsync(targetPath, html, Utf8NoBom).ConfigureAwait(false);
    }

    public Task ExportPdfAsync(string markdown, string title, string? documentPath, string targetPath) =>
        RenderOffscreenAsync(markdown, title, documentPath, async (environment, webView) =>
        {
            var settings = CreatePrintSettings(environment);
            settings.PageWidth = A4Width;
            settings.PageHeight = A4Height;
            if (!await webView.PrintToPdfAsync(targetPath, settings))
            {
                throw new IOException($"Impossible d'enregistrer le PDF « {targetPath} ».");
            }
        });

    public async Task<bool> PrintAsync(string markdown, string title, string? documentPath)
    {
        EnsureRuntime();
        var dialog = new PrintDialog { UserPageRangeEnabled = false };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var ticket = dialog.PrintTicket;
        await RenderOffscreenAsync(markdown, title, documentPath, async (environment, webView) =>
        {
            var settings = CreatePrintSettings(environment);
            settings.PrinterName = dialog.PrintQueue.FullName;
            settings.Copies = Math.Max(1, ticket.CopyCount ?? 1);
            settings.Orientation = ticket.PageOrientation is PageOrientation.Landscape or PageOrientation.ReverseLandscape
                ? CoreWebView2PrintOrientation.Landscape
                : CoreWebView2PrintOrientation.Portrait;
            if (ticket.PageMediaSize is { Width: > 0, Height: > 0 } media)
            {
                // PrintTicket sizes are in 1/96 inch.
                settings.PageWidth = media.Width.Value / 96;
                settings.PageHeight = media.Height.Value / 96;
            }

            var status = await webView.PrintAsync(settings);
            if (status != CoreWebView2PrintStatus.Succeeded)
            {
                throw new IOException(status == CoreWebView2PrintStatus.PrinterUnavailable
                    ? $"L'imprimante « {settings.PrinterName} » n'est pas disponible."
                    : "L'impression a échoué.");
            }
        });
        return true;
    }

    public void CopyHtmlToClipboard(string markdown)
    {
        var html = renderer.RenderBody(markdown);
        var data = new DataObject();
        data.SetData(DataFormats.Html, BuildCfHtml(html));
        data.SetData(DataFormats.UnicodeText, html);

        // The clipboard may be briefly locked by another process.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, true);
                return;
            }
            catch (COMException) when (attempt < 5)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>Wraps an HTML fragment in the Windows CF_HTML clipboard format (offsets are UTF-8 byte counts).</summary>
    internal static string BuildCfHtml(string fragment)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string prefix = "<html><head><meta charset=\"utf-8\"></head><body><!--StartFragment-->";
        const string suffix = "<!--EndFragment--></body></html>";

        var startHtml = string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0).Length;
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        var endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);
        return string.Format(CultureInfo.InvariantCulture, header, startHtml, endHtml, startFragment, endFragment) + prefix + fragment + suffix;
    }

    /// <summary>Replaces local &lt;img src&gt; files by data: URIs; remote, data: and missing images are left untouched.</summary>
    internal static string EmbedImages(string html, string? baseDirectory) =>
        ImageSourceRegex().Replace(html, match =>
        {
            var path = ResolveLocalImage(match.Groups["src"].Value, baseDirectory);
            if (path is null || !ImageMimeTypes.TryGetValue(Path.GetExtension(path), out var mime))
            {
                return match.Value;
            }

            var quote = match.Groups["quote"].Value;
            var data = Convert.ToBase64String(File.ReadAllBytes(path));
            return $"{match.Groups["prefix"].Value}{quote}data:{mime};base64,{data}{quote}";
        });

    private static string? ResolveLocalImage(string source, string? baseDirectory)
    {
        var src = Uri.UnescapeDataString(WebUtility.HtmlDecode(source).Trim());
        if (src.Length == 0 || src.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        string path;
        if (Uri.TryCreate(src, UriKind.Absolute, out var uri))
        {
            // http(s):, data:, mailto: ... are not local files.
            if (!uri.IsFile)
            {
                return null;
            }

            path = uri.LocalPath;
        }
        else
        {
            if (baseDirectory is null)
            {
                return null;
            }

            var end = src.IndexOfAny(['?', '#']);
            path = Path.GetFullPath(Path.Combine(baseDirectory, end < 0 ? src : src[..end]));
        }

        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Loads the standalone page (images embedded so relative ones resolve) in an invisible WebView2
    /// hosted by a never-shown window, runs <paramref name="action"/>, then cleans everything up.
    /// Must be called on the UI thread.
    /// </summary>
    private async Task RenderOffscreenAsync(string markdown, string title, string? documentPath,
        Func<CoreWebView2Environment, CoreWebView2, Task> action)
    {
        EnsureRuntime();
        // No ConfigureAwait(false): WebView2 and WPF objects below need the UI thread.
        var html = await Task.Run(() => BuildStandaloneHtml(markdown, title, documentPath, embedImages: true));
        var tempFile = Path.Combine(Path.GetTempPath(), $"MarkView-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(tempFile, html, Utf8NoBom);

        var host = new Window();
        CoreWebView2Controller? controller = null;
        try
        {
            var hwnd = new WindowInteropHelper(host).EnsureHandle();
            var environment = await WebViewEnvironment.GetAsync();
            controller = await environment.CreateCoreWebView2ControllerAsync(hwnd);
            controller.IsVisible = false;
            controller.Bounds = new System.Drawing.Rectangle(0, 0, 1024, 768);

            var webView = controller.CoreWebView2;
            // Belt and braces: a modal script dialog would block the invisible render forever.
            webView.Settings.AreDefaultScriptDialogsEnabled = false;
            webView.ScriptDialogOpening += (_, e) => e.Accept();
            var navigated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            webView.NavigationCompleted += (_, e) => navigated.TrySetResult(e.IsSuccess);
            webView.Navigate(new Uri(tempFile).AbsoluteUri);
            if (!await navigated.Task)
            {
                throw new IOException("Le rendu du document a échoué.");
            }

            await action(environment, webView);
        }
        finally
        {
            controller?.Close();
            host.Close();
            TryDelete(tempFile);
        }
    }

    private static CoreWebView2PrintSettings CreatePrintSettings(CoreWebView2Environment environment)
    {
        var settings = environment.CreatePrintSettings();
        settings.MarginTop = settings.MarginBottom = settings.MarginLeft = settings.MarginRight = PageMargin;
        settings.ShouldPrintBackgrounds = true;
        settings.ShouldPrintHeaderAndFooter = false;
        return settings;
    }

    private static void EnsureRuntime()
    {
        if (!WebViewEnvironment.IsRuntimeAvailable())
        {
            throw new InvalidOperationException(
                $"Le runtime Microsoft Edge WebView2 est introuvable : il est nécessaire pour l'export PDF et l'impression.\n" +
                $"Téléchargez-le sur {WebViewEnvironment.RuntimeDownloadUrl}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, $"Deleting the temporary export file failed: {path}");
        }
    }

    private static string ReadResource(string name)
    {
        using var stream = typeof(ExportService).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing embedded resource {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex("""(?<prefix><img\b[^>]*?\bsrc\s*=\s*)(?<quote>["'])(?<src>.*?)\k<quote>""", RegexOptions.IgnoreCase)]
    private static partial Regex ImageSourceRegex();
}
