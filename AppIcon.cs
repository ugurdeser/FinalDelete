namespace FileLockFinder;

/// <summary>Loads the embedded app icon at the requested size.</summary>
internal static class AppIcon
{
    public static Icon Get(Size size)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("FileLockFinder.app.ico");
        return stream != null ? new Icon(stream, size) : SystemIcons.Shield;
    }
}
