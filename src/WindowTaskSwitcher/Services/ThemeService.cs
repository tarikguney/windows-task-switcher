using System.Windows;
using Microsoft.Win32;

namespace WindowTaskSwitcher.Services;

public sealed class ThemeService : IDisposable
{
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValue = "AppsUseLightTheme";

    private readonly System.Threading.Timer _pollTimer;
    private bool _lastIsDark;

    public event Action<bool>? ThemeChanged;

    public bool IsDarkTheme { get; private set; }

    public ThemeService()
    {
        IsDarkTheme = DetectSystemDark();
        _lastIsDark = IsDarkTheme;

        // Poll registry every 2 seconds for theme changes
        // (WPF doesn't get SystemEvents.UserPreferenceChanged reliably for this)
        _pollTimer = new System.Threading.Timer(_ => CheckForThemeChange(), null,
            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    public void ApplyTheme()
    {
        var themePath = IsDarkTheme ? "Resources/DarkTheme.xaml" : "Resources/LightTheme.xaml";
        var themeUri = new Uri(themePath, UriKind.Relative);
        var themeDictionary = new ResourceDictionary { Source = themeUri };

        var app = Application.Current;
        var mergedDicts = app.Resources.MergedDictionaries;

        // Remove existing theme dictionaries (keep Styles.xaml which is index 0)
        for (int i = mergedDicts.Count - 1; i >= 0; i--)
        {
            var source = mergedDicts[i].Source;
            if (source != null && (source.OriginalString.Contains("DarkTheme") || source.OriginalString.Contains("LightTheme")))
                mergedDicts.RemoveAt(i);
        }

        // Add new theme (after Styles.xaml so it overrides color values)
        mergedDicts.Add(themeDictionary);
    }

    private void CheckForThemeChange()
    {
        bool currentIsDark = DetectSystemDark();
        if (currentIsDark != _lastIsDark)
        {
            _lastIsDark = currentIsDark;
            IsDarkTheme = currentIsDark;
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                ApplyTheme();
                ThemeChanged?.Invoke(currentIsDark);
            });
        }
    }

    private static bool DetectSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
            var value = key?.GetValue(RegistryValue);
            // 0 = dark, 1 = light
            return value is int intVal ? intVal == 0 : true;
        }
        catch
        {
            return true; // Default to dark
        }
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
    }
}
