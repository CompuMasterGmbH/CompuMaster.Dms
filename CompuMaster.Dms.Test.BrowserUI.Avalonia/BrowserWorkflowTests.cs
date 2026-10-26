using System.Globalization;
using CompuMaster.Dms.BrowserUI.Avalonia;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;
using CompuMaster.Dms.TestDemo.Avalonia;
using NUnit.Framework;

namespace CompuMaster.Dms.Test.BrowserUI.Avalonia;

[TestFixture]
public sealed class BrowserWorkflowTests
{
    private static DmsBrowserOptions All(string? root = null) => new() { InitialFolder = root, AllowedActions = (FileOrFolderActions)255 };
    [Test]
    public async Task Initial_selection_is_relative_to_browser_root_and_outside_paths_are_rejected()
    {
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, new DmsBrowserOptions { InitialFolder = "/Documents", SelectedFolder = "Projects" });
        await session.InitializeAsync();
        Assert.That(session.SelectedFolder, Is.EqualTo("Projects"));
        Assert.That(session.Files.Single().Name, Is.EqualTo("Notes.txt"));
        Assert.That((Action)(() => session.RelativePath("/DocumentsOther")), Throws.InvalidOperationException);
        await session.SelectPathAsync(null);
        Assert.That(session.SelectedFolder, Is.Null);
        Assert.That((Func<Task>)(async () => await session.SelectPathAsync("../Empty")), Throws.InstanceOf<DmsUserInputInvalidException>());
    }
    [Test]
    public async Task Expansion_does_not_fetch_children_until_requested_and_preserves_unknown_and_zero()
    {
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync();
        var documents = session.Roots[0].Children.Single(n => n.Name == "Documents");
        var empty = session.Roots[0].Children.Single(n => n.Name == "Empty");
        Assert.That(documents.Loaded, Is.False);
        Assert.That(documents.Children.Single().Placeholder, Is.True);
        Assert.That(empty.Children, Is.Empty);
        Assert.That(provider.Calls.Any(c => c.StartsWith("List:/Documents:")), Is.False);
        await session.ExpandAsync(documents);
        Assert.That(documents.Children.Single().Name, Is.EqualTo("Projects"));
        var calls = provider.Calls.Count;
        await session.ExpandAsync(documents);
        Assert.That(provider.Calls.Count, Is.EqualTo(calls));
        provider.OmitChildMetadata = true;
        var unknown = new BrowserSession(provider, All());
        await unknown.InitializeAsync();
        var unknownEmpty = unknown.Roots[0].Children.Single(n => n.Name == "Empty");
        Assert.That(BrowserNode.HasChildren(unknownEmpty.Item), Is.Null);
        Assert.That(unknownEmpty.Children.Single().Placeholder, Is.True);
        await unknown.ExpandAsync(unknownEmpty);
        Assert.That(unknownEmpty.Children, Is.Empty);
        Assert.That(unknownEmpty.Item!.ChildDirectoryCount, Is.Zero);
    }
    [Test]
    public async Task Folder_only_mode_and_unsupported_root_files_do_not_list_files()
    {
        var provider = new DemoDmsProvider { RootFilesSupported = false };
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync();
        Assert.That(session.Files, Is.Empty);
        Assert.That(provider.Calls.Any(c => c == "List:/:Files"), Is.False);
        session.BrowseMode = BrowseModes.Folders;
        await session.SelectPathAsync("Documents");
        Assert.That(session.Files, Is.Empty);
        session.BrowseMode = BrowseModes.FoldersAndFiles;
        await session.SelectPathAsync("Documents");
        Assert.That(session.Files.Count, Is.EqualTo(2));
    }
    [Test]
    public async Task Unsupported_collections_are_filtered_without_provider_specific_consumer_flags()
    {
        var provider = new DemoDmsProvider { CollectionsSupported = false };
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync();
        Assert.That(session.Roots[0].Children.Any(n => n.Name == "Collection"), Is.False);
    }
    [Test]
    public async Task Duplicate_filenames_are_marked_and_download_keeps_selected_resource_identity()
    {
        using var local = new OwnedDirectory();
        var provider = new DemoDmsProvider(false);
        var one = provider.AddFile("/same.txt", "first", "id-1");
        var two = provider.AddFile("/same.txt", "second", "id-2");
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync();
        Assert.That(session.Files.All(f => f.Duplicate), Is.True);
        var target = Path.Combine(local.Path, "selected.txt");
        await session.DownloadAsync(new[] { (two, target) });
        Assert.That(File.ReadAllText(target), Is.EqualTo("second"));
        Assert.That(provider.LastDownloadedID, Is.EqualTo("id-2"));
        Assert.That((Func<Task>)(async () => await session.DownloadAsync(new[] { (one, target), (two, target) })), Throws.InstanceOf<DmsUserInputInvalidException>());
    }
    [Test]
    public async Task Mutations_honor_host_action_flags_and_protect_browser_root()
    {
        var provider = new DemoDmsProvider();
        var readOnly = new BrowserSession(provider, new DmsBrowserOptions());
        await readOnly.InitializeAsync();
        Assert.That((Func<Task>)(async () => await readOnly.CreateDirectoryAsync("Denied")), Throws.InvalidOperationException);
        var writable = new BrowserSession(provider, All("/Documents"));
        await writable.InitializeAsync();
        Assert.That((Func<Task>)(async () => await writable.DeleteAsync(new[] { writable.Roots[0].Item! })), Throws.InstanceOf<DmsUserInputInvalidException>());
        Assert.That((Func<Task>)(async () => await writable.ResourceActionAsync(writable.Roots[0].Item!, "/Elsewhere", ResourceAction.Copy, _ => Task.FromResult(true))), Throws.InstanceOf<DmsUserInputInvalidException>());
        Assert.That(provider.Calls.Any(c => c.StartsWith("Delete:") || c.StartsWith("Create:")), Is.False);
    }
    [TestCase("")]
    [TestCase("..")]
    [TestCase(".")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    public void Invalid_names_are_rejected(string name)
    {
        var session = new BrowserSession(new DemoDmsProvider(), All());
        Assert.That((Action)(() => session.ValidateName(name)), Throws.InstanceOf<DmsUserInputInvalidException>());
    }
    [Test]
    public async Task Create_refresh_and_delete_reconcile_tree_and_child_metadata()
    {
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync();
        await session.SelectPathAsync("Empty");
        await session.CreateDirectoryAsync("Fresh");
        Assert.That(session.SelectedFolder, Is.EqualTo("Empty/Fresh"));
        var fresh = session.SelectedNode!;
        Assert.That(fresh.Item!.ChildDirectoryCount, Is.Zero);
        Assert.That(fresh.Parent!.Item!.ChildDirectoryCount, Is.EqualTo(1));
        await session.DeleteAsync(new[] { fresh.Item });
        Assert.That(session.SelectedFolder, Is.EqualTo("Empty"));
        Assert.That(session.SelectedNode!.Children, Is.Empty);
        Assert.That(session.SelectedNode.Item!.HasChildDirectories, Is.False);
    }
    [Test]
    public async Task Copy_rename_move_validate_destinations_and_confirm_overwrite()
    {
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, All("/Documents"));
        await session.InitializeAsync();
        var file = session.Files.Single(f => f.Name == "Welcome.txt").Item;
        Assert.That(await session.ResourceActionAsync(file, "/Documents/Shared.txt", ResourceAction.Copy, _ => Task.FromResult(false)), Is.False);
        Assert.That(await session.ResourceActionAsync(file, "/Documents/Copy.txt", ResourceAction.Copy, _ => throw new AssertionException("Unexpected overwrite prompt.")), Is.True);
        var copy = provider.ListRemoteItem("/Documents/Copy.txt")!;
        await session.ResourceActionAsync(copy, "/Documents/Renamed.txt", ResourceAction.Rename, _ => Task.FromResult(true));
        var renamed = provider.ListRemoteItem("/Documents/Renamed.txt")!;
        await session.ResourceActionAsync(renamed, "/Documents/Projects/Moved.txt", ResourceAction.Move, _ => Task.FromResult(true));
        Assert.That(provider.ListRemoteItem("/Documents/Projects/Moved.txt"), Is.Not.Null);
        Assert.That(provider.ListRemoteItem("/Documents/Renamed.txt"), Is.Null);
        Assert.That((Func<Task>)(async () => await session.ResourceActionAsync(file, file.FullName, ResourceAction.Copy, _ => Task.FromResult(true))), Throws.InstanceOf<DmsUserInputInvalidException>());
        Assert.That((Func<Task>)(async () => await session.ResourceActionAsync(file, "/Elsewhere.txt", ResourceAction.Copy, _ => Task.FromResult(true))), Throws.InvalidOperationException);
    }
    [Test]
    public async Task Partial_failure_refreshes_state_and_preserves_original_error()
    {
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync(); await session.SelectPathAsync("Documents");
        var items = session.Files.Select(f => f.Item).ToArray();
        var original = new IOException("Injected deletion failure.");
        provider.Failure = call => call == "Delete:" + items[1].FullName ? original : null;
        var ex = Assert.ThrowsAsync<InvalidOperationException>((Func<Task>)(async () => await session.DeleteAsync(items)));
        Assert.That(ex!.InnerException, Is.SameAs(original));
        Assert.That(session.Files.Any(f => f.Item == items[0]), Is.False);
        Assert.That(session.Files.Any(f => f.Item == items[1]), Is.True);
        Assert.That(session.IsBusy, Is.False);
    }
    [Test]
    public async Task External_directory_deletion_removes_stale_selection()
    {
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync(); await session.SelectPathAsync("Documents");
        provider.Items.RemoveAll(i => i.FullName == "/Documents" || i.FullName.StartsWith("/Documents/"));
        await session.RefreshAsync();
        Assert.That(session.SelectedNode, Is.EqualTo(session.Roots[0]));
        Assert.That(session.Files.Select(f => f.Name), Is.EqualTo(new[] { "Readme.txt" }));
    }
    [Test]
    public async Task Operations_are_exclusive_and_cancellation_restores_busy_state()
    {
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.BeforeListing = async (_, token) => await gate.Task.WaitAsync(token);
        using var cancellation = new CancellationTokenSource();
        var pending = session.SelectPathAsync("Documents", cancellation.Token);
        Assert.That(session.IsBusy, Is.True);
        Assert.That((Func<Task>)(async () => await session.RefreshAsync()), Throws.InvalidOperationException);
        cancellation.Cancel();
        Assert.That((Func<Task>)(async () => await pending), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(session.IsBusy, Is.False);
    }
    [Test]
    public async Task Local_restrictions_are_enforced_before_any_transfer()
    {
        using var local = new OwnedDirectory();
        var allowed = Path.Combine(local.Path, "allowed"); Directory.CreateDirectory(allowed);
        var outside = Path.Combine(local.Path, "outside.txt"); File.WriteAllText(outside, "private");
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, new DmsBrowserOptions { AllowedActions = (FileOrFolderActions)255, LocalParentMustFolder = allowed });
        await session.InitializeAsync();
        Assert.That((Func<Task>)(async () => await session.UploadAsync(new[] { outside })), Throws.InstanceOf<DmsUserInputInvalidException>());
        Assert.That((Func<Task>)(async () => await session.DownloadAsync(new[] { (session.Files.Single().Item, outside) })), Throws.InstanceOf<DmsUserInputInvalidException>());
        Assert.That(provider.Calls.Any(c => c.StartsWith("Upload:") || c.StartsWith("Download:")), Is.False);
        Assert.That((Action)(() => LocalPaths.Validate(Path.Combine(allowed, "..", "outside.txt"), allowed)), Throws.InstanceOf<DmsUserInputInvalidException>());
        Assert.That((Action)(() => LocalPaths.SafeChild(allowed, "../outside.txt")), Throws.InstanceOf<DmsUserInputInvalidException>());
    }
    [Test]
    public void Local_symlink_cannot_escape_required_transfer_directory()
    {
        using var local = new OwnedDirectory();
        var allowed = Path.Combine(local.Path, "allowed"); var outside = Path.Combine(local.Path, "outside");
        Directory.CreateDirectory(allowed); Directory.CreateDirectory(outside);
        var link = Path.Combine(allowed, "link");
        try { Directory.CreateSymbolicLink(link, outside); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException) { Assert.Ignore("Symbolic link creation unavailable: " + ex.GetType().Name); }
        try { Assert.That((Action)(() => LocalPaths.Validate(Path.Combine(link, "file.txt"), allowed)), Throws.InstanceOf<DmsUserInputInvalidException>()); }
        finally { Directory.Delete(link); }
    }
    [Test]
    public async Task Upload_and_download_preserve_bytes_and_timestamp()
    {
        using var local = new OwnedDirectory();
        var source = Path.Combine(local.Path, "payload.bin");
        File.WriteAllBytes(source, Enumerable.Range(0, 256).Select(i => (byte)i).ToArray());
        var provider = new DemoDmsProvider();
        var session = new BrowserSession(provider, All());
        await session.InitializeAsync(); await session.SelectPathAsync("Documents");
        await session.UploadAsync(new[] { source });
        var file = session.Files.Single(f => f.Name == "payload.bin").Item;
        var target = Path.Combine(local.Path, "download.bin");
        await session.DownloadAsync(new[] { (file, target) });
        Assert.That(File.ReadAllBytes(target), Is.EqualTo(File.ReadAllBytes(source)));
        Assert.That(File.GetLastWriteTime(target), Is.EqualTo(file.LastModificationOnLocalTime!.Value).Within(TimeSpan.FromSeconds(2)));
    }
    [TestCase("en", "Name")]
    [TestCase("de", "Name")]
    public void Localization_uses_linked_resources_without_WinForms_dependency(string culture, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture); Assert.That(UiText.Get("ColumnName"), Is.EqualTo(expected)); Assert.That(UiText.Get("ActionUpload"), Is.Not.EqualTo("ActionUpload")); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}

internal sealed class OwnedDirectory : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DmsAvaloniaTests", Guid.NewGuid().ToString("N"));
    internal OwnedDirectory()
    {
        if (Directory.Exists(Path)) throw new IOException("The test-owned directory already exists.");
        Directory.CreateDirectory(Path);
    }
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Path, true);
        }
        Assert.That(Directory.Exists(Path), Is.False, "Test-owned local resources were not removed.");
    }
}
