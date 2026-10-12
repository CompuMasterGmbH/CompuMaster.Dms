Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class TreeRefreshStateTest
    <Test>
    Public Sub SelectionAndExpansionSurviveCompleteNodeReplacementDuringRefresh()
        Dim provider As New TreeProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New TreeBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Dim parent = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "Folder40")
            dispatcher.Finish(browser.AddTreeChildrenAsync(parent))
            parent.Expand()
            Dim previous = parent.Nodes(0)
            browser.TreeViewDmsFolders.SelectedNode = previous
            dispatcher.WaitForEvents()
            provider.BeforeRootListing = Async Function()
                                             provider.BeforeRootListing = Nothing
                                             Await browser.LoadTreeAsync()
                                         End Function
            dispatcher.Finish(browser.RefreshCurrentFolderAndFilesAsync())
            Dim selected = browser.TreeViewDmsFolders.SelectedNode
            Assert.That(Object.ReferenceEquals(selected, previous), [Is].False)
            Assert.That(selected.Text, [Is].EqualTo("Child"))
            Assert.That(selected.Parent.Text, [Is].EqualTo("Folder40"))
            Assert.That(selected.Parent.IsExpanded, [Is].True)
            Assert.That(selected.IsVisible, [Is].True)
            Assert.That(provider.LastFilesPath, [Is].EqualTo("Folder40/Child"))
        End Using
    End Sub
    <TestCase(False), TestCase(True)>
    Public Sub RefreshKeepsExpandedSelectionAfterMetadataOrPathChanges(rename As Boolean)
        Dim provider As New TreeProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New TreeBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            Dim parent = root.Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "Folder40")
            dispatcher.Finish(browser.AddTreeChildrenAsync(parent))
            parent.Expand()
            browser.TreeViewDmsFolders.SelectedNode = parent.Nodes(0)
            dispatcher.WaitForEvents()
            If rename Then provider.ParentName = "Renamed40"
            dispatcher.Finish(browser.RefreshCurrentFolderAndFilesAsync())
            Dim selected = browser.TreeViewDmsFolders.SelectedNode
            Assert.That(selected.Text, [Is].EqualTo("Child"))
            Assert.That(selected.Parent.Text, [Is].EqualTo(provider.ParentName))
            Assert.That(selected.Parent.IsExpanded, [Is].True)
            Assert.That(selected.IsVisible, [Is].True)
            Assert.That(provider.LastFilesPath, [Is].EqualTo(provider.ParentName & "/Child"))
            Assert.That(browser.Failure, [Is].Null, browser.Failure?.ToString())
        End Using
    End Sub

    <Test>
    Public Sub RefreshingADropDestinationKeepsThePreviouslySelectedOtherBranch()
        Dim provider As New TreeProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New TreeBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Dim root = browser.TreeViewDmsFolders.Nodes(0)
            Dim selected = root.Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "Folder40")
            browser.TreeViewDmsFolders.SelectedNode = selected
            dispatcher.WaitForEvents()
            Dim target = root.Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "Folder01")
            dispatcher.Finish(browser.RefreshTreePreservingViewAsync(target))
            Assert.That(browser.TreeViewDmsFolders.SelectedNode, [Is].SameAs(selected))
            Assert.That(provider.LastFilesPath, [Is].EqualTo("Folder40"))
        End Using
    End Sub

    <Test>
    Public Sub RemovedSelectionFallsBackToItsSurvivingParent()
        Dim provider As New TreeProvider()
        Using dispatcher As New UiTestDispatcher(), browser As New TreeBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            Dim parent = browser.TreeViewDmsFolders.Nodes(0).Nodes.Cast(Of TreeNode)().Single(Function(node) node.Text = "Folder40")
            dispatcher.Finish(browser.AddTreeChildrenAsync(parent))
            browser.TreeViewDmsFolders.SelectedNode = parent.Nodes(0)
            dispatcher.WaitForEvents()
            provider.RemoveChild = True
            dispatcher.Finish(browser.RefreshCurrentFolderAndFilesAsync())
            Assert.That(browser.TreeViewDmsFolders.SelectedNode, [Is].SameAs(parent))
            Assert.That(provider.LastFilesPath, [Is].EqualTo("Folder40"))
        End Using
    End Sub

    Private Class TreeBrowser
        Inherits DmsBrowser
        Friend Failure As Exception
        Friend Sub New(provider As TreeProvider)
            MyBase.New(provider, Nothing, "Tree refresh test", Nothing, "", Nothing, BrowseModes.FoldersAndFiles,
                       FileOrFolderActions.AllowUploadFiles, DialogOperationModes.NoResults, "", "", "")
            ShowInTaskbar = False
            Opacity = 0
            AddHandler OperationFailed, Sub(sender, e) Failure = e.Exception
        End Sub
        Protected Overrides Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
        End Sub
    End Class

    Private Class TreeProvider
        Inherits NoDmsProvider
        Friend ParentName As String = "Folder40"
        Friend RemoveChild As Boolean
        Friend LastFilesPath As String
        Friend BeforeRootListing As Func(Of Task)
        Public Overrides ReadOnly Property SupportsCollections As Boolean = False
        Public Overrides Async Function ListDirectoryEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim items As New List(Of DmsResourceItem)
            If String.IsNullOrEmpty(path) Then
                If BeforeRootListing IsNot Nothing Then Await BeforeRootListing()
                For index = 0 To 49
                    Dim name = If(index = 40, ParentName, "Folder" & index.ToString("00"))
                    items.Add(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = name, .Name = name,
                        .ExtendedInfosFolderID = index.ToString(), .HasChildDirectories = index = 40})
                Next
            ElseIf path = ParentName AndAlso Not RemoveChild Then
                items.Add(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .FullName = ParentName & "/Child", .Name = "Child",
                    .ExtendedInfosFolderID = "child", .HasChildDirectories = False})
            End If
            Return items
        End Function
        Public Overrides Function ListEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return ListDirectoryEntriesAsync(path, cancellationToken)
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            LastFilesPath = path
            Return Task.FromResult(New List(Of DmsResourceItem))
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
    End Class
End Class
