Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser

    Private Sub ApplyTreeChildren(parentNode As TreeNode, ChildDirectories As List(Of DmsResourceItem))
        Dim ParentData As NodeTagData = CType(parentNode.Tag, NodeTagData)
        parentNode.Nodes.Clear()
        For Each ChildDirectory As DmsResourceItem In ChildDirectories
            Dim CollectionAllowed As Boolean = ChildDirectory.ItemType <> DmsResourceItem.ItemTypes.Collection OrElse
                (Me.DmsProvider.SupportsCollections AndAlso
                 (ParentData.DmsResourceItem Is Nothing OrElse ParentData.DmsResourceItem.ItemType = DmsResourceItem.ItemTypes.Collection))
            If CollectionAllowed Then Me.AddDirectoryTreeNode(parentNode, ChildDirectory)
        Next
        ParentData.ChildrenLoaded = True
        UpdateChildDirectoryMetadata(parentNode)
    End Sub

    Friend Async Function LoadTreeAsync() As Task
        Me.TreeViewDmsFolders.Nodes.Clear()
        If Me.InitialFolder <> Nothing Then
            Dim Folder As DmsResourceItem = Await Me.DmsProvider.ListRemoteItemAsync(Me.InitialFolder)
            If Folder Is Nothing Then Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(Me.InitialFolder)
            Me.RootNode = Me.TreeViewDmsFolders.Nodes.Add("", Me.InitialFolder)
            Me.RootNode.Tag = New NodeTagData(Folder)
            If Folder.ExtendedInfosHasGroupSharings OrElse Folder.ExtendedInfosHasUserSharings OrElse Folder.ExtendedInfosIsShared OrElse Folder.ExtendedInfosHasLinks Then
                Me.RootNode.ImageIndex = 3
                Me.RootNode.SelectedImageIndex = 3
            Else
                Me.RootNode.ImageIndex = 0
                Me.RootNode.SelectedImageIndex = 0
            End If
        Else
            Me.RootNode = Me.TreeViewDmsFolders.Nodes.Add("", Tools.NotEmptyOrAlternativeValue(Me.DmsProvider.BrowseInRootFolderName, "/"))
            Me.RootNode.Tag = New NodeTagData(Nothing)
            Me.RootNode.ImageIndex = 0
            Me.RootNode.SelectedImageIndex = 0
        End If
        Await Me.AddTreeChildrenAsync(Me.RootNode)
        Me.TreeViewDmsFolders.Sort()
        Me.RootNode.Expand()
    End Function

    Friend Async Function AddTreeChildrenAsync(parentNode As TreeNode) As Task
        Dim data As NodeTagData = DirectCast(parentNode.Tag, NodeTagData)
        If data.ChildrenLoaded Then Return
        If HasKnownChildDirectories(data.DmsResourceItem) = False Then
            Me.ApplyTreeChildren(parentNode, New List(Of DmsResourceItem))
            Return
        End If
        If data.ChildrenLoading IsNot Nothing Then
            Await data.ChildrenLoading
            Return
        End If
        data.ChildrenLoading = Me.LoadTreeChildrenAsync(parentNode)
        Try
            Await data.ChildrenLoading
        Finally
            data.ChildrenLoading = Nothing
        End Try
    End Function

    Private Async Function LoadTreeChildrenAsync(parentNode As TreeNode) As Task
        Dim data As NodeTagData = DirectCast(parentNode.Tag, NodeTagData)
        Dim path As String = If(data.DmsResourceItem?.FullName, Me.DmsProvider.BrowseInRootFolderName)
        Dim children = Await Me.DmsProvider.ListAllDirectoryItemsAsync(path)
        If Me.IsDisposed OrElse parentNode.TreeView IsNot Me.TreeViewDmsFolders Then Return
        Me.ApplyTreeChildren(parentNode, children)
    End Function

    Private Async Function SelectFolderPathAsync(path As String) As Task
        Dim previousSuppression As Boolean = Me.SuppressSelectionRefresh
        Me.SuppressSelectionRefresh = True
        Try
            If path = Nothing Then
                Me.TreeViewDmsFolders.SelectedNode = Me.RootNode
            Else
                Dim FolderHierarchy As New List(Of String)(path.Split(Me.DmsProvider.DirectorySeparator))
                Dim LastMatch As TreeNode = Me.RootNode
                For MyCounter As Integer = 0 To FolderHierarchy.Count - 1
                    Await Me.AddTreeChildrenAsync(LastMatch)
                    Dim NewMatch As TreeNode = Me.FindChildNode(LastMatch, FolderHierarchy(MyCounter))
                    If NewMatch Is Nothing Then
                        Exit For 'Path has been found partially, select as far as possible
                    Else
                        LastMatch = NewMatch
                    End If
                Next
                Me.TreeViewDmsFolders.SelectedNode = LastMatch
            End If
        Finally
            Me.SuppressSelectionRefresh = previousSuppression
        End Try
        Me.SelectedFolder = Me.SelectedFolderPath()
        Await Me.RefreshFilesListAsync()
    End Function

    Friend Async Function CreateNewDirectoryTreeNodeAsync(parentNode As TreeNode, folderName As String) As Task(Of TreeNode)
        Dim ParentData As NodeTagData = CType(parentNode.Tag, NodeTagData)
        If Not ParentData.ChildrenLoaded Then
            If HasKnownChildDirectories(ParentData.DmsResourceItem) = False Then
                ParentData.ChildrenLoaded = True
            Else
                Await Me.AddTreeChildrenAsync(parentNode)
            End If
        End If
        Dim NewFolderPath As String = Me.DmsProvider.CombinePath(CType(parentNode.Tag, NodeTagData).DmsResourceItem?.FullName, folderName)
        Await Me.DmsProvider.CreateDirectoryAsync(NewFolderPath)
        Dim Folder As DmsResourceItem = Await Me.DmsProvider.ListRemoteItemAsync(NewFolderPath)
        If Folder Is Nothing Then Throw New Data.DirectoryNotFoundException(NewFolderPath)
        'A newly created directory is empty even when the provider omits child metadata.
        If Not Folder.ChildDirectoryCount.HasValue AndAlso Not Folder.HasChildDirectories.HasValue Then
            Folder.ChildDirectoryCount = 0
            Folder.HasChildDirectories = False
        End If
        Me.AddDirectoryTreeNode(parentNode, Folder)
        RecordChildDirectoryCreated(parentNode)
        Dim NewNode As TreeNode = parentNode.Nodes.Cast(Of TreeNode)().Single(Function(node) node.Tag IsNot Nothing AndAlso CType(node.Tag, NodeTagData).DmsResourceItem Is Folder)
        'The empty child list is already known; expansion must not replace newly inserted children.
        If HasKnownChildDirectories(Folder) = False Then CType(NewNode.Tag, NodeTagData).ChildrenLoaded = True
        Return NewNode
    End Function

    Friend Async Function RefreshCurrentFolderAndFilesAsync() As Task
        Dim selectedNode As TreeNode = Me.TreeViewDmsFolders.SelectedNode
        If selectedNode Is Nothing AndAlso Me.LastFileListFolderPath IsNot Nothing Then
            Await Me.SelectFolderPathAsync(Me.PathRelativeToBrowserRoot(Me.LastFileListFolderPath))
            selectedNode = Me.TreeViewDmsFolders.SelectedNode
        ElseIf selectedNode Is Nothing AndAlso Me.SelectedFolder IsNot Nothing Then
            Await Me.SelectFolderPathAsync(Me.SelectedFolder)
            selectedNode = Me.TreeViewDmsFolders.SelectedNode
        End If
        If selectedNode Is Nothing Then selectedNode = Me.RootNode
        Dim selectionAncestors As New List(Of TreeNode)
        While selectedNode IsNot Nothing
            selectionAncestors.Add(selectedNode)
            selectedNode = selectedNode.Parent
        End While
        Me.SuppressSelectionRefresh = True
        Me.TreeViewDmsFolders.BeginUpdate()
        Try
            If Me.RootNode IsNot Nothing Then Await Me.RefreshLoadedTreeAsync(Me.RootNode, selectionAncestors.FirstOrDefault())
            Me.TreeViewDmsFolders.Sort()
            Me.TreeViewDmsFolders.SelectedNode = selectionAncestors.FirstOrDefault(Function(node) node.TreeView Is Me.TreeViewDmsFolders)
            If Me.TreeViewDmsFolders.SelectedNode Is Nothing Then Me.TreeViewDmsFolders.SelectedNode = Me.RootNode
            Me.SelectedFolder = Me.SelectedFolderPath()
        Finally
            Me.TreeViewDmsFolders.EndUpdate()
            Me.SuppressSelectionRefresh = False
        End Try
        Await Me.RefreshFilesListAsync()
    End Function

    Private Async Function RefreshLoadedTreeAsync(node As TreeNode, selectedNode As TreeNode) As Task
        Dim data As NodeTagData = DirectCast(node.Tag, NodeTagData)
        If Not data.ChildrenLoaded AndAlso node IsNot selectedNode Then Return
        Dim path As String = If(data.DmsResourceItem?.FullName, Me.DmsProvider.BrowseInRootFolderName)
        Await Me.DmsProvider.ResetCachesForRemoteItemsAsync(path, BaseDmsProvider.SearchItemType.AllItems)
        Dim children As List(Of DmsResourceItem) = Await Me.DmsProvider.ListAllDirectoryItemsAsync(path)
        Dim previousNodes As List(Of TreeNode) = node.Nodes.Cast(Of TreeNode)().Where(Function(child) child.Tag IsNot Nothing).ToList()
        For Each child As TreeNode In node.Nodes.Cast(Of TreeNode)().Where(Function(item) item.Tag Is Nothing).ToList()
            child.Remove()
        Next
        For Each item As DmsResourceItem In children
            If item.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso
                (Not Me.DmsProvider.SupportsCollections OrElse
                 (data.DmsResourceItem IsNot Nothing AndAlso data.DmsResourceItem.ItemType <> DmsResourceItem.ItemTypes.Collection)) Then Continue For
            Dim existing As TreeNode = previousNodes.FirstOrDefault(Function(child)
                                                                       Dim resource = DirectCast(child.Tag, NodeTagData).DmsResourceItem
                                                                       Return resource.FullName = item.FullName AndAlso resource.ItemType = item.ItemType
                                                                   End Function)
            If existing Is Nothing Then
                Me.AddDirectoryTreeNode(node, item)
            Else
                previousNodes.Remove(existing)
                DirectCast(existing.Tag, NodeTagData).DmsResourceItem = item
                existing.Text = item.Name
                Dim appearance As TreeNode = CreateDirectoryTreeNode(item)
                existing.ImageIndex = appearance.ImageIndex
                existing.SelectedImageIndex = appearance.SelectedImageIndex
                If Not DirectCast(existing.Tag, NodeTagData).ChildrenLoaded AndAlso existing IsNot selectedNode Then
                    existing.Nodes.Clear()
                    AddExpansionPlaceholderIfRequired(existing)
                Else
                    Await Me.RefreshLoadedTreeAsync(existing, selectedNode)
                End If
            End If
        Next
        For Each removed As TreeNode In previousNodes
            removed.Remove()
        Next
        data.ChildrenLoaded = True
        UpdateChildDirectoryMetadata(node)
    End Function

    Friend Async Function ReloadDmsInstanceViewAsync() As Task
        Me.TreeViewDmsFolders.BeginUpdate()
        Try
            Me.ListViewDmsFiles.Items.Clear()
            Me.ListViewDmsFiles.Tag = Nothing
            Me.LastFileListFolderPath = Nothing
            Me.SelectedFolder = Nothing
            Await Me.LoadTreeAsync()
            Await Me.SelectFolderPathAsync(Nothing)
        Finally
            Me.TreeViewDmsFolders.EndUpdate()
        End Try
        Me.UpdateDmsInstanceButton()
    End Function

    Private FileListRefreshVersion As Integer

    Friend Async Function RefreshFilesListAsync() As Task
        FileListRefreshVersion += 1
        Dim version As Integer = FileListRefreshVersion
        Dim selectedNode As TreeNode = Me.TreeViewDmsFolders.SelectedNode
        Dim currentFolder As NodeTagData = TryCast(selectedNode?.Tag, NodeTagData)
        Dim path As String = currentFolder?.DmsResourceItem?.FullName
        If currentFolder IsNot Nothing Then
            If path Is Nothing AndAlso Me.DmsProvider.SupportsFilesInRootFolder Then path = Me.DmsProvider.BrowseInRootFolderName
        Else
            path = Me.LastFileListFolderPath
            If path Is Nothing AndAlso Me.SelectedFolder IsNot Nothing Then path = Me.DmsProvider.CombinePath(Me.InitialFolder, Me.SelectedFolder)
        End If
        Dim files As New List(Of DmsResourceItem)
        If Not Me.SplitContainer.Panel2Collapsed AndAlso path IsNot Nothing Then
            Await Me.DmsProvider.ResetCachesForRemoteItemsAsync(path, BaseDmsProvider.SearchItemType.Files)
            Try
                files = Await Me.DmsProvider.ListAllFileItemsAsync(path)
            Catch ex As Data.DirectoryNotFoundException
                If Not Me.IsDisposed AndAlso version = FileListRefreshVersion AndAlso selectedNode Is Me.TreeViewDmsFolders.SelectedNode Then
                    Me.ShowMissingDirectory(Me.FindDirectoryNodeByPath(ex.RemotePath), ex.RemotePath)
                End If
                Return
            End Try
        End If
        If Me.IsDisposed OrElse version <> FileListRefreshVersion OrElse selectedNode IsNot Me.TreeViewDmsFolders.SelectedNode Then Return
        Me.LastFileListFolderPath = path
        Me.ListViewDmsFiles.Items.Clear()
        Me.ListViewDmsFiles.Tag = Nothing
        If Not Me.SplitContainer.Panel2Collapsed Then Me.DisplayFiles(files)
    End Function

    Friend Async Function DeleteFilesForUiAsync(files As IEnumerable(Of DmsResourceItem)) As Task
        For Each file As DmsResourceItem In files
            Await Me.DmsProvider.DeleteRemoteItemAsync(file)
        Next
    End Function

    Friend Async Function DeleteDirectoryForUiAsync(node As TreeNode) As Task
        Dim item As DmsResourceItem = DirectCast(node.Tag, NodeTagData).DmsResourceItem
        Dim parent As TreeNode = node.Parent
        Await Me.DmsProvider.DeleteRemoteItemAsync(item)
        If Me.IsDisposed OrElse node.TreeView IsNot Me.TreeViewDmsFolders Then Return
        Me.SuppressSelectionRefresh = True
        Try
            node.Remove()
            If parent IsNot Nothing Then
                RecordChildDirectoryDeleted(parent)
                Me.TreeViewDmsFolders.SelectedNode = parent
            End If
            Me.SelectedFolder = Me.SelectedFolderPath()
        Finally
            Me.SuppressSelectionRefresh = False
        End Try
        Await Me.RefreshFilesListAsync()
    End Function

    'Refresh even after a partly completed batch without hiding its original failure.
    Private Async Function RunOperationAndRefreshAsync(operation As Func(Of Task), refresh As Func(Of Task)) As Task
        Dim failure As Exception = Nothing
        Try
            Await operation()
        Catch ex As Exception
            failure = ex
        End Try
        Try
            Await refresh()
        Catch ex As Exception
            If failure Is Nothing Then failure = ex
        End Try
        If failure IsNot Nothing Then System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw()
    End Function

End Class
