using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Platform;

namespace CompuMaster.Dms.TestDemo.Avalonia;

internal static class DemoWindows
{
    internal static Window Create(DemoTarget target, bool offline)
    {
        var definition = new DemoDefinition(target);
        return offline || target == DemoTarget.Offline
            ? new DmsBrowser(new DemoDmsProvider(), definition.BrowserOptions(offline: true))
            : new DemoLoginWindow(definition);
    }
}
internal sealed class DemoLoginWindow : Window
{
    private readonly DemoDefinition definition;
    private readonly Func<IDmsLoginProfile, Task<BaseDmsProvider>> authorize;
    private readonly Func<Window, BaseDmsProvider, DmsBrowserOptions, Task> showBrowser;
    private readonly StackPanel fields = new() { Spacing = 8 };
    internal readonly TextBox Server = new() { Name = "ServerAddress" };
    internal readonly TextBox Customer = new() { Name = "CustomerNumber" };
    internal readonly TextBox Username = new() { Name = "Username" };
    internal readonly TextBox Password = new() { Name = "Password", PasswordChar = '●' };
    internal readonly TextBox Root = new() { Name = "StartPath", Text = "/" };
    internal readonly TextBlock Error = new() { TextWrapping = TextWrapping.Wrap };
    internal bool Connecting { get; private set; }
    internal DemoLoginWindow(DemoDefinition definition, Func<IDmsLoginProfile, Task<BaseDmsProvider>>? authorize = null,
        Func<Window, BaseDmsProvider, DmsBrowserOptions, Task>? showBrowser = null)
    {
        this.definition = definition;
        this.authorize = authorize ?? (profile => DmsFactory.CreateAuthorizedDmsProviderInstanceAsync(profile));
        this.showBrowser = showBrowser ?? ((owner, provider, options) => new DmsBrowser(provider, options).ShowDialog(owner));
        Title = definition.LoginTitle; Width = 600; Height = 540; MinWidth = 420; MinHeight = 360;
        FlowDirection = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CompuMaster.Dms.BrowserUI.Avalonia/Assets/favicon.ico")));
        fields.Children.Add(new TextBlock { Text = definition.Name, FontSize = 24, FontWeight = FontWeight.Bold });
        void Field(string label, TextBox input, bool visible = true)
        {
            input.IsVisible = visible;
            fields.Children.Add(new TextBlock { Text = label, IsVisible = visible });
            fields.Children.Add(input);
        }
        Field(DemoText.Get("ServerUrl"), Server, definition.RequiresServer);
        if (definition.Target == DemoTarget.Nextcloud)
            Server.PlaceholderText = DemoText.Extra("Instance URL or full WebDAV URL", "Instanz-URL oder vollständige WebDAV-URL");
        else Server.PlaceholderText = "https://…/remote.php/webdav/";
        Field(DemoText.Get("CustomerNumber"), Customer, definition.RequiresCustomer);
        Field(DemoText.Get("UserName"), Username); Field(DemoText.Get("Password"), Password);
        Field(DemoText.Get("StartPath"), Root);
        var connect = new Button { Content = DemoText.Get("SignIn"), IsDefault = true };
        var exit = new Button { Content = DemoText.Get("Exit"), IsCancel = true };
        connect.Click += async (_, _) => await ConnectAsync();
        exit.Click += (_, _) => Close();
        fields.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right, Children = { connect, exit } });
        fields.Children.Add(Error);
        Content = new ScrollViewer { Margin = new Thickness(20), Content = fields };
        Closing += (_, e) => { if (Connecting) e.Cancel = true; };
    }
    internal async Task ConnectAsync()
    {
        if (Connecting) return;
        Connecting = true; fields.IsEnabled = false; Error.Text = "";
        try
        {
            var profile = definition.CreateProfile(Server.Text ?? "", Username.Text ?? "", Password.Text ?? "", Customer.Text ?? "");
            var options = definition.BrowserOptions(Root.Text?.Trim());
            var provider = await authorize(profile);
            Password.Text = "";
            await showBrowser(this, provider, options);
        }
        catch (Exception) { Error.Text = DemoText.Get("OpenDmsFailed"); }
        finally { Connecting = false; fields.IsEnabled = true; }
    }
}
