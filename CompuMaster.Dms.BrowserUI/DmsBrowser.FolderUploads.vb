Imports System.Drawing
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data

Partial Public Class DmsBrowser
    Private ReadOnly UploadFolderButton As New ToolStripButton With {.Name = "UploadFolder", .Text = UiStrings.GetText("ActionUploadFolder")}
    Private ReadOnly UploadFolderContext As New ToolStripButton With {.Name = "UploadFolderContext", .Text = UiStrings.GetText("ActionUploadFolder")}

    Private Sub InitializeFolderUploads()
        UploadFolderButton.Image = ToolStripButtonUploadFile.Image
        UploadFolderContext.Image = ToolStripButtonUploadFile.Image
        ToolStripFileActions.Items.Insert(1, UploadFolderButton)
        ContextMenuStripFile.Items.Insert(1, UploadFolderContext)
        AddHandler ListViewDmsFiles.ItemDrag, AddressOf DragRemoteFiles
        AddHandler FormClosing, Sub(sender, e)
                                    If DragDownloadRunning Then e.Cancel = True
                                End Sub
        AddHandler UploadFolderButton.Click, AddressOf SelectFolderUpload
        AddHandler UploadFolderContext.Click, AddressOf SelectFolderUpload
        'Enabled includes the owner's temporary startup/transfer lock. Resynchronize
        'when file upload recovers so its copied disabled state cannot become permanent.
        AddHandler ToolStripButtonUploadFile.EnabledChanged, Sub(sender, e) UpdateUploadAccess()
        AddHandler ToolStripFileContextButtonUploadFile.EnabledChanged, Sub(sender, e) UpdateUploadAccess()
        For Each target As Control In New Control() {ListViewDmsFiles, TreeViewDmsFolders}
            AddHandler target.DragEnter, AddressOf DragUploadOver
            AddHandler target.DragOver, AddressOf DragUploadOver
            AddHandler target.DragDrop, AddressOf DropLocalPaths
        Next
        UpdateUploadAccess()
    End Sub

    Private Sub UpdateUploadAccess()
        UploadFolderButton.Available = ToolStripButtonUploadFile.Available
        UploadFolderButton.Enabled = ToolStripButtonUploadFile.Enabled
        UploadFolderContext.Available = ToolStripFileContextButtonUploadFile.Available
        UploadFolderContext.Enabled = ToolStripFileContextButtonUploadFile.Enabled
        Dim allow = (AllowedActions And FileOrFolderActions.AllowUploadFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles
        ListViewDmsFiles.AllowDrop = allow
        TreeViewDmsFolders.AllowDrop = allow
    End Sub

    Friend Function CanUploadDrop() As Boolean
        Return Not DragDownloadRunning AndAlso Not TransferRunning AndAlso Not ResourceActionRunning AndAlso (AllowedActions And FileOrFolderActions.AllowUploadFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles
    End Function

    Friend Function UploadDropNode(target As Control, screenPoint As Point) As TreeNode
        If target Is TreeViewDmsFolders Then Return TreeViewDmsFolders.GetNodeAt(TreeViewDmsFolders.PointToClient(screenPoint))
        If target Is ListViewDmsFiles Then Return TreeViewDmsFolders.SelectedNode
        Return Nothing
    End Function

    Private Sub DragUploadOver(sender As Object, e As DragEventArgs)
        Dim node = UploadDropNode(DirectCast(sender, Control), New Point(e.X, e.Y))
        e.Effect = If(CanUploadDrop() AndAlso e.Data.GetDataPresent(DataFormats.FileDrop) AndAlso TypeOf node?.Tag Is NodeTagData AndAlso (e.AllowedEffect And DragDropEffects.Copy) <> 0, DragDropEffects.Copy, DragDropEffects.None)
    End Sub

    Private Async Sub DropLocalPaths(sender As Object, e As DragEventArgs)
        If Not CanUploadDrop() OrElse Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        Dim node = UploadDropNode(DirectCast(sender, Control), New Point(e.X, e.Y))
        If Not TypeOf node?.Tag Is NodeTagData Then Return
        Await UploadLocalSelectionAsync(DirectCast(e.Data.GetData(DataFormats.FileDrop), String()), node)
    End Sub

    Private Async Sub SelectFolderUpload(sender As Object, e As EventArgs)
        If Not CanUploadDrop() OrElse TreeViewDmsFolders.SelectedNode Is Nothing Then Return
        Using picker As New FolderBrowserDialog With {.Description = UiStrings.GetText("ActionUploadFolder"), .SelectedPath = LocalDefaultFolderUploads, .ShowNewFolderButton = False}
            If picker.ShowDialog(Me) = DialogResult.OK Then Await UploadLocalSelectionAsync({picker.SelectedPath}, TreeViewDmsFolders.SelectedNode)
        End Using
    End Sub

    Private Async Function UploadLocalSelectionAsync(paths As String(), target As TreeNode) As Task
        If Not CanUploadDrop() Then Return
        Try
            Dim destination = DirectCast(target.Tag, NodeTagData).DmsResourceItem.FullName
            Dim successful As Boolean
            Await RunTransferAsync(Async Function()
                                       Dim plan As LocalUploadPlan
                                       Using preparation As New UploadProgressDialog(New String() {}, False, False, Me.Icon)
                                           preparation.Show(Me)
                                           Try
                                               plan = Await Task.Run(Function() LocalUploadPlan.Build(paths, preparation.CancellationToken, LocalParentMustFolder))
                                           Finally
                                               preparation.Finish()
                                           End Try
                                       End Using
                                       If plan.Files.Length = 0 AndAlso plan.Directories.Length = 0 Then Return
                                       successful = Await UploadWithDialogAsync(destination, plan.Files, plan.RelativePaths, plan.Directories)
                                       Await RefreshLoadedTreeAsync(target, target)
                                       TreeViewDmsFolders.Sort()
                                   End Function)
            If successful Then MessageBox.Show(Me, UiStrings.GetText("UploadSuccessful"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As OperationCanceledException
            Return
        Catch ex As Exception
            MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function
End Class
