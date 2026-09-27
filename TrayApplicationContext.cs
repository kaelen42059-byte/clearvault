namespace ClearVault;

// Owns the app's actual lifetime now: ClearVault stays running in the tray
// after the main window is closed, so background change notifications keep
// working. The tray icon's context menu is a plain native ContextMenuStrip --
// unlike Dropbox's own tray flyout, this is a real accessible Windows menu,
// readable with arrow keys the normal way.
sealed class TrayApplicationContext : ApplicationContext
{
    // NotifyIcon.Text throws if set to anything longer than this.
    private const int MaxTrayTooltipLength = 127;

    private readonly NotifyIcon _trayIcon;
    private readonly MainForm _mainForm;
    private readonly ChangeNotifier _changeNotifier;
    private readonly ToolStripItem _statusItem;
    private readonly ToolStripItem _lastChangeItem;

    public TrayApplicationContext()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.Load(),
            Text = "ClearVault",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainForm();

        _changeNotifier = new ChangeNotifier(_trayIcon);
        _mainForm = new MainForm(_changeNotifier);

        var menu = new ContextMenuStrip();

        // Status lines first, so arrowing into the menu reads the current
        // state straight away. Selecting either one just opens the window.
        _statusItem = menu.Items.Add("");
        _statusItem.Click += (_, _) => ShowMainForm();
        _lastChangeItem = menu.Items.Add("");
        _lastChangeItem.Click += (_, _) => ShowMainForm();

        menu.Items.Add(new ToolStripSeparator());

        var openItem = menu.Items.Add("&Open ClearVault");
        openItem.Click += (_, _) => ShowMainForm();

        menu.Items.Add(new ToolStripSeparator());

        var checkNowItem = menu.Items.Add("&Check for changes now");
        checkNowItem.Click += async (_, _) => await _changeNotifier.CheckNowAsync();

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = menu.Items.Add("E&xit ClearVault");
        exitItem.Click += (_, _) => ExitApplication();

        _trayIcon.ContextMenuStrip = menu;

        _changeNotifier.StatusChanged += (_, _) => UpdateTrayStatus();
        UpdateTrayStatus();
        _changeNotifier.Start();

        ShowMainForm();
    }

    // The tooltip is what a screen reader reads when arrowing onto the icon
    // in the notification area, so the sync status lives there too.
    private void UpdateTrayStatus()
    {
        var tooltip = $"ClearVault: {_changeNotifier.Status}";
        _trayIcon.Text = tooltip.Length > MaxTrayTooltipLength
            ? tooltip[..(MaxTrayTooltipLength - 3)] + "..."
            : tooltip;

        // "&&" so a literal ampersand in a status or file name isn't treated
        // as a menu access key.
        _statusItem.Text = $"Status: {_changeNotifier.Status}".Replace("&", "&&");
        _lastChangeItem.Text = $"Last change: {_changeNotifier.LastChange ?? "none since ClearVault started"}".Replace("&", "&&");
    }

    private void ShowMainForm()
    {
        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
    }

    private void ExitApplication()
    {
        _changeNotifier.Stop();
        _changeNotifier.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _mainForm.AllowClose = true;
        _mainForm.Close();
        ExitThread();
    }
}
