namespace ClearVault;

// Holds the Dropbox API "App key" (a public client identifier for the PKCE
// OAuth flow -- not a secret, safe to store in plain text). Register an app
// at https://www.dropbox.com/developers/apps to get one.
static class AppConfig
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVault");

    private static readonly string AppKeyPath = Path.Combine(ConfigDir, "appkey.txt");

    public static string? LoadAppKey()
    {
        return File.Exists(AppKeyPath) ? File.ReadAllText(AppKeyPath).Trim() : null;
    }

    public static void SaveAppKey(string appKey)
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(AppKeyPath, appKey.Trim());
    }
}
