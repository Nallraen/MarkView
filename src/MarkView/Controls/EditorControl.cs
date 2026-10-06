using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using MarkView.Services;
using MarkView.ViewModels;

namespace MarkView.Controls;

/// <summary>AvalonEdit wrapper: Markdown highlighting, formatting commands, find/replace, list continuation.</summary>
/// <remarks>The dependency properties and public methods are a contract used by MainWindow.xaml(.cs).</remarks>
public class EditorControl : UserControl
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document), typeof(TextDocument), typeof(EditorControl), new PropertyMetadata(null, (d, e) => ((EditorControl)d).OnDocumentChanged((TextDocument?)e.OldValue)));

    /// <summary>1-based caret line (bind OneWayToSource).</summary>
    public static readonly DependencyProperty CaretLineProperty = DependencyProperty.Register(
        nameof(CaretLine), typeof(int), typeof(EditorControl), new PropertyMetadata(1));

    /// <summary>1-based caret column (bind OneWayToSource).</summary>
    public static readonly DependencyProperty CaretColumnProperty = DependencyProperty.Register(
        nameof(CaretColumn), typeof(int), typeof(EditorControl), new PropertyMetadata(1));

    /// <summary>0-based index of the first visible line (bind OneWayToSource; drives the preview scroll sync).</summary>
    public static readonly DependencyProperty FirstVisibleLineProperty = DependencyProperty.Register(
        nameof(FirstVisibleLine), typeof(int), typeof(EditorControl), new PropertyMetadata(0));

    public static readonly DependencyProperty WordWrapProperty = DependencyProperty.Register(
        nameof(WordWrap), typeof(bool), typeof(EditorControl), new PropertyMetadata(true, (d, e) => ((EditorControl)d)._editor.WordWrap = (bool)e.NewValue));

    public static readonly DependencyProperty EditorFontFamilyProperty = DependencyProperty.Register(
        nameof(EditorFontFamily), typeof(FontFamily), typeof(EditorControl), new PropertyMetadata(new FontFamily("Consolas"), OnFontChanged));

    /// <summary>Font size at 100 % zoom.</summary>
    public static readonly DependencyProperty EditorFontSizeProperty = DependencyProperty.Register(
        nameof(EditorFontSize), typeof(double), typeof(EditorControl), new PropertyMetadata(14.0, OnFontChanged));

    /// <summary>Shared zoom factor; Ctrl+wheel in the editor updates it (two-way by default).</summary>
    public static readonly DependencyProperty ZoomFactorProperty = DependencyProperty.Register(
        nameof(ZoomFactor), typeof(double), typeof(EditorControl), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFontChanged));

    public static readonly DependencyProperty IsDarkThemeProperty = DependencyProperty.Register(
        nameof(IsDarkTheme), typeof(bool), typeof(EditorControl), new PropertyMetadata(false, (d, _) => ((EditorControl)d).ApplyTheme()));

    private static readonly Lazy<IHighlightingDefinition> LightHighlighting = new(() => LoadHighlighting("MarkdownLight.xshd"));
    private static readonly Lazy<IHighlightingDefinition> DarkHighlighting = new(() => LoadHighlighting("MarkdownDark.xshd"));

    private readonly TextEditor _editor = new() { ShowLineNumbers = true };
    private readonly FindReplacePanel _findPanel;
    private readonly TextDocument _emptyDocument = new();
    private readonly ConditionalWeakTable<TextDocument, ViewState> _viewStates = new();

    public EditorControl()
    {
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.WordWrap = WordWrap;
        _editor.Document = _emptyDocument;
        _editor.IsReadOnly = true;

        _editor.SetResourceReference(BackgroundProperty, "Brush.EditorBackground");
        _editor.SetResourceReference(ForegroundProperty, "Brush.EditorForeground");
        _editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty, "Brush.EditorLineNumbers");
        var textArea = _editor.TextArea;
        textArea.SetResourceReference(TextArea.SelectionBrushProperty, "Brush.EditorSelection");
        // Null keeps the syntax colors inside the selection; the caret follows the foreground brush.
        textArea.SelectionForeground = null;
        textArea.SelectionBorder = null;
        textArea.TextView.SetResourceReference(TextView.CurrentLineBackgroundProperty, "Brush.EditorCurrentLine");
        textArea.TextView.CurrentLineBorder = null;

        // Ctrl+I belongs to the main window (italic), not to AvalonEdit's "indent selection".
        foreach (var binding in textArea.CommandBindings.OfType<CommandBinding>().Where(b => b.Command == AvalonEditCommands.IndentSelection).ToList())
        {
            textArea.CommandBindings.Remove(binding);
        }

        textArea.Caret.PositionChanged += (_, _) => UpdateCaret();
        textArea.TextView.ScrollOffsetChanged += (_, _) => UpdateFirstVisibleLine();
        textArea.TextView.VisualLinesChanged += (_, _) => UpdateFirstVisibleLine();
        textArea.PreviewKeyDown += OnTextAreaPreviewKeyDown;
        _editor.PreviewMouseWheel += OnEditorPreviewMouseWheel;

        _findPanel = new FindReplacePanel(_editor);
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(_editor, 1);
        root.Children.Add(_findPanel);
        root.Children.Add(_editor);
        Content = root;

        RegisterCommands();
        ApplyFont();
        ApplyTheme();
    }

    public TextDocument? Document { get => (TextDocument?)GetValue(DocumentProperty); set => SetValue(DocumentProperty, value); }

    public int CaretLine { get => (int)GetValue(CaretLineProperty); set => SetValue(CaretLineProperty, value); }

    public int CaretColumn { get => (int)GetValue(CaretColumnProperty); set => SetValue(CaretColumnProperty, value); }

    public int FirstVisibleLine { get => (int)GetValue(FirstVisibleLineProperty); set => SetValue(FirstVisibleLineProperty, value); }

    public bool WordWrap { get => (bool)GetValue(WordWrapProperty); set => SetValue(WordWrapProperty, value); }

    public FontFamily EditorFontFamily { get => (FontFamily)GetValue(EditorFontFamilyProperty); set => SetValue(EditorFontFamilyProperty, value); }

    public double EditorFontSize { get => (double)GetValue(EditorFontSizeProperty); set => SetValue(EditorFontSizeProperty, value); }

    public double ZoomFactor { get => (double)GetValue(ZoomFactorProperty); set => SetValue(ZoomFactorProperty, value); }

    public bool IsDarkTheme { get => (bool)GetValue(IsDarkThemeProperty); set => SetValue(IsDarkThemeProperty, value); }

    private bool CanEdit => Document is not null && !_editor.IsReadOnly;

    /// <summary>Gives keyboard focus to the text area.</summary>
    public void FocusEditor() => _editor.TextArea.Focus();

    /// <summary>Opens the find panel (Ctrl+F), pre-filled with the selection.</summary>
    public void ShowFind() => _findPanel.Open(replace: false);

    /// <summary>Opens the find/replace panel (Ctrl+H).</summary>
    public void ShowReplace() => _findPanel.Open(replace: true);

    /// <summary>F3.</summary>
    public void FindNext() => _findPanel.Find(backwards: false);

    /// <summary>Shift+F3.</summary>
    public void FindPrevious() => _findPanel.Find(backwards: true);

    private static IHighlightingDefinition LoadHighlighting(string fileName)
    {
        using var stream = typeof(EditorControl).Assembly.GetManifestResourceStream("Syntax/" + fileName)
            ?? throw new InvalidOperationException($"Missing embedded resource Syntax/{fileName}.");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private static void OnFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EditorControl)d).ApplyFont();

    private void ApplyFont()
    {
        _editor.FontFamily = EditorFontFamily;
        _editor.FontSize = EditorFontSize * ZoomFactor;
    }

    private void ApplyTheme()
    {
        _editor.SyntaxHighlighting = IsDarkTheme ? DarkHighlighting.Value : LightHighlighting.Value;
        _findPanel.IsDarkTheme = IsDarkTheme;
    }

    /// <summary>Swaps the displayed document, remembering caret, selection and scroll position per document.</summary>
    private void OnDocumentChanged(TextDocument? oldDocument)
    {
        if (oldDocument is not null)
        {
            _viewStates.AddOrUpdate(oldDocument, new ViewState(_editor.SelectionStart, _editor.SelectionLength, _editor.VerticalOffset, _editor.HorizontalOffset));
        }

        var document = Document;
        _editor.Document = document ?? _emptyDocument;
        _editor.IsReadOnly = document is null;
        CommandManager.InvalidateRequerySuggested();

        if (document is null || !_viewStates.TryGetValue(document, out var state))
        {
            _editor.CaretOffset = 0;
            _editor.ScrollToHome();
            UpdateCaret();
            return;
        }

        var length = document.TextLength;
        var selectionStart = Math.Min(state.SelectionStart, length);
        _editor.Select(selectionStart, Math.Min(state.SelectionLength, length - selectionStart));
        UpdateCaret();
        // The scroll extent of the new document is only known after layout.
        Dispatcher.InvokeAsync(() =>
        {
            if (_editor.Document == document)
            {
                _editor.ScrollToVerticalOffset(state.VerticalOffset);
                _editor.ScrollToHorizontalOffset(state.HorizontalOffset);
            }
        }, DispatcherPriority.Loaded);
    }

    private void UpdateCaret()
    {
        var caret = _editor.TextArea.Caret;
        SetCurrentValue(CaretLineProperty, caret.Line);
        SetCurrentValue(CaretColumnProperty, caret.Column);
    }

    private void UpdateFirstVisibleLine()
    {
        var textView = _editor.TextArea.TextView;
        if (textView.Document is not null)
        {
            SetCurrentValue(FirstVisibleLineProperty, textView.GetDocumentLineByVisualTop(textView.ScrollOffset.Y).LineNumber - 1);
        }
    }

    private void OnEditorPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            var step = e.Delta > 0 ? 0.1 : -0.1;
            SetCurrentValue(ZoomFactorProperty, Math.Clamp(Math.Round(ZoomFactor + step, 2), MainViewModel.MinZoom, MainViewModel.MaxZoom));
        }
    }

    /// <summary>Esc closes the find panel; Enter continues or ends Markdown lists and quotes.</summary>
    private void OnTextAreaPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _findPanel.IsOpen)
        {
            e.Handled = true;
            _findPanel.Close();
            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || !CanEdit || !_editor.TextArea.Selection.IsEmpty)
        {
            return;
        }

        Guard(() =>
        {
            var document = _editor.Document;
            var line = document.GetLineByOffset(_editor.CaretOffset);
            var newline = TextUtilities.GetNewLineFromDocument(document, line.LineNumber);
            var edit = MarkdownFormatter.ContinueList(document.GetText(line), _editor.CaretOffset - line.Offset, newline);
            if (edit is { } value)
            {
                e.Handled = true;
                Apply(line.Offset, value);
            }
        });
    }

    private void RegisterCommands()
    {
        (RoutedCommand Command, Func<string, int, int, string, TextEdit> Format)[] commands =
        [
            (EditorCommands.Bold, (t, s, l, _) => MarkdownFormatter.ToggleWrap(t, s, l, "**")),
            (EditorCommands.Italic, (t, s, l, _) => MarkdownFormatter.ToggleWrap(t, s, l, "*")),
            (EditorCommands.Strikethrough, (t, s, l, _) => MarkdownFormatter.ToggleWrap(t, s, l, "~~")),
            (EditorCommands.InlineCode, (t, s, l, _) => MarkdownFormatter.ToggleWrap(t, s, l, "`")),
            (EditorCommands.Heading1, (t, s, l, _) => MarkdownFormatter.ToggleHeading(t, s, l, 1)),
            (EditorCommands.Heading2, (t, s, l, _) => MarkdownFormatter.ToggleHeading(t, s, l, 2)),
            (EditorCommands.Heading3, (t, s, l, _) => MarkdownFormatter.ToggleHeading(t, s, l, 3)),
            (EditorCommands.BulletList, (t, s, l, _) => MarkdownFormatter.ToggleLinePrefix(t, s, l, LinePrefix.Bullet)),
            (EditorCommands.NumberedList, (t, s, l, _) => MarkdownFormatter.ToggleLinePrefix(t, s, l, LinePrefix.Numbered)),
            (EditorCommands.TaskList, (t, s, l, _) => MarkdownFormatter.ToggleLinePrefix(t, s, l, LinePrefix.Task)),
            (EditorCommands.Quote, (t, s, l, _) => MarkdownFormatter.ToggleLinePrefix(t, s, l, LinePrefix.Quote)),
            (EditorCommands.Link, (t, s, l, _) => MarkdownFormatter.InsertLink(t, s, l, isImage: false)),
            (EditorCommands.Image, (t, s, l, _) => MarkdownFormatter.InsertLink(t, s, l, isImage: true)),
            (EditorCommands.CodeBlock, MarkdownFormatter.InsertCodeBlock),
            (EditorCommands.Table, MarkdownFormatter.InsertTable),
            (EditorCommands.HorizontalRule, MarkdownFormatter.InsertHorizontalRule),
        ];

        foreach (var (command, format) in commands)
        {
            CommandBindings.Add(new CommandBinding(command, (_, _) => Guard(() => Format(format)), (_, e) => e.CanExecute = CanEdit));
        }
    }

    /// <summary>
    /// Runs a formatter on the lines around the selection (one line of context on each side, so block insertions
    /// can check their neighbors) and applies the result as a single undo step.
    /// </summary>
    private void Format(Func<string, int, int, string, TextEdit> format)
    {
        var document = _editor.Document;
        var selectionStart = _editor.SelectionStart;
        var selectionLength = _editor.SelectionLength;
        var first = document.GetLineByOffset(selectionStart);
        var last = document.GetLineByOffset(selectionStart + selectionLength);
        var contextStart = (first.PreviousLine ?? first).Offset;
        var contextEnd = (last.NextLine ?? last).EndOffset;
        var context = document.GetText(contextStart, contextEnd - contextStart);
        var newline = TextUtilities.GetNewLineFromDocument(document, first.LineNumber);

        Apply(contextStart, format(context, selectionStart - contextStart, selectionLength, newline));
        FocusEditor();
    }

    private void Apply(int baseOffset, TextEdit edit)
    {
        _editor.Document.Replace(baseOffset + edit.Offset, edit.Length, edit.Replacement);
        _editor.Select(baseOffset + edit.SelectionStart, edit.SelectionLength);
        _editor.TextArea.Caret.BringCaretToView();
    }

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Editor command failed");
        }
    }

    private sealed record ViewState(int SelectionStart, int SelectionLength, double VerticalOffset, double HorizontalOffset);
}
