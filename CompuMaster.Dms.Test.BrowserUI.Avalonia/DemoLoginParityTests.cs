using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Headless.NUnit;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class DemoLoginParityTests
{
    private string directory = null!;
    [SetUp]
    public void Create_private_storage()
    {
        directory = Path.Combine(Path.GetTempPath(), "unittests_avalonia_persistence_" + Guid.NewGuid().ToString("N"));
        Assert.That(Directory.Exists(directory), Is.False);
        Directory.CreateDirectory(directory);
    }
    [TearDown]
    public void Cleanup_private_storage() => Directory.Delete(directory, true);
    private DemoInputBuffer Buffer(DemoTarget target) => new(target, directory, _ => null);

    [TestCase((int)DemoTarget.WebDav, "WebDav.Test", "WEBDAV")]
    [TestCase((int)DemoTarget.Nextcloud, "NextcloudWebDav.Test", "CMNEXTCLOUD")]
    [TestCase((int)DemoTarget.OwnCloudClassic, "OwnCloudWebDav.Test", "CMOWNCLOUD")]
    [TestCase((int)DemoTarget.ScopevisioTeamwork, "Scopevisio.Teamwork.Test", "SCOPEVISIOTEAMWORK")]
    public void Existing_WinForms_cache_names_and_environment_precedence_are_preserved(int target, string title, string prefix)
    {
        // Fixed historical MD5 key for "password", rather than deriving the expected value from implementation.
        var legacyPath = Path.Combine(directory, "~Buffer." + title + ".5F4DCC3B5AA765D61D8327DEB882CF99.tmp");
        File.WriteAllText(legacyPath, "fixture-disk");
        var buffer = Buffer((DemoTarget)target);
        Assert.That(buffer.Read("password"), Is.EqualTo("fixture-disk"));
        Assert.That(buffer.HasPersistedCredentials, Is.True);
        var env = new DemoInputBuffer((DemoTarget)target, directory, name => name == "TEST_" + prefix + "_PASSWORD" ? "fixture-env" : null);
        Assert.That(env.Read("password"), Is.EqualTo("fixture-env"));
        env = new DemoInputBuffer((DemoTarget)target, directory, _ => " ");
        Assert.That(env.Read("password"), Is.EqualTo("fixture-disk"));
        var selectors = new DemoInputBuffer((DemoTarget)target, directory, name =>
            name == "TEST_" + prefix + "_CUSTOMERNO" ? "123" : name == "TEST_" + prefix + "_SERVERURL" ? "https://example.invalid" : null);
        Assert.That(selectors.Read("customer no."), Is.EqualTo("123"));
        Assert.That(selectors.Read("server url"), Is.EqualTo("https://example.invalid"));
    }

    [AvaloniaTest]
    public void All_forms_reload_and_persist_every_supported_input_on_close()
    {
        foreach (var target in new[] { DemoTarget.WebDav, DemoTarget.Nextcloud, DemoTarget.OwnCloudClassic, DemoTarget.ScopevisioTeamwork })
        {
            var buffer = Buffer(target);
            foreach (var field in buffer.Fields) buffer.Persist(field, field switch
            {
                "organisation id" => "42", "expected teamwork tenant id" => "tenant-B", "start path" => "/Fixture",
                "customer no." => "123", _ => "fixture-" + field
            });
            var login = new DemoLoginWindow(new DemoDefinition(target), buffer: buffer);
            Assert.That(login.PersistCredentials.IsChecked, Is.True);
            Assert.That(login.Username.Text, Is.EqualTo("fixture-username"));
            Assert.That(login.Password.Text, Is.EqualTo("fixture-password"));
            Assert.That(login.Root.Text, Is.EqualTo("/Fixture"));
            Assert.That(login.Organisation.IsVisible, Is.EqualTo(target == DemoTarget.ScopevisioTeamwork));
            Assert.That(login.ExpectedTenant.IsVisible, Is.EqualTo(target == DemoTarget.ScopevisioTeamwork));
            if (target == DemoTarget.ScopevisioTeamwork)
            {
                Assert.That(login.Customer.Text, Is.EqualTo("123"));
                Assert.That(login.Organisation.Text, Is.EqualTo("42"));
                Assert.That(login.ExpectedTenant.Text, Is.EqualTo("tenant-B"));
            }
            else Assert.That(login.Server.Text, Is.EqualTo("fixture-server url"));
            login.Password.Text = "fixture-updated"; login.Root.Text = "/Updated";
            login.Show(); login.Close();
            Assert.That(buffer.Read("password"), Is.EqualTo("fixture-updated"));
            Assert.That(buffer.Read("start path"), Is.EqualTo("/Updated"));
            login = new DemoLoginWindow(new DemoDefinition(target), buffer: buffer);
            login.PersistCredentials.IsChecked = false; login.Show(); login.Close();
            Assert.That(buffer.Fields.Any(buffer.Exists), Is.False);
        }
    }

    [AvaloniaTest]
    public void Environment_only_inputs_do_not_opt_in_to_disk_storage()
    {
        var buffer = new DemoInputBuffer(DemoTarget.WebDav, directory, name => name.EndsWith("PASSWORD") ? "fixture-only" : null);
        var login = new DemoLoginWindow(new DemoDefinition(DemoTarget.WebDav), buffer: buffer);
        Assert.That(login.Password.Text, Is.EqualTo("fixture-only"));
        Assert.That(login.PersistCredentials.IsChecked, Is.False);
        login.Show(); login.Close();
        Assert.That(Directory.GetFiles(directory), Is.Empty);
    }

    [AvaloniaTest]
    public void Invalid_Scopevisio_selector_blocks_close_without_partial_cache_overwrite()
    {
        var buffer = Buffer(DemoTarget.ScopevisioTeamwork);
        buffer.Persist("username", "fixture-original");
        var login = new DemoLoginWindow(new DemoDefinition(DemoTarget.ScopevisioTeamwork), buffer: buffer);
        login.Organisation.Text = "invalid"; login.Username.Text = "fixture-changed";
        login.Show(); login.Close();
        Assert.That(login.IsVisible, Is.True);
        Assert.That(login.Error.Text, Is.EqualTo(DemoText.Get("InvalidOrganisationId")));
        Assert.That(buffer.Read("username"), Is.EqualTo("fixture-original"));
        login.Organisation.Text = "42"; login.Close();
        Assert.That(buffer.Read("username"), Is.EqualTo("fixture-changed"));
        Assert.That(buffer.Read("organisation id"), Is.EqualTo("42"));
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("1.5")]
    [TestCase(" ")]
    [TestCase("9223372036854775808")]
    public void Invalid_organisation_selectors_are_rejected_before_authorization(string input) =>
        Assert.Throws<ArgumentException>((Action)(() => DemoScopevisioSelection.Validate(input, "")));

    [Test]
    public void Organisation_and_tenant_remain_distinct_and_do_not_change_legacy_selection()
    {
        var definition = new DemoDefinition(DemoTarget.ScopevisioTeamwork);
        var legacy = definition.CreateProfile("", "fixture", "fixture", "123");
        Assert.That(legacy, Is.TypeOf<DmsLoginProfile>());
        var guarded = definition.CreateProfile("", "fixture", "fixture", "123", "", "tenant-B");
        Assert.That(guarded, Is.TypeOf<DmsLoginProfile>(), "Tenant-only guard is checked after interactive selection.");
        Assert.That(DemoScopevisioSelection.AllowsInstanceSwitch(guarded, "tenant-B"), Is.False);
        var explicitProfile = (ScopevisioLoginCredentials)definition.CreateProfile("", "fixture", "fixture", "123", " 42 ", "tenant-B");
        Assert.That(explicitProfile.OrganisationId, Is.EqualTo(42));
        Assert.That(explicitProfile.ExpectedTeamworkTenantId, Is.EqualTo("tenant-B"));
        Assert.That(DemoScopevisioSelection.AllowsInstanceSwitch(explicitProfile, ""), Is.False);
        Assert.That(DemoScopevisioSelection.AllowsInstanceSwitch(legacy, ""), Is.True);
    }

    [AvaloniaTest]
    public async Task Scopevisio_cancelled_picker_and_tenant_mismatch_never_open_browser()
    {
        var opened = 0;
        var provider = new DemoDmsProvider();
        DmsBrowserOptions? configured = null;
        string? selection = null;
        var login = new DemoLoginWindow(new DemoDefinition(DemoTarget.ScopevisioTeamwork),
            _ => Task.FromResult<BaseDmsProvider>(provider), (_, _, options) => { opened++; configured = options; return Task.CompletedTask; },
            Buffer(DemoTarget.ScopevisioTeamwork), _ => Task.FromResult(selection));
        login.Username.Text = "fixture"; login.Password.Text = "fixture"; login.Customer.Text = "123";
        await login.ConnectAsync();
        Assert.That(opened, Is.Zero);
        Assert.That(login.Error.Text, Is.Empty, "Cancellation is not a login failure.");
        selection = "second"; login.ExpectedTenant.Text = "another-tenant";
        await login.ConnectAsync();
        Assert.That(opened, Is.Zero);
        Assert.That(login.Error.Text, Is.EqualTo(DemoText.Get("OpenDmsFailed")));
        login.ExpectedTenant.Text = "second";
        await login.ConnectAsync();
        Assert.That(opened, Is.EqualTo(1));
        Assert.That(configured!.AllowedActions.HasFlag(FileOrFolderActions.AllowSwitchDmsInstance), Is.False);
        Assert.That(configured.RemoteInstance.SelectedStartupInstance, Is.EqualTo(RemoteInstanceSelector.StartupInstance.DefaultInstance));
        Assert.That(configured.RemoteInstance.SelectionDialog, Is.Null, "A guarded context must not enumerate other instances during browser initialization.");
        login.Close();
    }

    [AvaloniaTest]
    public async Task Explicit_Scopevisio_organisation_bypasses_picker_and_retains_the_authorized_context()
    {
        var provider = new DemoDmsProvider();
        IDmsLoginProfile? supplied = null;
        var opened = false;
        var login = new DemoLoginWindow(new DemoDefinition(DemoTarget.ScopevisioTeamwork),
            profile => { supplied = profile; return Task.FromResult<BaseDmsProvider>(provider); },
            (_, _, options) => { opened = true; Assert.That(options.AllowedActions.HasFlag(FileOrFolderActions.AllowSwitchDmsInstance), Is.False);
                Assert.That(options.RemoteInstance.SelectionDialog, Is.Null); return Task.CompletedTask; },
            Buffer(DemoTarget.ScopevisioTeamwork), _ => throw new AssertionException("An explicit organisation must not invoke the instance picker."));
        login.Username.Text = "fixture"; login.Password.Text = "fixture"; login.Customer.Text = "123";
        login.Organisation.Text = "42"; login.ExpectedTenant.Text = "demo";
        await login.ConnectAsync();
        Assert.That(opened, Is.True);
        Assert.That(supplied, Is.TypeOf<ScopevisioLoginCredentials>());
        Assert.That(((ScopevisioLoginCredentials)supplied!).OrganisationId, Is.EqualTo(42));
        login.Close();
    }

    [TestCase("/Documents", "Documents")]
    [TestCase("Documents", "Documents")]
    [TestCase(" folder ", " folder ")]
    [TestCase(" ", " ")]
    public void Starting_directory_preserves_the_WinForms_mapping_without_trimming_valid_names(string input, string expected) =>
        Assert.That(new DemoDefinition(DemoTarget.WebDav).BrowserOptions(input).InitialFolder, Is.EqualTo(expected));
}
