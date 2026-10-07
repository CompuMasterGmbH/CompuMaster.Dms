Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data

Namespace Providers
    Partial Public MustInherit Class CenterDeviceDmsProviderBase
        ''' <inheritdoc/>
        Protected Overrides ReadOnly Property SupportsAsynchronousCopyMove As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <inheritdoc/>
        Protected Overrides Async Function ValidateCopyMoveIdentityAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            If remoteSource.ItemType <> DmsResourceItem.ItemTypes.Folder Then Return
            Dim source = Await Me.GetNativeDirectoryForActionAsync(remoteSource, cancellationToken).ConfigureAwait(False)
            Dim path = Me.ParentDirectoryPath(remoteDestinationPath)
            Dim parent As CenterDevice.IO.DirectoryInfo = Nothing
            Do
                Try
                    parent = Await Me.OpenNativeTransferDirectoryAsync(path, cancellationToken).ConfigureAwait(False)
                Catch ex As Data.DirectoryNotFoundException
                    If path = Nothing Then Throw
                End Try
                If parent IsNot Nothing Then Exit Do
                path = Me.ParentDirectoryPath(path)
            Loop
            While parent IsNot Nothing
                If Me.IsSameDirectory(source, parent) Then Throw New ArgumentException(ProviderStrings.GetText("AFolderCannotBeCopiedOrMovedInto"), NameOf(remoteDestinationPath))
                parent = parent.ParentDirectory
            End While
        End Function

        ''' <summary>Copies a selected file through bounded native disk staging.</summary>
        ''' <param name="source">The selected source file.</param>
        ''' <param name="parent">The destination parent.</param>
        ''' <param name="name">The destination name.</param>
        ''' <param name="cancellationToken">Cancels admission and active requests.</param>
        ''' <returns>A task representing the copy.</returns>
        Protected Overridable Function CopyNativeFileAsync(source As CenterDevice.IO.FileInfo, parent As CenterDevice.IO.DirectoryInfo, name As String, cancellationToken As CancellationToken) As Task
            Return parent.AddCopyAsync(source, name, cancellationToken)
        End Function

        ''' <summary>Renames a selected native file.</summary>
        ''' <param name="file">The selected file.</param>
        ''' <param name="name">The new name.</param>
        ''' <param name="cancellationToken">Cancels admission and active requests.</param>
        ''' <returns>A task representing the rename.</returns>
        Protected Overridable Function RenameNativeFileAsync(file As CenterDevice.IO.FileInfo, name As String, cancellationToken As CancellationToken) As Task
            Return file.RenameAsync(name, cancellationToken)
        End Function

        ''' <summary>Moves a selected native file from its explicit parent.</summary>
        ''' <param name="file">The file with its source parent.</param>
        ''' <param name="parent">The destination parent.</param>
        ''' <param name="cancellationToken">Cancels admission and active requests.</param>
        ''' <returns>A task representing the move.</returns>
        Protected Overridable Function MoveNativeFileAsync(file As CenterDevice.IO.FileInfo, parent As CenterDevice.IO.DirectoryInfo, cancellationToken As CancellationToken) As Task
            Return file.MoveAsync(parent, cancellationToken)
        End Function

        ''' <summary>Deletes a selected native file.</summary>
        ''' <param name="file">The selected file.</param>
        ''' <param name="cancellationToken">Cancels admission and active requests.</param>
        ''' <returns>A task representing deletion.</returns>
        Protected Overridable Function DeleteNativeActionFileAsync(file As CenterDevice.IO.FileInfo, cancellationToken As CancellationToken) As Task
            Return file.DeleteAsync(cancellationToken)
        End Function

        ''' <summary>Renames a selected native directory.</summary>
        ''' <param name="directory">The selected directory.</param>
        ''' <param name="name">The new name.</param>
        ''' <param name="cancellationToken">Cancels admission and active requests.</param>
        ''' <returns>A task representing the rename.</returns>
        Protected Overridable Function RenameNativeDirectoryAsync(directory As CenterDevice.IO.DirectoryInfo, name As String, cancellationToken As CancellationToken) As Task
            Return directory.RenameAsync(name, cancellationToken)
        End Function

        ''' <summary>Moves a selected native folder.</summary>
        ''' <param name="directory">The selected folder.</param>
        ''' <param name="parent">The destination parent.</param>
        ''' <param name="cancellationToken">Cancels admission and active requests.</param>
        ''' <returns>A task representing the move.</returns>
        Protected Overridable Function MoveNativeDirectoryAsync(directory As CenterDevice.IO.DirectoryInfo, parent As CenterDevice.IO.DirectoryInfo, cancellationToken As CancellationToken) As Task
            Return directory.MoveAsync(parent, cancellationToken)
        End Function

        ''' <summary>Deletes a selected native directory after its contents have been moved.</summary>
        ''' <param name="directory">The selected directory.</param>
        ''' <param name="cancellationToken">Cancels admission and active requests.</param>
        ''' <returns>A task representing deletion.</returns>
        Protected Overridable Function DeleteNativeActionDirectoryAsync(directory As CenterDevice.IO.DirectoryInfo, cancellationToken As CancellationToken) As Task
            Return directory.DeleteAsync(cancellationToken)
        End Function

        Private Async Function CopyMoveNativeItemAsync(source As DmsResourceItem, destinationPath As String, overwrite As Boolean?, moving As Boolean, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            Dim parent As CenterDevice.IO.DirectoryInfo = Nothing
            Try
                If source.ItemType = DmsResourceItem.ItemTypes.Collection Then
                    If Not moving Then Throw New NotSupportedException(ProviderStrings.GetText("CenterDeviceCollectionsCannotBeCopied"))
                    If Me.ParentDirectoryPath(source.FullName) <> Nothing OrElse Me.ParentDirectoryPath(destinationPath) <> Nothing Then Throw New NotSupportedException(ProviderStrings.GetText("CenterDeviceCollectionsCanOnlyBeRenamedInThe"))
                    If Await Me.ListRemoteItemAsync(destinationPath, ct).ConfigureAwait(False) IsNot Nothing Then Throw New DirectoryAlreadyExistsException(destinationPath)
                    Await Me.RenameNativeDirectoryAsync(Await Me.GetNativeDirectoryForActionAsync(source, ct).ConfigureAwait(False), Me.ItemName(destinationPath), ct).ConfigureAwait(False)
                    Return
                End If
                parent = Await Me.OpenNativeTransferDirectoryAsync(Me.ParentDirectoryPath(destinationPath), ct).ConfigureAwait(False)
                Await Me.ValidateNativeUniqueActionDirectoryAsync(parent, destinationPath, ct).ConfigureAwait(False)
                If parent.IsRootDirectory Then Throw New NotSupportedException(ProviderStrings.GetText("CenterDeviceFilesAndFoldersMustRemainInsideA"))
                Dim name = Me.ItemName(destinationPath)
                If source.ItemType = DmsResourceItem.ItemTypes.File Then
                    Dim file = Await Me.GetNativeFileForActionAsync(source, moving, ct).ConfigureAwait(False)
                    Await Me.CopyMoveNativeFileExactAsync(file, parent, name, source.FullName, destinationPath, overwrite, moving, ct).ConfigureAwait(False)
                ElseIf source.ItemType = DmsResourceItem.ItemTypes.Folder Then
                    Dim directory = Await Me.GetNativeDirectoryForActionAsync(source, ct).ConfigureAwait(False)
                    Dim ancestor = parent
                    While ancestor IsNot Nothing
                        If Me.IsSameDirectory(directory, ancestor) Then Throw New ArgumentException(ProviderStrings.GetText("AFolderCannotBeCopiedOrMovedInto2"), NameOf(destinationPath))
                        ancestor = ancestor.ParentDirectory
                    End While
                    Dim target = Await Me.FindNativeActionDirectoryAsync(parent, name, ct).ConfigureAwait(False)
                    If target IsNot Nothing Then
                        If Me.IsSameDirectory(directory, target) Then Throw New ArgumentException(ProviderStrings.GetText("SourceAndDestinationIdentifyTheSameFolder"), NameOf(destinationPath))
                        If overwrite <> True Then Throw New DirectoryAlreadyExistsException(destinationPath)
                        Await Me.MergeNativeDirectoryAsync(directory, target, moving, source.FullName, destinationPath, ct).ConfigureAwait(False)
                        If moving Then Await Me.DeleteNativeActionDirectoryAsync(directory, ct).ConfigureAwait(False)
                    ElseIf moving Then
                        Await Me.MoveNativeDirectoryExactAsync(directory, parent, name, source.FullName, destinationPath, ct).ConfigureAwait(False)
                    Else
                        Await Me.CopyNativeDirectoryTreeAsync(directory, parent, name, source.FullName, destinationPath, ct).ConfigureAwait(False)
                    End If
                Else
                    Throw New NotSupportedException(ProviderStrings.Format("UnsupportedSourceItemType2", source.ItemType.ToString()))
                End If
            Finally
                If parent IsNot Nothing Then
                    parent.ResetFilesCache()
                    parent.ResetDirectoriesCache()
                End If
                Me.IOClient.RootDirectory.ResetFilesCache()
                Me.IOClient.RootDirectory.ResetDirectoriesCache()
            End Try
        End Function

        Private Async Function FindNativeActionDirectoryAsync(parent As CenterDevice.IO.DirectoryInfo, name As String, ct As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            Dim matches = (Await parent.GetDirectoriesAsync(ct).ConfigureAwait(False)).Where(Function(item) String.Equals(item.Name, name, StringComparison.Ordinal)).ToArray()
            If matches.Length > 1 Then Throw New RemotePathNotUniqueException(name)
            If Await parent.TryGetFileAsync(name, ct).ConfigureAwait(False) IsNot Nothing Then Throw New FileAlreadyExistsException(name)
            Return matches.FirstOrDefault()
        End Function

        Private Async Function UniqueNativeTemporaryNameAsync(parent As CenterDevice.IO.DirectoryInfo, ct As CancellationToken) As Task(Of String)
            Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each file In Await parent.GetFilesAsync(ct).ConfigureAwait(False)
                names.Add(file.FileName)
            Next
            For Each directory In Await parent.GetDirectoriesAsync(ct).ConfigureAwait(False)
                names.Add(directory.Name)
            Next
            Dim candidate As String
            Do
                candidate = ".compumaster-dms-" & Guid.NewGuid().ToString("N")
            Loop While names.Contains(candidate)
            Return candidate
        End Function

        Private Async Function CopyMoveNativeFileExactAsync(source As CenterDevice.IO.FileInfo, parent As CenterDevice.IO.DirectoryInfo, name As String, sourcePath As String, destinationPath As String, overwrite As Boolean?, moving As Boolean, ct As CancellationToken) As Task
            If (Await parent.GetDirectoriesAsync(ct).ConfigureAwait(False)).Any(Function(item) String.Equals(item.Name, name, StringComparison.Ordinal)) Then Throw New DirectoryAlreadyExistsException(destinationPath)
            Dim matches = (Await parent.GetFilesAsync(ct).ConfigureAwait(False)).Where(Function(item) String.Equals(item.FileName, name, StringComparison.Ordinal)).ToArray()
            If matches.Length > 1 Then Throw New RemotePathNotUniqueException(destinationPath)
            Dim existing = matches.FirstOrDefault()
            If existing IsNot Nothing Then RejectSameDocumentAction(source.ID, existing.ID, destinationPath)
            If existing IsNot Nothing AndAlso overwrite <> True Then Throw New FileAlreadyExistsException(destinationPath)
            If existing Is Nothing Then
                If moving Then
                    Await Me.MoveNativeFileToFinalNameAsync(source, parent, name, sourcePath, destinationPath, ct).ConfigureAwait(False)
                Else
                    Await Me.CopyNativeFileAsync(source, parent, name, ct).ConfigureAwait(False)
                End If
                Return
            End If
            Dim backup = Await Me.UniqueNativeTemporaryNameAsync(parent, ct).ConfigureAwait(False)
            Dim staged As CenterDevice.IO.FileInfo = Nothing
            If Not moving Then
                Dim temporaryName = Await Me.UniqueNativeTemporaryNameAsync(parent, ct).ConfigureAwait(False)
                Try
                    Await Me.CopyNativeFileAsync(source, parent, temporaryName, ct).ConfigureAwait(False)
                    parent.ResetFilesCache()
                    staged = Await parent.GetFileAsync(temporaryName, ct).ConfigureAwait(False)
                Catch ex As Exception
                    AddNativeReconciliationContext(ex, "Staged copy or identity lookup", source.ID, parent, temporaryName)
                    Throw
                End Try
            End If
            Dim failure As Exception = Nothing
            Dim backedUp As Boolean
            Try
                Await Me.RenameNativeFileAsync(existing, backup, ct).ConfigureAwait(False)
                backedUp = True
                If moving Then
                    Await Me.MoveNativeFileToFinalNameAsync(source, parent, name, sourcePath, destinationPath, ct).ConfigureAwait(False)
                Else
                    Await Me.RenameNativeFileAsync(staged, name, ct).ConfigureAwait(False)
                End If
            Catch ex As Exception
                failure = ex
            End Try
            If failure IsNot Nothing Then
                Dim errors As New List(Of Exception) From {failure}
                Using cleanup As New CancellationTokenSource(TimeSpan.FromSeconds(30))
                    If backedUp Then Await TryNativeCompensationAsync(Function() Me.RenameNativeFileAsync(existing, name, cleanup.Token), errors).ConfigureAwait(False)
                    If staged IsNot Nothing Then Await TryNativeCompensationAsync(Function() Me.DeleteNativeActionFileAsync(staged, cleanup.Token), errors).ConfigureAwait(False)
                End Using
                Me.ThrowNativeActionFailure(If(moving, "move", "copy"), sourcePath, destinationPath, errors)
            End If
            Try
                Await Me.DeleteNativeActionFileAsync(existing, ct).ConfigureAwait(False)
            Catch ex As Exception
                Throw New FileActionFailedException(If(moving, "move", "copy"), sourcePath, destinationPath, New InvalidOperationException(ProviderStrings.Format("TheOperationSucceededButReplacedDocumentIDRemains", existing.ID, backup), ex))
            End Try
        End Function

        Private Shared Async Function TryNativeCompensationAsync(action As Func(Of Task), errors As List(Of Exception)) As Task
            Try
                Await action().ConfigureAwait(False)
            Catch ex As Exception
                errors.Add(ex)
            End Try
        End Function

        Private Shared Sub AddNativeReconciliationContext(failure As Exception, phase As String, sourceId As String, parent As CenterDevice.IO.DirectoryInfo, temporaryName As String)
            ' These are candidate identities, not proof that a request committed. Do not repeat a write or
            ' delete an item found only by its temporary name when a response or identity lookup failed.
            Const key As String = "CompuMaster.Dms.Reconciliation"
            Dim details = phase & ": source resource ID " & sourceId & "; collection ID " & parent.CollectionID & "; parent folder ID " & If(parent.FolderID, "(collection root)") & "; candidate temporary name " & temporaryName & ". The request outcome is uncertain; reconcile server state before retrying."
            Dim previous = TryCast(failure.Data(key), String)
            failure.Data(key) = If(previous Is Nothing, details, previous & Environment.NewLine & details)
        End Sub

        Private Sub ThrowNativeActionFailure(action As String, sourcePath As String, destinationPath As String, errors As List(Of Exception))
            If errors.Count = 1 AndAlso TypeOf errors(0) Is OperationCanceledException Then System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors(0)).Throw()
            Throw New FileActionFailedException(action, sourcePath, destinationPath, New AggregateException(ProviderStrings.GetText("TheOperationFailedCompensationOfConfirmedChangesWas"), errors))
        End Sub

        Private Async Function MoveNativeFileToFinalNameAsync(file As CenterDevice.IO.FileInfo, parent As CenterDevice.IO.DirectoryInfo, name As String, sourcePath As String, destinationPath As String, ct As CancellationToken) As Task
            Dim originalName = file.FileName
            Dim originalParent = file.ParentDirectory
            If Me.IsSameDirectory(originalParent, parent) Then
                Await Me.RenameNativeFileAsync(file, name, ct).ConfigureAwait(False)
                Return
            ElseIf originalName = name Then
                Await Me.MoveNativeFileAsync(file, parent, ct).ConfigureAwait(False)
                Return
            End If
            Dim temporaryName = Await Me.UniqueNativeTemporaryNameAsync(originalParent, ct).ConfigureAwait(False)
            Try
                Await Me.RenameNativeFileAsync(file, temporaryName, ct).ConfigureAwait(False)
            Catch ex As Exception
                AddNativeReconciliationContext(ex, "Initial temporary file rename", file.ID, originalParent, temporaryName)
                Throw
            End Try
            Dim moved As Boolean
            Dim failure As Exception = Nothing
            Try
                Await Me.MoveNativeFileAsync(file, parent, ct).ConfigureAwait(False)
                moved = True
                Await Me.RenameNativeFileAsync(file, name, ct).ConfigureAwait(False)
            Catch ex As Exception
                failure = ex
            End Try
            If failure Is Nothing Then Return
            Dim errors As New List(Of Exception) From {failure}
            Using cleanup As New CancellationTokenSource(TimeSpan.FromSeconds(30))
                If moved Then Await TryNativeCompensationAsync(Function() Me.MoveNativeFileAsync(file, originalParent, cleanup.Token), errors).ConfigureAwait(False)
                Await TryNativeCompensationAsync(Function() Me.RenameNativeFileAsync(file, originalName, cleanup.Token), errors).ConfigureAwait(False)
            End Using
            Me.ThrowNativeActionFailure("move", sourcePath, destinationPath, errors)
        End Function

        Private Async Function MoveNativeDirectoryExactAsync(directory As CenterDevice.IO.DirectoryInfo, parent As CenterDevice.IO.DirectoryInfo, name As String, sourcePath As String, destinationPath As String, ct As CancellationToken) As Task
            Dim originalName = directory.Name
            Dim originalParent = directory.ParentDirectory
            If Me.IsSameDirectory(originalParent, parent) Then
                Await Me.RenameNativeDirectoryAsync(directory, name, ct).ConfigureAwait(False)
                Return
            ElseIf originalName = name Then
                Await Me.MoveNativeDirectoryAsync(directory, parent, ct).ConfigureAwait(False)
                Return
            End If
            Dim temporaryName = Await Me.UniqueNativeTemporaryNameAsync(originalParent, ct).ConfigureAwait(False)
            Try
                Await Me.RenameNativeDirectoryAsync(directory, temporaryName, ct).ConfigureAwait(False)
            Catch ex As Exception
                AddNativeReconciliationContext(ex, "Initial temporary folder rename", directory.FolderID, originalParent, temporaryName)
                Throw
            End Try
            Dim moved As Boolean
            Dim failure As Exception = Nothing
            Try
                Await Me.MoveNativeDirectoryAsync(directory, parent, ct).ConfigureAwait(False)
                moved = True
                Await Me.RenameNativeDirectoryAsync(directory, name, ct).ConfigureAwait(False)
            Catch ex As Exception
                failure = ex
            End Try
            If failure Is Nothing Then Return
            Dim errors As New List(Of Exception) From {failure}
            Using cleanup As New CancellationTokenSource(TimeSpan.FromSeconds(30))
                If moved Then Await TryNativeCompensationAsync(Function() Me.MoveNativeDirectoryAsync(directory, originalParent, cleanup.Token), errors).ConfigureAwait(False)
                Await TryNativeCompensationAsync(Function() Me.RenameNativeDirectoryAsync(directory, originalName, cleanup.Token), errors).ConfigureAwait(False)
            End Using
            Me.ThrowNativeActionFailure("move", sourcePath, destinationPath, errors)
        End Function

        Private Async Function ValidateNativeChildNamesAsync(directory As CenterDevice.IO.DirectoryInfo, path As String, ct As CancellationToken) As Task
            Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each file In Await directory.GetFilesAsync(ct).ConfigureAwait(False)
                If Not names.Add(file.FileName) Then Throw New RemotePathNotUniqueException(Me.CombinePath(path, file.FileName))
            Next
            For Each child In Await directory.GetDirectoriesAsync(ct).ConfigureAwait(False)
                If Not names.Add(child.Name) Then Throw New RemotePathNotUniqueException(Me.CombinePath(path, child.Name))
            Next
        End Function

        Private Async Function CopyNativeDirectoryTreeAsync(source As CenterDevice.IO.DirectoryInfo, parent As CenterDevice.IO.DirectoryInfo, name As String, sourcePath As String, destinationPath As String, ct As CancellationToken) As Task
            Await Me.ValidateNativeChildNamesAsync(source, sourcePath, ct).ConfigureAwait(False)
            Await Me.CreateNativeDirectoryAsync(parent, name, Nothing, ct).ConfigureAwait(False)
            parent.ResetDirectoriesCache()
            Await Me.MergeNativeDirectoryAsync(source, Await parent.GetDirectoryAsync(name, ct).ConfigureAwait(False), False, sourcePath, destinationPath, ct).ConfigureAwait(False)
        End Function

        Private Async Function MergeNativeDirectoryAsync(source As CenterDevice.IO.DirectoryInfo, destination As CenterDevice.IO.DirectoryInfo, moving As Boolean, sourcePath As String, destinationPath As String, ct As CancellationToken) As Task
            Await Me.ValidateNativeChildNamesAsync(source, sourcePath, ct).ConfigureAwait(False)
            Await Me.ValidateNativeChildNamesAsync(destination, destinationPath, ct).ConfigureAwait(False)
            Try
                For Each file In Await source.GetFilesAsync(ct).ConfigureAwait(False)
                    Await Me.CopyMoveNativeFileExactAsync(file, destination, file.FileName, Me.CombinePath(sourcePath, file.FileName), Me.CombinePath(destinationPath, file.FileName), True, moving, ct).ConfigureAwait(False)
                Next
                For Each child In Await source.GetDirectoriesAsync(ct).ConfigureAwait(False)
                    Dim target = Await Me.FindNativeActionDirectoryAsync(destination, child.Name, ct).ConfigureAwait(False)
                    Dim childSource = Me.CombinePath(sourcePath, child.Name)
                    Dim childDestination = Me.CombinePath(destinationPath, child.Name)
                    If target IsNot Nothing Then
                        If Me.IsSameDirectory(child, target) Then Throw New InvalidOperationException(ProviderStrings.GetText("ADirectoryMergeCannotConsumeTheSameFolder"))
                        Await Me.MergeNativeDirectoryAsync(child, target, moving, childSource, childDestination, ct).ConfigureAwait(False)
                        If moving Then Await Me.DeleteNativeActionDirectoryAsync(child, ct).ConfigureAwait(False)
                    ElseIf moving Then
                        Await Me.MoveNativeDirectoryExactAsync(child, destination, child.Name, childSource, childDestination, ct).ConfigureAwait(False)
                    Else
                        Await Me.CopyNativeDirectoryTreeAsync(child, destination, child.Name, childSource, childDestination, ct).ConfigureAwait(False)
                    End If
                Next
            Finally
                source.ResetFilesCache()
                source.ResetDirectoriesCache()
                destination.ResetFilesCache()
                destination.ResetDirectoriesCache()
            End Try
        End Function

        ''' <inheritdoc/>
        Protected Overrides Function MoveItemAsync(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            Return Me.CopyMoveNativeItemAsync(remoteSource, remoteDestinationPath, allowOverwrite, True, cancellationToken)
        End Function
    End Class
End Namespace
