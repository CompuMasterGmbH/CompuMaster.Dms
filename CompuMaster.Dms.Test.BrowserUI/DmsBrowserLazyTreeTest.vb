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
    Public Sub DirectoryPropertiesShowKnownChildStateWithoutFetchingChildren()
        Dim Provider As New InMemoryDmsProvider
        Dim Folder As DmsResourceItem = CreateDirectory("Folder", Nothing)

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            StringAssert.Contains(Environment.NewLine & "- HasChildDirectories: " & Environment.NewLine, Browser.PropertiesDetails(Folder))

            Dim Collection As DmsResourceItem = CreateDirectory("Collection", Nothing, DmsResourceItem.ItemTypes.Collection)
            Collection.HasChildDirectories = True
            StringAssert.Contains(Environment.NewLine & "- HasChildDirectories: True" & Environment.NewLine, Browser.PropertiesDetails(Collection))

            Folder.HasChildDirectories = False
            StringAssert.Contains(Environment.NewLine & "- HasChildDirectories: False" & Environment.NewLine, Browser.PropertiesDetails(Folder))

            Folder.HasChildDirectories = True
            StringAssert.Contains(Environment.NewLine & "- HasChildDirectories: True" & Environment.NewLine, Browser.PropertiesDetails(Folder))

            Folder.ChildDirectoryCount = 0
            StringAssert.Contains(Environment.NewLine & "- HasChildDirectories: False" & Environment.NewLine, Browser.PropertiesDetails(Folder))

            Folder.ChildDirectoryCount = 2
            Folder.HasChildDirectories = False
            StringAssert.Contains(Environment.NewLine & "- HasChildDirectories: True" & Environment.NewLine, Browser.PropertiesDetails(Folder))
            ClassicAssert.AreEqual(0, Provider.DirectoryListingCount)
        End Using
    End Sub

    <Test>
    Public Sub FilePropertiesShowFalseChildDirectoryState()
        Dim File As New DmsResourceItem With {
            .Name = "File.txt",
            .FullName = "File.txt",
            .ItemType = DmsResourceItem.ItemTypes.File
        }

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(New InMemoryDmsProvider)
            StringAssert.Contains(Environment.NewLine & "- HasChildDirectories: False" & Environment.NewLine, Browser.PropertiesDetails(File))
        End Using
    End Sub

    <Test>
    Public Sub PropertiesIndentDynamicFieldsAndLinkDetails()
        Dim Collection As DmsResourceItem = CreateDirectory("Collection", Nothing, DmsResourceItem.ItemTypes.Collection)
        Collection.ExtendedInfosVersionDateLocalTime = New DateTime(2026, 1, 2)
        Collection.ExtendedInfosLocks = New List(Of String) From {"sample-lock"}
        Dim Link As New DmsLink(Collection, "sample-link", Nothing, Nothing) With {.WebUrl = "https://example.test/link"}
        Collection.ExtendedInfosLinks = New List(Of DmsLink) From {Link}

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(New InMemoryDmsProvider)
            Dim PropertiesText As String = Browser.PropertiesDetails(Collection)
            Dim NewLine As String = Environment.NewLine
            StringAssert.Contains(Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesFullPath") & "Collection" & NewLine, PropertiesText)
            StringAssert.Contains(Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesDetails") & NewLine & "- " & Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesOwner"), PropertiesText)
            StringAssert.Contains(NewLine & "- " & Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesVersionDate"), PropertiesText)
            StringAssert.Contains(NewLine & "- " & Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesLocks") & NewLine & "  - sample-lock", PropertiesText)
            StringAssert.Contains(Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesSharings") & NewLine & "- IsShared: False", PropertiesText)
            StringAssert.Contains(NewLine & "- IsPublicCollection: False", PropertiesText)
            StringAssert.Contains(NewLine & "- Link: sample-link" & NewLine & "  - WebUrl: https://example.test/link", PropertiesText)
            StringAssert.Contains(Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesExtendedInformation") & NewLine & "- " & Global.CompuMaster.Dms.BrowserUI.UiStrings.GetText("PropertiesLastModification"), PropertiesText)
        End Using
    End Sub

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

    <TestCase(False)>
    <TestCase(True)>
    Public Sub TransferWaitCursorRestoresPreviousStateAfterSuccessOrFailure(fail As Boolean)
        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(New InMemoryDmsProvider)
            Dim PreviousCursor As Cursor = Cursor.Current
            Dim Operation As Action = Sub()
                                          ClassicAssert.IsTrue(Browser.UseWaitCursor)
                                          ClassicAssert.AreEqual(Cursors.WaitCursor, Cursor.Current)
                                          If fail Then Throw New InvalidOperationException("Transfer failed.")
                                      End Sub
            If fail Then
                Assert.Throws(Of InvalidOperationException)(Sub() Browser.RunWithWaitCursor(Operation))
            Else
                Browser.RunWithWaitCursor(Operation)
            End If
            ClassicAssert.IsFalse(Browser.UseWaitCursor)
            ClassicAssert.AreEqual(PreviousCursor, Cursor.Current)
        End Using
    End Sub

    <Test>
    Public Sub CreatingNestedDirectoryImmediatelyKeepsBothNodesAttached()
        Dim Provider As New InMemoryDmsProvider
        Provider.SetChildren("/", CreateDirectory("Parent", 0))

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()
            Dim Tree As TreeView = GetFolderTree(Browser)
            Dim ParentNode As TreeNode = FindNode(Tree.Nodes(0), "Parent")
            Dim First As TreeNode = Browser.CreateNewDirectoryTreeNode(ParentNode, "First")
            Tree.SelectedNode = First
            Dim Second As TreeNode = Browser.CreateNewDirectoryTreeNode(Tree.SelectedNode, "Second")
            'Selecting a child can expand its parent and trigger lazy loading.
            Browser.AddTreeChildren(First)
            Tree.SelectedNode = Second

            ClassicAssert.AreEqual("Parent/First/Second", Provider.CreatedDirectory.FullName)
            ClassicAssert.AreSame(Tree, First.TreeView)
            ClassicAssert.AreSame(Tree, Second.TreeView)
            ClassicAssert.AreSame(Second, Tree.SelectedNode)
            ClassicAssert.AreSame(First, Second.Parent)
            ClassicAssert.AreEqual(1, First.Nodes.Count)
            ClassicAssert.AreEqual(0, Second.Nodes.Count)
        End Using
    End Sub

    <Test>
    Public Sub NewlyCreatedDirectoryWithoutChildMetadataHasNoExpansionPlaceholder()
        Dim Provider As New InMemoryDmsProvider
        Dim Parent As DmsResourceItem = CreateDirectory("Parent", 0)
        Provider.SetChildren("/", Parent)

        Using Browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(Provider)
            Browser.LoadTree()
            Dim ParentNode As TreeNode = FindNode(GetFolderTree(Browser).Nodes(0), "Parent")
            Dim ListingCount As Integer = Provider.DirectoryListingCount
            Dim NewNode As TreeNode = Browser.CreateNewDirectoryTreeNode(ParentNode, "NewFolder")

            ClassicAssert.AreEqual("Parent/NewFolder", Provider.CreatedDirectory.FullName)
            ClassicAssert.AreEqual("NewFolder", NewNode.Text)
            ClassicAssert.AreEqual(0, NewNode.Nodes.Count)
            ClassicAssert.AreEqual(0, Provider.CreatedDirectory.ChildDirectoryCount.Value)
            ClassicAssert.IsFalse(Provider.CreatedDirectory.HasChildDirectories.Value)
            ClassicAssert.AreEqual(1, Parent.ChildDirectoryCount.Value)
            ClassicAssert.IsTrue(Parent.HasChildDirectories.Value)
            ClassicAssert.AreEqual(ListingCount, Provider.DirectoryListingCount)
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

        Public Property CreatedDirectory As DmsResourceItem

        Public Overrides Sub CreateDirectory(remoteDirectoryPath As String)
            Me.CreatedDirectory = DmsBrowserLazyTreeTest.CreateDirectory(remoteDirectoryPath, Nothing)
        End Sub

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            ClassicAssert.AreEqual(Me.CreatedDirectory.FullName, remotePath)
            Return Me.CreatedDirectory
        End Function

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
