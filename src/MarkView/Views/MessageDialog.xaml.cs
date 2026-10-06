using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace MarkView.Views;

/// <summary>Themed replacement for MessageBox: the first button is the default (accent) one.</summary>
public partial class MessageDialog : Window
{
    public enum Kind
    {
        Info,
        Question,
        Warning,
        Error,
    }

    public MessageDialog(string message, Kind kind, IReadOnlyList<string> buttons, int cancelIndex)
    {
        InitializeComponent();
        Result = cancelIndex;
        MessageText.Text = message;

        var (glyph, brushKey) = kind switch
        {
            Kind.Error => ("", "Brush.Error"),
            Kind.Warning => ("", "Brush.Warning"),
            Kind.Question => ("", "Brush.Accent"),
            _ => ("", "Brush.Accent"),
        };
        IconGlyph.Text = glyph;
        IconGlyph.SetResourceReference(TextElement.ForegroundProperty, brushKey);

        for (var i = 0; i < buttons.Count; i++)
        {
            var index = i;
            var button = new Button { Content = buttons[i], MinWidth = 96, Margin = new Thickness(8, 0, 0, 0), IsDefault = i == 0 };
            if (i == 0)
            {
                button.SetResourceReference(StyleProperty, "Button.Accent");
            }

            button.Click += (_, _) =>
            {
                Result = index;
                Close();
            };
            ButtonPanel.Children.Add(button);
        }

        Loaded += (_, _) => ((Button)ButtonPanel.Children[0]).Focus();
    }

    /// <summary>Index of the clicked button; the cancel index when closed with Esc or the title bar.</summary>
    public int Result { get; private set; }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        base.OnPreviewKeyDown(e);
    }
}
