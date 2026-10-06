using System.Globalization;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace MarkView.Services;

/// <summary>Markdig renderer tagging every block with its 0-based source line (<c>data-line</c>) for scroll sync.</summary>
public sealed class MarkdownRenderer : IMarkdownRenderer
{
    // Immutable once built, hence safe to share between threads.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePreciseSourceLocation()
        .Build();

    public string RenderBody(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var block in document.Descendants<Block>())
        {
            block.GetAttributes().AddPropertyIfNotExist("data-line", block.Line.ToString(CultureInfo.InvariantCulture));
        }

        return Markdown.ToHtml(document, Pipeline);
    }
}
