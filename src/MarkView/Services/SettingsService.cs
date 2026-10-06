using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarkView.Models;

namespace MarkView.Services;

/// <summary>Persists <see cref="AppSettings"/> as indented JSON; never throws on a bad file.</summary>
public sealed class SettingsService(string filePath) : ISettingsService
{
    internal const double MinFontSize = 6;
    internal const double MaxFontSize = 72;
    private const int MaxRecentFiles = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Window placement defaults to NaN ("let Windows decide").
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly PropertyInfo[] CopiedProperties = typeof(AppSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanRead && p.CanWrite)
        .ToArray();

    public SettingsService() : this(AppPaths.SettingsFile) { }

    public AppSettings Settings { get; } = new();

    public void Load()
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        AppSettings? loaded;
        try
        {
            loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, $"Settings file unreadable, defaults used: {filePath}");
            KeepBackup();
            return;
        }

        if (loaded is null)
        {
            return;
        }

        Sanitize(loaded);
        // The live instance is bound everywhere: copy values into it instead of replacing it.
        foreach (var property in CopiedProperties)
        {
            property.SetValue(Settings, property.GetValue(loaded));
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var tempPath = filePath + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(Settings, JsonOptions));
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, $"Saving settings failed: {filePath}");
        }
    }

    internal static double ClampFontSize(double size, double fallback) =>
        double.IsFinite(size) ? Math.Clamp(size, MinFontSize, MaxFontSize) : fallback;

    private static void Sanitize(AppSettings s)
    {
        var defaults = new AppSettings();

        if (!Enum.IsDefined(s.Theme))
        {
            s.Theme = defaults.Theme;
        }

        if (!Enum.IsDefined(s.ViewMode))
        {
            s.ViewMode = defaults.ViewMode;
        }

        if (string.IsNullOrWhiteSpace(s.EditorFontFamily))
        {
            s.EditorFontFamily = defaults.EditorFontFamily;
        }

        s.EditorFontSize = ClampFontSize(s.EditorFontSize, defaults.EditorFontSize);
        s.PreviewFontSize = ClampFontSize(s.PreviewFontSize, defaults.PreviewFontSize);
        s.SplitterRatio = double.IsFinite(s.SplitterRatio) ? Math.Clamp(s.SplitterRatio, 0.1, 0.9) : defaults.SplitterRatio;

        if (!double.IsFinite(s.WindowLeft) || !double.IsFinite(s.WindowTop))
        {
            s.WindowLeft = s.WindowTop = double.NaN;
        }

        if (!double.IsFinite(s.WindowWidth) || s.WindowWidth <= 0 || !double.IsFinite(s.WindowHeight) || s.WindowHeight <= 0)
        {
            s.WindowWidth = defaults.WindowWidth;
            s.WindowHeight = defaults.WindowHeight;
        }

        s.RecentFiles = (s.RecentFiles ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRecentFiles)
            .ToList();
        s.SessionFiles = (s.SessionFiles ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (s.SessionActiveIndex < 0 || s.SessionActiveIndex >= s.SessionFiles.Count)
        {
            s.SessionActiveIndex = 0;
        }
    }

    private void KeepBackup()
    {
        try
        {
            File.Copy(filePath, filePath + ".bak", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Backing up the corrupted settings file failed");
        }
    }
}
