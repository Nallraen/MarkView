using MarkView.Models;
using MarkView.ViewModels;

namespace MarkView.Tests;

public class DocumentViewModelTests
{
    [Fact]
    public void Reload_ReplacesOnlyTheChangedPart_AndStaysClean()
    {
        var document = new DocumentViewModel(@"C:\x.md", new TextFileContent("abcXdef", "UTF-8", LineEnding.Lf));

        document.Reload(new TextFileContent("abcYYdef", "UTF-8", LineEnding.Lf));

        Assert.Equal("abcYYdef", document.Document.Text);
        Assert.False(document.IsDirty);
        Assert.True(document.Document.UndoStack.CanUndo);
    }

    [Fact]
    public void Editing_MarksDirty_AndDisplayNameGetsStar()
    {
        var document = new DocumentViewModel(@"C:\notes.md", new TextFileContent("a", "UTF-8", LineEnding.CrLf));

        document.Document.Insert(1, "b");

        Assert.True(document.IsDirty);
        Assert.Equal("notes.md*", document.DisplayName);
    }
}
