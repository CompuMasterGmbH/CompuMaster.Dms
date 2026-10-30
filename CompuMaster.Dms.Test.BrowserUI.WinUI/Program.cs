using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NUnitLite;

namespace CompuMaster.Dms.Test.BrowserUI.WinUI;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var notice = new BrowserUI.DevelopmentGuiTestSession();
        notice.StartNotice();
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Console.WriteLine("Starting native WinUI test application.");
            Microsoft.UI.Xaml.Application.Start(parameters =>
            {
                try
                {
                    Console.WriteLine("WinUI dispatcher initialized.");
                    SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                    _ = new TestApp(args);
                }
                catch (Exception ex) { Console.Error.WriteLine("WinUI application construction failed: " + ex); Environment.ExitCode = 1; }
            });
        }
        finally { notice.StopNotice(); }
    }
}
public sealed partial class TestApp : Microsoft.UI.Xaml.Application
{
    private readonly string[] args;
    private Exception? runtimeFailure;
    private Microsoft.UI.Xaml.Window? window;
    internal TestApp(string[] args)
    {
        this.args = args;
        InitializeComponent();
        UnhandledException += (_, e) => { runtimeFailure ??= e.Exception; Console.Error.WriteLine("WinUI runtime failure: " + e.Exception); };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs arguments)
    {
        try { LaunchTests(); }
        catch (Exception ex) { Console.Error.WriteLine("WinUI test host launch failed: " + ex); Environment.ExitCode = 1; Exit(); }
    }
    private void LaunchTests()
    {
        Console.WriteLine("Creating native WinUI test host window.");
        var body = new Grid(); window = new Microsoft.UI.Xaml.Window { Title = "DMS WinUI offline test host", Content = body };
        UiThread.Queue = DispatcherQueue.GetForCurrentThread(); UiThread.Owner = window;
        var started = false;
        body.Loaded += async (_, _) =>
        {
            if (started) return; started = true;
            try { var result = await Task.Run(() => new AutoRun(typeof(TestApp).Assembly).Execute(args)); Environment.ExitCode = runtimeFailure is null ? result : 1; }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { window.Close(); Exit(); }
        };
        window.Activate();
    }
}
internal static class UiThread
{
    internal static DispatcherQueue Queue = null!;
    internal static Microsoft.UI.Xaml.Window Owner = null!;
    internal static Task Run(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!Queue.TryEnqueue(async () =>
        {
            try { await action(); completion.TrySetResult(); }
            catch (Exception ex) { completion.TrySetException(ex); }
        })) completion.TrySetException(new InvalidOperationException("WinUI dispatcher is unavailable."));
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
    }
}
