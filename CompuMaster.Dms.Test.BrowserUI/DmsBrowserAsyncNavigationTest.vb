Option Explicit On
Option Strict On

Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserAsyncNavigationTest

    Private previousContext As SynchronizationContext
    Private uiContext As PumpSynchronizationContext
    Private previousAutoInstall As Boolean

    <SetUp>
    Public Sub InstallUiContext()
        previousContext = SynchronizationContext.Current
        previousAutoInstall = WindowsFormsSynchronizationContext.AutoInstall
        WindowsFormsSynchronizationContext.AutoInstall = False
        uiContext = New PumpSynchronizationContext()
        SynchronizationContext.SetSynchronizationContext(uiContext)
    End Sub

    <TearDown>
    Public Sub RestoreContext()
        SynchronizationContext.SetSynchronizationContext(previousContext)
        WindowsFormsSynchronizationContext.AutoInstall = previousAutoInstall
    End Sub

    <TestCase(0)>
    <TestCase(1)>
    <TestCase(2)>
    Public Sub DelayedExpansionRestoresBusyStateAndAllowsRetry(outcome As Integer)
        Dim provider As New DelayedUiProvider
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Finish(browser.LoadTreeAsync())
            Dim parent As TreeNode = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Parent")
            Dim pending = provider.DelayDirectory("Parent")
            Dim operation = browser.RunTransferAsync(Function() browser.AddTreeChildrenAsync(parent))
            ClassicAssert.IsFalse(operation.IsCompleted)
            ClassicAssert.IsTrue(browser.Enabled)
            ClassicAssert.IsFalse(browser.SplitContainer.Enabled)
            ClassicAssert.IsTrue(browser.UseWaitCursor)
            ClassicAssert.IsNull(parent.Nodes(0).Tag, "The expansion placeholder must remain until loading succeeds.")
            Select Case outcome
                Case 0
                    pending.SetResult(New List(Of DmsResourceItem) From {DirectoryItem("Parent/Child", 0)})
                Case 1
                    pending.SetException(New InvalidOperationException("Listing failed."))
                Case 2
                    pending.SetCanceled()
            End Select
            PumpUntil(Function() operation.IsCompleted)
            If outcome = 0 Then
                operation.GetAwaiter().GetResult()
            ElseIf outcome = 1 Then
                Assert.Throws(Of InvalidOperationException)(Sub() operation.GetAwaiter().GetResult())
            Else
                Assert.Throws(Of TaskCanceledException)(Sub() operation.GetAwaiter().GetResult())
            End If
            ClassicAssert.IsTrue(browser.Enabled)
            ClassicAssert.IsFalse(browser.UseWaitCursor)
            If outcome <> 0 Then
                ClassicAssert.IsNull(parent.Nodes(0).Tag)
                provider.PendingDirectories.Remove("Parent")
                Finish(browser.RunTransferAsync(Function() browser.AddTreeChildrenAsync(parent)))
            End If
            ClassicAssert.AreEqual("Child", parent.Nodes(0).Text)
            ClassicAssert.AreEqual(1, provider.Parent.ChildDirectoryCount)
            Dim count As Integer = provider.DirectoryCalls
            Finish(browser.AddTreeChildrenAsync(parent))
            ClassicAssert.AreEqual(count, provider.DirectoryCalls)
        End Using
    End Sub

    <Test>
    Public Sub RepeatedExpansionSharesOnePendingDirectoryRequest()
        Dim provider As New DelayedUiProvider
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Finish(browser.LoadTreeAsync())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Parent")
            Dim pending = provider.DelayDirectory("Parent")
            Dim first = browser.AddTreeChildrenAsync(node)
            Dim second = browser.AddTreeChildrenAsync(node)
            ClassicAssert.AreEqual(2, provider.DirectoryCalls, "Root plus one shared child request.")
            pending.SetResult(New List(Of DmsResourceItem) From {DirectoryItem("Parent/Child", 0)})
            Finish(Task.WhenAll(first, second))
            ClassicAssert.AreEqual(1, node.Nodes.Count)
        End Using
    End Sub

    <Test>
    Public Sub KnownEmptyDirectoryDoesNotStartAChildRequest()
        Dim provider As New DelayedUiProvider
        provider.Parent.ChildDirectoryCount = 0
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Finish(browser.LoadTreeAsync())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Parent")
            Finish(browser.AddTreeChildrenAsync(node))
            ClassicAssert.AreEqual(1, provider.DirectoryCalls)
            ClassicAssert.AreEqual(0, node.Nodes.Count)
        End Using
    End Sub

    <Test>
    Public Sub SortingLoadedFilesDoesNotFetchThemAgain()
        Dim provider As New DelayedUiProvider
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            Finish(browser.LoadTreeAsync())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Parent")
            SelectWithoutRefresh(browser, node)
            Dim pending = provider.DelayFiles("Parent")
            pending.SetResult(New List(Of DmsResourceItem) From {FileItem("Parent/z.txt"), FileItem("Parent/a.txt")})
            Finish(browser.RefreshFilesListAsync())
            Dim calls As Integer = provider.FileCalls
            Dim sortHandler = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetMethod("ListViewDmsFiles_ColumnClick", BindingFlags.NonPublic Or BindingFlags.Instance)
            sortHandler.Invoke(browser, {browser.ListViewDmsFiles, New ColumnClickEventArgs(0)})
            ClassicAssert.AreEqual("z.txt", browser.ListViewDmsFiles.Items(0).Text)
            sortHandler.Invoke(browser, {browser.ListViewDmsFiles, New ColumnClickEventArgs(0)})
            ClassicAssert.AreEqual(calls, provider.FileCalls)
            ClassicAssert.AreEqual("a.txt", browser.ListViewDmsFiles.Items(0).Text)
            ClassicAssert.AreEqual("z.txt", browser.ListViewDmsFiles.Items(1).Text)
        End Using
    End Sub

    <Test>
    Public Sub BeforeExpandCancelsImmediatelyWhileChildrenLoad()
        Dim provider As New DelayedUiProvider
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Finish(browser.LoadTreeAsync())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Parent")
            Dim pending = provider.DelayDirectory("Parent")
            Dim args As New TreeViewCancelEventArgs(node, False, TreeViewAction.Expand)
            GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetMethod("TreeViewDmsFolders_BeforeExpand", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser, {browser.TreeViewDmsFolders, args})
            ClassicAssert.IsTrue(args.Cancel)
            ClassicAssert.IsTrue(browser.Enabled)
            ClassicAssert.IsFalse(browser.SplitContainer.Enabled)
            pending.SetResult(New List(Of DmsResourceItem) From {DirectoryItem("Parent/Child", 0)})
            PumpUntil(Function() browser.SplitContainer.Enabled)
            ClassicAssert.AreEqual("Child", node.Nodes(0).Text)
        End Using
    End Sub

    <Test>
    Public Sub OlderFolderResponseCannotReplaceTheNewSelection()
        Dim provider As New DelayedUiProvider
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            browser.BrowseMode = Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles
            Finish(browser.LoadTreeAsync())
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            Dim oldResponse = provider.DelayFiles("Parent")
            Dim newResponse = provider.DelayFiles("Other")
            SelectWithoutRefresh(browser, root.Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Parent"))
            Dim first = browser.RefreshFilesListAsync()
            SelectWithoutRefresh(browser, root.Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Other"))
            Dim second = browser.RefreshFilesListAsync()
            newResponse.SetResult(New List(Of DmsResourceItem) From {FileItem("Other/new.txt")})
            Finish(second)
            oldResponse.SetResult(New List(Of DmsResourceItem) From {FileItem("Parent/stale.txt")})
            Finish(first)
            ClassicAssert.AreEqual(1, browser.ListViewDmsFiles.Items.Count)
            ClassicAssert.AreEqual("new.txt", browser.ListViewDmsFiles.Items(0).Text)
        End Using
    End Sub

    <Test>
    Public Sub CreateAndDeleteAwaitProviderAndUpdateChildMetadata()
        Dim provider As New DelayedUiProvider
        provider.Parent.ChildDirectoryCount = 0
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Finish(browser.LoadTreeAsync())
            Dim parent = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(n) n.Text = "Parent")
            Dim created As TreeNode = Nothing
            provider.PendingCreate = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim createTask = browser.RunTransferAsync(Async Function()
                                                         created = Await browser.CreateNewDirectoryTreeNodeAsync(parent, "Created")
                                                     End Function)
            ClassicAssert.IsFalse(createTask.IsCompleted)
            ClassicAssert.IsTrue(browser.Enabled)
            ClassicAssert.IsFalse(browser.SplitContainer.Enabled)
            ClassicAssert.AreEqual(0, parent.Nodes.Count)
            provider.PendingCreate.SetResult(True)
            Finish(createTask)
            ClassicAssert.AreEqual("Created", created.Text)
            ClassicAssert.AreEqual(1, provider.Parent.ChildDirectoryCount)
            ClassicAssert.AreEqual(1, provider.DirectoryCalls, "Known-empty parents must not be fetched before creation.")

            provider.PendingDelete = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim deleteTask = browser.RunTransferAsync(Function() browser.DeleteDirectoryForUiAsync(created))
            ClassicAssert.IsFalse(deleteTask.IsCompleted)
            ClassicAssert.AreEqual(1, parent.Nodes.Count)
            ClassicAssert.AreSame(provider.CreatedItem, provider.DeletedItems.Single())
            provider.PendingDelete.SetResult(True)
            Finish(deleteTask)
            ClassicAssert.AreEqual(0, parent.Nodes.Count)
            ClassicAssert.AreEqual(0, provider.Parent.ChildDirectoryCount)
            ClassicAssert.AreSame(parent, browser.TreeViewDmsFolders.SelectedNode)
            ClassicAssert.IsTrue(browser.Enabled)
        End Using
    End Sub

    <Test>
    Public Sub DeleteBatchWaitsForEachItemAndPreservesIdentity()
        Dim provider As New DelayedUiProvider
        provider.PendingDelete = New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim first = FileItem("Parent/duplicate.txt")
        Dim second = FileItem("Parent/duplicate.txt")
        first.ExtendedInfosFileID = "first"
        second.ExtendedInfosFileID = "second"
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Dim operation = browser.RunTransferAsync(Function() browser.DeleteFilesForUiAsync({first, second}))
            ClassicAssert.AreEqual(1, provider.DeletedItems.Count)
            provider.PendingDelete.SetResult(True)
            Finish(operation)
            ClassicAssert.AreSame(first, provider.DeletedItems(0))
            ClassicAssert.AreSame(second, provider.DeletedItems(1))
            ClassicAssert.IsTrue(browser.Enabled)
        End Using
    End Sub

    Private Shared Sub SelectWithoutRefresh(browser As Global.CompuMaster.Dms.BrowserUI.DmsBrowser, node As TreeNode)
        Dim field = GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).GetField("SuppressSelectionRefresh", BindingFlags.Instance Or BindingFlags.NonPublic)
        field.SetValue(browser, True)
        Try
            browser.TreeViewDmsFolders.SelectedNode = node
        Finally
            field.SetValue(browser, False)
        End Try
    End Sub

    Private Shared Sub Finish(operation As Task)
        PumpUntil(Function() operation.IsCompleted)
        operation.GetAwaiter().GetResult()
    End Sub

    Private Shared Sub PumpUntil(condition As Func(Of Boolean))
        Dim deadline = DateTime.UtcNow.AddSeconds(5)
        While Not condition() AndAlso DateTime.UtcNow < deadline
            DirectCast(SynchronizationContext.Current, PumpSynchronizationContext).Drain()
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        ClassicAssert.IsTrue(condition(), "The pending UI operation did not complete.")
    End Sub

    Private Shared Function DirectoryItem(path As String, count As Integer?) As DmsResourceItem
        Return New DmsResourceItem With {.FullName = path, .Name = path.Split("/"c).Last(), .ItemType = DmsResourceItem.ItemTypes.Folder, .ChildDirectoryCount = count}
    End Function

    Private Shared Function FileItem(path As String) As DmsResourceItem
        Return New DmsResourceItem With {.FullName = path, .Name = path.Split("/"c).Last(), .ItemType = DmsResourceItem.ItemTypes.File}
    End Function

    'A deterministic STA dispatcher keeps continuations on the test's UI thread.
    Private Class PumpSynchronizationContext
        Inherits SynchronizationContext

        Private ReadOnly callbacks As New System.Collections.Concurrent.ConcurrentQueue(Of Action)

        Public Overrides Sub Post(callback As SendOrPostCallback, state As Object)
            callbacks.Enqueue(Sub() callback(state))
        End Sub

        Public Sub Drain()
            Dim callback As Action = Nothing
            While callbacks.TryDequeue(callback)
                callback()
            End While
        End Sub
    End Class

    Private Class DelayedUiProvider
        Inherits NoDmsProvider

        Public ReadOnly Parent As DmsResourceItem = DirectoryItem("Parent", Nothing)
        Public ReadOnly PendingDirectories As New Dictionary(Of String, TaskCompletionSource(Of List(Of DmsResourceItem)))
        Public ReadOnly PendingFiles As New Dictionary(Of String, TaskCompletionSource(Of List(Of DmsResourceItem)))
        Public ReadOnly DeletedItems As New List(Of DmsResourceItem)
        Public PendingCreate As TaskCompletionSource(Of Boolean)
        Public PendingDelete As TaskCompletionSource(Of Boolean)
        Public CreatedItem As DmsResourceItem
        Public DirectoryCalls As Integer
        Public FileCalls As Integer

        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return "/"
            End Get
        End Property

        Public Function DelayDirectory(path As String) As TaskCompletionSource(Of List(Of DmsResourceItem))
            Dim pending As New TaskCompletionSource(Of List(Of DmsResourceItem))(TaskCreationOptions.RunContinuationsAsynchronously)
            PendingDirectories.Add(path, pending)
            Return pending
        End Function

        Public Function DelayFiles(path As String) As TaskCompletionSource(Of List(Of DmsResourceItem))
            Dim pending As New TaskCompletionSource(Of List(Of DmsResourceItem))(TaskCreationOptions.RunContinuationsAsynchronously)
            PendingFiles.Add(path, pending)
            Return pending
        End Function

        Public Overrides Function ListAllDirectoryItemsAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            DirectoryCalls += 1
            If PendingDirectories.ContainsKey(path) Then Return PendingDirectories(path).Task
            Return Task.FromResult(If(path = "/", New List(Of DmsResourceItem) From {Parent, DirectoryItem("Other", 0)}, New List(Of DmsResourceItem) From {DirectoryItem("Parent/Child", 0)}))
        End Function

        Public Overrides Function ListAllFileItemsAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            FileCalls += 1
            If PendingFiles.ContainsKey(path) Then Return PendingFiles(path).Task
            Return Task.FromResult(New List(Of DmsResourceItem))
        End Function

        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function

        Public Overrides Function CreateDirectoryAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            CreatedItem = DirectoryItem(path, Nothing)
            Return PendingCreate.Task
        End Function

        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Task.FromResult(CreatedItem)
        End Function

        Public Overrides Function DeleteRemoteItemAsync(item As DmsResourceItem, Optional cancellationToken As CancellationToken = Nothing) As Task
            DeletedItems.Add(item)
            Return PendingDelete.Task
        End Function
    End Class
End Class
