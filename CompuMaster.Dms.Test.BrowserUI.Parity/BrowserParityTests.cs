using System.Reflection;
using CompuMaster.Dms.TestDemo.Avalonia;
using CompuMaster.Dms.Test.BrowserUI;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Headless;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.Themes.Fluent;
using global::Avalonia.VisualTree;
using NUnit.Framework;
using Av = CompuMaster.Dms.BrowserUI.Avalonia;
using Wf = CompuMaster.Dms.BrowserUI;
using Forms = System.Windows.Forms;
using Button = global::Avalonia.Controls.Button;
using ListBox = global::Avalonia.Controls.ListBox;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.Data;
using System.Globalization;

[assembly: AvaloniaTestApplication(typeof(CompuMaster.Dms.Test.BrowserUI.Parity.ParityApplication))]

namespace CompuMaster.Dms.Test.BrowserUI.Parity;

public sealed class ParityApplication : global::Avalonia.Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ParityApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

[SetUpFixture]
public sealed class WorkstationNotice
{
    private readonly DevelopmentGuiTestSession session = new();
    [OneTimeSetUp]
    public void Start() => session.StartNotice();
    [OneTimeTearDown]
    public void Finish() => session.StopNotice();
}

[TestFixture]
public sealed class BrowserParityTests
{
    private sealed record State(bool Visible, bool Enabled, bool? Checked = null);
    private sealed record Snapshot(Dictionary<string, State> Controls, string[] Files, int Selection);
    private static readonly (string Name, string Native)[] Toolbar =
    {
        ("ActionUpload", "ToolStripButtonUploadFile"), ("ActionUploadFolder", "UploadFolderButton"),
        ("ActionDownload", "ToolStripButtonDownloadFile"), ("ActionDownloadFolder", "DownloadFolderButton"),
        ("ActionOpen", "ToolStripButtonOpenFile"), ("ActionDelete", "ToolStripButtonDeleteFile"),
        ("ActionCopy", "ToolStripButtonCopyFile"), ("ActionRename", "ToolStripButtonRenameFile"), ("ActionMove", "ToolStripButtonMoveFile"),
        ("ActionFileSharings", "ToolStripButtonSharingsFile"), ("ActionFolderSharings", "ToolStripButtonSharingsFolder"),
        ("FileProperties", "ToolStripButtonPropertiesFile"), ("FolderProperties", "ToolStripButtonPropertiesFolder"),
        ("FileVersionHistory", "VersionHistoryButton"), ("ActionRefreshFiles", "ToolStripButtonRefreshFilesList")
    };
    private static readonly (string Name, string Native)[] FolderMenu =
    {
        ("ActionCreateFolder", "ToolStripFolderContextButtonNewFolder"), ("ActionCopy", "ToolStripFolderContextButtonCopyFolder"),
        ("ActionRename", "ToolStripFolderContextButtonRenameFolder"), ("ActionMove", "ToolStripFolderContextButtonMoveFolder"),
        ("ActionDelete", "ToolStripFolderContextButtonDeleteFolder"), ("ActionFolderSharings", "ToolStripFolderContextButtonShareFolder"),
        ("FolderProperties", "ToolStripFolderContextButtonProperties"), ("ActionRefreshFiles", "ToolStripFolderContextButtonRefreshFilesList"),
        ("ActionDownloadFolder", "DownloadFolderContext")
    };
    private static readonly (string Name, string Native)[] FileMenu =
    {
        ("ActionUpload", "ToolStripFileContextButtonUploadFile"), ("ActionUploadFolder", "UploadFolderContext"),
        ("ActionDownload", "ToolStripFileContextButtonDownloadFile"), ("ActionOpen", "ToolStripFileContextButtonOpenPreviewFile"),
        ("ActionDelete", "ToolStripFileContextButtonDeleteFile"), ("ActionCopy", "ToolStripFileContextButtonCopyFile"),
        ("ActionRename", "ToolStripFileContextButtonRenameFile"), ("ActionMove", "ToolStripFileContextButtonMoveFile"),
        ("ActionFileSharings", "ToolStripFileContextButtonShareFile"), ("FileProperties", "ToolStripFileContextButtonProperties"),
        ("FileVersionHistory", "VersionHistoryContext")
    };
    private static T Member<T>(object target, string name)
    {
        for (var type = target.GetType(); type is not null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? type.GetField("_" + name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null) return (T)field.GetValue(target)!;
        }
        throw new MissingFieldException(name);
    }
    private static T Native<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try { completion.SetResult(action()); }
            catch (Exception ex) { completion.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();
    }
    private sealed class ReferenceBrowser : Wf.DmsBrowser
    {
        internal Exception? Failure;
        internal ReferenceBrowser(BaseDmsProvider provider, int flags, bool folders, bool noResults, string? root = null, string? selected = "Documents") : base(provider, null, "Parity reference", null,
            root, selected, folders ? BrowseModes.Folders : BrowseModes.FoldersAndFiles,
            (FileOrFolderActions)flags, noResults ? DialogOperationModes.NoResults : DialogOperationModes.ReturnSelectedItems, "", "", "")
        {
            ShowInTaskbar = false; Opacity = 0;
            OperationFailed += (_, e) => Failure = e.Exception;
        }
        protected override void ShowFailureDialog(string message, string title, Forms.MessageBoxIcon icon) { }
    }
    private static Snapshot Reference(int flags, bool folders, bool sharing, bool versions, int selection, bool noResults, bool toggle,
        string? root = null, string? selected = "Documents", BaseDmsProvider? provider = null)
    {
        using var dispatcher = new UiTestDispatcher();
        using var browser = new ReferenceBrowser(provider ?? new DemoDmsProvider { SharingSupported = sharing, VersionsSupported = versions }, flags, folders, noResults, root, selected);
        browser.Show(); dispatcher.WaitForEvents();
        Assert.That(browser.Failure, Is.Null);
        var list = Member<Forms.ListView>(browser, "ListViewDmsFiles");
        if (toggle) { Member<Forms.CheckBox>(browser, "ButtonShowFiles").Checked = !folders; dispatcher.WaitForEvents();
            Member<Forms.CheckBox>(browser, "ButtonShowFiles").Checked = folders; dispatcher.WaitForEvents(); }
        foreach (Forms.ListViewItem item in list.Items.Cast<Forms.ListViewItem>().Take(selection)) item.Selected = true;
        Forms.Application.DoEvents(); dispatcher.WaitForEvents();
        // Opening the folder menu applies WinForms' current selection/capability rules.
        var opening = new System.ComponentModel.CancelEventArgs();
        typeof(Forms.ContextMenuStrip).GetMethod("OnOpening", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Member<Forms.ContextMenuStrip>(browser, "ContextMenuStripFolder"), new object[] { opening });
        var states = new Dictionary<string, State>();
        foreach (var (key, native) in Toolbar)
        {
            var item = Member<Forms.ToolStripItem>(browser, native);
            // ToolStrip items may be in the native overflow menu; Available is their
            // semantic presence, independently of that platform's packing/layout.
            var present = item.Available && item.Owner!.Visible;
            states["toolbar:" + key] = new(present, present && item.Enabled);
        }
        foreach (var (key, native) in FolderMenu)
        {
            var item = Member<Forms.ToolStripItem>(browser, native);
            states["folder:" + key] = new(item.Available, item.Available && item.Enabled);
        }
        foreach (var (key, native) in FileMenu)
        {
            var item = Member<Forms.ToolStripItem>(browser, native);
            states["file:" + key] = new(item.Available, item.Available && item.Enabled);
        }
        foreach (var (key, native) in new[] { ("ActionCreateFolder", "ButtonCreateNewFolder"), ("ActionOkay", "ButtonOkay"),
            ("ActionCancel", "ButtonCancel"), ("ActionClose", "ButtonClose"), ("ActionShowFiles", "ButtonShowFiles") })
        {
            var control = Member<Forms.Control>(browser, native);
            states["bottom:" + key] = new(control.Visible, control.Visible && control.Enabled, control is Forms.CheckBox check ? check.Checked : null);
        }
        return new(states, list.Items.Cast<Forms.ListViewItem>().Select(i => i.Text).Order().ToArray(), list.SelectedItems.Count);
    }
    [AvaloniaTest]
    [TestCase(0, false, true, true, 0, false)]
    [TestCase(2047, false, true, true, 0, false)]
    [TestCase(2047, false, true, true, 1, false)]
    [TestCase(2047, false, true, true, 2, true)]
    [TestCase(2047, true, true, true, 0, true)]
    [TestCase(2047, false, false, false, 1, false)]
    [TestCase(1, false, true, true, 0, false)]
    public async Task Action_availability_permissions_and_selection_match_the_running_WinForms_browser(int flags, bool folders, bool sharing, bool versions, int selection, bool noResults)
    {
        var expected = Native(() => Reference(flags, folders, sharing, versions, selection, noResults, false));
        var browser = new Av.DmsBrowser(new DemoDmsProvider { SharingSupported = sharing, VersionsSupported = versions }, new Av.DmsBrowserOptions
        {
            AllowedActions = (Av.FileOrFolderActions)flags, SelectedFolder = "Documents",
            BrowseMode = folders ? Av.BrowseModes.Folders : Av.BrowseModes.FoldersAndFiles,
            DialogOperationMode = noResults ? Av.DialogOperationModes.NoResults : Av.DialogOperationModes.ReturnSelectedItems
        });
        try
        {
            browser.Show(); await browser.Browser.InitializeAsync();
            foreach (var row in browser.Browser.Session.Files.Take(selection)) browser.Browser.FileGrid.SelectedItems.Add(row);
            var states = new Dictionary<string, State>();
            foreach (var (key, _) in Toolbar)
            {
                var control = browser.Browser.GetVisualDescendants().OfType<Button>().Single(b => b.Name == key);
                states["toolbar:" + key] = new(control.IsEffectivelyVisible, control.IsEffectivelyVisible && control.IsEnabled);
            }
            foreach (var (key, _) in FolderMenu)
            {
                var item = browser.Browser.FolderTree.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == key);
                states["folder:" + key] = new(item.IsVisible, item.IsVisible && item.IsEnabled);
            }
            foreach (var (key, _) in FileMenu)
            {
                var item = browser.Browser.FileGrid.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == key);
                states["file:" + key] = new(item.IsVisible, item.IsVisible && item.IsEnabled);
            }
            foreach (var key in new[] { "ActionCreateFolder", "ActionOkay", "ActionCancel", "ActionClose", "ActionShowFiles" })
            {
                var control = browser.Browser.GetVisualDescendants().OfType<Button>().Single(b => b.Name == key);
                states["bottom:" + key] = new(control.IsEffectivelyVisible, control.IsEffectivelyVisible && control.IsEnabled,
                    control is ToggleButton check ? check.IsChecked : null);
            }
            using (Assert.EnterMultipleScope())
            {
                foreach (var key in expected.Controls.Keys) Assert.That(states[key], Is.EqualTo(expected.Controls[key]), key);
                Assert.That(browser.Browser.Session.Files.Select(row => row.Name).Order(), Is.EqualTo(expected.Files));
                Assert.That(browser.Browser.FileGrid.SelectedItems.Count, Is.EqualTo(expected.Selection));
            }
        }
        finally { browser.Close(); }
    }

    [AvaloniaTest]
    [TestCase(false)]
    [TestCase(true)]
    public async Task Show_files_toggle_changes_the_same_mode_content_and_selected_folder(bool initialFolders)
    {
        var expected = Native(() => Reference(2047, initialFolders, true, true, 0, true, true));
        var browser = new Av.DmsBrowser(new DemoDmsProvider(), new Av.DmsBrowserOptions
        { AllowedActions = (Av.FileOrFolderActions)2047, SelectedFolder = "Documents", BrowseMode = initialFolders ? Av.BrowseModes.Folders : Av.BrowseModes.FoldersAndFiles });
        try
        {
            browser.Show(); await browser.Browser.InitializeAsync();
            var toggle = browser.Browser.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "ActionShowFiles");
            toggle.IsChecked = initialFolders;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (browser.Browser.IsBusy && DateTime.UtcNow < deadline) await Task.Delay(5);
            Assert.That(browser.Browser.IsBusy, Is.False);
            Assert.That(browser.Browser.FileGrid.IsVisible, Is.EqualTo(initialFolders));
            Assert.That(browser.Browser.Session.Files.Select(row => row.Name).Order(), Is.EqualTo(expected.Files));
            Assert.That(browser.Browser.SelectedFolder?.Trim('/'), Is.EqualTo("Documents"));
            await browser.Browser.SetBrowseModeAsync(initialFolders ? Av.BrowseModes.Folders : Av.BrowseModes.FoldersAndFiles);
            Assert.That(toggle.IsChecked, Is.EqualTo(!initialFolders), "Programmatic mode changes must update the visible switch.");
        }
        finally { browser.Close(); }
    }

    [AvaloniaTest]
    [TestCase(null, null, false)]
    [TestCase("collection/Child", null, false)]
    [TestCase("collection", null, true)]
    [TestCase("collection", "Child", false)]
    [TestCase("", "collection", false)]
    public async Task Root_and_scoped_collection_actions_match_the_reference(string? root, string? selected, bool restricted)
    {
        var expected = Native(() => Reference(2047, false, true, true, 0, true, false, root, selected, new DmsBrowserScopedCollectionTest.ScopedProvider()));
        var browser = new Av.DmsBrowser(new DmsBrowserScopedCollectionTest.ScopedProvider(), new Av.DmsBrowserOptions
        { AllowedActions = (Av.FileOrFolderActions)2047, InitialFolder = root, SelectedFolder = selected });
        try
        {
            browser.Show(); await browser.Browser.InitializeAsync();
            foreach (var (key, _) in FolderMenu)
            {
                var item = browser.Browser.FolderTree.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == key);
                Assert.That(new State(item.IsVisible, item.IsVisible && item.IsEnabled), Is.EqualTo(expected.Controls["folder:" + key]), key);
            }
            var share = browser.Browser.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ActionFolderSharings");
            Assert.That(share.IsVisible, Is.EqualTo(!restricted));
        }
        finally { browser.Close(); }
    }

    [AvaloniaTest]
    [TestCase("de-DE")]
    [TestCase("tr-TR")]
    public async Task Instance_order_and_preselection_match_WinForms_with_the_current_instance_first(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var instances = new[] { new DmsInstanceInfo("b", "alpha", false), new DmsInstanceInfo("selected", "Zulu", true),
                new DmsInstanceInfo("a", "Alpha", false), new DmsInstanceInfo("umlaut", "Äpfel", false) };
            var expected = Native(() =>
            {
                var type = typeof(Wf.DmsBrowser).Assembly.GetType("CompuMaster.Dms.BrowserUI.DmsInstanceSelectionDialog", true)!;
                using var dialog = (Forms.Form)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new object?[] { instances, null }, CultureInfo.CurrentCulture)!;
                var list = Member<Forms.ListBox>(dialog, "InstancesList");
                return (Order: list.Items.Cast<DmsInstanceInfo>().Select(i => i.ID).ToArray(), Selected: ((DmsInstanceInfo)list.SelectedItem!).ID);
            });
            var owner = new Window(); owner.Show();
            var selecting = Av.RemoteInstanceSelector.ShowDefaultSelectionDialogAsync(owner, instances);
            try
            {
                var picker = owner.OwnedWindows.Single();
                var list = picker.GetVisualDescendants().OfType<ListBox>().Single();
                Assert.That(list.ItemsSource!.Cast<Av.DialogChoice>().Select(i => i.ID), Is.EqualTo(expected.Order));
                Assert.That(((Av.DialogChoice)list.SelectedItem!).ID, Is.EqualTo(expected.Selected));
                Assert.That(expected.Order[0], Is.EqualTo("selected"));
                picker.Close();
                Assert.That(await selecting, Is.Null);
            }
            finally { foreach(var child in owner.OwnedWindows.ToArray()) child.Close(); owner.Close(); }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [AvaloniaTest]
    public async Task Inline_folder_rename_updates_the_same_resource_subtree_and_selection_as_WinForms()
    {
        const string target = "unittests_inline_renamed";
        var expected = Native(() =>
        {
            var provider = new DemoDmsProvider();
            using var dispatcher = new UiTestDispatcher();
            using var browser = new ReferenceBrowser(provider, 2047, false, true);
            browser.Show(); dispatcher.WaitForEvents();
            var tree = Member<Forms.TreeView>(browser, "TreeViewDmsFolders");
            Assert.That(provider.Items.Any(item => item.Name == target), Is.False);
            typeof(Wf.DmsBrowser).GetMethod("CommitFolderLabelEdit", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(browser, new object[] { tree, new Forms.NodeLabelEditEventArgs(tree.SelectedNode!, target) });
            dispatcher.WaitForEvents();
            Assert.That(browser.Failure, Is.Null);
            return provider.Items.Select(item => item.FullName).Order().ToArray();
        });
        var actual = new DemoDmsProvider();
        var window = new Av.DmsBrowser(actual, new Av.DmsBrowserOptions { SelectedFolder = "Documents", AllowedActions = (Av.FileOrFolderActions)2047 });
        try
        {
            window.Show(); await window.Browser.InitializeAsync();
            window.Browser.BeginFolderRename();
            var editor = window.Browser.FolderTree.GetVisualDescendants().OfType<TextBox>().Single(input => input.Name == "FolderNameEditor" && input.IsVisible);
            editor.Text = target; await window.Browser.CommitFolderRenameAsync();
            Assert.That(actual.Items.Select(item => item.FullName).Order(), Is.EqualTo(expected));
            Assert.That(window.Browser.SelectedFolder?.Trim('/'), Is.EqualTo(target));
        }
        finally { window.Close(); }
    }
}
