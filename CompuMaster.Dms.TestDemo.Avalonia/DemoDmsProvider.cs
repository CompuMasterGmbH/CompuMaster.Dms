using System.Text;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

namespace CompuMaster.Dms.TestDemo.Avalonia;

// A deterministic, process-local demonstration backend. It never accesses remote DMS servers.
internal class DemoDmsProvider : NoDmsProvider, IDmsInstanceProvider
{
    internal readonly List<DmsResourceItem> Items = new();
    private readonly Dictionary<string, byte[]> content = new();
    internal readonly List<string> Calls = new();
    internal bool SharingSupported = true;
    internal bool CollectionsSupported = true;
    internal bool RootFilesSupported = true;
    internal bool VersionsSupported = true;
    internal bool OmitChildMetadata { get; set; }
    internal BaseDmsProvider.DmsProviders ProviderKind = DmsProviders.WebDAV;
    internal Func<string, Exception?>? Failure { get; set; }
    internal Func<string?, CancellationToken, Task>? BeforeListing { get; set; }
    internal string? LastDownloadedID;
    internal string InstanceID = "demo";
    public override string Name => "Local demonstration DMS";
    public override string BrowseInRootFolderName => "/";
    public override DmsProviders DmsProviderID => ProviderKind;
    public override bool SupportsSharingSetup => SharingSupported;
    public override bool SupportsCollections => CollectionsSupported;
    public override bool SupportsFilesInRootFolder => RootFilesSupported;
    public override bool SupportsFileVersionHistory => VersionsSupported;
    public override bool SupportsFileVersionDownload => VersionsSupported;
    public override bool SupportsFileVersionRestore => VersionsSupported;
    public override bool SupportsFileVersionRemoval => VersionsSupported;
    public override string CurrentContextUserID => "me";
    private void Check(string call)
    {
        Calls.Add(call);
        if (Failure?.Invoke(call) is { } ex) throw ex;
    }
    internal DemoDmsProvider(bool seed = true)
    {
        if (!seed) return;
        AddDirectory("/Documents");
        AddDirectory("/Documents/Projects");
        AddDirectory("/Empty");
        AddDirectory("/Collection", DmsResourceItem.ItemTypes.Collection);
        AddFile("/Documents/Welcome.txt", "Avalonia DMS browser demonstration.\nUploads and changes stay in this process.");
        AddFile("/Documents/Projects/Notes.txt", "This nested folder is loaded on demand.");
        AddFile("/Readme.txt", "Select Documents to explore files, properties and sharing dialogs.");
        var shared = AddFile("/Documents/Shared.txt", "Example shared file.");
        shared.ExtendedInfosUserSharings.Add(new DmsShareForUser(shared, new DmsUser { ID = "alice", DisplayName = "Alice" }, true, true, false, false, false, false));
        shared.ExtendedInfosHasUserSharings = shared.ExtendedInfosIsShared = true;
        var link = new DmsLink(shared, this) { ID = "demo-link", Name = "Example link", AllowView = true, AllowDownload = true, WebUrl = "https://example.invalid/demo-link" };
        shared.ExtendedInfosLinks.Add(link);
    }
    internal DmsResourceItem AddDirectory(string path, DmsResourceItem.ItemTypes type = DmsResourceItem.ItemTypes.Folder)
    { var item = NewItem(path, type); Items.Add(item); return item; }
    internal DmsResourceItem AddFile(string path, string value, string? id = null)
    {
        var item = NewItem(path, DmsResourceItem.ItemTypes.File);
        item.ExtendedInfosFileID = id ?? Guid.NewGuid().ToString("N");
        content[item.ExtendedInfosFileID] = Encoding.UTF8.GetBytes(value);
        item.ContentLength = content[item.ExtendedInfosFileID].Length; Items.Add(item); return item;
    }
    private DmsResourceItem NewItem(string path, DmsResourceItem.ItemTypes type) => new()
    {
        Name = ItemName(path), FullName = path, Folder = ParentDirectoryPath(path), ItemType = type,
        LastModificationOnLocalTime = DateTime.Now, CreatedOnLocalTime = DateTime.Now,
        ExtendedInfosOwner = new DmsUser { ID = "owner", DisplayName = "Document owner" },
        ExtendedInfosFileID = type == DmsResourceItem.ItemTypes.File ? Guid.NewGuid().ToString("N") : null,
        ExtendedInfosFolderID = type == DmsResourceItem.ItemTypes.Folder ? Guid.NewGuid().ToString("N") : null,
        ExtendedInfosCollectionID = type == DmsResourceItem.ItemTypes.Collection ? Guid.NewGuid().ToString("N") : null,
        ExtendedInfosUserSharings = new(), ExtendedInfosGroupSharings = new(), ExtendedInfosLinks = new()
    };
    private static string Normal(string? path) => string.IsNullOrEmpty(path) || path == "/" ? "/" : "/" + path.Trim('/');
    private DmsResourceItem Metadata(DmsResourceItem item)
    {
        if (item.ItemType != DmsResourceItem.ItemTypes.File && !OmitChildMetadata)
        {
            item.ChildDirectoryCount = Items.Count(i => i.ItemType != DmsResourceItem.ItemTypes.File && Normal(i.Folder) == Normal(item.FullName));
            item.HasChildDirectories = item.ChildDirectoryCount > 0;
        }
        else { item.ChildDirectoryCount = null; item.HasChildDirectories = null; }
        return item;
    }
    public override void ResetCachesForRemoteItems(string remoteFolderPath, SearchItemType searchType) { Check("Reset:" + remoteFolderPath); }
    public override DmsResourceItem? ListRemoteItem(string remotePath)
    {
        Check("Item:" + remotePath);
        if (Normal(remotePath) == "/") return NewItem("/", DmsResourceItem.ItemTypes.Root);
        return Items.FirstOrDefault(i => Normal(i.FullName) == Normal(remotePath)) is { } item ? Metadata(item) : null;
    }
    public override List<DmsResourceItem> ListAllRemoteItems(string remoteFolderPath, SearchItemType searchType)
    {
        Check("List:" + Normal(remoteFolderPath) + ":" + searchType);
        if (Normal(remoteFolderPath) != "/" && !Items.Any(i => Normal(i.FullName) == Normal(remoteFolderPath) && i.ItemType != DmsResourceItem.ItemTypes.File))
            throw new CompuMaster.Dms.Data.DirectoryNotFoundException(remoteFolderPath);
        return Items.Where(i => Normal(i.Folder) == Normal(remoteFolderPath) && (searchType == SearchItemType.AllItems ||
            searchType == SearchItemType.Files && i.ItemType == DmsResourceItem.ItemTypes.File ||
            searchType == SearchItemType.Folders && i.ItemType == DmsResourceItem.ItemTypes.Folder ||
            searchType == SearchItemType.Collections && i.ItemType == DmsResourceItem.ItemTypes.Collection)).Select(Metadata).ToList();
    }
    public override async Task<List<DmsResourceItem>> ListAllRemoteItemsAsync(string remoteFolderPath, SearchItemType searchType, CancellationToken cancellationToken = default)
    {
        if (BeforeListing is not null) await BeforeListing(remoteFolderPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return ListAllRemoteItems(remoteFolderPath, searchType);
    }
    public override async Task<List<DmsResourceItem>> ListAllDirectoryItemsAsync(string remoteFolderPath, CancellationToken cancellationToken = default) =>
        (await ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.AllItems, cancellationToken))
            .Where(i => i.ItemType is DmsResourceItem.ItemTypes.Folder or DmsResourceItem.ItemTypes.Collection).ToList();
    public override async Task<List<DmsResourceItem>> ListAllFileItemsAsync(string remoteFolderPath, CancellationToken cancellationToken = default) =>
        (await ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.Files, cancellationToken))
            .Where(i => i.ItemType == DmsResourceItem.ItemTypes.File).ToList();
    public override void CreateDirectory(string remoteDirectoryPath)
    {
        Check("Create:" + remoteDirectoryPath);
        if (Items.Any(i => i.FullName == remoteDirectoryPath)) throw new IOException("The directory already exists.");
        AddDirectory(remoteDirectoryPath, ParentDirectoryPath(remoteDirectoryPath) == "/" && CollectionsSupported ? DmsResourceItem.ItemTypes.Collection : DmsResourceItem.ItemTypes.Folder);
    }
    public override void DeleteRemoteItem(DmsResourceItem remoteItem)
    {
        Check("Delete:" + remoteItem.FullName);
        if (remoteItem.ItemType == DmsResourceItem.ItemTypes.File)
            Items.RemoveAll(i => i.ExtendedInfosFileID == remoteItem.ExtendedInfosFileID);
        else Items.RemoveAll(i => i.FullName == remoteItem.FullName || i.FullName.StartsWith(remoteItem.FullName + "/", StringComparison.Ordinal));
    }
    public override void DeleteRemoteItem(string remotePath) =>
        DeleteRemoteItem(ListRemoteItem(remotePath) ?? throw new CompuMaster.Dms.Data.FileNotFoundException(remotePath));
    public override void UploadFile(string remoteFilePath, string localFilePath)
    {
        Check("Upload:" + remoteFilePath);
        Items.RemoveAll(i => i.FullName == remoteFilePath);
        var bytes = File.ReadAllBytes(localFilePath);
        var item = AddFile(remoteFilePath, ""); content[item.ExtendedInfosFileID] = bytes; item.ContentLength = bytes.Length;
    }
    public override void DownloadFile(string remoteFilePath, string localFilePath, DateTime? lastModificationDateOnLocalTime) =>
        DownloadFile(ListRemoteItem(remoteFilePath) ?? throw new CompuMaster.Dms.Data.FileNotFoundException(remoteFilePath), localFilePath);
    public override void DownloadFile(DmsResourceItem remoteFile, string localFilePath)
    {
        Check("Download:" + remoteFile.FullName); LastDownloadedID = remoteFile.ExtendedInfosFileID;
        File.WriteAllBytes(localFilePath, content[remoteFile.ExtendedInfosFileID]);
        if (remoteFile.LastModificationOnLocalTime.HasValue) File.SetLastWriteTime(localFilePath, remoteFile.LastModificationOnLocalTime.Value);
    }
    protected override void CopyFileItem(string source, string destination, bool? overwrite)
    {
        Check("Copy:" + source);
        var item = Items.Single(i => i.FullName == source);
        if (overwrite == true) Items.RemoveAll(i => i.FullName == destination);
        var copy = AddFile(destination, ""); content[copy.ExtendedInfosFileID] = content[item.ExtendedInfosFileID].ToArray(); copy.ContentLength = item.ContentLength;
    }
    protected override Task CopyFileItemAsync(string source, string destination, bool? overwrite)
    { CopyFileItem(source, destination, overwrite); return Task.CompletedTask; }
    protected override void CopyDirectoryItem(string source, string destination)
    {
        Check("CopyDirectory:" + source);
        if (!Items.Any(i => i.FullName == destination)) AddDirectory(destination);
        foreach (var item in Items.Where(i => i.FullName.StartsWith(source + "/", StringComparison.Ordinal)).ToArray())
        {
            var target = destination + item.FullName[source.Length..];
            if (item.ItemType == DmsResourceItem.ItemTypes.File) CopyFileItem(item.FullName, target, true);
            else if (!Items.Any(i => i.FullName == target)) AddDirectory(target, item.ItemType);
        }
    }
    protected override Task CopyDirectoryItemAsync(string source, string destination)
    { CopyDirectoryItem(source, destination); return Task.CompletedTask; }
    protected override void MoveFileItem(string source, string destination, bool? overwrite)
    { CopyFileItem(source, destination, overwrite); Items.RemoveAll(i => i.FullName == source); }
    protected override void MoveDirectoryItem(string source, string destination)
    { CopyDirectoryItem(source, destination); Items.RemoveAll(i => i.FullName == source || i.FullName.StartsWith(source + "/", StringComparison.Ordinal)); }
    public override DmsResourceItem? FindFileById(string id) => Items.FirstOrDefault(i => i.ExtendedInfosFileID == id);
    public override DmsResourceItem? FindFolderById(string id) => Items.FirstOrDefault(i => i.ExtendedInfosFolderID == id);
    public override DmsResourceItem? FindCollectionById(string id) => Items.FirstOrDefault(i => i.ExtendedInfosCollectionID == id);
    public override List<DmsUser> GetAllUsers() => new()
    {
        new DmsUser { ID = "me", DisplayName = "Current user" }, new DmsUser { ID = "owner", DisplayName = "Document owner" },
        new DmsUser { ID = "alice", DisplayName = "Alice" }, new DmsUser { ID = "bob", DisplayName = "Bob" }
    };
    public override List<DmsGroup> GetAllGroups() => new() { new DmsGroup { ID = "team", Name = "Project team" } };
    public override DmsLink CreateLink(DmsResourceItem resource, DmsLink share)
    {
        Check("CreateLink:" + resource.FullName);
        var result = (DmsLink)share.Clone(); result.ID = Guid.NewGuid().ToString("N");
        result.WebUrl = "https://example.invalid/" + result.ID; result.ParentDmsResourceItem = resource;
        return result;
    }
    public override void UpdateLink(DmsLink share) { Check("UpdateLink:" + share.ID); }
    public override void DeleteLink(DmsLink share) { Check("DeleteLink:" + share.ID); }
    public override void CreateSharing(DmsResourceItem resource, DmsShareForUser share) { Check("CreateUser:" + share.User.ID); }
    public override void CreateSharing(DmsResourceItem resource, DmsShareForGroup share) { Check("CreateGroup:" + share.Group.ID); }
    public override void UpdateSharing(DmsShareForUser share) { Check("UpdateUser:" + share.User.ID); }
    public override void UpdateSharing(DmsShareForGroup share) { Check("UpdateGroup:" + share.Group.ID); }
    public override void DeleteSharing(DmsShareForUser share) { Check("DeleteUser:" + share.User.ID); }
    public override void DeleteSharing(DmsShareForGroup share) { Check("DeleteGroup:" + share.Group.ID); }
    public DmsInstanceInfo CurrentDmsInstance => new(InstanceID, InstanceID == "demo" ? "Offline: Demonstration" : "Offline: Second instance", true);
    public IReadOnlyList<DmsInstanceInfo> ListAvailableDmsInstances() => new[] { new DmsInstanceInfo("demo", "Offline: Demonstration", InstanceID == "demo"), new DmsInstanceInfo("second", "Offline: Second instance", InstanceID == "second") };
    public void SelectDmsInstance(string instanceID)
    {
        Check("Instance:" + instanceID);
        if (instanceID is not ("demo" or "second")) throw new ArgumentOutOfRangeException(nameof(instanceID));
        InstanceID = instanceID;
    }
    public override List<DmsFileVersion> ListFileVersions(DmsResourceItem remoteFile)
    {
        Check("Versions:" + remoteFile.ExtendedInfosFileID);
        return new() { new DmsFileVersion(remoteFile, "version-1", DateTime.UtcNow.AddDays(-1), 18, "alice", "Alice", "Initial version") };
    }
    public override void DownloadFileVersion(DmsFileVersion version, Stream destination)
    { Check("DownloadVersion:" + version.VersionID); destination.Write(Encoding.UTF8.GetBytes("Historical content")); }
    public override void RestoreFileVersion(DmsFileVersion version)
    {
        Check("RestoreVersion:" + version.VersionID);
        var item = Items.Single(i => i.ExtendedInfosFileID == version.FileID);
        content[item.ExtendedInfosFileID] = Encoding.UTF8.GetBytes("Historical content"); item.ContentLength = 18;
    }
    public override void RemoveFileVersion(DmsFileVersion version) { Check("RemoveVersion:" + version.VersionID); }
}
