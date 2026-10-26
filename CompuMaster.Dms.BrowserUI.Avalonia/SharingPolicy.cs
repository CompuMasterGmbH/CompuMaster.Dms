using CompuMaster.Dms.Data;
using CompuMaster.Dms.Providers;

namespace CompuMaster.Dms.BrowserUI.Avalonia;

// Mirrors the WinForms adapters without adding provider-specific switches to the public API.
internal sealed class SharingPolicy
{
    internal SharingPolicy(BaseDmsProvider provider)
    {
        if (!provider.SupportsSharingSetup) throw new NotSupportedException("The provider does not support sharing.");
        WebDav = provider.DmsProviderID == BaseDmsProvider.DmsProviders.WebDAV;
        Teamwork = provider.DmsProviderID is BaseDmsProvider.DmsProviders.Scopevisio or BaseDmsProvider.DmsProviders.CenterDevice;
        if (!WebDav && !Teamwork) throw new NotSupportedException("Sharing controls are not available for this provider.");
    }
    internal bool WebDav { get; }
    internal bool Teamwork { get; }
    internal bool EditablePermission(string permission, bool link, bool creating, DmsResourceItem item, DmsLink? existing)
    {
        if (WebDav) return permission is "Edit" or "Delete" or "Share" ||
            permission == "Upload" && (!link || item.ItemType == DmsResourceItem.ItemTypes.Folder);
        if (!link) return false;
        return permission switch
        {
            "View" => creating,
            "Download" => creating || existing?.AllowUpload != true,
            "Upload" => creating && item.ItemType == DmsResourceItem.ItemTypes.Collection,
            _ => false
        };
    }
    internal void Validate(DmsShareBase share)
    {
        if (share is DmsLink link && Teamwork)
        {
            if (link.AllowDownload && !link.AllowView) throw new DmsUserInputInvalidException(UiText.Get("ViewRequiredForDownload"));
            if (link.AllowView == link.AllowUpload) throw new DmsUserInputInvalidException(UiText.Get("ViewOrUploadRequired"));
            if (link.AllowUpload && string.IsNullOrWhiteSpace(link.Name)) throw new DmsUserInputMissingException(UiText.Get("UploadLinkNameRequired"));
        }
        if (WebDav && share.AllowView != share.AllowDownload)
            throw new DmsUserInputInvalidException(UiText.Extra("View and Download must be selected together.", "Anzeigen und Herunterladen müssen gemeinsam ausgewählt sein."));
        if (!share.AllowedActions().Any()) throw new DmsUserInputMissingException(UiText.Extra("At least one permission is required.", "Mindestens eine Berechtigung ist erforderlich."));
        if (share is DmsLink limits && (limits.MaxBytes < 0 || limits.MaxDownloads < 0 || limits.MaxUploads < 0))
            throw new DmsUserInputInvalidException(UiText.Extra("Limits cannot be negative.", "Grenzwerte dürfen nicht negativ sein."));
    }
    internal static async Task SaveAsync(BaseDmsProvider provider, DmsResourceItem item, DmsShareBase share, bool creating)
    {
        new SharingPolicy(provider).Validate(share);
        share.ParentDmsResourceItem = item;
        switch (share)
        {
            case DmsLink link:
                DmsLink result = link;
                if (creating) result = await provider.CreateLinkAsync(item, link);
                else await provider.UpdateLinkAsync(link);
                if (result is null || string.IsNullOrWhiteSpace(result.ID)) throw new InvalidOperationException("The provider returned a link without an identifier.");
                result.ParentDmsResourceItem = item;
                item.ExtendedInfosLinks ??= new();
                item.ExtendedInfosLinks.RemoveAll(l => l.ID == result.ID);
                item.ExtendedInfosLinks.Add(result);
                break;
            case DmsShareForUser user:
                if (creating) await provider.CreateSharingAsync(item, user); else await provider.UpdateSharingAsync(user);
                item.ExtendedInfosUserSharings ??= new();
                item.ExtendedInfosUserSharings.RemoveAll(s => s.User.ID == user.User.ID);
                item.ExtendedInfosUserSharings.Add(user);
                item.ExtendedInfosHasUserSharings = item.ExtendedInfosUserSharings.Count > 0;
                break;
            case DmsShareForGroup group:
                if (creating) await provider.CreateSharingAsync(item, group); else await provider.UpdateSharingAsync(group);
                item.ExtendedInfosGroupSharings ??= new();
                item.ExtendedInfosGroupSharings.RemoveAll(s => s.Group.ID == group.Group.ID);
                item.ExtendedInfosGroupSharings.Add(group);
                item.ExtendedInfosHasGroupSharings = item.ExtendedInfosGroupSharings.Count > 0;
                break;
        }
        SynchronizeSharedFlag(item);
    }
    internal static async Task DeleteAsync(BaseDmsProvider provider, DmsResourceItem item, DmsShareBase share)
    {
        switch (share)
        {
            case DmsLink link:
                await provider.DeleteLinkAsync(link);
                item.ExtendedInfosLinks?.RemoveAll(l => l.ID == link.ID);
                break;
            case DmsShareForUser user:
                await provider.DeleteSharingAsync(user);
                item.ExtendedInfosUserSharings?.RemoveAll(s => s.User.ID == user.User.ID);
                item.ExtendedInfosHasUserSharings = item.ExtendedInfosUserSharings?.Count > 0;
                break;
            case DmsShareForGroup group:
                await provider.DeleteSharingAsync(group);
                item.ExtendedInfosGroupSharings?.RemoveAll(s => s.Group.ID == group.Group.ID);
                item.ExtendedInfosHasGroupSharings = item.ExtendedInfosGroupSharings?.Count > 0;
                break;
        }
        SynchronizeSharedFlag(item);
    }
    private static void SynchronizeSharedFlag(DmsResourceItem item) =>
        item.ExtendedInfosIsShared = item.ExtendedInfosHasLinks || item.ExtendedInfosHasGroupSharings || item.ExtendedInfosHasUserSharings ||
            item.ExtendedInfosHasHiddenUserSharings || item.ExtendedInfosHasHiddenGroupSharings || item.ExtendedInfosIsPublicCollection;
}
