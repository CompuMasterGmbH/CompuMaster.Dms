using CompuMaster.Dms.Data;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

/// <summary>Identifies the visible browser content.</summary>
public enum BrowseModes : byte
{
    /// <summary>Shows folders and collections.</summary>
    Folders = 0,
    /// <summary>Shows folders, collections and files.</summary>
    FoldersAndFiles = 1
}

/// <summary>Defines the operations permitted by the consuming application.</summary>
[Flags]
public enum FileOrFolderActions
{
    /// <summary>Permits selection without modifications or transfers.</summary>
    AllowSelectOnly = 0,
    /// <summary>Permits changing between folder and file browsing.</summary>
    AllowSwitchBrowseMode = 1,
    /// <summary>Permits creating directories.</summary>
    AllowCreateFolders = 2,
    /// <summary>Permits uploading files.</summary>
    AllowUploadFiles = 4,
    /// <summary>Permits downloading and opening files.</summary>
    AllowDownloadFiles = 8,
    /// <summary>Permits deleting files and directories.</summary>
    AllowDeleteFiles = 16,
    /// <summary>Permits copying, renaming and moving resources.</summary>
    AllowCopyRenameMoveFiles = 32,
    /// <summary>Permits managing supported sharing operations.</summary>
    AllowSharings = 64,
    /// <summary>Permits switching instances when a selection method is supplied.</summary>
    AllowSwitchDmsInstance = 128,
    /// <summary>Permits viewing supported version history without granting download or mutation rights.</summary>
    AllowViewFileVersionHistory = 256,
    /// <summary>Permits restoring historical versions independently of upload permission.</summary>
    AllowRestoreFileVersions = 512,
    /// <summary>Permits removing historical versions independently of ordinary file deletion.</summary>
    AllowRemoveFileVersions = 1024
}

/// <summary>Defines whether the browser returns a selection.</summary>
public enum DialogOperationModes : byte
{
    /// <summary>Shows confirmation and cancellation buttons.</summary>
    ReturnSelectedItems = 1,
    /// <summary>Shows a close button for browsing without a result.</summary>
    NoResults = 2
}

/// <summary>Configures provider-independent browser workflows.</summary>
public sealed class DmsBrowserOptions
{
    /// <summary>Gets or sets the title of the browser dialog.</summary>
    public string? FormTitle { get; set; }
    /// <summary>Gets or sets the remote directory treated as the browser root.</summary>
    public string? InitialFolder { get; set; }
    /// <summary>Gets or sets the initial folder relative to the browser root.</summary>
    public string? SelectedFolder { get; set; }
    /// <summary>Gets or sets the initial browser mode.</summary>
    public BrowseModes BrowseMode { get; set; } = BrowseModes.FoldersAndFiles;
    /// <summary>Gets or sets the operations permitted by the host.</summary>
    public FileOrFolderActions AllowedActions { get; set; } = FileOrFolderActions.AllowSelectOnly;
    /// <summary>Gets or sets the dialog's result behavior.</summary>
    public DialogOperationModes DialogOperationMode { get; set; } = DialogOperationModes.ReturnSelectedItems;
    /// <summary>Gets or sets the required ancestor directory for uploads and downloads.</summary>
    /// <remarks>Temporary previews are managed separately, as in the WinForms browser. Symbolic links cannot bypass this restriction.</remarks>
    public string? LocalParentMustFolder { get; set; }
    /// <summary>Gets or sets the initially displayed local download directory.</summary>
    public string? LocalDefaultFolderDownloads { get; set; }
    /// <summary>Gets or sets the initially displayed local upload directory.</summary>
    public string? LocalDefaultFolderUploads { get; set; }
    /// <summary>Gets or sets the startup instance and optional later selection method.</summary>
    /// <remarks>The default preserves the provider's existing instance.</remarks>
    public RemoteInstanceSelector RemoteInstance { get; set; } = new();
}

/// <summary>Contains the resources confirmed by a browser dialog.</summary>
public sealed class DmsBrowserSelection
{
    internal DmsBrowserSelection(string? folder, DmsResourceItem? item, IReadOnlyList<DmsResourceItem> files)
    { SelectedFolder = folder; Folder = item; Files = files; }
    /// <summary>Gets the selected folder relative to the browser root, or null for the root.</summary>
    public string? SelectedFolder { get; }
    /// <summary>Gets the selected folder resource, or null for a synthetic provider root.</summary>
    public DmsResourceItem? Folder { get; }
    /// <summary>Gets the selected files.</summary>
    public IReadOnlyList<DmsResourceItem> Files { get; }
}
