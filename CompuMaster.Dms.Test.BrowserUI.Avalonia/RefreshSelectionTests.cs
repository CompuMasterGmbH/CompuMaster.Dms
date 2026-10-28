using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using NUnit.Framework;
#if DMS_WINUI
using CompuMaster.Dms.BrowserUI.WinUI;
using CompuMaster.Dms.TestDemo.WinUI;
namespace CompuMaster.Dms.Test.BrowserUI.WinUI;
#else
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.TestDemo.Avalonia;
namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;
#endif

internal sealed class FreshListingProvider : DemoDmsProvider
{
    public override List<DmsResourceItem> ListAllRemoteItems(string path, SearchItemType type) =>
        base.ListAllRemoteItems(path, type).Select(item => new DmsResourceItem
        {
            ItemType = item.ItemType, Name = item.Name, FullName = item.FullName, Folder = item.Folder,
            ExtendedInfosFileID = item.ExtendedInfosFileID, ExtendedInfosFolderID = item.ExtendedInfosFolderID,
            ExtendedInfosCollectionID = item.ExtendedInfosCollectionID, HasChildDirectories = item.HasChildDirectories, ChildDirectoryCount = item.ChildDirectoryCount,
            ContentLength = item.ContentLength, LastModificationOnLocalTime = item.LastModificationOnLocalTime,
            ExtendedInfosOwner = item.ExtendedInfosOwner, ExtendedInfosUserSharings = item.ExtendedInfosUserSharings,
            ExtendedInfosGroupSharings = item.ExtendedInfosGroupSharings, ExtendedInfosLinks = item.ExtendedInfosLinks,
            ExtendedInfosHasUserSharings = item.ExtendedInfosHasUserSharings, ExtendedInfosHasGroupSharings = item.ExtendedInfosHasGroupSharings,
            ExtendedInfosHasOutgoingSharings = item.ExtendedInfosHasOutgoingSharings, ExtendedInfosIsReceivedShare = item.ExtendedInfosIsReceivedShare
        }).ToList();
}

[TestFixture]
public sealed class RefreshSelectionTests
{
    [Test]
    public async Task Large_multiselection_matches_fresh_rows_without_losing_or_duplicating_entries()
    {
        var provider = new FreshListingProvider();
        for (var index = 0; index < 2000; index++) provider.AddFile($"/Documents/unittests_bulk_{index}.txt", "fixture");
        var session = new BrowserSession(provider, new DmsBrowserOptions { SelectedFolder = "Documents" });
        await session.InitializeAsync();
        var ids = session.Files.Select(row => row.Item.ExtendedInfosFileID).ToArray();
        var snapshot = new FileSelectionSnapshot(); snapshot.Capture(session, session.Files.Select(row => row.Item));
        await session.RefreshAsync();
        var retained = snapshot.Retained(session);
        Assert.That(retained.Select(row => row.Item.ExtendedInfosFileID), Is.EquivalentTo(ids));
        Assert.That(retained.Distinct().Count(), Is.EqualTo(ids.Length));
    }
    [Test]
    public async Task Refresh_retains_stable_ids_and_unambiguous_paths_but_rejects_replacements_and_ambiguity()
    {
        var provider = new FreshListingProvider();
        var first = provider.Items.Single(item => item.Name == "Welcome.txt");
        var missing = provider.AddFile("/Documents/Missing.txt", "missing");
        var pathOnly = provider.AddFile("/Documents/PathOnly.txt", "path"); pathOnly.ExtendedInfosFileID = null;
        var ambiguous = provider.AddFile("/Documents/Ambiguous.txt", "ambiguous"); ambiguous.ExtendedInfosFileID = null;
        var session = new BrowserSession(provider, new DmsBrowserOptions { SelectedFolder = "Documents" });
        await session.InitializeAsync();
        var snapshot = new FileSelectionSnapshot(); snapshot.Capture(session, session.Files.Select(row => row.Item));
        var original = session.Files.ToArray();
        first.Name = "Renamed.txt"; first.FullName = "/Documents/Renamed.txt";
        provider.Items.RemoveAll(item => item.Name == "Shared.txt"); provider.AddFile("/Documents/Shared.txt", "replacement");
        provider.Items.Remove(missing);
        provider.AddFile(ambiguous.FullName, "duplicate").ExtendedInfosFileID = null;
        await session.RefreshAsync();
        var retained = snapshot.Retained(session);
        Assert.That(retained.Select(row => row.Name), Is.EquivalentTo(new[] { "Renamed.txt", "PathOnly.txt" }));
        Assert.That(retained.All(row => !original.Contains(row)), Is.True, "A refresh returns new row and resource instances.");
    }
    [TestCase("directory"), TestCase("instance"), TestCase("root"), TestCase("folder-replacement")]
    public async Task Selection_is_not_reused_across_changed_contexts(string change)
    {
        var provider = new FreshListingProvider();
        var session = new BrowserSession(provider, new DmsBrowserOptions { SelectedFolder = "Documents" });
        await session.InitializeAsync();
        var snapshot = new FileSelectionSnapshot(); snapshot.Capture(session, session.Files.Select(row => row.Item));
        if (change == "directory") await session.SelectPathAsync("Documents/Projects");
        else if (change == "instance") provider.InstanceID = "other-instance";
        else if (change == "root") await session.InitializeAsync();
        else
        {
            provider.Items.Single(item => item.FullName == "/Documents").ExtendedInfosFolderID = "replacement-folder";
            await session.RefreshAsync();
        }
        Assert.That(snapshot.Retained(session), Is.Empty);
    }
    [Test]
    public async Task Failed_file_refresh_keeps_the_previous_listing_and_selection()
    {
        var provider = new FreshListingProvider();
        var session = new BrowserSession(provider, new DmsBrowserOptions { SelectedFolder = "Documents" });
        await session.InitializeAsync(); var original = session.Files.ToArray();
        var snapshot = new FileSelectionSnapshot(); snapshot.Capture(session, original.Select(row => row.Item));
        provider.Failure = call => call == "List:/Documents:Files" ? new IOException("Offline fixture failure") : null;
        Assert.ThrowsAsync<IOException>((Func<Task>)(async () => await session.RefreshAsync()));
        Assert.That(session.Files, Is.EqualTo(original));
        Assert.That(snapshot.Retained(session), Is.EqualTo(original));
    }
}
