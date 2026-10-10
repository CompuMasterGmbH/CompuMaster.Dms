Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class FolderDownloadTest
    <TestCase(False, False), TestCase(True, False), TestCase(False, True), TestCase(True, True)>
    Public Sub FolderDownloadUsesDownloadPermissionAndRecoversAfterABusyOperation(allow As Boolean, foldersOnly As Boolean)
        Using dispatcher As New UiTestDispatcher, browser As New PreviewBrowser
            browser.BrowseMode = If(foldersOnly, DmsBrowser.BrowseModes.Folders, DmsBrowser.BrowseModes.FoldersAndFiles)
            browser.AllowedActions = If(allow, DmsBrowser.FileOrFolderActions.AllowDownloadFiles, CType(0, DmsBrowser.FileOrFolderActions))
            browser.Show()
            GetType(DmsBrowser).GetField("SuppressSelectionRefresh", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(browser, True)
            Dim tree = DirectCast(browser.Controls.Find("TreeViewDmsFolders", True).Single(), TreeView)
            tree.Nodes.Add(DmsBrowser.CreateDirectoryTreeNode(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = "Folder", .FullName = "folder"}))
            tree.SelectedNode = tree.Nodes(0)
            Dim toolbar = DirectCast(browser.Controls.Find("ToolStripFileActions", True).Single(), ToolStrip)
            Dim context = tree.ContextMenuStrip.Items("DownloadFolderContext")
            Assert.That(context.Available, [Is].EqualTo(allow))
            Assert.That(context.Enabled, [Is].EqualTo(allow))
            Assert.That(toolbar.Items("DownloadFolder").Available, [Is].EqualTo(allow AndAlso Not foldersOnly))
            Assert.That(browser.CanDownloadFolder(), [Is].EqualTo(allow))
            dispatcher.Finish(browser.RunTransferAsync(Function()
                                                          Assert.That(browser.CanDownloadFolder(), [Is].False)
                                                          Assert.That(context.Enabled, [Is].False)
                                                          Return Task.CompletedTask
                                                      End Function))
            Assert.That(context.Enabled, [Is].EqualTo(allow))
        End Using
    End Sub
    <Test>
    Public Async Function PlanningPreservesHierarchyEmptyFoldersAndFileIdentityWithoutWriting() As Task
        Dim root = NewLocalScope()
        Try
            Dim provider As New FixtureProvider
            Dim plan = Await RemoteDownloadPlan.BuildAsync(provider, provider.Folder, root, CancellationToken.None)
            Assert.That(plan.Directories, [Is].EquivalentTo({Path.Combine(root, "Remote"), Path.Combine(root, "Remote", "Sub"), Path.Combine(root, "Remote", "Empty")}))
            Assert.That(plan.Files.Single().ExtendedInfosFileID, [Is].EqualTo("selected-document"))
            Assert.That(plan.Destinations.Single(), [Is].EqualTo(Path.Combine(root, "Remote", "Sub", "file.bin")))
            Assert.That(Directory.EnumerateFileSystemEntries(root), [Is].Empty)
        Finally
            Directory.Delete(root, True)
        End Try
    End Function

    <TestCase(".."), TestCase("escape/name"), TestCase("CON.txt"), TestCase("trailing."), TestCase("bad:stream"), TestCase("space ")>
    Public Sub InvalidNamesFailBeforeAnyLocalWrites(name As String)
        Dim root = NewLocalScope()
        Try
            Dim provider As New FixtureProvider With {.BadName = name}
            Assert.ThrowsAsync(Of IOException)(Async Function()
                                                  Await RemoteDownloadPlan.BuildAsync(provider, provider.Folder, root, CancellationToken.None)
                                              End Function)
            Assert.That(Directory.EnumerateFileSystemEntries(root), [Is].Empty)
        Finally
            Directory.Delete(root, True)
        End Try
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub CyclesAndCaseCollisionsFailBeforeTransfer(cycle As Boolean)
        Dim root = NewLocalScope()
        Try
            Dim provider As New FixtureProvider With {.Cycle = cycle, .Collision = Not cycle}
            Assert.ThrowsAsync(Of IOException)(Async Function()
                                                  Await RemoteDownloadPlan.BuildAsync(provider, provider.Folder, root, CancellationToken.None)
                                              End Function)
            Assert.That(Directory.EnumerateFileSystemEntries(root), [Is].Empty)
        Finally
            Directory.Delete(root, True)
        End Try
    End Sub

    <TestCase(1, True), TestCase(2, True), TestCase(1, False), TestCase(2, False), TestCase(0, True)>
    Public Sub ConflictChoicesApplyToAllOnlyWhenRequested(action As Integer, all As Boolean)
        Dim calls As Integer
        Dim policy As New DownloadConflictPolicy(Function(path)
                                                    calls += 1
                                                    Return New DownloadConflictDecision With {.Choice = CType(action, DownloadConflictAction), .ApplyToAll = all}
                                                End Function)
        Assert.That(policy.Resolve("first"), [Is].EqualTo(CType(action, DownloadConflictAction)))
        Assert.That(policy.Resolve("second"), [Is].EqualTo(CType(action, DownloadConflictAction)))
        Assert.That(calls, [Is].EqualTo(If(all AndAlso action <> 0, 1, 2)))
        Using question As New DownloadConflictDialog("file.bin", Nothing)
            Assert.That(question.Choice, [Is].EqualTo(DownloadConflictAction.Cancel))
            Assert.That(question.ApplyToAll.Checked, [Is].False)
        End Using
    End Sub

    <TestCase(0), TestCase(1), TestCase(2), TestCase(3)>
    Public Async Function FolderDownloadsPreserveExistingFilesOnFailureCancellationOrUnapprovedReplacement(outcome As Integer) As Task
        Dim root = NewLocalScope()
        Try
            Dim destination = Path.Combine(root, "existing.bin")
            File.WriteAllBytes(destination, New Byte() {9})
            Dim provider As New FixtureProvider With {.Failure = outcome = 1, .Cancel = outcome = 2}
            Dim files = {New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "file.bin", .FullName = "remote/file.bin", .ExtendedInfosFileID = "selected-document"}}
            Dim observer As New Collector
            Dim task = DmsBrowser.DownloadBatchWithProgressAsync(provider, files, {destination}, observer, CancellationToken.None, {outcome <> 3})
            If outcome = 0 Then
                Await task
                Assert.That(File.ReadAllBytes(destination), [Is].EqualTo(New Byte() {1, 2, 3}))
                Assert.That(observer.Last.States(0), [Is].EqualTo(UploadFileState.Completed))
            Else
                Assert.CatchAsync(Async Function()
                                      Await task
                                  End Function)
                Assert.That(File.ReadAllBytes(destination), [Is].EqualTo(New Byte() {9}))
                Assert.That(observer.Last.States(0), [Is].Not.EqualTo(UploadFileState.Completed))
            End If
            Assert.That(Directory.GetFiles(root), [Is].EqualTo({destination}), "Owned partial files must be removed.")
            Assert.That(provider.SelectedId, [Is].EqualTo("selected-document"))
        Finally
            Directory.Delete(root, True)
        End Try
    End Function

    <Test>
    Public Sub ExistingFileCannotBeUsedAsAFolderAndPreCancelledPlanningDoesNotList()
        Dim root = NewLocalScope()
        Try
            File.WriteAllText(Path.Combine(root, "Remote"), "unrelated")
            Dim provider As New FixtureProvider
            Assert.ThrowsAsync(Of IOException)(Async Function()
                                                  Await RemoteDownloadPlan.BuildAsync(provider, provider.Folder, root, CancellationToken.None)
                                              End Function)
            Using cancelled As New CancellationTokenSource
                cancelled.Cancel()
                Assert.ThrowsAsync(Of OperationCanceledException)(Async Function()
                                                                     Await RemoteDownloadPlan.BuildAsync(provider, provider.Folder, root, cancelled.Token)
                                                                 End Function)
            End Using
            Assert.That(provider.Listings, [Is].Zero)
            Assert.That(File.ReadAllText(Path.Combine(root, "Remote")), [Is].EqualTo("unrelated"))
        Finally
            Directory.Delete(root, True)
        End Try
    End Sub

    Private Shared Function NewLocalScope() As String
        Dim root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "folder-download-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root), [Is].False)
        Directory.CreateDirectory(root)
        Return root
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function DirectoryCreationPreservesEmptyFoldersAndHonorsCancellation(cancel As Boolean) As Task
        Dim root = NewLocalScope()
        Try
            Dim nested = Path.Combine(root, "Folder", "Empty")
            Using token As New CancellationTokenSource
                If cancel Then token.Cancel()
                Dim operation = DmsBrowser.CreateDownloadDirectoriesAsync({nested}, token.Token)
                If cancel Then
                    Assert.CatchAsync(Of OperationCanceledException)(Async Function()
                                                                        Await operation
                                                                    End Function)
                    Assert.That(Directory.EnumerateFileSystemEntries(root), [Is].Empty)
                Else
                    Await operation
                    Assert.That(Directory.Exists(nested), [Is].True)
                End If
            End Using
        Finally
            Directory.Delete(root, True)
        End Try
    End Function
    Private Class Collector
        Implements IProgress(Of UploadBatchSnapshot)
        Friend Last As UploadBatchSnapshot
        Public Sub Report(value As UploadBatchSnapshot) Implements IProgress(Of UploadBatchSnapshot).Report
            Last = value
        End Sub
    End Class
    Private Class PreviewBrowser
        Inherits DmsBrowser
        Friend Sub New()
            MyBase.New(New NoDmsProvider)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
    Private Class FixtureProvider
        Inherits NoDmsProvider
        Friend ReadOnly Folder As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Collection, .Name = "Remote", .FullName = "remote"}
        Friend BadName As String
        Friend Cycle As Boolean
        Friend Collision As Boolean
        Friend Failure As Boolean
        Friend Cancel As Boolean
        Friend SelectedId As String
        Friend Listings As Integer
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Listings += 1
            Dim values As New List(Of DmsResourceItem)
            If Cycle Then
                values.Add(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = "Cycle", .FullName = "remote"})
            ElseIf path = "remote" Then
                values.Add(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = "Sub", .FullName = "remote/sub"})
                values.Add(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = "Empty", .FullName = "remote/empty"})
            End If
            Return Task.FromResult(values)
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim values As New List(Of DmsResourceItem)
            If path = "remote/sub" Then
                values.Add(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = If(BadName, "file.bin"), .FullName = "remote/sub/file.bin", .ExtendedInfosFileID = "selected-document"})
                If Collision Then values.Add(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "FILE.bin", .FullName = "remote/sub/FILE.bin", .ExtendedInfosFileID = "other-document"})
            End If
            Return Task.FromResult(values)
        End Function
        Public Overrides Function DownloadFileWithProgressAsync(file As DmsResourceItem, local As String, progress As IProgress(Of DmsTransferProgress), Optional token As CancellationToken = Nothing) As Task
            SelectedId = file.ExtendedInfosFileID
            System.IO.File.WriteAllBytes(local, New Byte() {1, 2, 3})
            If Failure Then Throw New IOException("Fixture download failed after writing.")
            If Cancel Then Throw New OperationCanceledException()
            progress.Report(New DmsTransferProgress(3, 3, DmsTransferPhase.Completed))
            Return Task.CompletedTask
        End Function
    End Class
End Class
