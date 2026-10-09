Imports System.Drawing
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class FolderUploadTest
    <Test>
    Public Sub MixedSelectionsPreserveHierarchyEmptyFoldersAndAvoidDoubleUploads()
        InLocalScope(Sub(root)
                         Dim folder = Path.Combine(root, "Folder")
                         Directory.CreateDirectory(Path.Combine(folder, "Empty"))
                         Directory.CreateDirectory(Path.Combine(folder, "Sub"))
                         File.WriteAllText(Path.Combine(folder, "Sub", "one.txt"), "one")
                         File.WriteAllText(Path.Combine(root, "two.txt"), "two")
                         Dim plan = LocalUploadPlan.Build({folder, Path.Combine(folder, "Sub"), Path.Combine(folder, "Sub", "one.txt"), Path.Combine(root, "two.txt")}, CancellationToken.None, root)
                         Assert.That(plan.RelativePaths, [Is].EquivalentTo({"Folder/Sub/one.txt", "two.txt"}))
                         Assert.That(plan.Directories, [Is].EqualTo({"Folder", "Folder/Empty", "Folder/Sub"}))
                         Assert.That(plan.Files, Has.Length.EqualTo(2))
                     End Sub)
    End Sub

    <Test>
    Public Sub CollidingNamesAndPathsOutsideTheConfiguredRootFailBeforeTransfer()
        InLocalScope(Sub(root)
                         Dim first = Path.Combine(root, "first", "same")
                         Dim second = Path.Combine(root, "second", "same")
                         Directory.CreateDirectory(first)
                         Directory.CreateDirectory(second)
                         Assert.Throws(Of IOException)(Sub() LocalUploadPlan.Build({first, second}, CancellationToken.None))
                         Assert.Throws(Of UnauthorizedAccessException)(Sub() LocalUploadPlan.Build({second}, CancellationToken.None, first))
                         Assert.That(LocalUploadPlan.NormalizeSourcePath(Path.GetPathRoot(root)), [Is].EqualTo(Path.GetPathRoot(root)), "A drive root must stay absolute instead of becoming a drive-relative path.")
                         Using cancellation As New CancellationTokenSource()
                             cancellation.Cancel()
                             Assert.Throws(Of OperationCanceledException)(Sub() LocalUploadPlan.Build({first}, cancellation.Token))
                         End Using
                     End Sub)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub UploadAccessAndFolderActionVisibilityFollowTheExistingFileAction(allow As Boolean)
        Using browser As New PreviewBrowser()
            browser.BrowseMode = DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = If(allow, DmsBrowser.FileOrFolderActions.AllowUploadFiles, CType(0, DmsBrowser.FileOrFolderActions))
            browser.Show()
            Dim toolbar = DirectCast(browser.Controls.Find("ToolStripFileActions", True).Single(), ToolStrip)
            Assert.That(toolbar.Items("UploadFolder").Available, [Is].EqualTo(toolbar.Items("ToolStripButtonUploadFile").Available))
            Assert.That(toolbar.Items("UploadFolder").Enabled, [Is].EqualTo(toolbar.Items("ToolStripButtonUploadFile").Enabled))
            Assert.That(browser.CanUploadDrop(), [Is].EqualTo(allow))
            Assert.That(browser.Controls.Find("TreeViewDmsFolders", True).Single().AllowDrop, [Is].EqualTo(allow))
            browser.BrowseMode = DmsBrowser.BrowseModes.Folders
            Assert.That(toolbar.Items("UploadFolder").Available, [Is].False)
            Assert.That(browser.CanUploadDrop(), [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub FolderUploadStaysEnabledAfterStartupRestoresTheFileToolbar()
        Using dispatcher As New UiTestDispatcher(), browser As New PreviewBrowser()
            browser.BrowseMode = DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowUploadFiles
            browser.Show()
            Dim toolbar = DirectCast(browser.Controls.Find("ToolStripFileActions", True).Single(), ToolStrip)
            For Each allow In New Boolean() {True, False, True}
                dispatcher.Finish(browser.RunTransferAsync(
                    Function()
                        browser.AllowedActions = If(allow, DmsBrowser.FileOrFolderActions.AllowUploadFiles, CType(0, DmsBrowser.FileOrFolderActions))
                        Assert.That(toolbar.Enabled, [Is].False, "Startup changes permissions while the toolbar is temporarily locked.")
                        Assert.That(toolbar.Items("UploadFolder").Enabled, [Is].False)
                        Return Task.CompletedTask
                    End Function))
                Assert.That(toolbar.Enabled, [Is].True)
                Assert.That(toolbar.Items("ToolStripButtonUploadFile").Enabled, [Is].EqualTo(allow))
                Assert.That(toolbar.Items("UploadFolder").Enabled, [Is].EqualTo(allow), "Folder upload must recover with file upload after startup or refresh.")
            Next
        End Using
    End Sub

    <Test>
    <TestCase(""), TestCase("/"), TestCase("dav/root")>
    Public Sub SyntheticServerRootAcceptsFileAndFolderUploadDestinations(rootPath As String)
        Using dispatcher As New UiTestDispatcher()
            Dim provider As New RootUploadProvider(rootPath)
            Using browser As New PreviewBrowser(provider)
                dispatcher.Finish(browser.LoadTreeAsync())
                Dim tree = DirectCast(browser.Controls.Find("TreeViewDmsFolders", True).Single(), TreeView)
                Dim destination = browser.UploadDestination(tree.Nodes(0))
                Assert.That(destination, [Is].EqualTo(rootPath), "A synthetic root uses the provider's root path even without resource metadata.")
                dispatcher.Finish(DmsBrowser.EnsureUploadDirectoriesAsync(provider, destination, {"Folder", "Folder/Empty"}, CancellationToken.None))
                dispatcher.Finish(DmsBrowser.UploadBatchWithProgressAsync(provider, destination, {"local-one", "local-two"}, New Collector(), CancellationToken.None, Nothing, True, {"one.txt", "Folder/two.txt"}))
                Assert.That(provider.Created, [Is].EqualTo({provider.CombinePath(rootPath, "Folder"), provider.CombinePath(rootPath, "Folder/Empty")}))
                Assert.That(provider.Uploaded, [Is].EqualTo({provider.CombinePath(rootPath, "one.txt"), provider.CombinePath(rootPath, "Folder/two.txt")}))
                Dim child = DmsBrowser.CreateDirectoryTreeNode(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = "Folder/Sub"})
                Assert.That(browser.UploadDestination(child), [Is].EqualTo("Folder/Sub"))
                Assert.Throws(Of DmsUserInputInvalidException)(Sub() browser.UploadDestination(Nothing))
                Assert.Throws(Of DmsUserInputInvalidException)(Sub() browser.UploadDestination(New TreeNode()))
            End Using
        End Using
    End Sub

    <Test>
    Public Sub TreeDropUsesTheNodeUnderTheMouseAndListDropUsesTheCurrentDirectory()
        Using browser As New PreviewBrowser()
            browser.Show()
            Dim tree = DirectCast(browser.Controls.Find("TreeViewDmsFolders", True).Single(), TreeView)
            Dim list = browser.Controls.Find("ListViewDmsFiles", True).Single()
            'This hit-test fixture has no loaded remote root and must not start navigation.
            GetType(DmsBrowser).GetField("SuppressSelectionRefresh", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(browser, True)
            Dim first = DmsBrowser.CreateDirectoryTreeNode(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = "first", .FullName = "first"})
            Dim second = DmsBrowser.CreateDirectoryTreeNode(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = "second", .FullName = "second"})
            tree.Nodes.AddRange({first, second})
            tree.SelectedNode = first
            Assert.That(browser.UploadDropNode(tree, tree.PointToScreen(New Point(second.Bounds.Left + 2, second.Bounds.Top + 2))), [Is].SameAs(second))
            Assert.That(browser.UploadDropNode(list, list.PointToScreen(Point.Empty)), [Is].SameAs(first))
            Assert.That(browser.UploadDropNode(tree, tree.PointToScreen(New Point(3, tree.Height - 5))), [Is].Null)
        End Using
    End Sub

    <Test>
    Public Sub DirectoryPreparationIsIdempotentAndUploadsKeepTheirRelativePaths()
        Using dispatcher As New UiTestDispatcher()
            Dim provider As New FixtureProvider()
            dispatcher.Finish(DmsBrowser.EnsureUploadDirectoriesAsync(provider, "destination", {"Folder", "Folder/Empty", "Folder/Sub"}, CancellationToken.None))
            dispatcher.Finish(DmsBrowser.EnsureUploadDirectoriesAsync(provider, "destination", {"Folder", "Folder/Empty", "Folder/Sub"}, CancellationToken.None))
            Assert.That(provider.Created, [Is].EqualTo({"destination/Folder", "destination/Folder/Empty", "destination/Folder/Sub"}))
            Dim observer As New Collector()
            dispatcher.Finish(DmsBrowser.UploadBatchWithProgressAsync(provider, "destination", {"local-one", "local-two"}, observer, CancellationToken.None, Nothing, True, {"Folder/Sub/one.txt", "two.txt"}))
            Assert.That(provider.Uploaded, [Is].EqualTo({"destination/Folder/Sub/one.txt", "destination/two.txt"}))
            Assert.That(observer.Last.States.All(Function(state) state = UploadFileState.Completed), [Is].True)
        End Using
    End Sub

    <Test>
    Public Sub EmptyFolderPreparationStillOffersCancellationAndTheInheritedIcon()
        Using dialog As New UploadProgressDialog(New String() {}, False, True, SystemIcons.Information)
            dialog.Show()
            Assert.That(dialog.LatestSnapshot.Files, [Is].Empty)
            Assert.That(dialog.Icon.Handle, [Is].EqualTo(SystemIcons.Information.Handle))
            dialog.Close()
            Assert.That(dialog.CancellationToken.IsCancellationRequested, [Is].True)
            dialog.Finish()
            dialog.Close()
        End Using
    End Sub

    <Test>
    Public Sub ActiveTransfersRejectBothIncomingUploadsAndOutgoingDownloads()
        Using dispatcher As New UiTestDispatcher(), browser As New PreviewBrowser()
            browser.BrowseMode = DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowUploadFiles Or DmsBrowser.FileOrFolderActions.AllowDownloadFiles
            Dim completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim operation = browser.RunTransferAsync(Function() completion.Task)
            Assert.That(browser.CanUploadDrop(), [Is].False)
            Assert.That(browser.CanDragDownload(), [Is].False)
            completion.SetResult(True)
            dispatcher.Finish(operation)
            Assert.That(browser.CanUploadDrop(), [Is].True)
            Assert.That(browser.CanDragDownload(), [Is].True)
        End Using
    End Sub

    Private Shared Sub InLocalScope(action As Action(Of String))
        Dim root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "folder-upload-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root), [Is].False)
        Directory.CreateDirectory(root)
        Try
            action(root)
        Finally
            Directory.Delete(root, True)
            Assert.That(Directory.Exists(root), [Is].False)
        End Try
    End Sub
    Private Class PreviewBrowser
        Inherits DmsBrowser
        Friend Sub New(Optional provider As BaseDmsProvider = Nothing)
            MyBase.New(If(provider, New NoDmsProvider()))
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
    Private Class Collector
        Implements IProgress(Of UploadBatchSnapshot)
        Friend Last As UploadBatchSnapshot
        Public Sub Report(value As UploadBatchSnapshot) Implements IProgress(Of UploadBatchSnapshot).Report
            Last = value
        End Sub
    End Class
    Private Class FixtureProvider
        Inherits NoDmsProvider
        Friend ReadOnly Created As New List(Of String)
        Friend ReadOnly Uploaded As New List(Of String)
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Task.FromResult(If(Created.Contains(path), New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = path}, Nothing))
        End Function
        Public Overrides Function CreateDirectoryAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Created.Add(path)
            Return Task.CompletedTask
        End Function
        Public Overrides Function UploadFileWithProgressAsync(path As String, local As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            Uploaded.Add(path)
            Return Task.CompletedTask
        End Function
    End Class

    Private Class RootUploadProvider
        Inherits FixtureProvider
        Private ReadOnly RootPath As String
        Friend Sub New(rootPath As String)
            Me.RootPath = rootPath
        End Sub
        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return RootPath
            End Get
        End Property
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(New List(Of DmsResourceItem)())
        End Function
    End Class
End Class
