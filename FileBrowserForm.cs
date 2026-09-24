using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Sharing;
using ClearVault.Auth;

namespace ClearVault;

// Browses one Dropbox account's files using only native controls: a Details-view
// ListView for the current folder, plain buttons for navigation, and a status
// TextBox for announcements -- the same "focus a read-only TextBox after
// changing its text" trick MainForm uses, since WinForms has no live-region
// equivalent for a Label.
sealed class FileBrowserForm : Form
{
    private readonly DropboxClient _client;
    private readonly string _refreshToken;
    private readonly string _appKey;

    private readonly Label _pathLabel;
    private readonly ListView _list;
    private readonly Button _upButton;
    private readonly Button _openButton;
    private readonly Button _downloadButton;
    private readonly Button _refreshButton;
    private readonly Button _shareButton;
    private readonly Button _newFolderButton;
    private readonly Button _uploadButton;
    private readonly Button _searchButton;
    private readonly Button _renameButton;
    private readonly Button _deleteButton;
    private readonly Button _moveButton;
    private readonly TextBox _statusBox;

    private string _currentPath = "";

    public FileBrowserForm(DropboxClient client, string refreshToken, string appKey, string accountLabel)
    {
        _client = client;
        _refreshToken = refreshToken;
        _appKey = appKey;

        Text = $"ClearVault - Files - {accountLabel}";
        ClientSize = new Size(680, 520);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 340);

        _pathLabel = new Label
        {
            Left = 12,
            Top = 12,
            Width = 656,
            Height = 20,
            Text = "Location: / (root)",
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
        };

        _list = new ListView
        {
            Left = 12,
            Top = _pathLabel.Bottom + 4,
            Width = 656,
            Height = 280,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AccessibleName = "Folder contents",
        };
        _list.Columns.Add("Name", 300);
        _list.Columns.Add("Type", 90);
        _list.Columns.Add("Size", 100);
        _list.Columns.Add("Modified", 150);
        _list.KeyDown += List_KeyDown;
        _list.DoubleClick += (_, _) => ActivateSelected();
        _list.SelectedIndexChanged += (_, _) => UpdateButtonStates();

        _upButton = new Button
        {
            Text = "&Up one level",
            Left = 12,
            Top = _list.Bottom + 12,
            Width = 120,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _upButton.Click += async (_, _) => await NavigateUpAsync();

        _openButton = new Button
        {
            Text = "&Open",
            Left = _upButton.Right + 8,
            Top = _upButton.Top,
            Width = 100,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _openButton.Click += (_, _) => ActivateSelected();

        _downloadButton = new Button
        {
            Text = "&Download...",
            Left = _openButton.Right + 8,
            Top = _upButton.Top,
            Width = 120,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _downloadButton.Click += DownloadButton_Click;

        _refreshButton = new Button
        {
            Text = "&Refresh",
            Left = _downloadButton.Right + 8,
            Top = _upButton.Top,
            Width = 100,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _refreshButton.Click += async (_, _) => await LoadFolderAsync(_currentPath);

        var closeButton = new Button
        {
            Text = "&Close",
            Left = _refreshButton.Right + 8,
            Top = _upButton.Top,
            Width = 100,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            DialogResult = DialogResult.Cancel,
        };

        _shareButton = new Button
        {
            Text = "&Share / Copy link",
            Left = 12,
            Top = _upButton.Bottom + 8,
            Width = 160,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _shareButton.Click += ShareButton_Click;

        _newFolderButton = new Button
        {
            Text = "New &folder...",
            Left = _shareButton.Right + 8,
            Top = _shareButton.Top,
            Width = 140,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _newFolderButton.Click += NewFolderButton_Click;

        _uploadButton = new Button
        {
            Text = "&Upload...",
            Left = _newFolderButton.Right + 8,
            Top = _shareButton.Top,
            Width = 120,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _uploadButton.Click += UploadButton_Click;

        _searchButton = new Button
        {
            Text = "&Search...",
            Left = _uploadButton.Right + 8,
            Top = _shareButton.Top,
            Width = 110,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _searchButton.Click += SearchButton_Click;

        _renameButton = new Button
        {
            Text = "Rena&me...",
            Left = 12,
            Top = _shareButton.Bottom + 8,
            Width = 110,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _renameButton.Click += RenameButton_Click;

        _deleteButton = new Button
        {
            Text = "&Delete...",
            Left = _renameButton.Right + 8,
            Top = _renameButton.Top,
            Width = 100,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _deleteButton.Click += DeleteButton_Click;

        _moveButton = new Button
        {
            Text = "M&ove to...",
            Left = _deleteButton.Right + 8,
            Top = _renameButton.Top,
            Width = 120,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _moveButton.Click += MoveButton_Click;

        var statusCaption = new Label
        {
            Left = 12,
            Top = _renameButton.Bottom + 12,
            Width = 656,
            Height = 20,
            Text = "Status:",
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        _statusBox = new TextBox
        {
            Left = 12,
            Top = statusCaption.Bottom + 4,
            Width = 656,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = "Status",
            TabStop = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        Controls.Add(_pathLabel);
        Controls.Add(_list);
        Controls.Add(_upButton);
        Controls.Add(_openButton);
        Controls.Add(_downloadButton);
        Controls.Add(_refreshButton);
        Controls.Add(closeButton);
        Controls.Add(_shareButton);
        Controls.Add(_newFolderButton);
        Controls.Add(_uploadButton);
        Controls.Add(_searchButton);
        Controls.Add(_renameButton);
        Controls.Add(_deleteButton);
        Controls.Add(_moveButton);
        Controls.Add(statusCaption);
        Controls.Add(_statusBox);

        CancelButton = closeButton;
        UpdateButtonStates();

        Load += async (_, _) => await LoadFolderAsync(_currentPath);
        HelpRequested += (_, e) => { HelpViewer.Open(); e.Handled = true; };
    }

    private void List_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ActivateSelected();
        }
        else if (e.KeyCode == Keys.Back)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = NavigateUpAsync();
        }
        else if (e.KeyCode == Keys.Delete)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            DeleteButton_Click(sender, EventArgs.Empty);
        }
        else if (e.KeyCode == Keys.F2)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            RenameButton_Click(sender, EventArgs.Empty);
        }
    }

    private Metadata? GetSelectedEntry() =>
        _list.SelectedItems.Count > 0 ? (Metadata)_list.SelectedItems[0].Tag! : null;

    private void UpdateButtonStates()
    {
        _upButton.Enabled = _currentPath != "";
        var entry = GetSelectedEntry();
        _openButton.Enabled = entry is not null;
        _downloadButton.Enabled = entry is { IsFile: true };
        _shareButton.Enabled = entry is not null;
        _renameButton.Enabled = entry is not null;
        _deleteButton.Enabled = entry is not null;
        _moveButton.Enabled = entry is not null;
    }

    private void SetControlsEnabled(bool enabled)
    {
        // Deliberately NOT disabling _list here: disabling a control that
        // currently has keyboard focus makes Windows immediately shift focus
        // to the next enabled control (the status box), before the code below
        // ever gets a chance to focus the list back -- Control.Focus() is a
        // silent no-op on a disabled control. The buttons are enough to
        // prevent re-entrant clicks while a load is in progress.
        _upButton.Enabled = enabled && _currentPath != "";
        _openButton.Enabled = enabled && GetSelectedEntry() is not null;
        _downloadButton.Enabled = enabled && GetSelectedEntry() is { IsFile: true };
        _shareButton.Enabled = enabled && GetSelectedEntry() is not null;
        _renameButton.Enabled = enabled && GetSelectedEntry() is not null;
        _deleteButton.Enabled = enabled && GetSelectedEntry() is not null;
        _moveButton.Enabled = enabled && GetSelectedEntry() is not null;
        _newFolderButton.Enabled = enabled;
        _uploadButton.Enabled = enabled;
        _searchButton.Enabled = enabled;
        _refreshButton.Enabled = enabled;
    }

    private void AnnounceStatus(string message) => AccessibleAnnouncer.Announce(_statusBox, message);

    private async Task NavigateUpAsync()
    {
        if (_currentPath == "")
        {
            return;
        }

        await LoadFolderAsync(GetParentPath(_currentPath));
    }

    private static string GetParentPath(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        return lastSlash > 0 ? path[..lastSlash] : "";
    }

    private async void ActivateSelected()
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            AnnounceStatus("Select an item first.");
            return;
        }

        if (entry.IsFolder)
        {
            await LoadFolderAsync(entry.AsFolder.PathLower);
            return;
        }

        var file = entry.AsFile;
        SetControlsEnabled(false);
        AnnounceStatus($"Opening {file.Name}...");
        try
        {
            await FileActions.OpenFileAsync(_refreshToken, _appKey, file);
            AnnounceStatus($"Opened {file.Name} with its default app.");
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Couldn't open {file.Name}: {DescribeError(ex)}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void DownloadButton_Click(object? sender, EventArgs e)
    {
        if (GetSelectedEntry() is not { IsFile: true } entry)
        {
            AnnounceStatus("Select a file first (folders can't be downloaded here).");
            return;
        }

        var file = entry.AsFile;
        using var dialog = new SaveFileDialog { FileName = file.Name, Title = "Save file" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetControlsEnabled(false);
        AnnounceStatus($"Downloading {file.Name}...");
        try
        {
            await using (var contentStream = await RawContentClient.DownloadAsync(_refreshToken, _appKey, file.PathLower))
            await using (var fileStream = File.Create(dialog.FileName))
            {
                await contentStream.CopyToAsync(fileStream);
            }

            AnnounceStatus($"Downloaded {file.Name}.");
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Download failed: {DescribeError(ex)}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void ShareButton_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            AnnounceStatus("Select an item first.");
            return;
        }

        SetControlsEnabled(false);
        AnnounceStatus($"Getting a share link for {entry.Name}...");
        try
        {
            string url;
            try
            {
                var linkMetadata = await _client.Sharing.CreateSharedLinkWithSettingsAsync(entry.PathLower);
                url = linkMetadata.Url;
            }
            catch (ApiException<CreateSharedLinkWithSettingsError> ex) when (ex.ErrorResponse.IsSharedLinkAlreadyExists)
            {
                var existing = ex.ErrorResponse.AsSharedLinkAlreadyExists.Value;
                if (existing.IsMetadata)
                {
                    url = existing.AsMetadata.Value.Url;
                }
                else
                {
                    // "Other" variant: the error confirms a link already exists but
                    // doesn't hand it back, so look it up directly.
                    var existingLinks = await _client.Sharing.ListSharedLinksAsync(entry.PathLower, directOnly: true);
                    url = existingLinks.Links.Count > 0
                        ? existingLinks.Links[0].Url
                        : throw new InvalidOperationException("Dropbox says a link already exists but didn't return one.");
                }
            }

            url = ApplyDownloadPreference(url);
            Clipboard.SetText(url);
            AnnounceStatus($"Share link for {entry.Name} copied to clipboard: {url}");
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Couldn't get a share link for {entry.Name}: {DescribeError(ex)}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private void SearchButton_Click(object? sender, EventArgs e)
    {
        using var prompt = new TextInputForm("Search", "Search your Dropbox for:");
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        using var results = new SearchResultsForm(_client, _refreshToken, _appKey, prompt.InputText);
        results.ShowDialog(this);
    }

    private async void NewFolderButton_Click(object? sender, EventArgs e)
    {
        using var prompt = new TextInputForm("New folder", "Folder name:");
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var name = prompt.InputText;
        var newPath = _currentPath == "" ? $"/{name}" : $"{_currentPath}/{name}";

        SetControlsEnabled(false);
        AnnounceStatus($"Creating folder {name}...");
        try
        {
            await _client.Files.CreateFolderV2Async(newPath);
            AnnounceStatus($"Created folder {name}.");
            await LoadFolderAsync(_currentPath);
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Couldn't create folder {name}: {DescribeError(ex)}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void UploadButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Title = "Upload file", Multiselect = true };
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.FileNames.Length == 0)
        {
            return;
        }

        SetControlsEnabled(false);
        try
        {
            foreach (var localPath in dialog.FileNames)
            {
                var name = Path.GetFileName(localPath);
                var destPath = _currentPath == "" ? $"/{name}" : $"{_currentPath}/{name}";
                AnnounceStatus($"Uploading {name}...");
                try
                {
                    await using var fileStream = File.OpenRead(localPath);
                    await _client.Files.UploadAsync(destPath, WriteMode.Overwrite.Instance, body: fileStream);
                    AnnounceStatus($"Uploaded {name}.");
                }
                catch (Exception ex)
                {
                    AnnounceStatus($"Couldn't upload {name}: {DescribeError(ex)}");
                }
            }

            await LoadFolderAsync(_currentPath);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void RenameButton_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            AnnounceStatus("Select an item first.");
            return;
        }

        using var prompt = new TextInputForm("Rename", $"New name for \"{entry.Name}\":", entry.Name);
        if (prompt.ShowDialog(this) != DialogResult.OK || prompt.InputText == entry.Name)
        {
            return;
        }

        var parent = GetParentPath(entry.PathLower);
        var newPath = parent == "" ? $"/{prompt.InputText}" : $"{parent}/{prompt.InputText}";

        SetControlsEnabled(false);
        AnnounceStatus($"Renaming {entry.Name}...");
        try
        {
            await _client.Files.MoveV2Async(entry.PathLower, newPath);
            AnnounceStatus($"Renamed {entry.Name} to {prompt.InputText}.");
            await LoadFolderAsync(_currentPath);
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Couldn't rename {entry.Name}: {DescribeError(ex)}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void DeleteButton_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            AnnounceStatus("Select an item first.");
            return;
        }

        var kind = entry.IsFolder ? "folder" : "file";
        var confirm = MessageBox.Show(this,
            $"Delete the {kind} \"{entry.Name}\"? Dropbox keeps deleted items for a while, so this is usually " +
            "recoverable from dropbox.com if it's a mistake, but confirm to continue.",
            "Confirm delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes)
        {
            return;
        }

        SetControlsEnabled(false);
        AnnounceStatus($"Deleting {entry.Name}...");
        try
        {
            await _client.Files.DeleteV2Async(entry.PathLower);
            AnnounceStatus($"Deleted {entry.Name}.");
            await LoadFolderAsync(_currentPath);
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Couldn't delete {entry.Name}: {DescribeError(ex)}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async void MoveButton_Click(object? sender, EventArgs e)
    {
        var entry = GetSelectedEntry();
        if (entry is null)
        {
            AnnounceStatus("Select an item first.");
            return;
        }

        using var prompt = new TextInputForm(
            "Move to",
            $"Destination folder path for \"{entry.Name}\" (e.g. /Documents -- use / for root):",
            _currentPath);
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var destFolder = prompt.InputText.Trim();
        if (destFolder == "/")
        {
            destFolder = "";
        }
        var newPath = destFolder == "" ? $"/{entry.Name}" : $"{destFolder}/{entry.Name}";

        SetControlsEnabled(false);
        AnnounceStatus($"Moving {entry.Name}...");
        try
        {
            await _client.Files.MoveV2Async(entry.PathLower, newPath);
            AnnounceStatus($"Moved {entry.Name} to {(destFolder == "" ? "the root folder" : destFolder)}.");
            await LoadFolderAsync(_currentPath);
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Couldn't move {entry.Name}: {DescribeError(ex)}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private async Task LoadFolderAsync(string path)
    {
        SetControlsEnabled(false);
        _statusBox.Text = "Loading folder contents...";
        try
        {
            var entries = new List<Metadata>();
            var result = await _client.Files.ListFolderAsync(path);
            entries.AddRange(result.Entries);
            while (result.HasMore)
            {
                result = await _client.Files.ListFolderContinueAsync(result.Cursor);
                entries.AddRange(result.Entries);
            }

            var sorted = entries
                .Where(entry => !entry.IsDeleted)
                .OrderByDescending(entry => entry.IsFolder)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _list.Items.Clear();
            foreach (var entry in sorted)
            {
                var item = new ListViewItem(entry.Name) { Tag = entry };
                if (entry.IsFolder)
                {
                    item.SubItems.Add("Folder");
                    item.SubItems.Add("");
                    item.SubItems.Add("");
                }
                else
                {
                    var file = entry.AsFile;
                    item.SubItems.Add("File");
                    item.SubItems.Add(FormatSize(file.Size));
                    item.SubItems.Add(file.ServerModified.ToLocalTime().ToString("g"));
                }
                _list.Items.Add(item);
            }

            _currentPath = path;
            _pathLabel.Text = "Location: " + (path == "" ? "/ (root)" : path);

            if (sorted.Count == 0)
            {
                AnnounceStatus("This folder is empty.");
            }
            else
            {
                AnnounceStatus($"Loaded {sorted.Count} item(s).");
                _list.Items[0].Selected = true;
                _list.Items[0].Focused = true;
                _list.Focus();
            }
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Couldn't load this folder: {ex.Message}");
        }
        finally
        {
            SetControlsEnabled(true);
            UpdateButtonStates();
        }
    }

    // Dropbox's own error messages are often just a bare {".tag": "other"} --
    // that's the real, unhelpfully vague answer from Dropbox's servers, not
    // something the SDK is hiding. This pulls out whatever more specific
    // reason IS available, plus the request ID Dropbox support can look up.
    private static string DescribeError(Exception ex)
    {
        if (ex is Dropbox.Api.HttpException httpEx)
        {
            return $"HTTP {httpEx.StatusCode} error from Dropbox: {httpEx.Message} (request ID: {httpEx.RequestId})";
        }

        if (ex is ApiException<CreateSharedLinkWithSettingsError> shareEx)
        {
            var reason = shareEx.ErrorResponse switch
            {
                { IsPath: true } => DescribeLookupError(shareEx.ErrorResponse.AsPath.Value),
                { IsAccessDenied: true } => "You don't have permission to share this item.",
                { IsEmailNotVerified: true } => "Your Dropbox account's email needs to be verified before you can create share links.",
                { IsBannedMember: true } => "This account isn't allowed to share content on this team.",
                { IsTooManySharedFolders: true } => "Too many shared folders -- Dropbox won't create another one.",
                _ => "Dropbox returned an unspecified error creating this share link.",
            };
            return $"{reason} (Dropbox request ID: {shareEx.RequestId})";
        }

        if (ex is ApiException<DownloadError> downloadEx)
        {
            var reason = downloadEx.ErrorResponse switch
            {
                { IsPath: true } => DescribeLookupError(downloadEx.ErrorResponse.AsPath.Value),
                { IsUnsupportedFile: true } =>
                    "Dropbox doesn't support downloading this file directly -- it may be an online-only " +
                    "format (like Google Docs or Paper) that needs exporting instead.",
                _ =>
                    "Dropbox returned an unspecified server-side error for this file. This is sometimes " +
                    "transient -- try again in a moment, or check whether the file opens fine on dropbox.com.",
            };
            return $"{reason} (Dropbox request ID: {downloadEx.RequestId})";
        }

        return ex.Message;
    }

    private static string DescribeLookupError(LookupError error) => error switch
    {
        { IsNotFound: true } => "Dropbox can't find this file anymore -- it may have been moved or deleted. Try Refresh.",
        { IsNotFile: true } => "That entry isn't actually a file.",
        { IsRestrictedContent: true } => "Dropbox is restricting access to this file's content.",
        { IsUnsupportedContentType: true } => "Dropbox doesn't support downloading this content type.",
        { IsLocked: true } => "This file is locked.",
        { IsMalformedPath: true } => "Dropbox rejected the file's path as malformed.",
        _ => "Dropbox gave an unspecified reason.",
    };

    // Dropbox share links end in "?dl=0" (opens a preview page in the
    // browser) by default. Swapping that to "dl=1" makes the link download
    // the file directly instead -- controlled by the checkbox next to Share,
    // persisted via AppSettings.
    private static string ApplyDownloadPreference(string url)
    {
        if (!AppSettings.DirectDownloadLinks)
        {
            return url;
        }

        if (url.Contains("dl=0"))
        {
            return url.Replace("dl=0", "dl=1");
        }

        if (url.Contains("dl=1"))
        {
            return url;
        }

        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}dl=1";
    }

    private static string FormatSize(ulong bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }
        return unitIndex == 0 ? $"{bytes} B" : $"{size:0.#} {units[unitIndex]}";
    }
}
