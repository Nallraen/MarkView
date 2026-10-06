using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MarkView.Services;

namespace MarkView.Tests;

public sealed class ExportServiceTests : IDisposable
{
    // Smallest valid PNG (1x1 transparent pixel).
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MarkView.Tests", Guid.NewGuid().ToString("N"));
    private readonly ExportService _service = new(new MarkdownRenderer());

    public ExportServiceTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void BuildStandaloneHtml_InlinesAssetsTitleAndBody()
    {
        var html = _service.BuildStandaloneHtml("# Bonjour\n\nTexte **gras**", "a <b> & c", null, embedImages: false);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        Assert.Contains("<title>a &lt;b&gt; &amp; c</title>", html);
        Assert.Contains(".markdown-body{color-scheme:light", html);
        Assert.Contains("max-width:980px", html);
        Assert.Contains("hljs.highlightAll();", html);
        Assert.Contains("<article class=\"markdown-body\">", html);
        Assert.Contains("<strong>gras</strong>", html);
        Assert.Contains("Bonjour</h1>", html);
        Assert.DoesNotContain("<link", html);
    }

    [Fact]
    public void BuildStandaloneHtml_OnlyNonceTaggedScriptsMayRun()
    {
        var html = _service.BuildStandaloneHtml("<img src=x onerror=\"alert(1)\">\n\n<script>alert(2)</script>", "t", null, embedImages: false);

        var nonce = Regex.Match(html, "script-src 'nonce-([0-9A-F]+)'").Groups[1].Value;
        Assert.NotEmpty(nonce);
        Assert.Equal(2, Regex.Matches(html, $"<script nonce=\"{nonce}\">").Count);
        Assert.DoesNotContain("unsafe-inline", Regex.Match(html, "script-src[^;]*").Value);
        Assert.Contains("<script>alert(2)</script>", html);
    }

    [Fact]
    public void BuildStandaloneHtml_WithoutEmbedding_KeepsRelativeImages()
    {
        File.WriteAllBytes(Path.Combine(_directory, "x.png"), Png);

        var html = _service.BuildStandaloneHtml("![alt](x.png)", "t", Path.Combine(_directory, "doc.md"), embedImages: false);

        Assert.Contains("src=\"x.png\"", html);
    }

    [Fact]
    public void BuildStandaloneHtml_WithEmbedding_TurnsLocalImagesIntoDataUris()
    {
        File.WriteAllBytes(Path.Combine(_directory, "x.png"), Png);

        var html = _service.BuildStandaloneHtml("![alt](x.png)\n\n![remote](https://example.com/y.png)\n\n![missing](nope.png)",
            "t", Path.Combine(_directory, "doc.md"), embedImages: true);

        Assert.Contains($"src=\"data:image/png;base64,{Convert.ToBase64String(Png)}\"", html);
        Assert.Contains("src=\"https://example.com/y.png\"", html);
        Assert.Contains("src=\"nope.png\"", html);
    }

    [Fact]
    public void EmbedImages_HandlesSingleQuotesAndSubfolders()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "img dir"));
        File.WriteAllBytes(Path.Combine(_directory, "img dir", "a.png"), Png);

        var html = ExportService.EmbedImages("<p><img alt='a' src='img%20dir/a.png'></p>", _directory);

        Assert.Equal($"<p><img alt='a' src='data:image/png;base64,{Convert.ToBase64String(Png)}'></p>", html);
    }

    [Fact]
    public void EmbedImages_UnsavedDocument_LeavesRelativeImages()
    {
        const string html = "<img src=\"x.png\">";

        Assert.Equal(html, ExportService.EmbedImages(html, baseDirectory: null));
    }

    [Fact]
    public void BuildCfHtml_OffsetsAreUtf8ByteOffsets()
    {
        const string fragment = "<p>Éléphant — 日本語 😀</p>";

        var cf = ExportService.BuildCfHtml(fragment);
        var bytes = Encoding.UTF8.GetBytes(cf);

        int Offset(string name) => int.Parse(Regex.Match(cf, name + @":(\d+)").Groups[1].Value);
        string Slice(int start, int end) => Encoding.UTF8.GetString(bytes, start, end - start);

        Assert.StartsWith("Version:0.9", cf);
        Assert.Equal(fragment, Slice(Offset("StartFragment"), Offset("EndFragment")));
        Assert.StartsWith("<html>", Slice(Offset("StartHTML"), Offset("EndHTML")));
        Assert.Equal(bytes.Length, Offset("EndHTML"));
        Assert.EndsWith("<!--StartFragment-->", Slice(0, Offset("StartFragment")));
    }
}
