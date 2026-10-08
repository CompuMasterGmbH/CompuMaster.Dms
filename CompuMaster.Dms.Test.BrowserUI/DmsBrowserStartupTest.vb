Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserStartupTest

    <TestCase(True, False, "", "")>
    <TestCase(False, False, "", "")>
    <TestCase(True, True, "", "")>
    <TestCase(True, False, "", "Child")>
    <TestCase(True, False, "Parent", "")>
    <TestCase(False, False, "Parent", "Child")>
    Public Sub InitialTreeAndFilesShareOnlyTheirOwnOperationSnapshot(rootFiles As Boolean, foldersOnly As Boolean, initialRoot As String, selectedPath As String)
        Dim provider As New CombinedStartupProvider(rootFiles)
        Dim mode = If(foldersOnly, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.Folders, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles)
        Using dispatcher As New UiTestDispatcher,
              browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider, Nothing, "Combined startup", Nothing, initialRoot, selectedPath, mode, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.DialogOperationModes.NoResults, "", "", "") With {.ShowInTaskbar = False, .Opacity = 0}
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.UseWaitCursor, [Is].False)
            Assert.That(browser.Enabled, [Is].True)
            Dim combined = Not foldersOnly AndAlso (rootFiles OrElse initialRoot <> "") AndAlso selectedPath = ""
            Assert.That(provider.CombinedCalls, [Is].EqualTo(If(combined, 1, 0)))
            Assert.That(provider.DirectoryCalls, [Is].EqualTo(If(combined, 0, 1)))
            Dim child = browser.TreeViewDmsFolders.Nodes(0).Nodes(0)
            Assert.That(child.ImageIndex, [Is].EqualTo(5), "Shared-folder symbols must be retained.")
            Assert.That(child.Nodes.Count, [Is].EqualTo(1), "Unknown child metadata must retain the lazy expansion placeholder.")
            Dim fileVisible = Not foldersOnly AndAlso (rootFiles OrElse initialRoot <> "" OrElse selectedPath <> "")
            Assert.That(browser.ListViewDmsFiles.Items.Count, [Is].EqualTo(If(fileVisible, 1, 0)))
            Assert.That(provider.FileCalls, [Is].EqualTo(If(selectedPath <> "", 1, 0)))
            If fileVisible Then
                Dim file = DirectCast(browser.ListViewDmsFiles.Items(0).Tag, DmsResourceItem)
                Assert.That(file.Folder.Trim(provider.DirectorySeparator), [Is].EqualTo(provider.CombinePath(initialRoot, selectedPath).Trim(provider.DirectorySeparator)), "A parent snapshot must not supply files for the selected child.")
                Assert.That(file.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Resource owner"))
                provider.Revision = 2
                dispatcher.Finish(browser.RefreshFilesListAsync())
                Assert.That(browser.ListViewDmsFiles.Items(0).Text, [Is].EqualTo("revision2.txt"), "Later file refreshes must request a fresh listing.")
                Assert.That(provider.FileCalls, [Is].EqualTo(If(selectedPath <> "", 2, 1)))
            End If
            browser.Close()
            dispatcher.WaitForEvents()
        End Using
    End Sub

    Private Class CombinedStartupProvider
        Inherits NoDmsProvider
        Private ReadOnly RootFiles As Boolean
        Public CombinedCalls As Integer
        Public DirectoryCalls As Integer
        Public FileCalls As Integer
        Public Revision As Integer = 1
        Public Sub New(rootFiles As Boolean)
            Me.RootFiles = rootFiles
        End Sub
        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return RootFiles
            End Get
        End Property
        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return ""
            End Get
        End Property
        Private Function DirectoryEntries(path As String) As List(Of DmsResourceItem)
            Return New List(Of DmsResourceItem) From {New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = CombinePath(path, "Child"), .Name = "Child", .ExtendedInfosIsShared = True}}
        End Function
        Private Function FileEntries(path As String) As List(Of DmsResourceItem)
            Dim name = "revision" & Revision & ".txt"
            Return New List(Of DmsResourceItem) From {New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = name, .FullName = CombinePath(path, name), .Folder = path, .ExtendedInfosOwner = New DmsUser With {.DisplayName = "Resource owner"}}}
        End Function
        Public Overrides Function ListEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            CombinedCalls += 1
            Dim items = DirectoryEntries(path)
            items.AddRange(FileEntries(path))
            Return Task.FromResult(items)
        End Function
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            DirectoryCalls += 1
            Return Task.FromResult(DirectoryEntries(path))
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            FileCalls += 1
            Return Task.FromResult(FileEntries(path))
        End Function
        Public Overrides Function ListRemoteItemAsync(path As String, Optional token As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Task.FromResult(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .Name = path, .FullName = path, .HasChildDirectories = True})
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
    End Class

    <TestCase(False)>
    <TestCase(True)>
    Public Sub ModalStartupCompletesDelayedListingOnTheWindowsFormsContext(emptyRoot As Boolean)
        Dim previousContext = SynchronizationContext.Current
        Dim previousAutoInstall = WindowsFormsSynchronizationContext.AutoInstall
        SynchronizationContext.SetSynchronizationContext(Nothing)
        WindowsFormsSynchronizationContext.AutoInstall = True
        Try
            Dim provider As New StartupProvider
            Using owner As New Form With {.ShowInTaskbar = False, .Opacity = 0},
                  browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider, Nothing, "Startup test", Nothing, "", "", Global.CompuMaster.Dms.BrowserUI.DmsBrowser.BrowseModes.FoldersAndFiles, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.FileOrFolderActions.AllowUploadFiles, Global.CompuMaster.Dms.BrowserUI.DmsBrowser.DialogOperationModes.NoResults, "", "", "") With {.ShowInTaskbar = False, .Opacity = 0},
                  watchdog As New System.Windows.Forms.Timer With {.Interval = 50}
                owner.Show()
                Dim deadline = DateTime.UtcNow.AddSeconds(5)
                Dim listingReleased As Boolean
                Dim startupCompleted As Boolean
                AddHandler watchdog.Tick,
                    Sub()
                        If provider.ListingRequested AndAlso Not listingReleased Then
                            listingReleased = True
                            Dim children As New List(Of DmsResourceItem)
                            If Not emptyRoot Then children.Add(New DmsResourceItem With {.Name = "Folder", .FullName = "Folder", .ItemType = DmsResourceItem.ItemTypes.Folder, .ChildDirectoryCount = 0})
                            provider.Listing.SetResult(children)
                        ElseIf listingReleased AndAlso browser.Enabled AndAlso browser.TreeViewDmsFolders.Nodes.Count = 1 AndAlso browser.TreeViewDmsFolders.Nodes(0).Nodes.Count = If(emptyRoot, 0, 1) Then
                            startupCompleted = True
                            browser.Close()
                        ElseIf DateTime.UtcNow >= deadline Then
                            browser.Close()
                        End If
                    End Sub
                watchdog.Start()
                browser.ShowDialog(owner)
                watchdog.Stop()
                Assert.That(listingReleased, [Is].True, "The modal browser must request its initial listing.")
                Assert.That(startupCompleted, [Is].True, $"The initial listing must populate the tree and restore the enabled state. Enabled={browser.Enabled}; roots={browser.TreeViewDmsFolders.Nodes.Count}; children={If(browser.TreeViewDmsFolders.Nodes.Count = 0, -1, browser.TreeViewDmsFolders.Nodes(0).Nodes.Count)}; wait={browser.UseWaitCursor}.")
                owner.Close()
            End Using
        Finally
            SynchronizationContext.SetSynchronizationContext(previousContext)
            WindowsFormsSynchronizationContext.AutoInstall = previousAutoInstall
        End Try
    End Sub

    Private Class StartupProvider
        Inherits NoDmsProvider

        Public ReadOnly Listing As New TaskCompletionSource(Of List(Of DmsResourceItem))(TaskCreationOptions.RunContinuationsAsynchronously)
        Public ListingRequested As Boolean

        Public Overrides Function ListAllDirectoryItemsAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            ListingRequested = True
            Return Listing.Task
        End Function

        Public Overrides Function ListAllFileItemsAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return Task.FromResult(New List(Of DmsResourceItem))
        End Function

        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
    End Class
End Class
