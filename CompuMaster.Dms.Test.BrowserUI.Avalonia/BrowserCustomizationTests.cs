using System.ComponentModel;
using System.Reflection;
using CompuMaster.Dms.Data;
using NUnit.Framework;
#if DMS_WINUI
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.TestDemo.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace CompuMaster.Dms.Test.BrowserUI.WinUI;
#else
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;
#endif

[TestFixture]
public sealed class BrowserCustomizationTests
{
    private static Task Gui(Func<Task> action)
    {
#if DMS_WINUI
        return UiThread.Run(action);
#else
        return action();
#endif
    }
    private static IEnumerable<Control> Controls(DmsBrowserControl browser)
    {
#if DMS_WINUI
        return NativeUiTests.Descendants(browser).OfType<Control>();
#else
        return browser.GetVisualDescendants().OfType<Control>();
#endif
    }
    private static bool Visible(object control)
    {
#if DMS_WINUI
        return ((UIElement)control).Visibility == Visibility.Visible;
#else
        return ((Control)control).IsVisible;
#endif
    }
    private static Button Button(DmsBrowserControl browser, string name) => Controls(browser).OfType<Button>().Single(b => b.Name == name);
    private static void Click(Button button)
    {
#if DMS_WINUI
        var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
#else
        BrowserUiTests.Click(button);
#endif
    }
    private static string[] OrderedFiles(DmsBrowserControl browser)
    {
#if DMS_WINUI
        return browser.FileGrid.Items.Cast<ListViewItem>().Select(item => ((FileRow)item.Tag).Name).ToArray();
#else
        return browser.FileGrid.CollectionView.Cast<FileRow>().Select(row => row.Name).ToArray();
#endif
    }
    private static Control FileMenuAction(DmsBrowserControl browser, string key)
    {
#if DMS_WINUI
        return ((MenuFlyout)browser.FileGrid.ContextFlyout).Items.OfType<MenuFlyoutItem>().Single(item => item.Name == key);
#else
        return browser.FileGrid.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Name == key);
#endif
    }
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    [TestCase(BrowserToolbarActions.Default), TestCase(BrowserToolbarActions.None), TestCase(BrowserToolbarActions.All)]
    public Task Toolbar_customization_keeps_context_actions_and_collapses_empty_toolbar(BrowserToolbarActions toolbar) => Gui(async () =>
    {
        var window = new DmsBrowser(new DemoDmsProvider(), new DmsBrowserOptions
        { SelectedFolder = "Documents", AllowedActions = (FileOrFolderActions)2047, ToolbarActions = toolbar });
        try
        {
            await RefreshSelectionUiTests.Show(window); var browser = window.Browser;
#if DMS_WINUI
            await RefreshSelectionUiTests.Until(() => browser.IsLoaded); browser.UpdateLayout();
#endif
            foreach (var (key, flag) in new[] { ("ActionOpen", BrowserToolbarActions.Open), ("ActionUpload", BrowserToolbarActions.Upload),
                ("ActionDelete", BrowserToolbarActions.Delete), ("FileProperties", BrowserToolbarActions.FileProperties),
                ("FileVersionHistory", BrowserToolbarActions.VersionHistory), ("ActionCopy", BrowserToolbarActions.Copy) })
            {
                Assert.That(Visible(Button(browser, key)), Is.EqualTo((toolbar & flag) != 0), key);
                Assert.That(Visible(FileMenuAction(browser, key)), Is.True, key + " context menu");
            }
            var toolbarField = typeof(DmsBrowserControl).GetField("fileToolbar", BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.That(Visible(toolbarField.GetValue(browser)!), Is.EqualTo(toolbar != BrowserToolbarActions.None));
            if (toolbar == BrowserToolbarActions.None)
            {
#if DMS_WINUI
                var panel = (Grid)typeof(DmsBrowserControl).GetField("filePanel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(browser)!;
                Assert.That(panel.RowDefinitions[0].ActualHeight, Is.Zero.Within(1));
#else
                Assert.That(browser.FileGrid.Bounds.Y, Is.Zero.Within(1));
#endif
            }
        }
        finally { window.Close(); }
    });
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    [TestCase("de"), TestCase("ar")]
    public Task Visibility_does_not_grant_permissions_and_bottom_controls_leave_no_path_or_theme_selector(string culture) => Gui(async () =>
    {
        var previous = System.Globalization.CultureInfo.CurrentUICulture;
        System.Globalization.CultureInfo.CurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
        var window = new DmsBrowser(new DemoDmsProvider(), new DmsBrowserOptions
        { SelectedFolder = "Documents", ToolbarActions = BrowserToolbarActions.All, ShowFilesButtonVisible = false,
            CreateFolderButtonVisible = false, AllowedActions = FileOrFolderActions.AllowCreateFolders | FileOrFolderActions.AllowSwitchBrowseMode,
            DialogOperationMode = DialogOperationModes.NoResults });
        try
        {
            await RefreshSelectionUiTests.Show(window); var browser = window.Browser;
#if DMS_WINUI
            await RefreshSelectionUiTests.Until(() => browser.IsLoaded); browser.UpdateLayout();
#endif
            Assert.That(Visible(Button(browser, "ActionOpen")), Is.False);
            Assert.That(Visible(Button(browser, "ActionDelete")), Is.False);
            Assert.That(Visible(Button(browser, "ActionCreateFolder")), Is.False);
            Assert.That(Visible(Controls(browser).Single(c => c.Name == "ActionShowFiles")), Is.False);
            Assert.That(Controls(browser).OfType<ComboBox>().Any(c => c.Name == "BrowserThemeSelector"), Is.False);
            var status = typeof(DmsBrowserControl).GetField("status", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(browser)!;
#if DMS_WINUI
            Assert.That(((TextBlock)status).Visibility, Is.EqualTo(Visibility.Collapsed));
            var close = Button(browser, "ActionClose");
            var terminal = (DependencyObject)typeof(DmsBrowserControl).GetField("terminal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(browser)!;
            var footer = (Grid)Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(terminal);
            var right = close.TransformToVisual(footer).TransformBounds(new Windows.Foundation.Rect(0, 0, close.ActualWidth, close.ActualHeight)).Right;
            Assert.That(right, Is.EqualTo(footer.ActualWidth - close.Margin.Right).Within(2));
#else
            Assert.That(((TextBlock)status).IsVisible, Is.False);
            var close = Button(browser, "ActionClose"); var footer = (Control)close.Parent!.Parent!;
            var right = Math.Max(close.TranslatePoint(new global::Avalonia.Point(0, 0), footer)!.Value.X,
                close.TranslatePoint(new global::Avalonia.Point(close.Bounds.Width, 0), footer)!.Value.X);
            Assert.That(right, Is.EqualTo(footer.Bounds.Width - close.Margin.Right).Within(2));
#endif
        }
        finally { window.Close(); System.Globalization.CultureInfo.CurrentUICulture = previous; }
    });
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    public Task Drag_settings_preserve_download_and_move_permissions_and_enforce_path_boundaries() => Gui(async () =>
    {
        var provider = new DemoDmsProvider();
        var options = new DmsBrowserOptions { SelectedFolder = "Documents", AllowedActions = (FileOrFolderActions)2047 };
        var window = new DmsBrowser(provider, options);
        try
        {
            await RefreshSelectionUiTests.Show(window); var browser = window.Browser;
#if DMS_WINUI
            await RefreshSelectionUiTests.Until(() => browser.IsLoaded); browser.UpdateLayout();
#endif
            var file = browser.Session.Files[0].Item;
            var context = new RemoteDrag(provider, provider.InstanceID, options.InitialFolder, new[] { file });
            typeof(DmsBrowserControl).GetField("activeDrag", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(browser, context);
            var target = browser.Session.Roots[0].Children.Single(n => n.Name == "Empty");
            Assert.That(browser.ValidMoveTarget(context, target), Is.True);
            options.EnableInternalDragAndDrop = false;
            Assert.That(browser.ValidMoveTarget(context, target), Is.False);
            bool CanDownload() => (bool)typeof(DmsBrowserControl).GetProperty("CanDragDownload", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(browser)!;
            Assert.That(CanDownload(), Is.True, "Disabling internal moves must preserve unrestricted external copies.");
            var parent = Path.Combine(Path.GetTempPath(), "unittests_drag_boundary");
            options.LocalParentMustFolder = parent;
            Assert.That(CanDownload(), Is.False, "An external destination cannot be verified by the native drag API.");
            Assert.That(browser.CanDropLocalPaths(new[] { Path.Combine(parent, "file.txt") }), Is.True);
            Assert.That(browser.CanDropLocalPaths(new[] { Path.Combine(parent + "_other", "file.txt") }), Is.False);
            Assert.That(browser.CanDropLocalPaths(new[] { Path.Combine(parent, "..", "outside.txt") }), Is.False);
            Assert.That(browser.CanDropLocalPaths(new[] { Path.Combine(parent, "file.txt"), Path.Combine(parent + "_other", "file.txt") }), Is.False);
            Assert.That(browser.CanDropLocalPaths(new string?[] { null }), Is.False);
            options.EnableInternalDragAndDrop = true;
            Assert.That(browser.ValidMoveTarget(context, target), Is.True, "The local restriction does not block internal moves.");
            options.AllowedActions = FileOrFolderActions.AllowDownloadFiles;
            Assert.That(browser.ValidMoveTarget(context, target), Is.False);
        }
        finally { window.Close(); }
    });
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    [TestCase(DialogOperationModes.ReturnSelectedItems), TestCase(DialogOperationModes.NoResults)]
    public Task File_double_click_opens_by_default_and_none_or_missing_permission_do_nothing(DialogOperationModes mode) => Gui(async () =>
    {
        var provider = new DemoDmsProvider();
        var options = new DmsBrowserOptions { SelectedFolder = "Documents", DialogOperationMode = mode, AllowedActions = FileOrFolderActions.AllowDownloadFiles };
        var window = new DmsBrowser(provider, options);
        try
        {
            await RefreshSelectionUiTests.Show(window); var browser = window.Browser;
#if DMS_WINUI
            await RefreshSelectionUiTests.Until(() => browser.IsLoaded); browser.UpdateLayout();
#endif
            RefreshSelectionUiTests.Select(browser, browser.Session.Files.ToArray());
            var opened = new List<string>(); browser.OpenDownloadedFileItem = path => { opened.Add(path); return Task.CompletedTask; };
            var confirmed = false; browser.SelectionConfirmed += (_, _) => confirmed = true;
            options.FileDoubleClickAction = BrowserFileDoubleClickAction.None;
            await browser.ActivateSelectedFilesAsync(); Assert.That(opened, Is.Empty);
            options.FileDoubleClickAction = BrowserFileDoubleClickAction.Open;
            options.AllowedActions = FileOrFolderActions.AllowSelectOnly;
            await browser.ActivateSelectedFilesAsync(); Assert.That(opened, Is.Empty);
            options.AllowedActions = FileOrFolderActions.AllowDownloadFiles;
            await browser.ActivateSelectedFilesAsync();
            Assert.That(opened.Count, Is.EqualTo(2)); Assert.That(opened.All(File.Exists), Is.True);
            Assert.That(confirmed, Is.False);
        }
        finally { window.Close(); window.Browser.CleanupTemporaryFiles(); }
    });
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    public Task Sorting_starts_with_newest_and_toggles_with_headers_and_survives_refresh() => Gui(async () =>
    {
        var provider = new DemoDmsProvider();
        provider.Items.Single(i => i.Name == "Welcome.txt").LastModificationOnLocalTime = new DateTime(2026, 1, 1);
        provider.Items.Single(i => i.Name == "Shared.txt").LastModificationOnLocalTime = new DateTime(2026, 2, 1);
        var window = new DmsBrowser(provider, new DmsBrowserOptions { SelectedFolder = "Documents" });
        try
        {
            await RefreshSelectionUiTests.Show(window); var browser = window.Browser;
#if DMS_WINUI
            await RefreshSelectionUiTests.Until(() => browser.IsLoaded); browser.UpdateLayout();
#endif
#if !DMS_WINUI
            window.UpdateLayout();
#endif
            Assert.That(OrderedFiles(browser), Is.EqualTo(new[] { "Shared.txt", "Welcome.txt" }));
#if DMS_WINUI
            var date = Button(browser, "SortModified");
            Assert.That(date.Content!.ToString(), Does.EndWith("▼")); Click(date);
            Assert.That(date.Content!.ToString(), Does.EndWith("▲"));
#else
            browser.FileGrid.Columns[2].Sort(); await Task.Delay(20);
#endif
            Assert.That(OrderedFiles(browser), Is.EqualTo(new[] { "Welcome.txt", "Shared.txt" }));
            await browser.RefreshAsync();
            Assert.That(OrderedFiles(browser), Is.EqualTo(new[] { "Welcome.txt", "Shared.txt" }));
#if DMS_WINUI
            Click(date); Assert.That(date.Content!.ToString(), Does.EndWith("▼"));
#else
            browser.FileGrid.Columns[2].Sort(); await Task.Delay(20);
#endif
            Assert.That(OrderedFiles(browser), Is.EqualTo(new[] { "Shared.txt", "Welcome.txt" }));
#if DMS_WINUI
            var name = Button(browser, "SortName"); Click(name); Assert.That(name.Content!.ToString(), Does.EndWith("▲")); Click(name);
#else
            browser.FileGrid.Columns[0].Sort(); await Task.Delay(20); browser.FileGrid.Columns[0].Sort(); await Task.Delay(20);
#endif
            Assert.That(OrderedFiles(browser), Is.EqualTo(new[] { "Welcome.txt", "Shared.txt" }));
        }
        finally { window.Close(); }
    });
}
