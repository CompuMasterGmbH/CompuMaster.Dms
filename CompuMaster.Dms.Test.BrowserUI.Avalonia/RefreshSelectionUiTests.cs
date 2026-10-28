using CompuMaster.Dms.Data;
using NUnit.Framework;
#if DMS_WINUI
using CompuMaster.Dms.BrowserUI.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace CompuMaster.Dms.Test.BrowserUI.WinUI;
#else
using CompuMaster.Dms.BrowserUI.Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;
#endif

[TestFixture]
public sealed class RefreshSelectionUiTests
{
    private static Task Gui(Func<Task> action)
    {
#if DMS_WINUI
        return UiThread.Run(action);
#else
        return action();
#endif
    }
    internal static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ready()) { if (DateTime.UtcNow > deadline) Assert.Fail("The offline UI did not reach the expected state."); await Task.Delay(10); }
    }
    internal static async Task Show(DmsBrowser window)
    {
#if DMS_WINUI
        _ = window.ShowAsync(); await window.Browser.InitializeAsync();
#else
        window.Show();
#endif
        await Until(() => !window.Browser.IsBusy); await Task.Delay(20);
    }
    internal static void Select(DmsBrowserControl browser, IEnumerable<FileRow> rows)
    {
#if DMS_WINUI
        browser.FileGrid.SelectedItems.Clear();
        foreach (var row in rows) browser.FileGrid.SelectedItems.Add(browser.FileGrid.Items.Cast<ListViewItem>().Single(item => ReferenceEquals(item.Tag, row)));
#else
        var grid = browser.GetVisualDescendants().OfType<DataGrid>().Single(grid => grid.Name == "DmsFiles");
        grid.SelectedItems.Clear(); foreach (var row in rows) grid.SelectedItems.Add(row);
#endif
    }
    internal static async Task ClickSharing(DmsBrowserControl browser)
    {
#if DMS_WINUI
        var button = NativeUiTests.Descendants(browser).OfType<Button>().Single(button => button.Name == "ActionFileSharings");
        await Until(() => button.IsEnabled);
        var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button);
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)peer.GetPattern(Microsoft.UI.Xaml.Automation.Peers.PatternInterface.Invoke)).Invoke();
#else
        var button = BrowserUiTests.FindButton(browser, "ActionFileSharings");
        await Until(() => button.IsEnabled); BrowserUiTests.Click(button);
#endif
    }
    internal static bool ProgressVisible(DmsBrowserControl browser)
    {
#if DMS_WINUI
        return NativeUiTests.Descendants(browser).OfType<ProgressBar>().Single().Visibility == Visibility.Visible;
#else
        return browser.GetVisualDescendants().OfType<ProgressBar>().Single().IsVisible;
#endif
    }
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    [TestCase(false), TestCase(true)]
    public Task Refresh_restores_surviving_multiselection_with_fresh_resource_instances(bool removeOne) => Gui(async () =>
    {
        var provider = new FreshListingProvider();
        var window = new DmsBrowser(provider, new DmsBrowserOptions { SelectedFolder = "Documents" });
        try
        {
            await Show(window); var browser = window.Browser;
            Select(browser, browser.Session.Files.ToArray());
            var selected = browser.Selection.Files.ToArray(); Assert.That(selected.Length, Is.EqualTo(2));
            if (removeOne) provider.Items.RemoveAll(item => item.Name == "Shared.txt");
            await browser.RefreshAsync(); await Task.Delay(20);
            var expected = selected.Where(item => !removeOne || item.Name != "Shared.txt").Select(item => item.ExtendedInfosFileID);
            Assert.That(browser.Selection.Files.Select(item => item.ExtendedInfosFileID), Is.EquivalentTo(expected));
            Assert.That(browser.Selection.Files.All(item => !selected.Any(old => ReferenceEquals(item, old))), Is.True);
        }
        finally { window.Close(); }
    });
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    public Task Navigating_away_and_back_does_not_restore_a_stale_selection() => Gui(async () =>
    {
        var window = new DmsBrowser(new FreshListingProvider(), new DmsBrowserOptions { SelectedFolder = "Documents" });
        try
        {
            await Show(window); var browser = window.Browser; Select(browser, browser.Session.Files.Take(1).ToArray());
            await browser.Session.SelectPathAsync("Documents/Projects"); await browser.Session.SelectPathAsync("Documents");
            Assert.That(browser.Selection.Files, Is.Empty);
        }
        finally { window.Close(); }
    });
#if DMS_WINUI
    [Test]
#else
    [AvaloniaTest]
#endif
    public Task Idle_sharing_dialog_keeps_interaction_protected_without_parent_progress_or_an_unnecessary_refresh() => Gui(async () =>
    {
        var provider = new FreshListingProvider();
        var window = new DmsBrowser(provider, new DmsBrowserOptions { SelectedFolder = "Documents", AllowedActions = (FileOrFolderActions)2047 });
        try
        {
            await Show(window); var browser = window.Browser;
            var row = browser.Session.Files.Single(row => row.Name == "Welcome.txt"); Select(browser, new[] { row });
            await ClickSharing(browser); await Until(() => browser.ActiveSharingDialog is { IsRunning: false });
            Assert.That(browser.IsBusy, Is.True, "An owned dialog still prevents concurrent browser actions.");
            Assert.That(ProgressVisible(browser), Is.False, "Waiting for dialog input is not background work.");
            var calls = provider.Calls.Count(call => call.StartsWith("List:") || call.StartsWith("Reset:"));
            Assert.That(browser.ActiveSharingDialog!.HasChanges, Is.False);
            browser.ActiveSharingDialog.Close(); await Until(() => !browser.IsBusy);
            Assert.That(provider.Calls.Count(call => call.StartsWith("List:") || call.StartsWith("Reset:")), Is.EqualTo(calls));
            Assert.That(browser.Selection.Files.Single().ExtendedInfosFileID, Is.EqualTo(row.Item.ExtendedInfosFileID));
            Assert.That(ProgressVisible(browser), Is.False);
        }
        finally { window.Browser.ActiveSharingDialog?.Close(); await Until(() => !window.Browser.IsBusy); window.Close(); }
    });
}
