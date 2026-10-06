using CommunityToolkit.Mvvm.ComponentModel;

namespace MarkView.Models;

/// <summary>User settings, persisted as JSON. Observable so the UI reacts to changes live.</summary>
public partial class AppSettings : ObservableObject
{
    [ObservableProperty] private AppTheme _theme = AppTheme.System;
    [ObservableProperty] private string _editorFontFamily = "Cascadia Mono, Consolas";
    [ObservableProperty] private double _editorFontSize = 14;
    [ObservableProperty] private bool _wordWrap = true;
    [ObservableProperty] private double _previewFontSize = 16;
    [ObservableProperty] private bool _syncScroll = true;
    /// <summary>Current view mode; also the mode restored at startup.</summary>
    [ObservableProperty] private ViewMode _viewMode = ViewMode.SideBySide;
    [ObservableProperty] private bool _exportEmbedImages;

    // Window placement (NaN = let Windows decide).
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; }
    /// <summary>Editor share of the width in side-by-side mode (0..1).</summary>
    public double SplitterRatio { get; set; } = 0.5;

    public List<string> RecentFiles { get; set; } = [];
    public List<string> SessionFiles { get; set; } = [];
    public int SessionActiveIndex { get; set; }
}
