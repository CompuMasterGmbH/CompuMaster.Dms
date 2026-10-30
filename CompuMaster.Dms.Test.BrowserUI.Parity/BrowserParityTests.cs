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
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ParityApplication>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
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
public sealed partial class BrowserParityTests
{
    [AvaloniaTest]
    [TestCase("overview", "de")]
    [TestCase("overview", "en")]
    [TestCase("user", "de")]
    [TestCase("user", "en")]
    [TestCase("group", "de")]
    [TestCase("group", "en")]
    [TestCase("link", "de")]
    [TestCase("link", "en")]
    public async Task Sharing_dialog_group_headers_match_all_native_reference_groups(string kind, string culture)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        var expected = Native(() =>
        {
            using Forms.Form reference = kind switch
            {
                "overview" => new Wf.DmsItemSharings(),
                "link" => new Wf.DmsLinkShareSetup(),
                "group" => new Wf.DmsStandardShareSetup((DmsShareForGroup)null!, new DemoDmsProvider(), new List<string>()),
                _ => new Wf.DmsStandardShareSetup((DmsShareForUser)null!, new DemoDmsProvider(), new List<string>())
            };
            static IEnumerable<Forms.Control> Descendants(Forms.Control parent) =>
                parent.Controls.Cast<Forms.Control>().SelectMany(child => new[] { child }.Concat(Descendants(child)));
            return Descendants(reference).OfType<Forms.GroupBox>().Select(group => group.Text).Order(StringComparer.Ordinal).ToArray();
        });
        var provider = new DemoDmsProvider();
        var item = provider.Items.Single(entry => entry.Name == "Welcome.txt");
        Av.OperationWindow dialog = kind switch
        {
            "overview" => new Av.DmsItemSharings(provider, item),
            "link" => new Av.DmsLinkShareSetup(provider, item, null),
            _ => new Av.DmsStandardShareSetup(provider, item, kind == "group", null)
        };
        var owner = new Window();
        try
        {
            owner.Show(); dialog.Show(owner);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (dialog.IsRunning && DateTime.UtcNow < deadline) await Task.Delay(5);
            Assert.That(dialog.IsRunning, Is.False);
            dialog.UpdateLayout();
            var groups = dialog.GetVisualDescendants().OfType<GroupBox>().ToArray();
            Assert.That(groups.Select(group => group.Header?.ToString()).Order(StringComparer.Ordinal), Is.EqualTo(expected));
            Assert.That(groups.All(group => group.IsVisible && group.Content is Control), Is.True);
        }
        finally { dialog.Close(); owner.Close(); CultureInfo.CurrentUICulture = previous; }
    }

    private sealed record IconData(int Width, int Height, byte[] Pixels);
    private sealed record State(bool Visible, bool Enabled, bool? Checked = null, IconData? Icon = null);
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
    private static IconData? IconPixels(System.Drawing.Image? image)
    {
        if (image is null) return null;
        using var bitmap = new System.Drawing.Bitmap(image);
        var pixels = new byte[bitmap.Width * bitmap.Height * 4];
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                var offset = (y * bitmap.Width + x) * 4;
                // Transparent RGB values are not visible and may differ after decoding.
                if (pixel.A == 0) continue;
                pixels[offset] = (byte)(pixel.R * pixel.A / 255); pixels[offset + 1] = (byte)(pixel.G * pixel.A / 255);
                pixels[offset + 2] = (byte)(pixel.B * pixel.A / 255); pixels[offset + 3] = pixel.A;
            }
        return new(bitmap.Width, bitmap.Height, pixels);
    }
    private static IconData? IconPixels(global::Avalonia.Controls.Image? image)
    {
        if (image?.Source is not global::Avalonia.Media.Imaging.Bitmap source) return null;
        using var encoded = new MemoryStream(); source.Save(encoded, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); encoded.Position = 0;
        using var bitmap = new System.Drawing.Bitmap(encoded);
        return IconPixels(bitmap);
    }
    private static void AssertState(State actual, State expected, string key)
    {
        Assert.That(actual with { Icon = null }, Is.EqualTo(expected with { Icon = null }), key);
        Assert.That(actual.Icon is null, Is.EqualTo(expected.Icon is null), key + " icon presence");
        if (actual.Icon is not { } icon || expected.Icon is not { } reference) return;
        Assert.That((icon.Width, icon.Height), Is.EqualTo((reference.Width, reference.Height)), key + " icon dimensions");
        // Skia/GDI differ slightly when premultiplying translucent edge pixels.
        Assert.That(icon.Pixels.Zip(reference.Pixels).Max(pair => Math.Abs(pair.First - pair.Second)),
            Is.LessThanOrEqualTo(2), key + " must use the same icon artwork as WinForms");
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
        string? root = null, string? selected = "Documents", BaseDmsProvider? provider = null, int toolbar = (int)Av.BrowserToolbarActions.Default)
    {
        using var dispatcher = new UiTestDispatcher();
        using var browser = new ReferenceBrowser(provider ?? new DemoDmsProvider { SharingSupported = sharing, VersionsSupported = versions }, flags, folders, noResults, root, selected);
        browser.ToolbarActions = (Wf.BrowserToolbarActions)toolbar;
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
            states["toolbar:" + key] = new(present, present && item.Enabled, Icon: IconPixels(item.Image));
        }
        foreach (var (key, native) in FolderMenu)
        {
            var item = Member<Forms.ToolStripItem>(browser, native);
            states["folder:" + key] = new(item.Available, item.Available && item.Enabled, Icon: IconPixels(item.Image));
        }
        foreach (var (key, native) in FileMenu)
        {
            var item = Member<Forms.ToolStripItem>(browser, native);
            states["file:" + key] = new(item.Available, item.Available && item.Enabled, Icon: IconPixels(item.Image));
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
    [TestCase(0, false, true, true, 0, false, (int)Av.BrowserToolbarActions.Default)]
    [TestCase(2047, false, true, true, 0, false, (int)Av.BrowserToolbarActions.Default)]
    [TestCase(2047, false, true, true, 1, false, (int)Av.BrowserToolbarActions.Default)]
    [TestCase(2047, false, true, true, 2, true, (int)Av.BrowserToolbarActions.Default)]
    [TestCase(2047, true, true, true, 0, true, (int)Av.BrowserToolbarActions.Default)]
    [TestCase(2047, false, false, false, 1, false, (int)Av.BrowserToolbarActions.Default)]
    [TestCase(1, false, true, true, 0, false, (int)Av.BrowserToolbarActions.Default)]
    [TestCase(2047, false, true, true, 1, false, (int)Av.BrowserToolbarActions.None)]
    [TestCase(2047, false, true, true, 1, false, (int)Av.BrowserToolbarActions.All)]
    public async Task Action_availability_permissions_and_selection_match_the_running_WinForms_browser(int flags, bool folders, bool sharing, bool versions, int selection, bool noResults, int toolbar)
    {
        var expected = Native(() => Reference(flags, folders, sharing, versions, selection, noResults, false, toolbar: toolbar));
        var browser = new Av.DmsBrowser(new DemoDmsProvider { SharingSupported = sharing, VersionsSupported = versions }, new Av.DmsBrowserOptions
        {
            AllowedActions = (Av.FileOrFolderActions)flags, SelectedFolder = "Documents", ToolbarActions = (Av.BrowserToolbarActions)toolbar,
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
                states["toolbar:" + key] = new(control.IsEffectivelyVisible, control.IsEffectivelyVisible && control.IsEnabled,
                    Icon: IconPixels((control.Content as StackPanel)?.Children.OfType<global::Avalonia.Controls.Image>().SingleOrDefault()));
            }
            foreach (var (key, _) in FolderMenu)
            {
                var item = browser.Browser.FolderTree.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == key);
                states["folder:" + key] = new(item.IsVisible, item.IsVisible && item.IsEnabled, Icon: IconPixels(item.Icon as global::Avalonia.Controls.Image));
            }
            foreach (var (key, _) in FileMenu)
            {
                var item = browser.Browser.FileGrid.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == key);
                states["file:" + key] = new(item.IsVisible, item.IsVisible && item.IsEnabled, Icon: IconPixels(item.Icon as global::Avalonia.Controls.Image));
            }
            foreach (var key in new[] { "ActionCreateFolder", "ActionOkay", "ActionCancel", "ActionClose", "ActionShowFiles" })
            {
                var control = browser.Browser.GetVisualDescendants().OfType<Button>().Single(b => b.Name == key);
                states["bottom:" + key] = new(control.IsEffectivelyVisible, control.IsEffectivelyVisible && control.IsEnabled,
                    control is ToggleButton check ? check.IsChecked : null);
            }
            using (Assert.EnterMultipleScope())
            {
                foreach (var key in expected.Controls.Keys) AssertState(states[key], expected.Controls[key], key);
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
        { AllowedActions = (Av.FileOrFolderActions)2047, InitialFolder = root, SelectedFolder = selected, ToolbarActions = Av.BrowserToolbarActions.All });
        try
        {
            browser.Show(); await browser.Browser.InitializeAsync();
            foreach (var (key, _) in FolderMenu)
            {
                var item = browser.Browser.FolderTree.ContextMenu!.Items.OfType<MenuItem>().Single(i => i.Name == key);
                AssertState(new State(item.IsVisible, item.IsVisible && item.IsEnabled, Icon: IconPixels(item.Icon as global::Avalonia.Controls.Image)),
                    expected.Controls["folder:" + key], key);
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
    [AvaloniaTest]
    [TestCase(false, false), TestCase(true, false), TestCase(true, true)]
    public async Task Appearance_modes_keep_both_browsers_readable(bool dark, bool custom)
    {
        var directory = Environment.GetEnvironmentVariable("DMS_THEME_SCREENSHOTS");
        Native(() =>
        {
            using var dispatcher = new UiTestDispatcher();
            using var browser = new ReferenceBrowser(new DemoDmsProvider(), 2047, false, true);
            browser.Appearance = dark ? Wf.BrowserTheme.Dark : Wf.BrowserTheme.Light;
            if (custom) browser.DarkPalette = new Wf.BrowserPalette("#172334", "#24364A", "#EDF2FA", "#5399D5", "#081524", "#647A90");
            browser.Opacity = 1; browser.Show(); dispatcher.WaitForEvents();
            var list = Member<Forms.ListView>(browser, "ListViewDmsFiles");
            Assert.That(list.BackColor.GetBrightness(), dark ? Is.LessThan(0.3f) : Is.GreaterThan(0.8f));
            Assert.That(list.ForeColor.GetBrightness(), dark ? Is.GreaterThan(0.8f) : Is.LessThan(0.3f));
            Assert.That(browser.Controls.Find("BrowserThemeSelector", false), Is.Empty);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                using var image = new System.Drawing.Bitmap(browser.Width, browser.Height);
                browser.DrawToBitmap(image, new System.Drawing.Rectangle(0, 0, browser.Width, browser.Height));
                image.Save(Path.Combine(directory, "winforms-" + (custom ? "custom" : dark ? "dark" : "light") + ".png"));
            }
            return true;
        });
        var window = new Av.DmsBrowser(new DemoDmsProvider(), new() { SelectedFolder = "Documents", AllowedActions = (Av.FileOrFolderActions)2047,
            DialogOperationMode = Av.DialogOperationModes.NoResults, Theme = dark ? Av.BrowserTheme.Dark : Av.BrowserTheme.Light,
            DarkPalette = custom ? new Av.BrowserPalette("#172334", "#24364A", "#EDF2FA", "#5399D5", "#081524", "#647A90") : null });
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); window.UpdateLayout();
            Assert.That(window.Browser.FileGrid.ActualThemeVariant, Is.EqualTo(dark ? global::Avalonia.Styling.ThemeVariant.Dark : global::Avalonia.Styling.ThemeVariant.Light));
            if (!string.IsNullOrEmpty(directory))
            {
                using var image = new global::Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
                image.Render(window); image.Save(Path.Combine(directory, "avalonia-" + (custom ? "custom" : dark ? "dark" : "light") + ".png"), global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
        }
        finally { window.Close(); }
    }

}
