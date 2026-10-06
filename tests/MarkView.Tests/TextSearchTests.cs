using System.Text.RegularExpressions;
using MarkView.Services;

namespace MarkView.Tests;

public class TextSearchTests
{
    private static Regex Create(string pattern, bool matchCase = false, bool wholeWord = false, bool useRegex = false)
    {
        Assert.True(TextSearch.TryCreateRegex(pattern, matchCase, wholeWord, useRegex, out var regex));
        return regex;
    }

    [Fact]
    public void IgnoresCaseByDefault()
    {
        Assert.Equal(2, TextSearch.FindAll(Create("abc"), "ABC abc").Count);
        Assert.Single(TextSearch.FindAll(Create("abc", matchCase: true), "ABC abc"));
    }

    [Fact]
    public void WholeWord_SkipsPartialMatches()
    {
        var matches = TextSearch.FindAll(Create("cat", wholeWord: true), "cat concat cats cat.");
        Assert.Equal(new[] { 0, 16 }, matches.Select(m => m.Index));
    }

    [Fact]
    public void LiteralMode_EscapesRegexCharacters()
    {
        Assert.Single(TextSearch.FindAll(Create("a.b"), "a.b axb"));
        Assert.Equal(2, TextSearch.FindAll(Create("a.b", useRegex: true), "a.b axb").Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("(unclosed")]
    [InlineData("[a-")]
    public void InvalidOrEmptyPattern_ReturnsFalse(string pattern)
    {
        Assert.False(TextSearch.TryCreateRegex(pattern, false, false, useRegex: true, out _));
    }

    [Fact]
    public void FindNext_WrapsToStart()
    {
        var regex = Create("x");
        Assert.Equal(4, TextSearch.FindNext(regex, "x x x", 3)!.Index);
        Assert.Equal(0, TextSearch.FindNext(regex, "x x x", 5)!.Index);
    }

    [Fact]
    public void FindPrevious_WrapsToEnd()
    {
        var regex = Create("x");
        Assert.Equal(2, TextSearch.FindPrevious(regex, "x x x", 4)!.Index);
        Assert.Equal(4, TextSearch.FindPrevious(regex, "x x x", 0)!.Index);
    }

    [Fact]
    public void Find_ReturnsNullWithoutMatch()
    {
        Assert.Null(TextSearch.FindNext(Create("z"), "abc", 0));
        Assert.Null(TextSearch.FindPrevious(Create("z"), "abc", 0));
    }

    [Fact]
    public void ZeroLengthMatchesAreIgnored()
    {
        var regex = Create("^", useRegex: true);
        Assert.Empty(TextSearch.FindAll(regex, "a\nb"));
        Assert.Null(TextSearch.FindNext(regex, "a\nb", 0));
    }

    [Fact]
    public void MatchAt_OnlyAcceptsExactRange()
    {
        var regex = Create("ab");
        Assert.NotNull(TextSearch.MatchAt(regex, "xab", 1, 2));
        Assert.Null(TextSearch.MatchAt(regex, "xab", 0, 3));
        Assert.Null(TextSearch.MatchAt(regex, "xab", 1, 0));
    }

    [Fact]
    public void ReplaceAll_Literal()
    {
        var text = "a-b-c";
        Assert.Equal("a+b+c", ApplyAll(text, TextSearch.ReplaceAll(Create("-"), text, "+", useRegex: false)));
    }

    [Fact]
    public void ReplaceAll_LiteralKeepsDollarSigns()
    {
        var text = "price";
        Assert.Equal("$1", ApplyAll(text, TextSearch.ReplaceAll(Create("price"), text, "$1", useRegex: false)));
    }

    [Fact]
    public void ReplaceAll_RegexSubstitutesGroups()
    {
        var text = "John Smith, Jane Doe";
        var edits = TextSearch.ReplaceAll(Create(@"(\w+) (\w+)", useRegex: true), text, "$2 $1", useRegex: true);
        Assert.Equal(2, edits.Count);
        Assert.Equal("Smith John, Doe Jane", ApplyAll(text, edits));
    }

    private static string ApplyAll(string text, List<(int Offset, int Length, string Text)> edits)
    {
        for (var i = edits.Count - 1; i >= 0; i--)
        {
            text = text.Remove(edits[i].Offset, edits[i].Length).Insert(edits[i].Offset, edits[i].Text);
        }

        return text;
    }
}
