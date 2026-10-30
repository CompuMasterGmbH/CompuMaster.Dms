using System.Globalization;
using System.Reflection;
using System.Resources;
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.TestDemo.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinUIPackageConsumer;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(parameters =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
    }
}
public sealed partial class App : Application
{
    private Exception? runtimeFailure;
    public App() { InitializeComponent(); UnhandledException += (_, e) => { runtimeFailure ??= e.Exception; Console.Error.WriteLine(e.Exception); Environment.ExitCode = 1; }; }
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        DmsBrowser? browser = null; Window? sharing = null;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            var assembly = typeof(DmsBrowser).Assembly;
            if (assembly.GetReferencedAssemblies().Any(reference => reference.Name == "System.Windows.Forms" || reference.Name == "CompuMaster.Dms.BrowserUI" || reference.Name!.StartsWith("Avalonia")))
                throw new InvalidDataException("The native component depends on another UI framework.");
            // The WinUI build merges referenced package PRIs into the application's resource index.
            if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "Consumer.pri"))) throw new InvalidDataException("The merged application PRI is missing.");
            var resources = new ResourceManager("CompuMaster.Dms.BrowserUI.WinUI.UiStrings", assembly);
            if (resources.GetString("ActionUpload", CultureInfo.CurrentUICulture)?.Replace("&", "") != "Dateien hochladen") throw new InvalidDataException("German resources are missing.");
            var provider = new DemoDmsProvider();
            browser = new DmsBrowser(provider, new DmsBrowserOptions
            {
                SelectedFolder = "Documents", AllowedActions = (FileOrFolderActions)2047,
                EnableInternalDragAndDrop = false, ToolbarActions = BrowserToolbarActions.All,
                ShowFilesButtonVisible = false, CreateFolderButtonVisible = false,
                FileDoubleClickAction = BrowserFileDoubleClickAction.None
            });
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync(); await Task.Delay(150);
            if (browser.Selection.Folder?.Name != "Documents") throw new InvalidDataException("The installed component did not initialize the selected directory.");
            browser.Browser.Appearance = BrowserTheme.Dark;
            browser.Browser.DarkPalette = new BrowserPalette("#172334", "#24364A", "#EDF2FA", "#5399D5", "#081524", "#647A90");
            await Task.Delay(30);
            if (((FrameworkElement)browser.Content).ActualTheme != ElementTheme.Dark ||
                ((Microsoft.UI.Xaml.Media.SolidColorBrush)((Microsoft.UI.Xaml.Controls.ContentControl)browser.Content).Background).Color != Windows.UI.Color.FromArgb(255, 23, 35, 52))
                throw new InvalidDataException("The packaged dark appearance or custom palette did not apply.");
            // Exercise packaged DataGrid styles and grouped content, independently of repository project references.
            var type = assembly.GetType("CompuMaster.Dms.BrowserUI.WinUI.DmsItemSharings", true)!;
            sharing = (Window)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { provider, provider.Items.Single(item => item.Name == "Welcome.txt") }, CultureInfo.InvariantCulture)!;
            var show = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(method => method.Name == "ShowDialog" && !method.IsGenericMethod);
            _ = (Task)show.Invoke(sharing, new object[] { browser })!;
            await Task.Delay(200);
            if (((FrameworkElement)sharing.Content).ActualHeight <= 0) throw new InvalidDataException("The installed sharing dialog did not render.");
            if (runtimeFailure is not null) throw new InvalidOperationException("The native consumer reported a runtime failure.", runtimeFailure);
            Console.WriteLine("WinUI NuGet consumer passed: native runtime, browser, sharing/DataGrid, German resources, PRI and dependency isolation.");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "consumer-success.json"), System.Text.Json.JsonSerializer.Serialize(new { NativeBrowser = true, SharingStyles = true, GermanResources = true, Pri = true, DependencyIsolation = true }));
            Environment.ExitCode = 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
        finally { sharing?.Close(); browser?.Close(); if (runtimeFailure is not null) Environment.ExitCode = 1; Exit(); }
    }
}
