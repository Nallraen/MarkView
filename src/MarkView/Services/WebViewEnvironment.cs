using Microsoft.Web.WebView2.Core;

namespace MarkView.Services;

/// <summary>
/// Single shared WebView2 environment (user data in %LocalAppData%\MarkView\WebView2),
/// so every WebView2 of the app uses the same, writable profile.
/// </summary>
public static class WebViewEnvironment
{
    public const string RuntimeDownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>True when an Evergreen/fixed WebView2 runtime is installed.</summary>
    public static bool IsRuntimeAvailable()
    {
        try
        {
            return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString());
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Must be called from the UI thread.</summary>
    public static Task<CoreWebView2Environment> GetAsync() =>
        _environment ??= CoreWebView2Environment.CreateAsync(null, AppPaths.WebViewUserDataDir);
}
