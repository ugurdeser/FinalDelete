namespace FileLockFinder;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Allow only one instance in the background.
        using var mutex = new Mutex(true, @"Local\FileLockFinder_SingleInstance", out bool isNew);
        if (!isNew) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
