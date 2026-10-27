using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CompuMaster.Dms.TestDemo.WinUI;

internal static class DemoStartup
{
    internal static void Run(DemoTarget target, string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parameters =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new DemoApp(target, target == DemoTarget.Offline || args.Contains("--offline"));
        });
    }
}
/// <summary>Initializes the native WinUI demo application.</summary>
public sealed partial class DemoApp : Application
{
    private readonly DemoTarget target;
    private readonly bool offline;
    private Window? window;
    internal DemoApp(DemoTarget target, bool offline)
    {
        this.target = target; this.offline = offline;
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var definition = new DemoDefinition(target);
        if (offline)
        {
            var browser = new BrowserUI.WinUI.DmsBrowser(new DemoDmsProvider { ProviderKind = definition.Provider }, definition.BrowserOptions(offline: true));
            window = browser; _ = browser.ShowAsync();
        }
        else { window = new DemoLoginWindow(definition); window.Activate(); }
        window.Closed += (_, _) => Exit();
    }
}
