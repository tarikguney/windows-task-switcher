using Microsoft.Win32;

namespace WindowTaskSwitcher.Services;

public sealed class StartupService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowTaskSwitcher";

    public bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
        return key?.GetValue(AppName) != null;
    }

    public void SetStartupEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
        if (key == null) return;

        if (enabled)
        {
            string exePath = Environment.ProcessPath ?? string.Empty;
            if (!string.IsNullOrEmpty(exePath))
                key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
