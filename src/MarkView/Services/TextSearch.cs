using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace MarkView.Services;

/// <summary>Pure find/replace helpers used by the editor's find panel. Zero-length matches are ignored.</summary>
public static class TextSearch
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Builds the search regex; false when the pattern is empty or not a valid regular expression.</summary>
    public static bool TryCreateRegex(string pattern, bool matchCase, bool wholeWord, bool useRegex, [NotNullWhen(true)] out Regex? regex)
    {
        regex = null;
        if (pattern.Length == 0)
        {
            return false;
        }

        var body = useRegex ? pattern : Regex.Escape(pattern);
        if (wholeWord)
        {
            body = $@"(?<!\w)(?:{body})(?!\w)";
        }

        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant | (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            regex = new Regex(body, options, MatchTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>First match starting at or after <paramref name="offset"/>, wrapping to the start of the text.</summary>
    public static Match? FindNext(Regex regex, string text, int offset) =>
        FirstNonEmpty(regex.Match(text, Math.Clamp(offset, 0, text.Length))) ?? FirstNonEmpty(regex.Match(text));

    /// <summary>Last match starting before <paramref name="offset"/>, wrapping to the end of the text.</summary>
    public static Match? FindPrevious(Regex regex, string text, int offset)
    {
        var matches = FindAll(regex, text);
        var before = matches.LastOrDefault(m => m.Index < offset);
        return before ?? matches.LastOrDefault();
    }

    /// <summary>All non-empty matches, in document order.</summary>
    public static List<Match> FindAll(Regex regex, string text) => regex.Matches(text).Where(m => m.Length > 0).ToList();

    /// <summary>The match covering exactly [offset, offset + length), if any (used to replace the current selection).</summary>
    public static Match? MatchAt(Regex regex, string text, int offset, int length)
    {
        if (length == 0 || offset + length > text.Length)
        {
            return null;
        }

        var match = regex.Match(text, offset);
        return match.Success && match.Index == offset && match.Length == length ? match : null;
    }

    /// <summary>Replacement text for <paramref name="match"/>: <c>$1</c>-style substitutions in regex mode, literal otherwise.</summary>
    public static string Expand(Match match, string replacement, bool useRegex) => useRegex ? match.Result(replacement) : replacement;

    /// <summary>Edits replacing every match, in document order (apply them from the last one to keep offsets valid).</summary>
    public static List<(int Offset, int Length, string Text)> ReplaceAll(Regex regex, string text, string replacement, bool useRegex) =>
        FindAll(regex, text).Select(m => (m.Index, m.Length, Expand(m, replacement, useRegex))).ToList();

    private static Match? FirstNonEmpty(Match match)
    {
        while (match.Success && match.Length == 0)
        {
            match = match.NextMatch();
        }

        return match.Success ? match : null;
    }
}
