using System.Diagnostics;

namespace FileLockFinder;

internal sealed record LockingProcess(int Pid, string AppName, string? ExePath, string Reason, string LockedPath);

/// <summary>Finds which processes are keeping a file or folder in use.</summary>
internal static class LockFinder
{
    private const int PerFileThreshold = 300; // up to this many files, report exactly which file is locked
    private const int MaxFiles = 20000;
    private const int ChunkSize = 500;

    public static List<LockingProcess> FindMany(IEnumerable<string> paths) => paths.SelectMany(Find).ToList();

    public static List<LockingProcess> Find(string path)
    {
        var found = new Dictionary<int, LockingProcess>();

        if (File.Exists(path))
        {
            foreach (var (pid, name) in RestartManager.GetLockers([path]))
                Add(found, pid, name, Lang.T("Dosyayı açık tutuyor", "Has the file open"), path);
        }
        else if (Directory.Exists(path))
        {
            var files = EnumerateFiles(path).Take(MaxFiles).ToList();
            if (files.Count <= PerFileThreshold)
            {
                foreach (var file in files)
                    foreach (var (pid, name) in RestartManager.GetLockers([file]))
                        Add(found, pid, name, Lang.T("İçindeki bir dosyayı açık tutuyor", "Has a file inside open"), file);
            }
            else
            {
                foreach (var chunk in files.Chunk(ChunkSize))
                    foreach (var (pid, name) in RestartManager.GetLockers(chunk))
                        Add(found, pid, name, Lang.T("Klasördeki dosyalardan birini açık tutuyor", "Has a file in this folder open"), path);
            }

            AddProcessesRunningInside(path, found);
        }

        return found.Values.OrderBy(l => l.AppName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Processes whose exe or working directory is inside the folder (e.g. an open terminal).</summary>
    private static void AddProcessesRunningInside(string path, Dictionary<int, LockingProcess> found)
    {
        string folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        int self = Environment.ProcessId;

        foreach (var proc in Process.GetProcesses())
        {
            using (proc)
            {
                int pid = proc.Id;
                if (pid <= 4 || pid == self || found.ContainsKey(pid)) continue;

                string? exe = NativeMethods.GetProcessImagePath(pid);
                if (exe != null && IsInside(exe, folder))
                {
                    Add(found, pid, proc.ProcessName, Lang.T("Programın kendisi bu klasörden çalışıyor", "Program runs from this folder"), exe);
                    continue;
                }

                string? cwd = NativeMethods.GetProcessCurrentDirectory(pid);
                if (cwd != null && IsInside(cwd, folder))
                    Add(found, pid, proc.ProcessName, Lang.T("Çalışma dizini bu klasörde", "Working directory is in this folder"), cwd);
            }
        }
    }

    private static bool IsInside(string candidate, string folder)
    {
        candidate = Path.TrimEndingDirectorySeparator(candidate);
        return candidate.Equals(folder, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateFiles(string folder)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        return Directory.EnumerateFiles(folder, "*", options);
    }

    private static void Add(Dictionary<int, LockingProcess> found, int pid, string name, string reason, string lockedPath)
    {
        if (found.ContainsKey(pid)) return;
        if (string.IsNullOrWhiteSpace(name))
        {
            try { using var p = Process.GetProcessById(pid); name = p.ProcessName; }
            catch { name = "?"; }
        }
        found[pid] = new LockingProcess(pid, name, NativeMethods.GetProcessImagePath(pid), reason, lockedPath);
    }
}
