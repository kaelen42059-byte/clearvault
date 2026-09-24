namespace ClearVault;

// Owns the app's actual lifetime now: ClearVault stays running in the tray
// after the main window is closed, so background change notifications keep
// working. The tray icon's context menu is a plain native ContextMenuStrip --
// unlike Dropbox's own tray flyout, this is a real accessible Windows menu,
// readable with arrow keys the normal way.
sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly MainForm _mainForm;
    private readonly ChangeNotifier _changeNotifier;

    public TrayApplicationContext()
    {
        _mainForm = new MainForm();

        _trayIcon = new NotifyIcon
        {
            Icon = AppIcon.Load(),
            Text = "ClearVault",
            Visible = true,
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainForm();

        _changeNotifier = new ChangeNotifier(_trayIcon);
        _changeNotifier.Start();

        var menu = new ContextMenuStrip();

        var openItem = menu.Items.Add("&Open ClearVault");
        openItem.Click += (_, _) => ShowMainForm();

        menu.Items.Add(new ToolStripSeparator());

        var checkNowItem = menu.Items.Add("&Check for changes now");
        checkNowItem.Click += async (_, _) => await _changeNotifier.CheckNowAsync();

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = menu.Items.Add("E&xit ClearVault");
        exitItem.Click += (_, _) => ExitApplication();

        _trayIcon.ContextMenuStrip = menu;

        ShowMainForm();
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
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _mainForm.AllowClose = true;
        _mainForm.Close();
        ExitThread();
    }
}
