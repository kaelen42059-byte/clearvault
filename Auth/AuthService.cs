using System.Diagnostics;
using System.Net;
using Dropbox.Api;

namespace ClearVault.Auth;

sealed class SignInResult
{
    public required string AccountId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string RefreshToken { get; init; }
}

// Signs a user into Dropbox using the OAuth 2 PKCE flow: open the system
// browser to Dropbox's real login page (which is a normal, accessible
// webpage), catch the redirect on a local loopback listener, and exchange
// the code for a long-lived refresh token. No embedded browser control is
// used anywhere in this app -- that Chromium-panel-inside-a-window pattern
// is exactly what makes the official Dropbox app unusable with a screen
// reader, so this app never does it.
static class AuthService
{
    // Must exactly match a redirect URI registered on the Dropbox app
    // (https://www.dropbox.com/developers/apps), including the trailing slash.
    private const string RedirectUri = "http://127.0.0.1:52874/clearvault/";

    private static readonly string[] Scopes =
    {
        "account_info.read",
        "files.metadata.read",
        "files.metadata.write",
        "files.content.read",
        "files.content.write",
        "sharing.read",
        "sharing.write",
    };

    public static async Task<SignInResult> SignInAsync(string appKey, CancellationToken cancellationToken)
    {
        var flow = new PKCEOAuthFlow();
        var state = Guid.NewGuid().ToString("N");

        var authorizeUri = flow.GetAuthorizeUri(
            OAuthResponseType.Code,
            appKey,
            RedirectUri,
            state: state,
            tokenAccessType: TokenAccessType.Offline,
            scopeList: Scopes,
            includeGrantedScopes: IncludeGrantedScopes.None);

        using var listener = new HttpListener();
        listener.Prefixes.Add(RedirectUri);
        listener.Start();

        try
        {
            Process.Start(new ProcessStartInfo(authorizeUri.ToString()) { UseShellExecute = true });

            using var registration = cancellationToken.Register(listener.Stop);
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var requestUrl = context.Request.Url!;
            const string responseHtml =
                "<html><body>Signed in. You can close this browser tab and return to ClearVault.</body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
            context.Response.OutputStream.Close();

            var oauthResult = await flow.ProcessCodeFlowAsync(requestUrl, appKey, RedirectUri, state);

            using var client = new DropboxClient(oauthResult.RefreshToken, appKey);
            var account = await client.Users.GetCurrentAccountAsync();

            return new SignInResult
            {
                AccountId = account.AccountId,
                Email = account.Email,
                DisplayName = account.Name.DisplayName,
                RefreshToken = oauthResult.RefreshToken,
            };
        }
        finally
        {
            listener.Stop();
        }
    }
}
