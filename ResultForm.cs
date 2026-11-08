using System.Diagnostics;

namespace FileLockFinder;

/// <summary>Shows which processes are using the file(s), with buttons to end them or open their location.</summary>
internal sealed class ResultForm : Form
{
    private readonly Label _header = new()
    {
        Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 6), AutoEllipsis = true,
    };
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
    };
    private readonly Button _kill = new() { Text = Lang.T("İşlemi Sonlandır", "End Process"), AutoSize = true };
    private readonly Button _locate = new() { Text = Lang.T("Konumu Aç", "Open Location"), AutoSize = true };
    private readonly Button _refresh = new() { Text = Lang.T("Yenile", "Refresh"), AutoSize = true };
    private readonly Button _close = new() { Text = Lang.T("Kapat", "Close"), AutoSize = true };

    private List<string> _paths = [];

    public ResultForm()
    {
        Text = Lang.T("Dosyayı kim kullanıyor?", "Who is using this file?");
        Font = SystemFonts.MessageBoxFont;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(820, 300);
        MinimumSize = new Size(500, 200);
        Icon = SystemIcons.Shield;

        _list.Columns.Add("Program", 170);
        _list.Columns.Add("PID", 60);
        _list.Columns.Add(Lang.T("Kullanılan öğe", "Item in use"), 170);
        _list.Columns.Add(Lang.T("Neden", "Reason"), 200);
        _list.Columns.Add(Lang.T("Exe yolu", "Executable path"), 400);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6),
        };
        buttons.Controls.AddRange([_close, _refresh, _locate, _kill]);

        // Fill control is added first so it docks last.
        Controls.Add(_list);
        Controls.Add(buttons);
        Controls.Add(_header);

        _list.SelectedIndexChanged += (_, _) => UpdateButtons();
        _list.DoubleClick += (_, _) => OpenLocation();
        _kill.Click += async (_, _) => await KillSelectedAsync();
        _locate.Click += (_, _) => OpenLocation();
        _refresh.Click += async (_, _) => await RefreshAsync();
        _close.Click += (_, _) => Hide();

        // Keep a single instance alive: closing just hides the window.
        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
    }

    public void ShowResults(IReadOnlyList<string> paths, List<LockingProcess> lockers, Rectangle? near)
    {
        _paths = [.. paths];
        _header.Text = paths.Count == 1
            ? paths[0]
            : $"{paths.Count} {Lang.T("öğe", "items")}: " + string.Join(", ", paths.Select(Path.GetFileName));

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var l in lockers)
        {
            string item = Path.GetFileName(Path.TrimEndingDirectorySeparator(l.LockedPath));
            _list.Items.Add(new ListViewItem([l.AppName, l.Pid.ToString(), item, l.Reason, l.ExePath ?? Lang.T("(erişilemedi)", "(access denied)")])
            {
                Tag = l,
            });
        }
        if (lockers.Count == 0)
        {
            _list.Items.Add(new ListViewItem([Lang.T("Kullanan işlem bulunamadı", "No process found"), "", "",
                Lang.T("Kilit kalkmış olabilir ya da işlem yönetici yetkisiyle çalışıyor olabilir",
                       "The lock may be gone, or the process may be running as administrator"), ""]));
        }
        else
        {
            _list.Items[0].Selected = true;
        }
        _list.EndUpdate();

        if (near is Rectangle r) PlaceNear(r);
        if (!Visible) Show();
        WindowState = FormWindowState.Normal;
        Activate();
        UpdateButtons();
    }

    private void PlaceNear(Rectangle target)
    {
        Rectangle area = Screen.FromRectangle(target).WorkingArea;
        int x = target.Right + 8;
        if (x + Width > area.Right) x = target.Left - Width - 8;   // no room on the right -> left side
        if (x < area.Left) x = area.Left;
        int y = Math.Clamp(target.Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
        Location = new Point(x, y);
    }

    private LockingProcess? Selected => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as LockingProcess : null;

    private void UpdateButtons()
    {
        _kill.Enabled = Selected != null;
        _locate.Enabled = Selected?.ExePath != null;
    }

    private async Task KillSelectedAsync()
    {
        if (Selected is not LockingProcess p) return;

        var answer = MessageBox.Show(this,
            Lang.T($"{p.AppName} (PID {p.Pid}) sonlandırılsın mı?\n\nKaydedilmemiş veriler kaybolabilir.",
                   $"End {p.AppName} (PID {p.Pid})?\n\nUnsaved data may be lost."),
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            using var proc = Process.GetProcessById(p.Pid);
            proc.Kill();
            await Task.Run(() => proc.WaitForExit(3000));
        }
        catch (ArgumentException) { /* already exited */ }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                Lang.T($"İşlem sonlandırılamadı: {ex.Message}\n\nBu programı yönetici olarak çalıştırmayı deneyin.",
                       $"Could not end the process: {ex.Message}\n\nTry running this program as administrator."),
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        await RefreshAsync();
    }

    private void OpenLocation()
    {
        if (Selected?.ExePath is not string exe) return;
        Process.Start("explorer.exe", $"/select,\"{exe}\"");
    }

    private async Task RefreshAsync()
    {
        UseWaitCursor = true;
        try
        {
            var paths = _paths;
            var lockers = await Task.Run(() => LockFinder.FindMany(paths));
            if (IsDisposed) return; // window was closed (e.g. language changed) while searching
            ShowResults(paths, lockers, null);
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (!IsDisposed) UseWaitCursor = false;
        }
    }
}
