using MarkView.ViewModels;

namespace MarkView.Tests;

public class StatusCountTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("un", 1)]
    [InlineData("un deux\r\ntrois\tquatre", 4)]
    [InlineData("  **gras** et `code`  ", 3)]
    public void CountWords_SplitsOnWhitespace(string text, int expected) =>
        Assert.Equal(expected, MainViewModel.CountWords(text));

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("ab\r\ncd\nef", 6)]
    [InlineData("é è", 3)]
    public void CountChars_IgnoresLineBreaks(string text, int expected) =>
        Assert.Equal(expected, MainViewModel.CountChars(text));
}
