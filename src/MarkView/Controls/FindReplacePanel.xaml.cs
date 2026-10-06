using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using TextSearch = MarkView.Services.TextSearch;

namespace MarkView.Controls;

/// <summary>Find/replace bar docked above the editor; highlights every match of the active document.</summary>
public partial class FindReplacePanel : UserControl
{
    private static readonly Brush ErrorBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD1, 0x24, 0x2F)));
    private static readonly Brush LightHighlight = Freeze(new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xD3, 0x3D)));
    private static readonly Brush DarkHighlight = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0xBB, 0x80, 0x09)));

    private readonly TextEditor _editor;
    private readonly MatchRenderer _renderer = new();
    private readonly DispatcherTimer _refreshTimer;
    private TextDocument? _document;
    private bool _jumpOnRefresh;

    public FindReplacePanel(TextEditor editor)
    {
        InitializeComponent();
        _editor = editor;
        _editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
        _editor.DocumentChanged += (_, _) => AttachDocument();
        // Note: debounced full rescan of the document; enough for ~1 MB files, switch to an incremental search if larger files matter.
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(200) };
        _refreshTimer.Tick += (_, _) => Refresh();
        AttachDocument();
    }

    public bool IsOpen => Visibility == Visibility.Visible;

    /// <summary>Picks the match highlight color for the theme.</summary>
    public bool IsDarkTheme
    {
        set
        {
            _renderer.Brush = value ? DarkHighlight : LightHighlight;
            _editor.TextArea.TextView.InvalidateLayer(_renderer.Layer);
        }
    }

    /// <summary>Shows the panel (with the replace row in replace mode), pre-filled with a single-line selection.</summary>
    public void Open(bool replace)
    {
        var selection = _editor.SelectedText;
        if (selection.Length > 0 && !selection.Contains('\n') && !selection.Contains('\r'))
        {
            FindBox.Text = RegexToggle.IsChecked == true ? Regex.Escape(selection) : selection;
        }

        var replaceVisibility = replace ? Visibility.Visible : Visibility.Collapsed;
        ReplaceBox.Visibility = replaceVisibility;
        ReplaceButtons.Visibility = replaceVisibility;
        Visibility = Visibility.Visible;
        _jumpOnRefresh = false;
        Refresh();
        FindBox.Focus();
        FindBox.SelectAll();
    }

    /// <summary>F3 / Shift+F3: opens the panel when there is nothing to search yet.</summary>
    public void Find(bool backwards)
    {
        if (FindBox.Text.Length == 0)
        {
            Open(replace: false);
            return;
        }

        if (!TryGetRegex(out var regex))
        {
            return;
        }

        var text = _editor.Document.Text;
        RunGuarded(() => Select(backwards
            ? TextSearch.FindPrevious(regex, text, _editor.SelectionStart)
            : TextSearch.FindNext(regex, text, _editor.SelectionStart + _editor.SelectionLength)));
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        _refreshTimer.Stop();
        SetMatches([]);
        _editor.TextArea.Focus();
    }

    private void AttachDocument()
    {
        if (_document is not null)
        {
            _document.TextChanged -= OnDocumentTextChanged;
        }

        _document = _editor.Document;
        if (_document is not null)
        {
            _document.TextChanged += OnDocumentTextChanged;
        }

        if (IsOpen)
        {
            Refresh();
        }
        else
        {
            SetMatches([]);
        }
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (IsOpen)
        {
            Schedule();
        }
    }

    private void OnSearchChanged(object sender, RoutedEventArgs e)
    {
        _jumpOnRefresh = true;
        Schedule();
    }

    private void Schedule()
    {
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    /// <summary>Recomputes the highlighted matches and the result count; jumps to the next match after a search change.</summary>
    private void Refresh()
    {
        _refreshTimer.Stop();
        FindBox.ClearValue(BorderBrushProperty);
        StatusText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.ForegroundMuted");
        if (FindBox.Text.Length == 0 || _editor.Document is null)
        {
            StatusText.Text = "";
            SetMatches([]);
            return;
        }

        if (!TryGetRegex(out var regex))
        {
            return;
        }

        RunGuarded(() =>
        {
            var text = _editor.Document.Text;
            var matches = TextSearch.FindAll(regex, text);
            SetMatches(matches);
            StatusText.Text = matches.Count switch
            {
                0 => "Aucun résultat",
                1 => "1 résultat",
                var n => $"{n} résultats",
            };

            if (_jumpOnRefresh)
            {
                _jumpOnRefresh = false;
                Select(TextSearch.FindNext(regex, text, _editor.SelectionStart));
            }
        });
    }

    private bool TryGetRegex(out Regex regex)
    {
        if (TextSearch.TryCreateRegex(FindBox.Text, MatchCaseToggle.IsChecked == true, WholeWordToggle.IsChecked == true, RegexToggle.IsChecked == true, out var created))
        {
            regex = created;
            return true;
        }

        regex = null!;
        ShowError("Expression invalide");
        return false;
    }

    /// <summary>Runs a search step; a regex too slow for the document shows an error instead of throwing.</summary>
    private void RunGuarded(Action action)
    {
        try
        {
            action();
        }
        catch (RegexMatchTimeoutException)
        {
            ShowError("Recherche trop longue");
        }
    }

    private void ShowError(string message)
    {
        SetMatches([]);
        FindBox.BorderBrush = ErrorBrush;
        StatusText.Foreground = ErrorBrush;
        StatusText.Text = message;
    }

    private void SetMatches(List<Match> matches)
    {
        _renderer.Matches = matches;
        _editor.TextArea.TextView.InvalidateLayer(_renderer.Layer);
    }

    private void Select(Match? match)
    {
        if (match is null)
        {
            return;
        }

        _editor.Select(match.Index, match.Length);
        var location = _editor.Document.GetLocation(match.Index);
        _editor.ScrollTo(location.Line, location.Column);
    }

    private void Replace()
    {
        if (_editor.IsReadOnly || !TryGetRegex(out var regex))
        {
            return;
        }

        RunGuarded(() =>
        {
            var useRegex = RegexToggle.IsChecked == true;
            var current = TextSearch.MatchAt(regex, _editor.Document.Text, _editor.SelectionStart, _editor.SelectionLength);
            if (current is not null)
            {
                var replacement = TextSearch.Expand(current, ReplaceBox.Text, useRegex);
                _editor.Document.Replace(current.Index, current.Length, replacement);
                _editor.Select(current.Index + replacement.Length, 0);
            }

            Select(TextSearch.FindNext(regex, _editor.Document.Text, _editor.SelectionStart));
        });
    }

    private void ReplaceAll()
    {
        if (_editor.IsReadOnly || !TryGetRegex(out var regex))
        {
            return;
        }

        RunGuarded(() =>
        {
            var document = _editor.Document;
            var edits = TextSearch.ReplaceAll(regex, document.Text, ReplaceBox.Text, RegexToggle.IsChecked == true);
            document.BeginUpdate();
            try
            {
                for (var i = edits.Count - 1; i >= 0; i--)
                {
                    document.Replace(edits[i].Offset, edits[i].Length, edits[i].Text);
                }
            }
            finally
            {
                document.EndUpdate();
            }

            Refresh();
            StatusText.Text = edits.Count == 1 ? "1 remplacement" : $"{edits.Count} remplacements";
        });
    }

    private void OnPanelPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnFindBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Find(backwards: Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        }
    }

    private void OnReplaceBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Replace();
        }
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e) => Find(backwards: true);

    private void OnNextClick(object sender, RoutedEventArgs e) => Find(backwards: false);

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnReplaceClick(object sender, RoutedEventArgs e) => Replace();

    private void OnReplaceAllClick(object sender, RoutedEventArgs e) => ReplaceAll();

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    /// <summary>Paints the matches that intersect the visible lines.</summary>
    private sealed class MatchRenderer : IBackgroundRenderer
    {
        public List<Match> Matches { get; set; } = [];

        public Brush Brush { get; set; } = LightHighlight;

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            var document = textView.Document;
            if (Matches.Count == 0 || document is null || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
            {
                return;
            }

            var visibleStart = textView.VisualLines[0].FirstDocumentLine.Offset;
            var visibleEnd = textView.VisualLines[^1].LastDocumentLine.EndOffset;
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 2 };

            // Matches are sorted and disjoint: binary-search the first one ending inside the visible range.
            int low = 0, high = Matches.Count;
            while (low < high)
            {
                var mid = (low + high) / 2;
                if (Matches[mid].Index + Matches[mid].Length <= visibleStart)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            for (var i = low; i < Matches.Count && Matches[i].Index <= visibleEnd; i++)
            {
                var match = Matches[i];
                // Matches can be stale (one refresh behind the text); skip those past the end of the document.
                if (match.Index + match.Length <= document.TextLength)
                {
                    builder.AddSegment(textView, new TextSegment { StartOffset = match.Index, Length = match.Length });
                }
            }

            var geometry = builder.CreateGeometry();
            if (geometry is not null)
            {
                drawingContext.DrawGeometry(Brush, null, geometry);
            }
        }
    }
}
