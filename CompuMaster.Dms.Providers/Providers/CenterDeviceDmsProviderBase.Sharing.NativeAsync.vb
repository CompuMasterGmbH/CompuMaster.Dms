Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Common
Imports CenterDevice.Rest.Clients.Groups
Imports CenterDevice.Rest.Clients.User
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Changes user or group sharing by the selected native resource identifier.</summary>
        ''' <param name="resource">The selected collection or folder.</param>
        ''' <param name="users">The user identifiers, or nothing when only groups change.</param>
        ''' <param name="groups">The group identifiers, or nothing when only users change.</param>
        ''' <param name="remove">Whether the selected sharing entries are removed.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The server response, including any failed principals.</returns>
        Protected Overridable Function ChangeNativeSharingAsync(resource As DmsResourceItem, users As List(Of String), groups As List(Of String), remove As Boolean, cancellationToken As CancellationToken) As Task(Of SharingResponse)
            If resource.ItemType = DmsResourceItem.ItemTypes.Collection Then
                If remove Then Return Me.IOClient.ApiClient.Collection.UnshareCollectionAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosCollectionID, users, groups, cancellationToken)
                Return Me.IOClient.ApiClient.Collection.ShareCollectionAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosCollectionID, users, groups, cancellationToken)
            End If
            If remove Then Return Me.IOClient.ApiClient.Folder.UnshareFolderAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosFolderID, users, groups, cancellationToken)
            Return Me.IOClient.ApiClient.Folder.ShareFolderAsync(Me.IOClient.CurrentAuthenticationContextUserID, resource.ExtendedInfosFolderID, users, groups, cancellationToken)
        End Function

        Private Async Function ApplyNativeSharingAsync(resource As DmsResourceItem, users As List(Of String), groups As List(Of String), remove As Boolean, cancellationToken As CancellationToken) As Task
            If resource Is Nothing Then Throw New ArgumentNullException(NameOf(resource))
            cancellationToken.ThrowIfCancellationRequested()
            If resource.ItemType <> DmsResourceItem.ItemTypes.Collection AndAlso resource.ItemType <> DmsResourceItem.ItemTypes.Folder Then Throw New NotSupportedException("User/group sharing is supported only for collections and folders.")
            Dim id = If(resource.ItemType = DmsResourceItem.ItemTypes.Collection, resource.ExtendedInfosCollectionID, resource.ExtendedInfosFolderID)
            If String.IsNullOrEmpty(id) Then Throw New InvalidOperationException("Sharing requires the selected resource identifier.")
            Dim principals = If(users, groups)
            If principals Is Nothing OrElse principals.Count = 0 OrElse principals.Any(Function(value) String.IsNullOrWhiteSpace(value)) Then Throw New ArgumentException("Sharing requires a nonempty user or group identifier.")
            Try
                Dim response = Await Me.ChangeNativeSharingAsync(resource, users, groups, remove, cancellationToken).ConfigureAwait(False)
                ValidateSharingResponse(response)
            Finally
                'Selected identifiers need not carry a parent path. Reconcile uncertain changes through fresh navigation.
                Me.IOClient.RootDirectory.ResetDirectoriesCache()
                Me.IOClient.RootDirectory.ResetFilesCache()
            End Try
        End Function

        ''' <inheritdoc/>
        Public Overrides Function CreateSharingAsync(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup, Optional cancellationToken As CancellationToken = Nothing) As Task
            If dmsResource Is Nothing Then Throw New ArgumentNullException(NameOf(dmsResource))
            If shareInfo Is Nothing Then Throw New ArgumentNullException(NameOf(shareInfo))
            Return Me.ApplyNativeSharingAsync(shareInfo.ParentDmsResourceItem, Nothing, New List(Of String) From {shareInfo.Group.ID}, False, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function CreateSharingAsync(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser, Optional cancellationToken As CancellationToken = Nothing) As Task
            If dmsResource Is Nothing Then Throw New ArgumentNullException(NameOf(dmsResource))
            If shareInfo Is Nothing Then Throw New ArgumentNullException(NameOf(shareInfo))
            Return Me.ApplyNativeSharingAsync(shareInfo.ParentDmsResourceItem, New List(Of String) From {shareInfo.User.ID}, Nothing, False, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function DeleteSharingAsync(shareInfo As DmsShareForGroup, Optional cancellationToken As CancellationToken = Nothing) As Task
            If shareInfo Is Nothing Then Throw New ArgumentNullException(NameOf(shareInfo))
            Return Me.ApplyNativeSharingAsync(shareInfo.ParentDmsResourceItem, Nothing, New List(Of String) From {shareInfo.Group.ID}, True, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function DeleteSharingAsync(shareInfo As DmsShareForUser, Optional cancellationToken As CancellationToken = Nothing) As Task
            If shareInfo Is Nothing Then Throw New ArgumentNullException(NameOf(shareInfo))
            Return Me.ApplyNativeSharingAsync(shareInfo.ParentDmsResourceItem, New List(Of String) From {shareInfo.User.ID}, Nothing, True, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function UpdateSharingAsync(shareInfo As DmsShareForGroup, Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Throw New NotSupportedException("Updating of share properties not supported")
        End Function

        ''' <inheritdoc/>
        Public Overrides Function UpdateSharingAsync(shareInfo As DmsShareForUser, Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Throw New NotSupportedException("Updating of share properties not supported")
        End Function

        ''' <summary>Retrieves visible groups through native asynchronous HTTP I/O.</summary>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The visible group metadata.</returns>
        Protected Overridable Function LoadNativeGroupsAsync(cancellationToken As CancellationToken) As Task(Of GroupList)
            Return Me.IOClient.ApiClient.Groups.GetAllGroupsAsync(Me.IOClient.CurrentAuthenticationContextUserID, CenterDevice.Model.Groups.GroupsFilter.AllVisibleGroupsForCurrentUser, cancellationToken)
        End Function

        ''' <summary>Retrieves active visible users through native asynchronous HTTP I/O.</summary>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The active user metadata.</returns>
        Protected Overridable Function LoadNativeUsersAsync(cancellationToken As CancellationToken) As Task(Of UserList(Of BaseUserData))
            Return CenterDeviceUserMetadataClient.Create(Me.IOClient.ApiClient).GetUsersAsync(Me.IOClient.CurrentAuthenticationContextUserID, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function GetAllGroupsAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsGroup))
            cancellationToken.ThrowIfCancellationRequested()
            Dim metadata = Await Me.LoadNativeGroupsAsync(cancellationToken).ConfigureAwait(False)
            Dim result As New List(Of DmsGroup)()
            If metadata?.Groups Is Nothing Then Throw New InvalidOperationException("The native group response contains no group list.")
            For Each group In metadata.Groups
                cancellationToken.ThrowIfCancellationRequested()
                result.Add(New DmsGroup With {.ID = group.Id, .Name = Me.NormalizeGroupDisplayName(group.Id, group.Name)})
            Next
            Return result
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function GetAllUsersAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsUser))
            cancellationToken.ThrowIfCancellationRequested()
            Dim metadata = Await Me.LoadNativeUsersAsync(cancellationToken).ConfigureAwait(False)
            Dim result As New List(Of DmsUser)()
            If metadata?.Users Is Nothing Then Throw New InvalidOperationException("The native user response contains no user list.")
            For Each user In metadata.Users
                cancellationToken.ThrowIfCancellationRequested()
                result.Add(New DmsUser With {.ID = user.Id, .DisplayName = If(user.GetFullName(), String.Empty).Trim(), .EMailAddress = user.Email})
            Next
            Return result
        End Function
    End Class
End Namespace
