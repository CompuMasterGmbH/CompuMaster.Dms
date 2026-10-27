using System.Diagnostics;
using CompuMaster.Dms.Test.BrowserUI;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("An executable path is required.");
        var notice = new DevelopmentGuiTestSession();
        try
        {
            notice.StartNotice();
            var start = new ProcessStartInfo(Path.GetFullPath(args[0])) { UseShellExecute = false };
            foreach (var argument in args.Skip(1)) start.ArgumentList.Add(argument);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("The GUI test application could not be started.");
            process.WaitForExit(); return process.ExitCode;
        }
        finally { notice.StopNotice(); }
    }
}
