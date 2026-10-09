Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private Async Function DownloadWithDialogAsync(files As DmsResourceItem(), destinations As String()) As Task
        If files.Length = 0 Then Return
        Dim names = files.Select(Function(file) file.Name).ToArray()
        Using dialog As New UploadProgressDialog(names, True, True, Me.Icon)
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

    Friend Shared Async Function DownloadBatchWithProgressAsync(provider As BaseDmsProvider, files As DmsResourceItem(), destinations As String(), observer As IProgress(Of UploadBatchSnapshot), cancellationToken As CancellationToken) As Task
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
                Await provider.DownloadFileWithProgressAsync(files(current), destinations(current), progress, cancellationToken)
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
End Class
