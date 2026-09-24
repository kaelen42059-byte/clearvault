using Dropbox.Api;
using Dropbox.Api.Common;

namespace ClearVault.Auth;

static class ClientFactory
{
    public static DropboxClient Create(TokenStore tokenStore, StoredAccount account, string appKey)
    {
        var refreshToken = tokenStore.GetRefreshToken(account);
        return new DropboxClient(refreshToken, appKey);
    }

    // When "Team integration" is on and this account belongs to a Dropbox
    // Business/Team with a team-space root, returns a client scoped to that
    // team namespace (via the Dropbox-API-Path-Root header) so team folders
    // show up when browsing. For a personal account, or with the setting
    // off, this is a plain no-op -- same client as Create() above.
    public static async Task<DropboxClient> CreateForBrowsingAsync(TokenStore tokenStore, StoredAccount account, string appKey)
    {
        var client = Create(tokenStore, account, appKey);
        if (!AppSettings.TeamIntegrationEnabled)
        {
            return client;
        }

        var fullAccount = await client.Users.GetCurrentAccountAsync();
        return fullAccount.RootInfo.IsTeam
            ? client.WithPathRoot(new PathRoot.Root(fullAccount.RootInfo.RootNamespaceId))
            : client;
    }
}
