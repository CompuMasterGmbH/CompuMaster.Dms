Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Reconstructs a selected folder hierarchy through native identifier lookups.</summary>
        ''' <param name="folderId">The selected folder identifier.</param>
        ''' <param name="expectedCollectionId">The selected collection identifier, or nothing when unknown.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The folder with its explicit parent chain.</returns>
        ''' <exception cref="InvalidOperationException">The hierarchy contains a cycle or no longer matches the selected collection.</exception>
        Protected Overridable Function GetNativeFolderDirectoryByIdAsync(folderId As String, expectedCollectionId As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            Return Me.LoadNativeFolderHierarchyAsync(folderId, expectedCollectionId, New HashSet(Of String)(StringComparer.Ordinal), cancellationToken)
        End Function

        Private Async Function LoadNativeFolderHierarchyAsync(folderId As String, expectedCollectionId As String, visited As HashSet(Of String), cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            cancellationToken.ThrowIfCancellationRequested()
            If String.IsNullOrEmpty(folderId) Then Throw New ArgumentException("A selected folder identifier is required.", NameOf(folderId))
            If Not visited.Add(folderId) Then Throw New InvalidOperationException("A cycle was found in the CenterDevice folder hierarchy at folder ID " & folderId & ".")
            Dim metadata = Await Me.LoadNativeFolderByIdAsync(folderId, cancellationToken).ConfigureAwait(False)
            If metadata Is Nothing Then Throw New Data.DirectoryNotFoundException(folderId)
            If Not String.Equals(metadata.Id, folderId, StringComparison.Ordinal) Then Throw New InvalidOperationException("Folder metadata does not match the requested identifier.")
            If Not String.IsNullOrEmpty(expectedCollectionId) AndAlso Not String.IsNullOrEmpty(metadata.Collection) AndAlso Not String.Equals(metadata.Collection, expectedCollectionId, StringComparison.Ordinal) Then
                Throw New InvalidOperationException("Folder ID " & folderId & " no longer belongs to its expected CenterDevice collection.")
            End If
            Dim collectionId = If(String.IsNullOrEmpty(metadata.Collection), expectedCollectionId, metadata.Collection)
            Dim parent As CenterDevice.IO.DirectoryInfo
            If String.IsNullOrEmpty(metadata.Parent) OrElse String.Equals(metadata.Parent, CenterDevice.Rest.RestApiConstants.NONE, StringComparison.Ordinal) Then
                If String.IsNullOrEmpty(collectionId) Then Throw New InvalidOperationException("The collection of CenterDevice folder ID " & folderId & " is unknown.")
                parent = Await Me.LoadNativeCollectionDirectoryAsync(collectionId, cancellationToken).ConfigureAwait(False)
            Else
                parent = Await Me.LoadNativeFolderHierarchyAsync(metadata.Parent, collectionId, visited, cancellationToken).ConfigureAwait(False)
            End If
            cancellationToken.ThrowIfCancellationRequested()
            Return New CenterDevice.IO.DirectoryInfo(Me.IOClient, parent, metadata)
        End Function

        Private Async Function LoadNativeCollectionDirectoryAsync(collectionId As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            cancellationToken.ThrowIfCancellationRequested()
            Dim metadata = Await Me.LoadNativeCollectionByIdAsync(collectionId, cancellationToken).ConfigureAwait(False)
            If metadata Is Nothing Then Throw New Data.DirectoryNotFoundException(collectionId)
            If Not String.Equals(metadata.Id, collectionId, StringComparison.Ordinal) Then Throw New InvalidOperationException("Collection metadata does not match the requested identifier.")
            cancellationToken.ThrowIfCancellationRequested()
            Return New CenterDevice.IO.DirectoryInfo(Me.IOClient, Me.IOClient.RootDirectory, metadata)
        End Function

        ''' <summary>Resolves the selected directory for a native copy or move without selecting a same-name sibling.</summary>
        ''' <param name="resource">The selected collection or folder.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The directory with an explicit parent hierarchy.</returns>
        Protected Overridable Async Function GetNativeDirectoryForActionAsync(resource As DmsResourceItem, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            If resource Is Nothing Then Throw New ArgumentNullException(NameOf(resource))
            cancellationToken.ThrowIfCancellationRequested()
            Select Case resource.ItemType
                Case DmsResourceItem.ItemTypes.Collection
                    If Not String.IsNullOrEmpty(resource.ExtendedInfosCollectionID) Then Return Await Me.LoadNativeCollectionDirectoryAsync(resource.ExtendedInfosCollectionID, cancellationToken).ConfigureAwait(False)
                Case DmsResourceItem.ItemTypes.Folder
                    If Not String.IsNullOrEmpty(resource.ExtendedInfosFolderID) Then Return Await Me.GetNativeFolderDirectoryByIdAsync(resource.ExtendedInfosFolderID, resource.ExtendedInfosAssignedCollectionID, cancellationToken).ConfigureAwait(False)
                Case Else
                    Throw New NotSupportedException("The selected resource is not a collection or folder.")
            End Select
            If resource.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(resource.FullName)
            Dim directory = Await Me.OpenNativeTransferDirectoryAsync(resource.FullName, cancellationToken).ConfigureAwait(False)
            Await Me.ValidateNativeUniqueActionDirectoryAsync(directory, resource.FullName, cancellationToken).ConfigureAwait(False)
            If (resource.ItemType = DmsResourceItem.ItemTypes.Collection) <> (directory.Type = CenterDevice.IO.DirectoryInfo.DirectoryType.Collection) Then Throw New InvalidOperationException("The selected directory path no longer has the expected type.")
            Return directory
        End Function

        Private Async Function ValidateNativeUniqueActionDirectoryAsync(directory As CenterDevice.IO.DirectoryInfo, remotePath As String, cancellationToken As CancellationToken) As Task
            While directory.ParentDirectory IsNot Nothing
                Dim siblings = Await directory.ParentDirectory.GetDirectoriesAsync(cancellationToken).ConfigureAwait(False)
                If siblings.Any(Function(sibling) (sibling.CollectionID <> directory.CollectionID OrElse sibling.FolderID <> directory.FolderID) AndAlso String.Equals(sibling.Name, directory.Name, StringComparison.Ordinal)) Then Throw New RemotePathNotUniqueException(remotePath)
                directory = directory.ParentDirectory
            End While
        End Function

        ''' <summary>Resolves a selected native file and, for moves, its explicit source parent.</summary>
        ''' <param name="resource">The selected file with its identifier and optional assigned parent identifiers.</param>
        ''' <param name="requireParent">Whether an unambiguous source parent is required for a move.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>The selected file. A copy-only identifier lookup may leave its parent unset.</returns>
        ''' <exception cref="InvalidOperationException">A parentless file has multiple or hidden directory references and cannot be moved without selecting its source parent.</exception>
        Protected Overridable Async Function GetNativeFileForActionAsync(resource As DmsResourceItem, requireParent As Boolean, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.FileInfo)
            If resource Is Nothing Then Throw New ArgumentNullException(NameOf(resource))
            cancellationToken.ThrowIfCancellationRequested()
            If resource.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New NotSupportedException("The selected resource is not a file.")
            If String.IsNullOrEmpty(resource.ExtendedInfosFileID) Then
                If resource.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(resource.FullName)
                Await Me.ValidateNativeUniqueDeletionParentAsync(resource.FullName, cancellationToken).ConfigureAwait(False)
                Dim resolved = Await Me.ListRemoteItemAsync(resource.FullName, cancellationToken).ConfigureAwait(False)
                If resolved Is Nothing Then Throw New Data.FileNotFoundException(resource.FullName)
                If resolved.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New InvalidOperationException("The selected file path no longer identifies a file.")
                If resolved.ExtendedInfosCollisionDetected Then Throw New RemotePathNotUniqueException(resource.FullName)
                If String.IsNullOrEmpty(resolved.ExtendedInfosFileID) Then Throw New InvalidOperationException("The resolved file has no identifier.")
                Return Await Me.GetNativeFileForActionAsync(resolved, requireParent, cancellationToken).ConfigureAwait(False)
            End If
            If Not requireParent Then Return Await Me.GetNativeDownloadFileByIdAsync(resource.ExtendedInfosFileID, cancellationToken).ConfigureAwait(False)
            Dim parent As CenterDevice.IO.DirectoryInfo
            If Not String.IsNullOrEmpty(resource.ExtendedInfosAssignedFolderID) Then
                parent = Await Me.GetNativeFolderDirectoryByIdAsync(resource.ExtendedInfosAssignedFolderID, resource.ExtendedInfosAssignedCollectionID, cancellationToken).ConfigureAwait(False)
            ElseIf Not String.IsNullOrEmpty(resource.ExtendedInfosAssignedCollectionID) Then
                parent = Await Me.LoadNativeCollectionDirectoryAsync(resource.ExtendedInfosAssignedCollectionID, cancellationToken).ConfigureAwait(False)
            ElseIf Me.ParentDirectoryPath(resource.FullName) <> Nothing Then
                parent = Await Me.OpenNativeTransferDirectoryAsync(Me.ParentDirectoryPath(resource.FullName), cancellationToken).ConfigureAwait(False)
                Await Me.ValidateNativeUniqueActionDirectoryAsync(parent, resource.FullName, cancellationToken).ConfigureAwait(False)
            Else
                Dim metadata = Await Me.LoadNativeFileByIdAsync(resource.ExtendedInfosFileID, cancellationToken).ConfigureAwait(False)
                If metadata Is Nothing Then Throw New Data.FileNotFoundException(resource.FullName)
                If Not String.Equals(metadata.Id, resource.ExtendedInfosFileID, StringComparison.Ordinal) Then Throw New InvalidOperationException("File metadata does not match the requested identifier.")
                Dim collections = metadata.Collections?.Visible
                If collections Is Nothing OrElse collections.Count <> 1 OrElse metadata.Collections.NotVisibleCount <> 0 OrElse (metadata.Folders IsNot Nothing AndAlso metadata.Folders.Count > 1) Then
                    Throw New InvalidOperationException("Moving a document with multiple or unknown directory references requires a selected source parent.")
                End If
                If metadata.Folders IsNot Nothing AndAlso metadata.Folders.Count = 1 Then
                    parent = Await Me.GetNativeFolderDirectoryByIdAsync(metadata.Folders(0), collections(0), cancellationToken).ConfigureAwait(False)
                Else
                    parent = Await Me.LoadNativeCollectionDirectoryAsync(collections(0), cancellationToken).ConfigureAwait(False)
                End If
            End If
            For Each file In Await parent.GetFilesAsync(cancellationToken).ConfigureAwait(False)
                If String.Equals(file.ID, resource.ExtendedInfosFileID, StringComparison.Ordinal) Then Return file
            Next
            Throw New Data.FileNotFoundException(resource.FullName)
        End Function
    End Class
End Namespace
