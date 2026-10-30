using CompuMaster.Dms.Data;

#if DMS_WINUI
namespace CompuMaster.Dms.BrowserUI.WinUI;
#else
namespace CompuMaster.Dms.BrowserUI.Avalonia;
#endif

/// <summary>Selects the appearance of a browser and its owned dialogs.</summary>
public enum BrowserTheme
{
    /// <summary>Inherits the host appearance or follows the operating system for standalone windows.</summary>
    System,
    /// <summary>Uses a light appearance.</summary>
    Light,
    /// <summary>Uses a dark appearance.</summary>
    Dark
}

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

/// <summary>Identifies actions displayed in the toolbar in addition to their context menus.</summary>
/// <remarks>Visibility never grants permission to execute an action.</remarks>
[Flags]
public enum BrowserToolbarActions
{
    /// <summary>Hides every toolbar action.</summary>
    None = 0,
    /// <summary>Shows the action for opening files.</summary>
    Open = 1,
    /// <summary>Shows the action for downloading files.</summary>
    Download = 2,
    /// <summary>Shows the action for downloading folders.</summary>
    DownloadFolder = 4,
    /// <summary>Shows the action for uploading files.</summary>
    Upload = 8,
    /// <summary>Shows the action for uploading folders.</summary>
    UploadFolder = 16,
    /// <summary>Shows the action for refreshing the listing.</summary>
    Refresh = 32,
    /// <summary>Shows the action for copying files.</summary>
    Copy = 64,
    /// <summary>Shows the action for moving files.</summary>
    Move = 128,
    /// <summary>Shows the action for renaming files.</summary>
    Rename = 256,
    /// <summary>Shows the action for deleting files.</summary>
    Delete = 512,
    /// <summary>Shows the action for managing file sharing.</summary>
    FileSharings = 1024,
    /// <summary>Shows the action for managing folder sharing.</summary>
    FolderSharings = 2048,
    /// <summary>Shows the action for viewing file properties.</summary>
    FileProperties = 4096,
    /// <summary>Shows the action for viewing folder properties.</summary>
    FolderProperties = 8192,
    /// <summary>Shows the action for viewing version history.</summary>
    VersionHistory = 16384,
    /// <summary>Shows the standard open, transfer and refresh actions.</summary>
    Default = Open | Download | DownloadFolder | Upload | UploadFolder | Refresh,
    /// <summary>Shows every action permitted by the host and provider.</summary>
    All = 32767
}

/// <summary>Selects the action performed when files are double-clicked.</summary>
public enum BrowserFileDoubleClickAction
{
    /// <summary>Opens selected files as temporary read-only previews when downloading is permitted.</summary>
    Open,
    /// <summary>Prompts for a download destination when downloading is permitted.</summary>
    Download,
    /// <summary>Performs no action.</summary>
    None
}

/// <summary>Configures provider-independent browser workflows.</summary>
public sealed class DmsBrowserOptions
{
    /// <summary>Gets or sets whether permitted resources can be moved by drag and drop inside this browser, defaulting to true.</summary>
    public bool EnableInternalDragAndDrop { get; set; } = true;
    /// <summary>Gets or sets the actions shown in the toolbar, subject to existing permissions and provider capabilities.</summary>
    /// <remarks>Other permitted actions remain available through context menus. Configure before construction.</remarks>
    public BrowserToolbarActions ToolbarActions { get; set; } = BrowserToolbarActions.Default;
    /// <summary>Gets or sets whether the permitted browse-mode switch is displayed, defaulting to true.</summary>
    public bool ShowFilesButtonVisible { get; set; } = true;
    /// <summary>Gets or sets whether the permitted create-folder button is displayed, defaulting to true.</summary>
    /// <remarks>The context-menu action remains available.</remarks>
    public bool CreateFolderButtonVisible { get; set; } = true;
    /// <summary>Gets or sets the file double-click action, defaulting to Open in both dialog modes.</summary>
    /// <remarks>This setting does not grant download permission or change explicit selection confirmation.</remarks>
    public BrowserFileDoubleClickAction FileDoubleClickAction { get; set; } = BrowserFileDoubleClickAction.Open;

    internal bool ShowsToolbarAction(string key) => (ToolbarActions & ToolbarAction(key)) != 0;
    private static BrowserToolbarActions ToolbarAction(string key) => key switch
    {
        "ActionOpen" => BrowserToolbarActions.Open,
        "ActionDownload" => BrowserToolbarActions.Download,
        "ActionDownloadFolder" => BrowserToolbarActions.DownloadFolder,
        "ActionUpload" => BrowserToolbarActions.Upload,
        "ActionUploadFolder" => BrowserToolbarActions.UploadFolder,
        "ActionRefreshFiles" => BrowserToolbarActions.Refresh,
        "ActionCopy" => BrowserToolbarActions.Copy,
        "ActionMove" => BrowserToolbarActions.Move,
        "ActionRename" => BrowserToolbarActions.Rename,
        "ActionDelete" => BrowserToolbarActions.Delete,
        "ActionFileSharings" => BrowserToolbarActions.FileSharings,
        "ActionFolderSharings" => BrowserToolbarActions.FolderSharings,
        "FileProperties" => BrowserToolbarActions.FileProperties,
        "FolderProperties" => BrowserToolbarActions.FolderProperties,
        "FileVersionHistory" => BrowserToolbarActions.VersionHistory,
        _ => BrowserToolbarActions.None
    };

    /// <summary>Gets or sets the initial appearance, defaulting to the host or system preference.</summary>
    /// <remarks>Use the browser control's Appearance property to change appearance after construction.</remarks>
    public BrowserTheme Theme { get; set; } = BrowserTheme.System;
    /// <summary>Gets or sets the optional palette used when the effective appearance is light.</summary>
    public BrowserPalette? LightPalette { get; set; }
    /// <summary>Gets or sets the optional palette used when the effective appearance is dark.</summary>
    public BrowserPalette? DarkPalette { get; set; }
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
    /// <remarks>Temporary previews are managed separately. External drag downloads are disabled when this restriction is set because their final destination cannot be verified. Symbolic links cannot bypass this restriction.</remarks>
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
