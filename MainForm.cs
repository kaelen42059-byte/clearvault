using ClearVault.Auth;

namespace ClearVault;

sealed class MainForm : Form
{
    private readonly TokenStore _tokenStore = new();
    private string? _appKey;

    // Set by TrayApplicationContext right before a real exit. Otherwise,
    // closing the window (the X button, Alt+F4) just hides it -- the app
    // keeps running in the tray so background notifications keep working.
    internal bool AllowClose;

    private readonly ListBox _accountList;
    private readonly Button _addAccountButton;
    private readonly Button _switchAccountButton;
    private readonly Button _removeAccountButton;
    private readonly Button _browseFilesButton;
    private readonly Button _preferencesButton;
    private readonly TextBox _statusBox;

    private sealed class AccountItem
    {
        public required StoredAccount Account { get; init; }
        public required bool IsActive { get; init; }
        public override string ToString() =>
            IsActive
                ? $"Active: {Account.DisplayName} ({Account.Email})"
                : $"{Account.DisplayName} ({Account.Email})";
    }

    public MainForm()
    {
        Text = "ClearVault";
        ClientSize = new Size(560, 400);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(480, 320);

        var accountsLabel = new Label
        {
            Left = 12,
            Top = 12,
            Width = 536,
            Height = 20,
            Text = "&Signed-in Dropbox accounts:",
        };

        _accountList = new ListBox
        {
            Left = 12,
            Top = accountsLabel.Bottom + 4,
            Width = 536,
            Height = 160,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            AccessibleName = "Signed-in Dropbox accounts",
        };
        accountsLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        _addAccountButton = new Button
        {
            Text = "&Add account...",
            Left = 12,
            Top = _accountList.Bottom + 12,
            Width = 160,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _addAccountButton.Click += AddAccountButton_Click;

        _switchAccountButton = new Button
        {
            Text = "&Switch to selected",
            Left = _addAccountButton.Right + 8,
            Top = _addAccountButton.Top,
            Width = 160,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _switchAccountButton.Click += SwitchAccountButton_Click;

        _removeAccountButton = new Button
        {
            Text = "&Remove (sign out)",
            Left = _switchAccountButton.Right + 8,
            Top = _addAccountButton.Top,
            Width = 160,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _removeAccountButton.Click += RemoveAccountButton_Click;

        _browseFilesButton = new Button
        {
            Text = "&Browse files...",
            Left = 12,
            Top = _addAccountButton.Bottom + 8,
            Width = 160,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _browseFilesButton.Click += BrowseFilesButton_Click;

        _preferencesButton = new Button
        {
            Text = "&Preferences...",
            Left = _browseFilesButton.Right + 8,
            Top = _browseFilesButton.Top,
            Width = 160,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
        };
        _preferencesButton.Click += (_, _) =>
        {
            using var preferences = new PreferencesForm();
            preferences.ShowDialog(this);
        };

        var statusLabel = new Label
        {
            Left = 12,
            Top = _browseFilesButton.Bottom + 16,
            Width = 536,
            Height = 20,
            Text = "Status:",
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        _statusBox = new TextBox
        {
            Left = 12,
            Top = statusLabel.Bottom + 4,
            Width = 536,
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            AccessibleName = "Status",
            TabStop = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
        };

        Controls.Add(accountsLabel);
        Controls.Add(_accountList);
        Controls.Add(_addAccountButton);
        Controls.Add(_switchAccountButton);
        Controls.Add(_removeAccountButton);
        Controls.Add(_browseFilesButton);
        Controls.Add(_preferencesButton);
        Controls.Add(statusLabel);
        Controls.Add(_statusBox);

        Load += MainForm_Load;
        HelpRequested += (_, e) => { HelpViewer.Open(); e.Handled = true; };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {
        _appKey = AppConfig.LoadAppKey();
        if (_appKey is null)
        {
            using var setup = new AppKeySetupForm();
            if (setup.ShowDialog(this) != DialogResult.OK)
            {
                SetStatus("No Dropbox App key was provided. Press \"Add account\" to run setup again.");
                RefreshAccountList();
                return;
            }

            _appKey = setup.EnteredAppKey;
            AppConfig.SaveAppKey(_appKey);
        }

        RefreshAccountList();
        SetStatus(_tokenStore.Accounts.Count == 0
            ? "Not signed in. Press \"Add account\" to sign in to Dropbox."
            : $"Ready. {_tokenStore.Accounts.Count} account(s) signed in.");
    }

    private void RefreshAccountList()
    {
        _accountList.Items.Clear();
        foreach (var account in _tokenStore.Accounts)
        {
            _accountList.Items.Add(new AccountItem
            {
                Account = account,
                IsActive = account.AccountId == _tokenStore.ActiveAccountId,
            });
        }
    }

    private void SetStatus(string message) => AccessibleAnnouncer.Announce(_statusBox, message);

    private void SetControlsEnabled(bool enabled)
    {
        _addAccountButton.Enabled = enabled;
        _switchAccountButton.Enabled = enabled;
        _removeAccountButton.Enabled = enabled;
        _browseFilesButton.Enabled = enabled;
        _accountList.Enabled = enabled;
    }

    private async void AddAccountButton_Click(object? sender, EventArgs e)
    {
        if (_appKey is null)
        {
            return;
        }

        SetControlsEnabled(false);
        SetStatus("Opening your browser to sign in to Dropbox. Finish signing in there, then return here.");

        try
        {
            var result = await AuthService.SignInAsync(_appKey, CancellationToken.None);
            _tokenStore.AddOrUpdateAccount(result.AccountId, result.Email, result.DisplayName, result.RefreshToken);
            RefreshAccountList();
            SetStatus($"Signed in as {result.DisplayName} ({result.Email}). This is now the active account.");
        }
        catch (Exception ex)
        {
            SetStatus($"Sign-in failed: {ex.Message}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    private void SwitchAccountButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountItem item)
        {
            SetStatus("Select an account in the list first, then press \"Switch to selected\".");
            return;
        }

        _tokenStore.SetActiveAccount(item.Account.AccountId);
        RefreshAccountList();
        SetStatus($"Switched to {item.Account.DisplayName} ({item.Account.Email}).");
    }

    private void RemoveAccountButton_Click(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItem is not AccountItem item)
        {
            SetStatus("Select an account in the list first, then press \"Remove (sign out)\".");
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Sign out of {item.Account.DisplayName} ({item.Account.Email}) in ClearVault? " +
            "Your files stay safely in Dropbox -- this only removes it from this app's account list.",
            "Confirm sign out",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            return;
        }

        _tokenStore.RemoveAccount(item.Account.AccountId);
        RefreshAccountList();
        SetStatus($"Signed out of {item.Account.Email}.");
    }

    private async void BrowseFilesButton_Click(object? sender, EventArgs e)
    {
        if (_appKey is null)
        {
            return;
        }

        var account = _tokenStore.ActiveAccount;
        if (account is null)
        {
            SetStatus("No active account. Add or switch to an account first, then press \"Browse files\".");
            return;
        }

        SetControlsEnabled(false);
        try
        {
            using var client = await ClientFactory.CreateForBrowsingAsync(_tokenStore, account, _appKey);
            var refreshToken = _tokenStore.GetRefreshToken(account);
            using var browser = new FileBrowserForm(client, refreshToken, _appKey, $"{account.DisplayName} ({account.Email})");
            browser.ShowDialog(this);
        }
        catch (Exception ex)
        {
            SetStatus($"Couldn't open the file browser: {ex.Message}");
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }
}
