Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports Ui = CompuMaster.Dms.BrowserUI

<TestFixture, Apartment(ApartmentState.STA)>
Public Class DmsBrowserCustomizationTest
    Private Shared Function Field(Of T)(browser As Ui.DmsBrowser, name As String) As T
        Dim info = GetType(Ui.DmsBrowser).GetField(name, BindingFlags.Instance Or BindingFlags.NonPublic)
        If info Is Nothing Then info = GetType(Ui.DmsBrowser).GetField("_" & name, BindingFlags.Instance Or BindingFlags.NonPublic)
        Return DirectCast(info.GetValue(browser), T)
    End Function
    Private Shared Function Invoke(browser As Ui.DmsBrowser, name As String, ParamArray args As Object()) As Object
        Return GetType(Ui.DmsBrowser).GetMethod(name, BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(browser, args)
    End Function
    Private Class Provider
        Inherits NoDmsProvider
        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return True
            End Get
        End Property
        Public Overrides ReadOnly Property SupportsFileVersionHistory As Boolean
            Get
                Return True
            End Get
        End Property
    End Class

    <TestCase(Ui.BrowserToolbarActions.Default), TestCase(Ui.BrowserToolbarActions.None), TestCase(Ui.BrowserToolbarActions.All)>
    Public Sub ToolbarSettingsPreserveContextMenusAndRemoveEmptySpace(actions As Ui.BrowserToolbarActions)
        Using browser As New Ui.DmsBrowser(New Provider())
            browser.BrowseMode = Ui.DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = CType(2047, Ui.DmsBrowser.FileOrFolderActions)
            browser.ToolbarActions = actions
            browser.ShowInTaskbar = False : browser.Opacity = 0 : browser.Show()
            Assert.That(Field(Of ToolStripButton)(browser, "ToolStripButtonOpenFile").Available, [Is].EqualTo((actions And Ui.BrowserToolbarActions.Open) <> 0))
            Assert.That(Field(Of ToolStripButton)(browser, "ToolStripButtonDeleteFile").Available, [Is].EqualTo((actions And Ui.BrowserToolbarActions.Delete) <> 0))
            Assert.That(Field(Of ToolStripButton)(browser, "ToolStripFileContextButtonDeleteFile").Available, [Is].True)
            Assert.That(browser.VersionHistoryContext.Available, [Is].True)
            Assert.That(browser.VersionHistoryButton.Available, [Is].EqualTo((actions And Ui.BrowserToolbarActions.VersionHistory) <> 0))
            Dim host = Field(Of FlowLayoutPanel)(browser, "FlowLayoutPanel1")
            Assert.That(host.Visible, [Is].EqualTo(actions <> Ui.BrowserToolbarActions.None))
            If actions = Ui.BrowserToolbarActions.None Then Assert.That(Field(Of ListView)(browser, "ListViewDmsFiles").Top, [Is].Zero)
            browser.ToolbarActions = Ui.BrowserToolbarActions.All
            Assert.That(Field(Of ToolStripButton)(browser, "ToolStripButtonDeleteFile").Available, [Is].True, "Runtime settings can restore an allowed button.")
            browser.AllowedActions = Ui.DmsBrowser.FileOrFolderActions.AllowSelectOnly
            Assert.That(Field(Of ToolStripButton)(browser, "ToolStripButtonDeleteFile").Available, [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub BottomButtonVisibilityIsIndependentOfRightsAndThemeSelectorIsAbsent()
        Using browser As New Ui.DmsBrowser(New Provider())
            browser.AllowedActions = Ui.DmsBrowser.FileOrFolderActions.AllowCreateFolders Or Ui.DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode
            browser.ShowFilesButtonVisible = False : browser.CreateFolderButtonVisible = False
            browser.ShowInTaskbar = False : browser.Opacity = 0 : browser.Show()
            Assert.That(Field(Of Button)(browser, "ButtonCreateNewFolder").Visible, [Is].False)
            Assert.That(Field(Of CheckBox)(browser, "ButtonShowFiles").Visible, [Is].False)
            Assert.That(Field(Of ToolStripButton)(browser, "ToolStripFolderContextButtonNewFolder").Available, [Is].True)
            Assert.That(browser.Controls.Find("BrowserThemeSelector", True), [Is].Empty)
            Assert.That(Field(Of Button)(browser, "ButtonCreateNewFolder").Image, [Is].Not.Null)
            Assert.That(Field(Of Button)(browser, "ButtonClose").Image, [Is].Not.Null)
            browser.ShowFilesButtonVisible = True : browser.CreateFolderButtonVisible = True
            Assert.That(Field(Of Button)(browser, "ButtonCreateNewFolder").Visible, [Is].True)
            Assert.That(Field(Of CheckBox)(browser, "ButtonShowFiles").Visible, [Is].True)
        End Using
    End Sub

    <Test>
    Public Sub InternalDragSettingAndLocalRestrictionDoNotGrantRightsOrLeakExternalCopies()
        Using browser As New Ui.DmsBrowser(New Provider())
            browser.BrowseMode = Ui.DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = Ui.DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or Ui.DmsBrowser.FileOrFolderActions.AllowDownloadFiles
            Assert.That(CBool(Invoke(browser, "CanStartRemoteMove")), [Is].True)
            browser.EnableInternalDragAndDrop = False
            Assert.That(CBool(Invoke(browser, "CanStartRemoteMove")), [Is].False)
            Assert.That(browser.CanDragDownload(), [Is].True)
            Dim parent = Path.Combine(Path.GetTempPath(), "unittests_drag_boundary")
            browser.LocalParentMustFolder = parent
            Assert.That(browser.CanDragDownload(), [Is].False)
            Assert.That(browser.IsAllowedLocalTransferPath(Path.Combine(parent, "file.txt")), [Is].True)
            Assert.That(browser.IsAllowedLocalTransferPath(Path.Combine(parent & "_other", "file.txt")), [Is].False)
            Assert.That(browser.IsAllowedLocalTransferPath(Path.Combine(parent, "..", "file.txt")), [Is].False)
            browser.EnableInternalDragAndDrop = True
            Assert.That(CBool(Invoke(browser, "CanStartRemoteMove")), [Is].True)
            browser.AllowedActions = Ui.DmsBrowser.FileOrFolderActions.AllowSelectOnly
            Assert.That(CBool(Invoke(browser, "CanStartRemoteMove")), [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub SortingShowsDirectionAndTogglesWithoutLosingTheChosenColumn()
        Using browser As New Ui.DmsBrowser(New Provider())
            Dim list = Field(Of ListView)(browser, "ListViewDmsFiles")
            Dim items As New List(Of DmsResourceItem) From {
                New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "older.txt", .FullName = "older.txt", .LastModificationOnLocalTime = New DateTime(2026, 1, 1)},
                New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "newer.txt", .FullName = "newer.txt", .LastModificationOnLocalTime = New DateTime(2026, 2, 1)}}
            list.Tag = items
            Invoke(browser, "DisplayFiles", items)
            Assert.That(list.Items(0).Text, [Is].EqualTo("newer.txt"))
            Assert.That(list.Columns(2).Text, Does.EndWith("▼"))
            Invoke(browser, "ListViewDmsFiles_ColumnClick", list, New ColumnClickEventArgs(2))
            Assert.That(list.Items(0).Text, [Is].EqualTo("older.txt"))
            Assert.That(list.Columns(2).Text, Does.EndWith("▲"))
            Invoke(browser, "ListViewDmsFiles_ColumnClick", list, New ColumnClickEventArgs(2))
            Assert.That(list.Items(0).Text, [Is].EqualTo("newer.txt"))
            Invoke(browser, "ListViewDmsFiles_ColumnClick", list, New ColumnClickEventArgs(0))
            Assert.That(list.Items(0).Text, [Is].EqualTo("newer.txt"))
            Assert.That(list.Columns(0).Text, Does.EndWith("▲"))
            Assert.That(list.Columns(2).Text, Does.Not.EndWith("▼"))
            Invoke(browser, "ListViewDmsFiles_ColumnClick", list, New ColumnClickEventArgs(0))
            Assert.That(list.Items(0).Text, [Is].EqualTo("older.txt"))
            Assert.That(list.Columns(0).Text, Does.EndWith("▼"))
        End Using
    End Sub
    Private Class PreviewBrowser
        Inherits Ui.DmsBrowser
        Public Sub New(mode As Ui.DmsBrowser.DialogOperationModes)
            MyBase.New(New PreviewProvider(), Nothing, "Offline double-click test", Nothing, Nothing, Nothing,
                Ui.DmsBrowser.BrowseModes.FoldersAndFiles, Ui.DmsBrowser.FileOrFolderActions.AllowDownloadFiles, mode, Nothing, Nothing, Nothing)
            ShowInTaskbar = False : Opacity = 0
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
            'The selected resources are supplied by this test, without startup navigation.
        End Sub
    End Class
    Private Class PreviewProvider
        Inherits NoDmsProvider
        Public Overrides Sub DownloadFile(remoteFile As DmsResourceItem, localFilePath As String)
            File.WriteAllText(localFilePath, remoteFile.Name)
        End Sub
    End Class

    <TestCase(Ui.DmsBrowser.DialogOperationModes.NoResults), TestCase(Ui.DmsBrowser.DialogOperationModes.ReturnSelectedItems)>
    Public Sub DoubleClickOpensByDefaultAndNoneOrMissingPermissionDoesNothing(mode As Ui.DmsBrowser.DialogOperationModes)
        Using dispatcher As New UiTestDispatcher(), browser As New PreviewBrowser(mode)
            browser.Show()
            Dim list = Field(Of ListView)(browser, "ListViewDmsFiles")
            For Each name In New String() {"unittests_first.txt", "unittests_second.txt"}
                list.Items.Add(New ListViewItem(name) With {.Tag = New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = name, .FullName = name}, .Selected = True})
            Next
            Dim opened As New List(Of String)
            browser.OpenDownloadedFileItem = Function(file)
                                                 opened.Add(file.FilePath)
                                                 Return Nothing
                                             End Function
            Assert.That(browser.FileDoubleClickAction, [Is].EqualTo(Ui.BrowserFileDoubleClickAction.Open))
            browser.FileDoubleClickAction = Ui.BrowserFileDoubleClickAction.None
            browser.ActivateSelectedFiles() : dispatcher.WaitForEvents()
            Assert.That(opened, [Is].Empty)
            browser.FileDoubleClickAction = Ui.BrowserFileDoubleClickAction.Open
            browser.AllowedActions = Ui.DmsBrowser.FileOrFolderActions.AllowSelectOnly
            browser.ActivateSelectedFiles() : dispatcher.WaitForEvents()
            Assert.That(opened, [Is].Empty)
            browser.AllowedActions = Ui.DmsBrowser.FileOrFolderActions.AllowDownloadFiles
            browser.ActivateSelectedFiles() : dispatcher.WaitForEvents()
            Assert.That(opened.Count, [Is].EqualTo(2))
            Assert.That(opened.All(AddressOf File.Exists), [Is].True)
            Assert.That(browser.DialogResult, [Is].EqualTo(DialogResult.None), "Double click does not confirm a selection.")
        End Using
    End Sub

    <Test>
    Public Sub UploadRestrictionRejectsSymbolicLinksInAncestors()
        Dim root = Path.Combine(Path.GetTempPath(), "unittests_browser_paths_" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root) OrElse File.Exists(root), [Is].False)
        Dim allowed = Path.Combine(root, "allowed")
        Dim outside = Path.Combine(root, "outside")
        Dim link = Path.Combine(allowed, "link")
        Try
            Directory.CreateDirectory(allowed) : Directory.CreateDirectory(outside)
            Try
                Directory.CreateSymbolicLink(link, outside)
            Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is IOException OrElse TypeOf ex Is PlatformNotSupportedException
                Assert.Ignore("Symbolic link creation is unavailable: " & ex.GetType().Name)
            End Try
            Using browser As New Ui.DmsBrowser(New Provider())
                browser.LocalParentMustFolder = allowed
                Assert.That(browser.IsAllowedLocalTransferPath(Path.Combine(link, "file.txt")), [Is].False)
                Assert.Throws(Of IOException)(Sub() Ui.LocalUploadPlan.ValidateLocalPath(Path.Combine(link, "file.txt"), allowed))
            End Using
        Finally
            If Directory.Exists(link) Then Directory.Delete(link)
            If Directory.Exists(root) Then Directory.Delete(root, True)
            Assert.That(Directory.Exists(root), [Is].False)
        End Try
    End Sub

    <Test>
    Public Sub IncomingDragRejectsOutsideAndMixedSelectionsBeforeAcceptingACopy()
        Using dispatcher As New UiTestDispatcher(), browser As New PreviewBrowser(Ui.DmsBrowser.DialogOperationModes.NoResults)
            browser.AllowedActions = Ui.DmsBrowser.FileOrFolderActions.AllowUploadFiles
            browser.Show()
            Dim tree = Field(Of TreeView)(browser, "TreeViewDmsFolders")
            Dim list = Field(Of ListView)(browser, "ListViewDmsFiles")
            GetType(Ui.DmsBrowser).GetField("SuppressSelectionRefresh", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(browser, True)
            Dim target = Ui.DmsBrowser.CreateDirectoryTreeNode(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = "unittests_target", .FullName = "unittests_target"})
            tree.Nodes.Add(target) : tree.SelectedNode = target
            Dim parent = Path.Combine(Path.GetTempPath(), "unittests_drag_boundary")
            browser.LocalParentMustFolder = parent
            Dim allowed = Path.Combine(parent, "file.txt")
            Dim outside = Path.Combine(parent & "_other", "file.txt")
            Assert.That(browser.CanUploadLocalPaths({allowed}, target), [Is].True)
            For Each paths In New String()() {New String() {allowed}, New String() {outside}, New String() {allowed, outside}}
                Dim data As New DataObject(DataFormats.FileDrop, paths)
                Dim point = list.PointToScreen(Drawing.Point.Empty)
                Dim args As New DragEventArgs(data, 0, point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None)
                Invoke(browser, "DragUploadOver", list, args)
                Assert.That(args.Effect, [Is].EqualTo(If(paths.Contains(outside), DragDropEffects.None, DragDropEffects.Copy)))
                If paths.Contains(outside) Then
                    Invoke(browser, "DropLocalPaths", list, args)
                    dispatcher.WaitForEvents()
                End If
            Next
        End Using
    End Sub

End Class
