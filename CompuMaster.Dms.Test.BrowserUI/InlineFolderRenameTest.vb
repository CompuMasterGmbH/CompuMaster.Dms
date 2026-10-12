Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class InlineFolderRenameTest
    <Test>
    Public Sub F2OnlyStartsNativeEditingForAnIdlePermittedChild()
        Dim provider As New RenameProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New RenameBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            Dim key As New KeyEventArgs(Keys.F2)
            GetType(TreeView).GetMethod("OnKeyDown", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser.TreeViewDmsFolders, {key})
            Assert.That(key.Handled, [Is].True)
            Assert.That(root.IsEditing, [Is].False)
            Dim child = root.Nodes(0)
            browser.TreeViewDmsFolders.SelectedNode = child
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
            Dim operation = browser.RunTransferAsync(Function() completion.Task)
            Dim begin As New NodeLabelEditEventArgs(child)
            RaiseLabelEvent(browser, "OnBeforeLabelEdit", begin)
            Assert.That(begin.CancelEdit, [Is].True)
            completion.SetResult(True)
            dispatcher.Finish(operation)
            key = New KeyEventArgs(Keys.F2)
            GetType(TreeView).GetMethod("OnKeyDown", BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser.TreeViewDmsFolders, {key})
            Assert.That(key.Handled, [Is].True)
            Assert.That(child.IsEditing, [Is].True)
            child.EndEdit(True)
            dispatcher.WaitForEvents()
            Assert.That(provider.Moves, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub UnknownPathsAndUnsupportedCollectionsCannotStartEditing()
        Dim provider As New RenameProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New RenameBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Dim child = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            Dim resource = DirectCast(child.Tag.GetType().GetField("DmsResourceItem").GetValue(child.Tag), DmsResourceItem)
            Dim path = resource.FullName
            resource.FullName = Nothing
            Dim begin As New NodeLabelEditEventArgs(child)
            RaiseLabelEvent(browser, "OnBeforeLabelEdit", begin)
            Assert.That(begin.CancelEdit, [Is].True)
            resource.FullName = path
            resource.ItemType = DmsResourceItem.ItemTypes.Collection
            begin = New NodeLabelEditEventArgs(child)
            RaiseLabelEvent(browser, "OnBeforeLabelEdit", begin)
            Assert.That(begin.CancelEdit, [Is].True)
            Assert.That(provider.Moves, [Is].Zero)
        End Using
    End Sub

    <TestCase("", DmsResourceItem.ItemTypes.Root)>
    <TestCase("Scoped", DmsResourceItem.ItemTypes.Folder)>
    <TestCase("Scoped", DmsResourceItem.ItemTypes.Collection)>
    Public Sub EveryBrowserRootRejectsNativeLabelEditing(initialRoot As String, rootType As DmsResourceItem.ItemTypes)
        Dim provider As New RenameProvider With {.RootPath = initialRoot, .RootType = rootType}
        Using dispatcher As New UiTestDispatcher(), browser As New RenameBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            Dim args As New NodeLabelEditEventArgs(root)
            RaiseLabelEvent(browser, "OnBeforeLabelEdit", args)
            Assert.That(args.CancelEdit, [Is].True)
            Dim commit As New NodeLabelEditEventArgs(root, "RenamedRoot")
            RaiseLabelEvent(browser, "OnAfterLabelEdit", commit)
            dispatcher.WaitForEvents()
            Assert.That(commit.CancelEdit, [Is].True)
            Assert.That(provider.Moves, [Is].Zero)
        End Using
    End Sub

    <TestCase(Nothing), TestCase("Old"), TestCase(" Old "), TestCase("../bad"), TestCase(" ")>
    Public Sub CancellationUnchangedAndInvalidLabelsNeverWrite(label As String)
        Dim provider As New RenameProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New RenameBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            Dim args As New NodeLabelEditEventArgs(node, label)
            RaiseLabelEvent(browser, "OnAfterLabelEdit", args)
            Assert.That(args.CancelEdit, [Is].True)
            dispatcher.WaitForEvents()
            Assert.That(provider.Moves, [Is].Zero)
            Assert.That(node.Text, [Is].EqualTo("Old"))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub ValidRenameWritesOnceAndOnlyRefreshesTheLabelAfterSuccess(fail As Boolean)
        Dim provider As New RenameProvider With {.Fail = fail}
        Using dispatcher As New UiTestDispatcher(), browser As New RenameBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            browser.TreeViewDmsFolders.SelectedNode = node
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim args As New NodeLabelEditEventArgs(node, "New")
            RaiseLabelEvent(browser, "OnAfterLabelEdit", args)
            Assert.That(args.CancelEdit, [Is].True)
            Assert.That(node.Text, [Is].EqualTo("Old"))
            dispatcher.WaitForEvents()
            Assert.That(provider.Moves, [Is].EqualTo(1))
            Assert.That(browser.TreeViewDmsFolders.SelectedNode.Text, [Is].EqualTo(If(fail, "Old", "New")))
            Assert.That(provider.LastDestination, [Is].EqualTo(provider.CombinePath(provider.RootPath, "New")))
            Assert.That(browser.Failure IsNot Nothing, [Is].EqualTo(fail))
        End Using
    End Sub

    <Test>
    Public Sub PermissionIsCheckedAtBeginAndAgainBeforeTheBackendMove()
        Dim provider As New RenameProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New RenameBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim node = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowSelectOnly
            Dim begin As New NodeLabelEditEventArgs(node)
            RaiseLabelEvent(browser, "OnBeforeLabelEdit", begin)
            Assert.That(begin.CancelEdit, [Is].True)
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles
            begin = New NodeLabelEditEventArgs(node)
            RaiseLabelEvent(browser, "OnBeforeLabelEdit", begin)
            Assert.That(begin.CancelEdit, [Is].False)
            provider.BeforeLookup = Sub() browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowSelectOnly
            RaiseLabelEvent(browser, "OnAfterLabelEdit", New NodeLabelEditEventArgs(node, "New"))
            dispatcher.WaitForEvents()
            Assert.That(provider.Moves, [Is].Zero)
            Assert.That(node.Text, [Is].EqualTo("Old"))
        End Using
    End Sub

    Private Shared Sub RaiseLabelEvent(browser As DmsBrowser, method As String, args As NodeLabelEditEventArgs)
        GetType(TreeView).GetMethod(method, BindingFlags.NonPublic Or BindingFlags.Instance).Invoke(browser.TreeViewDmsFolders, {args})
    End Sub

    Private Class RenameBrowser
        Inherits DmsBrowser
        Friend Failure As Exception
        Friend Sub New(provider As RenameProvider)
            MyBase.New(provider, Nothing, "Inline rename test", Nothing, provider.RootPath, Nothing, BrowseModes.FoldersAndFiles,
                       FileOrFolderActions.AllowCopyRenameMoveFiles, DialogOperationModes.NoResults, "", "", "")
            AddHandler OperationFailed, Sub(sender, e) Failure = e.Exception
            ShowInTaskbar = False
            Opacity = 0
        End Sub
        Protected Overrides Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
        End Sub
    End Class

    Private Class RenameProvider
        Inherits NoDmsProvider
        Friend RootPath As String = ""
        Friend RootType As DmsResourceItem.ItemTypes = DmsResourceItem.ItemTypes.Root
        Friend FolderName As String = "Old"
        Friend Moves As Integer
        Friend Fail As Boolean
        Friend LastDestination As String
        Friend BeforeLookup As Action
        Public Overrides ReadOnly Property SupportsCollections As Boolean = False
        Protected Overrides ReadOnly Property SupportsAsynchronousCopyMove As Boolean = True
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            BeforeLookup?.Invoke()
            If If(path, "").Trim(DirectorySeparator) = RootPath.Trim(DirectorySeparator) Then Return Task.FromResult(New DmsResourceItem With {.FullName = RootPath, .Name = RootPath, .ItemType = RootType, .HasChildDirectories = True})
            Return Task.FromResult(Of DmsResourceItem)(Nothing)
        End Function
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim entries As New List(Of DmsResourceItem)
            If If(path, "").Trim(DirectorySeparator) = RootPath.Trim(DirectorySeparator) Then
                entries.Add(New DmsResourceItem With {.Name = FolderName, .FullName = CombinePath(RootPath, FolderName), .ExtendedInfosFolderID = "folder-id", .ItemType = DmsResourceItem.ItemTypes.Folder, .HasChildDirectories = False})
            End If
            Return Task.FromResult(entries)
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
        Protected Overrides Function MoveItemAsync(source As DmsResourceItem, destinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            Moves += 1
            LastDestination = destinationPath
            If Fail Then Throw New InvalidOperationException("Fixture rename failure")
            FolderName = "New"
            Return Task.CompletedTask
        End Function
    End Class
End Class
