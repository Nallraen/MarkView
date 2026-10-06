using System.Windows;
using MarkView.ViewModels;

namespace MarkView.Views;

/// <summary>Settings dialog: OK commits through the view model, Annuler (IsCancel) just closes.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Apply();
        DialogResult = true;
    }
}
