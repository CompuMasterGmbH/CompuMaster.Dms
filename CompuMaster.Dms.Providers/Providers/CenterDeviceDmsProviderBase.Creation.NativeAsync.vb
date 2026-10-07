Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Creates an immediate child through the native asynchronous client.</summary>
        ''' <param name="parent">The selected parent directory.</param>
        ''' <param name="name">The child name.</param>
        ''' <param name="style">The explicit directory kind, or nothing to use the client's existing default.</param>
        ''' <param name="cancellationToken">Cancels admission and active HTTP I/O.</param>
        ''' <returns>A task representing the creation request.</returns>
        Protected Overridable Function CreateNativeDirectoryAsync(parent As CenterDevice.IO.DirectoryInfo, name As String, style As CenterDevice.IO.DirectoryInfo.DirectoryType?, cancellationToken As CancellationToken) As Task
            If style.HasValue Then Return parent.CreateDirectoryAsync(name, style.Value, cancellationToken)
            Return parent.CreateDirectoryAsync(name, cancellationToken)
        End Function

        Private Async Function CreateNativeDirectoryAtPathAsync(remotePath As String, style As CenterDevice.IO.DirectoryInfo.DirectoryType?, operation As String, cancellationToken As CancellationToken) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Dim parent = Await Me.OpenNativeTransferDirectoryAsync(Me.ParentDirectoryPath(remotePath), cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Try
                Await Me.CreateNativeDirectoryAsync(parent, Me.ItemName(remotePath), style, cancellationToken).ConfigureAwait(False)
            Catch ex As CenterDevice.Rest.Exceptions.RestClientException
                If ex.ErrorResponse Is Nothing Then Throw New System.IO.IOException(ProviderStrings.Format("Failed", operation, remotePath), ex)
                Throw New System.IO.IOException(ProviderStrings.Format("Failed", operation, remotePath), New ResponseStatusCodeException(ex.ErrorResponse.Code, ex.ErrorResponse.Message, ex))
            Finally
                parent.ResetDirectoriesCache()
                'A cached collection's known-empty hint can also be stale after an uncertain child mutation.
                If Not parent.IsRootDirectory Then Me.IOClient.RootDirectory.ResetDirectoriesCache()
            End Try
        End Function

        ''' <inheritdoc/>
        Public Overrides Function CreateFolderAsync(remoteDirectoryPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.CreateNativeDirectoryAtPathAsync(remoteDirectoryPath, CenterDevice.IO.DirectoryInfo.DirectoryType.Folder, NameOf(CreateFolder), cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function CreateDirectoryAsync(remoteDirectoryPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.CreateNativeDirectoryAtPathAsync(remoteDirectoryPath, Nothing, NameOf(CreateDirectory), cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function CreateCollectionAsync(remoteCollectionName As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            If Me.ParentDirectoryPath(remoteCollectionName) <> Nothing Then
                Throw New NotSupportedException(ProviderStrings.Format("CollectionsMustBeLocatedInTheRootFolder", remoteCollectionName))
            End If
            Return Me.CreateNativeDirectoryAtPathAsync(remoteCollectionName, CenterDevice.IO.DirectoryInfo.DirectoryType.Collection, NameOf(CreateCollection), cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function CreateFolderAsync(remoteDirectoryPath As String, createParentFolders As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If remoteDirectoryPath Is Nothing Then Throw New ArgumentNullException(NameOf(remoteDirectoryPath))
            cancellationToken.ThrowIfCancellationRequested()
            Dim parentPath = Me.ParentDirectoryPath(remoteDirectoryPath)
            If parentPath <> Nothing AndAlso Not Await Me.RemoteItemExistsAsync(parentPath, cancellationToken).ConfigureAwait(False) Then
                If Not createParentFolders Then Throw New Data.DirectoryNotFoundException(parentPath)
                Await Me.CreateFolderAsync(parentPath, True, cancellationToken).ConfigureAwait(False)
            End If
            Await Me.CreateFolderAsync(remoteDirectoryPath, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>Preserves the existing optional-parent overload's explicit folder creation behavior.</remarks>
        Public Overrides Function CreateDirectoryAsync(remoteDirectoryPath As String, createParentFolders As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Me.CreateFolderAsync(remoteDirectoryPath, createParentFolders, cancellationToken)
        End Function

        Private Async Function EnsureNativeUploadParentAsync(remoteFilePath As String, recursive As Boolean, cancellationToken As CancellationToken) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Dim parentPath = Me.ParentDirectoryPath(remoteFilePath)
            If parentPath <> Nothing AndAlso Not Await Me.RemoteItemExistsAsync(parentPath, cancellationToken).ConfigureAwait(False) Then
                Await Me.CreateFolderAsync(parentPath, recursive, cancellationToken).ConfigureAwait(False)
            End If
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function UploadFileAsync(remoteFilePath As String, localFilePath As String, createDirectoryStructureIfMissing As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If createDirectoryStructureIfMissing Then Await Me.EnsureNativeUploadParentAsync(remoteFilePath, False, cancellationToken).ConfigureAwait(False)
            Await Me.UploadFileAsync(remoteFilePath, localFilePath, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function UploadFileAsync(remoteFilePath As String, binaryData As Func(Of System.IO.Stream), createDirectoryStructureIfMissing As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If createDirectoryStructureIfMissing Then Await Me.EnsureNativeUploadParentAsync(remoteFilePath, False, cancellationToken).ConfigureAwait(False)
            Await Me.UploadFileAsync(remoteFilePath, binaryData, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function UploadFileAsync(remoteFilePath As String, binaryData As Byte(), createDirectoryStructureIfMissing As Boolean, Optional cancellationToken As CancellationToken = Nothing) As Task
            If createDirectoryStructureIfMissing Then Await Me.EnsureNativeUploadParentAsync(remoteFilePath, True, cancellationToken).ConfigureAwait(False)
            Await Me.UploadFileAsync(remoteFilePath, binaryData, cancellationToken).ConfigureAwait(False)
        End Function
    End Class
End Namespace
