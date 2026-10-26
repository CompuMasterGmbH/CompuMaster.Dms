using System.Globalization;
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Providers;
using global::Avalonia;
using global::Avalonia.Headless;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.Themes.Fluent;
using NUnit.Framework;

[assembly: AvaloniaTestApplication(typeof(Consumer.AppBuilderFactory))]

namespace Consumer;

public sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
}
public static class AppBuilderFactory
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
public sealed class PackageTests
{
    [AvaloniaTest]
    public void Installed_package_loads_icon_datagrid_styles_and_German_resources()
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        try
        {
            var window = new DmsBrowser(new NoDmsProvider());
            Assert.That(window.Title, Is.EqualTo("DMS Ordner durchsuchen"));
            Assert.That(window.Icon, Is.Not.Null);
            Assert.That(window.Browser, Is.Not.Null);
            Assert.That(typeof(DmsBrowser).Assembly.GetReferencedAssemblies()
                .Any(d => d.Name is "System.Windows.Forms" or "System.Drawing.Common" or "CompuMaster.Dms.BrowserUI"), Is.False);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
