Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <inheritdoc/>
        ''' <remarks>Uploads a new version of the selected document identifier. It never selects a replacement target by file name. Source-parent membership is revalidated; server-side changes after validation are not transactional.</remarks>
        Public Overrides Async Function ReplaceFileWithProgressAsync(remoteFile As DmsResourceItem, localFilePath As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File OrElse String.IsNullOrEmpty(remoteFile.ExtendedInfosFileID) Then Throw New ArgumentException("The replacement destination must identify an existing document.", NameOf(remoteFile))
            cancellationToken.ThrowIfCancellationRequested()
#If NATIVE_ASYNC Then
            Dim file = Await Me.GetNativeFileForActionAsync(remoteFile, True, cancellationToken).ConfigureAwait(False)
            If file.ID <> remoteFile.ExtendedInfosFileID OrElse file.FileName <> remoteFile.Name Then Throw New InvalidOperationException("The replacement document identity or name changed.")
            Dim modified As DateTime
            Using input = System.IO.File.OpenRead(localFilePath)
                modified = System.IO.File.GetLastWriteTimeUtc(localFilePath)
            End Using
            Dim tracked As UploadProgressStream = Nothing
            Try
                Await Me.UploadNativeFileAsync(file.ParentDirectory, file, file.FileName,
                    Function()
                        tracked = New UploadProgressStream(New FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous Or FileOptions.SequentialScan), progress)
                        Return CType(tracked, Stream)
                    End Function, modified, cancellationToken).ConfigureAwait(False)
            Finally
                file.ParentDirectory.ResetFilesCache()
            End Try
            progress?.Report(If(tracked Is Nothing, New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Completed), tracked.Snapshot(DmsTransferPhase.Completed)))
#Else
            progress?.Report(New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Transferring))
            Await Me.RunSynchronousFallbackAsync(Sub()
                                                    Dim file = Me.GetFileItem(remoteFile)
                                                    If file.FileName <> remoteFile.Name Then Throw New InvalidOperationException("The replacement document name changed.")
                                                    Try
                                                        file.UploadNewVersion(localFilePath, System.IO.File.GetLastWriteTimeUtc(localFilePath))
                                                    Finally
                                                        file.ParentDirectory.ResetFilesCache()
                                                    End Try
                                                End Sub, cancellationToken).ConfigureAwait(False)
            progress?.Report(New DmsTransferProgress(Nothing, Nothing, DmsTransferPhase.Completed))
#End If
        End Function
    End Class
End Namespace
