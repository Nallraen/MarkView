namespace MarkView.Models;

/// <summary>Decoded content of a text file plus the metadata needed to save it back.</summary>
/// <param name="Text">Decoded text, line endings untouched.</param>
/// <param name="EncodingName">Display name of the detected encoding (e.g. "UTF-8", "UTF-8 BOM", "UTF-16 LE").</param>
/// <param name="LineEnding">Dominant line ending of the file (CRLF when the file has none).</param>
public sealed record TextFileContent(string Text, string EncodingName, LineEnding LineEnding);
