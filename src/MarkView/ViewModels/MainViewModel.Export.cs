using System.IO;
using CommunityToolkit.Mvvm.Input;
using MarkView.Models;
using MarkView.Services;

namespace MarkView.ViewModels;

// Command names below are a contract (bound from MainWindow.xaml).
public partial class MainViewModel
{
    private bool HasActiveDocument => ActiveDocument is not null;

    [RelayCommand(CanExecute = nameof(HasActiveDocument))]
    private async Task ExportHtmlAsync()
    {
        if (ActiveDocument is not { } document
            || _dialogs.ShowSaveDialog(ExportTitle(document) + ".html", "Page HTML (*.html)|*.html", "html") is not { } path)
        {
            return;
        }

        await RunExportAsync("L'export HTML a échoué", () =>
            _exportService.ExportHtmlAsync(document.Document.Text, ExportTitle(document), document.FilePath, path, Settings.ExportEmbedImages));
    }

    [RelayCommand(CanExecute = nameof(HasActiveDocument))]
    private async Task ExportPdfAsync()
    {
        if (ActiveDocument is not { } document
            || _dialogs.ShowSaveDialog(ExportTitle(document) + ".pdf", "Document PDF (*.pdf)|*.pdf", "pdf") is not { } path)
        {
            return;
        }

        await RunExportAsync("L'export PDF a échoué", () =>
            _exportService.ExportPdfAsync(document.Document.Text, ExportTitle(document), document.FilePath, path));
    }

    [RelayCommand(CanExecute = nameof(HasActiveDocument))]
    private async Task PrintAsync()
    {
        if (ActiveDocument is { } document)
        {
            await RunExportAsync("L'impression a échoué", () =>
                _exportService.PrintAsync(document.Document.Text, ExportTitle(document), document.FilePath));
        }
    }

    [RelayCommand(CanExecute = nameof(HasActiveDocument))]
    private void CopyHtml()
    {
        if (ActiveDocument is not { } document)
        {
            return;
        }

        try
        {
            _exportService.CopyHtmlToClipboard(document.Document.Text);
        }
        catch (Exception ex)
        {
            ReportExportError("La copie en HTML a échoué", ex);
        }
    }

    [RelayCommand]
    private void OpenSettings() => _dialogs.ShowSettings();

    [RelayCommand]
    private void SetTheme(AppTheme mode)
    {
        Settings.Theme = mode;
        _themeService.Apply(mode);
        _settingsService.Save();
    }

    private static string ExportTitle(DocumentViewModel document) => Path.GetFileNameWithoutExtension(document.FileName);

    private async Task RunExportAsync(string failure, Func<Task> export)
    {
        try
        {
            await export();
        }
        catch (Exception ex)
        {
            ReportExportError(failure, ex);
        }
    }

    private void ReportExportError(string failure, Exception ex)
    {
        Log.Error(ex, failure);
        _dialogs.ShowError($"{failure}.\n\n{ex.Message}");
    }
}
