Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Friend NotInheritable Class RemoteDownloadPlan
    Friend ReadOnly TargetRoot As String
    Friend ReadOnly Directories As New List(Of String)
    Friend ReadOnly Files As New List(Of DmsResourceItem)
    Friend ReadOnly Destinations As New List(Of String)
    Private ReadOnly Paths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly Ancestors As New HashSet(Of String)(StringComparer.Ordinal)

    Private Sub New(targetRoot As String)
        Me.TargetRoot = targetRoot
    End Sub

    Friend Shared Async Function BuildAsync(provider As BaseDmsProvider, folder As DmsResourceItem, localParent As String, token As CancellationToken) As Task(Of RemoteDownloadPlan)
        If provider Is Nothing Then Throw New ArgumentNullException(NameOf(provider))
        If folder Is Nothing OrElse folder.ItemType = DmsResourceItem.ItemTypes.File Then Throw New ArgumentException(NameOf(folder))
        token.ThrowIfCancellationRequested()
        Dim name = If(folder.ItemType = DmsResourceItem.ItemTypes.Root, "DMS", folder.Name)
        ValidateName(name)
        Dim root = Path.Combine(Path.GetFullPath(localParent), name)
        Dim plan As New RemoteDownloadPlan(root)
        Await plan.AddDirectoryAsync(provider, folder, root, 0, token).ConfigureAwait(False)
        Return plan
    End Function

    Private Async Function AddDirectoryAsync(provider As BaseDmsProvider, folder As DmsResourceItem, localPath As String, depth As Integer, token As CancellationToken) As Task
        token.ThrowIfCancellationRequested()
        If depth > 256 OrElse folder.FullName Is Nothing Then Throw New IOException(UiStrings.GetText("DownloadHierarchyInvalid"))
        Dim key = If(String.IsNullOrEmpty(folder.ExtendedInfosFolderID), "path:" & folder.FullName, "folder:" & folder.ExtendedInfosFolderID)
        If Not Ancestors.Add(key) Then Throw New IOException(UiStrings.GetText("DownloadHierarchyInvalid"))
        Try
            AddDestination(localPath, True)
            Directories.Add(localPath)
            Dim children = Await provider.ListDirectoryEntriesAsync(folder.FullName, token).ConfigureAwait(False)
            Dim files = Await provider.ListFileEntriesAsync(folder.FullName, token).ConfigureAwait(False)
            For Each file In files
                token.ThrowIfCancellationRequested()
                If file.ItemType <> DmsResourceItem.ItemTypes.File OrElse file.FullName Is Nothing Then Throw New IOException(UiStrings.GetText("DownloadHierarchyInvalid"))
                ValidateName(file.Name)
                Dim destination = Path.Combine(localPath, file.Name)
                AddDestination(destination, False)
                Me.Files.Add(file)
                Me.Destinations.Add(destination)
            Next
            For Each child In children
                If child.ItemType <> DmsResourceItem.ItemTypes.Folder AndAlso child.ItemType <> DmsResourceItem.ItemTypes.Collection Then Throw New IOException(UiStrings.GetText("DownloadHierarchyInvalid"))
                ValidateName(child.Name)
                Await AddDirectoryAsync(provider, child, Path.Combine(localPath, child.Name), depth + 1, token).ConfigureAwait(False)
            Next
        Finally
            Ancestors.Remove(key)
        End Try
    End Function

    Private Sub AddDestination(destination As String, directory As Boolean)
        Dim full = Path.GetFullPath(destination)
        If Not full.Equals(TargetRoot, StringComparison.OrdinalIgnoreCase) AndAlso Not full.StartsWith(TargetRoot & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then Throw New IOException(UiStrings.GetText("DownloadHierarchyInvalid"))
        If Not Paths.Add(full) Then Throw New IOException(UiStrings.Format("DownloadDestinationCollision", full))
        ValidateLocalPath(full, directory)
    End Sub

    Friend Shared Sub ValidateLocalPath(fullPath As String, directory As Boolean)
        Dim current = Path.GetFullPath(fullPath)
        Dim leaf = True
        While current IsNot Nothing
            Dim attributes As FileAttributes?
            Try
                attributes = File.GetAttributes(current)
            Catch ex As System.IO.FileNotFoundException
                attributes = Nothing
            Catch ex As System.IO.DirectoryNotFoundException
                attributes = Nothing
            End Try
            If attributes.HasValue Then
                If (attributes.Value And FileAttributes.ReparsePoint) <> 0 Then Throw New IOException(UiStrings.Format("DownloadLinkedPathUnsupported", current))
                Dim isDirectory = (attributes.Value And FileAttributes.Directory) <> 0
                If isDirectory <> If(leaf, directory, True) Then Throw New IOException(UiStrings.Format("DownloadDestinationCollision", current))
            End If
            leaf = False
            current = Path.GetDirectoryName(current)
        End While
    End Sub

    Private Shared Sub ValidateName(name As String)
        If String.IsNullOrWhiteSpace(name) OrElse name = "." OrElse name = ".." OrElse name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse
            name.EndsWith(".", StringComparison.Ordinal) OrElse name.EndsWith(" ", StringComparison.Ordinal) OrElse
            System.Text.RegularExpressions.Regex.IsMatch(name, "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase) Then
            Throw New IOException(UiStrings.Format("DownloadInvalidName", name))
        End If
    End Sub
End Class
