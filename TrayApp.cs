using Microsoft.Win32;

namespace FileLockFinder;

/// <summary>Tray icon + background watcher. Runs for the lifetime of the app.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "FileLockFinder";

    private readonly NotifyIcon _tray;
    private readonly DialogWatcher _watcher;
    private readonly Control _invoker = new(); // used to defer work out of the hook callback
    private ResultForm? _form;
    private bool _busy;

    public TrayApp()
    {
        _invoker.CreateControl();

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "File Lock Finder",
            ContextMenuStrip = BuildMenu(),
            Visible = true,
        };
        _tray.DoubleClick += async (_, _) => await CheckFileAsync();

        _watcher = new DialogWatcher();
        _watcher.InUseDialogShown += hwnd => _invoker.BeginInvoke(new Action(() => OnInUseDialog(hwnd)));

        _tray.ShowBalloonTip(2500, "File Lock Finder",
            Lang.T("Arka planda çalışıyor. \"Dosya kullanımda\" uyarısı çıktığında kimin kullandığını gösterecek.",
                   "Running in the background. When a \"file in use\" warning appears, it will show who is using it."),
            ToolTipIcon.Info);
    }

    private ContextMenuStrip BuildMenu()
    {
        var startup = new ToolStripMenuItem(Lang.T("Windows ile başlat", "Start with Windows"))
        {
            Checked = IsStartupEnabled(), CheckOnClick = true,
        };
        startup.CheckedChanged += (_, _) => SetStartup(startup.Checked);

        var language = new ToolStripMenuItem("Dil / Language");
        language.DropDownItems.Add(new ToolStripMenuItem("Türkçe", null, (_, _) => ChangeLanguage(false)) { Checked = !Lang.English });
        language.DropDownItems.Add(new ToolStripMenuItem("English", null, (_, _) => ChangeLanguage(true)) { Checked = Lang.English });

        var menu = new ContextMenuStrip();
        menu.Items.Add(Lang.T("Dosya kontrol et...", "Check file..."), null, async (_, _) => await CheckFileAsync());
        menu.Items.Add(Lang.T("Klasör kontrol et...", "Check folder..."), null, async (_, _) => await CheckFolderAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startup);
        menu.Items.Add(language);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Lang.T("Çıkış", "Exit"), null, (_, _) => ExitThread());
        return menu;
    }

    private void ChangeLanguage(bool english)
    {
        Lang.Set(english);
        var old = _tray.ContextMenuStrip;
        _tray.ContextMenuStrip = BuildMenu();
        old?.Dispose();
        // Recreate the result window in the new language next time it is shown.
        _form?.Dispose();
        _form = null;
    }

    private async void OnInUseDialog(IntPtr dialog)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            Rectangle? near = NativeMethods.GetWindowRect(dialog, out var r)
                ? Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)
                : null;

            var groups = ExplorerSelection.Collect(NativeMethods.GetWindow(dialog, NativeMethods.GW_OWNER));
            if (groups.Count == 0)
            {
                _tray.ShowBalloonTip(3000, "File Lock Finder",
                    Lang.T("Explorer'da seçili dosya bulunamadı. Tepsi menüsünden elle kontrol edebilirsiniz.",
                           "No selected file found in Explorer. You can check manually from the tray menu."), ToolTipIcon.Warning);
                return;
            }

            // Windows that own the dialog are checked first; others only if nothing was found.
            var ordered = groups.OrderByDescending(g => g.Preferred).ToList();
            var (paths, lockers) = await Task.Run(() =>
            {
                foreach (var g in ordered)
                {
                    var found = LockFinder.FindMany(g.Paths);
                    if (found.Count > 0) return (g.Paths, found);
                }
                return (ordered[0].Paths, new List<LockingProcess>());
            });

            ShowForm(paths, lockers, near);
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(3000, "File Lock Finder", Lang.T("Hata: ", "Error: ") + ex.Message, ToolTipIcon.Error);
        }
        finally { _busy = false; }
    }

    private async Task CheckFileAsync()
    {
        using var dlg = new OpenFileDialog { Title = Lang.T("Kontrol edilecek dosyayı seçin", "Select the file to check"), Multiselect = true };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        await CheckPathsAsync(dlg.FileNames);
    }

    private async Task CheckFolderAsync()
    {
        using var dlg = new FolderBrowserDialog { Description = Lang.T("Kontrol edilecek klasörü seçin", "Select the folder to check"), UseDescriptionForTitle = true };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        await CheckPathsAsync([dlg.SelectedPath]);
    }

    private async Task CheckPathsAsync(string[] paths)
    {
        var lockers = await Task.Run(() => LockFinder.FindMany(paths));
        ShowForm(paths, lockers, null);
    }

    private void ShowForm(IReadOnlyList<string> paths, List<LockingProcess> lockers, Rectangle? near)
    {
        _form ??= new ResultForm();
        _form.ShowResults(paths, lockers, near);
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) != null;
    }

    private static void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enable) key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    protected override void ExitThreadCore()
    {
        _watcher.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _form?.Dispose();
        _invoker.Dispose();
        base.ExitThreadCore();
    }
}
