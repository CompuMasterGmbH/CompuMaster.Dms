Imports System.ComponentModel
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable, Apartment(ApartmentState.STA)>
Public Class DmsBrowserRootActionTest
    <TestCase("Scoped", DmsResourceItem.ItemTypes.Collection)>
    <TestCase("Scoped", DmsResourceItem.ItemTypes.Folder)>
    <TestCase("", DmsResourceItem.ItemTypes.Root)>
    Public Sub RootMenuHidesMutationsAndRestoresPermittedChildActions(initialRoot As String, rootType As DmsResourceItem.ItemTypes)
        Using dispatcher As New UiTestDispatcher, browser As New RootBrowser(New RootProvider With {.RootType = rootType}, initialRoot)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.StartupFailure, [Is].Null, If(browser.StartupFailure?.ToString(), ""))
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            AssertMutations(browser, False, False)
            OpenMenu(browser)
            AssertMutations(browser, False, False)
            Assert.That(browser.ToolStripFolderContextButtonNewFolder.Available, [Is].True)
            Assert.That(browser.ToolStripFolderContextButtonRefreshFilesList.Available, [Is].True)
            Assert.That(browser.ToolStripFolderContextButtonProperties.Available, [Is].True)
            Assert.That(browser.ContextMenuStripFolder.Items("DownloadFolderContext").Available, [Is].True)
            Assert.That(browser.ContextMenuStripFolder.Items("DownloadFolderContext").Enabled, [Is].True)
            browser.TreeViewDmsFolders.SelectedNode = root.Nodes(0)
            dispatcher.WaitForEvents()
            OpenMenu(browser)
            AssertMutations(browser, True, True)
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowDeleteFiles
            OpenMenu(browser)
            AssertMutations(browser, False, True)
            browser.TreeViewDmsFolders.SelectedNode = root
            dispatcher.WaitForEvents()
            OpenMenu(browser)
            AssertMutations(browser, False, False)
            dispatcher.Finish(browser.RunTransferAsync(Function() Task.CompletedTask))
            AssertMutations(browser, False, False)
            browser.TreeViewDmsFolders.SelectedNode = root.Nodes(0)
            dispatcher.WaitForEvents()
            OpenMenu(browser)
            AssertMutations(browser, False, True)
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowSelectOnly
            OpenMenu(browser)
            AssertMutations(browser, False, False)
        End Using
    End Sub

    <TestCase(DmsResourceItem.ItemTypes.Collection)>
    <TestCase(DmsResourceItem.ItemTypes.Folder)>
    Public Sub DirectRootDeletionIsRejectedBeforeCallingTheProvider(rootType As DmsResourceItem.ItemTypes)
        Dim provider As New RootProvider With {.RootType = rootType}
        Using dispatcher As New UiTestDispatcher, browser As New RootBrowser(provider, "Scoped")
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.StartupFailure, [Is].Null, If(browser.StartupFailure?.ToString(), ""))
            Assert.Throws(Of DmsUserInputInvalidException)(Sub() dispatcher.Finish(browser.DeleteDirectoryForUiAsync(browser.TreeViewDmsFolders.Nodes(0))))
            Assert.That(provider.DeleteCalls, [Is].Zero)
            'A stale command invocation must also fail before opening a confirmation or touching the server.
            GetType(DmsBrowser).GetMethod("ToolStripFolderContextButtonDeleteFolder_Click", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser, {browser.ToolStripFolderContextButtonDeleteFolder, EventArgs.Empty})
            dispatcher.WaitForEvents()
            Assert.That(browser.StartupFailure, [Is].InstanceOf(Of DmsUserErrorMessageException)())
            Assert.That(provider.DeleteCalls, [Is].Zero)
            Assert.That(browser.TreeViewDmsFolders.Nodes.Count, [Is].EqualTo(1))
            browser.TreeViewDmsFolders.SelectedNode = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            dispatcher.WaitForEvents()
            dispatcher.Finish(browser.DeleteDirectoryForUiAsync(browser.TreeViewDmsFolders.SelectedNode))
            Assert.That(provider.DeleteCalls, [Is].EqualTo(1), "Ordinary child folders remain deletable.")
        End Using
    End Sub

    Private Shared Sub OpenMenu(browser As DmsBrowser)
        Dim args As New CancelEventArgs
        GetType(ContextMenuStrip).GetMethod("OnOpening", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser.ContextMenuStripFolder, {args})
        Assert.That(args.Cancel, [Is].False)
    End Sub

    Private Shared Sub AssertMutations(browser As DmsBrowser, copyRenameMove As Boolean, delete As Boolean)
        For Each button As ToolStripButton In {browser.ToolStripFolderContextButtonCopyFolder, browser.ToolStripFolderContextButtonRenameFolder, browser.ToolStripFolderContextButtonMoveFolder}
            Assert.That(button.Available, [Is].EqualTo(copyRenameMove), button.Name)
            Assert.That(button.Enabled, [Is].EqualTo(copyRenameMove), button.Name)
        Next
        Assert.That(browser.ToolStripFolderContextButtonDeleteFolder.Available, [Is].EqualTo(delete))
        Assert.That(browser.ToolStripFolderContextButtonDeleteFolder.Enabled, [Is].EqualTo(delete))
    End Sub

    Private Class RootBrowser
        Inherits DmsBrowser
        Public StartupFailure As Exception
        Public Sub New(provider As BaseDmsProvider, initialRoot As String)
            MyBase.New(provider, Nothing, "Root action test", Nothing, initialRoot, Nothing, BrowseModes.FoldersAndFiles,
                       FileOrFolderActions.AllowCopyRenameMoveFiles Or FileOrFolderActions.AllowDeleteFiles Or FileOrFolderActions.AllowDownloadFiles Or FileOrFolderActions.AllowCreateFolders,
                       DialogOperationModes.NoResults, "", "", "")
            AddHandler OperationFailed, Sub(sender, e) StartupFailure = e.Exception
            ShowInTaskbar = False
            Opacity = 0
        End Sub
        Protected Overrides Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
            'Captured through OperationFailed above.
        End Sub
    End Class

    Private Class RootProvider
        Inherits NoDmsProvider
        Public RootType As DmsResourceItem.ItemTypes
        Public DeleteCalls As Integer
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Task.FromResult(New DmsResourceItem With {.FullName = path, .Name = path, .ItemType = RootType, .HasChildDirectories = True})
        End Function
        Public Overrides Function ListEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(New List(Of DmsResourceItem) From {New DmsResourceItem With {.FullName = CombinePath(path, "Child"), .Name = "Child", .ItemType = DmsResourceItem.ItemTypes.Folder, .HasChildDirectories = False}})
        End Function
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return ListEntriesAsync(path, cancellationToken)
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(New List(Of DmsResourceItem))
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
        Public Overrides Function DeleteRemoteItemAsync(item As DmsResourceItem, Optional cancellationToken As CancellationToken = Nothing) As Task
            DeleteCalls += 1
            Return Task.CompletedTask
        End Function
    End Class
End Class
