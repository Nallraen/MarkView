using System.IO;

namespace MarkView.Services;

public static class AppPaths
{
    /// <summary>%AppData%\MarkView: settings and logs.</summary>
    public static string AppDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarkView");

    /// <summary>%LocalAppData%\MarkView: WebView2 profile and extracted preview assets.</summary>
    public static string LocalDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkView");

    public static string SettingsFile { get; } = Path.Combine(AppDataDir, "settings.json");

    public static string LogsDir { get; } = Path.Combine(AppDataDir, "logs");

    public static string WebViewUserDataDir { get; } = Path.Combine(LocalDataDir, "WebView2");
}
