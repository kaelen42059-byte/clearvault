namespace ClearVault;

// Small reusable "type one line of text" dialog -- used for New Folder name,
// Rename, and Move-to-path. .NET has no built-in input box, and this keeps
// every prompt in the app using the same plain, fully-labeled controls.
sealed class TextInputForm : Form
{
    public string InputText { get; private set; } = "";

    public TextInputForm(string title, string prompt, string initialValue = "")
    {
        Text = title;
        ClientSize = new Size(440, 130);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var label = new Label
        {
            Text = prompt,
            Left = 12,
            Top = 12,
            Width = 416,
            Height = 20,
        };

        var textBox = new TextBox
        {
            Left = 12,
            Top = label.Bottom + 4,
            Width = 416,
            Text = initialValue,
            AccessibleName = prompt,
        };

        var okButton = new Button
        {
            Text = "&OK",
            Left = 12,
            Top = textBox.Bottom + 16,
            Width = 100,
            DialogResult = DialogResult.OK,
        };
        okButton.Click += (_, _) =>
        {
            var value = textBox.Text.Trim();
            if (value.Length == 0)
            {
                MessageBox.Show(this, "Please enter a value.", "Value required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            InputText = value;
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Left = okButton.Right + 8,
            Top = okButton.Top,
            Width = 100,
            DialogResult = DialogResult.Cancel,
        };

        Controls.Add(label);
        Controls.Add(textBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        ActiveControl = textBox;
        textBox.SelectAll();
    }
}
