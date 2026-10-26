using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Layout;
using global::Avalonia.Themes.Fluent;

namespace CompuMaster.Dms.TestDemo.Avalonia;

internal static class Program
{
    internal static string[] Arguments = Array.Empty<string>();
    [STAThread]
    public static void Main(string[] args)
    {
        Arguments = args;
        AppBuilder.Configure<DemoApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}

internal sealed class DemoApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Program.Arguments.Contains("--webdav")) desktop.MainWindow = Login(BaseDmsProvider.DmsProviders.WebDAV);
            else if (Program.Arguments.Contains("--scopevisio")) desktop.MainWindow = Login(BaseDmsProvider.DmsProviders.Scopevisio);
            else desktop.MainWindow = new DmsBrowser(new DemoDmsProvider(), Options("DMS Browser DEMO for Avalonia", "Documents"));
        }
        base.OnFrameworkInitializationCompleted();
    }
    private static DmsBrowserOptions Options(string title, string? folder = null) => new()
    {
        FormTitle = title, SelectedFolder = folder, AllowedActions = (FileOrFolderActions)255,
        DialogOperationMode = DialogOperationModes.NoResults,
        RemoteInstance = new RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.DefaultInstance,
            RemoteInstanceSelector.ShowDefaultSelectionDialogAsync)
    };
    private static Window Login(BaseDmsProvider.DmsProviders provider)
    {
        var window = new Window { Title = "Avalonia DMS connection", Width = 580, Height = 480 };
        var body = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
        TextBox Field(string title, bool password = false)
        {
            body.Children.Add(new TextBlock { Text = title });
            var input = new TextBox(); if (password) input.PasswordChar = '●'; body.Children.Add(input); return input;
        }
        var server = Field(provider == BaseDmsProvider.DmsProviders.WebDAV ? "WebDAV server URL" : "Server URL (optional)");
        var customer = Field("Customer instance (Scopevisio)");
        var user = Field("Username");
        var password = Field("Password", true);
        var connect = new Button { Content = "Connect", HorizontalAlignment = HorizontalAlignment.Right };
        var error = new TextBlock { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
        body.Children.Add(connect); body.Children.Add(error); window.Content = body;
        connect.Click += async (_, _) =>
        {
            connect.IsEnabled = false;
            try
            {
                var profile = new DmsLoginProfile { DmsProvider = provider, BaseUrl = server.Text, CustomerInstance = customer.Text,
                    Username = user.Text, Password = password.Text };
                var authorized = await DmsFactory.CreateAuthorizedDmsProviderInstanceAsync(profile);
                password.Text = "";
                var browser = new DmsBrowser(authorized, Options("DMS Browser DEMO for Avalonia – " + authorized.Name));
                await browser.ShowDialog(window);
            }
            catch (Exception ex) { error.Text = ex.Message; }
            finally { connect.IsEnabled = true; }
        };
        return window;
    }
}
