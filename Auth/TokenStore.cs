using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClearVault.Auth;

// Persists signed-in Dropbox accounts to disk so the app supports more than
// one account (the whole reason this project exists: switching accounts
// should be a list selection, not a five-attempt fight with a tray flyout).
// Refresh tokens are encrypted with DPAPI, scoped to the current Windows user,
// so the file on disk is useless to anyone without this Windows login.
sealed class TokenStore
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVault");

    private static readonly string StorePath = Path.Combine(ConfigDir, "accounts.json");

    private sealed class StoreFile
    {
        public List<StoredAccount> Accounts { get; set; } = new();
        public string? ActiveAccountId { get; set; }
    }

    private StoreFile _store = Load();

    public IReadOnlyList<StoredAccount> Accounts => _store.Accounts;

    public string? ActiveAccountId => _store.ActiveAccountId;

    public StoredAccount? ActiveAccount =>
        _store.Accounts.FirstOrDefault(a => a.AccountId == _store.ActiveAccountId);

    public string GetRefreshToken(StoredAccount account)
    {
        var protectedBytes = Convert.FromBase64String(account.RefreshTokenProtected);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public void AddOrUpdateAccount(string accountId, string email, string displayName, string refreshToken)
    {
        var plainBytes = Encoding.UTF8.GetBytes(refreshToken);
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);

        var existing = _store.Accounts.FirstOrDefault(a => a.AccountId == accountId);
        if (existing is not null)
        {
            _store.Accounts.Remove(existing);
        }

        _store.Accounts.Add(new StoredAccount
        {
            AccountId = accountId,
            Email = email,
            DisplayName = displayName,
            RefreshTokenProtected = Convert.ToBase64String(protectedBytes),
        });

        _store.ActiveAccountId = accountId;
        Save();
    }

    public void RemoveAccount(string accountId)
    {
        _store.Accounts.RemoveAll(a => a.AccountId == accountId);
        if (_store.ActiveAccountId == accountId)
        {
            _store.ActiveAccountId = _store.Accounts.Count > 0 ? _store.Accounts[0].AccountId : null;
        }
        Save();
    }

    public void SetActiveAccount(string accountId)
    {
        _store.ActiveAccountId = accountId;
        Save();
    }

    private static StoreFile Load()
    {
        if (!File.Exists(StorePath))
        {
            return new StoreFile();
        }

        try
        {
            var json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<StoreFile>(json) ?? new StoreFile();
        }
        catch (JsonException)
        {
            // Corrupt store file -- start fresh rather than crash the app.
            return new StoreFile();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(_store, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(StorePath, json);
    }
}
