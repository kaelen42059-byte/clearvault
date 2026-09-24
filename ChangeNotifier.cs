using Dropbox.Api;
using Dropbox.Api.Files;
using ClearVault.Auth;

namespace ClearVault;

// Polls the active account for changes and pops a short tray balloon when
// something changed -- "hey, this happened," nothing more. Dropbox's .NET
// SDK doesn't wrap the real-time longpoll endpoint, so this uses plain
// cursor-based polling instead: cheap (delta-only after the first call) and
// plenty timely for a personal notification, just not instant.
sealed class ChangeNotifier
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(90);

    private readonly NotifyIcon _trayIcon;
    private readonly TokenStore _tokenStore = new();
    private readonly System.Windows.Forms.Timer _timer;

    private string? _cursor;
    private string? _cursorAccountId;
    private bool _checking;

    public ChangeNotifier(NotifyIcon trayIcon)
    {
        _trayIcon = trayIcon;
        _timer = new System.Windows.Forms.Timer { Interval = (int)PollInterval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await CheckNowAsync();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public async Task CheckNowAsync()
    {
        // Timer ticks and manual "Check now" clicks can overlap; skip a tick
        // rather than run two checks at once.
        if (_checking)
        {
            return;
        }
        _checking = true;

        try
        {
            var appKey = AppConfig.LoadAppKey();
            var account = _tokenStore.ActiveAccount;
            if (appKey is null || account is null)
            {
                return;
            }

            var client = ClientFactory.Create(_tokenStore, account, appKey);

            if (_cursor is null || _cursorAccountId != account.AccountId)
            {
                // First check (or the active account changed): establish a
                // baseline silently. We only want to notify about changes
                // that happen FROM NOW ON, not dump the account's entire
                // existing content as "changes" the moment we start watching it.
                var latest = await client.Files.ListFolderGetLatestCursorAsync("", recursive: true);
                _cursor = latest.Cursor;
                _cursorAccountId = account.AccountId;
                return;
            }

            var allEntries = new List<Metadata>();
            ListFolderResult result;
            try
            {
                result = await client.Files.ListFolderContinueAsync(_cursor);
            }
            catch (ApiException<ListFolderContinueError> ex) when (ex.ErrorResponse.IsReset)
            {
                // Cursor expired server-side -- resync quietly, no notification.
                var latest = await client.Files.ListFolderGetLatestCursorAsync("", recursive: true);
                _cursor = latest.Cursor;
                return;
            }

            allEntries.AddRange(result.Entries);
            _cursor = result.Cursor;
            while (result.HasMore)
            {
                result = await client.Files.ListFolderContinueAsync(_cursor);
                allEntries.AddRange(result.Entries);
                _cursor = result.Cursor;
            }

            if (allEntries.Count > 0 && AppSettings.BackgroundNotifications)
            {
                Notify(allEntries);
            }
        }
        catch
        {
            // A failed background check shouldn't crash the app or nag the
            // user -- just try again next cycle.
        }
        finally
        {
            _checking = false;
        }
    }

    private void Notify(IReadOnlyList<Metadata> entries)
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

        var text = string.Join(" | ", parts);
        if (text.Length == 0)
        {
            return;
        }

        _trayIcon.BalloonTipTitle = "Dropbox";
        _trayIcon.BalloonTipText = text;
        _trayIcon.ShowBalloonTip(6000);
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
}
