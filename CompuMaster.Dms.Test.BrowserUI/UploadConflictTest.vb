Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class UploadConflictTest
    <TestCase(DownloadConflictAction.Replace, False, 2, 4)>
    <TestCase(DownloadConflictAction.Replace, True, 1, 4)>
    <TestCase(DownloadConflictAction.Skip, False, 2, 2)>
    <TestCase(DownloadConflictAction.Skip, True, 1, 2)>
    <TestCase(DownloadConflictAction.Cancel, False, 1, 1)>
    Public Sub DecisionsAffectConflictsOnlyAndCancellationPreservesCompletedFiles(choiceValue As Integer, all As Boolean, prompts As Integer, writes As Integer)
        Dim choice = CType(choiceValue, DownloadConflictAction)
        Using dispatcher As New UiTestDispatcher()
            Dim provider As New ConflictProvider()
            Dim questions As Integer
            Dim policy As New UploadConflictPolicy(Function(path)
                                                       questions += 1
                                                       Return New DownloadConflictDecision With {.Choice = choice, .ApplyToAll = all}
                                                   End Function)
            Dim observer As New Collector()
            Dim operation = DmsBrowser.UploadBatchWithProgressAsync(provider, "folder", {"new-first", "existing-a", "existing-b", "new-last"}, observer, CancellationToken.None, conflicts:=policy)
            If choice = DownloadConflictAction.Cancel Then
                dispatcher.PumpUntil(Function() operation.IsCompleted)
                Assert.Throws(Of OperationCanceledException)(Sub() operation.GetAwaiter().GetResult())
                Assert.That(observer.Latest.States, [Is].EqualTo({UploadFileState.Completed, UploadFileState.Cancelled, UploadFileState.Waiting, UploadFileState.Waiting}))
            Else
                dispatcher.Finish(operation)
                Assert.That(observer.Latest.States(0), [Is].EqualTo(UploadFileState.Completed))
                Assert.That(observer.Latest.States(3), [Is].EqualTo(UploadFileState.Completed))
                If choice = DownloadConflictAction.Skip Then
                    Assert.That(observer.Latest.States(1), [Is].EqualTo(UploadFileState.Skipped))
                    Assert.That(observer.Latest.Counters(1).BytesTransferred, [Is].Zero)
                    Assert.That(provider.Written, Does.Not.Contain("folder/existing-a"))
                End If
            End If
            Assert.That(questions, [Is].EqualTo(prompts))
            Assert.That(provider.Written.Count, [Is].EqualTo(writes))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub RetryKeepsDecisionsAndNeverUploadsCompletedOrSkippedFilesAgain(skip As Boolean)
        Using dispatcher As New UiTestDispatcher()
            Dim provider As New ConflictProvider With {.FailLast = True}
            Dim questions As Integer
            Dim policy As New UploadConflictPolicy(Function(path)
                                                       questions += 1
                                                       Return New DownloadConflictDecision With {.Choice = If(skip, DownloadConflictAction.Skip, DownloadConflictAction.Replace), .ApplyToAll = True}
                                                   End Function)
            Dim observer As New Collector()
            Dim operation = DmsBrowser.UploadBatchWithProgressAsync(provider, "folder", {"existing-a", "new-last", "new-after"}, observer, CancellationToken.None, conflicts:=policy)
            dispatcher.PumpUntil(Function() operation.IsCompleted)
            Assert.Throws(Of InvalidOperationException)(Sub() operation.GetAwaiter().GetResult())
            Dim previous = observer.Latest
            provider.FailLast = False
            dispatcher.Finish(DmsBrowser.UploadBatchWithProgressAsync(provider, "folder", previous.Files, observer, CancellationToken.None, previous, conflicts:=policy))
            Assert.That(questions, [Is].EqualTo(1))
            Assert.That(provider.Written.Where(Function(path) path = "folder/existing-a").Count(), [Is].EqualTo(If(skip, 0, 1)))
            Assert.That(provider.Written.Where(Function(path) path = "folder/new-last").Count(), [Is].EqualTo(1))
            Assert.That(provider.Written.Last(), [Is].EqualTo("folder/new-after"))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub AmbiguousPathsAndChangedIdentityFailBeforePayload(ambiguous As Boolean)
        Using dispatcher As New UiTestDispatcher()
            Dim provider As New ConflictProvider With {.Ambiguous = ambiguous}
            Dim policy As New UploadConflictPolicy(Function(path)
                                                       provider.Identity = "different-file"
                                                       Return New DownloadConflictDecision With {.Choice = DownloadConflictAction.Replace}
                                                   End Function)
            Dim operation = DmsBrowser.UploadBatchWithProgressAsync(provider, "folder", {"existing-a"}, New Collector(), CancellationToken.None, conflicts:=policy)
            dispatcher.PumpUntil(Function() operation.IsCompleted)
            If ambiguous Then
                Assert.Throws(Of RemotePathNotUniqueException)(Sub() operation.GetAwaiter().GetResult())
            Else
                Assert.Throws(Of InvalidOperationException)(Sub() operation.GetAwaiter().GetResult())
            End If
            Assert.That(provider.Written, [Is].Empty)
        End Using
    End Sub

    Private Class Collector
        Implements IProgress(Of UploadBatchSnapshot)
        Friend Latest As UploadBatchSnapshot
        Public Sub Report(value As UploadBatchSnapshot) Implements IProgress(Of UploadBatchSnapshot).Report
            Latest = value
        End Sub
    End Class
    Private Class ConflictProvider
        Inherits NoDmsProvider
        Friend ReadOnly Written As New List(Of String)
        Friend ReadOnly ReplacedIds As New List(Of String)
        Friend Identity As String = "exact-file"
        Friend Ambiguous As Boolean
        Friend FailLast As Boolean
        Public Overrides ReadOnly Property SupportsNonUniqueRemoteItems As Boolean = True
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            Return Task.FromResult(If(path.Contains("existing"), New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .FullName = path, .ExtendedInfosFileID = Identity, .ExtendedInfosCollisionDetected = Ambiguous}, Nothing))
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
        Public Overrides Function UploadFileWithProgressAsync(path As String, file As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            If FailLast AndAlso path.EndsWith("new-last") Then Throw New InvalidOperationException("Fixture payload failure")
            Written.Add(path)
            progress.Report(New DmsTransferProgress(1, 1, DmsTransferPhase.Completed))
            Return Task.CompletedTask
        End Function
        Public Overrides Function ReplaceFileWithProgressAsync(remoteFile As DmsResourceItem, localFilePath As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            ReplacedIds.Add(remoteFile.ExtendedInfosFileID)
            Return UploadFileWithProgressAsync(remoteFile.FullName, localFilePath, progress, cancellationToken)
        End Function
    End Class
End Class
