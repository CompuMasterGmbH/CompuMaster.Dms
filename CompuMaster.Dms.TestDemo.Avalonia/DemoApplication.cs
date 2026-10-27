using global::Avalonia;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Themes.Fluent;

namespace CompuMaster.Dms.TestDemo.Avalonia;

internal static class DemoStartup
{
    internal static DemoTarget Target { get; private set; }
    internal static bool Offline { get; private set; }
    internal static void Run(DemoTarget target, string[] args)
    {
        Target = target; Offline = target == DemoTarget.Offline || args.Contains("--offline");
        AppBuilder.Configure<DemoApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}
internal sealed class DemoApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = DemoWindows.Create(DemoStartup.Target, DemoStartup.Offline);
        base.OnFrameworkInitializationCompleted();
    }
}
