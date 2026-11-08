using System.Diagnostics;

namespace FileLockFinder;

/// <summary>
/// Listens for Explorer's "File In Use" / "Folder In Use" dialog using WinEvent hooks
/// (event-driven, no polling). Must be created on a thread with a message loop.
/// </summary>
internal sealed class DialogWatcher : IDisposable
{
    private const uint EVENT_SYSTEM_DIALOGSTART = 0x0010;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private static readonly string[] TitleKeywords = ["in use", "kullanım", "kullanim"];

    private readonly NativeMethods.WinEventDelegate _callback; // keep alive: native code holds a pointer
    private readonly List<IntPtr> _hooks = [];
    private readonly Dictionary<IntPtr, DateTime> _handled = [];

    public event Action<IntPtr>? InUseDialogShown;

    public DialogWatcher()
    {
        _callback = OnWinEvent;
        foreach (uint evt in new[] { EVENT_SYSTEM_DIALOGSTART, EVENT_OBJECT_SHOW, EVENT_OBJECT_NAMECHANGE })
        {
            IntPtr hook = NativeMethods.SetWinEventHook(evt, evt, IntPtr.Zero, _callback, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            if (hook != IntPtr.Zero) _hooks.Add(hook);
        }
    }

    private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != 0 || idChild != 0) return;
        if (!IsInUseDialog(hwnd)) return;

        // Each dialog is reported once.
        foreach (var stale in _handled.Keys.Where(h => !NativeMethods.IsWindow(h)).ToList())
            _handled.Remove(stale);
        if (!_handled.TryAdd(hwnd, DateTime.UtcNow)) return;

        InUseDialogShown?.Invoke(hwnd);
    }

    private static bool IsInUseDialog(IntPtr hwnd)
    {
        if (NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT) != hwnd) return false;

        string title = NativeMethods.GetText(hwnd).ToLowerInvariant();
        if (title.Length == 0 || !TitleKeywords.Any(title.Contains)) return false;

        // Exclude normal Explorer folder windows (their title is the folder name).
        string cls = NativeMethods.GetClass(hwnd);
        if (cls is "CabinetWClass" or "ExploreWClass") return false;
        if (cls != "#32770" && NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) == IntPtr.Zero) return false;

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public void Dispose()
    {
        foreach (var hook in _hooks) NativeMethods.UnhookWinEvent(hook);
        _hooks.Clear();
    }
}
