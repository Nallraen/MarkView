using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MarkView.Services;
using MarkView.ViewModels;
using MarkView.Views;

namespace MarkView;

public partial class App : Application
{
    private ServiceProvider? _services;
    private SingleInstanceService? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        RegisterExceptionHandlers();

        _singleInstance = new SingleInstanceService();
        if (!_singleInstance.TryAcquire(e.Args))
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        // WPF bindings format with en-US unless told otherwise: use the user's culture (e.g. "5 015", "100 %").
        var language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(language));
        FrameworkContentElement.LanguageProperty.OverrideMetadata(typeof(System.Windows.Documents.TextElement), new FrameworkPropertyMetadata(language));
        _services = ConfigureServices();

        var settings = _services.GetRequiredService<ISettingsService>();
        settings.Load();
        _services.GetRequiredService<IThemeService>().Apply(settings.Settings.Theme);

        var window = _services.GetRequiredService<MainWindow>();
        window.StartupPaths = e.Args;
        MainWindow = window;
        _singleInstance.FilesReceived += async (_, paths) => await window.OpenFromOtherInstanceAsync(paths);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<ISettingsService, SettingsService>(_ => new SettingsService());
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsWindow>();
        services.AddSingleton<Func<SettingsWindow>>(sp => () => sp.GetRequiredService<SettingsWindow>());
        return services.BuildServiceProvider();
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Error(ex, "Unhandled exception (AppDomain)");
                ShowCrashMessage(ex);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled exception (UI thread)");
        ShowCrashMessage(e.Exception);
        e.Handled = true;
    }

    private static void ShowCrashMessage(Exception ex) =>
        MessageBox.Show(
            $"Une erreur inattendue s'est produite :\n\n{ex.Message}\n\nLes détails ont été enregistrés dans :\n{AppPaths.LogsDir}",
            "MarkView",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
}
