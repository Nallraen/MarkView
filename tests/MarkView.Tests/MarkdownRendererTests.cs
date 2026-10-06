using MarkView.Services;

namespace MarkView.Tests;

public class MarkdownRendererTests
{
    private readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void RendersPipeTable()
    {
        var html = _renderer.RenderBody("| A | B |\n|---|---|\n| 1 | 2 |\n");

        Assert.Contains("<table", html);
        Assert.Contains("<th", html);
        Assert.Contains(">2</td>", html);
    }

    [Fact]
    public void RendersTaskList()
    {
        var html = _renderer.RenderBody("- [ ] todo\n- [x] done\n");

        Assert.Contains("type=\"checkbox\"", html);
        Assert.Contains("checked=\"checked\"", html);
    }

    [Fact]
    public void FencedCodeCarriesLanguageClass()
    {
        var html = _renderer.RenderBody("```csharp\nvar x = 1;\n```\n");

        Assert.Contains("<pre><code class=\"language-csharp\"", html);
        Assert.Contains("var x = 1;", html);
    }

    [Fact]
    public void RendersFootnotes()
    {
        var html = _renderer.RenderBody("Text[^1].\n\n[^1]: The note.\n");

        Assert.Contains("class=\"footnote-ref\"", html);
        Assert.Contains("The note.", html);
    }

    [Fact]
    public void BlocksCarryZeroBasedSourceLines()
    {
        var html = _renderer.RenderBody("# Title\n\nParagraph\n\n- one\n- two\n");

        Assert.Contains("<h1 id=\"title\" data-line=\"0\">", html);
        Assert.Contains("<p data-line=\"2\">", html);
        Assert.Contains("<ul data-line=\"4\">", html);
        Assert.Contains("<li data-line=\"4\">", html);
        Assert.Contains("<li data-line=\"5\">", html);
    }

    [Fact]
    public void PreservesRawHtml()
    {
        var html = _renderer.RenderBody("<div class=\"note\">raw <b>html</b></div>\n");

        Assert.Contains("<div class=\"note\">raw <b>html</b></div>", html);
    }
}
