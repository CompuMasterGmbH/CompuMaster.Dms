using global::Avalonia;
using global::Avalonia.Headless;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(CompuMaster.Dms.Test.BrowserUI.Avalonia.TestAppBuilder))]

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

public sealed class TestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApplication>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
