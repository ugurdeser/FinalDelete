using System.Runtime.InteropServices;

namespace FileLockFinder;

/// <summary>Selected items of one Explorer window/tab (or the desktop).</summary>
internal sealed record SelectionGroup(bool Preferred, IReadOnlyList<string> Paths);

/// <summary>Reads which files are currently selected in Explorer windows and on the desktop.</summary>
internal static class ExplorerSelection
{
    /// <param name="dialogOwner">Owner of the "in use" dialog; matching windows are marked Preferred.</param>
    public static List<SelectionGroup> Collect(IntPtr dialogOwner)
    {
        IntPtr ownerRoot = dialogOwner == IntPtr.Zero
            ? IntPtr.Zero
            : NativeMethods.GetAncestor(dialogOwner, NativeMethods.GA_ROOTOWNER);

        var groups = new List<SelectionGroup>();

        // Explorer windows (each Windows 11 tab is a separate entry with the same HWND).
        try
        {
            Type? type = Type.GetTypeFromProgID("Shell.Application");
            if (type != null)
            {
                dynamic shell = Activator.CreateInstance(type)!;
                dynamic windows = shell.Windows();
                int count = windows.Count;
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        dynamic? window = windows.Item(i);
                        if (window is null) continue;
                        var hwnd = new IntPtr(Convert.ToInt64(window.HWND));
                        List<string> paths = ReadSelection(window.Document);
                        if (paths.Count > 0)
                            groups.Add(new SelectionGroup(hwnd == ownerRoot || hwnd == dialogOwner, paths));
                    }
                    catch { /* not a folder view (e.g. closed tab) */ }
                }
            }
        }
        catch { }

        // Desktop
        try
        {
            var paths = ReadDesktopSelection();
            if (paths.Count > 0)
            {
                string cls = NativeMethods.GetClass(ownerRoot);
                groups.Add(new SelectionGroup(cls is "Progman" or "WorkerW", paths));
            }
        }
        catch { }

        return groups;
    }

    private static List<string> ReadSelection(object folderView)
    {
        var list = new List<string>();
        dynamic view = folderView;
        dynamic items = view.SelectedItems();
        int count = items.Count;
        for (int i = 0; i < count; i++)
        {
            string? path = items.Item(i)?.Path;
            if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
                list.Add(path);
        }
        return list;
    }

    // ---- Desktop: IShellWindows.FindWindowSW -> IShellBrowser -> IShellView -> folder view dispatch ----

    private const int SWC_DESKTOP = 8;
    private const int SWFO_NEEDDISPATCH = 1;
    private const uint SVGIO_BACKGROUND = 0;
    private static readonly Guid SID_STopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid IID_IShellBrowser = new("000214E2-0000-0000-C000-000000000046");
    private static readonly Guid IID_IDispatch = new("00020400-0000-0000-C000-000000000046");

    private static List<string> ReadDesktopSelection()
    {
        var shellWindows = (IShellWindows)new ShellWindowsClass();
        object loc = 0; // CSIDL_DESKTOP
        object root = null!;
        object desktop = shellWindows.FindWindowSW(ref loc, ref root, SWC_DESKTOP, out _, SWFO_NEEDDISPATCH);

        var provider = (IServiceProvider)desktop;
        Guid sid = SID_STopLevelBrowser, iid = IID_IShellBrowser;
        if (provider.QueryService(ref sid, ref iid, out object browserObj) != 0) return [];

        var browser = (IShellBrowser)browserObj;
        browser.QueryActiveShellView(out IShellView view);

        Guid iidDispatch = IID_IDispatch;
        view.GetItemObject(SVGIO_BACKGROUND, ref iidDispatch, out object folderView);
        return ReadSelection(folderView);
    }

    [ComImport, Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")]
    private class ShellWindowsClass { }

    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IShellWindows
    {
        int Count { get; }
        [return: MarshalAs(UnmanagedType.IDispatch)] object Item([MarshalAs(UnmanagedType.Struct)] object index);
        [return: MarshalAs(UnmanagedType.IUnknown)] object _NewEnum();
        int Register([MarshalAs(UnmanagedType.IDispatch)] object pid, int hwnd, int swClass);
        int RegisterPending(int threadId, [MarshalAs(UnmanagedType.Struct)] ref object loc,
            [MarshalAs(UnmanagedType.Struct)] ref object locRoot, int swClass);
        void Revoke(int cookie);
        void OnNavigate(int cookie, [MarshalAs(UnmanagedType.Struct)] ref object loc);
        void OnActivated(int cookie, [MarshalAs(UnmanagedType.VariantBool)] bool active);
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object FindWindowSW([MarshalAs(UnmanagedType.Struct)] ref object loc,
            [MarshalAs(UnmanagedType.Struct)] ref object locRoot, int swClass, out int hwnd, int options);
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }

    // Only the vtable order matters for the methods we never call.
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void GetWindow(out IntPtr hwnd);
        void ContextSensitiveHelp(int enterMode);
        void InsertMenusSB(IntPtr hmenuShared, IntPtr widths);
        void SetMenuSB(IntPtr hmenuShared, IntPtr holemenuRes, IntPtr hwndActiveObject);
        void RemoveMenusSB(IntPtr hmenuShared);
        void SetStatusTextSB(IntPtr statusText);
        void EnableModelessSB(int enable);
        void TranslateAcceleratorSB(IntPtr msg, short id);
        void BrowseObject(IntPtr pidl, uint flags);
        void GetViewStateStream(uint mode, out IntPtr stream);
        void GetControlWindow(uint id, out IntPtr hwnd);
        void SendControlMsg(uint id, uint msg, IntPtr wParam, IntPtr lParam, out IntPtr result);
        void QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out IShellView view);
    }

    [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellView
    {
        void GetWindow(out IntPtr hwnd);
        void ContextSensitiveHelp(int enterMode);
        [PreserveSig] int TranslateAccelerator(IntPtr msg);
        void EnableModeless(int enable);
        void UIActivate(uint state);
        void Refresh();
        void CreateViewWindow(IntPtr previous, IntPtr folderSettings, IntPtr browser, IntPtr rect, out IntPtr hwnd);
        void DestroyViewWindow();
        void GetCurrentInfo(IntPtr folderSettings);
        void AddPropertySheetPages(uint reserved, IntPtr addPage, IntPtr lParam);
        void SaveViewState();
        void SelectItem(IntPtr pidl, uint flags);
        void GetItemObject(uint item, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    }
}
