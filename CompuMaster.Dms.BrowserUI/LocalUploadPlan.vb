Imports System.IO
Imports System.Threading

Friend NotInheritable Class LocalUploadPlan
    Friend ReadOnly Files As String()
    Friend ReadOnly RelativePaths As String()
    Friend ReadOnly Directories As String()

    Private Sub New(files As List(Of String), relativePaths As List(Of String), directories As List(Of String))
        Me.Files = files.ToArray()
        Me.RelativePaths = relativePaths.ToArray()
        Me.Directories = directories.ToArray()
    End Sub

    Friend Shared Function Build(selection As String(), cancellationToken As CancellationToken, Optional localParent As String = Nothing) As LocalUploadPlan
        Dim roots = selection.Select(Function(localPath) NormalizeSourcePath(localPath)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        Dim files As New List(Of String)
        Dim paths As New List(Of String)
        Dim directories As New List(Of String)
        Dim destinations As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each root In roots
            cancellationToken.ThrowIfCancellationRequested()
            If localParent IsNot Nothing Then
                Dim parent = Path.GetFullPath(localParent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                If Not root.Equals(parent, StringComparison.OrdinalIgnoreCase) AndAlso Not root.StartsWith(parent & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then Throw New UnauthorizedAccessException(UiStrings.Format("OutsideRequiredFolder", localParent))
            End If
            If roots.Any(Function(other) Not other.Equals(root, StringComparison.OrdinalIgnoreCase) AndAlso Directory.Exists(other) AndAlso root.StartsWith(other.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) Then Continue For
            AddPath(root, Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).TrimEnd(":"c), files, paths, directories, destinations, cancellationToken)
        Next
        Return New LocalUploadPlan(files, paths, directories)
    End Function

    Friend Shared Function NormalizeSourcePath(source As String) As String
        Dim full = Path.GetFullPath(source)
        Return If(full.Length = Path.GetPathRoot(full).Length, full, full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
    End Function

    Private Shared Sub AddPath(source As String, relative As String, files As List(Of String), paths As List(Of String), directories As List(Of String), destinations As Dictionary(Of String, String), token As CancellationToken)
        token.ThrowIfCancellationRequested()
        Dim attributes = File.GetAttributes(source)
        If (attributes And FileAttributes.ReparsePoint) <> 0 Then Throw New IOException(UiStrings.Format("UploadLinkedPathUnsupported", source))
        If destinations.ContainsKey(relative) Then Throw New IOException(UiStrings.Format("UploadSelectionCollision", relative))
        destinations.Add(relative, source)
        If (attributes And FileAttributes.Directory) <> 0 Then
            directories.Add(relative)
            For Each child In Directory.EnumerateFileSystemEntries(source).OrderBy(Function(path) path, StringComparer.OrdinalIgnoreCase)
                AddPath(child, relative & "/" & Path.GetFileName(child), files, paths, directories, destinations, token)
            Next
        Else
            files.Add(source)
            paths.Add(relative)
        End If
    End Sub
End Class
