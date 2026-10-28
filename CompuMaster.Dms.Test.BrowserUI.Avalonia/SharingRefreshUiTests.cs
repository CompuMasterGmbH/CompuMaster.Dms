using System.Reflection;
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class SharingRefreshUiTests
{
    [AvaloniaTest]
    [TestCase("save"), TestCase("cancel"), TestCase("delete"), TestCase("link-save"), TestCase("link-cancel")]
    public async Task Actual_share_changes_refresh_surviving_selection_but_canceling_editors_does_not(string action)
    {
        var provider = new FreshListingProvider();
        var window = new DmsBrowser(provider, new DmsBrowserOptions { SelectedFolder = "Documents", AllowedActions = (FileOrFolderActions)2047 });
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await RefreshSelectionUiTests.Show(window); var browser = window.Browser;
            var row = browser.Session.Files.Single(row => row.Name == "Shared.txt"); RefreshSelectionUiTests.Select(browser, new[] { row });
            await RefreshSelectionUiTests.ClickSharing(browser);
            await RefreshSelectionUiTests.Until(() => browser.ActiveSharingDialog is { IsRunning: false });
            var dialog = browser.ActiveSharingDialog!;
            object?[] arguments;
            string method;
            if (action == "delete")
            {
                var grid = dialog.GetVisualDescendants().OfType<DataGrid>().Single(grid => grid.Name == "InternalSharings");
                grid.SelectedItem = grid.ItemsSource.Cast<object>().Single(); method = "DeleteAsync"; arguments = new object?[] { grid };
            }
            else if (action.StartsWith("link-")) { method = "EditLinkAsync"; arguments = new object?[] { provider.Items.Single(item => item.Name == "Shared.txt").ExtendedInfosLinks.Single() }; }
            else { method = "EditStandardAsync"; arguments = new object?[] { false, provider.Items.Single(item => item.Name == "Shared.txt").ExtendedInfosUserSharings.Single() }; }
            var operation = (Task)typeof(DmsItemSharings).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(dialog, arguments)!;
            await RefreshSelectionUiTests.Until(() => dialog.OwnedWindows.Any());
            var editor = dialog.OwnedWindows.Single();
            await RefreshSelectionUiTests.Until(() => editor is not OperationWindow operationWindow || !operationWindow.IsRunning);
            if (action.EndsWith("cancel")) editor.Close();
            else BrowserUiTests.Click(BrowserUiTests.FindButton(editor, action == "delete" ? "ActionOkay" : "ActionSave"));
            await operation;
            var changed = !action.EndsWith("cancel"); Assert.That(dialog.HasChanges, Is.EqualTo(changed));
            Assert.That(RefreshSelectionUiTests.ProgressVisible(browser), Is.False);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.BeforeListing = (_, _) => { entered.TrySetResult(); return release.Task; };
            var calls = provider.Calls.Count(call => call.StartsWith("List:") || call.StartsWith("Reset:"));
            dialog.Close();
            if (changed)
            {
                await RefreshSelectionUiTests.Until(() => entered.Task.IsCompleted);
                Assert.That(RefreshSelectionUiTests.ProgressVisible(browser), Is.True, "Real refresh work still has a loading indicator.");
            }
            release.TrySetResult(); await RefreshSelectionUiTests.Until(() => !browser.IsBusy);
            var after = provider.Calls.Count(call => call.StartsWith("List:") || call.StartsWith("Reset:"));
            Assert.That(after > calls, Is.EqualTo(changed));
            Assert.That(browser.Selection.Files.Single().ExtendedInfosFileID, Is.EqualTo(row.Item.ExtendedInfosFileID));
        }
        finally
        {
            release.TrySetResult(); window.Browser.ActiveSharingDialog?.Close();
            await RefreshSelectionUiTests.Until(() => !window.Browser.IsBusy); window.Close();
        }
    }
}
