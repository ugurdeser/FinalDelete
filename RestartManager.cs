using System.Runtime.InteropServices;
using System.Text;

namespace FileLockFinder;

/// <summary>Wraps the Windows Restart Manager API, which reports which processes hold a file open.</summary>
internal static class RestartManager
{
    private const int ERROR_MORE_DATA = 234;
    private const int CCH_RM_SESSION_KEY = 32;

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)] public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint sessionHandle, int flags, StringBuilder sessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint sessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint sessionHandle, uint nFiles, string[] fileNames,
        uint nApplications, RM_UNIQUE_PROCESS[]? applications, uint nServices, string[]? serviceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint sessionHandle, out uint procInfoNeeded, ref uint procInfo,
        [In, Out] RM_PROCESS_INFO[]? affectedApps, ref uint rebootReasons);

    public static List<(int Pid, string AppName)> GetLockers(string[] files)
    {
        var result = new List<(int, string)>();
        if (files.Length == 0) return result;
        if (RmStartSession(out uint session, 0, new StringBuilder(CCH_RM_SESSION_KEY + 1)) != 0) return result;

        try
        {
            if (RmRegisterResources(session, (uint)files.Length, files, 0, null, 0, null) != 0) return result;

            uint count = 0, reasons = 0;
            RM_PROCESS_INFO[]? infos = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                int rc = RmGetList(session, out uint needed, ref count, infos, ref reasons);
                if (rc == 0)
                {
                    for (int i = 0; i < count; i++)
                        result.Add((infos![i].Process.dwProcessId, infos[i].strAppName));
                    break;
                }
                if (rc != ERROR_MORE_DATA) break;
                infos = new RM_PROCESS_INFO[needed];
                count = needed;
            }
        }
        finally { RmEndSession(session); }

        return result;
    }
}
