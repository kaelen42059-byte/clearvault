using System.Reflection;

namespace ClearVault;

// Loads the app icon from the embedded resource (Assets/ClearVault.ico) so it
// works from the single-file published exe without needing a separate file
// shipped alongside it. Falls back to a system icon if something's wrong with
// the embed, rather than crashing the app over a missing icon.
static class AppIcon
{
    public static Icon Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("ClearVault.ico", StringComparison.Ordinal));
        if (resourceName is null)
        {
            return SystemIcons.Application;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }
}
