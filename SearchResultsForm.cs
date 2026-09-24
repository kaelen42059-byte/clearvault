using Dropbox.Api;
using Dropbox.Api.Files;
using ClearVault.Auth;

namespace ClearVault;

// Shows search results as a proper Details-view list (Name, Type, Path,
// Modified), same pattern as FileBrowserForm. Only "Open" is supported here
// (files only) -- for anything else (share, rename, delete, move), browse to
// the item's actual folder, shown in the Path column.
sealed class SearchResultsForm : Form
{
    private readonly DropboxClient _client;
    private readonly string _refreshToken;
    private readonly string _appKey;
    private readonly string _query;

    private readonly ListView _list;
    private readonly Button _openButton;
    private readonly TextBox _statusBox;

    public SearchResultsForm(DropboxClient client, string refreshToken, string appKey, string query)
    {
        _client = client;
        _refreshToken = refreshToken;
        _appKey = appKey;
        _query = query;

        Text = $"ClearVault - Search results for \"{query}\"";
        ClientSize = new Size(700, 420);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 320);

        _list = new ListView
        {
            Left = 12,
            Top = 12,
            Width = 676,
            Height = 300,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AccessibleName = $"Search results for {query}",
        };
        _list.Columns.Add("Name", 220);
        _list.Columns.Add("Type", 70);
        _list.Columns.Add("Folder", 260);
        _list.Columns.Add("Modified", 120);
        _list.DoubleClick += (_, _) => OpenSelected();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                OpenSelected();
            }
        };
        _list.SelectedIndexChanged += (_, _) => UpdateButtonStates();

        _openButton = new Button
        {
            Text = "&Open",
            Left = 12,
            Top = _list.Bottom + 12,
            Width = 100,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _openButton.Click += (_, _) => OpenSelected();

        var closeButton = new Button
        {
            Text = "&Close",
            Left = _openButton.Right + 8,
            Top = _openButton.Top,
            Width = 100,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            DialogResult = DialogResult.Cancel,
        };

        var statusCaption = new Label
        {
            Left = 12,
            Top = _openButton.Bottom + 12,
            Width = 676,
            Height = 20,
            Text = "Status:",
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        _statusBox = new TextBox
        {
            Left = 12,
            Top = statusCaption.Bottom + 4,
            Width = 676,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = "Status",
            TabStop = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        Controls.Add(_list);
        Controls.Add(_openButton);
        Controls.Add(closeButton);
        Controls.Add(statusCaption);
        Controls.Add(_statusBox);

        CancelButton = closeButton;
        UpdateButtonStates();

        Load += async (_, _) => await RunSearchAsync();
        HelpRequested += (_, e) => { HelpViewer.Open(); e.Handled = true; };
    }

    private Metadata? GetSelectedEntry() =>
        _list.SelectedItems.Count > 0 ? (Metadata)_list.SelectedItems[0].Tag! : null;

    private void UpdateButtonStates() => _openButton.Enabled = GetSelectedEntry() is { IsFile: true };

    private void SetControlsEnabled(bool enabled)
    {
        _openButton.Enabled = enabled && GetSelectedEntry() is { IsFile: true };
        _list.Enabled = true; // never disable the list itself -- see FileBrowserForm's note on focus theft
    }

    private void AnnounceStatus(string message) => AccessibleAnnouncer.Announce(_statusBox, message);

    private async Task RunSearchAsync()
    {
        SetControlsEnabled(false);
        AnnounceStatus($"Searching for \"{_query}\"...");
        try
        {
            var result = await _client.Files.SearchV2Async(_query);

            _list.Items.Clear();
            foreach (var match in result.Matches)
            {
                if (!match.Metadata.IsMetadata)
                {
                    continue;
                }

                var entry = match.Metadata.AsMetadata.Value;
                var item = new ListViewItem(entry.Name) { Tag = entry };
                var parent = GetParentDisplayPath(entry.PathLower);
                if (entry.IsFolder)
                {
                    item.SubItems.Add("Folder");
                    item.SubItems.Add(parent);
                    item.SubItems.Add("");
                }
                else
                {
                    item.SubItems.Add("File");
                    item.SubItems.Add(parent);
                    item.SubItems.Add(entry.AsFile.ServerModified.ToLocalTime().ToString("g"));
                }
                _list.Items.Add(item);
            }

            var suffix = result.HasMore ? " (more results exist; refine your search to narrow it down)" : "";
            if (_list.Items.Count == 0)
            {
                AnnounceStatus($"No results for \"{_query}\".");
            }
            else
            {
                AnnounceStatus($"Found {_list.Items.Count} result(s) for \"{_query}\"{suffix}.");
                _list.Items[0].Selected = true;
                _list.Items[0].Focused = true;
                _list.Focus();
            }
        }
        catch (Exception ex)
        {
            AnnounceStatus($"Search failed: {ex.Message}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private static string GetParentDisplayPath(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        var parent = lastSlash > 0 ? path[..lastSlash] : "";
        return parent == "" ? "/ (root)" : parent;
    }

    private async void OpenSelected()
    {
        if (GetSelectedEntry() is not { IsFile: true } entry)
        {
            AnnounceStatus("Select a file to open (folders aren't openable here -- browse to them instead).");
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
            AnnounceStatus($"Couldn't open {file.Name}: {ex.Message}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }
}
