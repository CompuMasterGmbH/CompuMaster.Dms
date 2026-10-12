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
            AddHandler target.DragEnter, AddressOf DragRemoteMoveOver
            AddHandler target.DragOver, AddressOf DragRemoteMoveOver
            AddHandler target.DragDrop, AddressOf DropRemoteMove
            AddHandler target.DragLeave, Sub(sender, e)
                                             If ActiveRemoteDrag IsNot Nothing Then ActiveRemoteDrag.HoverTarget = Nothing
                                         End Sub
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
        TreeViewDmsFolders.AllowDrop = allow OrElse ((AllowedActions And FileOrFolderActions.AllowCopyRenameMoveFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles)
    End Sub

    Friend Function CanUploadDrop() As Boolean
        Return Not DragDownloadRunning AndAlso Not TransferRunning AndAlso Not ResourceActionRunning AndAlso (AllowedActions And FileOrFolderActions.AllowUploadFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles
    End Function

    Friend Function UploadDropNode(target As Control, screenPoint As Point) As TreeNode
        If target Is TreeViewDmsFolders Then Return TreeViewDmsFolders.GetNodeAt(TreeViewDmsFolders.PointToClient(screenPoint))
        If target Is ListViewDmsFiles Then Return TreeViewDmsFolders.SelectedNode
        Return Nothing
    End Function

    Friend Function UploadDestination(node As TreeNode) As String
        Dim data = TryCast(node?.Tag, NodeTagData)
        If data Is Nothing Then Throw New DmsUserInputInvalidException(UiStrings.GetText("NoFolderSelected"))
        'The synthetic server root intentionally has no resource metadata.
        Return If(data.DmsResourceItem?.FullName, Me.DmsProvider.BrowseInRootFolderName)
    End Function

    Private Function IsValidUploadTarget(node As TreeNode, Optional ownsOperation As Boolean = False) As Boolean
        If node Is Nothing OrElse node.TreeView IsNot TreeViewDmsFolders OrElse Not TypeOf node.Tag Is NodeTagData OrElse
            (Not ownsOperation AndAlso Not TreeViewDmsFolders.Enabled) Then Return False
        Dim resource = DirectCast(node.Tag, NodeTagData).DmsResourceItem
        If resource Is Nothing AndAlso node IsNot RootNode Then Return False
        If resource IsNot Nothing AndAlso (resource.ItemType = DmsResourceItem.ItemTypes.File OrElse resource.ExtendedInfosCollisionDetected OrElse
            String.IsNullOrEmpty(resource.FullName) OrElse (resource.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso Not DmsProvider.SupportsCollections)) Then Return False
        Return IsWithinBrowserRoot(UploadDestination(node))
    End Function

    Friend Function CanAcceptDirectFiles(node As TreeNode) As Boolean
        If Not IsValidUploadTarget(node, True) Then Return False
        Dim path = UploadDestination(node)
        Dim actualRoot = String.Equals(If(path, "").Trim(DmsProvider.DirectorySeparator), If(DmsProvider.BrowseInRootFolderName, "").Trim(DmsProvider.DirectorySeparator), StringComparison.Ordinal)
        Return Not actualRoot OrElse DmsProvider.SupportsFilesInRootFolder
    End Function

    Friend Function CanUploadLocalPaths(paths As String(), node As TreeNode) As Boolean
        If paths Is Nothing OrElse paths.Length = 0 OrElse Not CanUploadDrop() OrElse Not IsValidUploadTarget(node) Then Return False
        Return CanAcceptDirectFiles(node) OrElse paths.All(Function(path) System.IO.Directory.Exists(path))
    End Function

    Friend Sub ValidateUploadPlan(node As TreeNode, plan As LocalUploadPlan)
        If Not IsValidUploadTarget(node, True) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("NoFolderSelected"))
        If Not CanAcceptDirectFiles(node) AndAlso plan.RelativePaths.Any(Function(path) Not path.Contains("/")) Then Throw New DmsUserInputInvalidException(UiStrings.GetText("RootFileDropUnavailable"))
    End Sub

    Private Sub DragUploadOver(sender As Object, e As DragEventArgs)
        Dim node = UploadDropNode(DirectCast(sender, Control), New Point(e.X, e.Y))
        e.Effect = DragDropEffects.None
        If Not CanUploadDrop() OrElse (e.AllowedEffect And DragDropEffects.Copy) = 0 OrElse Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
        If CanUploadLocalPaths(paths, node) Then e.Effect = DragDropEffects.Copy
    End Sub

    Private Async Sub DropLocalPaths(sender As Object, e As DragEventArgs)
        If Not CanUploadDrop() OrElse Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        Dim node = UploadDropNode(DirectCast(sender, Control), New Point(e.X, e.Y))
        Dim paths = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
        If (e.AllowedEffect And DragDropEffects.Copy) = 0 OrElse Not CanUploadLocalPaths(paths, node) Then Return
        Await UploadLocalSelectionAsync(paths, node)
    End Sub

    Private Async Sub SelectFolderUpload(sender As Object, e As EventArgs)
        If Not CanUploadDrop() OrElse TreeViewDmsFolders.SelectedNode Is Nothing Then Return
        Using picker As New FolderBrowserDialog With {.Description = GetTransferWindowTitle("ActionUploadFolder"), .SelectedPath = LocalDefaultFolderUploads, .ShowNewFolderButton = False}
            If picker.ShowDialog(Me) = DialogResult.OK Then Await UploadLocalSelectionAsync({picker.SelectedPath}, TreeViewDmsFolders.SelectedNode)
        End Using
    End Sub

    Private Async Function UploadLocalSelectionAsync(paths As String(), target As TreeNode) As Task
        If Not CanUploadLocalPaths(paths, target) Then Return
        Try
            Dim provider = DmsProvider
            Dim scope = InitialFolder
            Dim instance = TryCast(provider, Providers.IDmsInstanceProvider)?.CurrentDmsInstance?.ID
            Dim destination = UploadDestination(target)
            Dim targetResource = DirectCast(target.Tag, NodeTagData).DmsResourceItem
            Dim targetFolderId = targetResource?.ExtendedInfosFolderID
            Dim targetCollectionId = targetResource?.ExtendedInfosCollectionID
            Dim canWrite As Func(Of Boolean) = Function() Not IsDisposed AndAlso DmsProvider Is provider AndAlso InitialFolder = scope AndAlso
                TryCast(provider, Providers.IDmsInstanceProvider)?.CurrentDmsInstance?.ID = instance AndAlso
                (AllowedActions And FileOrFolderActions.AllowUploadFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles AndAlso
                IsValidUploadTarget(target, True) AndAlso UploadDestination(target) = destination AndAlso
                DirectCast(target.Tag, NodeTagData).DmsResourceItem?.ExtendedInfosFolderID = targetFolderId AndAlso
                DirectCast(target.Tag, NodeTagData).DmsResourceItem?.ExtendedInfosCollectionID = targetCollectionId
            Dim successful As Boolean
            Await RunTransferAsync(Async Function()
                                       Dim plan As LocalUploadPlan
                                       Using preparation = CreateTransferDialog(New String() {}, False, False)
                                           preparation.Show(Me)
                                           Try
                                               plan = Await Task.Run(Function() LocalUploadPlan.Build(paths, preparation.CancellationToken, LocalParentMustFolder))
                                           Finally
                                               preparation.Finish()
                                           End Try
                                       End Using
                                       If plan.Files.Length = 0 AndAlso plan.Directories.Length = 0 Then Return
                                       If Not canWrite() Then Throw New UnauthorizedAccessException()
                                       ValidateUploadPlan(target, plan)
                                       Dim allowedPlan As Func(Of Boolean) = Function()
                                                                                If Not canWrite() Then Return False
                                                                                Return CanAcceptDirectFiles(target) OrElse Not plan.RelativePaths.Any(Function(path) Not path.Contains("/"))
                                                                            End Function
                                       successful = Await UploadWithDialogAsync(destination, plan.Files, plan.RelativePaths, plan.Directories, allowedPlan)
                                       Await RefreshTreePreservingViewAsync(target)
                                   End Function)
            If successful Then MessageBox.Show(Me, LastUploadSummary, Text, MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As OperationCanceledException
            Return
        Catch ex As Exception
            Me.HandleBrowserFailure(ex)
        End Try
    End Function
End Class
