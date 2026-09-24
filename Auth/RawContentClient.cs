using System.Net.Http.Headers;
using System.Text.Json;

namespace ClearVault.Auth;

// Works around a long-standing bug in the Dropbox .NET SDK: for download-style
// requests, DropboxRequestHandler explicitly sets the Content-Type header to
// null (a leftover workaround for old libcurl bindings -- see
// https://github.com/dropbox/dropbox-sdk-dotnet/issues/77). Dropbox's content
// server rejects that with a bare, unhelpful HTTP 400 "other" error for some
// requests. This bypasses Files.DownloadAsync entirely and talks to the
// content server directly with a normal Content-Type, which Dropbox accepts.
static class RawContentClient
{
    private static readonly HttpClient Http = new();

    public static async Task<Stream> DownloadAsync(string refreshToken, string appKey, string path)
    {
        var accessToken = await GetAccessTokenAsync(refreshToken, appKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://content.dropboxapi.com/2/files/download");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Dropbox-API-Arg", JsonSerializer.Serialize(new { path }));
        request.Content = new ByteArrayContent(Array.Empty<byte>());
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"HTTP {(int)response.StatusCode} downloading '{path}': {body.Trim()}");
        }

        return await response.Content.ReadAsStreamAsync();
    }

    private static async Task<string> GetAccessTokenAsync(string refreshToken, string appKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.dropboxapi.com/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = appKey,
            }),
        };

        var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Couldn't refresh the Dropbox access token: {body.Trim()}");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("access_token").GetString()!;
    }
}
