Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest.Clients.Collections
Imports CenterDevice.Rest.Clients.Folders
Imports CenterDevice.Rest.Clients.Documents.Metadata
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, SetUICulture("en")>
Public Class CenterDeviceNativeCopyMoveTest
    <TestCase(False), TestCase(True)>
    Public Async Function PublicOperationPreservesSelectedFileId(moving As Boolean) As Task
        Dim provider As New ActionProvider()
        Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", moving)
        Assert.That(provider.Events(0), [Is].EqualTo(If(moving, "rename:selected", "copy:selected")))
        Assert.That(provider.Io.Files.Any(Function(file) file.Filename = "new.txt" AndAlso file.Parent = "destination"), [Is].True)
        Assert.That(provider.SupportsAsynchronousIo, [Is].False, "Copy/move opt-in must not claim every provider operation is native.")
        Assert.That(provider.ResetCalls, [Is].GreaterThan(0))
    End Function

    <TestCase(False), TestCase(True)>
    Public Async Function OverwriteStagesAndDeletesOnlyTheReplacedId(moving As Boolean) As Task
        Dim provider As New ActionProvider()
        provider.AddTarget()
        Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", moving)
        Assert.That(provider.Events, Does.Contain("delete:replaced"))
        Assert.That(provider.Events, Does.Not.Contain("delete:selected"))
        Assert.That(provider.Io.Files.Where(Function(file) file.Parent = "destination" AndAlso file.Filename = "new.txt").Count(), [Is].EqualTo(1))
        Assert.That(provider.Io.Files.Any(Function(file) file.Filename.StartsWith(".compumaster-dms-", StringComparison.Ordinal)), [Is].False)
    End Function

    <TestCase(False), TestCase(True)>
    Public Sub DisallowedOverwriteDoesNotWrite(moving As Boolean)
        Dim provider As New ActionProvider()
        provider.AddTarget()
        Assert.ThrowsAsync(Of FileAlreadyExistsException)(CType(Async Function()
                                                                  Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", moving, False)
                                                              End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub SharedDocumentIdentityIsRejectedBeforeMutation(moving As Boolean)
        Dim provider As New ActionProvider()
        provider.AddTarget("selected")
        Assert.ThrowsAsync(Of ArgumentException)(CType(Async Function()
                                                         Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", moving)
                                                     End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub FailedPromotionRestoresBackupAndReportsOriginalFailure(moving As Boolean)
        Dim provider As New ActionProvider With {.FailPromotion = True}
        provider.AddTarget()
        Dim errorResult = Assert.ThrowsAsync(Of FileActionFailedException)(CType(Async Function()
                                                                                     Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", moving)
                                                                                 End Function, Func(Of Task)))
        Assert.That(errorResult.ToString(), Does.Contain("promotion failed"))
        Assert.That(provider.Io.Files.Single(Function(file) file.Id = "replaced").Filename, [Is].EqualTo("new.txt"))
        Assert.That(provider.Events, Does.Not.Contain("delete:replaced"))
        If moving Then
            Assert.That(provider.Io.Files.Single(Function(file) file.Id = "selected").Parent, [Is].EqualTo("source"))
            Assert.That(provider.Io.Files.Single(Function(file) file.Id = "selected").Filename, [Is].EqualTo("old.txt"))
        Else
            Assert.That(provider.Io.Files.Any(Function(file) file.Id.StartsWith("copied", StringComparison.Ordinal)), [Is].False)
        End If
    End Sub

    <Test>
    Public Sub FailedCompensationRetainsBothErrors()
        Dim provider As New ActionProvider With {.FailPromotion = True, .FailRestore = True}
        provider.AddTarget()
        Dim errorResult = Assert.ThrowsAsync(Of FileActionFailedException)(CType(Async Function()
                                                                                     Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", False)
                                                                                 End Function, Func(Of Task)))
        Assert.That(errorResult.ToString(), Does.Contain("promotion failed"))
        Assert.That(DirectCast(errorResult.InnerException, AggregateException).Flatten().InnerExceptions.Select(Function(errorItem) errorItem.Message), Does.Contain("restore failed"))
    End Sub

    <Test>
    Public Sub CleanupFailureReportsSuccessfulCopyAndRemainingBackupId()
        Dim provider As New ActionProvider With {.FailDeleteBackup = True}
        provider.AddTarget()
        Dim errorResult = Assert.ThrowsAsync(Of FileActionFailedException)(CType(Async Function()
                                                                                     Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", False)
                                                                                 End Function, Func(Of Task)))
        Assert.That(errorResult.ToString(), Does.Contain("operation succeeded"))
        Assert.That(errorResult.ToString(), Does.Contain("replaced"))
        Assert.That(provider.Io.Files.Where(Function(file) file.Parent = "destination" AndAlso file.Filename = "new.txt").Count(), [Is].EqualTo(1))
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub UncertainStagingReportsTheCandidateWithoutReplayingOrDeleting(canceled As Boolean)
        Dim original As Exception = If(canceled, CType(New OperationCanceledException("staging response lost"), Exception), New InvalidOperationException("staging response lost"))
        Dim provider As New ActionProvider With {.StagingFailure = original}
        provider.AddTarget()
        Dim errorResult = Assert.CatchAsync(Of Exception)(CType(Async Function()
                                                                   Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", False)
                                                               End Function, Func(Of Task)))
        Assert.That(errorResult, [Is].SameAs(original), "Diagnostics must preserve the original exception and cancellation type.")
        Assert.That(CStr(errorResult.Data("CompuMaster.Dms.Reconciliation")), Does.Contain(provider.TemporaryName))
        Assert.That(CStr(errorResult.Data("CompuMaster.Dms.Reconciliation")), Does.Contain("selected"))
        Assert.That(CStr(errorResult.Data("CompuMaster.Dms.Reconciliation")), Does.Contain("destination"))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"copy:selected"}))
        Assert.That(provider.Io.Files.Single(Function(file) file.Id = "replaced").Filename, [Is].EqualTo("new.txt"))
        Assert.That(provider.Io.Files.Any(Function(file) file.Filename = provider.TemporaryName), [Is].True)
    End Sub

    <Test>
    Public Sub CanceledStagedIdentityLookupRetainsTheCandidateAndCancellation()
        Dim original As New OperationCanceledException("staged identity lookup canceled")
        Dim provider As New ActionProvider()
        provider.Io.StagedLookupFailure = original
        provider.AddTarget()
        Dim errorResult = Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                                    Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", False)
                                                                                End Function, Func(Of Task)))
        Assert.That(errorResult, [Is].SameAs(original))
        Assert.That(CStr(errorResult.Data("CompuMaster.Dms.Reconciliation")), Does.Contain(provider.TemporaryName))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"copy:selected"}))
        Assert.That(provider.Io.Files.Single(Function(file) file.Id = "replaced").Filename, [Is].EqualTo("new.txt"))
    End Sub

    <TestCase(False, False), TestCase(False, True), TestCase(True, False), TestCase(True, True)>
    Public Sub UncertainFirstTemporaryRenameReportsIdentityWithoutFurtherWrites(folder As Boolean, applied As Boolean)
        Dim original As New InvalidOperationException("temporary rename response lost")
        Dim provider As New ActionProvider With {.TemporaryRenameFailure = original, .ApplyTemporaryRename = applied}
        Dim item = If(folder, provider.DirectoryItem("source"), provider.FileItem())
        Dim errorResult = Assert.CatchAsync(Of Exception)(CType(Async Function()
                                                                   Await provider.ActAsync(item, If(folder, "collection/destination/new-folder", "collection/destination/new.txt"), True)
                                                               End Function, Func(Of Task)))
        Assert.That(errorResult, [Is].SameAs(original))
        Dim details = CStr(errorResult.Data("CompuMaster.Dms.Reconciliation"))
        Assert.That(details, Does.Contain(provider.TemporaryName))
        Assert.That(details, Does.Contain(If(folder, "source", "selected")))
        Assert.That(details, Does.Contain("collection"))
        Assert.That(provider.Events, [Is].EqualTo(New String() {If(folder, "rename-directory:source", "rename:selected")}))
        If folder Then
            Assert.That(provider.Io.Folders.Single(Function(value) value.Id = "source").Name, [Is].EqualTo(If(applied, provider.TemporaryName, "source")))
        Else
            Assert.That(provider.Io.Files.Single(Function(value) value.Id = "selected").Filename, [Is].EqualTo(If(applied, provider.TemporaryName, "old.txt")))
        End If
    End Sub

    <Test>
    Public Sub CanceledTemporaryRenameKeepsItsCancellationIdentityAndCandidate()
        Dim original As New OperationCanceledException("temporary rename canceled")
        Dim provider As New ActionProvider With {.TemporaryRenameFailure = original, .ApplyTemporaryRename = True}
        Dim errorResult = Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                                    Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", True)
                                                                                End Function, Func(Of Task)))
        Assert.That(errorResult, [Is].SameAs(original))
        Assert.That(CStr(errorResult.Data("CompuMaster.Dms.Reconciliation")), Does.Contain(provider.TemporaryName))
        Assert.That(provider.Events, [Is].EqualTo(New String() {"rename:selected"}))
    End Sub

    <Test>
    Public Sub CancellationAfterMoveUsesIndependentCompensationToken()
        Using cancellation As New CancellationTokenSource()
            Dim provider As New ActionProvider With {.CancelAfterMove = cancellation}
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", True, True, cancellation.Token)
                                                                     End Function, Func(Of Task)))
            Assert.That(provider.Io.Files.Single().Parent, [Is].EqualTo("source"))
            Assert.That(provider.Io.Files.Single().Filename, [Is].EqualTo("old.txt"))
            Assert.That(provider.CompensationWasCancelable, [Is].True)
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub PreCanceledOperationHasNoWrites(moving As Boolean)
        Dim provider As New ActionProvider()
        Using cancellation As New CancellationTokenSource()
            cancellation.Cancel()
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                         Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", moving, True, cancellation.Token)
                                                                     End Function, Func(Of Task)))
            Assert.That(provider.Events, [Is].Empty)
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub DuplicateDestinationNamesFailBeforeWrites(moving As Boolean)
        Dim provider As New ActionProvider()
        provider.AddTarget()
        provider.AddTarget("second")
        Assert.ThrowsAsync(Of RemotePathNotUniqueException)(CType(Async Function()
                                                                     Await provider.ActAsync(provider.FileItem(), "collection/destination/new.txt", moving)
                                                                 End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub DirectoryIdentityPreventsSelfMergeAndDescendantMove(moving As Boolean)
        Dim provider As New ActionProvider()
        Dim source = provider.DirectoryItem("source")
        source.FullName = "outdated-selection"
        Assert.ThrowsAsync(Of ArgumentException)(CType(Async Function()
                                                         Await provider.ActAsync(source, "collection/source/child", moving)
                                                     End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <Test>
    Public Sub StaleSourcePathCannotCreateParentsInsideItsOwnIdentity()
        Dim provider As New ActionProvider()
        Dim source = provider.DirectoryItem("source")
        source.FullName = "stale-source-path"
        Assert.ThrowsAsync(Of ArgumentException)(CType(Async Function()
                                                         Await provider.CopyAsync(source, "collection/source/missing/child", True, True, CancellationToken.None)
                                                     End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Async Function FolderMergeCopiesOrMovesContentsWithTheirIds(moving As Boolean) As Task
        Dim provider As New ActionProvider()
        Await provider.ActAsync(provider.DirectoryItem("source"), "collection/destination", moving)
        Assert.That(provider.Io.Files.Any(Function(file) file.Filename = "old.txt" AndAlso file.Parent = "destination"), [Is].True)
        Assert.That(provider.Events, Does.Contain(If(moving, "move:selected", "copy:selected")))
        Assert.That(provider.Events.Contains("delete-directory:source"), [Is].EqualTo(moving))
    End Function

    <Test>
    Public Async Function NewFolderCopyCreatesBeforeCopyingChildren() As Task
        Dim provider As New ActionProvider()
        Await provider.ActAsync(provider.DirectoryItem("source"), "collection/destination/new-folder", False)
        Assert.That(provider.Events.Take(2), [Is].EqualTo(New String() {"create:new-folder", "copy:selected"}))
    End Function

    <Test>
    Public Async Function FolderMovePreservesFolderIdAndChangesName() As Task
        Dim provider As New ActionProvider()
        Await provider.ActAsync(provider.DirectoryItem("source"), "collection/destination/new-folder", True)
        Assert.That(provider.Io.Folders.Single(Function(folder) folder.Id = "source").Parent, [Is].EqualTo("destination"))
        Assert.That(provider.Io.Folders.Single(Function(folder) folder.Id = "source").Name, [Is].EqualTo("new-folder"))
    End Function

    <Test>
    Public Sub FolderCopyWithAmbiguousChildNamespaceDoesNotCreateTarget()
        Dim provider As New ActionProvider()
        provider.Io.Files.Add(New FixtureDocument With {.Id = "duplicate", .Filename = "OLD.TXT", .Parent = "source"})
        Assert.ThrowsAsync(Of RemotePathNotUniqueException)(CType(Async Function()
                                                                     Await provider.ActAsync(provider.DirectoryItem("source"), "collection/destination/new-folder", False)
                                                                 End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Async Function PublicPathOverloadResolvesThenUsesNativeComposition(moving As Boolean) As Task
        Dim provider As New ActionProvider()
        If moving Then
            Await provider.MoveAsync("collection/source/old.txt", "collection/destination/new.txt", True, False)
        Else
            Await provider.CopyAsync("collection/source/old.txt", "collection/destination/new.txt", True, False, CancellationToken.None)
        End If
        Assert.That(provider.Events, Does.Contain(If(moving, "move:selected", "copy:selected")))
    End Function

    <Test>
    Public Async Function SameParentMoveOnlyRenamesSelectedFile() As Task
        Dim provider As New ActionProvider()
        Await provider.ActAsync(provider.FileItem(), "collection/source/new.txt", True)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"rename:selected"}))
    End Function

    <Test>
    Public Sub FolderPromotionFailureRestoresConfirmedMove()
        Dim provider As New ActionProvider With {.FailDirectoryPromotion = True}
        Assert.ThrowsAsync(Of FileActionFailedException)(CType(Async Function()
                                                                  Await provider.ActAsync(provider.DirectoryItem("source"), "collection/destination/new-folder", True)
                                                              End Function, Func(Of Task)))
        Assert.That(provider.Io.Folders.Single(Function(folder) folder.Id = "source").Name, [Is].EqualTo("source"))
        Assert.That(provider.Io.Folders.Single(Function(folder) folder.Id = "source").Parent, [Is].Null)
    End Sub

    <Test>
    Public Async Function CollectionMoveOnlyRenamesInRoot() As Task
        Dim provider As New ActionProvider()
        Dim item As New DmsResourceItem With {.FullName = "collection", .Name = "collection", .ItemType = DmsResourceItem.ItemTypes.Collection, .ExtendedInfosCollectionID = "collection"}
        Await provider.ActAsync(item, "renamed", True)
        Assert.That(provider.Events, [Is].EqualTo(New String() {"rename-collection:collection"}))
    End Function

    <Test>
    Public Sub CollectionCopyIsExplicitlyUnsupported()
        Dim provider As New ActionProvider()
        Dim item As New DmsResourceItem With {.FullName = "collection", .Name = "collection", .ItemType = DmsResourceItem.ItemTypes.Collection, .ExtendedInfosCollectionID = "collection"}
        Assert.ThrowsAsync(Of NotSupportedException)(CType(Async Function()
                                                              Await provider.ActAsync(item, "renamed", False)
                                                          End Function, Func(Of Task)))
        Assert.That(provider.Events, [Is].Empty)
    End Sub

    Private Class ActionProvider
        Inherits ScopevisioTeamworkDmsProvider
        Public ReadOnly Io As New FixtureIo()
        Public ReadOnly Events As New List(Of String)()
        Public FailPromotion, FailRestore, FailDeleteBackup, FailDirectoryPromotion As Boolean
        Public StagingFailure, TemporaryRenameFailure As Exception
        Public ApplyTemporaryRename As Boolean
        Public TemporaryName As String
        Public CancelAfterMove As CancellationTokenSource
        Public CompensationWasCancelable As Boolean
        Public ResetCalls As Integer
        Public Sub New()
            Me.IOClient = Io
        End Sub
        Public Function FileItem() As DmsResourceItem
            Return New DmsResourceItem With {.FullName = "collection/source/old.txt", .Name = "old.txt", .ItemType = DmsResourceItem.ItemTypes.File, .ExtendedInfosFileID = "selected", .ExtendedInfosCollisionDetected = True}
        End Function
        Public Function DirectoryItem(id As String) As DmsResourceItem
            Return New DmsResourceItem With {.FullName = "collection/" & id, .Name = id, .ItemType = DmsResourceItem.ItemTypes.Folder, .ExtendedInfosFolderID = id}
        End Function
        Public Function ActAsync(item As DmsResourceItem, path As String, moving As Boolean, Optional overwrite As Boolean = True, Optional ct As CancellationToken = Nothing) As Task
            If moving Then Return Me.MoveAsync(item, path, overwrite, False, ct)
            Return Me.CopyAsync(item, path, overwrite, False, ct)
        End Function
        Public Sub AddTarget(Optional id As String = "replaced")
            Io.Files.Add(New FixtureDocument With {.Id = id, .Filename = "new.txt", .Parent = "destination"})
        End Sub
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            cancellationToken.ThrowIfCancellationRequested()
            If path = "collection" Then Return Task.FromResult(New DmsResourceItem With {.FullName = path, .Name = path, .ItemType = DmsResourceItem.ItemTypes.Collection, .ExtendedInfosCollectionID = path})
            If path = "collection/source/old.txt" Then
                Dim item = FileItem()
                item.ExtendedInfosCollisionDetected = False
                Return Task.FromResult(item)
            End If
            Dim directory = Io.Folders.FirstOrDefault(Function(folder) "collection/" & folder.Name = path)
            Return Task.FromResult(If(directory Is Nothing, Nothing, DirectoryItem(directory.Id)))
        End Function
        Public Overrides Function ListAllRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            cancellationToken.ThrowIfCancellationRequested()
            If path = "collection/source" Then Return Task.FromResult(New List(Of DmsResourceItem) From {FileItem()})
            If path <> "collection" Then Throw New InvalidOperationException("The selected source must not be replaced with a path lookup.")
            Return Task.FromResult(Io.Folders.Where(Function(folder) folder.Parent Is Nothing).Select(Function(folder) DirectoryItem(folder.Id)).ToList())
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            ResetCalls += 1
            Return Task.CompletedTask
        End Function
        Protected Overrides Sub CopyItem(source As DmsResourceItem, destination As String, overwrite As Boolean?)
            Throw New InvalidOperationException("Synchronous copy must not be used.")
        End Sub
        Protected Overrides Sub MoveItem(source As DmsResourceItem, destination As String, overwrite As Boolean?)
            Throw New InvalidOperationException("Synchronous move must not be used.")
        End Sub
        Protected Overrides Function OpenNativeTransferDirectoryAsync(path As String, ct As CancellationToken) As Task(Of Global.CenterDevice.IO.DirectoryInfo)
            ct.ThrowIfCancellationRequested()
            Dim collection As New Global.CenterDevice.IO.DirectoryInfo(Io, Io.RootDirectory, New Collection With {.Id = "collection", .Name = "collection"})
            If path = Nothing Then Return Task.FromResult(Io.RootDirectory)
            If path = "collection" Then Return Task.FromResult(collection)
            path = path.Split("/"c).Last()
            Dim folderMetadata = Io.Folders.SingleOrDefault(Function(folder) folder.Id = path)
            If folderMetadata Is Nothing Then Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(path)
            Return Task.FromResult(New Global.CenterDevice.IO.DirectoryInfo(Io, collection, folderMetadata))
        End Function
        Protected Overrides Function GetNativeDirectoryForActionAsync(item As DmsResourceItem, ct As CancellationToken) As Task(Of Global.CenterDevice.IO.DirectoryInfo)
            If item.ItemType = DmsResourceItem.ItemTypes.Collection Then Return Me.OpenNativeTransferDirectoryAsync("collection", ct)
            Return Me.OpenNativeTransferDirectoryAsync(item.ExtendedInfosFolderID, ct)
        End Function
        Protected Overrides Async Function GetNativeFileForActionAsync(item As DmsResourceItem, requireParent As Boolean, ct As CancellationToken) As Task(Of Global.CenterDevice.IO.FileInfo)
            Dim parent = Await Me.OpenNativeTransferDirectoryAsync("source", ct)
            Return New Global.CenterDevice.IO.FileInfo(Io, parent, Io.Files.First(Function(file) file.Id = item.ExtendedInfosFileID))
        End Function
        Protected Overrides Function CopyNativeFileAsync(source As Global.CenterDevice.IO.FileInfo, parent As Global.CenterDevice.IO.DirectoryInfo, name As String, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            Events.Add("copy:" & source.ID)
            Io.Files.Add(New FixtureDocument With {.Id = "copied-" & Io.Files.Count.ToString(), .Filename = name, .Parent = parent.FolderID})
            parent.ResetFilesCache()
            If name.StartsWith(".compumaster-dms-", StringComparison.Ordinal) Then
                TemporaryName = name
                If StagingFailure IsNot Nothing Then Throw StagingFailure
            End If
            Return Task.CompletedTask
        End Function
        Protected Overrides Function RenameNativeFileAsync(file As Global.CenterDevice.IO.FileInfo, name As String, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            Events.Add("rename:" & file.ID)
            If file.ID = "selected" AndAlso name.StartsWith(".compumaster-dms-", StringComparison.Ordinal) AndAlso TemporaryRenameFailure IsNot Nothing Then
                TemporaryName = name
                If ApplyTemporaryRename Then Io.Files.First(Function(item) item.Id = file.ID).Filename = name
                Throw TemporaryRenameFailure
            End If
            If name = "new.txt" AndAlso file.ID <> "replaced" AndAlso FailPromotion Then Throw New InvalidOperationException("promotion failed")
            If name = "new.txt" AndAlso file.ID = "replaced" AndAlso FailRestore Then Throw New InvalidOperationException("restore failed")
            Io.Files.First(Function(item) item.Id = file.ID).Filename = name
            file.ParentDirectory?.ResetFilesCache()
            If CancelAfterMove IsNot Nothing AndAlso CancelAfterMove.IsCancellationRequested Then CompensationWasCancelable = ct.CanBeCanceled AndAlso Not ct.IsCancellationRequested
            Return Task.CompletedTask
        End Function
        Protected Overrides Function MoveNativeFileAsync(file As Global.CenterDevice.IO.FileInfo, parent As Global.CenterDevice.IO.DirectoryInfo, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            Events.Add("move:" & file.ID)
            Io.Files.First(Function(item) item.Id = file.ID).Parent = parent.FolderID
            file.ParentDirectory?.ResetFilesCache()
            parent.ResetFilesCache()
            If CancelAfterMove IsNot Nothing AndAlso parent.FolderID = "destination" Then CancelAfterMove.Cancel()
            Return Task.CompletedTask
        End Function
        Protected Overrides Function DeleteNativeActionFileAsync(file As Global.CenterDevice.IO.FileInfo, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            Events.Add("delete:" & file.ID)
            If file.ID = "replaced" AndAlso FailDeleteBackup Then Throw New InvalidOperationException("delete backup failed")
            Io.Files.RemoveAll(Function(item) item.Id = file.ID)
            file.ParentDirectory?.ResetFilesCache()
            Return Task.CompletedTask
        End Function
        Protected Overrides Function CreateNativeDirectoryAsync(parent As Global.CenterDevice.IO.DirectoryInfo, name As String, style As Global.CenterDevice.IO.DirectoryInfo.DirectoryType?, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            Events.Add("create:" & name)
            Io.Folders.Add(New Folder With {.Id = name, .Name = name, .Parent = parent.FolderID, .Collection = "collection"})
            parent.ResetDirectoriesCache()
            Return Task.CompletedTask
        End Function
        Protected Overrides Function RenameNativeDirectoryAsync(directory As Global.CenterDevice.IO.DirectoryInfo, name As String, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            If directory.Type = Global.CenterDevice.IO.DirectoryInfo.DirectoryType.Collection Then
                Events.Add("rename-collection:" & directory.CollectionID)
                Return Task.CompletedTask
            End If
            Events.Add("rename-directory:" & directory.FolderID)
            If name.StartsWith(".compumaster-dms-", StringComparison.Ordinal) AndAlso TemporaryRenameFailure IsNot Nothing Then
                TemporaryName = name
                If ApplyTemporaryRename Then Io.Folders.Single(Function(folder) folder.Id = directory.FolderID).Name = name
                Throw TemporaryRenameFailure
            End If
            If name = "new-folder" AndAlso FailDirectoryPromotion Then Throw New InvalidOperationException("folder promotion failed")
            Io.Folders.Single(Function(folder) folder.Id = directory.FolderID).Name = name
            directory.ParentDirectory.ResetDirectoriesCache()
            Return Task.CompletedTask
        End Function
        Protected Overrides Function MoveNativeDirectoryAsync(directory As Global.CenterDevice.IO.DirectoryInfo, parent As Global.CenterDevice.IO.DirectoryInfo, ct As CancellationToken) As Task
            ct.ThrowIfCancellationRequested()
            Events.Add("move-directory:" & directory.FolderID)
            Io.Folders.Single(Function(folder) folder.Id = directory.FolderID).Parent = parent.FolderID
            directory.ParentDirectory.ResetDirectoriesCache()
            parent.ResetDirectoriesCache()
            Return Task.CompletedTask
        End Function
        Protected Overrides Function DeleteNativeActionDirectoryAsync(directory As Global.CenterDevice.IO.DirectoryInfo, ct As CancellationToken) As Task
            Events.Add("delete-directory:" & directory.FolderID)
            Io.Folders.RemoveAll(Function(folder) folder.Id = directory.FolderID)
            Return Task.CompletedTask
        End Function
    End Class

    Private Class FixtureDocument
        Inherits DocumentFullMetadata
        Public Property Parent As String
    End Class

    Private Class FixtureIo
        Inherits Global.CenterDevice.IO.IOClientBase
        Public ReadOnly Folders As New List(Of Folder) From {New Folder With {.Id = "source", .Name = "source", .Collection = "collection"}, New Folder With {.Id = "destination", .Name = "destination", .Collection = "collection"}}
        Public ReadOnly Files As New List(Of FixtureDocument) From {New FixtureDocument With {.Id = "selected", .Filename = "old.txt", .Parent = "source"}}
        Public StagedLookupFailure As Exception
        Public Sub New()
            MyBase.New(Nothing, "fixture-user")
        End Sub
        Protected Overrides Function LookupChildDocumentsAsync(collectionId As String, parentId As String, ct As CancellationToken) As Task(Of List(Of DocumentFullMetadata))
            ct.ThrowIfCancellationRequested()
            If StagedLookupFailure IsNot Nothing AndAlso Files.Any(Function(file) file.Parent = parentId AndAlso file.Id.StartsWith("copied-", StringComparison.Ordinal)) Then Throw StagedLookupFailure
            Return Task.FromResult(Files.Where(Function(file) file.Parent = parentId).Select(Function(file) New DocumentFullMetadata With {.Id = file.Id, .Filename = file.Filename}).ToList())
        End Function
        Protected Overrides Function LookupChildFoldersAsync(collectionId As String, parentId As String, ct As CancellationToken) As Task(Of List(Of Folder))
            ct.ThrowIfCancellationRequested()
            Return Task.FromResult(Folders.Where(Function(folder) folder.Parent = If(parentId = CenterDevice.Rest.RestApiConstants.NONE, Nothing, parentId)).Select(Function(folder) New Folder With {.Id = folder.Id, .Name = folder.Name, .Parent = folder.Parent, .Collection = folder.Collection}).ToList())
        End Function
        Protected Overrides Function LookupCollectionsAsync(ct As CancellationToken) As Task(Of List(Of Collection))
            ct.ThrowIfCancellationRequested()
            Return Task.FromResult(New List(Of Collection) From {New Collection With {.Id = "collection", .Name = "collection"}})
        End Function
    End Class
End Class
