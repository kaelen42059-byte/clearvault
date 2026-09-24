using System.Diagnostics;
using Dropbox.Api.Files;
using ClearVault.Auth;

namespace ClearVault;

// Shared file actions used by both FileBrowserForm and SearchResultsForm, so
// there's exactly one place that knows how to download-then-launch a file.
static class FileActions
{
    public static async Task OpenFileAsync(string refreshToken, string appKey, FileMetadata file)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ClearVault");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, file.Name);

        await using (var contentStream = await RawContentClient.DownloadAsync(refreshToken, appKey, file.PathLower))
        await using (var fileStream = File.Create(tempPath))
        {
            await contentStream.CopyToAsync(fileStream);
        }

        Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
    }
}
