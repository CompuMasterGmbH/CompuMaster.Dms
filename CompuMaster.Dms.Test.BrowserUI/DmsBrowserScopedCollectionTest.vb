Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA)>
Public Class DmsBrowserScopedCollectionTest
    Private Const AllActions As DmsBrowser.FileOrFolderActions = DmsBrowser.FileOrFolderActions.AllowCreateFolders Or DmsBrowser.FileOrFolderActions.AllowSharings Or DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or DmsBrowser.FileOrFolderActions.AllowDeleteFiles

    <TestCase(False), TestCase(True)>
    Public Sub ScopedCollectionHidesItsManagementWhileKeepingChildFolderActions(foldersOnly As Boolean)
        Dim provider As New ScopedProvider()
        Using dispatcher As New UiTestDispatcher(), browser = OpenBrowser(provider, "collection", foldersOnly)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.ToolStripButtonSharingsFolder.Available, [Is].False, "The scoped collection must not offer collection sharing/link management.")
            Assert.That(browser.ToolStripFolderContextButtonShareFolder.Available, [Is].False)
            Assert.That(browser.ToolStripFolderContextButtonRenameFolder.Available, [Is].False)
            Assert.That(browser.ToolStripFolderContextButtonDeleteFolder.Available, [Is].False)
            Assert.That(browser.ButtonCreateNewFolder.Enabled, [Is].True, "Creating an ordinary child folder remains supported.")
            dispatcher.Finish(browser.RunTransferAsync(Function() Task.CompletedTask))
            Assert.That(browser.ToolStripButtonSharingsFolder.Available, [Is].False, "Busy-state recovery must not restore collection controls.")
            If Not foldersOnly Then SavePreview(browser, "root")
            browser.TreeViewDmsFolders.SelectedNode = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            dispatcher.WaitForEvents()
            Assert.That(browser.ToolStripButtonSharingsFolder.Available, [Is].True, "Ordinary folder sharing stays available.")
            Assert.That(browser.ToolStripFolderContextButtonRenameFolder.Available, [Is].True)
            If Not foldersOnly Then SavePreview(browser, "child")
            browser.Close()
            dispatcher.WaitForEvents()
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub DirectCollectionCommandsAreRejectedBeforeProviderWrites(asynchronous As Boolean)
        Dim provider As New ScopedProvider()
        Using dispatcher As New UiTestDispatcher(), browser = OpenBrowser(provider, "collection", False)
            browser.Show()
            dispatcher.WaitForEvents()
            Dim item = provider.ListRemoteItem("collection")
            Assert.Throws(Of DmsUserInputInvalidException)(Sub() browser.EnsureCollectionActionWithinBrowserScope(item))
            For Each action In New DmsBrowser.ResourceAction() {DmsBrowser.ResourceAction.Copy, DmsBrowser.ResourceAction.Move, DmsBrowser.ResourceAction.Rename}
                If asynchronous Then
                    Assert.ThrowsAsync(Of DmsUserInputInvalidException)(Async Function() As Task
                                                                           Await browser.ExecuteResourceActionAsync(item, "collection/new", action)
                                                                       End Function)
                Else
                    Assert.Throws(Of DmsUserInputInvalidException)(Sub() browser.ExecuteResourceAction(item, "collection/new", action))
                End If
            Next
            Assert.ThrowsAsync(Of DmsUserInputInvalidException)(Async Function() As Task
                                                                   Await browser.DeleteDirectoryForUiAsync(browser.TreeViewDmsFolders.Nodes(0))
                                                               End Function)
            Assert.That(provider.Writes, [Is].Zero)
            browser.Close()
            dispatcher.WaitForEvents()
        End Using
    End Sub

    <TestCase(""), TestCase("/")>
    Public Sub ASelectedFolderAtTheActualProviderRootRetainsCollectionActions(root As String)
        Dim provider As New ScopedProvider()
        Using dispatcher As New UiTestDispatcher(), browser = OpenBrowser(provider, root, False)
            browser.SelectedFolder = "collection"
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.TreeViewDmsFolders.SelectedNode.Text, [Is].EqualTo("collection"))
            Assert.That(browser.ToolStripButtonSharingsFolder.Available, [Is].True)
            Assert.DoesNotThrow(Sub() browser.EnsureCollectionActionWithinBrowserScope(provider.ListRemoteItem("collection")))
            browser.Close()
            dispatcher.WaitForEvents()
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub AStaleRootNodeCannotCreateAtTheHiddenProviderRoot(asynchronous As Boolean)
        Dim provider As New ScopedProvider()
        Using dispatcher As New UiTestDispatcher(), browser = OpenBrowser(provider, "", False)
            browser.Show()
            dispatcher.WaitForEvents()
            'Simulate changing the public root setting before the old tree has been reloaded.
            browser.InitialFolder = "collection"
            Dim oldRoot = browser.TreeViewDmsFolders.Nodes(0)
            If asynchronous Then
                Assert.ThrowsAsync(Of DmsUserInputInvalidException)(Async Function() As Task
                                                                       Await browser.CreateNewDirectoryTreeNodeAsync(oldRoot, "invisible")
                                                                   End Function)
            Else
                Assert.Throws(Of DmsUserInputInvalidException)(Sub() browser.CreateNewDirectoryTreeNode(oldRoot, "invisible"))
            End If
            Assert.That(provider.Writes, [Is].Zero)
            browser.Close()
            dispatcher.WaitForEvents()
        End Using
    End Sub

    Private Shared Sub SavePreview(browser As DmsBrowser, name As String)
        Dim directory = Environment.GetEnvironmentVariable("DMS_SCOPED_ROOT_PREVIEWS")
        If String.IsNullOrEmpty(directory) Then Return
        Using preview As New System.Drawing.Bitmap(browser.Width, browser.Height)
            browser.DrawToBitmap(preview, New System.Drawing.Rectangle(0, 0, preview.Width, preview.Height))
            preview.Save(System.IO.Path.Combine(directory, name & ".png"), System.Drawing.Imaging.ImageFormat.Png)
        End Using
    End Sub

    <TestCase(""), TestCase("/")>
    Public Sub CollectionManagementStaysHiddenUntilTheVisibleRootIsReloaded(nextRoot As String)
        Dim provider As New ScopedProvider()
        Using dispatcher As New UiTestDispatcher(), browser = OpenBrowser(provider, "collection", False)
            browser.Show()
            dispatcher.WaitForEvents()
            browser.InitialFolder = nextRoot
            Assert.That(browser.ToolStripButtonSharingsFolder.Available, [Is].False, "The actual provider root is still absent from the displayed tree.")
            Assert.Throws(Of DmsUserInputInvalidException)(Sub() browser.EnsureCollectionActionWithinBrowserScope(provider.ListRemoteItem("collection")))
            dispatcher.Finish(browser.LoadTreeAsync())
            browser.TreeViewDmsFolders.SelectedNode = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            dispatcher.WaitForEvents()
            Assert.That(browser.ToolStripButtonSharingsFolder.Available, [Is].True)
            Assert.DoesNotThrow(Sub() browser.EnsureCollectionActionWithinBrowserScope(provider.ListRemoteItem("collection")))
            browser.Close()
            dispatcher.WaitForEvents()
        End Using
    End Sub

    <TestCase("collection", False), TestCase("collection", True), TestCase("collection/Child", False), TestCase("collection/Child", True), TestCase("/collection", True)>
    Public Sub DirectoryCreationUsesTheVisibleRootAndDisplaysTheCreatedChild(root As String, asynchronous As Boolean)
        Dim provider As New ScopedProvider()
        Using dispatcher As New UiTestDispatcher(), browser = OpenBrowser(provider, root, False)
            browser.Show()
            dispatcher.WaitForEvents()
            Dim parent = browser.TreeViewDmsFolders.Nodes(0)
            If asynchronous Then
                dispatcher.Finish(browser.CreateNewDirectoryTreeNodeAsync(parent, "Created"))
            Else
                browser.CreateNewDirectoryTreeNode(parent, "Created")
            End If
            Assert.That(provider.Created, [Is].EqualTo(New String() {root.Trim("/"c) & "/Created"}))
            Assert.That(parent.Nodes.Cast(Of TreeNode)().Any(Function(node) node.Text = "Created"), [Is].True)
            Assert.That(provider.CollectionWrites, [Is].Zero)
            browser.Close()
            dispatcher.WaitForEvents()
        End Using
    End Sub

    Private Shared Function OpenBrowser(provider As ScopedProvider, root As String, foldersOnly As Boolean) As DmsBrowser
        Return New DmsBrowser(provider, Nothing, "Scoped collection audit", Nothing, root, "", If(foldersOnly, DmsBrowser.BrowseModes.Folders, DmsBrowser.BrowseModes.FoldersAndFiles), AllActions, DmsBrowser.DialogOperationModes.NoResults, "", "", "") With {.ShowInTaskbar = False}
    End Function

    <Test>
    Public Sub OtherCollectionProvidersRetainTheirOwnScopedCollectionCapabilities()
        Dim provider As New OtherCollectionProvider()
        Using browser As New DmsBrowser(provider)
            browser.InitialFolder = "collection"
            Assert.DoesNotThrow(Sub() browser.EnsureCollectionActionWithinBrowserScope(New DmsResourceItem With {.FullName = "collection", .ItemType = DmsResourceItem.ItemTypes.Collection}))
        End Using
    End Sub

    Private Class OtherCollectionProvider
        Inherits NoDmsProvider
        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    Private Class ScopedProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Created As New List(Of String)()
        Public CollectionWrites As Integer
        Public Writes As Integer
        Public Overrides ReadOnly Property DirectorySeparator As Char
            Get
                Return "/"c
            End Get
        End Property
        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return ""
            End Get
        End Property
        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return True
            End Get
        End Property
        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return True
            End Get
        End Property
        Public Overrides Function ListRemoteItem(path As String) As DmsResourceItem
            path = If(path, "").Trim("/"c)
            Return New DmsResourceItem With {.FullName = path, .Name = ItemName(path), .ItemType = If(path = "", DmsResourceItem.ItemTypes.Root, If(path = "collection", DmsResourceItem.ItemTypes.Collection, DmsResourceItem.ItemTypes.Folder)), .HasChildDirectories = path = "" OrElse path = "collection" OrElse path = "collection/Child"}
        End Function
        Public Overrides Function ListRemoteItemAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Task.FromResult(ListRemoteItem(path))
        End Function
        Private Function Children(path As String) As List(Of DmsResourceItem)
            path = If(path, "").Trim("/"c)
            Dim entries As New List(Of String) From {"collection", "collection/Child", "collection/Child/Grandchild"}
            entries.AddRange(Created)
            Return entries.Where(Function(entry) ParentDirectoryPath(entry) = path).Select(Function(entry) ListRemoteItem(entry)).ToList()
        End Function
        Public Overrides Function ListAllRemoteItems(path As String, searchType As BaseDmsProvider.SearchItemType) As List(Of DmsResourceItem)
            If searchType = BaseDmsProvider.SearchItemType.Files Then Return New List(Of DmsResourceItem)()
            Return Children(path)
        End Function
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(Children(path))
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(New List(Of DmsResourceItem)())
        End Function
        Public Overrides Function ListEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(Children(path))
        End Function
        Public Overrides Sub ResetCachesForRemoteItems(path As String, searchType As BaseDmsProvider.SearchItemType)
        End Sub
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As BaseDmsProvider.SearchItemType, Optional token As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
        Public Overrides Sub CreateDirectory(path As String)
            Writes += 1
            Created.Add(path)
            If ParentDirectoryPath(path) = "" Then CollectionWrites += 1
        End Sub
        Public Overrides Function CreateDirectoryAsync(path As String, Optional token As CancellationToken = Nothing) As Task
            CreateDirectory(path)
            Return Task.CompletedTask
        End Function
        Public Overrides Function DeleteRemoteItemAsync(item As DmsResourceItem, Optional token As CancellationToken = Nothing) As Task
            Writes += 1
            Return Task.CompletedTask
        End Function
    End Class
End Class
