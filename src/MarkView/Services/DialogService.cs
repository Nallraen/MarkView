using System.Windows;
using Microsoft.Win32;
using MarkView.Views;

namespace MarkView.Services;

/// <summary>Modal UI: themed message dialogs, file dialogs and the settings window.</summary>
public sealed class DialogService(Func<SettingsWindow> settingsWindowFactory, IThemeService themeService) : IDialogService
{
    private const string MarkdownFilter =
        "Fichiers Markdown (*.md;*.markdown;*.mdown;*.txt)|*.md;*.markdown;*.mdown;*.txt|Tous les fichiers (*.*)|*.*";

    public string[]? ShowOpenMarkdownDialog()
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = MarkdownFilter };
        return dialog.ShowDialog(Owner) == true ? dialog.FileNames : null;
    }

    public string? ShowSaveDialog(string suggestedFileName, string filter, string defaultExtension)
    {
        var dialog = new SaveFileDialog { FileName = suggestedFileName, Filter = filter, DefaultExt = defaultExtension, AddExtension = true };
        return dialog.ShowDialog(Owner) == true ? dialog.FileName : null;
    }

    public SaveChoice AskSaveChanges(string documentName) =>
        ShowMessage($"Voulez-vous enregistrer les modifications apportées à « {documentName} » ?", MessageDialog.Kind.Warning,
                ["Enregistrer", "Ne pas enregistrer", "Annuler"], cancelIndex: 2) switch
        {
            0 => SaveChoice.Save,
            1 => SaveChoice.DontSave,
            _ => SaveChoice.Cancel,
        };

    public bool AskReloadChangedFile(string documentName) =>
        ShowMessage($"« {documentName} » a été modifié en dehors de MarkView, mais contient aussi des modifications non enregistrées.\n\n" +
                    "Recharger la version du disque (vos modifications seront perdues) ?",
            MessageDialog.Kind.Question, ["Recharger", "Conserver mes modifications"], cancelIndex: 1) == 0;

    public void ShowError(string message) => ShowMessage(message, MessageDialog.Kind.Error, ["OK"], cancelIndex: 0);

    public void ShowInfo(string message) => ShowMessage(message, MessageDialog.Kind.Info, ["OK"], cancelIndex: 0);

    public bool ShowSettings() => ShowModal(settingsWindowFactory()) == true;

    private int ShowMessage(string message, MessageDialog.Kind kind, string[] buttons, int cancelIndex)
    {
        var dialog = new MessageDialog(message, kind, buttons, cancelIndex);
        ShowModal(dialog);
        return dialog.Result;
    }

    private bool? ShowModal(Window window)
    {
        var owner = Owner;
        window.Owner = owner;
        window.WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        window.ShowInTaskbar = owner is null;
        // The handle exists from SourceInitialized on: the title bar is themed before the first frame.
        window.SourceInitialized += (_, _) => themeService.ApplyTitleBar(window);
        return window.ShowDialog();
    }

    /// <summary>The active window, else the main window; null before any window is shown.</summary>
    private static Window? Owner
    {
        get
        {
            var app = Application.Current;
            if (app is null)
            {
                return null;
            }

            var visible = app.Windows.OfType<Window>().Where(w => w.IsVisible).ToList();
            return visible.FirstOrDefault(w => w.IsActive) ?? visible.FirstOrDefault(w => w == app.MainWindow);
        }
    }
}
