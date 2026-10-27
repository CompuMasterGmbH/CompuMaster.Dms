using System.Text;
using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

#if DMS_WINUI
namespace CompuMaster.Dms.BrowserUI.WinUI;
#else
namespace CompuMaster.Dms.BrowserUI.Avalonia;
#endif

internal static class ItemProperties
{
    internal static async Task<string> FormatAsync(BaseDmsProvider provider, DmsResourceItem item)
    {
        foreach (var link in item.ExtendedInfosLinks ?? new()) await link.RefreshAsync();
        var collections = new Dictionary<string, string>();
        var folders = new Dictionary<string, string>();
        foreach (var id in item.ExtendedInfosReferencedFromCollectionIDs ?? new())
        {
            try { collections[id] = (await provider.FindCollectionByIdAsync(id))?.Name ?? id; }
            catch (Exception) { collections[id] = id; } // Optional display-name lookup must not hide the remaining properties.
        }
        foreach (var id in item.ExtendedInfosReferencedFromFolderIDs ?? new())
        {
            try { folders[id] = (await provider.FindFolderByIdAsync(id))?.Name ?? id; }
            catch (Exception) { folders[id] = id; }
        }
        // User/group display names may be lazily resolved by synchronous ecosystem callbacks.
        return await Task.Run(() =>
        {
            var text = new StringBuilder();
            void Add(string label, object? value) => text.AppendLine(label + value);
            Add(UiText.Get("PropertiesFullPath"), item.FullName);
            Add(UiText.Get("PropertiesParentFolder"), item.Folder);
            Add(UiText.Get("PropertiesParentCollection"), item.Collection);
            if (item.ItemType == DmsResourceItem.ItemTypes.File) Add(UiText.Get("PropertiesFileSize"), item.ContentLength.ToString("N0") + " Bytes");
            text.AppendLine().AppendLine(UiText.Get("PropertiesDetails"));
            Add(UiText.Get("PropertiesOwner"), item.ExtendedInfosOwner);
            Add(UiText.Get("PropertiesVersionNumber"), item.ExtendedInfosVersion);
            Add(UiText.Get("PropertiesVersionDate"), item.ExtendedInfosVersionDateLocalTime);
            Add(UiText.Get("PropertiesArchivedDate"), item.ExtendedInfosArchivedDateLocalTime);
            Add(UiText.Get("PropertiesLockedBy"), item.ExtendedInfosLockedByUser);
            foreach (var value in item.ExtendedInfosLocks ?? new()) Add(UiText.Get("PropertiesLocks"), value);
            if (item.ExtendedInfosCollisionDetected) text.AppendLine(UiText.Get("PropertiesCollisionWarning"));
            text.AppendLine().AppendLine(UiText.Get("PropertiesSharings")).AppendLine(SharingSummary.Format(item)).AppendLine();
            Add("IsShared: ", item.ExtendedInfosIsShared);
            Add("IsPublicCollection: ", item.ExtendedInfosIsPublicCollection);
            Add("HasHiddenGroupSharings: ", item.ExtendedInfosHasHiddenGroupSharings);
            Add("HasGroupSharings: ", item.ExtendedInfosHasGroupSharings);
            foreach (var share in item.ExtendedInfosGroupSharings ?? new()) Add("  ", share);
            Add("HasHiddenUserSharings: ", item.ExtendedInfosHasHiddenUserSharings);
            Add("HasUserSharings: ", item.ExtendedInfosHasUserSharings);
            foreach (var share in item.ExtendedInfosUserSharings ?? new()) Add("  ", share);
            foreach (var link in item.ExtendedInfosLinks ?? new())
            {
                Add("Link: ", link.ID); Add("  Name: ", link.Name);
                Add("  WebUrl: ", link.WebUrl); Add("  DownloadUrl: ", link.DownloadUrl);
                Add("  Password: ", link.Password); Add("  ExpiresOn: ", link.ExpiryDateLocalTime);
                Add("  MaxDownloads: ", link.MaxDownloads); Add("  MaxUploads: ", link.MaxUploads);
                Add("  MaxBytes: ", link.MaxBytes); Add("  UploadsCount: ", link.UploadsCount);
                Add("  DownloadsCount: ", link.DownloadsCount); Add("  ViewsCount: ", link.ViewsCount);
                Add("  UploadedBytes: ", link.UploadedBytes);
                Add("  AllowView: ", link.AllowView); Add("  AllowDownload: ", link.AllowDownload);
                Add("  AllowUpload: ", link.AllowUpload); Add("  AllowEdit: ", link.AllowEdit);
                Add("  AllowDelete: ", link.AllowDelete); Add("  AllowShare: ", link.AllowShare);
            }
            text.AppendLine().AppendLine(UiText.Get("PropertiesExtendedInformation"));
            Add(UiText.Get("PropertiesLastModification"), item.LastModificationOnLocalTime);
            Add(UiText.Extra("Created: ", "Erstellt: "), item.CreatedOnLocalTime);
            Add("LastModificationUser: ", item.ExtendedInfosLastModificationUser);
            Add("HasChildDirectories: ", BrowserNode.HasChildren(item));
            Add("ChildDirectoryCount: ", item.ChildDirectoryCount);
            Add("IsHidden: ", item.IsHidden);
            Add("IsIntelligent: ", item.ExtendedInfosIsIntelligent);
            Add("IsAuditing: ", item.ExtendedInfosIsAuditing);
            Add("Hash/ETag: ", item.ProviderSpecificHashOrETag);
            Add("File ID: ", item.ExtendedInfosFileID); Add("Folder ID: ", item.ExtendedInfosFolderID);
            Add("Collection ID: ", item.ExtendedInfosCollectionID);
            Add("Assigned Collection ID: ", item.ExtendedInfosAssignedCollectionID);
            Add("Assigned Folder ID: ", item.ExtendedInfosAssignedFolderID);
            if (collections.Count > 0) { text.AppendLine(UiText.Get("PropertiesReferencedCollectionIds")); foreach (var entry in collections) Add("  ", entry.Value + " (" + entry.Key + ")"); }
            if (folders.Count > 0) { text.AppendLine(UiText.Get("PropertiesReferencedFolderIds")); foreach (var entry in folders) Add("  ", entry.Value + " (" + entry.Key + ")"); }
            return text.ToString();
        });
    }
}
