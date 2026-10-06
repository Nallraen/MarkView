using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using ICSharpCode.AvalonEdit.Document;
using MarkView.Models;

namespace MarkView.ViewModels;

/// <summary>One open document (tab). Must be created and used on the UI thread (TextDocument has thread affinity).</summary>
public partial class DocumentViewModel : ObservableObject
{
    private static int _untitledCounter;

    private readonly string _untitledName;

    public DocumentViewModel(string? filePath = null, string text = "")
    {
        _filePath = filePath;
        Document = new TextDocument(text);
        _untitledName = filePath is null ? $"Sans titre {++_untitledCounter}" : string.Empty;
        Document.UndoStack.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
            {
                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(DisplayName));
            }
        };
    }

    /// <summary>Document loaded from disk.</summary>
    public DocumentViewModel(string filePath, TextFileContent content)
        : this(filePath, content.Text)
    {
        _encodingName = content.EncodingName;
        _lineEnding = content.LineEnding;
        DiskText = content.Text;
    }

    /// <summary>AvalonEdit document; holds the text and the undo stack.</summary>
    public TextDocument Document { get; }

    /// <summary>Full path, or null for a document never saved.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName), nameof(DisplayName), nameof(DirectoryPath))]
    private string? _filePath;

    /// <summary>File name ("Sans titre N" when unsaved).</summary>
    public string FileName => FilePath is null ? _untitledName : Path.GetFileName(FilePath);

    /// <summary>Folder of the file (used to resolve relative images and links), null when unsaved.</summary>
    public string? DirectoryPath => FilePath is null ? null : Path.GetDirectoryName(FilePath);

    /// <summary>True when the text differs from the last saved/loaded version.</summary>
    public bool IsDirty => !Document.UndoStack.IsOriginalFile;

    /// <summary>Tab header and window title: file name followed by "*" when modified.</summary>
    public string DisplayName => IsDirty ? FileName + "*" : FileName;

    /// <summary>True for an empty, never saved, unmodified document (replaced when a file is opened).</summary>
    public bool IsPristine => FilePath is null && !IsDirty && Document.TextLength == 0;

    /// <summary>Text as last read from or written to disk; tells external changes apart from our own saves.</summary>
    internal string? DiskText { get; set; }

    [ObservableProperty] private string _encodingName = "UTF-8";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LineEndingLabel))]
    private LineEnding _lineEnding = LineEnding.CrLf;

    /// <summary>"CRLF" or "LF" for the status bar.</summary>
    public string LineEndingLabel => LineEnding == LineEnding.Lf ? "LF" : "CRLF";

    /// <summary>1-based caret line, pushed by the editor.</summary>
    [ObservableProperty] private int _caretLine = 1;

    /// <summary>1-based caret column, pushed by the editor.</summary>
    [ObservableProperty] private int _caretColumn = 1;

    /// <summary>
    /// Replaces the text with the disk version as a single undoable edit, then marks it as saved.
    /// Only the differing middle part is replaced, so the caret and scroll position survive when they lie outside it.
    /// </summary>
    public void Reload(TextFileContent content)
    {
        var current = Document.Text;
        var text = content.Text;
        var prefix = current.AsSpan().CommonPrefixLength(text);
        var suffix = 0;
        var maxSuffix = Math.Min(current.Length, text.Length) - prefix;
        while (suffix < maxSuffix && current[^(suffix + 1)] == text[^(suffix + 1)])
        {
            suffix++;
        }

        if (prefix + suffix < Math.Max(current.Length, text.Length))
        {
            Document.Replace(prefix, current.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
        }

        Document.UndoStack.MarkAsOriginalFile();
        DiskText = text;
        EncodingName = content.EncodingName;
        LineEnding = content.LineEnding;
    }
}
