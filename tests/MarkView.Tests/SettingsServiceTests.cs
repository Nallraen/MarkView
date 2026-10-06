using System.IO;
using MarkView.Models;
using MarkView.Services;

namespace MarkView.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MarkView.Tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var saved = new SettingsService(FilePath);
        saved.Settings.Theme = AppTheme.Dark;
        saved.Settings.EditorFontFamily = "Consolas";
        saved.Settings.EditorFontSize = 18;
        saved.Settings.ViewMode = ViewMode.PreviewOnly;
        saved.Settings.ExportEmbedImages = true;
        saved.Settings.SplitterRatio = 0.3;
        saved.Settings.RecentFiles = [@"C:\a.md", @"C:\b.md"];
        saved.Save();

        var loaded = new SettingsService(FilePath);
        loaded.Load();

        Assert.Equal(AppTheme.Dark, loaded.Settings.Theme);
        Assert.Equal("Consolas", loaded.Settings.EditorFontFamily);
        Assert.Equal(18, loaded.Settings.EditorFontSize);
        Assert.Equal(ViewMode.PreviewOnly, loaded.Settings.ViewMode);
        Assert.True(loaded.Settings.ExportEmbedImages);
        Assert.Equal(0.3, loaded.Settings.SplitterRatio);
        Assert.Equal([@"C:\a.md", @"C:\b.md"], loaded.Settings.RecentFiles);
        Assert.True(double.IsNaN(loaded.Settings.WindowLeft));
        Assert.Contains("\"Dark\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Load_MissingFile_KeepsDefaults()
    {
        var service = new SettingsService(FilePath);
        service.Load();

        AssertDefaults(service.Settings);
    }

    [Fact]
    public void Load_CorruptedFile_KeepsDefaultsWithoutThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ \"Theme\": \"Dark\", \"EditorFontSize\": ");

        var service = new SettingsService(FilePath);
        service.Load();

        AssertDefaults(service.Settings);
        Assert.True(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void Load_OutOfRangeValues_AreSanitized()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var recents = string.Join(",", Enumerable.Range(0, 15).Select(i => $"\"C:\\\\f{i}.md\""));
        File.WriteAllText(FilePath, $$"""
            {
              "Theme": 42,
              "ViewMode": 7,
              "EditorFontFamily": null,
              "EditorFontSize": 500,
              "PreviewFontSize": -3,
              "SplitterRatio": 2,
              "WindowWidth": -10,
              "RecentFiles": [{{recents}}],
              "SessionFiles": null,
              "SessionActiveIndex": 9
            }
            """);

        var service = new SettingsService(FilePath);
        service.Load();
        var s = service.Settings;

        Assert.Equal(AppTheme.System, s.Theme);
        Assert.Equal(ViewMode.SideBySide, s.ViewMode);
        Assert.Equal(new AppSettings().EditorFontFamily, s.EditorFontFamily);
        Assert.Equal(SettingsService.MaxFontSize, s.EditorFontSize);
        Assert.Equal(SettingsService.MinFontSize, s.PreviewFontSize);
        Assert.Equal(0.9, s.SplitterRatio);
        Assert.Equal(new AppSettings().WindowWidth, s.WindowWidth);
        Assert.Equal(10, s.RecentFiles.Count);
        Assert.Empty(s.SessionFiles);
        Assert.Equal(0, s.SessionActiveIndex);
    }

    [Fact]
    public void Load_PopulatesTheSameInstance()
    {
        new SettingsService(FilePath) { Settings = { WordWrap = false } }.Save();
        var service = new SettingsService(FilePath);
        var instance = service.Settings;
        var changed = new List<string?>();
        instance.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        service.Load();

        Assert.Same(instance, service.Settings);
        Assert.False(instance.WordWrap);
        Assert.Contains(nameof(AppSettings.WordWrap), changed);
    }

    private static void AssertDefaults(AppSettings s)
    {
        var defaults = new AppSettings();
        Assert.Equal(defaults.Theme, s.Theme);
        Assert.Equal(defaults.EditorFontSize, s.EditorFontSize);
        Assert.Equal(defaults.ViewMode, s.ViewMode);
        Assert.Empty(s.RecentFiles);
    }
}
