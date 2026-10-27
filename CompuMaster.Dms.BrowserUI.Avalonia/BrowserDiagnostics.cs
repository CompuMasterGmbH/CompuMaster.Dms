using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

#if DMS_WINUI
namespace CompuMaster.Dms.BrowserUI.WinUI;
#else
namespace CompuMaster.Dms.BrowserUI.Avalonia;
#endif

/// <summary>Identifies the browser boundary where a failure occurred.</summary>
public enum DmsBrowserFailurePhase
{
    /// <summary>Initializes the browser view.</summary>
    Startup,
    /// <summary>Authorizes the provider.</summary>
    Authentication,
    /// <summary>Selects a DMS instance.</summary>
    InstanceSelection,
    /// <summary>Looks up the configured remote root.</summary>
    ScopedRootLookup,
    /// <summary>Retrieves selected resource details.</summary>
    MetadataEnrichment,
    /// <summary>Lists child directories.</summary>
    ChildListing,
    /// <summary>Selects the initial directory.</summary>
    InitialSelection,
    /// <summary>Refreshes displayed resources.</summary>
    Refresh,
    /// <summary>Performs a user-requested action.</summary>
    Action
}
/// <summary>Provides an original exception to explicitly attached host diagnostics.</summary>
/// <remarks>Exceptions may contain sensitive provider data. Subscribers must redact them before persistence or transmission.</remarks>
public sealed class DmsBrowserFailureEventArgs : EventArgs
{
    internal DmsBrowserFailureEventArgs(Exception exception, DmsBrowserFailurePhase phase, string operation)
    { Exception = exception; Phase = phase; Operation = operation; }
    /// <summary>Gets the original exception with its stack, data and inner exceptions.</summary>
    public Exception Exception { get; }
    /// <summary>Gets the browser phase observed at the failure boundary.</summary>
    public DmsBrowserFailurePhase Phase { get; }
    /// <summary>Gets the operation name without resource paths, request URLs or credentials.</summary>
    public string Operation { get; }
    /// <summary>Gets or sets whether the standard error dialog is suppressed.</summary>
    public bool SuppressDialog { get; set; }
}
public sealed partial class DmsBrowserControl
{
    /// <summary>Reports failures consumed by UI handlers before presenting the standard error dialog.</summary>
    /// <remarks>Expected cancellation is excluded. Subscriber failures do not replace the original exception.
    /// No exception is automatically persisted. Task-returning APIs retain exception propagation.</remarks>
    public event EventHandler<DmsBrowserFailureEventArgs>? OperationFailed;
    internal DmsBrowserFailureEventArgs ReportFailure(Exception ex, DmsBrowserFailurePhase phase, string operation)
    {
        var failure = new DmsBrowserFailureEventArgs(ex, phase, operation);
        foreach (var handler in OperationFailed?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((EventHandler<DmsBrowserFailureEventArgs>)handler)(this, failure); }
            catch { /* Diagnostics must not prevent cleanup or replace the failure. */ }
        }
        return failure;
    }
    internal async Task ReportFailureAsync(Exception ex, DmsBrowserFailurePhase phase,
        [System.Runtime.CompilerServices.CallerMemberName] string operation = "")
    {
        var failure = ReportFailure(ex, phase, operation);
        if (!failure.SuppressDialog) await Dialogs.MessageAsync(Owner, UiText.FailureMessage(ex));
    }
}
internal static class ResourceDetails
{
    internal static async Task<DmsResourceItem> LoadAsync(BaseDmsProvider provider, DmsResourceItem entry)
    {
        DmsResourceItem? details;
        // WebDAV identifiers describe identity; they do not imply an ID-search API.
        var webdavFolder = provider.DmsProviderID == BaseDmsProvider.DmsProviders.WebDAV && entry.ItemType == DmsResourceItem.ItemTypes.Folder;
        var webdavFile = provider.DmsProviderID == BaseDmsProvider.DmsProviders.WebDAV && entry.ItemType == DmsResourceItem.ItemTypes.File;
        if (entry.ItemType == DmsResourceItem.ItemTypes.Collection && !string.IsNullOrEmpty(entry.ExtendedInfosCollectionID))
            details = await provider.FindCollectionByIdAsync(entry.ExtendedInfosCollectionID);
        else if (entry.ItemType == DmsResourceItem.ItemTypes.Folder && !string.IsNullOrEmpty(entry.ExtendedInfosFolderID) && !webdavFolder)
            details = await provider.FindFolderByIdAsync(entry.ExtendedInfosFolderID);
        else if (entry.ItemType == DmsResourceItem.ItemTypes.File && !string.IsNullOrEmpty(entry.ExtendedInfosFileID) && !webdavFile)
            details = await provider.FindFileByIdAsync(entry.ExtendedInfosFileID);
        else details = await provider.ListRemoteItemAsync(entry.FullName);
        if (details is null)
        {
            if (entry.ItemType == DmsResourceItem.ItemTypes.File) throw new Data.FileNotFoundException(entry.FullName);
            throw new Data.DirectoryNotFoundException(entry.FullName);
        }
        if (webdavFolder && !string.IsNullOrEmpty(entry.ExtendedInfosFolderID) &&
            (details.ItemType != entry.ItemType || details.ExtendedInfosFolderID != entry.ExtendedInfosFolderID))
            throw new InvalidOperationException(UiText.Get("VersionActionUnavailable"));
        if (webdavFile && !string.IsNullOrEmpty(entry.ExtendedInfosFileID) &&
            (details.ItemType != entry.ItemType || details.ExtendedInfosFileID != entry.ExtendedInfosFileID))
            throw new InvalidOperationException("The selected file identity changed or is no longer available.");
        details.FullName = entry.FullName; details.Folder = entry.Folder; details.Collection = entry.Collection;
        details.ExtendedInfosAssignedCollectionID = entry.ExtendedInfosAssignedCollectionID;
        details.ExtendedInfosAssignedFolderID = entry.ExtendedInfosAssignedFolderID;
        details.ExtendedInfosCollisionDetected = entry.ExtendedInfosCollisionDetected;
        return details;
    }
}
