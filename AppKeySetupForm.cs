namespace ClearVault;

// First-run dialog: the user must register a Dropbox API app under their own
// account and paste the resulting App key here. This can't be automated --
// it requires their live, logged-in Dropbox session in a real browser.
sealed class AppKeySetupForm : Form
{
    private readonly TextBox _appKeyBox;
    public string EnteredAppKey { get; private set; } = "";

    public AppKeySetupForm()
    {
        Text = "ClearVault - First-time setup";
        ClientSize = new Size(560, 300);
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var instructions = new Label
        {
            Left = 12,
            Top = 12,
            Width = 536,
            Height = 150,
            Text =
                "ClearVault needs a Dropbox API app key before it can sign you in.\r\n\r\n" +
                "1. Open https://www.dropbox.com/developers/apps in your browser and sign in.\r\n" +
                "2. Choose Create app, then Scoped access, then Full Dropbox (or App folder), " +
                "give it any name, and create it.\r\n" +
                "3. On the app's Settings tab, find \"Redirect URIs\", type this exact address into " +
                "the box, and press the Add button next to it (this saves it immediately -- there is " +
                "no separate Submit button for this field):\r\n" +
                "   http://127.0.0.1:52874/clearvault/\r\n" +
                "4. On the Permissions tab, enable: account_info.read, files.metadata.read, " +
                "files.metadata.write, files.content.read, files.content.write, sharing.read, " +
                "sharing.write. Press Submit.\r\n" +
                "5. Back on the Settings tab, copy the \"App key\" value and paste it below.",
        };

        var appKeyLabel = new Label
        {
            Left = 12,
            Top = instructions.Bottom + 8,
            Width = 100,
            Height = 23,
            Text = "App &key:",
        };

        _appKeyBox = new TextBox
        {
            Left = appKeyLabel.Right + 8,
            Top = appKeyLabel.Top - 2,
            Width = 420,
            AccessibleName = "Dropbox App key",
        };

        var okButton = new Button
        {
            Text = "&Save and continue",
            Left = 12,
            Top = appKeyLabel.Bottom + 16,
            Width = 160,
            DialogResult = DialogResult.OK,
        };
        okButton.Click += (_, _) =>
        {
            var value = _appKeyBox.Text.Trim();
            if (value.Length == 0)
            {
                MessageBox.Show(this, "Please paste your Dropbox App key first.", "App key required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            EnteredAppKey = value;
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = okButton.Right + 8,
            Top = okButton.Top,
            Width = 100,
            DialogResult = DialogResult.Cancel,
        };

        Controls.Add(instructions);
        Controls.Add(appKeyLabel);
        Controls.Add(_appKeyBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}
