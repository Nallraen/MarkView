using System.Windows.Input;

namespace MarkView.Controls;

/// <summary>
/// Routed commands handled by <see cref="EditorControl"/> (CommandBindings). Key gestures are declared
/// as KeyBindings on the main window, so no gesture is attached here.
/// </summary>
public static class EditorCommands
{
    public static RoutedUICommand Bold { get; } = Create("Gras", nameof(Bold));
    public static RoutedUICommand Italic { get; } = Create("Italique", nameof(Italic));
    public static RoutedUICommand Strikethrough { get; } = Create("Barré", nameof(Strikethrough));
    public static RoutedUICommand InlineCode { get; } = Create("Code inline", nameof(InlineCode));
    public static RoutedUICommand Heading1 { get; } = Create("Titre 1", nameof(Heading1));
    public static RoutedUICommand Heading2 { get; } = Create("Titre 2", nameof(Heading2));
    public static RoutedUICommand Heading3 { get; } = Create("Titre 3", nameof(Heading3));
    public static RoutedUICommand BulletList { get; } = Create("Liste à puces", nameof(BulletList));
    public static RoutedUICommand NumberedList { get; } = Create("Liste numérotée", nameof(NumberedList));
    public static RoutedUICommand TaskList { get; } = Create("Case à cocher", nameof(TaskList));
    public static RoutedUICommand Quote { get; } = Create("Citation", nameof(Quote));
    public static RoutedUICommand Link { get; } = Create("Lien", nameof(Link));
    public static RoutedUICommand Image { get; } = Create("Image", nameof(Image));
    public static RoutedUICommand CodeBlock { get; } = Create("Bloc de code", nameof(CodeBlock));
    public static RoutedUICommand Table { get; } = Create("Tableau", nameof(Table));
    public static RoutedUICommand HorizontalRule { get; } = Create("Ligne horizontale", nameof(HorizontalRule));

    private static RoutedUICommand Create(string text, string name) => new(text, name, typeof(EditorCommands));
}
