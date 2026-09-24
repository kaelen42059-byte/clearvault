using System.Text.Json;

namespace ClearVault;

// Small persisted user preferences. Currently just one setting; this is
// deliberately a plain JSON file (not tied to any UI) so a future dedicated
// Preferences window can read/write the same store without this setting
// needing to move anywhere.
static class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVault", "settings.json");

    private sealed class Data
    {
        // Dropbox share links end in "?dl=0" (opens a preview page) by default.
        // Changing that to "dl=1" makes the link download the file directly.
        public bool DirectDownloadLinks { get; set; } = true;

        // Off by default: only relevant to Dropbox Business/Team accounts, and
        // a no-op for personal accounts even when turned on.
        public bool TeamIntegrationEnabled { get; set; } = false;

        // Background change notifications (tray balloon tips). On by default
        // once the feature exists -- the app now stays running in the tray
        // regardless, so this just controls whether it also pops notifications.
        public bool BackgroundNotifications { get; set; } = true;
    }

    public static bool DirectDownloadLinks
    {
        get => Load().DirectDownloadLinks;
        set
        {
            var data = Load();
            data.DirectDownloadLinks = value;
            Save(data);
        }
    }

    public static bool TeamIntegrationEnabled
    {
        get => Load().TeamIntegrationEnabled;
        set
        {
            var data = Load();
            data.TeamIntegrationEnabled = value;
            Save(data);
        }
    }

    public static bool BackgroundNotifications
    {
        get => Load().BackgroundNotifications;
        set
        {
            var data = Load();
            data.BackgroundNotifications = value;
            Save(data);
        }
    }

    private static Data Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new Data();
        }

        try
        {
            return JsonSerializer.Deserialize<Data>(File.ReadAllText(SettingsPath)) ?? new Data();
        }
        catch (JsonException)
        {
            return new Data();
        }
    }

    private static void Save(Data data)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(data));
    }
}
