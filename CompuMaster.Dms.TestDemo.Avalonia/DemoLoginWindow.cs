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
    internal static Window Create(DemoTarget target, bool offline, DemoInputBuffer? buffer = null)
    {
        var definition = new DemoDefinition(target);
        return offline || target == DemoTarget.Offline
            ? new DmsBrowser(new DemoDmsProvider(), definition.BrowserOptions(offline: true))
            : new DemoLoginWindow(definition, buffer: buffer);
    }
}
internal sealed class DemoLoginWindow : Window
{
    private readonly DemoDefinition definition;
    private readonly DemoInputBuffer buffer;
    private readonly Func<IDmsLoginProfile, Task<BaseDmsProvider>> authorize;
    private readonly Func<Window, BaseDmsProvider, DmsBrowserOptions, Task> showBrowser;
    private readonly Func<IReadOnlyList<DmsInstanceInfo>, Task<string?>> chooseInstance;
    private readonly StackPanel fields = new() { Spacing = 8 };
    internal readonly TextBox Server = new() { Name = "ServerAddress" };
    internal readonly TextBox Customer = new() { Name = "CustomerNumber" };
    internal readonly TextBox Organisation = new() { Name = "OrganisationId" };
    internal readonly TextBox ExpectedTenant = new() { Name = "ExpectedTeamworkTenantId" };
    internal readonly TextBox Username = new() { Name = "Username" };
    internal readonly TextBox Password = new() { Name = "Password", PasswordChar = '●' };
    internal readonly TextBox Root = new() { Name = "StartPath", Text = "/" };
    internal readonly TextBlock Error = new() { TextWrapping = TextWrapping.Wrap };
    internal readonly CheckBox PersistCredentials = new() { Name = "PersistCredentials", Content = DemoText.Get("PersistCredentials") };
    internal bool Connecting { get; private set; }
    internal DemoLoginWindow(DemoDefinition definition, Func<IDmsLoginProfile, Task<BaseDmsProvider>>? authorize = null,
        Func<Window, BaseDmsProvider, DmsBrowserOptions, Task>? showBrowser = null, DemoInputBuffer? buffer = null,
        Func<IReadOnlyList<DmsInstanceInfo>, Task<string?>>? chooseInstance = null)
    {
        this.definition = definition;
        this.buffer = buffer ?? new DemoInputBuffer(definition.Target);
        this.authorize = authorize ?? (profile => DmsFactory.CreateAuthorizedDmsProviderInstanceAsync(profile));
        this.showBrowser = showBrowser ?? ((owner, provider, options) => new DmsBrowser(provider, options).ShowDialog(owner));
        this.chooseInstance = chooseInstance ?? (instances => RemoteInstanceSelector.ShowDefaultSelectionDialogAsync(this, instances));
        Title = definition.LoginTitle; Width = 680; Height = definition.RequiresCustomer ? 720 : 540; MinWidth = 420; MinHeight = 360;
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
        Field(DemoText.Get("OrganisationId"), Organisation, definition.RequiresCustomer);
        Field(DemoText.Get("ExpectedTeamworkTenantId"), ExpectedTenant, definition.RequiresCustomer);
        Field(DemoText.Get("StartPath"), Root);
        fields.Children.Add(PersistCredentials);
        var connect = new Button { Content = DemoText.Get("SignIn"), IsDefault = true };
        var exit = new Button { Content = DemoText.Get("Exit"), IsCancel = true };
        connect.Click += async (_, _) => await ConnectAsync();
        exit.Click += (_, _) => Close();
        fields.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right, Children = { connect, exit } });
        fields.Children.Add(Error);
        Content = new ScrollViewer { Margin = new Thickness(20), Content = fields };
        Server.Text = this.buffer.Read("server url"); Customer.Text = this.buffer.Read("customer no.");
        Username.Text = this.buffer.Read("username"); Password.Text = this.buffer.Read("password");
        Root.Text = this.buffer.Read("start path") ?? "/";
        Organisation.Text = definition.RequiresCustomer ? this.buffer.Read("organisation id") : null;
        ExpectedTenant.Text = definition.RequiresCustomer ? this.buffer.Read("expected teamwork tenant id") : null;
        PersistCredentials.IsChecked = this.buffer.HasPersistedCredentials;
        Closing += (_, e) =>
        {
            if (Connecting) { e.Cancel = true; return; }
            try { SaveInputs(); }
            catch (ArgumentException ex) { e.Cancel = true; Error.Text = ex.Message; }
            catch (IOException) { e.Cancel = true; Error.Text = DemoText.Extra("The demo settings could not be saved.", "Die Demo-Einstellungen konnten nicht gespeichert werden."); }
            catch (UnauthorizedAccessException) { e.Cancel = true; Error.Text = DemoText.Extra("The demo settings could not be saved.", "Die Demo-Einstellungen konnten nicht gespeichert werden."); }
        };
    }
    internal void SaveInputs()
    {
        if (PersistCredentials.IsChecked == true)
        {
            if (definition.RequiresCustomer) DemoScopevisioSelection.Validate(Organisation.Text ?? "", ExpectedTenant.Text ?? "");
            foreach (var field in buffer.Fields) buffer.Persist(field, field switch
            {
                "username" => Username.Text ?? "", "password" => Password.Text ?? "", "server url" => Server.Text ?? "",
                "customer no." => Customer.Text ?? "", "start path" => Root.Text ?? "", "organisation id" => Organisation.Text ?? "",
                "expected teamwork tenant id" => ExpectedTenant.Text ?? "", _ => throw new InvalidOperationException()
            });
        }
        else foreach (var field in buffer.Fields) buffer.Remove(field);
    }
    internal async Task ConnectAsync()
    {
        if (Connecting) return;
        Connecting = true; fields.IsEnabled = false; Error.Text = "";
        try
        {
            var tenant = ExpectedTenant.Text ?? "";
            var profile = definition.CreateProfile(Server.Text ?? "", Username.Text ?? "", Password.Text ?? "", Customer.Text ?? "", Organisation.Text ?? "", tenant);
            var options = definition.BrowserOptions(Root.Text);
            var provider = await authorize(profile);
            if (definition.RequiresCustomer)
            {
                if (!await DemoScopevisioSelection.SelectAsync(provider, profile, tenant, chooseInstance)) return;
                options.RemoteInstance = new RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.DefaultInstance,
                    RemoteInstanceSelector.ShowDefaultSelectionDialogAsync);
                if (!DemoScopevisioSelection.AllowsInstanceSwitch(profile, tenant))
                    options.AllowedActions &= ~FileOrFolderActions.AllowSwitchDmsInstance;
            }
            await showBrowser(this, provider, options);
        }
        catch (Exception) { Error.Text = DemoText.Get("OpenDmsFailed"); }
        finally { Connecting = false; fields.IsEnabled = true; }
    }
}
