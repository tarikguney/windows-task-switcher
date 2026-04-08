using System.IO;
using System.Text.Json;

namespace WindowTaskSwitcher.Models;

public sealed class UserPreferences
{
    public string HotkeyModifiers { get; set; } = "Ctrl";
    public string HotkeyKey { get; set; } = "Space";
    public bool OverrideAltTab { get; set; } = false;
    public bool RunAtStartup { get; set; } = false;
    public int MaxResults { get; set; } = 15;
    public bool ShowPreviews { get; set; } = false;

    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowTaskSwitcher");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public static UserPreferences Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<UserPreferences>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
