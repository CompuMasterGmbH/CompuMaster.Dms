Imports System.IO

Friend NotInheritable Class FileVersionDownloadDestination
    Friend Shared Function Validate(destination As String, requiredParent As String) As String
        Dim full = Path.GetFullPath(destination)
        If Not String.IsNullOrEmpty(requiredParent) Then
            Dim parent = Path.GetFullPath(requiredParent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            If Not full.StartsWith(parent & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then
                Throw New IOException(UiStrings.Format("OutsideRequiredFolder", requiredParent))
            End If
        End If
        RemoteDownloadPlan.ValidateLocalPath(full, False)
        Return full
    End Function

    Friend Shared Sub Save(destination As String, requiredParent As String, replace As Boolean,
                          download As Action(Of Stream), lastModificationUtc As DateTime?)
        Dim full = Validate(destination, requiredParent)
        Dim temporary = Path.Combine(Path.GetDirectoryName(full), ".cm-dms-version-" & Guid.NewGuid().ToString("N") & ".tmp")
        Dim failure As Exception = Nothing
        Try
            Using stream As New FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                download(stream)
            End Using
            If lastModificationUtc.HasValue Then File.SetLastWriteTimeUtc(temporary, lastModificationUtc.Value.ToUniversalTime())
            Validate(full, requiredParent)
            If File.Exists(full) Then
                If Not replace Then Throw New IOException(UiStrings.Format("DownloadDestinationChanged", full))
                File.Replace(temporary, full, Nothing)
            Else
                File.Move(temporary, full)
            End If
        Catch ex As Exception
            failure = ex
            Throw
        Finally
            Try
                If File.Exists(temporary) Then File.Delete(temporary)
            Catch cleanup As Exception
                If failure Is Nothing Then Throw
                failure.Data("DownloadCleanupFailure") = cleanup.ToString()
            End Try
        End Try
    End Sub
End Class
