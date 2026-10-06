using System.IO;
using System.Security;
using CommunityToolkit.Mvvm.Input;
using MarkView.Models;
using MarkView.Services;

namespace MarkView.ViewModels;

// File commands, external-change watching and session. Runs on the UI thread only.
public partial class MainViewModel
{
    private const string SaveFilter = "Fichiers Markdown (*.md)|*.md|Texte (*.txt)|*.txt|Tous les fichiers (*.*)|*.*";

    private readonly Dictionary<DocumentViewModel, FileWatcher> _watchers = [];
    private bool _isReloadPromptOpen;

    /// <summary>Recent files for the File menu (most recent first). A new list instance after every change.</summary>
    public IReadOnlyList<string> RecentFiles => _recentFiles.Items;

    /// <summary>Called from the constructor.</summary>
    private void InitializeFiles()
    {
        _recentFiles.Changed += OnRecentFilesChanged;
        _recentFiles.Prune();
    }

    /// <summary>Called from Dispose: releases file watchers.</summary>
    private void DisposeFiles()
    {
        _recentFiles.Changed -= OnRecentFilesChanged;
        foreach (var watcher in _watchers.Values)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    /// <summary>Opens files (command line, drag &amp; drop, second instance, preview links). Already-open files are just activated.</summary>
    public async Task OpenFilesAsync(IEnumerable<string> paths)
    {
        // Dropped folders are ignored rather than reported as "access denied".
        foreach (var path in paths.Where(p => !Directory.Exists(p)).ToList())
        {
            await OpenFileAsync(path, addToRecent: true);
        }
    }

    /// <summary>Asks to save every dirty document. Returns false if the user cancelled (the app must not close).</summary>
    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (var document in Documents.Where(d => d.IsDirty).ToList())
        {
            if (!await ConfirmCloseAsync(document))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Stores the paths of saved open documents in the settings (the caller saves the settings).</summary>
    public void SaveSession()
    {
        var saved = Documents.Where(d => d.FilePath is not null).ToList();
        Settings.SessionFiles = [.. saved.Select(d => d.FilePath!)];
        Settings.SessionActiveIndex = ActiveDocument is null ? 0 : Math.Max(0, saved.IndexOf(ActiveDocument));
    }

    private async Task RestoreSessionAsync()
    {
        var paths = Settings.SessionFiles.ToList();
        var activeIndex = Settings.SessionActiveIndex;
        DocumentViewModel? toActivate = null;
        for (var i = 0; i < paths.Count; i++)
        {
            if (!File.Exists(paths[i]))
            {
                continue;
            }

            // Restoring must not reorder the recent files.
            var document = await OpenFileAsync(paths[i], addToRecent: false);
            if (i == activeIndex)
            {
                toActivate = document;
            }
        }

        if (toActivate is not null)
        {
            ActiveDocument = toActivate;
        }
    }

    [RelayCommand]
    private void NewDocument()
    {
        var document = new DocumentViewModel();
        Documents.Add(document);
        ActiveDocument = document;
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (_dialogs.ShowOpenMarkdownDialog() is { } paths)
        {
            await OpenFilesAsync(paths);
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string path)
    {
        if (!File.Exists(path))
        {
            _recentFiles.Remove(path);
            _dialogs.ShowError($"Le fichier « {path} » est introuvable. Il a été retiré des fichiers récents.");
            return;
        }

        await OpenFileAsync(path, addToRecent: true);
    }

    [RelayCommand]
    private void ClearRecent() => _recentFiles.Clear();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (ActiveDocument is { } document)
        {
            await SaveDocumentAsync(document, saveAs: false);
        }
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (ActiveDocument is { } document)
        {
            await SaveDocumentAsync(document, saveAs: true);
        }
    }

    /// <summary>Closes the given document (the active one when null), asking to save if dirty.</summary>
    [RelayCommand]
    private async Task CloseDocumentAsync(DocumentViewModel? document)
    {
        document ??= ActiveDocument;
        if (document is not null && await ConfirmCloseAsync(document))
        {
            RemoveDocument(document);
        }
    }

    [RelayCommand]
    private async Task CloseAllAsync()
    {
        if (!await ConfirmCloseAllAsync())
        {
            return;
        }

        foreach (var document in Documents.ToList())
        {
            StopWatching(document);
        }

        Documents.Clear();
        NewDocument();
    }

    private async Task<DocumentViewModel?> OpenFileAsync(string path, bool addToRecent)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            if (FindDocument(fullPath) is null)
            {
                var content = await _fileService.ReadAsync(fullPath);
                // Checked again: the same file may have been opened while reading (e.g. dropped twice).
                if (FindDocument(fullPath) is null)
                {
                    AddLoadedDocument(new DocumentViewModel(fullPath, content));
                }
            }

            var document = FindDocument(fullPath)!;
            ActiveDocument = document;
            if (addToRecent)
            {
                _recentFiles.Add(fullPath);
            }

            return document;
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Log.Error(ex, $"Cannot open {path}");
            _dialogs.ShowError($"Impossible d'ouvrir « {path} » : {Describe(ex)}");
            return null;
        }
    }

    private void AddLoadedDocument(DocumentViewModel document)
    {
        // An untouched "Sans titre" alone in the window is replaced instead of kept as clutter.
        var placeholder = Documents.Count == 1 && Documents[0].IsPristine ? Documents[0] : null;
        Documents.Add(document);
        ActiveDocument = document;
        if (placeholder is not null)
        {
            Documents.Remove(placeholder);
        }

        Watch(document);
    }

    private DocumentViewModel? FindDocument(string fullPath) =>
        Documents.FirstOrDefault(d => string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

    /// <summary>Saves the document (asking for a path when untitled or <paramref name="saveAs"/>). False when cancelled or failed.</summary>
    private async Task<bool> SaveDocumentAsync(DocumentViewModel document, bool saveAs)
    {
        var path = document.FilePath;
        if (saveAs || path is null)
        {
            var suggestedName = document.FilePath is null ? document.FileName + ".md" : document.FileName;
            path = _dialogs.ShowSaveDialog(suggestedName, SaveFilter, ".md");
            if (path is null)
            {
                return false;
            }

            if (FindDocument(path) is { } other && other != document)
            {
                _dialogs.ShowError($"« {path} » est déjà ouvert dans un autre onglet. Fermez-le avant de l'écraser.");
                return false;
            }
        }

        var text = document.Document.Text;
        var previousDiskText = document.DiskText;
        var wasDirty = document.IsDirty;
        // Set before writing so the watcher recognises our own save; marked as saved now so edits typed during the write stay dirty.
        document.DiskText = FileService.NormalizeLineEndings(text, document.LineEnding);
        document.Document.UndoStack.MarkAsOriginalFile();
        try
        {
            await _fileService.WriteAsync(path, text, document.LineEnding);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            Log.Error(ex, $"Cannot save {path}");
            document.DiskText = previousDiskText;
            if (wasDirty)
            {
                document.Document.UndoStack.DiscardOriginalFileMarker();
            }

            _dialogs.ShowError($"Impossible d'enregistrer « {path} » : {Describe(ex)}");
            return false;
        }

        document.EncodingName = "UTF-8";
        if (!string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            document.FilePath = path;
            Watch(document);
        }

        _recentFiles.Add(path);
        return true;
    }

    /// <summary>Asks to save a dirty document. True when it can be closed.</summary>
    private async Task<bool> ConfirmCloseAsync(DocumentViewModel document)
    {
        if (!document.IsDirty)
        {
            return true;
        }

        ActiveDocument = document;
        return _dialogs.AskSaveChanges(document.FileName) switch
        {
            SaveChoice.Save => await SaveDocumentAsync(document, saveAs: false),
            SaveChoice.DontSave => true,
            _ => false,
        };
    }

    private void RemoveDocument(DocumentViewModel document)
    {
        var index = Documents.IndexOf(document);
        if (index < 0)
        {
            return;
        }

        var wasActive = ActiveDocument == document;
        StopWatching(document);
        Documents.RemoveAt(index);
        if (Documents.Count == 0)
        {
            NewDocument();
        }
        else if (wasActive)
        {
            ActiveDocument = Documents[Math.Min(index, Documents.Count - 1)];
        }
    }

    // ---- External changes -------------------------------------------------------------------

    private void Watch(DocumentViewModel document)
    {
        StopWatching(document);
        try
        {
            _watchers[document] = new FileWatcher(document.FilePath!, () => _ = CheckExternalChangeAsync(document));
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // E.g. a network share without change notifications: the document simply isn't watched.
            Log.Error(ex, $"Cannot watch {document.FilePath}");
        }
    }

    private void StopWatching(DocumentViewModel document)
    {
        if (_watchers.Remove(document, out var watcher))
        {
            watcher.Dispose();
        }
    }

    private async Task CheckExternalChangeAsync(DocumentViewModel document)
    {
        if (_isReloadPromptOpen || document.FilePath is not { } path)
        {
            return;
        }

        try
        {
            if (!File.Exists(path))
            {
                // Deleted or renamed outside: keep the text and flag it as unsaved, so closing offers to save it again.
                document.Document.UndoStack.DiscardOriginalFileMarker();
                return;
            }

            var content = await _fileService.ReadAsync(path);
            if (!IsStillOpen(document, path) || content.Text == document.DiskText)
            {
                return;
            }

            if (document.IsDirty)
            {
                ActiveDocument = document;
                _isReloadPromptOpen = true;
                bool reload;
                try
                {
                    reload = _dialogs.AskReloadChangedFile(document.FileName);
                }
                finally
                {
                    _isReloadPromptOpen = false;
                }

                if (!reload)
                {
                    // Don't ask again for this disk version.
                    document.DiskText = content.Text;
                    return;
                }

                // Re-read: the file may have changed again while the question was open.
                content = await _fileService.ReadAsync(path);
                if (!IsStillOpen(document, path))
                {
                    return;
                }
            }

            document.Reload(content);
        }
        catch (Exception ex) when (IsFileError(ex))
        {
            // Typically the file is still locked by the program writing it; its next write triggers another check.
            Log.Error(ex, $"Cannot reload {path}");
        }
    }

    private bool IsStillOpen(DocumentViewModel document, string path) => Documents.Contains(document) && document.FilePath == path;

    private void OnRecentFilesChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(RecentFiles));
        // Persisted right away so the list survives a crash.
        _settingsService.Save();
    }

    private static bool IsFileError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException;

    private static string Describe(Exception ex) => ex switch
    {
        FileNotFoundException or DirectoryNotFoundException => "fichier introuvable.",
        UnauthorizedAccessException or SecurityException => "accès refusé.",
        PathTooLongException => "chemin trop long.",
        ArgumentException or NotSupportedException => "chemin invalide.",
        _ => ex.Message,
    };
}
