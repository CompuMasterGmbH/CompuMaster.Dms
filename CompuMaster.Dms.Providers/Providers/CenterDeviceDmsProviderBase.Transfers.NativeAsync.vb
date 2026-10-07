Option Explicit On
Option Strict On

Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <summary>Opens a parent directory for native asynchronous transfers.</summary>
        ''' <param name="remoteFolderPath">The remote parent path.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The selected parent directory.</returns>
        ''' <exception cref="Data.DirectoryNotFoundException">The parent path does not exist.</exception>
        Protected Overridable Async Function OpenNativeTransferDirectoryAsync(remoteFolderPath As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            Try
                Return Await Me.IOClient.RootDirectory.OpenDirectoryPathAsync(remoteFolderPath, cancellationToken).ConfigureAwait(False)
            Catch ex As CenterDevice.Model.Exceptions.DirectoryNotFoundException
                Throw New Data.DirectoryNotFoundException(remoteFolderPath, ex)
            End Try
        End Function

        ''' <summary>Retrieves the selected file for a native path-based download.</summary>
        ''' <param name="remoteFilePath">The remote file path.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The file selected by the existing path lookup rules.</returns>
        Protected Overridable Async Function GetNativeDownloadFileAsync(remoteFilePath As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.FileInfo)
            Dim parent = Await Me.OpenNativeTransferDirectoryAsync(Me.ParentDirectoryPath(remoteFilePath), cancellationToken).ConfigureAwait(False)
            Return Await parent.GetFileAsync(Me.ItemName(remoteFilePath), cancellationToken).ConfigureAwait(False)
        End Function

        ''' <summary>Retrieves a file for a native identifier-based download.</summary>
        ''' <param name="id">The selected file identifier.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>The identified file without resolving an arbitrary parent path.</returns>
        Protected Overridable Async Function GetNativeDownloadFileByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of CenterDevice.IO.FileInfo)
            cancellationToken.ThrowIfCancellationRequested()
            Dim metadata = Await Me.LoadNativeFileByIdAsync(id, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            If metadata Is Nothing Then Throw New Data.FileNotFoundException(id)
            If Not String.Equals(metadata.Id, id, StringComparison.Ordinal) Then Throw New InvalidOperationException("File metadata does not match the requested identifier.")
            Return New CenterDevice.IO.FileInfo(Me.IOClient, Nothing, metadata)
        End Function

        ''' <summary>Uploads a new file or a new version using native asynchronous I/O.</summary>
        ''' <param name="parent">The selected destination directory.</param>
        ''' <param name="existingFile">The existing version target, or nothing to create a file.</param>
        ''' <param name="fileName">The new file name.</param>
        ''' <param name="binaryData">A factory providing a fresh readable stream at its beginning with an available length. The client owns each returned stream.</param>
        ''' <param name="cancellationToken">Cancels request admission and active HTTP I/O.</param>
        ''' <returns>A task representing upload completion.</returns>
        Protected Overridable Async Function UploadNativeFileAsync(parent As CenterDevice.IO.DirectoryInfo, existingFile As CenterDevice.IO.FileInfo, fileName As String, binaryData As Func(Of Stream), cancellationToken As CancellationToken) As Task
            If existingFile IsNot Nothing Then
                Await existingFile.UploadNewVersionAsync(binaryData, cancellationToken).ConfigureAwait(False)
            Else
                Await parent.UploadAndCreateNewFileAsync(binaryData, fileName, cancellationToken).ConfigureAwait(False)
            End If
        End Function

        ''' <inheritdoc/>
        ''' <remarks>Uses bounded streaming I/O and can cancel active requests. Existing path-based version selection is preserved.</remarks>
        Public Overrides Function UploadFileAsync(remoteFilePath As String, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            If localFilePath Is Nothing Then Throw New ArgumentNullException(NameOf(localFilePath))
            Return Me.UploadFileAsync(remoteFilePath, Function() New FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous Or FileOptions.SequentialScan), cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Function UploadFileAsync(remoteFilePath As String, binaryData As Byte(), Optional cancellationToken As CancellationToken = Nothing) As Task
            If binaryData Is Nothing Then Throw New ArgumentNullException(NameOf(binaryData))
            Return Me.UploadFileAsync(remoteFilePath, Function() New MemoryStream(binaryData, False), cancellationToken)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>The client owns each factory stream. An uncertain upload failure invalidates the file cache and is not automatically replayed.</remarks>
        Public Overrides Async Function UploadFileAsync(remoteFilePath As String, binaryData As Func(Of Stream), Optional cancellationToken As CancellationToken = Nothing) As Task
            If binaryData Is Nothing Then Throw New ArgumentNullException(NameOf(binaryData))
            cancellationToken.ThrowIfCancellationRequested()
            Dim parent = Await Me.OpenNativeTransferDirectoryAsync(Me.ParentDirectoryPath(remoteFilePath), cancellationToken).ConfigureAwait(False)
            Dim fileName = Me.ItemName(remoteFilePath)
            Dim existingFile = Await parent.TryGetFileAsync(fileName, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Try
                Await Me.UploadNativeFileAsync(parent, existingFile, fileName, binaryData, cancellationToken).ConfigureAwait(False)
            Finally
                parent.ResetFilesCache()
            End Try
        End Function

        Private Shared Async Function DownloadNativeFileToDiskAsync(file As CenterDevice.IO.FileInfo, localFilePath As String, lastModificationDateOnLocalTime As DateTime?, cancellationToken As CancellationToken) As Task
            Await file.DownloadAsync(localFilePath, 0, cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            If lastModificationDateOnLocalTime.HasValue AndAlso lastModificationDateOnLocalTime.Value <> Nothing Then
                System.IO.File.SetLastWriteTime(localFilePath, lastModificationDateOnLocalTime.Value)
            End If
        End Function

        ''' <inheritdoc/>
        ''' <remarks>Streams to disk with bounded buffering. A failure or cancellation can leave a partial destination, matching the synchronous overwrite contract; the requested timestamp is applied only after success.</remarks>
        Public Overrides Async Function DownloadFileAsync(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?, Optional cancellationToken As CancellationToken = Nothing) As Task
            cancellationToken.ThrowIfCancellationRequested()
            Dim file = Await Me.GetNativeDownloadFileAsync(remoteFilePath, cancellationToken).ConfigureAwait(False)
            Await DownloadNativeFileToDiskAsync(file, localFilePath, lastModificationDateOnLocalTime, cancellationToken).ConfigureAwait(False)
        End Function

        ''' <inheritdoc/>
        ''' <remarks>A supplied file identifier is preserved even for duplicate names. Streaming, cancellation, and partial-destination behavior match the path-based native download.</remarks>
        Public Overrides Async Function DownloadFileAsync(remoteFile As DmsResourceItem, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New ArgumentException("The remote resource must be a file.", NameOf(remoteFile))
            cancellationToken.ThrowIfCancellationRequested()
            Dim file As CenterDevice.IO.FileInfo
            If String.IsNullOrEmpty(remoteFile.ExtendedInfosFileID) Then
                file = Await Me.GetNativeDownloadFileAsync(remoteFile.FullName, cancellationToken).ConfigureAwait(False)
            Else
                file = Await Me.GetNativeDownloadFileByIdAsync(remoteFile.ExtendedInfosFileID, cancellationToken).ConfigureAwait(False)
            End If
            Await DownloadNativeFileToDiskAsync(file, localFilePath, remoteFile.LastModificationOnLocalTime, cancellationToken).ConfigureAwait(False)
        End Function
    End Class
End Namespace
