using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Headless.NUnit;
using global::Avalonia.VisualTree;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class WebDavDetailsTests
{
    private sealed class PathOnlyProvider : DemoDmsProvider
    {
        internal int IdentifierLookups;
        public override DmsResourceItem? FindFileById(string id)
        { IdentifierLookups++; throw new NotImplementedException("WebDAV exposes identity metadata without an identifier lookup API."); }
        public override DmsResourceItem? FindFolderById(string id)
        { IdentifierLookups++; throw new NotImplementedException("WebDAV exposes identity metadata without an identifier lookup API."); }
    }

    [Test]
    public async Task File_identity_metadata_does_not_trigger_an_unsupported_identifier_lookup()
    {
        var provider = new PathOnlyProvider();
        var selected = provider.Items.Single(i => i.Name == "Welcome.txt");
        selected.ExtendedInfosAssignedFolderID = "selected-parent";
        var details = await ResourceDetails.LoadAsync(provider, selected);
        Assert.That(details.ExtendedInfosFileID, Is.EqualTo(selected.ExtendedInfosFileID));
        Assert.That(details.FullName, Is.EqualTo(selected.FullName));
        Assert.That(details.ExtendedInfosAssignedFolderID, Is.EqualTo("selected-parent"));
        Assert.That(provider.IdentifierLookups, Is.Zero);
        Assert.That(provider.Calls, Does.Contain("Item:" + selected.FullName));
    }

    [TestCase("different", DmsResourceItem.ItemTypes.File)]
    [TestCase(null, DmsResourceItem.ItemTypes.File)]
    [TestCase("selected", DmsResourceItem.ItemTypes.Folder)]
    public void Path_lookup_rejects_a_replaced_file_missing_identity_or_changed_item_type(string? returnedId, DmsResourceItem.ItemTypes returnedKind)
    {
        var provider = new PathOnlyProvider();
        var selected = new DmsResourceItem { ItemType = DmsResourceItem.ItemTypes.File,
            FullName = "/Documents/Welcome.txt", ExtendedInfosFileID = "selected" };
        var current = provider.Items.Single(i => i.Name == "Welcome.txt");
        current.ExtendedInfosFileID = returnedId; current.ItemType = returnedKind;
        Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => await ResourceDetails.LoadAsync(provider, selected)));
        Assert.That(provider.IdentifierLookups, Is.Zero);
    }

    [Test]
    public async Task Identifier_based_providers_still_resolve_the_selected_duplicate_by_id()
    {
        var provider = new DemoDmsProvider(false) { ProviderKind = BaseDmsProvider.DmsProviders.Scopevisio };
        provider.AddFile("/same.txt", "first", "first");
        var selected = provider.AddFile("/same.txt", "second", "second");
        var result = await ResourceDetails.LoadAsync(provider, selected);
        Assert.That(result.ExtendedInfosFileID, Is.EqualTo("second"));
        Assert.That(provider.Calls.Any(c => c.StartsWith("Item:")), Is.False);
    }

    [AvaloniaTest]
    [TestCase("ActionFileSharings")]
    [TestCase("FileProperties")]
    public async Task File_action_opens_its_dialog_for_WebDAV_entries_with_file_ids(string action)
    {
        var provider = new PathOnlyProvider();
        var window = new DmsBrowser(provider, new DmsBrowserOptions
            { SelectedFolder = "Documents", AllowedActions = (FileOrFolderActions)2047 });
        Exception? failure = null;
        window.Browser.OperationFailed += (_, args) => { failure = args.Exception; args.SuppressDialog = true; };
        try
        {
            window.Show(); await window.Browser.InitializeAsync(); await BrowserUiTests.IdleAsync(window.Browser);
            window.Browser.FileGrid.SelectedItem = window.Browser.Session.Files.Single(f => f.Name == "Welcome.txt");
            BrowserUiTests.Click(BrowserUiTests.FindButton(window.Browser, action));
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (window.OwnedWindows.Count == 0 && failure is null && DateTime.UtcNow < deadline) await Task.Delay(5);
            Assert.That(failure, Is.Null);
            Assert.That(provider.IdentifierLookups, Is.Zero);
            var dialog = window.OwnedWindows.Single();
            if (action == "ActionFileSharings") Assert.That(dialog, Is.TypeOf<DmsItemSharings>());
            else Assert.That(dialog.GetVisualDescendants().OfType<TextBox>().Single().Text,
                Does.Contain("/Documents/Welcome.txt").And.Contain("Document owner"));
        }
        finally
        {
            foreach (var child in window.OwnedWindows.ToArray())
            {
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (child is OperationWindow { IsRunning: true } && DateTime.UtcNow < deadline) await Task.Delay(5);
                child.Close();
            }
            await BrowserUiTests.IdleAsync(window.Browser);
            window.Close();
        }
    }
}
