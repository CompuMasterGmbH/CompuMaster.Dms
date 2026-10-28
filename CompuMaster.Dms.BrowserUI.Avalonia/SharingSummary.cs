using CompuMaster.Dms.Data;
#if DMS_WINUI
namespace CompuMaster.Dms.BrowserUI.WinUI;
#else
namespace CompuMaster.Dms.BrowserUI.Avalonia;
#endif

internal static class SharingSummary
{
    private static string State(bool? value) => value.HasValue ? UiText.Get(value.Value ? "SharingYes" : "SharingNo") : UiText.Get("SharingUnknown");
    internal static string Format(DmsResourceItem item) => FormatCore(item, false);
    internal static string FormatDialog(DmsResourceItem item) => FormatCore(item, true);
    private static string FormatCore(DmsResourceItem item, bool dialog)
    {
        var lines = new List<string>();
        if (!dialog || item.ExtendedInfosIsReceivedShare != false)
            lines.Add(UiText.Format("ReceivedSharingStatus", State(item.ExtendedInfosIsReceivedShare)));
        if (item.ExtendedInfosIsReceivedShare == true)
        {
            lines.Insert(0, UiText.Get("ReceivedSharingTitle"));
            foreach (var grant in item.ExtendedInfosReceivedSharings ?? new())
            {
                lines.Add(UiText.Format("SharingBy", grant.SharedBy.DisplayName ?? UiText.Get("UnknownValue")));
                lines.Add(UiText.Format("SharingResourceOwner", grant.ResourceOwner.DisplayName ?? UiText.Get("UnknownValue")));
                var kind = UiText.Get(grant.RecipientKind == DmsReceivedShareKind.User ? "EntityUser" : grant.RecipientKind == DmsReceivedShareKind.Group ? "EntityGroup" : "UnknownValue");
                var recipient = string.IsNullOrWhiteSpace(grant.RecipientDisplayName) ? grant.RecipientID : grant.RecipientDisplayName;
                lines.Add(UiText.Format("SharingRecipient", string.IsNullOrWhiteSpace(recipient) ? UiText.Get("UnknownValue") : recipient, kind));
                lines.Add(grant.IsInherited == true ? UiText.Format("SharingInherited", grant.SharedPath ?? UiText.Get("UnknownValue")) : UiText.Get(grant.IsInherited == false ? "SharingDirect" : "SharingInheritanceUnknown"));
                var permissions = grant.PermissionsAvailable ? string.Join(", ", grant.AllowedActions().Select(action => UiText.Get("Permission" + action))) : UiText.Get("SharingUnknown");
                lines.Add(UiText.Format("SharingGrantPermissions", permissions.Length == 0 ? UiText.Get("SharingNoPermissions") : permissions));
                lines.Add("");
            }
        }
        if (!dialog || !item.ExtendedInfosHasOutgoingSharings.HasValue)
        {
            lines.Add(UiText.Format("OutgoingSharingStatus", State(item.ExtendedInfosHasOutgoingSharings)));
            lines.Add(UiText.Get("OutgoingSharingExplanation"));
        }
        return string.Join(Environment.NewLine, lines).TrimEnd();
    }
}
