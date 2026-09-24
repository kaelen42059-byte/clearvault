using System.Reflection;

namespace ClearVault;

// F1 opens this the same way the old Windows "compiled HTML help" convention
// did, minus the actual .chm format -- that compiler is a 20-year-old
// discontinued tool with no safe, official download anymore. Opening a plain
// HTML page in the user's own browser gets the same "press F1, get help"
// result without depending on that.
static class HelpViewer
{
    public static void Open()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("help.html", StringComparison.Ordinal));
        if (resourceName is null)
        {
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "ClearVault");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, "help.html");

        using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
        using (var fileStream = File.Create(tempPath))
        {
            resourceStream?.CopyTo(fileStream);
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
    }
}
