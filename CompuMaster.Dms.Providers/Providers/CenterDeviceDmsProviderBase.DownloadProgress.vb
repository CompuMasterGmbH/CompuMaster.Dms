Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
#If Not NATIVE_ASYNC Then
        ''' <inheritdoc/>
        ''' <remarks>Reports selected-resource bytes while copying the SDK stream. The synchronous SDK fallback cannot interrupt an active remote request. Existing partial-destination and timestamp behavior is preserved.</remarks>
        Public Overrides Function DownloadFileWithProgressAsync(remoteFile As DmsResourceItem, localFilePath As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New ArgumentException(ProviderStrings.GetText("TheRemoteResourceMustBeAFile"), NameOf(remoteFile))
            Return Me.RunSynchronousFallbackAsync(
                Sub()
                    Dim file As CenterDevice.IO.FileInfo
                    If String.IsNullOrEmpty(remoteFile.ExtendedInfosFileID) Then
                        Dim parent = Me.GetDirectoryItem(Me.ParentDirectoryPath(remoteFile.FullName), RessourceNotFoundHandling.ThrowNotFoundExceptionIfItemOrParentDirectoryIsNotFound)
                        file = parent.GetFile(Me.ItemName(remoteFile.FullName))
                    Else
                        file = New CenterDevice.IO.FileInfo(Me.IOClient, Nothing, Me.IOClient.ApiClient.Document.GetDocumentMetadata(Me.IOClient.CurrentAuthenticationContextUserID, remoteFile.ExtendedInfosFileID))
                    End If
                    Dim bytes As Long
                    Using output As New FileStream(localFilePath, FileMode.Create), input = file.Download()
                        bytes = DownloadProgress.CopyAsync(input, output, file.Size, progress, CancellationToken.None).GetAwaiter().GetResult()
                        output.Flush(True)
                    End Using
                    If file.ModificationDate.HasValue Then System.IO.File.SetLastWriteTimeUtc(localFilePath, file.ModificationDate.Value)
                    If remoteFile.LastModificationOnLocalTime.HasValue AndAlso remoteFile.LastModificationOnLocalTime.Value <> Nothing Then System.IO.File.SetLastWriteTime(localFilePath, remoteFile.LastModificationOnLocalTime.Value)
                    progress?.Report(New DmsTransferProgress(bytes, file.Size, DmsTransferPhase.Completed))
                End Sub, cancellationToken)
        End Function
#End If
    End Class
End Namespace
