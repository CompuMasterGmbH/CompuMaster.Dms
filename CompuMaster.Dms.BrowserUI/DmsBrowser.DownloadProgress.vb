Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private Async Function DownloadWithDialogAsync(files As DmsResourceItem(), destinations As String()) As Task
        If files.Length = 0 Then Return
        Dim names = files.Select(Function(file) file.Name).ToArray()
        Using dialog = CreateTransferDialog(names, True)
            dialog.Show(Me)
            Try
                Await DownloadBatchWithProgressAsync(Me.DmsProvider, files, destinations, dialog, dialog.CancellationToken)
            Catch ex As Exception
                dialog.SetFailure(ex)
                dialog.Finish()
                dialog.ShowDialog(Me)
                'Failure has been shown once in the transfer dialog.
                Return
            End Try
            dialog.Finish()
        End Using
        System.Windows.Forms.MessageBox.Show(Me, UiStrings.GetText("DownloadSuccessful"), Me.Text, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information)
    End Function

    Friend Shared Async Function DownloadBatchWithProgressAsync(provider As BaseDmsProvider, files As DmsResourceItem(), destinations As String(), observer As IProgress(Of UploadBatchSnapshot), cancellationToken As CancellationToken, Optional replacementPermissions As Boolean() = Nothing) As Task
        Dim names = files.Select(Function(file) file.Name).ToArray()
        Dim states = Enumerable.Repeat(UploadFileState.Waiting, files.Length).ToArray()
        Dim counters(files.Length - 1) As DmsTransferProgress
        Dim errors(files.Length - 1) As Exception
        For index As Integer = 0 To files.Length - 1
            Dim current = index
            cancellationToken.ThrowIfCancellationRequested()
            states(current) = UploadFileState.Transferring
            observer.Report(New UploadBatchSnapshot(names, states, counters, current, errors))
            Dim active = True
            Dim progress As New UploadProgressRecorder(Sub(value)
                                                         If Not active Then Return
                                                         counters(current) = value
                                                         states(current) = If(value.Phase = DmsTransferPhase.Transferring, UploadFileState.Transferring, UploadFileState.Finalizing)
                                                         observer.Report(New UploadBatchSnapshot(names, states, counters, current, errors))
                                                     End Sub)
            Try
                If replacementPermissions Is Nothing Then
                    Await provider.DownloadFileWithProgressAsync(files(current), destinations(current), progress, cancellationToken)
                Else
                    Await DownloadFolderFileAsync(provider, files(current), destinations(current), replacementPermissions(current), progress, cancellationToken)
                End If
                active = False
                counters(current) = progress.Latest
                states(current) = UploadFileState.Completed
            Catch ex As Exception
                active = False
                counters(current) = progress.Latest
                states(current) = If(TypeOf ex Is OperationCanceledException, UploadFileState.Cancelled, UploadFileState.Failed)
                errors(current) = ex
                observer.Report(New UploadBatchSnapshot(names, states, counters, current, errors))
                Throw
            End Try
            observer.Report(New UploadBatchSnapshot(names, states, counters, current, errors))
        Next
    End Function

    Private Shared Async Function DownloadFolderFileAsync(provider As BaseDmsProvider, file As DmsResourceItem, destination As String, replace As Boolean, progress As IProgress(Of DmsTransferProgress), cancellationToken As CancellationToken) As Task
        RemoteDownloadPlan.ValidateLocalPath(destination, False)
        Dim temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination), ".cm-dms-download-" & Guid.NewGuid().ToString("N") & ".tmp")
        Using reserve As New System.IO.FileStream(temporary, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None)
        End Using
        Dim failure As Exception = Nothing
        Try
            Await provider.DownloadFileWithProgressAsync(file, temporary, progress, cancellationToken)
            cancellationToken.ThrowIfCancellationRequested()
            RemoteDownloadPlan.ValidateLocalPath(destination, False)
            If System.IO.File.Exists(destination) Then
                If Not replace Then Throw New System.IO.IOException(UiStrings.Format("DownloadDestinationChanged", destination))
                System.IO.File.Replace(temporary, destination, Nothing)
            Else
                System.IO.File.Move(temporary, destination)
            End If
        Catch ex As Exception
            failure = ex
            Throw
        Finally
            Try
                If System.IO.File.Exists(temporary) Then System.IO.File.Delete(temporary)
            Catch cleanup As Exception
                If failure Is Nothing Then Throw
                failure.Data("DownloadCleanupFailure") = cleanup.ToString()
            End Try
        End Try
    End Function
End Class
