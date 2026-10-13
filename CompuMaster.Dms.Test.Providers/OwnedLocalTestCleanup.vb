Option Explicit On
Option Strict On

Imports System.IO

Friend NotInheritable Class OwnedLocalTestCleanup
    Private Sub New()
    End Sub

    Friend Shared Sub Cleanup(closeWindow As Action, disposeWindow As Action, directoryPath As String,
                              directoryAdmitted As Boolean, trackedFiles As IEnumerable(Of String))
        Dim failures As New List(Of Exception)
        For Each action In {closeWindow, disposeWindow}
            If action Is Nothing Then Continue For
            Try
                action()
            Catch ex As Exception
                failures.Add(ex)
            End Try
        Next
        If directoryAdmitted Then
            Dim parent = Path.GetFullPath(directoryPath)
            For Each file In trackedFiles
                Try
                    Dim fullPath = Path.GetFullPath(file)
                    If Not String.Equals(Path.GetDirectoryName(fullPath), parent, StringComparison.Ordinal) Then
                        Throw New InvalidOperationException("A tracked test file does not belong to the admitted parent directory.")
                    End If
                    If Global.System.IO.File.Exists(fullPath) Then Global.System.IO.File.Delete(fullPath)
                    If Global.System.IO.File.Exists(fullPath) Then Throw New IOException("An owned test file remains after cleanup.")
                Catch ex As Exception
                    failures.Add(ex)
                End Try
            Next
            Try
                If Directory.Exists(parent) Then Directory.Delete(parent, False)
                If Directory.Exists(parent) Then Throw New IOException("An admitted test directory remains after cleanup.")
            Catch ex As Exception
                failures.Add(ex)
            End Try
        End If
        If failures.Count <> 0 Then Throw New AggregateException("Owned test cleanup failed; all admitted cleanup actions were attempted.", failures)
    End Sub
End Class
