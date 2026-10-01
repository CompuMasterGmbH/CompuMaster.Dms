Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserLazyTreeTest

    <Test>
    Public Sub InitialLoadUsesMetadataWithoutListingEveryVisibleDirectory()
        Dim Provider As New InMemoryDmsProvider
        Dim BooleanEmpty As DmsResourceItem = CreateDirectory("BooleanEmpty", Nothing)
        BooleanEmpty.HasChildDirectories = False
        Dim BooleanNonEmpty As DmsResourceItem = CreateDirectory("BooleanNonEmpty", Nothing)
        BooleanNonEmpty.HasChildDirectories = True
        Provider.SetChildren("/",
                             CreateDirectory("Empty", 0),
                             CreateDirectory("One", 1),
                             CreateDirectory("Many", 3),
                             BooleanEmpty,
                             BooleanNonEmpty,
                             CreateDirectory("Unknown", Nothing))
        Provider.SetChildren("Unknown", CreateDirectory("Unknown/Child", 0))

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()

            Dim Root As TreeNode = GetFolderTree(Browser).Nodes(0)
            ClassicAssert.AreEqual(1, Provider.DirectoryListingCount)
            ClassicAssert.AreEqual(0, FindNode(Root, "Empty").Nodes.Count)
            ClassicAssert.AreEqual(1, FindNode(Root, "One").Nodes.Count)
            ClassicAssert.AreEqual(1, FindNode(Root, "Many").Nodes.Count)
            ClassicAssert.AreEqual(0, FindNode(Root, "BooleanEmpty").Nodes.Count)
            ClassicAssert.AreEqual(1, FindNode(Root, "BooleanNonEmpty").Nodes.Count)

            Dim Unknown As TreeNode = FindNode(Root, "Unknown")
            ClassicAssert.AreEqual(1, Unknown.Nodes.Count)
            Browser.AddTreeChildren(Unknown)

            ClassicAssert.AreEqual(2, Provider.DirectoryListingCount)
            ClassicAssert.AreEqual(1, Unknown.Nodes.Count)
            ClassicAssert.AreEqual("Child", Unknown.Nodes(0).Text)
        End Using
    End Sub

    <Test>
    Public Sub RefreshChangesKnownZeroChildrenToOneChild()
        Dim Provider As New InMemoryDmsProvider
        Dim Parent As DmsResourceItem = CreateDirectory("Parent", 0)
        Provider.SetChildren("/", Parent)
        Provider.SetChildren("Parent")

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()
            Dim ParentNode As TreeNode = FindNode(GetFolderTree(Browser).Nodes(0), "Parent")
            ClassicAssert.AreEqual(0, ParentNode.Nodes.Count)

            Provider.SetChildren("Parent", CreateDirectory("Parent/Child", 0))
            Browser.RefreshTreeNode(ParentNode)

            ClassicAssert.AreEqual(1, ParentNode.Nodes.Count)
            ClassicAssert.AreEqual("Child", ParentNode.Nodes(0).Text)
            ClassicAssert.AreEqual(1, Parent.ChildDirectoryCount.Value)
            ClassicAssert.IsTrue(Parent.HasChildDirectories.Value)
        End Using
    End Sub

    <Test>
    Public Sub SelectedDirectoryReportsItsActualParentNode()
        Dim Provider As New InMemoryDmsProvider
        Provider.SetChildren("/", CreateDirectory("Parent", 0))

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()
            Dim Root As TreeNode = GetFolderTree(Browser).Nodes(0)
            GetFolderTree(Browser).SelectedNode = FindNode(Root, "Parent")

            Dim Method = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetMethod("CurrentParentOfSelectedFolderNode", System.Reflection.BindingFlags.Instance Or System.Reflection.BindingFlags.NonPublic)
            ClassicAssert.AreSame(Root, Method.Invoke(Browser, Nothing))
        End Using
    End Sub

    <Test>
    Public Sub FileListRefreshToleratesMissingTreeSelection()
        Dim Provider As New InMemoryDmsProvider
        Provider.SetChildren("/")

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            Browser.LoadTree()
            GetFolderTree(Browser).SelectedNode = Nothing

            Assert.DoesNotThrow(Sub() InvokeInstanceMethod(Browser, "RefreshFilesList"))
            ClassicAssert.AreEqual(0, Browser.ListViewDmsFiles.Items.Count)
        End Using
    End Sub

    <Test>
    Public Sub RefreshRestoresTheFolderShownInTheFileList()
        Dim Provider As New InMemoryDmsProvider
        Provider.SetChildren("/", CreateDirectory("Test-Temp", 0))
        Provider.SetChildren("Test-Temp", New DmsResourceItem With {
            .ItemType = DmsResourceItem.ItemTypes.File,
            .Name = "example.txt",
            .FullName = "Test-Temp/example.txt"
        })

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            Browser.LoadTree()
            Dim selectedNode As TreeNode = FindNode(GetFolderTree(Browser).Nodes(0), "Test-Temp")
            GetFolderTree(Browser).SelectedNode = selectedNode
            InvokeInstanceMethod(Browser, "TreeViewDmsFolders_AfterSelect", GetFolderTree(Browser), New TreeViewEventArgs(selectedNode))
            ClassicAssert.AreEqual("Test-Temp", Browser.SelectedFolder)
            ClassicAssert.AreEqual(1, Browser.ListViewDmsFiles.Items.Count)

            GetFolderTree(Browser).SelectedNode = Nothing
            Browser.SelectedFolder = Nothing
            Browser.RefreshCurrentFolderAndFiles()

            ClassicAssert.AreEqual("Test-Temp", Browser.SelectedFolder)
            ClassicAssert.AreEqual("Test-Temp", GetFolderTree(Browser).SelectedNode.Text)
            ClassicAssert.AreEqual(1, Browser.ListViewDmsFiles.Items.Count)
        End Using
    End Sub

    <Test>
    Public Sub RefreshChangesOneChildToKnownZeroChildren()
        Dim Provider As New InMemoryDmsProvider
        Dim Parent As DmsResourceItem = CreateDirectory("Parent", 1)
        Provider.SetChildren("/", Parent)
        Provider.SetChildren("Parent", CreateDirectory("Parent/Child", 0))

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()
            Dim ParentNode As TreeNode = FindNode(GetFolderTree(Browser).Nodes(0), "Parent")
            ClassicAssert.AreEqual(1, ParentNode.Nodes.Count)

            Provider.SetChildren("Parent")
            Browser.RefreshTreeNode(ParentNode)

            ClassicAssert.AreEqual(0, ParentNode.Nodes.Count)
            ClassicAssert.AreEqual(0, Parent.ChildDirectoryCount.Value)
            ClassicAssert.IsFalse(Parent.HasChildDirectories.Value)
        End Using
    End Sub

    <Test>
    Public Sub ExternallyDeletedFolderIsRemovedWithItsCachedContents()
        Dim provider As New InMemoryDmsProvider
        Dim parentResource As DmsResourceItem = CreateDirectory("Parent", 1)
        provider.SetChildren("/", parentResource)
        provider.SetChildren("Parent", CreateDirectory("Parent/Deleted", 0))
        provider.SetChildren("Parent/Deleted", New DmsResourceItem With {
            .ItemType = DmsResourceItem.ItemTypes.File,
            .Name = "stale.txt",
            .FullName = "Parent/Deleted/stale.txt"
        })

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            browser.LoadTree()
            Dim parent As TreeNode = FindNode(GetFolderTree(browser).Nodes(0), "Parent")
            browser.AddTreeChildren(parent)
            Dim deleted As TreeNode = FindNode(parent, "Deleted")
            GetFolderTree(browser).SelectedNode = deleted
            InvokeInstanceMethod(browser, "TreeViewDmsFolders_AfterSelect", GetFolderTree(browser), New TreeViewEventArgs(deleted))
            ClassicAssert.AreEqual(1, browser.ListViewDmsFiles.Items.Count)

            browser.RemoveMissingDirectory(deleted)

            ClassicAssert.AreEqual(0, parent.Nodes.Count)
            ClassicAssert.AreEqual(0, parentResource.ChildDirectoryCount.Value)
            ClassicAssert.AreSame(parent, GetFolderTree(browser).SelectedNode)
            ClassicAssert.AreEqual("Parent", browser.SelectedFolder)
            ClassicAssert.AreEqual(0, browser.ListViewDmsFiles.Items.Count)
            ClassicAssert.IsNull(browser.ListViewDmsFiles.Tag)
        End Using
    End Sub

    <Test>
    Public Sub ExternallyDeletedFileIsRemovedFromListAndCache()
        Dim provider As New InMemoryDmsProvider
        Dim stale As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "stale.txt", .FullName = "Folder/stale.txt"}
        Dim remaining As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "remaining.txt", .FullName = "Folder/remaining.txt"}
        provider.SetChildren("/", CreateDirectory("Folder", 0))
        provider.SetChildren("Folder", stale, remaining)

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            browser.LoadTree()
            Dim selectedNode As TreeNode = FindNode(GetFolderTree(browser).Nodes(0), "Folder")
            GetFolderTree(browser).SelectedNode = selectedNode
            InvokeInstanceMethod(browser, "TreeViewDmsFolders_AfterSelect", GetFolderTree(browser), New TreeViewEventArgs(selectedNode))
            ClassicAssert.AreEqual(2, browser.ListViewDmsFiles.Items.Count)

            browser.RemoveMissingFile(stale.FullName)

            ClassicAssert.AreEqual(1, browser.ListViewDmsFiles.Items.Count)
            ClassicAssert.AreEqual("remaining.txt", browser.ListViewDmsFiles.Items(0).Text)
            ClassicAssert.AreEqual(1, CType(browser.ListViewDmsFiles.Tag, List(Of DmsResourceItem)).Count)
            ClassicAssert.AreSame(remaining, CType(browser.ListViewDmsFiles.Tag, List(Of DmsResourceItem))(0))
        End Using
    End Sub

    <Test>
    Public Sub CreatingAndDeletingFirstChildUpdatesExpansionState()
        Dim Provider As New InMemoryDmsProvider
        Dim Parent As DmsResourceItem = CreateDirectory("Parent", 0)
        Provider.SetChildren("/", Parent)

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()
            Dim ParentNode As TreeNode = FindNode(GetFolderTree(Browser).Nodes(0), "Parent")
            Dim Child As DmsResourceItem = CreateDirectory("Parent/Child", 0)

            InvokeInstanceMethod(Browser, "AddDirectoryTreeNode", ParentNode, Child)
            InvokeSharedMethod("RecordChildDirectoryCreated", ParentNode)

            ClassicAssert.AreEqual(1, ParentNode.Nodes.Count)
            ClassicAssert.AreEqual(1, Parent.ChildDirectoryCount.Value)
            ClassicAssert.IsTrue(Parent.HasChildDirectories.Value)

            ParentNode.Nodes.RemoveAt(0)
            InvokeSharedMethod("RecordChildDirectoryDeleted", ParentNode)

            ClassicAssert.AreEqual(0, ParentNode.Nodes.Count)
            ClassicAssert.AreEqual(0, Parent.ChildDirectoryCount.Value)
            ClassicAssert.IsFalse(Parent.HasChildDirectories.Value)
        End Using
    End Sub

    <Test>
    Public Sub CollectionChildrenAreLoadedOnlyWhenCollectionIsExpanded()
        Dim Provider As New InMemoryDmsProvider
        Dim Collection As DmsResourceItem = CreateDirectory("Collection", Nothing, DmsResourceItem.ItemTypes.Collection)
        Provider.SetChildren("/", Collection)
        Provider.SetChildren("Collection",
                             CreateDirectory("Collection/First", 0),
                             CreateDirectory("Collection/Second", 0))

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()
            Dim CollectionNode As TreeNode = FindNode(GetFolderTree(Browser).Nodes(0), "Collection")

            ClassicAssert.AreEqual(1, Provider.DirectoryListingCount)
            ClassicAssert.AreEqual(1, CollectionNode.Nodes.Count)
            Browser.AddTreeChildren(CollectionNode)

            ClassicAssert.AreEqual(2, Provider.DirectoryListingCount)
            ClassicAssert.AreEqual(2, CollectionNode.Nodes.Count)
            ClassicAssert.AreEqual(2, Collection.ChildDirectoryCount.Value)
            ClassicAssert.IsTrue(Collection.HasChildDirectories.Value)
        End Using
    End Sub

    <Test>
    Public Sub RootFilesAreListedWhenProviderSupportsThem()
        Dim Provider As New InMemoryDmsProvider With {.RootPath = ""}
        Dim RootFile As New DmsResourceItem With {
            .Name = "root.txt",
            .FullName = "root.txt",
            .ItemType = DmsResourceItem.ItemTypes.File
        }
        Provider.SetChildren("", RootFile, CreateDirectory("Subfolder", 0))

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            Browser.LoadTree()
            Browser.TreeViewDmsFolders.SelectedNode = GetFolderTree(Browser).Nodes(0)
            InvokeInstanceMethod(Browser, "RefreshFilesList")

            ClassicAssert.AreEqual(1, Browser.ListViewDmsFiles.Items.Count)
            ClassicAssert.AreEqual("root.txt", Browser.ListViewDmsFiles.Items(0).Text)
            ClassicAssert.IsNotEmpty(Provider.FileListingPaths)
            ClassicAssert.IsTrue(Provider.FileListingPaths.All(Function(path) path = ""))
        End Using
    End Sub

    <Test>
    Public Sub RootFilesAreNotRequestedWhenProviderDoesNotSupportThem()
        Dim Provider As New InMemoryDmsProvider With {.RootPath = "", .SupportsRootFiles = False}
        Provider.SetChildren("")

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            Browser.LoadTree()
            Browser.TreeViewDmsFolders.SelectedNode = GetFolderTree(Browser).Nodes(0)
            InvokeInstanceMethod(Browser, "RefreshFilesList")

            ClassicAssert.AreEqual(0, Browser.ListViewDmsFiles.Items.Count)
            ClassicAssert.AreEqual(0, Provider.FileListingPaths.Count)
        End Using
    End Sub

    Private Shared Function CreateDirectory(fullName As String, childCount As Integer?, Optional itemType As DmsResourceItem.ItemTypes = DmsResourceItem.ItemTypes.Folder) As DmsResourceItem
        Dim SeparatorPosition As Integer = fullName.LastIndexOf("/"c)
        Return New DmsResourceItem With {
            .Name = If(SeparatorPosition < 0, fullName, fullName.Substring(SeparatorPosition + 1)),
            .FullName = fullName,
            .ItemType = itemType,
            .ChildDirectoryCount = childCount,
            .HasChildDirectories = If(childCount.HasValue, CType(childCount.Value > 0, Boolean?), Nothing)
        }
    End Function

    Private Shared Function GetFolderTree(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser) As TreeView
        Return browser.TreeViewDmsFolders
    End Function

    Private Shared Function FindNode(parent As TreeNode, text As String) As TreeNode
        Return parent.Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = text)
    End Function

    Private Shared Sub InvokeInstanceMethod(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser, methodName As String, ParamArray arguments As Object())
        Dim Method = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetMethod(methodName, System.Reflection.BindingFlags.Instance Or System.Reflection.BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(Method)
        Method.Invoke(browser, arguments)
    End Sub

    Private Shared Sub InvokeSharedMethod(methodName As String, ParamArray arguments As Object())
        Dim Method = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetMethod(methodName, System.Reflection.BindingFlags.Static Or System.Reflection.BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(Method)
        Method.Invoke(Nothing, arguments)
    End Sub

    Private Class InMemoryDmsProvider
        Inherits NoDmsProvider

        Private ReadOnly ChildrenByPath As New Dictionary(Of String, List(Of DmsResourceItem))(StringComparer.Ordinal)

        Public Property DirectoryListingCount As Integer
        Public Property RootPath As String = "/"
        Public Property SupportsRootFiles As Boolean = True
        Public ReadOnly Property FileListingPaths As New List(Of String)

        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return Me.RootPath
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return Me.SupportsRootFiles
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return True
            End Get
        End Property

        Public Sub SetChildren(parentPath As String, ParamArray children As DmsResourceItem())
            Me.ChildrenByPath(parentPath) = New List(Of DmsResourceItem)(children)
        End Sub

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Me.DirectoryListingCount += 1
            If searchType = SearchItemType.Files Then Me.FileListingPaths.Add(remoteFolderPath)
            Dim Children As List(Of DmsResourceItem) = Nothing
            If Not Me.ChildrenByPath.TryGetValue(remoteFolderPath, Children) Then Return New List(Of DmsResourceItem)
            Return New List(Of DmsResourceItem)(Children)
        End Function

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
        End Sub

    End Class

End Class
