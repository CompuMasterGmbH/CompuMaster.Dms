Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class RootFileDropTest
    <TestCase("", False), TestCase("/", False), TestCase("dav/root", False), TestCase("", True), TestCase("/", True), TestCase("dav/root", True)>
    Public Sub ActualRootCapabilitiesApplyToLooseFilesAndMixedPlansButAllowFolderTrees(root As String, rootFiles As Boolean)
        InLocalScope(Sub(directory)
                         Dim file = Path.Combine(directory, "loose.txt")
                         Dim folder = Path.Combine(directory, "Folder")
                         System.IO.Directory.CreateDirectory(Path.Combine(folder, "Empty"))
                         System.IO.File.WriteAllText(file, "loose")
                         System.IO.File.WriteAllText(Path.Combine(folder, "nested.txt"), "nested")
                         Dim provider As New RootProvider(root) With {.RootFiles = rootFiles}
                         Using dispatcher As New UiTestDispatcher(), browser As New RootBrowser(provider, "")
                             browser.Show()
                             dispatcher.WaitForEvents()
                             Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
                             Dim target = browser.TreeViewDmsFolders.Nodes(0)
                             For Each paths In New String()() {New String() {file}, New String() {folder}, New String() {folder, file}}
                                 Dim permitted = rootFiles OrElse paths.Length = 1 AndAlso paths(0) = folder
                                 Assert.That(browser.CanUploadLocalPaths(paths, target), [Is].EqualTo(permitted))
                                 Dim plan = LocalUploadPlan.Build(paths, CancellationToken.None)
                                 If permitted Then
                                     Assert.DoesNotThrow(Sub() browser.ValidateUploadPlan(target, plan))
                                 Else
                                     Assert.Throws(Of DmsUserInputInvalidException)(Sub() browser.ValidateUploadPlan(target, plan))
                                 End If
                                 Dim data As New DataObject(DataFormats.FileDrop, paths)
                                 Dim point = browser.TreeViewDmsFolders.PointToScreen(New Drawing.Point(target.Bounds.Left + 2, target.Bounds.Top + 2))
                                 Dim over As New DragEventArgs(data, 1, point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None)
                                 GetType(Control).GetMethod("OnDragOver", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser.TreeViewDmsFolders, {over})
                                 Assert.That(over.Effect, [Is].EqualTo(If(permitted, DragDropEffects.Copy, DragDropEffects.None)))
                             Next
                             Assert.That(provider.Writes, [Is].Zero)
                         End Using
                     End Sub)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub AnOrdinaryScopedRootCanAcceptFilesEvenWhenTheServerRootCannot(rootFiles As Boolean)
        InLocalScope(Sub(directory)
                         Dim file = Path.Combine(directory, "loose.txt")
                         System.IO.File.WriteAllText(file, "loose")
                         Dim provider As New RootProvider("") With {.RootFiles = rootFiles}
                         Using dispatcher As New UiTestDispatcher(), browser As New RootBrowser(provider, "scoped")
                             browser.Show()
                             dispatcher.WaitForEvents()
                             Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
                             Dim root = browser.TreeViewDmsFolders.Nodes(0)
                             Assert.That(browser.CanUploadLocalPaths({file}, root), [Is].True)
                             Assert.That(browser.CanAcceptDirectFiles(root), [Is].True)
                             Assert.DoesNotThrow(Sub() browser.ValidateUploadPlan(root, LocalUploadPlan.Build({file}, CancellationToken.None)))
                         End Using
                     End Sub)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub DropRechecksChangedRootCapabilityOrUploadPermission(permission As Boolean)
        InLocalScope(Sub(directory)
                         Dim file = Path.Combine(directory, "loose.txt")
                         System.IO.File.WriteAllText(file, "loose")
                         Dim provider As New RootProvider("") With {.RootFiles = True}
                         Using dispatcher As New UiTestDispatcher(), browser As New RootBrowser(provider, "")
                             browser.Show()
                             dispatcher.WaitForEvents()
                             Dim target = browser.TreeViewDmsFolders.Nodes(0)
                             Dim point = browser.TreeViewDmsFolders.PointToScreen(New Drawing.Point(target.Bounds.Left + 2, target.Bounds.Top + 2))
                             Dim over As New DragEventArgs(New DataObject(DataFormats.FileDrop, New String() {file}), 1, point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None)
                             GetType(Control).GetMethod("OnDragOver", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser.TreeViewDmsFolders, {over})
                             Assert.That(over.Effect, [Is].EqualTo(DragDropEffects.Copy))
                             If permission Then
                                 browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowSelectOnly
                             Else
                                 provider.RootFiles = False
                             End If
                             GetType(Control).GetMethod("OnDragDrop", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser.TreeViewDmsFolders, {over})
                             dispatcher.WaitForEvents()
                             Assert.That(provider.Writes, [Is].Zero)
                         End Using
                     End Sub)
    End Sub

    <Test>
    Public Sub DirectoryAndPayloadWritesRecheckTheirGateAfterRemoteLookup()
        Using dispatcher As New UiTestDispatcher()
            Dim permitted As Boolean = True
            Dim provider As New RootProvider("") With {.BeforeLookup = Sub() permitted = False}
            Dim operation = DmsBrowser.EnsureUploadDirectoriesAsync(provider, "", {"Folder"}, CancellationToken.None, canWrite:=Function() permitted)
            dispatcher.PumpUntil(Function() operation.IsCompleted)
            Assert.Throws(Of UnauthorizedAccessException)(Sub() operation.GetAwaiter().GetResult())
            Assert.That(provider.Writes, [Is].Zero)
            permitted = True
            Dim conflicts As New UploadConflictPolicy(Function(path) New DownloadConflictDecision With {.Choice = DownloadConflictAction.Replace})
            operation = DmsBrowser.UploadBatchWithProgressAsync(provider, "", {"file"}, New Collector(), CancellationToken.None, conflicts:=conflicts, canWrite:=Function() permitted)
            dispatcher.PumpUntil(Function() operation.IsCompleted)
            Assert.Throws(Of UnauthorizedAccessException)(Sub() operation.GetAwaiter().GetResult())
            Assert.That(provider.Writes, [Is].Zero)
        End Using
    End Sub

    Private Shared Sub InLocalScope(action As Action(Of String))
        Dim root = Path.Combine(Path.GetTempPath(), "dms-root-drop-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root), [Is].False)
        System.IO.Directory.CreateDirectory(root)
        Try
            action(root)
        Finally
            Directory.Delete(root, True)
            Assert.That(Directory.Exists(root), [Is].False)
        End Try
    End Sub
    Private Class Collector
        Implements IProgress(Of UploadBatchSnapshot)
        Public Sub Report(value As UploadBatchSnapshot) Implements IProgress(Of UploadBatchSnapshot).Report
        End Sub
    End Class
    Private Class RootBrowser
        Inherits DmsBrowser
        Friend Failure As Exception
        Friend Sub New(provider As RootProvider, initial As String)
            MyBase.New(provider, Nothing, "Root drop test", Nothing, initial, Nothing, BrowseModes.FoldersAndFiles, FileOrFolderActions.AllowUploadFiles, DialogOperationModes.NoResults, "", "", "")
            ShowInTaskbar = False
            Opacity = 0
            AddHandler OperationFailed, Sub(sender, e) Failure = e.Exception
        End Sub
        Protected Overrides Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
        End Sub
    End Class
    Private Class RootProvider
        Inherits NoDmsProvider
        Private ReadOnly RootPath As String
        Friend RootFiles As Boolean
        Friend Writes As Integer
        Friend BeforeLookup As Action
        Friend Sub New(root As String)
            RootPath = root
        End Sub
        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return RootPath
            End Get
        End Property
        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return RootFiles
            End Get
        End Property
        Public Overrides ReadOnly Property SupportsCollections As Boolean = False
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            BeforeLookup?.Invoke()
            Return Task.FromResult(If(path = "scoped", New DmsResourceItem With {.FullName = path, .Name = path, .ItemType = DmsResourceItem.ItemTypes.Folder, .ExtendedInfosFolderID = "scoped-id", .HasChildDirectories = False}, Nothing))
        End Function
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(New List(Of DmsResourceItem))
        End Function
        Public Overrides Function ListEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return ListDirectoryEntriesAsync(path, cancellationToken)
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(New List(Of DmsResourceItem))
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
        Public Overrides Function CreateDirectoryAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Writes += 1
            Return Task.CompletedTask
        End Function
        Public Overrides Function UploadFileWithProgressAsync(path As String, file As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            Writes += 1
            Return Task.CompletedTask
        End Function
    End Class
End Class
