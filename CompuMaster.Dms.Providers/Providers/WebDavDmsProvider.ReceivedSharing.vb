Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Ocs.Core
Imports CompuMaster.Ocs.Types
Imports System.Threading

Namespace Providers
    Partial Public Class WebDavDmsProvider
        Private Sub TryLoadReceivedSharingMetadata(item As DmsResourceItem)
            If Me.OcsSharingClient Is Nothing Then Return
            Try
                'Query the authenticated account once for a selected resource, never once per listing entry.
                Dim client = Me.OcsSharingClient
                Dim dav = Me.WebDavClient
                Dim root = Me.OcsRootPath
                Dim account = Me._AuthorizedUser
                Dim admitted As Func(Of Boolean) = Function() Object.ReferenceEquals(client, Me.OcsSharingClient) AndAlso Object.ReferenceEquals(dav, Me.WebDavClient) AndAlso root = Me.OcsRootPath AndAlso account = Me._AuthorizedUser
                Dim grants = client.GetReceivedShares()
                If Not admitted() Then Throw New InvalidOperationException("The authenticated sharing context changed.")
                Dim snapshot As New DmsResourceItem With {.FullName = item.FullName, .ItemType = item.ItemType,
                    .ExtendedInfosFileID = item.ExtendedInfosFileID, .ExtendedInfosFolderID = item.ExtendedInfosFolderID,
                    .ExtendedInfosOwner = item.ExtendedInfosOwner, .ExtendedInfosIsShared = item.ExtendedInfosIsShared,
                    .ExtendedInfosHasOutgoingSharings = item.ExtendedInfosHasOutgoingSharings}
                ApplyReceivedSharingMetadata(snapshot, grants, Function(path, id) admitted() AndAlso VerifyReceivedAncestor(path, id) AndAlso admitted())
                If Not admitted() Then Throw New InvalidOperationException("The authenticated sharing context changed.")
                item.ExtendedInfosIsReceivedShare = snapshot.ExtendedInfosIsReceivedShare
                item.ExtendedInfosReceivedSharings = snapshot.ExtendedInfosReceivedSharings
                For Each grant In item.ExtendedInfosReceivedSharings
                    grant.ParentDmsResourceItem = item
                Next
                item.ExtendedInfosOwner = snapshot.ExtendedInfosOwner
                item.ExtendedInfosIsShared = snapshot.ExtendedInfosIsShared
            Catch
                item.ExtendedInfosIsReceivedShare = Nothing
                item.ExtendedInfosReceivedSharings = New List(Of DmsReceivedShare)
                'Unavailable supplemental metadata is not proof that known aggregate sharing disappeared.
            End Try
        End Sub

        Private Async Function LoadReceivedSharingMetadataAsync(item As DmsResourceItem, token As CancellationToken) As Task
            If Me.OcsSharingClient Is Nothing Then Return
            Await Me.RunSynchronousFallbackAsync(Sub() TryLoadReceivedSharingMetadata(item), token).ConfigureAwait(False)
            token.ThrowIfCancellationRequested()
        End Function

        Private Function VerifyReceivedAncestor(path As String, identity As String) As Boolean
            If String.IsNullOrEmpty(identity) Then Return False
            Dim root = ReceivedPath(Me.OcsRootPath)
            Dim target = ReceivedPath(path)
            If root Is Nothing OrElse target Is Nothing Then Return False
            If root <> "/" AndAlso target <> root AndAlso Not target.StartsWith(root & "/", StringComparison.Ordinal) Then Return False
            Dim relative = If(root = "/", target.TrimStart("/"c), target.Substring(root.Length).TrimStart("/"c))
            If relative.Contains("%") Then Return False
            Try
                Dim response = Me.PropfindResourceAsync(relative, Global.WebDav.ApplyTo.Propfind.ResourceOnly, CancellationToken.None).GetAwaiter().GetResult()
                If Not response.IsSuccessful OrElse response.Resources.Count <> 1 Then Return False
                Dim resource = Me.CreateDmsResourceItem(response.Resources(0))
                Return resource.ItemType <> DmsResourceItem.ItemTypes.File AndAlso String.Equals(resource.ExtendedInfosFolderID, identity, StringComparison.Ordinal)
            Catch
                Return False
            End Try
        End Function

        Friend Sub ApplyReceivedSharingMetadata(item As DmsResourceItem, shares As IEnumerable(Of Share), Optional verifyAncestor As Func(Of String, String, Boolean) = Nothing)
            item.ExtendedInfosReceivedSharings = New List(Of DmsReceivedShare)
            item.ExtendedInfosIsReceivedShare = Nothing
            If shares Is Nothing Then Return
            Dim expected = ReceivedPath(Me.ToOcsPath(item.FullName))
            If expected Is Nothing Then Return
            Dim itemId = If(item.ItemType = DmsResourceItem.ItemTypes.File, item.ExtendedInfosFileID, item.ExtendedInfosFolderID)
            Dim uncertain = False
            For Each share In shares
                If share Is Nothing Then
                    uncertain = True
                    Continue For
                End If
                Dim target = ReceivedPath(share.TargetPath)
                Dim source = If(share.AdvancedProperties?.FileSource, share.AdvancedProperties?.ItemSource)
                Dim kind = share.AdvancedProperties?.ItemType
                Dim sameId = Not String.IsNullOrEmpty(itemId) AndAlso Not String.IsNullOrEmpty(source) AndAlso String.Equals(itemId, source, StringComparison.Ordinal)
                Dim samePath = target IsNot Nothing AndAlso String.Equals(expected, target, StringComparison.Ordinal)
                Dim inherited = Not sameId AndAlso target IsNot Nothing AndAlso expected <> target AndAlso
                    (target = "/" OrElse expected.StartsWith(target & "/", StringComparison.Ordinal))
                If Not sameId AndAlso Not samePath AndAlso Not inherited Then
                    If target Is Nothing Then uncertain = True
                    Continue For
                End If
                Dim compatible = If(item.ItemType = DmsResourceItem.ItemTypes.File, kind = "file", kind = "folder")
                If inherited Then
                    If kind <> "folder" OrElse String.IsNullOrEmpty(source) OrElse verifyAncestor Is Nothing OrElse Not verifyAncestor(target, source) Then
                        uncertain = True
                        Continue For
                    End If
                ElseIf Not sameId OrElse Not compatible Then
                    'Path reuse or incomplete identity cannot establish provenance for the selected object.
                    uncertain = True
                    Continue For
                End If
                Dim grant As New DmsReceivedShare With {
                    .ParentDmsResourceItem = item, .ID = share.ShareId.ToString(Globalization.CultureInfo.InvariantCulture),
                    .SharedBy = New DmsUser With {.ID = share.AdvancedProperties?.Owner, .DisplayName = share.AdvancedProperties?.DisplaynameOwner},
                    .ResourceOwner = New DmsUser With {.ID = share.AdvancedProperties?.FileOwner, .DisplayName = share.AdvancedProperties?.FileOwnerDisplayname},
                    .RecipientKind = If(share.Type = OcsShareType.Group, DmsReceivedShareKind.Group, If(share.Type = OcsShareType.User, DmsReceivedShareKind.User, DmsReceivedShareKind.Unknown)),
                    .RecipientID = If(TryCast(share, UserShare)?.SharedWith, TryCast(share, GroupShare)?.SharedWith),
                    .RecipientDisplayName = share.AdvancedProperties?.SharedWithDisplayname,
                    .SharedPath = target, .IsInherited = inherited, .PermissionsAvailable = share.PermissionsAvailable,
                    .AllowView = HasOcsPermission(CInt(share.Permissions), OcsPermission.Read),
                    .AllowDownload = HasOcsPermission(CInt(share.Permissions), OcsPermission.Read),
                    .AllowEdit = HasOcsPermission(CInt(share.Permissions), OcsPermission.Update),
                    .AllowUpload = HasOcsPermission(CInt(share.Permissions), OcsPermission.Create),
                    .AllowDelete = HasOcsPermission(CInt(share.Permissions), OcsPermission.Delete),
                    .AllowShare = HasOcsPermission(CInt(share.Permissions), OcsPermission.Share)}
                item.ExtendedInfosReceivedSharings.Add(grant)
                If Not inherited AndAlso String.IsNullOrEmpty(item.ExtendedInfosOwner.ID) AndAlso Not String.IsNullOrEmpty(grant.ResourceOwner.ID) Then item.ExtendedInfosOwner = grant.ResourceOwner
            Next
            item.ExtendedInfosIsReceivedShare = If(item.ExtendedInfosReceivedSharings.Count > 0, CType(True, Boolean?), If(uncertain, Nothing, CType(False, Boolean?)))
            ReconcileSharingAggregate(item)
        End Sub

        Private Shared Function ReceivedPath(path As String) As String
            If path Is Nothing Then Return Nothing
            Try
                Dim result = Uri.UnescapeDataString(path)
                If result.Contains("://") OrElse result.Contains("\") OrElse result.Split("/"c).Any(Function(part) part = ".." OrElse part = ".") Then Return Nothing
                Return "/" & result.Trim("/"c)
            Catch ex As UriFormatException
                Return Nothing
            End Try
        End Function

        Private Shared Sub ReconcileSharingAggregate(item As DmsResourceItem)
            If item.ExtendedInfosIsReceivedShare.GetValueOrDefault() OrElse item.ExtendedInfosHasOutgoingSharings.GetValueOrDefault() Then
                item.ExtendedInfosIsShared = True
            ElseIf item.ExtendedInfosIsReceivedShare.HasValue AndAlso item.ExtendedInfosHasOutgoingSharings.HasValue Then
                item.ExtendedInfosIsShared = False
            End If
        End Sub
    End Class
End Namespace
