using CompuMaster.Dms.BrowserUI.Avalonia;

// Runs on the process main thread so AppKit is exercised correctly on macOS,
// without a DMS profile, file download, window or GUI automation.
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        foreach (var extension in new[] { ".pdf", ".csv", ".txt", ".zip", ".dms-unknown-extension" })
        {
            var icon = PlatformFileIcons.Read(extension, 48);
            var pixels = icon is { Width: > 0, Height: > 0 } && icon.Data.Length == icon.Width * icon.Height * 4 &&
                Enumerable.Range(0, icon.Width * icon.Height).Any(i => icon.Data[i * 4 + 3] != 0);
            var png = icon is { Width: 0 } && icon.Data.Length > 24 &&
                icon.Data.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            if (!pixels && !png)
            {
                Console.Error.WriteLine($"Native icon backend did not return a usable {extension} icon on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}.");
                return 1;
            }
            Console.WriteLine($"Native association icon {extension}: passed ({(png ? "PNG" : "BGRA")}).");
        }
        return 0;
    }
}
