using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CompuMaster.Dms.TestDemo.WinUI;

internal sealed class DemoLoginWindow : Window
{
    private readonly DemoDefinition definition;
    private readonly DemoInputBuffer buffer;
    private readonly Func<IDmsLoginProfile, Task<BaseDmsProvider>> authorize;
    private readonly Func<Window, BaseDmsProvider, DmsBrowserOptions, Task> showBrowser;
    private readonly Func<IReadOnlyList<DmsInstanceInfo>, Task<string?>> chooseInstance;
    private readonly StackPanel fields = new() { Spacing = 6, Margin = new Thickness(20) };
    private readonly ContentControl container = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    internal readonly TextBox Server = new() { Name = "ServerAddress" };
    internal readonly TextBox Customer = new() { Name = "CustomerNumber" };
    internal readonly TextBox Organisation = new() { Name = "OrganisationId" };
    internal readonly TextBox ExpectedTenant = new() { Name = "ExpectedTeamworkTenantId" };
    internal readonly TextBox Username = new() { Name = "Username" };
    internal readonly PasswordBox Password = new() { Name = "Password" };
    internal readonly TextBox Root = new() { Name = "StartPath", Text = "/" };
    internal readonly TextBlock Error = new() { TextWrapping = TextWrapping.Wrap };
    internal readonly Microsoft.UI.Xaml.Controls.CheckBox PersistCredentials = new() { Name = "PersistCredentials", Content = DemoText.Get("PersistCredentials") };
    internal bool Connecting { get; private set; }
    internal DemoLoginWindow(DemoDefinition definition, Func<IDmsLoginProfile, Task<BaseDmsProvider>>? authorize = null,
        Func<Window, BaseDmsProvider, DmsBrowserOptions, Task>? showBrowser = null, DemoInputBuffer? buffer = null,
        Func<IReadOnlyList<DmsInstanceInfo>, Task<string?>>? chooseInstance = null)
    {
        this.definition = definition; this.buffer = buffer ?? new DemoInputBuffer(definition.Target);
        this.authorize = authorize ?? (profile => DmsFactory.CreateAuthorizedDmsProviderInstanceAsync(profile));
        this.showBrowser = showBrowser ?? ((owner, provider, options) => new DmsBrowser(provider, options).ShowDialogAsync(owner));
        this.chooseInstance = chooseInstance ?? (instances => RemoteInstanceSelector.ShowDefaultSelectionDialogAsync(this, instances));
        Title = definition.LoginTitle; AppWindow.Resize(new Windows.Graphics.SizeInt32(680, definition.RequiresCustomer ? 780 : 600));
        SetApplicationIcon();
        fields.FlowDirection = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        fields.Children.Add(new TextBlock { Text = definition.Name, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        void Field(string label, FrameworkElement input, bool visible = true)
        {
            input.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            fields.Children.Add(new TextBlock { Text = label, Visibility = input.Visibility }); fields.Children.Add(input);
        }
        Field(DemoText.Get("ServerUrl"), Server, definition.RequiresServer);
        Server.PlaceholderText = definition.Target == DemoTarget.Nextcloud ? DemoText.Extra("Instance URL or full WebDAV URL", "Instanz-URL oder vollständige WebDAV-URL") : "https://…/remote.php/webdav/";
        Field(DemoText.Get("CustomerNumber"), Customer, definition.RequiresCustomer);
        Field(DemoText.Get("UserName"), Username); Field(DemoText.Get("Password"), Password);
        Field(DemoText.Get("OrganisationId"), Organisation, definition.RequiresCustomer);
        Field(DemoText.Get("ExpectedTeamworkTenantId"), ExpectedTenant, definition.RequiresCustomer);
        Field(DemoText.Get("StartPath"), Root); fields.Children.Add(PersistCredentials);
        var connect = new Button { Name = "SignIn", Content = DemoText.Get("SignIn") };
        var exit = new Button { Content = DemoText.Get("Exit") };
        connect.Click += async (_, _) => await ConnectAsync(); exit.Click += (_, _) => Close();
        fields.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { connect, exit } });
        fields.Children.Add(Error);
        container.Content = new ScrollViewer { Content = fields };
        var themedRoot = (ContentControl)Microsoft.UI.Xaml.Markup.XamlReader.Load("<ContentControl xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource ApplicationPageBackgroundThemeBrush}' Foreground='{ThemeResource ApplicationForegroundThemeBrush}' HorizontalContentAlignment='Stretch' VerticalContentAlignment='Stretch'/>");
        themedRoot.Template = (ControlTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContentControl'><Border Background='{TemplateBinding Background}'><ContentPresenter Content='{TemplateBinding Content}' ContentTemplate='{TemplateBinding ContentTemplate}' HorizontalAlignment='Stretch' VerticalAlignment='Stretch'/></Border></ControlTemplate>");
        themedRoot.Content = container; Content = themedRoot;
        fields.Children.Add(BrowserAppearance.CreateSelector(themedRoot));
        Server.Text = this.buffer.Read("server url") ?? ""; Customer.Text = this.buffer.Read("customer no.") ?? "";
        Username.Text = this.buffer.Read("username") ?? ""; Password.Password = this.buffer.Read("password") ?? "";
        Root.Text = this.buffer.Read("start path") ?? "/";
        Organisation.Text = definition.RequiresCustomer ? this.buffer.Read("organisation id") ?? "" : "";
        ExpectedTenant.Text = definition.RequiresCustomer ? this.buffer.Read("expected teamwork tenant id") ?? "" : "";
        PersistCredentials.IsChecked = this.buffer.HasPersistedCredentials;
        fields.KeyDown += async (_, e) => { if (e.Key == Windows.System.VirtualKey.Enter) { e.Handled = true; await ConnectAsync(); } };
        AppWindow.Closing += (_, e) =>
        {
            if (Connecting) { e.Cancel = true; return; }
            try { SaveInputs(); }
            catch (ArgumentException ex) { e.Cancel = true; Error.Text = ex.Message; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { e.Cancel = true; Error.Text = DemoText.Extra("The demo settings could not be saved.", "Die Demo-Einstellungen konnten nicht gespeichert werden."); }
        };
    }
    internal void SaveInputs()
    {
        if (PersistCredentials.IsChecked == true)
        {
            if (definition.RequiresCustomer) DemoScopevisioSelection.Validate(Organisation.Text, ExpectedTenant.Text);
            foreach (var field in buffer.Fields) buffer.Persist(field, field switch
            {
                "username" => Username.Text, "password" => Password.Password, "server url" => Server.Text,
                "customer no." => Customer.Text, "start path" => Root.Text, "organisation id" => Organisation.Text,
                "expected teamwork tenant id" => ExpectedTenant.Text, _ => throw new InvalidOperationException()
            });
        }
        else foreach (var field in buffer.Fields) buffer.Remove(field);
    }
    private void SetApplicationIcon()
    {
        if (Environment.ProcessPath is not { } executable || ExtractIconEx(executable, 0, out var large, out var small, 1) == 0) return;
        try
        {
            AppWindow.SetIcon(Microsoft.UI.Win32Interop.GetIconIdFromIcon(large != 0 ? large : small));
            var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (large != 0) SendMessage(handle, 0x80, 1, large);
            if (small != 0) SendMessage(handle, 0x80, 0, small);
        }
        catch { if (large != 0) DestroyIcon(large); if (small != 0) DestroyIcon(small); throw; }
        Closed += (_, _) => { if (large != 0) DestroyIcon(large); if (small != 0) DestroyIcon(small); };
    }
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, uint message, nint word, nint value);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint icon);

    internal async Task ConnectAsync()
    {
        if (Connecting) return;
        Connecting = true; container.IsEnabled = false; Error.Text = "";
        try
        {
            var tenant = ExpectedTenant.Text;
            var profile = definition.CreateProfile(Server.Text, Username.Text, Password.Password, Customer.Text, Organisation.Text, tenant);
            var options = definition.BrowserOptions(Root.Text);
            options.Theme = BrowserAppearance.GetTheme((FrameworkElement)Content); var provider = await authorize(profile);
            if (definition.RequiresCustomer)
            {
                if (!await DemoScopevisioSelection.SelectAsync(provider, profile, tenant, chooseInstance)) return;
                var allowsSwitch = DemoScopevisioSelection.AllowsInstanceSwitch(profile, tenant);
                options.RemoteInstance = new RemoteInstanceSelector(RemoteInstanceSelector.StartupInstance.DefaultInstance, allowsSwitch ? RemoteInstanceSelector.ShowDefaultSelectionDialogAsync : null);
                if (!allowsSwitch) options.AllowedActions &= ~FileOrFolderActions.AllowSwitchDmsInstance;
            }
            await showBrowser(this, provider, options);
        }
        catch (Exception) { Error.Text = DemoText.Get("OpenDmsFailed"); }
        finally { Connecting = false; container.IsEnabled = true; }
    }
}
