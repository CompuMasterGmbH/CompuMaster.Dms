Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Deletes a resource by its selected identifier using native asynchronous I/O.</summary>
        ''' <param name="remoteItem">The collection, folder, or file carrying its selected identifier.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>A task representing the deletion request.</returns>
        Protected Overridable Function DeleteNativeResourceAsync(remoteItem As DmsResourceItem, cancellationToken As CancellationToken) As Task
            Select Case remoteItem.ItemType
                Case DmsResourceItem.ItemTypes.Collection
                    Return Me.IOClient.ApiClient.Collection.DeleteCollectionAsync(Me.IOClient.CurrentAuthenticationContextUserID, remoteItem.ExtendedInfosCollectionID, cancellationToken)
                Case DmsResourceItem.ItemTypes.Folder
                    Return Me.IOClient.ApiClient.Folder.DeleteFolderAsync(Me.IOClient.CurrentAuthenticationContextUserID, remoteItem.ExtendedInfosFolderID, cancellationToken)
                Case DmsResourceItem.ItemTypes.File
                    Return Me.IOClient.ApiClient.Document.DeleteDocumentAsync(Me.IOClient.CurrentAuthenticationContextUserID, remoteItem.ExtendedInfosFileID, cancellationToken)
                Case Else
                    Throw New NotImplementedException(remoteItem.ItemType.ToString())
            End Select
        End Function

        ''' <summary>Removes a collection's upload link before deleting the collection.</summary>
        ''' <param name="linkId">The selected upload-link identifier.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>A task representing upload-link deletion.</returns>
        Protected Overridable Function DeleteNativeUploadLinkAsync(linkId As String, cancellationToken As CancellationToken) As Task
            Return Me.IOClient.ApiClient.UploadLink.DeleteLinkAsync(Me.IOClient.CurrentAuthenticationContextUserID, linkId, cancellationToken)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>Retains the selected identifier for duplicate names. Uncertain failures invalidate root navigation caches without replaying the write.</remarks>
        Public Overrides Async Function DeleteRemoteItemAsync(remoteItem As DmsResourceItem, Optional cancellationToken As CancellationToken = Nothing) As Task
            If remoteItem Is Nothing Then Throw New ArgumentNullException(NameOf(remoteItem))
            cancellationToken.ThrowIfCancellationRequested()
            Dim id As String
            Select Case remoteItem.ItemType
                Case DmsResourceItem.ItemTypes.Root
                    Throw New DmsUserErrorMessageException("Root folder can't be deleted")
                Case DmsResourceItem.ItemTypes.Collection : id = remoteItem.ExtendedInfosCollectionID
                Case DmsResourceItem.ItemTypes.Folder : id = remoteItem.ExtendedInfosFolderID
                Case DmsResourceItem.ItemTypes.File : id = remoteItem.ExtendedInfosFileID
                Case Else : Throw New NotImplementedException(remoteItem.ItemType.ToString())
            End Select
            If String.IsNullOrEmpty(id) Then
                If remoteItem.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteItem.FullName)
                Await Me.ValidateNativeUniqueDeletionParentAsync(remoteItem.FullName, cancellationToken).ConfigureAwait(False)
                Dim resolved = Await Me.ListRemoteItemAsync(remoteItem.FullName, cancellationToken).ConfigureAwait(False)
                If resolved Is Nothing Then Throw New Data.FileNotFoundException(remoteItem.FullName)
                If resolved.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(remoteItem.FullName)
                ValidateNativeDeletionType(resolved, remoteItem.ItemType, Nothing)
                Dim resolvedId = If(resolved.ItemType = DmsResourceItem.ItemTypes.File, resolved.ExtendedInfosFileID, If(resolved.ItemType = DmsResourceItem.ItemTypes.Folder, resolved.ExtendedInfosFolderID, resolved.ExtendedInfosCollectionID))
                If String.IsNullOrEmpty(resolvedId) Then Throw New InvalidOperationException("The resolved resource has no identifier: " & remoteItem.FullName)
                Await Me.DeleteRemoteItemAsync(resolved, cancellationToken).ConfigureAwait(False)
                Return
            End If
            Try
                If remoteItem.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso remoteItem.ExtendedInfosHasLinks Then
                    For Each link In remoteItem.ExtendedInfosLinks
                        cancellationToken.ThrowIfCancellationRequested()
                        If link.AllowUpload Then
                            If String.IsNullOrEmpty(link.ID) Then Throw New InvalidOperationException("The collection's upload link has no identifier.")
                            Await Me.DeleteNativeUploadLinkAsync(link.ID, cancellationToken).ConfigureAwait(False)
                        End If
                    Next
                End If
                cancellationToken.ThrowIfCancellationRequested()
                Await Me.DeleteNativeResourceAsync(remoteItem, cancellationToken).ConfigureAwait(False)
            Finally
                'Identifier lookups need not have a selected parent. Fresh root navigation must reconcile any uncertain mutation.
                Me.IOClient.RootDirectory.ResetDirectoriesCache()
                Me.IOClient.RootDirectory.ResetFilesCache()
                If remoteItem.ItemType = DmsResourceItem.ItemTypes.Collection Then Me._AllUploadLinks = Nothing
            End Try
        End Function

        Private Async Function ValidateNativeUniqueDeletionParentAsync(remotePath As String, cancellationToken As CancellationToken) As Task
            Dim parentPath = Me.ParentDirectoryPath(remotePath)
            If parentPath = Nothing Then Return
            Dim directory = Await Me.OpenNativeTransferDirectoryAsync(parentPath, cancellationToken).ConfigureAwait(False)
            While directory.ParentDirectory IsNot Nothing
                Dim siblings = Await directory.ParentDirectory.GetDirectoriesAsync(cancellationToken).ConfigureAwait(False)
                If siblings.Any(Function(sibling) (sibling.CollectionID <> directory.CollectionID OrElse sibling.FolderID <> directory.FolderID) AndAlso String.Equals(sibling.Name, directory.Name, StringComparison.Ordinal)) Then
                    Throw New RemotePathNotUniqueException(remotePath)
                End If
                directory = directory.ParentDirectory
            End While
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function DeleteRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            If remotePath = Nothing OrElse remotePath.Trim(Me.DirectorySeparator) = "" Then Throw New DmsUserErrorMessageException("Root folder can't be deleted")
            Dim parent = Await Me.OpenNativeTransferDirectoryAsync(Me.ParentDirectoryPath(remotePath), cancellationToken).ConfigureAwait(False)
            Dim name = Me.ItemName(remotePath)
            If Not remotePath.EndsWith(Me.DirectorySeparator) Then
                Dim file = Await parent.TryGetFileAsync(name, cancellationToken).ConfigureAwait(False)
                If file IsNot Nothing Then
                    Try
                        Await Me.DeleteRemoteItemAsync(Me.CreateDmsResourceItem(file, False), cancellationToken).ConfigureAwait(False)
                    Finally
                        parent.ResetFilesCache()
                    End Try
                    Return
                End If
            End If
            Dim directory As CenterDevice.IO.DirectoryInfo
            Try
                directory = Await parent.GetDirectoryAsync(name, cancellationToken).ConfigureAwait(False)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New Data.DirectoryNotFoundException(remotePath, ex)
            End Try
            Dim links As CenterDevice.Rest.Clients.Link.UploadLinks = Nothing
            If directory.CollectionID <> Nothing Then links = Await Me.LoadNativeUploadLinksAsync(cancellationToken).ConfigureAwait(False)
            Try
                Await Me.DeleteRemoteItemAsync(Me.CreateDmsResourceItem(directory, links, False), cancellationToken).ConfigureAwait(False)
            Finally
                parent.ResetDirectoriesCache()
            End Try
        End Function

        Private Shared Sub ValidateNativeDeletionType(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes?)
            If expectedItemType = Nothing Then Throw New ArgumentNullException(NameOf(expectedItemType))
            If alternativeExpectedItemType.HasValue AndAlso alternativeExpectedItemType.Value = Nothing Then Throw New ArgumentNullException(NameOf(alternativeExpectedItemType))
            If remoteItem Is Nothing Then Throw New ArgumentNullException(NameOf(remoteItem))
            If remoteItem.ItemType <> expectedItemType AndAlso (Not alternativeExpectedItemType.HasValue OrElse remoteItem.ItemType <> alternativeExpectedItemType.Value) Then
                Throw New ArgumentException("ItemType " & expectedItemType.ToString() & If(alternativeExpectedItemType.HasValue, " or " & alternativeExpectedItemType.Value.ToString(), "") & " expected, but was " & remoteItem.ItemType.ToString(), NameOf(remoteItem))
            End If
        End Sub

        ''' <inheritdoc/>
        Public Overrides Function DeleteRemoteItemAsync(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            ValidateNativeDeletionType(remoteItem, expectedItemType, Nothing)
            Return Me.DeleteRemoteItemAsync(remoteItem, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function DeleteRemoteItemAsync(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            ValidateNativeDeletionType(remoteItem, expectedItemType, alternativeExpectedItemType)
            Return Me.DeleteRemoteItemAsync(remoteItem, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function DeleteRemoteItemAsync(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            Dim item = Await Me.ListRemoteItemAsync(remotePath, cancellationToken).ConfigureAwait(False)
            If item Is Nothing Then Throw New Data.FileNotFoundException(remotePath)
            Await Me.DeleteRemoteItemAsync(item, expectedItemType, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function DeleteRemoteItemAsync(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes, alternativeExpectedItemType As DmsResourceItem.ItemTypes, Optional cancellationToken As CancellationToken = Nothing) As Task
            Dim item = Await Me.ListRemoteItemAsync(remotePath, cancellationToken).ConfigureAwait(False)
            If item Is Nothing Then Throw New Data.FileNotFoundException(remotePath)
            Await Me.DeleteRemoteItemAsync(item, expectedItemType, alternativeExpectedItemType, cancellationToken).ConfigureAwait(False)
        End Function
    End Class
End Namespace
