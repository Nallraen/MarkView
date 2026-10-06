using MarkView.Services;

namespace MarkView.Tests;

public class MarkdownFormatterTests
{
    /// <summary>Applies the edit; returns the new text with the selection shown as [ ] (or | for a caret).</summary>
    private static string Apply(string text, TextEdit edit)
    {
        var result = text.Remove(edit.Offset, edit.Length).Insert(edit.Offset, edit.Replacement);
        return edit.SelectionLength == 0
            ? result.Insert(edit.SelectionStart, "|")
            : result.Insert(edit.SelectionStart + edit.SelectionLength, "]").Insert(edit.SelectionStart, "[");
    }

    [Fact]
    public void Wrap_AddsMarkersAroundSelection()
    {
        Assert.Equal("a **[word]** b", Apply("a word b", MarkdownFormatter.ToggleWrap("a word b", 2, 4, "**")));
    }

    [Fact]
    public void Wrap_WithoutSelection_PutsCaretBetweenMarkers()
    {
        Assert.Equal("a ~~|~~b", Apply("a b", MarkdownFormatter.ToggleWrap("a b", 2, 0, "~~")));
    }

    [Fact]
    public void Wrap_RemovesMarkersAroundSelection()
    {
        Assert.Equal("a [word] b", Apply("a **word** b", MarkdownFormatter.ToggleWrap("a **word** b", 4, 4, "**")));
    }

    [Fact]
    public void Wrap_RemovesMarkersInsideSelection()
    {
        Assert.Equal("a [code] b", Apply("a `code` b", MarkdownFormatter.ToggleWrap("a `code` b", 2, 6, "`")));
    }

    [Fact]
    public void Wrap_RemovesEmptyPairAroundCaret()
    {
        Assert.Equal("a |b", Apply("a ****b", MarkdownFormatter.ToggleWrap("a ****b", 4, 0, "**")));
    }

    [Fact]
    public void Italic_OnBoldText_AddsItalicInsteadOfBreakingBold()
    {
        Assert.Equal("***[word]***", Apply("**word**", MarkdownFormatter.ToggleWrap("**word**", 2, 4, "*")));
    }

    [Fact]
    public void Italic_OnBoldItalicText_RemovesOnlyItalic()
    {
        Assert.Equal("**[word]**", Apply("***word***", MarkdownFormatter.ToggleWrap("***word***", 3, 4, "*")));
    }

    [Theory]
    [InlineData("Title", 1, "# |Title")]
    [InlineData("## Title", 1, "# |Title")]
    [InlineData("# Title", 3, "### |Title")]
    [InlineData("## Title", 2, "|Title")]
    public void Heading_SetsReplacesOrRemovesLevel(string text, int level, string expected)
    {
        Assert.Equal(expected, Apply(text, MarkdownFormatter.ToggleHeading(text, 0, 0, level)));
    }

    [Fact]
    public void Heading_KeepsCaretInText()
    {
        Assert.Equal("## Ti|tle", Apply("Title", MarkdownFormatter.ToggleHeading("Title", 2, 0, 2)));
    }

    [Fact]
    public void Bullet_PrefixesEveryTouchedLine()
    {
        var text = "one\ntwo\nthree";
        Assert.Equal("- [one\n- two]\nthree", Apply(text, MarkdownFormatter.ToggleLinePrefix(text, 0, 7, LinePrefix.Bullet)));
    }

    [Fact]
    public void Bullet_SelectionEndingAtLineStart_DoesNotTouchThatLine()
    {
        var text = "one\ntwo";
        Assert.Equal("- [one\n]two", Apply(text, MarkdownFormatter.ToggleLinePrefix(text, 0, 4, LinePrefix.Bullet)));
    }

    [Fact]
    public void Bullet_OnEmptyLine_PutsCaretAfterMarker()
    {
        Assert.Equal("- |", Apply("", MarkdownFormatter.ToggleLinePrefix("", 0, 0, LinePrefix.Bullet)));
    }

    [Fact]
    public void Bullet_TogglesOffWhenAllLinesHaveIt()
    {
        var text = "- one\n- two";
        Assert.Equal("[one\ntwo]", Apply(text, MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, LinePrefix.Bullet)));
    }

    [Fact]
    public void Bullet_SkipsBlankLinesAndKeepsIndentation()
    {
        var text = "one\n\n  two";
        Assert.Equal("- [one\n\n  - two]", Apply(text, MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, LinePrefix.Bullet)));
    }

    [Fact]
    public void Numbered_NumbersLinesAndReplacesBullets()
    {
        var text = "- a\n* b\nc";
        Assert.Equal("1. [a\n2. b\n3. c]", Apply(text, MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, LinePrefix.Numbered)));
    }

    [Fact]
    public void Numbered_TogglesOff()
    {
        var text = "1. a\n2. b";
        Assert.Equal("[a\nb]", Apply(text, MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, LinePrefix.Numbered)));
    }

    [Fact]
    public void Task_ReplacesNumberAndTogglesOff()
    {
        Assert.Equal("- [ ] |a", Apply("1. a", MarkdownFormatter.ToggleLinePrefix("1. a", 3, 0, LinePrefix.Task)));
        Assert.Equal("|a", Apply("- [x] a", MarkdownFormatter.ToggleLinePrefix("- [x] a", 6, 0, LinePrefix.Task)));
    }

    [Fact]
    public void Bullet_OnTaskLine_ReplacesTaskBox()
    {
        Assert.Equal("- |a", Apply("- [ ] a", MarkdownFormatter.ToggleLinePrefix("- [ ] a", 6, 0, LinePrefix.Bullet)));
    }

    [Fact]
    public void Quote_TogglesOnAndOff()
    {
        var text = "a\r\nb";
        Assert.Equal("> [a\r\n> b]", Apply(text, MarkdownFormatter.ToggleLinePrefix(text, 0, text.Length, LinePrefix.Quote)));
        var quoted = "> a\r\n> b";
        Assert.Equal("[a\r\nb]", Apply(quoted, MarkdownFormatter.ToggleLinePrefix(quoted, 0, quoted.Length, LinePrefix.Quote)));
    }

    [Fact]
    public void Link_WrapsSelectionAndSelectsUrl()
    {
        Assert.Equal("see [docs]([url])", Apply("see docs", MarkdownFormatter.InsertLink("see docs", 4, 4, isImage: false)));
    }

    [Fact]
    public void Link_WithoutSelection_PutsCaretBetweenBrackets()
    {
        Assert.Equal("[|](url)", Apply("", MarkdownFormatter.InsertLink("", 0, 0, isImage: false)));
    }

    [Fact]
    public void Image_UsesSelectionAsAltAndSelectsPath()
    {
        Assert.Equal("![logo]([chemin])", Apply("logo", MarkdownFormatter.InsertLink("logo", 0, 4, isImage: true)));
        Assert.Equal("![|](chemin)", Apply("", MarkdownFormatter.InsertLink("", 0, 0, isImage: true)));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void CodeBlock_FencesSelectedLines(string nl)
    {
        var text = $"a{nl}b";
        Assert.Equal($"```|{nl}a{nl}b{nl}```", Apply(text, MarkdownFormatter.InsertCodeBlock(text, 0, text.Length, nl)));
    }

    [Fact]
    public void CodeBlock_OnBlankLine_PutsCaretInside()
    {
        Assert.Equal("```\n|\n```", Apply("", MarkdownFormatter.InsertCodeBlock("", 0, 0, "\n")));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void HorizontalRule_AfterTextLine_AddsBlankLineSoItIsNotASetextHeading(string nl)
    {
        Assert.Equal($"text{nl}{nl}---|", Apply("text", MarkdownFormatter.InsertHorizontalRule("text", 4, 0, nl)));
    }

    [Fact]
    public void HorizontalRule_OnBlankLineBetweenParagraphs_KeepsBlankLinesAround()
    {
        var text = "a\r\n\r\nb";
        Assert.Equal("a\r\n\r\n---|\r\n\r\nb", Apply(text, MarkdownFormatter.InsertHorizontalRule(text, 3, 0, "\r\n")));
    }

    [Fact]
    public void Table_InsertsTemplateAndSelectsFirstHeader()
    {
        var result = Apply("", MarkdownFormatter.InsertTable("", 0, 0, "\n"));
        Assert.StartsWith("| [Colonne 1] | Colonne 2 | Colonne 3 |\n| ---", result);
        Assert.Equal(4, result.Split('\n').Length);
    }

    [Theory]
    [InlineData("- item", "- item\n- |")]
    [InlineData("* item", "* item\n* |")]
    [InlineData("  + item", "  + item\n  + |")]
    [InlineData("1. item", "1. item\n2. |")]
    [InlineData("9) item", "9) item\n10) |")]
    [InlineData("- [x] done", "- [x] done\n- [ ] |")]
    [InlineData("> quote", "> quote\n> |")]
    [InlineData("> - item", "> - item\n> - |")]
    public void ContinueList_InsertsNextMarker(string line, string expected)
    {
        var edit = MarkdownFormatter.ContinueList(line, line.Length, "\n");
        Assert.NotNull(edit);
        Assert.Equal(expected, Apply(line, edit.Value));
    }

    [Fact]
    public void ContinueList_InMiddleOfItem_MovesRestToNextItem()
    {
        var edit = MarkdownFormatter.ContinueList("- ab", 3, "\r\n");
        Assert.Equal("- a\r\n- |b", Apply("- ab", edit!.Value));
    }

    [Theory]
    [InlineData("- ", "|")]
    [InlineData("  1. ", "|")]
    [InlineData("- [ ] ", "|")]
    [InlineData("> ", "|")]
    [InlineData("> - ", "> |")]
    public void ContinueList_OnEmptyItem_EndsList(string line, string expected)
    {
        var edit = MarkdownFormatter.ContinueList(line, line.Length, "\n");
        Assert.Equal(expected, Apply(line, edit!.Value));
    }

    [Theory]
    [InlineData("plain text", 10)]
    [InlineData("**bold**", 8)]
    [InlineData("- item", 1)]
    [InlineData("---", 3)]
    public void ContinueList_ReturnsNullWhenEnterIsNormal(string line, int caret)
    {
        Assert.Null(MarkdownFormatter.ContinueList(line, caret, "\n"));
    }
}
