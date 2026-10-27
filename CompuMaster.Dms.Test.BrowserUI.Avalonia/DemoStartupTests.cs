using System.Globalization;
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class DemoStartupTests
{
    [TestCase("--webdav", (int)DemoTarget.WebDav)]
    [TestCase("--nextcloud", (int)DemoTarget.Nextcloud)]
    [TestCase("--owncloud", (int)DemoTarget.OwnCloudClassic)]
    [TestCase("--scopevisio", (int)DemoTarget.ScopevisioTeamwork)]
    public void General_demo_preserves_existing_arguments_and_supports_the_new_targets(string argument, int expected) =>
        Assert.That(DemoDefinition.FromArguments(new[] { argument }), Is.EqualTo((DemoTarget)expected));

    [Test]
    public void General_demo_defaults_to_offline_and_rejects_conflicting_targets()
    {
        Assert.That(DemoDefinition.FromArguments(Array.Empty<string>()), Is.EqualTo(DemoTarget.Offline));
        Assert.Throws<ArgumentException>((Action)(() => DemoDefinition.FromArguments(new[] { "--webdav", "--scopevisio" })));
    }
    [TestCase((int)DemoTarget.WebDav)]
    [TestCase((int)DemoTarget.Nextcloud)]
    [TestCase((int)DemoTarget.OwnCloudClassic)]
    [TestCase((int)DemoTarget.ScopevisioTeamwork)]
    public void Profiles_route_to_the_expected_provider_without_authorization(int target)
    {
        var definition = new DemoDefinition((DemoTarget)target);
        var profile = definition.CreateProfile("https://example.invalid/cloud/", "fixture-user", "fixture-only", "123");
        Assert.That(profile.ProviderID, Is.EqualTo(target == (int)DemoTarget.ScopevisioTeamwork ?
            BaseDmsProvider.DmsProviders.Scopevisio : BaseDmsProvider.DmsProviders.WebDAV));
        Assert.That(profile.CustomerInstance, Is.EqualTo(definition.RequiresCustomer ? "123" : null));
        Assert.That(profile.ServerAddress, definition.RequiresCustomer ? Is.Null : Is.Not.Null);
        Assert.That(definition.BrowserOptions().RemoteInstance.SelectedStartupInstance, Is.EqualTo(definition.RequiresCustomer ?
            RemoteInstanceSelector.StartupInstance.SelectionDialog : RemoteInstanceSelector.StartupInstance.DefaultInstance));
    }
    [TestCase("https://example.invalid/cloud", "https://example.invalid/cloud/remote.php/dav/files/user%20name/")]
    [TestCase("https://example.invalid/cloud/remote.php/dav", "https://example.invalid/cloud/remote.php/dav/files/user%20name/")]
    [TestCase("https://example.invalid/cloud/remote.php/dav/files/explicit/", "https://example.invalid/cloud/remote.php/dav/files/explicit/")]
    [TestCase("https://example.invalid/cloud/remote.php/webdav/", "https://example.invalid/cloud/remote.php/webdav/")]
    public void Nextcloud_accepts_instance_and_explicit_WebDAV_urls(string input, string expected)
    {
        Assert.That(new DemoDefinition(DemoTarget.Nextcloud).ResolveServerUrl(input, "user name"), Is.EqualTo(expected));
    }
    [TestCase("file:///tmp/test")]
    [TestCase("not a URL")]
    [TestCase("https://embedded:credentials@example.invalid/")]
    public void Invalid_server_addresses_are_rejected_without_network_access(string input) =>
        Assert.Throws<ArgumentException>((Action)(() => new DemoDefinition(DemoTarget.Nextcloud).ResolveServerUrl(input, "fixture")));

    [AvaloniaTest]
    public void Dedicated_demos_open_their_own_Avalonia_login_windows()
    {
        foreach (var target in new[] { DemoTarget.WebDav, DemoTarget.Nextcloud, DemoTarget.OwnCloudClassic, DemoTarget.ScopevisioTeamwork })
        {
            var definition = new DemoDefinition(target);
            var login = DemoWindows.Create(target, false) as DemoLoginWindow;
            Assert.That(login, Is.Not.Null);
            Assert.That(login!.Title, Does.Contain(definition.Name).And.Contain("Avalonia"));
            Assert.That(login.Server.IsVisible, Is.EqualTo(definition.RequiresServer));
            Assert.That(login.Customer.IsVisible, Is.EqualTo(definition.RequiresCustomer));
            Assert.That(login.Password.PasswordChar, Is.EqualTo('●'));
            login.Close();
        }
    }
    [AvaloniaTest]
    public async Task Every_dedicated_demo_can_start_offline_without_a_login_or_server()
    {
        foreach (var target in new[] { DemoTarget.Offline, DemoTarget.WebDav, DemoTarget.Nextcloud, DemoTarget.OwnCloudClassic, DemoTarget.ScopevisioTeamwork })
        {
            var browser = (DmsBrowser)DemoWindows.Create(target, true);
            try
            {
                browser.Show(); await browser.Browser.InitializeAsync(); await BrowserUiTests.IdleAsync(browser.Browser);
                Assert.That(browser.Browser.DmsProvider, Is.TypeOf<DemoDmsProvider>());
                Assert.That(browser.Browser.SelectedFolder, Is.EqualTo("Documents"));
                if (target != DemoTarget.Offline) Assert.That(browser.Title, Does.Contain("offline"));
            }
            finally { browser.Close(); }
        }
    }
    [AvaloniaTest]
    public async Task Successful_sign_in_opens_the_Avalonia_browser_with_the_selected_root()
    {
        IDmsLoginProfile? supplied = null;
        DmsBrowserOptions? options = null;
        var provider = new DemoDmsProvider();
        var login = new DemoLoginWindow(new DemoDefinition(DemoTarget.Nextcloud),
            profile => { supplied = profile; return Task.FromResult<BaseDmsProvider>(provider); },
            (_, actual, configured) => { Assert.That(actual, Is.SameAs(provider)); options = configured; return Task.CompletedTask; });
        login.Server.Text = "https://example.invalid/cloud";
        login.Username.Text = "fixture-user"; login.Password.Text = "fixture-only"; login.Root.Text = "/Documents";
        await login.ConnectAsync();
        Assert.That(supplied!.ServerAddress, Is.EqualTo("https://example.invalid/cloud/remote.php/dav/files/fixture-user/"));
        Assert.That(options!.InitialFolder, Is.EqualTo("/Documents"));
        Assert.That(options.FormTitle, Does.Contain("Nextcloud").And.Contain("Avalonia"));
        Assert.That(login.Password.Text, Is.Empty);
        Assert.That(login.Error.Text, Is.Empty);
        login.Close();
    }
    [AvaloniaTest]
    public async Task Failed_sign_in_preserves_the_window_and_hides_provider_exception_details()
    {
        var login = new DemoLoginWindow(new DemoDefinition(DemoTarget.WebDav),
            _ => Task.FromException<BaseDmsProvider>(new IOException("sensitive provider diagnostics")));
        login.Server.Text = "https://example.invalid/dav/";
        login.Username.Text = "fixture-user"; login.Password.Text = "fixture-only";
        await login.ConnectAsync();
        Assert.That(login.Error.Text, Is.EqualTo(DemoText.Get("OpenDmsFailed")));
        Assert.That(login.Error.Text, Does.Not.Contain("sensitive"));
        Assert.That(login.Connecting, Is.False);
        login.Close();
    }
    [AvaloniaTest]
    public void Shared_login_resources_follow_German_and_RTL_cultures()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
            Assert.That(DemoText.Get("SignIn"), Is.EqualTo("Anmelden"));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ar");
            var login = new DemoLoginWindow(new DemoDefinition(DemoTarget.WebDav));
            Assert.That(login.FlowDirection, Is.EqualTo(global::Avalonia.Media.FlowDirection.RightToLeft));
            Assert.That(DemoText.Get("SignIn"), Is.Not.EqualTo("SignIn"));
            login.Close();
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
