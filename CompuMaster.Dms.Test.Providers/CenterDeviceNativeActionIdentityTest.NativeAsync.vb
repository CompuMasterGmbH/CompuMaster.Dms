Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CenterDevice.Rest.Clients.Folders
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceNativeActionIdentityTest
    <Test>
    Public Async Function SelectedFolderReconstructsParentsByIdWithoutChoosingDuplicatePaths() As Task
        Dim provider As New ActionProvider()
        Dim directory = Await provider.DirectoryAsync(FolderItem(), CancellationToken.None)
        Assert.That(directory.FolderID, [Is].EqualTo("child"))
        Assert.That(directory.ParentDirectory.FolderID, [Is].EqualTo("parent"))
        Assert.That(directory.AssociatedCollection.CollectionID, [Is].EqualTo("c1"))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"folder:child", "folder:parent", "collection:c1"}))
        Assert.That(provider.Io.CollectionCalls, [Is].Zero)
    End Function

    <Test>
    Public Async Function SelectedCollectionDoesNotSearchRootNames() As Task
        Dim provider As New ActionProvider()
        Dim item As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Collection, .ExtendedInfosCollectionID = "c2", .FullName = "duplicate"}
        Assert.That((Await provider.DirectoryAsync(item, CancellationToken.None)).CollectionID, [Is].EqualTo("c2"))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"collection:c2"}))
        Assert.That(provider.Io.CollectionCalls, [Is].Zero)
    End Function

    <Test>
    Public Sub FolderParentCyclesFailWithoutUnboundedRequests()
        Dim provider As New ActionProvider()
        provider.Folders("parent").Parent = "child"
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.DirectoryAsync(FolderItem(), CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"folder:child", "folder:parent"}))
    End Sub

    <Test>
    Public Sub FolderMovedToAnotherCollectionDoesNotUseTheOldSelection()
        Dim provider As New ActionProvider()
        provider.Folders("child").Collection = "c2"
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.DirectoryAsync(FolderItem(), CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"folder:child"}))
    End Sub

    <Test>
    Public Sub RootFolderWithoutCollectionMetadataCannotChooseAnArbitraryCollection()
        Dim provider As New ActionProvider()
        provider.Folders("child").Parent = CenterDevice.Rest.RestApiConstants.NONE
        provider.Folders("child").Collection = Nothing
        Dim item = FolderItem()
        item.ExtendedInfosAssignedCollectionID = Nothing
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.DirectoryAsync(item, CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"folder:child"}))
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub MismatchedSuccessfulMetadataDoesNotRetargetTheSelectedIdentity(collection As Boolean)
        Dim provider As New ActionProvider With {.WrongId = True}
        Dim item = If(collection, New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Collection, .ExtendedInfosCollectionID = "c1"}, FolderItem())
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.DirectoryAsync(item, CancellationToken.None)
                                                               End Function, Func(Of Task)))
    End Sub

    <Test>
    Public Async Function CopyOnlyIdentifierLookupDoesNotInventAMoveParent() As Task
        Dim provider As New ActionProvider()
        Dim file = Await provider.FileAsync(FileItem(), False, CancellationToken.None)
        Assert.That(file.ID, [Is].EqualTo("selected-file"))
        Assert.That(file.ParentDirectory, [Is].Null)
        Assert.That(file.Size, [Is].EqualTo(7L * 1073741824L))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"file:selected-file"}))
        Assert.That(provider.Io.DocumentCalls, [Is].Zero)
    End Function

    <TestCase(False), TestCase(True)>
    Public Sub ParentlessMoveRejectsMultipleOrHiddenReferences(hidden As Boolean)
        Dim provider As New ActionProvider()
        If hidden Then provider.Metadata.Collections = New SharingInfo With {.Visible = New List(Of String) From {"c1"}, .NotVisibleCount = 1}
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.FileAsync(FileItem(), True, CancellationToken.None)
                                                               End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"file:selected-file"}))
    End Sub

    <TestCase(False), TestCase(True)>
    Public Async Function AssignedParentResolvesTheSelectedFileAmongDuplicateNames(folder As Boolean) As Task
        Dim provider As New ActionProvider()
        Dim item = FileItem()
        item.ExtendedInfosAssignedCollectionID = "c1"
        If folder Then item.ExtendedInfosAssignedFolderID = "child"
        Dim file = Await provider.FileAsync(item, True, CancellationToken.None)
        Assert.That(file.ID, [Is].EqualTo("selected-file"))
        Assert.That(file.ParentDirectory.AssociatedCollection.CollectionID, [Is].EqualTo("c1"))
        Assert.That(file.ParentDirectory.FolderID, [Is].EqualTo(If(folder, "child", Nothing)))
        Assert.That(provider.Events.Any(Function(value) value.StartsWith("file:")), [Is].False)
        Assert.That(provider.Io.DocumentCalls, [Is].EqualTo(1))
    End Function

    <Test>
    Public Sub StaleAssignedParentCannotMoveAnUnrelatedSameNameFile()
        Dim provider As New ActionProvider()
        provider.Io.HasSelectedFile = False
        Dim item = FileItem()
        item.ExtendedInfosAssignedCollectionID = "c1"
        Assert.ThrowsAsync(Of CompuMaster.Dms.Data.FileNotFoundException)(CType(Async Function()
                                                                                  Await provider.FileAsync(item, True, CancellationToken.None)
                                                                              End Function, Func(Of Task)))
    End Sub

    <Test>
    Public Async Function ATrulyUniqueParentReferenceCanBeReconstructed() As Task
        Dim provider As New ActionProvider()
        provider.Metadata.Collections = New SharingInfo With {.Visible = New List(Of String) From {"c1"}}
        provider.Metadata.Folders = New List(Of String)()
        Dim file = Await provider.FileAsync(FileItem(), True, CancellationToken.None)
        Assert.That(file.ParentDirectory.CollectionID, [Is].EqualTo("c1"))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"file:selected-file", "collection:c1"}))
    End Function

    <Test>
    Public Sub MismatchedFileMetadataCannotChooseTheMoveParent()
        Dim provider As New ActionProvider()
        provider.Metadata.Id = "different-file"
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.FileAsync(FileItem(), True, CancellationToken.None)
                                                               End Function, Func(Of Task)))
    End Sub

    <Test>
    Public Sub MismatchedCopyMetadataCannotDownloadADifferentIdentifier()
        Dim provider As New ActionProvider()
        provider.Metadata.Id = "different-file"
        Assert.ThrowsAsync(Of InvalidOperationException)(CType(Async Function()
                                                                   Await provider.FileAsync(FileItem(), False, CancellationToken.None)
                                                               End Function, Func(Of Task)))
    End Sub

    <Test>
    Public Async Function ActiveHierarchyLookupCancellationStopsBeforeTheNextParentRequest() As Task
        Dim provider As New ActionProvider With {.Block = True}
        Using cancellation As New CancellationTokenSource()
            Dim operation = provider.DirectoryAsync(FolderItem(), cancellation.Token)
            Await provider.Entered.Task
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await operation
                                                                   End Function, Func(Of Task)))
        End Using
        Assert.That(provider.Events, [Is].EqualTo(New String() {"folder:child"}))
    End Function

    <Test>
    Public Sub CanceledHierarchyLookupDoesNotDispatch()
        Dim provider As New ActionProvider()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await provider.DirectoryAsync(FolderItem(), cancellation.Token)
                                                                   End Function, Func(Of Task)))
        End Using
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    Private Shared Function FolderItem() As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Folder, .ExtendedInfosFolderID = "child", .ExtendedInfosAssignedCollectionID = "c1", .FullName = "duplicate", .ExtendedInfosCollisionDetected = True}
    End Function
    Private Shared Function FileItem() As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .ExtendedInfosFileID = "selected-file", .FullName = "duplicate", .ExtendedInfosCollisionDetected = True}
    End Function

    Private Class ActionProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Io As New FixtureIo()
        Public ReadOnly Events As New List(Of String)()
        Public ReadOnly Folders As New Dictionary(Of String, Folder) From {
            {"child", New Folder With {.Id = "child", .Name = "duplicate", .Collection = "c1", .Parent = "parent"}},
            {"parent", New Folder With {.Id = "parent", .Name = "duplicate", .Collection = "c1", .Parent = CenterDevice.Rest.RestApiConstants.NONE}}
        }
        Public Metadata As New DocumentFullMetadata With {.Id = "selected-file", .Filename = "duplicate", .Size = 7L * 1073741824L,
            .Collections = New SharingInfo With {.Visible = New List(Of String) From {"c1", "c2"}}, .Folders = New List(Of String) From {"child", "other-folder"}}
        Public Block As Boolean
        Public WrongId As Boolean
        Public ReadOnly Entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Public Sub New()
            Me.IOClient = Io
        End Sub
        Public Function DirectoryAsync(item As DmsResourceItem, token As CancellationToken) As Task(Of CenterDevice.IO.DirectoryInfo)
            Return Me.GetNativeDirectoryForActionAsync(item, token)
        End Function
        Public Function FileAsync(item As DmsResourceItem, requireParent As Boolean, token As CancellationToken) As Task(Of CenterDevice.IO.FileInfo)
            Return Me.GetNativeFileForActionAsync(item, requireParent, token)
        End Function
        Protected Overrides Async Function LoadNativeFolderByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of Folder)
            Events.Add("folder:" & id)
            Entered.TrySetResult(True)
            If Block Then Await Task.Delay(Timeout.Infinite, cancellationToken)
            If WrongId Then Return New Folder With {.Id = "wrong-id"}
            Return Folders(id)
        End Function
        Protected Overrides Function LoadNativeCollectionByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of Collection)
            cancellationToken.ThrowIfCancellationRequested()
            Events.Add("collection:" & id)
            Return Task.FromResult(New Collection With {.Id = If(WrongId, "wrong-id", id), .Name = "duplicate"})
        End Function
        Protected Overrides Function LoadNativeFileByIdAsync(id As String, cancellationToken As CancellationToken) As Task(Of DocumentFullMetadata)
            cancellationToken.ThrowIfCancellationRequested()
            Events.Add("file:" & id)
            Return Task.FromResult(Metadata)
        End Function
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public CollectionCalls As Integer
        Public DocumentCalls As Integer
        Public HasSelectedFile As Boolean = True
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
        Protected Overrides Function LookupCollectionsAsync(cancellationToken As CancellationToken) As Task(Of List(Of Collection))
            CollectionCalls += 1
            Throw New InvalidOperationException("Selected IDs must not require a root-name lookup.")
        End Function
        Protected Overrides Function LookupChildDocumentsAsync(collectionId As String, parentId As String, cancellationToken As CancellationToken) As Task(Of List(Of DocumentFullMetadata))
            cancellationToken.ThrowIfCancellationRequested()
            DocumentCalls += 1
            Dim result As New List(Of DocumentFullMetadata) From {New DocumentFullMetadata With {.Id = "other-file", .Filename = "duplicate"}}
            If HasSelectedFile Then result.Add(New DocumentFullMetadata With {.Id = "selected-file", .Filename = "duplicate"})
            Return Task.FromResult(result)
        End Function
    End Class
End Class
