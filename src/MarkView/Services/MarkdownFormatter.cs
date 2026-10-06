using System.Text;
using System.Text.RegularExpressions;

namespace MarkView.Services;

/// <summary>
/// Replaces <see cref="Length"/> characters at <see cref="Offset"/> with <see cref="Replacement"/>, then selects
/// <see cref="SelectionLength"/> characters from <see cref="SelectionStart"/> (offsets in the edited text).
/// </summary>
public readonly record struct TextEdit(int Offset, int Length, string Replacement, int SelectionStart, int SelectionLength);

/// <summary>Line prefixes toggled by the list and quote commands.</summary>
public enum LinePrefix
{
    Bullet,
    Numbered,
    Task,
    Quote,
}

/// <summary>
/// Pure Markdown text transformations behind the formatting commands. Every function receives a text, the
/// selection inside it and returns the edit to apply, so the editor can run it as a single undo step.
/// </summary>
public static partial class MarkdownFormatter
{
    private const string TableTemplate =
        "| Colonne 1 | Colonne 2 | Colonne 3 |{0}| --------- | --------- | --------- |{0}| Cellule   | Cellule   | Cellule   |{0}| Cellule   | Cellule   | Cellule   |";

    [GeneratedRegex(@"^[ \t]*(?<marker>#{1,6}(?:[ \t]+|$))")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^[ \t]*(?<marker>(?:[-*+]|\d{1,9}[.)])[ \t]+(?:\[[ xX]\](?:[ \t]+|$))?)")]
    private static partial Regex ListRegex();

    [GeneratedRegex(@"^[ \t]*(?<marker>>[ \t]?)")]
    private static partial Regex QuoteRegex();

    [GeneratedRegex(@"^(?<prefix>[ \t]*(?:>[ \t]?)*[ \t]*)(?:(?<bullet>[-*+])(?<space>[ \t]+|$)(?<task>\[[ xX]\](?:[ \t]+|$))?|(?<number>\d{1,9})(?<delimiter>[.)])(?<space>[ \t]+|$))?")]
    private static partial Regex ContinuationRegex();

    /// <summary>Bold, italic, strikethrough, inline code: removes <paramref name="marker"/> around or at the edges of the selection, adds it otherwise.</summary>
    public static TextEdit ToggleWrap(string text, int selStart, int selLength, string marker)
    {
        var selEnd = selStart + selLength;
        var m = marker.Length;
        var c = marker[0];

        if (selLength >= 2 * m && HasMarker(RunAfter(text, selStart, c, selEnd), marker) && HasMarker(RunBefore(text, selEnd, c, selStart), marker))
        {
            var inner = text.Substring(selStart + m, selLength - 2 * m);
            return new TextEdit(selStart, selLength, inner, selStart, inner.Length);
        }

        if (HasMarker(RunBefore(text, selStart, c, 0), marker) && HasMarker(RunAfter(text, selEnd, c, text.Length), marker))
        {
            var inner = text.Substring(selStart, selLength);
            return new TextEdit(selStart - m, selLength + 2 * m, inner, selStart - m, selLength);
        }

        return new TextEdit(selStart, selLength, marker + text.Substring(selStart, selLength) + marker, selStart + m, selLength);
    }

    /// <summary>Sets heading <paramref name="level"/> on the touched lines (same level again removes it).</summary>
    public static TextEdit ToggleHeading(string text, int selStart, int selLength, int level)
    {
        var target = new string('#', level) + " ";
        return TransformLines(text, selStart, selLength, HeadingRegex(),
            existing => existing.TrimEnd().Length == level,
            _ => target);
    }

    /// <summary>Toggles a list or quote prefix on every touched line, replacing another list type's marker.</summary>
    public static TextEdit ToggleLinePrefix(string text, int selStart, int selLength, LinePrefix prefix)
    {
        return prefix switch
        {
            LinePrefix.Bullet => TransformLines(text, selStart, selLength, ListRegex(),
                existing => !char.IsDigit(existing[0]) && !existing.Contains('['), _ => "- "),
            LinePrefix.Numbered => TransformLines(text, selStart, selLength, ListRegex(),
                existing => char.IsDigit(existing[0]), index => $"{index + 1}. "),
            LinePrefix.Task => TransformLines(text, selStart, selLength, ListRegex(),
                existing => existing.Contains('['), _ => "- [ ] "),
            _ => TransformLines(text, selStart, selLength, QuoteRegex(), _ => true, _ => "> "),
        };
    }

    /// <summary><c>[selection](url)</c> with "url" selected, or <c>[](url)</c> with the caret between the brackets.</summary>
    public static TextEdit InsertLink(string text, int selStart, int selLength, bool isImage)
    {
        var opening = isImage ? "![" : "[";
        var target = isImage ? "chemin" : "url";
        var label = text.Substring(selStart, selLength);
        var replacement = $"{opening}{label}]({target})";
        return selLength == 0
            ? new TextEdit(selStart, 0, replacement, selStart + opening.Length, 0)
            : new TextEdit(selStart, selLength, replacement, selStart + opening.Length + label.Length + 2, target.Length);
    }

    /// <summary>Fences the touched lines with <c>```</c> (caret after the opening fence), or inserts an empty block on a blank line.</summary>
    public static TextEdit InsertCodeBlock(string text, int selStart, int selLength, string newline)
    {
        var lines = TouchedLines(text, selStart, selLength);
        var (start, _) = lines[0];
        var (_, end) = lines[^1];
        if (lines.Count == 1 && string.IsNullOrWhiteSpace(text[start..end]))
        {
            var fence = "```" + newline;
            return InsertBlock(text, selStart, selLength, fence + newline + "```", fence.Length, 0, newline);
        }

        var replacement = "```" + newline + text[start..end] + newline + "```";
        return new TextEdit(start, end - start, replacement, start + 3, 0);
    }

    /// <summary>Inserts a 3-column table template on its own lines, its first header cell selected.</summary>
    public static TextEdit InsertTable(string text, int selStart, int selLength, string newline) =>
        InsertBlock(text, selStart, selLength, string.Format(TableTemplate, newline), 2, "Colonne 1".Length, newline);

    /// <summary>Inserts <c>---</c> on its own line.</summary>
    public static TextEdit InsertHorizontalRule(string text, int selStart, int selLength, string newline) =>
        InsertBlock(text, selStart, selLength, "---", 3, 0, newline);

    /// <summary>
    /// Enter on a list item or quote line: returns the edit continuing it (next number, unchecked task box) or,
    /// on an empty item, removing its marker. Null when Enter should behave normally. Offsets are relative to <paramref name="line"/>.
    /// </summary>
    public static TextEdit? ContinueList(string line, int caret, string newline)
    {
        var match = ContinuationRegex().Match(line);
        var prefix = match.Groups["prefix"].Value;
        var bullet = match.Groups["bullet"];
        var number = match.Groups["number"];
        var hasQuote = prefix.Contains('>');
        if ((!hasQuote && !bullet.Success && !number.Success) || caret < match.Length)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(line[match.Length..]))
        {
            var kept = bullet.Success || number.Success ? (hasQuote ? prefix : "") : "";
            return new TextEdit(0, line.Length, kept, kept.Length, 0);
        }

        var space = match.Groups["space"].Value.Length == 0 ? " " : match.Groups["space"].Value;
        var marker = bullet.Success
            ? bullet.Value + space + (match.Groups["task"].Success ? "[ ] " : "")
            : number.Success
                ? (long.Parse(number.Value) + 1).ToString().PadLeft(number.Value.Length, '0') + match.Groups["delimiter"].Value + space
                : "";
        var insertion = newline + prefix + marker;
        return new TextEdit(caret, 0, insertion, caret + insertion.Length, 0);
    }

    private static bool HasMarker(int run, string marker) => marker switch
    {
        // "*" is italic only for an odd run (1 or 3 = bold italic); "**" is bold for 2 or 3.
        "*" => run is 1 or 3,
        "**" => run is 2 or 3,
        _ => run == marker.Length,
    };

    private static int RunBefore(string text, int end, char c, int limit)
    {
        var i = end;
        while (i > limit && text[i - 1] == c)
        {
            i--;
        }

        return end - i;
    }

    private static int RunAfter(string text, int start, char c, int limit)
    {
        var i = start;
        while (i < limit && text[i] == c)
        {
            i++;
        }

        return i - start;
    }

    /// <summary>
    /// Replaces the prefix family matched by <paramref name="family"/> on every touched line (blank lines are skipped
    /// in multi-line selections). Removes the prefix instead when every line already has it (<paramref name="isTarget"/>).
    /// </summary>
    private static TextEdit TransformLines(string text, int selStart, int selLength, Regex family, Func<string, bool> isTarget, Func<int, string> newMarker)
    {
        var lines = TouchedLines(text, selStart, selLength);
        var skipBlank = lines.Count > 1;
        // Null marks a skipped blank line.
        var markers = lines
            .Select(l => skipBlank && string.IsNullOrWhiteSpace(text[l.Start..l.End]) ? null : family.Match(text, l.Start, l.End - l.Start).Groups["marker"])
            .ToList();
        var remove = markers.All(g => g is null || (g.Success && isTarget(g.Value)));

        var builder = new StringBuilder();
        var cursor = lines[0].Start;
        var delta = 0;
        var index = 0;
        int newStart = selStart, newEnd = selStart + selLength;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var group = markers[i];
            var found = group is { Success: true };
            var markerStart = found ? group!.Index : line.Start + IndentLength(text, line.Start, line.End);
            var markerEnd = found ? group!.Index + group.Length : markerStart;
            var replacement = group is null || remove ? "" : newMarker(index++);

            builder.Append(text, cursor, markerStart - cursor).Append(replacement);
            cursor = markerEnd;
            newStart = MapOffset(selStart, newStart, line, markerStart, markerEnd, replacement.Length, delta);
            newEnd = MapOffset(selStart + selLength, newEnd, line, markerStart, markerEnd, replacement.Length, delta);
            delta += replacement.Length - (markerEnd - markerStart);
        }

        var end = lines[^1].End;
        builder.Append(text, cursor, end - cursor);
        if (selStart + selLength > end)
        {
            newEnd = selStart + selLength + delta;
        }

        return new TextEdit(lines[0].Start, end - lines[0].Start, builder.ToString(), newStart, Math.Max(0, newEnd - newStart));
    }

    /// <summary>Maps <paramref name="offset"/> when it lies on <paramref name="line"/>; returns <paramref name="current"/> otherwise.</summary>
    private static int MapOffset(int offset, int current, (int Start, int End) line, int markerStart, int markerEnd, int newLength, int delta)
    {
        if (offset < line.Start || offset > line.End)
        {
            return current;
        }

        return delta + (offset < markerStart ? offset : offset < markerEnd ? markerStart + newLength : offset + newLength - (markerEnd - markerStart));
    }

    /// <summary>Puts <paramref name="block"/> on its own lines, after the caret line unless it is blank, with blank lines around it.</summary>
    private static TextEdit InsertBlock(string text, int selStart, int selLength, string block, int selectInBlock, int selectLength, string newline)
    {
        var start = LineStart(text, selStart + selLength);
        var end = LineEnd(text, start);
        var blank = string.IsNullOrWhiteSpace(text[start..end]);
        var previousEnd = start > 1 && text[start - 1] == '\n' && text[start - 2] == '\r' ? start - 2 : start - 1;
        var previousBlank = start == 0 || string.IsNullOrWhiteSpace(text[LineStart(text, previousEnd)..previousEnd]);
        var nextStart = end < text.Length ? NextLineStart(text, end) : -1;
        var nextBlank = nextStart < 0 || string.IsNullOrWhiteSpace(text[nextStart..LineEnd(text, nextStart)]);

        var before = blank ? (previousBlank ? "" : newline) : newline + newline;
        var replacement = before + block + (nextBlank ? "" : newline);
        var offset = blank ? start : end;
        return new TextEdit(offset, blank ? end - start : 0, replacement, offset + before.Length + selectInBlock, selectLength);
    }

    /// <summary>Lines (start, end without line break) touched by the selection; a selection ending at a line start does not touch that line.</summary>
    private static List<(int Start, int End)> TouchedLines(string text, int selStart, int selLength)
    {
        var lines = new List<(int Start, int End)>();
        var start = LineStart(text, selStart);
        while (true)
        {
            var end = LineEnd(text, start);
            lines.Add((start, end));
            if (end >= text.Length)
            {
                return lines;
            }

            start = NextLineStart(text, end);
            if (start >= selStart + selLength)
            {
                return lines;
            }
        }
    }

    private static int IndentLength(string text, int start, int end)
    {
        var i = start;
        while (i < end && text[i] is ' ' or '\t')
        {
            i++;
        }

        return i - start;
    }

    private static int LineStart(string text, int offset)
    {
        while (offset > 0 && text[offset - 1] is not ('\n' or '\r'))
        {
            offset--;
        }

        return offset;
    }

    private static int LineEnd(string text, int offset)
    {
        while (offset < text.Length && text[offset] is not ('\n' or '\r'))
        {
            offset++;
        }

        return offset;
    }

    private static int NextLineStart(string text, int lineEnd) =>
        text[lineEnd] == '\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n' ? lineEnd + 2 : lineEnd + 1;
}
