Option Explicit On
Option Strict On

Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Link
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Retrieves upload links for a native listing snapshot.</summary>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The current upload links with sharing limits and usage metadata.</returns>
        Protected Overridable Function LoadNativeUploadLinksAsync(cancellationToken As CancellationToken) As Task(Of UploadLinks)
            Return Me.IOClient.ApiClient.UploadLinks.GetAllUploadLinksAsync(Me.IOClient.CurrentAuthenticationContextUserID, cancellationToken)
        End Function

        ''' <inheritdoc/>
        ''' <exception cref="Data.DirectoryNotFoundException">The directory to list does not exist.</exception>
        Public Overrides Function ListAllRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.ListNativeRemoteItemsAsync(remoteFolderPath, searchType, True, cancellationToken)
        End Function

        Private Async Function ListNativeRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, prepareDetails As Boolean, cancellationToken As CancellationToken) As Task(Of List(Of DmsResourceItem))
            Dim directory = Await Me.OpenDirectoryForListingAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Dim result As New List(Of DmsResourceItem)
            If searchType = SearchItemType.Folders OrElse searchType = SearchItemType.Collections OrElse searchType = SearchItemType.AllItems Then
                Dim children = Await directory.GetDirectoriesAsync(cancellationToken).ConfigureAwait(False)
                Dim links As UploadLinks = Nothing
                If children.Any(Function(child) child.CollectionID IsNot Nothing) Then
                    links = Await Me.LoadNativeUploadLinksAsync(cancellationToken).ConfigureAwait(False)
                End If
                For Each child In children
                    cancellationToken.ThrowIfCancellationRequested()
                    If searchType = SearchItemType.AllItems OrElse
                        (searchType = SearchItemType.Collections AndAlso child.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Collection) OrElse
                        (searchType = SearchItemType.Folders AndAlso child.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Folder) Then
                        Dim collision = children.Any(Function(sibling) (sibling.CollectionID <> child.CollectionID OrElse sibling.FolderID <> child.FolderID) AndAlso String.Equals(sibling.Name, child.Name, StringComparison.Ordinal))
                        result.Add(Me.CreateDmsResourceItem(child, links, collision))
                    End If
                Next
            End If
            If searchType = SearchItemType.Files OrElse searchType = SearchItemType.AllItems Then
                Dim files = Await directory.GetFilesAsync(cancellationToken).ConfigureAwait(False)
                For Each file In files
                    cancellationToken.ThrowIfCancellationRequested()
                    Dim collision = files.Any(Function(sibling) sibling.ID <> file.ID AndAlso String.Equals(sibling.FileName, file.FileName, StringComparison.Ordinal))
                    result.Add(Me.CreateDmsResourceItem(file, collision))
                Next
            End If
            If prepareDetails Then
                Await Me.PrepareNativePrincipalSnapshotsAsync(result, cancellationToken).ConfigureAwait(False)
                Await Me.PrepareNativeLinkSnapshotsAsync(result, cancellationToken).ConfigureAwait(False)
            End If
            Return result
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListFileEntriesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.ListNativeRemoteItemsAsync(remoteFolderPath, SearchItemType.Files, False, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListAllCollectionItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.Collections, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListAllFolderItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.Folders, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListAllFileItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Me.ListAllRemoteItemsAsync(remoteFolderPath, SearchItemType.Files, cancellationToken)
        End Function

        ''' <inheritdoc/>
        ''' <exception cref="Data.DirectoryNotFoundException">The directory to list does not exist.</exception>
        Public Overrides Async Function ListAllCollectionNamesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            Dim directory = Await Me.OpenDirectoryForListingAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Return (Await directory.GetDirectoriesAsync(cancellationToken).ConfigureAwait(False)).Where(Function(child) child.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Collection).Select(Function(child) child.Name).ToList()
        End Function

        ''' <inheritdoc/>
        ''' <exception cref="Data.DirectoryNotFoundException">The directory to list does not exist.</exception>
        Public Overrides Async Function ListAllFolderNamesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            Dim directory = Await Me.OpenDirectoryForListingAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Return (Await directory.GetDirectoriesAsync(cancellationToken).ConfigureAwait(False)).Select(Function(child) child.Name).ToList()
        End Function

        Private Async Function OpenDirectoryForListingAsync(remoteFolderPath As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            Try
                Return Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New Data.DirectoryNotFoundException(remoteFolderPath, ex)
            End Try
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function ListAllFileNamesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of String))
            Dim directory = Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Return (Await directory.GetFilesAsync(cancellationToken).ConfigureAwait(False)).Select(Function(file) file.FileName).ToList()
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function ResetCachesForRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Dim directory = Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Select Case searchType
                Case SearchItemType.AllItems
                    directory.ResetDirectoriesCache()
                    directory.ResetFilesCache()
                Case SearchItemType.Folders, SearchItemType.Collections
                    directory.ResetDirectoriesCache()
                Case SearchItemType.Files
                    directory.ResetFilesCache()
                Case Else
                    Throw New ArgumentOutOfRangeException(NameOf(searchType))
            End Select
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function RemoteItemExistsUniquelyAsAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem.FoundItemResult)
            Dim item = Await Me.ListRemoteItemAsync(remotePath, cancellationToken).ConfigureAwait(False)
            If item Is Nothing Then Return DmsResourceItem.FoundItemResult.NotFound
            If item.ExtendedInfosCollisionDetected Then Return DmsResourceItem.FoundItemResult.WithNameCollisions
            Return CType(CType(item.ItemType, Byte), DmsResourceItem.FoundItemResult)
        End Function
        ''' <summary>Retrieves immediate child folders with optional child-directory metadata.</summary>
        ''' <param name="directory">The parent directory to list.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The folder records retaining optional child-directory metadata.</returns>
        Protected Overridable Async Function LoadNativeChildFolderMetadataAsync(directory As CenterDevice.IO.DirectoryInfo, cancellationToken As CancellationToken) As Task(Of List(Of CenterDevice.Rest.Clients.Folders.Folder))
            Dim parentId = If(directory.CollectionID IsNot Nothing, CenterDevice.Rest.RestApiConstants.NONE, directory.FolderID)
            Return (Await CenterDeviceFolderMetadataClient.Create(Me.IOClient.ApiClient).GetFoldersWithMetadataAsync(Me.IOClient.CurrentAuthenticationContextUserID, directory.CollectionID, parentId, cancellationToken).ConfigureAwait(False)).Cast(Of CenterDevice.Rest.Clients.Folders.Folder)().ToList()
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function ListDirectoryEntriesAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim directory = Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Dim result As New List(Of DmsResourceItem)
            Dim uploadLinks As UploadLinks = Nothing
            If directory.IsRootDirectory Then
                Dim children = Await directory.GetDirectoriesAsync(cancellationToken).ConfigureAwait(False)
                If children.Any(Function(child) child.CollectionID IsNot Nothing) Then
                    uploadLinks = Await Me.LoadNativeUploadLinksAsync(cancellationToken).ConfigureAwait(False)
                End If
                For Each child In children
                    cancellationToken.ThrowIfCancellationRequested()
                    Dim collision = children.Any(Function(sibling) (sibling.CollectionID <> child.CollectionID OrElse sibling.FolderID <> child.FolderID) AndAlso String.Equals(sibling.Name, child.Name, StringComparison.Ordinal))
                    result.Add(Me.CreateDmsResourceItem(child, uploadLinks, collision))
                Next
            Else
                Dim folders = Await Me.LoadNativeChildFolderMetadataAsync(directory, cancellationToken).ConfigureAwait(False)
                For Each folder In folders
                    cancellationToken.ThrowIfCancellationRequested()
                    Dim collision = folders.Any(Function(sibling) sibling.Id <> folder.Id AndAlso String.Equals(sibling.Name, folder.Name, StringComparison.Ordinal))
                    Dim item = Me.CreateDmsResourceItem(New CenterDevice.IO.DirectoryInfo(Me.IOClient, directory, folder), Nothing, collision)
                    Dim metadata = TryCast(folder, FolderWithChildMetadata)
                    If metadata IsNot Nothing Then item.HasChildDirectories = metadata.HasSubFoldersMetadata
                    result.Add(item)
                Next
            End If
            Return result
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function ListAllDirectoryItemsAsync(remoteFolderPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim directory = Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            If directory.IsRootDirectory Then Return Await Me.ListAllCollectionItemsAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Dim folders = Await Me.LoadNativeChildFolderMetadataAsync(directory, cancellationToken).ConfigureAwait(False)
            Dim result As New List(Of DmsResourceItem)
            For Each folder In folders
                cancellationToken.ThrowIfCancellationRequested()
                Dim collision = folders.Any(Function(sibling) sibling.Id <> folder.Id AndAlso String.Equals(sibling.Name, folder.Name, StringComparison.Ordinal))
                Dim item = Me.CreateDmsResourceItem(New CenterDevice.IO.DirectoryInfo(Me.IOClient, directory, folder), Nothing, collision)
                Dim metadata = TryCast(folder, FolderWithChildMetadata)
                If metadata IsNot Nothing Then item.HasChildDirectories = metadata.HasSubFoldersMetadata
                result.Add(item)
            Next
            Await Me.PrepareNativePrincipalSnapshotsAsync(result, cancellationToken).ConfigureAwait(False)
            Await Me.PrepareNativeLinkSnapshotsAsync(result, cancellationToken).ConfigureAwait(False)
            Return result
        End Function

        Private Async Function ConvertNativeDirectoryAsync(directory As CenterDevice.IO.DirectoryInfo, cancellationToken As CancellationToken) As Task(Of DmsResourceItem)
            Dim collision As Boolean = False
            If directory.ParentDirectory IsNot Nothing Then
                Dim siblings = Await directory.ParentDirectory.GetDirectoriesAsync(cancellationToken).ConfigureAwait(False)
                collision = siblings.Any(Function(sibling) (sibling.CollectionID <> directory.CollectionID OrElse sibling.FolderID <> directory.FolderID) AndAlso String.Equals(sibling.Name, directory.Name, StringComparison.Ordinal))
            End If
            Dim links As UploadLinks = Nothing
            If directory.CollectionID IsNot Nothing Then links = Await Me.LoadNativeUploadLinksAsync(cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Dim result = Me.CreateDmsResourceItem(directory, links, collision)
            Await Me.PrepareNativePrincipalSnapshotsAsync(New List(Of DmsResourceItem) From {result}, cancellationToken).ConfigureAwait(False)
            Await Me.PrepareNativeLinkSnapshotsAsync(New List(Of DmsResourceItem) From {result}, cancellationToken).ConfigureAwait(False)
            Return result
        End Function

        Private Async Function ConvertNativeFileAsync(file As CenterDevice.IO.FileInfo, cancellationToken As CancellationToken) As Task(Of DmsResourceItem)
            Dim collision As Boolean = False
            If file.ParentDirectory IsNot Nothing Then
                Dim siblings = Await file.ParentDirectory.GetFilesAsync(cancellationToken).ConfigureAwait(False)
                collision = siblings.Any(Function(sibling) sibling.ID <> file.ID AndAlso String.Equals(sibling.FileName, file.FileName, StringComparison.Ordinal))
            End If
            cancellationToken.ThrowIfCancellationRequested()
            Dim result = Me.CreateDmsResourceItem(file, collision)
            Await Me.PrepareNativePrincipalSnapshotsAsync(New List(Of DmsResourceItem) From {result}, cancellationToken).ConfigureAwait(False)
            Await Me.PrepareNativeLinkSnapshotsAsync(New List(Of DmsResourceItem) From {result}, cancellationToken).ConfigureAwait(False)
            Return result
        End Function

        ''' <summary>Retrieves collection metadata by identifier using native asynchronous I/O.</summary>
        ''' <param name="id">The collection identifier.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The collection metadata without selecting a parent path.</returns>
        Protected Overridable Function LoadNativeCollectionByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Collections.Collection)
            Return Me.IOClient.ApiClient.Collection.GetCollectionAsync(Me.IOClient.CurrentAuthenticationContextUserID, id, cancellationToken)
        End Function

        ''' <summary>Retrieves folder metadata by identifier using native asynchronous I/O.</summary>
        ''' <param name="id">The folder identifier.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The folder metadata without selecting a parent path.</returns>
        Protected Overridable Function LoadNativeFolderByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Folders.Folder)
            Return Me.IOClient.ApiClient.Folder.GetFolderAsync(Me.IOClient.CurrentAuthenticationContextUserID, id, Nothing, cancellationToken)
        End Function

        ''' <summary>Retrieves file metadata by identifier using native asynchronous I/O.</summary>
        ''' <param name="id">The file identifier.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The file metadata retaining all referencing collection and folder identifiers.</returns>
        Protected Overridable Function LoadNativeFileByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata)
            Return Me.IOClient.ApiClient.Document.GetDocumentMetadataAsync(Me.IOClient.CurrentAuthenticationContextUserID, id, cancellationToken)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>The identifier selects the collection independently of same-name paths. Cancellation reaches active HTTP I/O.</remarks>
        Public Overrides Async Function FindCollectionByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            cancellationToken.ThrowIfCancellationRequested()
            Dim metadata = Await Me.LoadNativeCollectionByIdAsync(id, cancellationToken).ConfigureAwait(False)
            Return Await Me.ConvertNativeDirectoryAsync(New CenterDevice.IO.DirectoryInfo(Me.IOClient, Nothing, metadata), cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>The identifier selects the folder without choosing a parent path. Cancellation reaches active HTTP I/O.</remarks>
        Public Overrides Async Function FindFolderByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            cancellationToken.ThrowIfCancellationRequested()
            Dim metadata = Await Me.LoadNativeFolderByIdAsync(id, cancellationToken).ConfigureAwait(False)
            Dim item = Await Me.ConvertNativeDirectoryAsync(New CenterDevice.IO.DirectoryInfo(Me.IOClient, Nothing, metadata), cancellationToken).ConfigureAwait(False)
            Dim childMetadata = TryCast(metadata, FolderWithChildMetadata)
            If childMetadata IsNot Nothing Then item.HasChildDirectories = childMetadata.HasSubFoldersMetadata
            Return item
        End Function

        ''' <inheritdoc/>
        ''' <remarks>The returned item retains reference identifiers; its name is not a selected collection/folder path. Cancellation reaches active HTTP I/O.</remarks>
        Public Overrides Async Function FindFileByIdAsync(id As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            cancellationToken.ThrowIfCancellationRequested()
            Dim metadata = Await Me.LoadNativeFileByIdAsync(id, cancellationToken).ConfigureAwait(False)
            Return Await Me.ConvertNativeFileAsync(New CenterDevice.IO.FileInfo(Me.IOClient, Nothing, metadata), cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function ListRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Dim parentPath = Me.ParentDirectoryPath(remotePath)
            Dim parent As CenterDevice.IO.DirectoryInfo
            Try
                parent = If(parentPath Is Nothing, Me.IOClient.RootDirectory, Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(parentPath, cancellationToken).ConfigureAwait(False))
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Return Nothing
            End Try
            Dim file = Await parent.TryGetFileAsync(Me.ItemName(remotePath), cancellationToken).ConfigureAwait(False)
            If file IsNot Nothing Then Return Await Me.ConvertNativeFileAsync(file, cancellationToken).ConfigureAwait(False)
            Dim directory As CenterDevice.IO.DirectoryInfo
            Try
                directory = Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remotePath, cancellationToken).ConfigureAwait(False)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Return Nothing
            End Try
            Return Await Me.ConvertNativeDirectoryAsync(directory, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function RemoteItemExistsAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of Boolean)
            Return Await Me.FindNativeItemTypeAsync(remotePath, cancellationToken).ConfigureAwait(False) <> DmsResourceItem.FoundItemType.NotFound
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function RemoteItemExistsAsAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem.FoundItemType)
            Return Await Me.FindNativeItemTypeAsync(remotePath, cancellationToken).ConfigureAwait(False)
        End Function

        Private Async Function FindNativeItemTypeAsync(remotePath As String, cancellationToken As CancellationToken) As Task(Of DmsResourceItem.FoundItemType)
            cancellationToken.ThrowIfCancellationRequested()
            Dim parentPath = Me.ParentDirectoryPath(remotePath)
            Dim parent As CenterDevice.IO.DirectoryInfo
            Try
                parent = If(parentPath Is Nothing, Me.IOClient.RootDirectory, Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(parentPath, cancellationToken).ConfigureAwait(False))
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Return DmsResourceItem.FoundItemType.NotFound
            End Try
            If Await parent.TryGetFileAsync(Me.ItemName(remotePath), cancellationToken).ConfigureAwait(False) IsNot Nothing Then Return DmsResourceItem.FoundItemType.File
            Dim directory As CenterDevice.IO.DirectoryInfo
            Try
                directory = Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remotePath, cancellationToken).ConfigureAwait(False)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Return DmsResourceItem.FoundItemType.NotFound
            End Try
            cancellationToken.ThrowIfCancellationRequested()
            If directory.IsRootDirectory Then Return DmsResourceItem.FoundItemType.Root
            If directory.CollectionID <> Nothing Then Return DmsResourceItem.FoundItemType.Collection
            Return DmsResourceItem.FoundItemType.Folder
        End Function
        ''' <inheritdoc/>
        Public Overrides Function GetCurrentContextUserIDAsync(Optional cancellationToken As CancellationToken = Nothing) As Task(Of String)
            Return Me.IOClient.CurrentContextUserIdAsync(cancellationToken)
        End Function
    End Class
End Namespace
