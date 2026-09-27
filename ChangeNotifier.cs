using System.Text.Json;
using Dropbox.Api;
using Dropbox.Api.Files;
using ClearVault.Auth;

namespace ClearVault;

// Polls the active account for changes and pops a short tray balloon when
// something changed -- "hey, this happened," nothing more. Dropbox's .NET
// SDK doesn't wrap the real-time longpoll endpoint, so this uses plain
// cursor-based polling instead: cheap (delta-only after the first call) and
// plenty timely for a personal notification, just not instant.
//
// It also keeps a plain-language status ("Up to date", "Checking for
// changes", "Can't reach Dropbox"...) that the main window's status bar and
// the tray icon's tooltip/menu show, so there's always a way to ask "is
// this thing actually watching?" and "what was the last change?".
sealed class ChangeNotifier : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(90);

    // The cursor is saved per account so changes made while ClearVault
    // wasn't running (PC off overnight, app restarted) are still reported on
    // the next start, instead of silently becoming the new baseline.
    private static readonly string CursorStorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClearVault", "change-cursors.json");

    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;

    private DropboxClient? _client;
    private string? _clientKey;
    private bool _checking;
    private bool _recheckRequested;
    private DateTime? _lastSuccessfulCheck;

    public ChangeNotifier(NotifyIcon trayIcon)
    {
        _trayIcon = trayIcon;
        _timer = new System.Windows.Forms.Timer { Interval = (int)PollInterval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await CheckNowAsync();
    }

    public event EventHandler? StatusChanged;

    // Short status, e.g. "Up to date (checked 3:42 PM)". Kept brief because
    // the tray tooltip has a hard length limit.
    public string Status { get; private set; } = "Starting";

    // One-line summary of the most recent change seen, e.g. "3:40 PM: Changed: report.pdf",
    // or null if nothing has changed since ClearVault started.
    public string? LastChange { get; private set; }

    public void Start()
    {
        _timer.Start();
        // Check right away instead of waiting a full interval after launch.
        _ = CheckNowAsync();
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Dispose();
        _client?.Dispose();
    }

    public async Task CheckNowAsync()
    {
        // Timer ticks and manual "Check now" clicks can overlap. Never run
        // two checks at once; instead run one more right after this one, so
        // a check asked for mid-run (say, right after switching accounts)
        // isn't lost.
        if (_checking)
        {
            _recheckRequested = true;
            return;
        }
        _checking = true;

        do
        {
            _recheckRequested = false;
            await CheckOnceAsync();
        }
        while (_recheckRequested);

        _checking = false;
    }

    private async Task CheckOnceAsync()
    {
        try
        {
            var appKey = AppConfig.LoadAppKey();
            if (appKey is null)
            {
                SetStatus("Not set up yet");
                return;
            }

            // Read the account list fresh on every check. The main window has
            // its own TokenStore, so a copy loaded once here would never see
            // an account added (or switched to) after ClearVault started --
            // and would never check anything.
            var tokenStore = new TokenStore();
            var account = tokenStore.ActiveAccount;
            if (account is null)
            {
                SetStatus("Not signed in");
                return;
            }

            var cursorKey = $"{account.AccountId}|{(AppSettings.TeamIntegrationEnabled ? "team" : "personal")}";
            var client = await GetClientAsync(tokenStore, account, appKey, cursorKey);
            var cursor = LoadCursor(cursorKey);

            if (cursor is null)
            {
                // First check for this account: establish a baseline silently.
                // We only want to notify about changes that happen FROM NOW
                // ON, not dump the account's entire existing content as
                // "changes" the moment we start watching it.
                SetStatus("Indexing");
                var latest = await client.Files.ListFolderGetLatestCursorAsync("", recursive: true);
                SaveCursor(cursorKey, latest.Cursor);
                MarkUpToDate();
                return;
            }

            SetStatus("Checking for changes");

            // Collect every page before saving the new cursor, so a failure
            // partway through retries the whole batch next time instead of
            // skipping the pages that were already read.
            var allEntries = new List<Metadata>();
            try
            {
                var result = await client.Files.ListFolderContinueAsync(cursor);
                allEntries.AddRange(result.Entries);
                while (result.HasMore)
                {
                    result = await client.Files.ListFolderContinueAsync(result.Cursor);
                    allEntries.AddRange(result.Entries);
                }
                cursor = result.Cursor;
            }
            catch (ApiException<ListFolderContinueError>)
            {
                // Cursor expired or no longer valid for this account -- resync
                // quietly. Changes in that gap can't be listed, so there's
                // nothing to notify about.
                SetStatus("Indexing");
                var latest = await client.Files.ListFolderGetLatestCursorAsync("", recursive: true);
                SaveCursor(cursorKey, latest.Cursor);
                MarkUpToDate();
                return;
            }

            SaveCursor(cursorKey, cursor);

            var summary = Summarize(allEntries);
            if (summary is not null)
            {
                LastChange = $"{DateTime.Now:t}: {summary}";
                if (AppSettings.BackgroundNotifications)
                {
                    _trayIcon.BalloonTipTitle = "Dropbox";
                    _trayIcon.BalloonTipText = summary;
                    _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    _trayIcon.ShowBalloonTip(6000);
                }
            }

            MarkUpToDate();
        }
        catch (Exception ex)
        {
            // A failed background check shouldn't crash the app or pop up
            // errors -- but it also shouldn't fail invisibly. Show it in the
            // status and try again next cycle with a fresh connection.
            _client?.Dispose();
            _client = null;
            _clientKey = null;

            var since = _lastSuccessfulCheck is { } last ? $" since {last:t}" : "";
            SetStatus($"Can't reach Dropbox{since}, retrying", ex.Message);
        }
    }

    // Reuses one client while the account and team setting stay the same.
    // Creating a new DropboxClient on every poll leaked an HttpClient each
    // time and refreshed the access token every 90 seconds.
    private async Task<DropboxClient> GetClientAsync(TokenStore tokenStore, StoredAccount account, string appKey, string key)
    {
        if (_client is not null && _clientKey == key)
        {
            return _client;
        }

        _client?.Dispose();
        _client = null;
        // Same client the file browser uses, so with Team integration on,
        // changes in team-space folders are watched too.
        _client = await ClientFactory.CreateForBrowsingAsync(tokenStore, account, appKey);
        _clientKey = key;
        return _client;
    }

    private void MarkUpToDate()
    {
        _lastSuccessfulCheck = DateTime.Now;
        var paused = AppSettings.BackgroundNotifications ? "" : ", notifications off";
        SetStatus($"Up to date (checked {_lastSuccessfulCheck:t}{paused})");
    }

    private void SetStatus(string status, string? detail = null)
    {
        var full = detail is null ? status : $"{status}: {detail}";
        if (full == Status)
        {
            return;
        }
        Status = full;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string? Summarize(IReadOnlyList<Metadata> entries)
    {
        var changed = entries.Where(entry => !entry.IsDeleted).Select(entry => entry.Name).ToList();
        var removed = entries.Where(entry => entry.IsDeleted).Select(entry => entry.Name).ToList();

        var parts = new List<string>();
        if (changed.Count > 0)
        {
            parts.Add(Summarize("Changed", changed));
        }
        if (removed.Count > 0)
        {
            parts.Add(Summarize("Removed", removed));
        }

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string Summarize(string verb, IReadOnlyList<string> names)
    {
        if (names.Count == 1)
        {
            return $"{verb}: {names[0]}";
        }

        var shown = string.Join(", ", names.Take(2));
        var extra = names.Count > 2 ? $", +{names.Count - 2} more" : "";
        return $"{verb} {names.Count} items: {shown}{extra}";
    }

    private static Dictionary<string, string> LoadCursors()
    {
        try
        {
            return File.Exists(CursorStorePath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(CursorStorePath)) ?? new()
                : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return new();
        }
    }

    private static string? LoadCursor(string key) => LoadCursors().GetValueOrDefault(key);

    private static void SaveCursor(string key, string cursor)
    {
        var cursors = LoadCursors();
        if (cursors.GetValueOrDefault(key) == cursor)
        {
            return;
        }
        cursors[key] = cursor;
        Directory.CreateDirectory(Path.GetDirectoryName(CursorStorePath)!);
        File.WriteAllText(CursorStorePath, JsonSerializer.Serialize(cursors));
    }
}
