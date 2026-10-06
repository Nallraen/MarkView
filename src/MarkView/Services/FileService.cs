using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MarkView.Models;

namespace MarkView.Services;

/// <summary>Text file I/O: BOM / UTF-8 / Windows-1252 detection on read, UTF-8 without BOM and safe replace on write.</summary>
public sealed partial class FileService : IFileService
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly Encoding Windows1252;

    static FileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1252 = Encoding.GetEncoding(1252);
    }

    public async Task<TextFileContent> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        // Share everything: the file may still be open in the program that just changed it.
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return Decode(buffer.ToArray());
    }

    public async Task WriteAsync(string path, string text, LineEnding lineEnding, CancellationToken cancellationToken = default)
    {
        var bytes = Utf8NoBom.GetBytes(NormalizeLineEndings(text, lineEnding));
        var fullPath = Path.GetFullPath(path);
        var tempPath = Path.Combine(Path.GetDirectoryName(fullPath)!, $"{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        // Write next to the target then swap it in, so a crash mid-save never leaves a truncated original.
        try
        {
            await WriteBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            if (File.Exists(fullPath))
            {
                File.Replace(tempPath, fullPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }

            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // E.g. no permission to create files in the folder while the file itself is writable.
            Log.Error(ex, $"Safe save of {fullPath} failed, falling back to a direct write");
        }
        finally
        {
            TryDelete(tempPath);
        }

        await WriteBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Decodes raw file bytes: BOM first, then strict UTF-8, then Windows-1252.</summary>
    public static TextFileContent Decode(byte[] bytes)
    {
        var span = bytes.AsSpan();
        (Encoding? encoding, int bomLength, string name) = span switch
        {
            [0xEF, 0xBB, 0xBF, ..] => (Utf8NoBom, 3, "UTF-8 BOM"),
            [0xFF, 0xFE, 0x00, 0x00, ..] => (Encoding.UTF32, 4, "UTF-32 LE"),
            [0xFF, 0xFE, ..] => (Encoding.Unicode, 2, "UTF-16 LE"),
            [0xFE, 0xFF, ..] => (Encoding.BigEndianUnicode, 2, "UTF-16 BE"),
            _ => (null, 0, "UTF-8"),
        };

        string text;
        if (encoding is not null)
        {
            text = encoding.GetString(span[bomLength..]);
        }
        else
        {
            try
            {
                text = StrictUtf8.GetString(span);
            }
            catch (DecoderFallbackException)
            {
                text = Windows1252.GetString(span);
                name = "Windows-1252";
            }
        }

        return new TextFileContent(text, name, DetectLineEnding(text));
    }

    /// <summary>Dominant line ending: LF when lone LFs outnumber CRLFs, CRLF otherwise (including text without any line break).</summary>
    public static LineEnding DetectLineEnding(string text)
    {
        int crlf = 0, lf = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n')
            {
                continue;
            }

            if (i > 0 && text[i - 1] == '\r')
            {
                crlf++;
            }
            else
            {
                lf++;
            }
        }

        return lf > crlf ? LineEnding.Lf : LineEnding.CrLf;
    }

    /// <summary>Converts every CRLF, CR and LF to <paramref name="lineEnding"/>.</summary>
    public static string NormalizeLineEndings(string text, LineEnding lineEnding) =>
        LineBreakRegex().Replace(text, lineEnding == LineEnding.Lf ? "\n" : "\r\n");

    // Not string.ReplaceLineEndings: it also rewrites form feeds and Unicode separators.
    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreakRegex();

    private static async Task WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, $"Cannot delete temporary file {path}");
        }
    }
}
