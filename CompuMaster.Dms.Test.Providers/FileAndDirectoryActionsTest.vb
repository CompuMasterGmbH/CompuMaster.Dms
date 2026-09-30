Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
<Parallelizable(ParallelScope.All)>
Public Class FileAndDirectoryActionsTest

    <Test>
    Public Sub ItemOverloadPreservesSelectedCollidingSource()
        Dim Provider As New IdentityAwareProvider(True)
        Dim Source As DmsResourceItem = Item("source/duplicate.txt", DmsResourceItem.ItemTypes.File, True)
        Source.ExtendedInfosFileID = "selected-file-id"

        Provider.Copy(Source, "copy.txt", False, False)

        Assert.That(Provider.CopiedSource, [Is].SameAs(Source))
        Assert.That(Provider.CopiedSource.ExtendedInfosFileID, [Is].EqualTo("selected-file-id"))
    End Sub

    <Test>
    Public Sub PathOverloadRejectsCollidingSource()
        Dim Provider As New IdentityAwareProvider(True)
        Provider.Items.Add("source/duplicate.txt", Item("source/duplicate.txt", DmsResourceItem.ItemTypes.File, True))

        Assert.Throws(Of RemotePathNotUniqueException)(Sub() Provider.Copy("source/duplicate.txt", "copy.txt"))
        Assert.That(Provider.CopiedSource, [Is].Null)
    End Sub

    <Test>
    Public Sub DestinationCollisionIsRejectedEvenWhenOverwriteIsAllowed()
        Dim Provider As New IdentityAwareProvider(True)
        Dim Source As DmsResourceItem = Item("source.txt", DmsResourceItem.ItemTypes.File)
        Provider.Items.Add("target.txt", Item("target.txt", DmsResourceItem.ItemTypes.File))
        Provider.ListedItems.Add(Item("target.txt", DmsResourceItem.ItemTypes.File))
        Provider.ListedItems.Add(Item("target.txt", DmsResourceItem.ItemTypes.File))

        Assert.Throws(Of RemotePathNotUniqueException)(Sub() Provider.Copy(Source, "target.txt", True, False))
        Assert.That(Provider.CopiedSource, [Is].Null)
    End Sub

    <Test>
    Public Sub RootCollectionRenameIsNotRejectedByRootFileRestriction()
        Dim Provider As New IdentityAwareProvider(False)
        Dim Source As DmsResourceItem = Item("Collection-A", DmsResourceItem.ItemTypes.Collection)
        Source.ExtendedInfosCollectionID = "collection-id"

        Provider.Move(Source, "Collection-B", False, False)

        Assert.That(Provider.MovedSource, [Is].SameAs(Source))
        Assert.That(Provider.MovedDestination, [Is].EqualTo("Collection-B"))
    End Sub

    <Test>
    Public Sub LegacyFileHookRemainsTheDefaultBridge()
        Dim Provider As New LegacyBridgeProvider()
        Provider.Items.Add("source.txt", Item("source.txt", DmsResourceItem.ItemTypes.File))

        Provider.Copy("source.txt", "copy.txt")

        Assert.That(Provider.CopiedFiles, [Is].EquivalentTo(New String() {"source.txt->copy.txt"}))
    End Sub

    <Test>
    Public Sub DirectoryOverwriteCopiesByMergingAndKeepsTargetOnlyItems()
        Dim Provider As LegacyBridgeProvider = CreateMergeProvider()

        Provider.Copy(Provider.Items("source"), "target", True, False)

        Assert.That(Provider.CopiedFiles, [Is].EquivalentTo(New String() {"source/replaced.txt->target/replaced.txt", "source/new.txt->target/new.txt"}))
        Assert.That(Provider.DeletedItems, [Is].Empty)
        Assert.That(Provider.Items.ContainsKey("target/keep.txt"), [Is].True)
    End Sub

    <Test>
    Public Sub DirectoryOverwriteMoveDeletesSourceOnlyAfterChildrenMoved()
        Dim Provider As LegacyBridgeProvider = CreateMergeProvider()

        Provider.Move(Provider.Items("source"), "target", True, False)

        Assert.That(Provider.MovedFiles, [Is].EquivalentTo(New String() {"source/replaced.txt->target/replaced.txt", "source/new.txt->target/new.txt"}))
        Assert.That(Provider.DeletedItems, Has.Count.EqualTo(1))
        Assert.That(Provider.DeletedItems(0), [Is].SameAs(Provider.Items("source")))
    End Sub

    <Test>
    Public Sub DirectoryOverwriteMoveKeepsSourceDirectoryAfterChildFailure()
        Dim Provider As LegacyBridgeProvider = CreateMergeProvider()
        Provider.FailMoveSourcePath = "source/new.txt"

        Assert.Throws(Of InvalidOperationException)(Sub() Provider.Move(Provider.Items("source"), "target", True, False))

        Assert.That(Provider.DeletedItems, [Is].Empty)
    End Sub

    Private Shared Function CreateMergeProvider() As LegacyBridgeProvider
        Dim Provider As New LegacyBridgeProvider()
        Provider.Items.Add("source", Item("source", DmsResourceItem.ItemTypes.Folder))
        Provider.Items.Add("target", Item("target", DmsResourceItem.ItemTypes.Folder))
        Provider.Items.Add("source/replaced.txt", Item("source/replaced.txt", DmsResourceItem.ItemTypes.File))
        Provider.Items.Add("source/new.txt", Item("source/new.txt", DmsResourceItem.ItemTypes.File))
        Provider.Items.Add("target/replaced.txt", Item("target/replaced.txt", DmsResourceItem.ItemTypes.File))
        Provider.Items.Add("target/keep.txt", Item("target/keep.txt", DmsResourceItem.ItemTypes.File))
        Return Provider
    End Function

    Private Shared Function Item(fullName As String, itemType As DmsResourceItem.ItemTypes, Optional collision As Boolean = False) As DmsResourceItem
        Dim SeparatorIndex As Integer = fullName.LastIndexOf("/"c)
        Return New DmsResourceItem With {
            .FullName = fullName,
            .Name = If(SeparatorIndex < 0, fullName, fullName.Substring(SeparatorIndex + 1)),
            .ItemType = itemType,
            .ExtendedInfosCollisionDetected = collision
        }
    End Function

    Private Class IdentityAwareProvider
        Inherits NoDmsProvider

        Private ReadOnly _supportsFilesInRoot As Boolean

        Public Sub New(supportsFilesInRoot As Boolean)
            _supportsFilesInRoot = supportsFilesInRoot
        End Sub

        Public ReadOnly Items As New Dictionary(Of String, DmsResourceItem)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly ListedItems As New List(Of DmsResourceItem)
        Public Property CopiedSource As DmsResourceItem
        Public Property MovedSource As DmsResourceItem
        Public Property MovedDestination As String

        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return _supportsFilesInRoot
            End Get
        End Property

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Dim Result As DmsResourceItem = Nothing
            Items.TryGetValue(remotePath, Result)
            Return Result
        End Function

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Return New List(Of DmsResourceItem)(ListedItems)
        End Function

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
        End Sub

        Protected Overrides Sub CopyItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            CopiedSource = remoteSource
        End Sub

        Protected Overrides Sub MoveItem(remoteSource As DmsResourceItem, remoteDestinationPath As String, allowOverwrite As Boolean?)
            MovedSource = remoteSource
            MovedDestination = remoteDestinationPath
        End Sub
    End Class

    Private Class LegacyBridgeProvider
        Inherits NoDmsProvider

        Public ReadOnly Items As New Dictionary(Of String, DmsResourceItem)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly CopiedFiles As New List(Of String)
        Public ReadOnly MovedFiles As New List(Of String)
        Public ReadOnly DeletedItems As New List(Of DmsResourceItem)
        Public Property FailMoveSourcePath As String

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Dim Result As DmsResourceItem = Nothing
            Items.TryGetValue(remotePath, Result)
            Return Result
        End Function

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Candidate As DmsResourceItem In Items.Values
                If Candidate.FullName <> remoteFolderPath AndAlso Me.ParentDirectoryPath(Candidate.FullName) = remoteFolderPath Then Result.Add(Candidate)
            Next
            Return Result
        End Function

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
        End Sub

        Protected Overrides Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            CopiedFiles.Add(remoteSourcePath & "->" & remoteDestinationPath)
        End Sub

        Protected Overrides Async Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            CopyFileItem(remoteSourcePath, remoteDestinationPath, allowOverwrite)
            Await Task.CompletedTask
        End Function

        Protected Overrides Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            If remoteSourcePath = FailMoveSourcePath Then Throw New InvalidOperationException("Simulated move failure")
            MovedFiles.Add(remoteSourcePath & "->" & remoteDestinationPath)
        End Sub

        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            DeletedItems.Add(remoteItem)
        End Sub
    End Class

End Class
