Option Explicit On
Option Strict On
Imports CompuMaster.Dms.Data

Friend NotInheritable Class SharingSummary
    Private Shared Function State(value As Boolean?) As String
        Return If(value.HasValue, UiStrings.GetText(If(value.GetValueOrDefault(), "SharingYes", "SharingNo")), UiStrings.GetText("SharingUnknown"))
    End Function
    Friend Shared Sub SynchronizeOutgoing(item As DmsResourceItem)
        item.ExtendedInfosHasOutgoingSharings = item.ExtendedInfosHasLinks OrElse item.ExtendedInfosHasGroupSharings OrElse item.ExtendedInfosHasUserSharings OrElse
            item.ExtendedInfosHasHiddenUserSharings OrElse item.ExtendedInfosHasHiddenGroupSharings OrElse item.ExtendedInfosIsPublicCollection
        item.ExtendedInfosIsShared = item.ExtendedInfosHasOutgoingSharings.GetValueOrDefault() OrElse item.ExtendedInfosIsReceivedShare.GetValueOrDefault() OrElse
            (Not item.ExtendedInfosIsReceivedShare.HasValue AndAlso item.ExtendedInfosIsShared)
    End Sub
    Friend Shared Function Format(item As DmsResourceItem) As String
        Dim lines As New List(Of String) From {UiStrings.Format("ReceivedSharingStatus", State(item.ExtendedInfosIsReceivedShare))}
        If item.ExtendedInfosIsReceivedShare.GetValueOrDefault() Then
            lines.Insert(0, UiStrings.GetText("ReceivedSharingTitle"))
            For Each grant In If(item.ExtendedInfosReceivedSharings, New List(Of DmsReceivedShare))
                lines.Add(UiStrings.Format("SharingBy", If(grant.SharedBy.DisplayName, UiStrings.GetText("UnknownValue"))))
                lines.Add(UiStrings.Format("SharingResourceOwner", If(grant.ResourceOwner.DisplayName, UiStrings.GetText("UnknownValue"))))
                Dim kind = UiStrings.GetText(If(grant.RecipientKind = DmsReceivedShareKind.User, "EntityUser", If(grant.RecipientKind = DmsReceivedShareKind.Group, "EntityGroup", "UnknownValue")))
                Dim recipient = If(String.IsNullOrWhiteSpace(grant.RecipientDisplayName), grant.RecipientID, grant.RecipientDisplayName)
                lines.Add(UiStrings.Format("SharingRecipient", If(String.IsNullOrWhiteSpace(recipient), UiStrings.GetText("UnknownValue"), recipient), kind))
                If grant.IsInherited.GetValueOrDefault() Then
                    lines.Add(UiStrings.Format("SharingInherited", If(grant.SharedPath, UiStrings.GetText("UnknownValue"))))
                Else
                    lines.Add(UiStrings.GetText(If(grant.IsInherited.HasValue, "SharingDirect", "SharingInheritanceUnknown")))
                End If
                Dim permissions = If(grant.PermissionsAvailable, DmsItemSharings.LocalizedAllowedActions(grant), UiStrings.GetText("SharingUnknown"))
                lines.Add(UiStrings.Format("SharingGrantPermissions", If(permissions.Length = 0, UiStrings.GetText("SharingNoPermissions"), permissions)))
                lines.Add("")
            Next
        End If
        lines.Add(UiStrings.Format("OutgoingSharingStatus", State(item.ExtendedInfosHasOutgoingSharings)))
        lines.Add(UiStrings.GetText("OutgoingSharingExplanation"))
        Return String.Join(Environment.NewLine, lines)
    End Function
End Class
