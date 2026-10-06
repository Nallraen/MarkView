using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using MarkView.Models;

namespace MarkView.Services;

/// <summary>
/// Swaps the palette dictionary (Application.Resources.MergedDictionaries[0]) and themes the Win32 title bars.
/// In System mode it follows the Windows "apps" theme, live.
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int DwmUseImmersiveDarkMode = 20;
    // Windows 10 builds before 20H1 used this undocumented value.
    private const int DwmUseImmersiveDarkModeLegacy = 19;

    private readonly Dispatcher _dispatcher;
    private bool? _appliedDark;

    public ThemeService()
    {
        _dispatcher = Application.Current.Dispatcher;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public AppTheme Mode { get; private set; } = AppTheme.System;

    public bool IsDark { get; private set; }

    public event EventHandler? ThemeChanged;

    public void Apply(AppTheme mode)
    {
        var isDark = mode switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsWindowsDark(),
        };

        var modeChanged = mode != Mode;
        Mode = mode;
        if (isDark == _appliedDark)
        {
            if (modeChanged)
            {
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        _appliedDark = isDark;
        IsDark = isDark;
        var palette = isDark ? "Dark" : "Light";
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/MarkView;component/Themes/{palette}.xaml"),
        };

        foreach (Window window in Application.Current.Windows)
        {
            ApplyTitleBar(window);
        }

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTitleBar(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(hwnd, DwmUseImmersiveDarkModeLegacy, ref value, sizeof(int));
        }
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private static bool IsWindowsDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    // Raised on a SystemEvents thread: re-evaluate on the UI thread.
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            _dispatcher.InvokeAsync(() =>
            {
                if (Mode == AppTheme.System)
                {
                    Apply(AppTheme.System);
                }
            });
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
