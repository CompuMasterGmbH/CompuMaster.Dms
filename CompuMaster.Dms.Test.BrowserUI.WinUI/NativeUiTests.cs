using System.Globalization;
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NUnit.Framework;
using Wf = CompuMaster.Dms.BrowserUI;
using Forms = System.Windows.Forms;

namespace CompuMaster.Dms.Test.BrowserUI.WinUI;

[TestFixture]
public sealed class NativeUiTests
{
    [Test]
    public Task File_order_starts_with_newest_modification_and_header_actions_sort_names() => UiThread.Run(async () =>
    {
        var provider = new DemoDmsProvider();
        provider.AddFile("/Documents/AAA-old.txt", "old").LastModificationOnLocalTime = DateTime.Now.AddDays(-1);
        provider.AddFile("/Documents/ZZZ-new.txt", "new").LastModificationOnLocalTime = DateTime.Now.AddDays(1);
        var browser = new DmsBrowser(provider, new DmsBrowserOptions { SelectedFolder = "Documents" });
        try
        {
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync(); await Task.Delay(100);
            string First() => ((FileRow)((ListViewItem)browser.Browser.FileGrid.Items[0]).Tag).Name;
            Assert.That(First(), Is.EqualTo("ZZZ-new.txt"));
            var header = Descendants(browser.Browser).OfType<Microsoft.UI.Xaml.Controls.Button>().Single(button => button.Name == "ColumnName");
            var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(header);
            var invoke = (Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke);
            invoke.Invoke(); await Task.Delay(20); Assert.That(First(), Is.EqualTo("AAA-old.txt"));
            invoke.Invoke(); await Task.Delay(20); Assert.That(First(), Is.EqualTo("ZZZ-new.txt"));
        }
        finally { browser.Close(); }
    });
    [TestCase(null), TestCase("")]
    public Task Offline_demo_initializes_the_complete_provider_root_and_instance_switcher(string? root) => UiThread.Run(async () =>
    {
        var options = new DemoDefinition(DemoTarget.Offline).BrowserOptions(offline: true);
        options.InitialFolder = root;
        var browser = new DmsBrowser(new DemoDmsProvider(), options);
        try
        {
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync(); await Task.Delay(100);
            Assert.That(browser.Browser.SelectedFolder, Is.EqualTo("Documents"));
            Assert.That(browser.Browser.FolderTree.RootNodes.Single().Children.Count, Is.GreaterThan(1));
            var item = (TreeViewItem)browser.Browser.FolderTree.ContainerFromNode(browser.Browser.FolderTree.RootNodes.Single());
            Assert.That(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(item), Is.EqualTo("/"), "The rendered root must expose its directory name, not TreeViewNode.ToString().");
        }
        finally { browser.Close(); }
    });
    [TestCase("en"), TestCase("de")]
    public Task Native_browser_navigates_selects_and_switches_modes(string language) => UiThread.Run(async () =>
    {
        var previous = CultureInfo.CurrentUICulture; CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        var browser = new DmsBrowser(new DemoDmsProvider(), new DmsBrowserOptions { InitialFolder = "/Documents", SelectedFolder = "Projects", AllowedActions = (FileOrFolderActions)2047 });
        try
        {
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync();
            Assert.That(browser.SelectedFolderForTest(), Is.EqualTo("Projects"));
            Assert.That(browser.Browser.FileGrid.Items.Count, Is.EqualTo(1));
            browser.Browser.FileGrid.SelectedItems.Add(browser.Browser.FileGrid.Items[0]);
            Assert.That(browser.Selection.Files.Single().Name, Is.EqualTo("Notes.txt"));
            await browser.Browser.SelectFolderAsync(null);
            Assert.That(browser.Browser.FileGrid.Items.Count, Is.EqualTo(2));
            await browser.Browser.SetBrowseModeAsync(BrowseModes.Folders);
            Assert.That(browser.Browser.FileGrid.Items.Count, Is.Zero);
            await browser.Browser.SetBrowseModeAsync(BrowseModes.FoldersAndFiles);
            Assert.That(browser.Browser.FileGrid.Items.Count, Is.EqualTo(2));
            Assert.That(Descendants(browser.Browser).OfType<Microsoft.UI.Xaml.Controls.Button>().Single(button => button.Name == "ActionUpload").Content, Is.TypeOf<StackPanel>());
        }
        finally { browser.Close(); CultureInfo.CurrentUICulture = previous; }
    });

    [TestCase("overview", "en"), TestCase("overview", "de")]
    [TestCase("user", "en"), TestCase("user", "de")]
    [TestCase("group", "en"), TestCase("group", "de")]
    [TestCase("link", "en"), TestCase("link", "de")]
    public Task Native_sharing_group_headers_match_actual_WinForms(string kind, string language) => UiThread.Run(async () =>
    {
        var previous = CultureInfo.CurrentUICulture; CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        var provider = new DemoDmsProvider(); var item = provider.Items.Single(entry => entry.Name == "Welcome.txt");
        using Forms.Form reference = kind switch
        {
            "overview" => new Wf.DmsItemSharings(), "link" => new Wf.DmsLinkShareSetup(),
            "group" => new Wf.DmsStandardShareSetup((DmsShareForGroup)null!, provider, new List<string>()),
            _ => new Wf.DmsStandardShareSetup((DmsShareForUser)null!, provider, new List<string>())
        };
        static IEnumerable<Forms.Control> FormsChildren(Forms.Control parent) => parent.Controls.Cast<Forms.Control>().SelectMany(child => new[] { child }.Concat(FormsChildren(child)));
        var expected = FormsChildren(reference).OfType<Forms.GroupBox>().Select(group => group.Text).Order(StringComparer.Ordinal).ToArray();
        OperationWindow dialog = kind switch
        {
            "overview" => new DmsItemSharings(provider, item), "link" => new DmsLinkShareSetup(provider, item, null),
            _ => new DmsStandardShareSetup(provider, item, kind == "group", null)
        };
        try
        {
            _ = dialog.ShowDialog(UiThread.Owner);
            for (var attempt = 0; attempt < 100 && dialog.IsRunning; attempt++) await Task.Delay(10);
            Assert.That(dialog.IsRunning, Is.False);
            var groups = Descendants(dialog.Content).OfType<GroupBox>().ToArray();
            Assert.That(groups.Select(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName).Order(StringComparer.Ordinal), Is.EqualTo(expected));
            Assert.That(groups.All(group => group.Content is StackPanel), Is.True);
        }
        finally { dialog.Close(); CultureInfo.CurrentUICulture = previous; }
    });

    [Test]
    public Task Native_permissions_preserve_download_view_dependency_and_do_not_grant_other_rights() => UiThread.Run(() =>
    {
        var provider = new DemoDmsProvider(); var item = provider.Items.Single(entry => entry.Name == "Welcome.txt");
        var share = new DmsShareForUser(item, new DmsUser { ID = "fixture" }, true, true, true, true, true, true);
        var editor = new PermissionEditor(new SharingPolicy(provider), share, item, false, false);
        editor.Control.Children.OfType<Microsoft.UI.Xaml.Controls.CheckBox>().Single(check => check.Name == "PermissionView").IsChecked = false;
        Assert.That(new[] { "Download", "Edit", "Delete", "Share" }.Select(editor.Value), Is.All.False);
        Assert.That(editor.Value("Upload"), Is.True);
        editor.Control.Children.OfType<Microsoft.UI.Xaml.Controls.CheckBox>().Single(check => check.Name == "PermissionDownload").IsChecked = true;
        Assert.That(editor.Value("View"), Is.True);
        return Task.CompletedTask;
    });

    [TestCase(1), TestCase(2), TestCase(3), TestCase(4)]
    public Task Demo_credentials_use_private_buffers_and_retain_legacy_cache_semantics(int target) => UiThread.Run(async () =>
    {
        using var directory = new OwnedDirectory();
        var buffer = new DemoInputBuffer((DemoTarget)target, directory.Path, _ => null);
        var legacyPassword = Path.Combine(directory.Path, "~Buffer." + (target switch { 1 => "WebDav.Test", 2 => "NextcloudWebDav.Test", 3 => "OwnCloudWebDav.Test", _ => "Scopevisio.Teamwork.Test" }) + ".5F4DCC3B5AA765D61D8327DEB882CF99.tmp");
        File.WriteAllText(legacyPassword, "fixture-disk");
        var provider = new DemoDmsProvider(); DmsBrowserOptions? receivedOptions = null;
        var login = new DemoLoginWindow(new DemoDefinition((DemoTarget)target), _ => Task.FromResult<BaseDmsProvider>(provider),
            (_, _, options) => { receivedOptions = options; return Task.CompletedTask; }, buffer, instances => Task.FromResult<string?>(instances[0].ID));
        try
        {
            Assert.That(login.Password.Password, Is.EqualTo("fixture-disk"));
            Assert.That(login.PersistCredentials.IsChecked, Is.True);
            Assert.That(login.Server.Visibility == Visibility.Visible, Is.EqualTo(target != 4));
            Assert.That(login.Customer.Visibility == Visibility.Visible, Is.EqualTo(target == 4));
            login.Server.Text = "https://fixture.invalid/"; login.Username.Text = "fixture-user"; login.Customer.Text = "123"; login.Root.Text = "/Documents";
            login.Password.Password = "fixture-updated"; login.SaveInputs();
            Assert.That(File.ReadAllText(legacyPassword), Is.EqualTo("fixture-updated"));
            await login.ConnectAsync();
            Assert.That(login.Error.Text, Is.Empty);
            Assert.That(receivedOptions?.InitialFolder, Is.EqualTo("Documents"));
            Assert.That(login.Connecting, Is.False);
            login.PersistCredentials.IsChecked = false; login.SaveInputs();
            Assert.That(File.Exists(legacyPassword), Is.False);
        }
        finally { login.Close(); }
    });

    [TestCase(".pdf"), TestCase(".csv"), TestCase(".txt"), TestCase(".zip"), TestCase(".unittests_unknown")]
    public void Windows_file_association_icons_copy_pixels_without_creating_files(string extension)
    {
        var data = WindowsFileIcons.Read(extension, 32);
        Assert.That(data, Is.Not.Null); Assert.That(data!.Data.Length, Is.EqualTo(32 * 32 * 4));
        Assert.That(data.Data.Any(value => value != 0), Is.True);
    }

    [Test]
    public Task Version_permissions_are_independent_and_rechecked_before_mutation() => UiThread.Run(async () =>
    {
        var provider = new DemoDmsProvider(); var file = provider.Items.Single(entry => entry.Name == "Welcome.txt");
        var rights = FileOrFolderActions.AllowViewFileVersionHistory;
        var session = new VersionSession(provider, file, () => rights);
        var version = (await session.ListAsync()).First();
        Assert.That(session.CanDownload, Is.False); Assert.That(session.CanRestore, Is.False); Assert.That(session.CanRemove, Is.False);
        rights |= FileOrFolderActions.AllowRestoreFileVersions;
        Assert.That(session.CanRestore, Is.True); Assert.That(session.CanDownload, Is.False);
        rights = FileOrFolderActions.AllowViewFileVersionHistory;
        Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => await session.RestoreAsync(version)));
    });

    internal static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        IEnumerable<DependencyObject> children = parent switch
        {
            UserControl { Content: DependencyObject content } => new[] { content },
            Panel panel => panel.Children.Cast<DependencyObject>(), Border { Child: not null } border => new[] { border.Child },
            ContentControl { Content: DependencyObject child } => new[] { child }, _ => Array.Empty<DependencyObject>()
        };
        return children.SelectMany(child => new[] { child }.Concat(Descendants(child)));
    }
}
internal static class BrowserTestExtensions
{
    internal static string? SelectedFolderForTest(this DmsBrowser browser) => browser.Browser.SelectedFolder;
}
