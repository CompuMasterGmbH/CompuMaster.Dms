Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data

Partial Public Class DmsBrowser
    Private ReadOnly DownloadFolderButton As New ToolStripButton With {.Name = "DownloadFolder", .Text = UiStrings.GetText("ActionDownloadFolder")}
    Private ReadOnly DownloadFolderContext As New ToolStripButton With {.Name = "DownloadFolderContext", .Text = UiStrings.GetText("ActionDownloadFolder")}

    Private Sub InitializeFolderDownloads()
        DownloadFolderButton.Image = ToolStripButtonDownloadFile.Image
        DownloadFolderContext.Image = ToolStripButtonDownloadFile.Image
        ToolStripFileActions.Items.Insert(ToolStripFileActions.Items.IndexOf(ToolStripButtonDownloadFile) + 1, DownloadFolderButton)
        ContextMenuStripFolder.Items.Insert(0, DownloadFolderContext)
        AddHandler DownloadFolderButton.Click, AddressOf SelectFolderDownload
        AddHandler DownloadFolderContext.Click, AddressOf SelectFolderDownload
        AddHandler TreeViewDmsFolders.AfterSelect, Sub(sender, e) UpdateFolderDownloadAccess()
        AddHandler TreeViewDmsFolders.EnabledChanged, Sub(sender, e) UpdateFolderDownloadAccess()
        UpdateFolderDownloadAccess()
    End Sub

    Private Sub UpdateFolderDownloadAccess()
        Dim allowed = (AllowedActions And FileOrFolderActions.AllowDownloadFiles) <> 0
        DownloadFolderButton.Available = allowed AndAlso BrowseMode = BrowseModes.FoldersAndFiles
        DownloadFolderContext.Available = allowed
        Dim enabled = CanDownloadFolder() AndAlso TreeViewDmsFolders.Enabled
        DownloadFolderButton.Enabled = enabled
        DownloadFolderContext.Enabled = enabled
    End Sub

    Friend Function CanDownloadFolder() As Boolean
        Return Not TransferRunning AndAlso Not ResourceActionRunning AndAlso Not DragDownloadRunning AndAlso
            (AllowedActions And FileOrFolderActions.AllowDownloadFiles) <> 0 AndAlso TypeOf TreeViewDmsFolders.SelectedNode?.Tag Is NodeTagData
    End Function

    Private Async Sub SelectFolderDownload(sender As Object, e As EventArgs)
        If Not CanDownloadFolder() Then Return
        Dim data = DirectCast(TreeViewDmsFolders.SelectedNode.Tag, NodeTagData)
        Dim folder = If(data.DmsResourceItem, New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Root, .Name = "DMS", .FullName = DmsProvider.BrowseInRootFolderName})
        Using picker As New FolderBrowserDialog With {.Description = GetTransferWindowTitle("ActionDownloadFolder"), .SelectedPath = LocalDefaultFolderDownloads, .ShowNewFolderButton = True}
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            If Not String.IsNullOrEmpty(LocalParentMustFolder) Then
                Dim parent = Path.GetFullPath(LocalParentMustFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                Dim target = Path.GetFullPath(picker.SelectedPath)
                If Not target.Equals(parent, StringComparison.OrdinalIgnoreCase) AndAlso Not target.StartsWith(parent & Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) Then
                    MessageBox.Show(Me, UiStrings.Format("OutsideRequiredFolder", LocalParentMustFolder), Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End If
            End If
            Try
                Await RunTransferAsync(Async Function()
                                           Dim plan As RemoteDownloadPlan
                                           Using preparation = CreateTransferDialog(New String() {}, True, False)
                                               preparation.Show(Me)
                                               Try
                                                   plan = Await RemoteDownloadPlan.BuildAsync(DmsProvider, folder, picker.SelectedPath, preparation.CancellationToken)
                                               Finally
                                                   preparation.Finish()
                                               End Try
                                           End Using
                                           Dim selected As New List(Of DmsResourceItem)
                                           Dim destinations As New List(Of String)
                                           Dim permissions As New List(Of Boolean)
                                           Dim conflicts As New DownloadConflictPolicy(Function(path)
                                                                                          Using question As New DownloadConflictDialog(path, Me.Icon)
                                                                                              question.ShowDialog(Me)
                                                                                              Return New DownloadConflictDecision With {.Choice = question.Choice, .ApplyToAll = question.ApplyToAll.Checked}
                                                                                          End Using
                                                                                      End Function)
                                           Dim skipped As Integer
                                           For index = 0 To plan.Files.Count - 1
                                               Dim path = plan.Destinations(index)
                                               RemoteDownloadPlan.ValidateLocalPath(path, False)
                                               Dim replace As Boolean
                                               If File.Exists(path) Then
                                                   Dim choice = conflicts.Resolve(path)
                                                   If choice = DownloadConflictAction.Cancel Then Throw New OperationCanceledException()
                                                   If choice = DownloadConflictAction.Skip Then
                                                       skipped += 1
                                                       Continue For
                                                   End If
                                                   replace = True
                                               End If
                                               selected.Add(plan.Files(index))
                                               destinations.Add(path)
                                               permissions.Add(replace)
                                           Next
                                           Using dialog = CreateTransferDialog(selected.Select(Function(file) file.Name).ToArray(), True, False)
                                               dialog.Show(Me)
                                               Try
                                                   Await CreateDownloadDirectoriesAsync(plan.Directories, dialog.CancellationToken)
                                                   If selected.Count > 0 Then
                                                       Await DownloadBatchWithProgressAsync(DmsProvider, selected.ToArray(), destinations.ToArray(), dialog, dialog.CancellationToken, permissions.ToArray())
                                                   End If
                                               Catch ex As Exception
                                                   dialog.SetFailure(ex)
                                                   dialog.Finish()
                                                   dialog.ShowDialog(Me)
                                                   Return
                                               End Try
                                               dialog.Finish()
                                           End Using
                                           MessageBox.Show(Me, UiStrings.Format("FolderDownloadCompleted", selected.Count, skipped), Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
                                       End Function)
            Catch ex As OperationCanceledException
                Return
            Catch ex As Exception
                MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.ToString()), Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Friend Shared Function CreateDownloadDirectoriesAsync(paths As IEnumerable(Of String), cancellationToken As CancellationToken) As Task
        Return Task.Run(Sub()
                            For Each folderPath In paths
                                cancellationToken.ThrowIfCancellationRequested()
                                RemoteDownloadPlan.ValidateLocalPath(folderPath, True)
                                Directory.CreateDirectory(folderPath)
                            Next
                        End Sub, cancellationToken)
    End Function
End Class
