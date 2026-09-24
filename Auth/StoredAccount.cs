namespace ClearVault.Auth;

// One signed-in Dropbox account. RefreshTokenProtected is the refresh token
// encrypted at rest with Windows DPAPI (see TokenStore) -- never plain text on disk.
public sealed class StoredAccount
{
    public required string AccountId { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string RefreshTokenProtected { get; set; }
}
