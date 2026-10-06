using System.Windows;
using MarkView.Models;

namespace MarkView.Services;

/// <summary>Markdown → HTML conversion. Implementations must be thread-safe (called from a worker thread).</summary>
public interface IMarkdownRenderer
{
    /// <summary>
    /// Renders the markdown to an HTML fragment (no &lt;html&gt;/&lt;body&gt;).
    /// Every top-level block carries a <c>data-line</c> attribute holding its 0-based source line.
    /// </summary>
    string RenderBody(string markdown);
}

/// <summary>Text file I/O with encoding and line-ending detection.</summary>
public interface IFileService
{
    /// <summary>Reads a file, detecting its encoding (BOM, UTF-8 by default) and dominant line ending.</summary>
    Task<TextFileContent> ReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Writes the text as UTF-8 without BOM, normalising every line break to <paramref name="lineEnding"/>.</summary>
    Task WriteAsync(string path, string text, LineEnding lineEnding, CancellationToken cancellationToken = default);
}

/// <summary>Loads and saves <see cref="AppSettings"/> (JSON in %AppData%\MarkView\settings.json).</summary>
public interface ISettingsService
{
    /// <summary>The live settings instance (same object for the whole app lifetime).</summary>
    AppSettings Settings { get; }

    /// <summary>Loads settings from disk into <see cref="Settings"/>; falls back to defaults on a missing or corrupted file.</summary>
    void Load();

    void Save();
}

/// <summary>Applies the light/dark theme to WPF resources and window title bars.</summary>
public interface IThemeService
{
    AppTheme Mode { get; }

    /// <summary>Effective theme: true when the dark palette is applied.</summary>
    bool IsDark { get; }

    /// <summary>Raised (on the UI thread) whenever <see cref="IsDark"/> changes, including after a Windows theme switch in System mode.</summary>
    event EventHandler? ThemeChanged;

    /// <summary>Applies the mode immediately (swaps Application.Resources.MergedDictionaries[0], updates every open window's title bar).</summary>
    void Apply(AppTheme mode);

    /// <summary>Applies the current dark/light title bar to a window (call once its handle exists).</summary>
    void ApplyTitleBar(Window window);
}

/// <summary>Document export: HTML, PDF, printing and clipboard.</summary>
public interface IExportService
{
    /// <summary>Builds a standalone HTML page (light theme CSS and highlight.js inlined).</summary>
    /// <param name="documentPath">Path of the source document, used to resolve relative images; null for unsaved documents.</param>
    string BuildStandaloneHtml(string markdown, string title, string? documentPath, bool embedImages);

    Task ExportHtmlAsync(string markdown, string title, string? documentPath, string targetPath, bool embedImages);

    /// <summary>Renders the document in the light theme and prints it to an A4 PDF.</summary>
    Task ExportPdfAsync(string markdown, string title, string? documentPath, string targetPath);

    /// <summary>Shows a print dialog and prints the document (light theme). Returns false when cancelled.</summary>
    Task<bool> PrintAsync(string markdown, string title, string? documentPath);

    /// <summary>Copies the rendered HTML to the clipboard (HTML + plain-text formats).</summary>
    void CopyHtmlToClipboard(string markdown);
}

/// <summary>Most-recently-used files (max 10), persisted in the settings.</summary>
public interface IRecentFilesService
{
    IReadOnlyList<string> Items { get; }

    event EventHandler? Changed;

    /// <summary>Moves (or inserts) the path at the top of the list.</summary>
    void Add(string path);

    void Remove(string path);

    void Clear();

    /// <summary>Drops entries whose file no longer exists.</summary>
    void Prune();
}

public enum SaveChoice
{
    Save,
    DontSave,
    Cancel,
}

/// <summary>All modal UI used by view models.</summary>
public interface IDialogService
{
    /// <summary>Open dialog with multi-selection; null when cancelled.</summary>
    string[]? ShowOpenMarkdownDialog();

    /// <summary>Save dialog; returns the chosen path or null when cancelled.</summary>
    /// <param name="filter">Win32 filter string, e.g. "Markdown (*.md)|*.md".</param>
    string? ShowSaveDialog(string suggestedFileName, string filter, string defaultExtension);

    /// <summary>"Save changes to X?" with Enregistrer / Ne pas enregistrer / Annuler.</summary>
    SaveChoice AskSaveChanges(string documentName);

    /// <summary>The file changed on disk while it has unsaved edits: true = reload from disk.</summary>
    bool AskReloadChangedFile(string documentName);

    void ShowError(string message);

    void ShowInfo(string message);

    /// <summary>Opens the settings window. Returns true when the user validated.</summary>
    bool ShowSettings();
}
