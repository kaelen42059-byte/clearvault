namespace ClearVault;

// Both checkboxes save immediately on change (no separate OK/Save step) --
// consistent with how the rest of the app avoids "did I remember to save"
// ambiguity. Close is the only button because there's nothing to discard.
sealed class PreferencesForm : Form
{
    public PreferencesForm()
    {
        Text = "ClearVault - Preferences";
        ClientSize = new Size(480, 260);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var directDownloadCheckBox = new CheckBox
        {
            Text = "&Direct-download links (adds dl=1 to copied share links)",
            Left = 12,
            Top = 12,
            Width = 456,
            Checked = AppSettings.DirectDownloadLinks,
        };
        directDownloadCheckBox.CheckedChanged += (_, _) =>
            AppSettings.DirectDownloadLinks = directDownloadCheckBox.Checked;

        var teamIntegrationCheckBox = new CheckBox
        {
            Text = "&Enable Team integration (browse Dropbox Business/Team space folders)",
            Left = 12,
            Top = directDownloadCheckBox.Bottom + 12,
            Width = 456,
            Height = 36,
            Checked = AppSettings.TeamIntegrationEnabled,
        };
        teamIntegrationCheckBox.CheckedChanged += (_, _) =>
            AppSettings.TeamIntegrationEnabled = teamIntegrationCheckBox.Checked;

        var teamIntegrationNote = new Label
        {
            Text = "Only applies if your Dropbox account is a member of a Business/Team " +
                   "account with a team space. Does nothing for a personal account.",
            Left = 12,
            Top = teamIntegrationCheckBox.Bottom + 4,
            Width = 456,
            Height = 36,
            ForeColor = SystemColors.GrayText,
        };

        var notificationsCheckBox = new CheckBox
        {
            Text = "&Notify me about Dropbox changes (tray balloon, checked about every 90 seconds)",
            Left = 12,
            Top = teamIntegrationNote.Bottom + 12,
            Width = 456,
            Height = 36,
            Checked = AppSettings.BackgroundNotifications,
        };
        notificationsCheckBox.CheckedChanged += (_, _) =>
            AppSettings.BackgroundNotifications = notificationsCheckBox.Checked;

        var notificationsNote = new Label
        {
            Text = "ClearVault now keeps running in the tray after you close this window, so it can keep " +
                   "checking. Turning this off stops the notifications, not the background running -- use " +
                   "\"Exit ClearVault\" from the tray icon's menu to fully quit.",
            Left = 12,
            Top = notificationsCheckBox.Bottom + 4,
            Width = 456,
            Height = 48,
            ForeColor = SystemColors.GrayText,
        };

        var closeButton = new Button
        {
            Text = "&Close",
            Left = 12,
            Top = notificationsNote.Bottom + 12,
            Width = 100,
            DialogResult = DialogResult.OK,
        };

        Controls.Add(directDownloadCheckBox);
        Controls.Add(teamIntegrationCheckBox);
        Controls.Add(teamIntegrationNote);
        Controls.Add(notificationsCheckBox);
        Controls.Add(notificationsNote);
        Controls.Add(closeButton);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        HelpRequested += (_, e) => { HelpViewer.Open(); e.Handled = true; };
    }
}
