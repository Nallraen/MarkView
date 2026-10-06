using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MarkView.Models;
using MarkView.Services;

namespace MarkView.ViewModels;

/// <summary>A choice shown in a ComboBox.</summary>
public sealed record Choice<T>(T Value, string Label);

/// <summary>Editable copy of the settings: <see cref="Apply"/> commits it; discarding the instance cancels.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private static readonly Lazy<IReadOnlyList<string>> MonospaceFontList = new(LoadMonospaceFonts);

    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    [ObservableProperty] private AppTheme _theme;
    [ObservableProperty] private string _editorFontFamily;
    [ObservableProperty] private double _editorFontSize;
    [ObservableProperty] private bool _wordWrap;
    [ObservableProperty] private double _previewFontSize;
    [ObservableProperty] private bool _syncScroll;
    [ObservableProperty] private ViewMode _viewMode;
    [ObservableProperty] private bool _exportEmbedImages;

    public SettingsViewModel(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService;
        _themeService = themeService;

        var s = settingsService.Settings;
        _theme = s.Theme;
        _editorFontFamily = s.EditorFontFamily;
        _editorFontSize = s.EditorFontSize;
        _wordWrap = s.WordWrap;
        _previewFontSize = s.PreviewFontSize;
        _syncScroll = s.SyncScroll;
        _viewMode = s.ViewMode;
        _exportEmbedImages = s.ExportEmbedImages;
    }

    public IReadOnlyList<Choice<AppTheme>> Themes { get; } =
    [
        new(AppTheme.Light, "Clair"),
        new(AppTheme.Dark, "Sombre"),
        new(AppTheme.System, "Système"),
    ];

    public IReadOnlyList<Choice<ViewMode>> ViewModes { get; } =
    [
        new(ViewMode.SideBySide, "Côte à côte"),
        new(ViewMode.EditorOnly, "Édition seule"),
        new(ViewMode.PreviewOnly, "Aperçu seul"),
    ];

    /// <summary>Installed fixed-pitch fonts (any other font can still be typed).</summary>
    public IReadOnlyList<string> EditorFonts => MonospaceFontList.Value;

    public IReadOnlyList<double> FontSizes { get; } = [10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24, 28];

    /// <summary>Copies the edited values to the live settings, applies the theme and saves.</summary>
    public void Apply()
    {
        var s = _settingsService.Settings;
        s.Theme = Theme;
        if (!string.IsNullOrWhiteSpace(EditorFontFamily))
        {
            s.EditorFontFamily = EditorFontFamily.Trim();
        }

        s.EditorFontSize = SettingsService.ClampFontSize(EditorFontSize, s.EditorFontSize);
        s.WordWrap = WordWrap;
        s.PreviewFontSize = SettingsService.ClampFontSize(PreviewFontSize, s.PreviewFontSize);
        s.SyncScroll = SyncScroll;
        s.ViewMode = ViewMode;
        s.ExportEmbedImages = ExportEmbedImages;

        _themeService.Apply(Theme);
        _settingsService.Save();
    }

    private static IReadOnlyList<string> LoadMonospaceFonts() =>
        Fonts.SystemFontFamilies
            .Where(IsMonospace)
            .Select(f => f.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    // Fixed pitch ⇔ "i" and "W" have the same advance width.
    private static bool IsMonospace(FontFamily family)
    {
        var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        return typeface.TryGetGlyphTypeface(out var glyphs)
            && glyphs.CharacterToGlyphMap.TryGetValue('i', out var narrow)
            && glyphs.CharacterToGlyphMap.TryGetValue('W', out var wide)
            && Math.Abs(glyphs.AdvanceWidths[narrow] - glyphs.AdvanceWidths[wide]) < 1e-6;
    }
}
