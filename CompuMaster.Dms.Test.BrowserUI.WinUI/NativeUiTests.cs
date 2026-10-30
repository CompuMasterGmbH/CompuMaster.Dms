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
    [TestCase(false, false, false), TestCase(false, true, false), TestCase(false, null, true)]
    [TestCase(null, false, true), TestCase(true, true, true)]
    public Task Summary_panel_visibility_and_spacing_follow_available_information(bool? received, bool? outgoing, bool visible) => UiThread.Run(async () =>
    {
        var item = ReceivedSharingTests.ReceivedItem(); item.ExtendedInfosIsReceivedShare = received; item.ExtendedInfosHasOutgoingSharings = outgoing;
        var dialog = new DmsItemSharings(new DemoDmsProvider(), item);
        Task? pending = null;
        try
        {
            pending = dialog.ShowDialog(UiThread.Owner); await Task.Delay(100);
            var panel = Descendants(dialog.Content).OfType<Border>().Single(border => border.Name == "ReceivedSharingSummaryPanel");
            Assert.That(panel.Visibility, Is.EqualTo(visible ? Visibility.Visible : Visibility.Collapsed));
            var summary = Descendants(panel).OfType<TextBlock>().Single();
            var reference = typeof(Wf.DmsItemSharings).Assembly.GetType("CompuMaster.Dms.BrowserUI.SharingSummary")!.GetMethod("FormatDialog", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            Assert.That(summary.Text, Is.EqualTo(reference.Invoke(null, new object[] { item })));
            if (!visible)
            {
                var content = (StackPanel)Descendants(dialog.Content).OfType<ScrollViewer>().Single(viewer => viewer.Name == "SharingScrollViewer").Content;
                var ownerPosition = content.Children[1].TransformToVisual(content).TransformPoint(new Windows.Foundation.Point());
                Assert.That(ownerPosition.Y, Is.EqualTo(0).Within(0.5), "A hidden summary must not leave a blank heading row.");
            }
        }
        finally { dialog.Close(); if (pending is not null) await pending; }
    });

    [TestCase(720), TestCase(520)]
    public Task Constrained_sharing_dialog_leaves_the_overlay_scrollbar_clear_of_content(int width) => UiThread.Run(async () =>
    {
        var dialog = new DmsItemSharings(new DemoDmsProvider(), ReceivedSharingTests.ReceivedItem());
        Task? pending = null;
        try
        {
            pending = dialog.ShowDialog(UiThread.Owner);
            dialog.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, 480));
            await Task.Delay(150);
            dialog.Content.UpdateLayout();
            var viewer = Descendants(dialog.Content).OfType<ScrollViewer>().Single(scroll => scroll.Name == "SharingScrollViewer");
            Assert.That(viewer.ScrollableHeight, Is.GreaterThan(0), "The test must exercise a visible vertical scrollbar.");
            var controls = Descendants(viewer).OfType<FrameworkElement>().Where(element => element is GroupBox || element.Name == "ReceivedSharingSummaryPanel").ToArray();
            Assert.That(controls.Length, Is.EqualTo(3));
            foreach (var control in controls)
            {
                var right = control.TransformToVisual(viewer).TransformPoint(new Windows.Foundation.Point(control.ActualWidth, 0)).X;
                Assert.That(control.ActualWidth, Is.GreaterThan(100));
                Assert.That(right, Is.LessThanOrEqualTo(viewer.ActualWidth - 16 + 0.5), "The expanded native scrollbar needs a clear 16-DIP lane.");
            }
        }
        finally { dialog.Close(); if (pending is not null) await pending; }
    });

    private sealed class DisappearingFolderProvider : DemoDmsProvider
    {
        public override Task<List<DmsResourceItem>> ListDirectoryEntriesAsync(string remoteFolderPath, CancellationToken cancellationToken = default)
        {
            if (remoteFolderPath.Trim('/') == "Documents") throw new CompuMaster.Dms.Data.DirectoryNotFoundException(remoteFolderPath,
                new CompuMaster.Dms.Data.ResponseStatusCodeException(404, "Not Found"));
            return base.ListDirectoryEntriesAsync(remoteFolderPath, cancellationToken);
        }
    }
    [Test]
    public Task Expanding_a_disappeared_folder_removes_the_stale_node_and_retains_http_diagnostics() => UiThread.Run(async () =>
    {
        var browser = new DmsBrowser(new DisappearingFolderProvider());
        DmsBrowserFailureEventArgs? failure = null;
        browser.OperationFailed += (_, args) => { args.SuppressDialog = true; failure = args; };
        try
        {
            _ = browser.ShowAsync(); await browser.Browser.InitializeAsync(); await Task.Delay(100);
            var node = browser.Browser.FolderTree.RootNodes.Single().Children.Single(child => ((StackPanel)child.Content).Children.OfType<TextBlock>().Any(text => text.Text == "Documents"));
            node.IsExpanded = true;
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (failure is null && DateTime.UtcNow < deadline) await Task.Delay(20);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure!.Phase, Is.EqualTo(DmsBrowserFailurePhase.ChildListing));
            Assert.That(BrowserErrorDetails.Format(failure.Exception, failure.Phase, failure.Operation), Does.Contain("HTTP: 404").And.Contain("ExpandFolder"));
            Assert.That(browser.Browser.Session.AllNodes().Any(model => model.Name == "Documents"), Is.False);
        }
        finally { browser.Close(); }
    });

    [TestCase("en"), TestCase("de")]
    public Task Received_summary_is_visible_read_only_and_matches_WinForms(string language) => UiThread.Run(async () =>
    {
        var previous = CultureInfo.CurrentUICulture; CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
        var owner = new Window { Content = new Grid() };
        var item = ReceivedSharingTests.ReceivedItem();
        var dialog = new DmsItemSharings(new DemoDmsProvider(), item);
        Task? pending = null;
        try
        {
            owner.Activate(); pending = dialog.ShowDialog(owner); await Task.Delay(100);
            var summary = Descendants(dialog.Content).OfType<TextBlock>().Single(text => text.Name == "ReceivedSharingSummary");
            Assert.That(summary.Text, Does.Contain(UiText.Get("ReceivedSharingTitle")).And.Contain("Sharing person").And.Contain("Original owner"));
            var reference = typeof(Wf.DmsItemSharings).Assembly.GetType("CompuMaster.Dms.BrowserUI.SharingSummary")!.GetMethod("FormatDialog", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            Assert.That(summary.Text, Is.EqualTo(reference.Invoke(null, new object[] { item })));
            var grids = Descendants(dialog.Content).OfType<CommunityToolkit.WinUI.UI.Controls.DataGrid>().ToArray();
            Assert.That(grids.All(grid => grid.ItemsSource.Cast<object>().Count() == 0), Is.True, "Incoming grants never enter the editable outgoing lists.");
        }
        finally { dialog.Close(); if (pending is not null) await pending; owner.Close(); CultureInfo.CurrentUICulture = previous; }
    });

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
            var header = Descendants(browser.Browser).OfType<Microsoft.UI.Xaml.Controls.Button>().Single(button => button.Name == "SortName");
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
            TreeViewItem? item = null;
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                browser.Browser.UpdateLayout();
                item = browser.Browser.FolderTree.ContainerFromNode(browser.Browser.FolderTree.RootNodes.Single()) as TreeViewItem;
                if (item is not null && Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(item) == "/") break;
                await Task.Delay(20);
            }
            Assert.That(item, Is.Not.Null, "The native TreeView must realize its root container before reading its automation properties.");
            Assert.That(Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(item!), Is.EqualTo("/"), "The rendered root must expose its directory name, not TreeViewNode.ToString().");
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
