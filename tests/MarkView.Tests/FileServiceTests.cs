using System.IO;
using System.Text;
using MarkView.Models;
using MarkView.Services;

namespace MarkView.Tests;

public sealed class FileServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("MarkViewTests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("utf-8", "UTF-8 BOM")]
    [InlineData("utf-16", "UTF-16 LE")]
    [InlineData("utf-16BE", "UTF-16 BE")]
    [InlineData("utf-32", "UTF-32 LE")]
    public void Decode_DetectsBom(string encodingName, string expectedLabel)
    {
        var encoding = Encoding.GetEncoding(encodingName);
        byte[] bytes = [.. encoding.GetPreamble(), .. encoding.GetBytes("# Été\r\n")];

        var content = FileService.Decode(bytes);

        Assert.Equal("# Été\r\n", content.Text);
        Assert.Equal(expectedLabel, content.EncodingName);
    }

    [Fact]
    public void Decode_PlainUtf8()
    {
        var content = FileService.Decode(Encoding.UTF8.GetBytes("café ✓"));

        Assert.Equal("café ✓", content.Text);
        Assert.Equal("UTF-8", content.EncodingName);
    }

    [Fact]
    public void Decode_InvalidUtf8_FallsBackToWindows1252()
    {
        // "café" in Windows-1252: 0xE9 alone is not valid UTF-8.
        var content = FileService.Decode([0x63, 0x61, 0x66, 0xE9]);

        Assert.Equal("café", content.Text);
        Assert.Equal("Windows-1252", content.EncodingName);
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", LineEnding.CrLf)]
    [InlineData("a\nb\nc", LineEnding.Lf)]
    [InlineData("a\nb\nc\r\nd", LineEnding.Lf)]
    [InlineData("a\r\nb\r\nc\nd", LineEnding.CrLf)]
    [InlineData("a\r\nb\n", LineEnding.CrLf)]
    [InlineData("no line break", LineEnding.CrLf)]
    [InlineData("", LineEnding.CrLf)]
    public void DetectLineEnding_PicksDominant(string text, LineEnding expected) =>
        Assert.Equal(expected, FileService.DetectLineEnding(text));

    [Fact]
    public void Decode_KeepsOriginalLineEndings()
    {
        var content = FileService.Decode(Encoding.UTF8.GetBytes("a\r\nb\nc"));

        Assert.Equal("a\r\nb\nc", content.Text);
    }

    [Theory]
    [InlineData("a\r\nb\nc\rd\f", LineEnding.Lf, "a\nb\nc\nd\f")]
    [InlineData("a\r\nb\nc\rd", LineEnding.CrLf, "a\r\nb\r\nc\r\nd")]
    [InlineData("\n\n", LineEnding.CrLf, "\r\n\r\n")]
    public void NormalizeLineEndings_ConvertsEveryBreak(string text, LineEnding lineEnding, string expected) =>
        Assert.Equal(expected, FileService.NormalizeLineEndings(text, lineEnding));

    [Theory]
    [InlineData(LineEnding.CrLf)]
    [InlineData(LineEnding.Lf)]
    public async Task WriteThenRead_RoundTrips(LineEnding lineEnding)
    {
        var service = new FileService();
        var path = Path.Combine(_dir, "doc.md");
        const string text = "# Titre\r\nÉté ✓\nfin";

        await service.WriteAsync(path, text, lineEnding);
        var content = await service.ReadAsync(path);

        Assert.Equal(FileService.NormalizeLineEndings(text, lineEnding), content.Text);
        Assert.Equal(lineEnding, content.LineEnding);
        Assert.Equal("UTF-8", content.EncodingName);
    }

    [Fact]
    public async Task Write_UsesUtf8WithoutBom_AndReplacesExistingFile()
    {
        var service = new FileService();
        var path = Path.Combine(_dir, "doc.md");
        await File.WriteAllTextAsync(path, "ancien contenu plus long", Encoding.Unicode);

        await service.WriteAsync(path, "é", LineEnding.Lf);

        Assert.Equal(new byte[] { 0xC3, 0xA9 }, await File.ReadAllBytesAsync(path));
        Assert.Equal(["doc.md"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }
}
