Imports System.Reflection
Imports System.Runtime.InteropServices.ComTypes
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class RemoteFileMoveTest
    <TestCase(False), TestCase(True)>
    Public Sub FailedInstanceValidationDuringHoverOrCommitPreservesTheOriginalDiagnostic(failAtCommit As Boolean)
        Dim provider As New MoveProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New MoveBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Dim tree = browser.TreeViewDmsFolders
            tree.SelectedNode = tree.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "source")
            dispatcher.WaitForEvents()
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or DmsBrowser.FileOrFolderActions.AllowDownloadFiles
            Dim failure As New InvalidOperationException("Fixture instance validation failed")
            Dim effect As DragDropEffects
            browser.ShellDragOperation = Function(data, dropSource)
                                             Dim destination = tree.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "destination")
                                             Dim point = tree.PointToScreen(New Drawing.Point(destination.Bounds.Left + 2, destination.Bounds.Top + 2))
                                             Dim over As New DragEventArgs(New DataObject(data), 1, point.X, point.Y, DragDropEffects.Copy Or DragDropEffects.Move, DragDropEffects.None)
                                             If Not failAtCommit Then provider.InstanceFailure = failure
                                             RaiseDrag(tree, "OnDragOver", over)
                                             effect = over.Effect
                                             RaiseDrag(tree, "OnDragDrop", over)
                                             If failAtCommit Then provider.InstanceFailure = failure
                                             Return If(failAtCommit, &H40100, &H40101)
                                         End Function
            GetType(ListView).GetMethod("OnItemDrag", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(browser.ListViewDmsFiles, {New ItemDragEventArgs(MouseButtons.Left, browser.ListViewDmsFiles.Items(0))})
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].SameAs(failure))
            Assert.That(effect, [Is].EqualTo(If(failAtCommit, DragDropEffects.Move, DragDropEffects.None)))
            Assert.That(provider.Moved, [Is].Empty)
            provider.InstanceFailure = Nothing
            Assert.That(browser.CanDragDownload(), [Is].True)
        End Using
    End Sub

    <Test>
    Public Sub FailedInstanceSnapshotDoesNotEscapeTheCallbackOrLeaveTheDragBusy()
        Dim provider As New MoveProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New MoveBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            browser.TreeViewDmsFolders.SelectedNode = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "source")
            dispatcher.WaitForEvents()
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or DmsBrowser.FileOrFolderActions.AllowDownloadFiles
            Dim shellCalls As Integer
            browser.ShellDragOperation = Function(data, dropSource)
                                             shellCalls += 1
                                             Return &H40101
                                         End Function
            Dim failure As New InvalidOperationException("Fixture current instance could not be read")
            provider.InstanceFailure = failure
            GetType(ListView).GetMethod("OnItemDrag", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(browser.ListViewDmsFiles, {New ItemDragEventArgs(MouseButtons.Left, browser.ListViewDmsFiles.Items(0))})
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].SameAs(failure))
            Assert.That(shellCalls, [Is].Zero)
            Assert.That(provider.Moved, [Is].Empty)
            provider.InstanceFailure = Nothing
            Assert.That(browser.CanDragDownload(), [Is].True, "A failed context snapshot must release the drag operation lock.")
        End Using
    End Sub

    <TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5), TestCase(6), TestCase(7), TestCase(8), TestCase(9), TestCase(10)>
    Public Sub NativePayloadRoutesOnlyAValidInternalMoveAndPreservesPartialResults(scenario As Integer)
        Dim provider As New MoveProvider With {.SecondFile = scenario = 3 OrElse scenario = 5, .DuplicateIdentity = scenario = 3, .AmbiguousDestination = scenario = 4, .FailSecond = scenario = 5,
            .RootFiles = scenario <> 7 AndAlso scenario <> 8, .ScopeRoot = If(scenario = 8, "Scoped", "")}
        Using dispatcher As New UiTestDispatcher(), browser As New MoveBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim tree = browser.TreeViewDmsFolders
            Dim source = tree.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "source")
            Dim destination = tree.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "destination")
            tree.SelectedNode = source
            dispatcher.WaitForEvents()
            For Each row As ListViewItem In browser.ListViewDmsFiles.Items
                row.Selected = True
            Next
            Dim origin = browser.ListViewDmsFiles.Items(0)
            Dim offered As DragDropEffects
            Dim deferred As Integer = -1
            Dim descriptors As Integer
            browser.ShellDragOperation = Function(data, dropSource)
                                             Dim format = ShellDownloadData.FileFormat(ShellDownloadData.DescriptorFormat, -1, TYMED.TYMED_HGLOBAL)
                                             descriptors = data.QueryGetData(format)
                                             Dim payload As New DataObject(data)
                                             Dim target = If(scenario = 7 OrElse scenario = 8, tree.Nodes(0), If(scenario = 2, source, destination))
                                             Dim point = tree.PointToScreen(New Drawing.Point(target.Bounds.Left + 2, target.Bounds.Top + 2))
                                             Dim over As New DragEventArgs(payload, 1, point.X, point.Y, DragDropEffects.Copy Or DragDropEffects.Move, DragDropEffects.None)
                                             RaiseDrag(tree, "OnDragOver", over)
                                             offered = over.Effect
                                             If scenario = 1 Then browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowSelectOnly
                                             If scenario = 6 Then browser.InitialFolder = "other-scope"
                                             If scenario = 9 Then GetType(DmsBrowser).GetField("_DmsProviderOverride", BindingFlags.NonPublic Or BindingFlags.Instance).SetValue(browser, New NoDmsProvider())
                                             If scenario = 10 Then provider.InstanceId = "changed-instance"
                                             RaiseDrag(tree, "OnDragDrop", over)
                                             deferred = provider.Moved.Count
                                             Return &H40100
                                         End Function
            GetType(ListView).GetMethod("OnItemDrag", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(browser.ListViewDmsFiles, {New ItemDragEventArgs(MouseButtons.Left, origin)})
            dispatcher.WaitForEvents()
            Assert.That(deferred, [Is].Zero, "No remote write may run inside the native Drop callback.")
            Assert.That(descriptors, [Is].Not.Zero, "Move-only permission must not expose downloads to the shell.")
            Assert.That(offered, [Is].EqualTo(If(scenario = 2 OrElse scenario = 3 OrElse scenario = 7, DragDropEffects.None, DragDropEffects.Move)))
            Assert.That(provider.Moved.Count, [Is].EqualTo(If(scenario = 0 OrElse scenario = 5 OrElse scenario = 8, 1, 0)), browser.Failure?.ToString())
            If scenario = 0 OrElse scenario = 5 OrElse scenario = 8 Then
                Assert.That(provider.Moved(0).ExtendedInfosFileID, [Is].EqualTo("file-1"))
                Assert.That(provider.Destinations(0), [Is].EqualTo(If(scenario = 8, "Scoped/first.txt", "destination/first.txt")))
                Assert.That(tree.SelectedNode.Text, [Is].EqualTo("source"))
            End If
            Assert.That(browser.Failure IsNot Nothing, [Is].EqualTo(scenario = 4 OrElse scenario = 5), browser.Failure?.ToString())
        End Using
    End Sub

    <Test>
    Public Sub AForeignTokenAndTheFileListAreNeverInternalMoveTargets()
        Dim provider As New MoveProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New MoveBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            browser.TreeViewDmsFolders.SelectedNode = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "source")
            dispatcher.WaitForEvents()
            Dim origin = browser.ListViewDmsFiles.Items(0)
            Dim foreignEffect As DragDropEffects
            Dim listEffect As DragDropEffects
            browser.ShellDragOperation = Function(data, dropSource)
                                             Dim foreign As New DataObject()
                                             foreign.SetData(ShellDownloadData.InternalMoveFormatName, New System.IO.MemoryStream(Guid.NewGuid().ToByteArray()))
                                             Dim point = browser.TreeViewDmsFolders.PointToScreen(Drawing.Point.Empty)
                                             Dim over As New DragEventArgs(foreign, 1, point.X, point.Y, DragDropEffects.Move, DragDropEffects.None)
                                             RaiseDrag(browser.TreeViewDmsFolders, "OnDragOver", over)
                                             foreignEffect = over.Effect
                                             over = New DragEventArgs(New DataObject(data), 1, 0, 0, DragDropEffects.Move, DragDropEffects.None)
                                             RaiseDrag(browser.ListViewDmsFiles, "OnDragOver", over)
                                             listEffect = over.Effect
                                             Return &H40101
                                         End Function
            GetType(ListView).GetMethod("OnItemDrag", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(browser.ListViewDmsFiles, {New ItemDragEventArgs(MouseButtons.Left, origin)})
            dispatcher.WaitForEvents()
            Assert.That(foreignEffect, [Is].EqualTo(DragDropEffects.None))
            Assert.That(listEffect, [Is].EqualTo(DragDropEffects.None))
            Assert.That(provider.Moved, [Is].Empty)
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
        End Using
    End Sub

    Private Shared Sub RaiseDrag(control As Control, name As String, args As DragEventArgs)
        GetType(Control).GetMethod(name, BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(control, {args})
    End Sub
    Private Class MoveBrowser
        Inherits DmsBrowser
        Friend Failure As Exception
        Friend Sub New(provider As MoveProvider)
            MyBase.New(provider, Nothing, "Remote move test", Nothing, provider.ScopeRoot, Nothing, BrowseModes.FoldersAndFiles, FileOrFolderActions.AllowCopyRenameMoveFiles, DialogOperationModes.NoResults, "", "", "")
            ShowInTaskbar = False
            Opacity = 0
            AddHandler OperationFailed, Sub(sender, e) Failure = e.Exception
        End Sub
        Protected Overrides Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
        End Sub
    End Class
    Private Class MoveProvider
        Inherits NoDmsProvider
        Implements IDmsInstanceProvider
        Friend InstanceId As String = "fixture-instance"
        Friend InstanceFailure As Exception
        Public ReadOnly Property CurrentDmsInstance As DmsInstanceInfo Implements IDmsInstanceProvider.CurrentDmsInstance
            Get
                If InstanceFailure IsNot Nothing Then Throw InstanceFailure
                Return New DmsInstanceInfo(InstanceId, "Fixture", True)
            End Get
        End Property
        Public Function ListAvailableDmsInstances() As IReadOnlyList(Of DmsInstanceInfo) Implements IDmsInstanceProvider.ListAvailableDmsInstances
            Return New List(Of DmsInstanceInfo) From {CurrentDmsInstance}
        End Function
        Public Sub SelectDmsInstance(id As String) Implements IDmsInstanceProvider.SelectDmsInstance
            InstanceId = id
        End Sub
        Friend SecondFile As Boolean
        Friend DuplicateIdentity As Boolean
        Friend AmbiguousDestination As Boolean
        Friend FailSecond As Boolean
        Friend RootFiles As Boolean = True
        Friend ScopeRoot As String = ""
        Friend ReadOnly Moved As New List(Of DmsResourceItem)
        Friend ReadOnly Destinations As New List(Of String)
        Public Overrides ReadOnly Property SupportsCollections As Boolean = False
        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return RootFiles
            End Get
        End Property
        Protected Overrides ReadOnly Property SupportsAsynchronousCopyMove As Boolean = True
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(If(If(path, "") = ScopeRoot, New List(Of DmsResourceItem) From {Folder(CombinePath(ScopeRoot, "source")), Folder(CombinePath(ScopeRoot, "destination"))}, New List(Of DmsResourceItem)))
        End Function
        Private Shared Function Folder(name As String) As DmsResourceItem
            Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = name, .Name = name.Split("/"c).Last(), .ExtendedInfosFolderID = name, .HasChildDirectories = False}
        End Function
        Public Overrides Function ListEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return ListDirectoryEntriesAsync(path, cancellationToken)
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim files As New List(Of DmsResourceItem)
            If path = CombinePath(ScopeRoot, "source") Then
                If Not Moved.Any(Function(file) file.ExtendedInfosFileID = "file-1") Then files.Add(RemoteFile("first.txt", "file-1", path))
                If SecondFile Then files.Add(RemoteFile("second.txt", If(DuplicateIdentity, "file-1", "file-2"), path))
            End If
            Return Task.FromResult(files)
        End Function
        Private Shared Function RemoteFile(name As String, id As String, parent As String) As DmsResourceItem
            Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = parent & "/" & name, .Name = name, .ExtendedInfosFileID = id, .ExtendedInfosAssignedFolderID = parent}
        End Function
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            If ScopeRoot <> "" AndAlso path = ScopeRoot Then
                Dim root = Folder(ScopeRoot)
                root.HasChildDirectories = True
                Return Task.FromResult(root)
            End If
            If path = CombinePath(ScopeRoot, "source") OrElse path = CombinePath(ScopeRoot, "destination") Then Return Task.FromResult(Folder(path))
            Return Task.FromResult(If(AmbiguousDestination, New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = path, .ExtendedInfosCollisionDetected = True}, Nothing))
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
        Protected Overrides Function MoveItemAsync(source As DmsResourceItem, destinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            If FailSecond AndAlso source.ExtendedInfosFileID = "file-2" Then Throw New InvalidOperationException("Fixture second move failed")
            Moved.Add(source)
            Destinations.Add(destinationPath)
            Return Task.CompletedTask
        End Function
    End Class
End Class
