Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserStartupTest

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
