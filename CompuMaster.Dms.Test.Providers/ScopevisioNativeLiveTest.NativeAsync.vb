Option Explicit On
Option Strict On

Imports System.IO
Imports System.Linq
Imports System.Runtime.ExceptionServices
Imports System.Security.Cryptography
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Category("RemoteDms"), Category("TestLevel2"), Category("ScopevisioNativeAsync"), NonParallelizable>
Public Class ScopevisioNativeLiveTest
    Friend Const OwnedCollection As String = "ZZZ_UnitTests_CM.Dms_NativeAsync"

    <Test>
    Public Async Function NativeCrudSelectedIdentityAndLinkSnapshots() As Task
        Await WithOwnedCollectionAsync(Async Function(provider, token)
                                          Dim folder = OwnedCollection & "/source"
                                          Dim source = folder & "/payload.bin"
                                          Await provider.CreateFolderAsync(folder, token)
                                          Dim payload = Text.Encoding.UTF8.GetBytes("Native source-mode live fixture.")
                                          Await provider.UploadFileAsync(source, Function() New MemoryStream(payload, False), token)
                                          Dim selected = Await provider.ListRemoteItemAsync(source, token)
                                          Assert.That(selected.ExtendedInfosFileID, [Is].Not.Null.And.Not.Empty)
                                          Await provider.CopyAsync(selected, OwnedCollection & "/copy/payload.bin", False, True, token)
                                          Dim copied = Await provider.ListRemoteItemAsync(OwnedCollection & "/copy/payload.bin", token)
                                          Assert.That(copied.ExtendedInfosFileID, [Is].Not.EqualTo(selected.ExtendedInfosFileID))
                                          Await provider.MoveAsync(copied, OwnedCollection & "/moved.bin", False, False, token)
                                          Dim moved = Await provider.ListRemoteItemAsync(OwnedCollection & "/moved.bin", token)
                                          Assert.That(moved.ExtendedInfosFileID, [Is].EqualTo(copied.ExtendedInfosFileID))
                                          Await provider.UploadFileAsync(moved.FullName, payload, token)
                                          Dim byId = Await provider.FindFileByIdAsync(moved.ExtendedInfosFileID, token)
                                          Assert.That(byId.ExtendedInfosFileID, [Is].EqualTo(moved.ExtendedInfosFileID))
                                          Dim local = Path.GetTempFileName()
                                          Try
                                              Await provider.DownloadFileAsync(byId, local, token)
                                              Assert.That(File.ReadAllBytes(local), [Is].EqualTo(payload))
                                          Finally
                                              File.Delete(local)
                                          End Try

                                          Dim settings As New DmsLink(moved, provider)
                                          settings.Initialize(Nothing, DateTime.Now.AddHours(1), 10, Nothing, Nothing, 0, 0, Nothing, Nothing, Nothing, Nothing, Nothing, True, True, False, False, False, False)
                                          Dim created = Await provider.CreateLinkAsync(moved, settings, token)
                                          Await provider.RefreshLinkAsync(created, token)
                                          Assert.That(created.DetailsInitialized, [Is].True)
                                          Assert.That(created.WebUrl, [Is].Not.Null.And.Not.Empty)
                                          Assert.That(created.MaxDownloads, [Is].EqualTo(10))
                                          Await provider.ResetCachesForRemoteItemsAsync(OwnedCollection, BaseDmsProvider.SearchItemType.AllItems, token)
                                          Dim listed = Await provider.FindFileByIdAsync(moved.ExtendedInfosFileID, token)
                                          Dim publishedLink = listed.ExtendedInfosLinks.Single(Function(link) link.ID = created.ID)
                                          Assert.That(publishedLink.DetailsInitialized, [Is].True)
                                          Assert.That(publishedLink.WebUrl, [Is].EqualTo(created.WebUrl))
                                          Await provider.DeleteLinkAsync(created, token)
                                          Await provider.DeleteRemoteItemAsync(selected, DmsResourceItem.ItemTypes.File, token)
                                          Assert.That(Await provider.RemoteItemExistsAsync(source, token), [Is].False)
                                      End Function)
    End Function

    <Test>
    Public Async Function FileBackedLargeTransferAndActiveDownloadCancellation() As Task
        Await WithOwnedCollectionAsync(Async Function(provider, token)
                                          Dim mib = Integer.Parse(RequiredEnvironment("NATIVE_SCOPEVISIO_TRANSFER_MIB"), Globalization.CultureInfo.InvariantCulture)
                                          If mib < 256 OrElse mib > 4096 Then Throw New InvalidOperationException("The live transfer size must be between 256 and 4096 MiB.")
                                          Dim size = CLng(mib) * 1024L * 1024L
                                          Dim source = Path.GetTempFileName()
                                          Dim downloaded = Path.GetTempFileName()
                                          Dim partialTarget = Path.GetTempFileName()
                                          Try
                                              If New DriveInfo(Path.GetPathRoot(source)).AvailableFreeSpace < size * 3 + 268435456L Then Throw New IOException("Insufficient local CI disk capacity for the requested live transfer fixtures.")
                                              Dim block(65535) As Byte
                                              For index = 0 To block.Length - 1
                                                  block(index) = CByte(index Mod 251)
                                              Next
                                              Using output As New FileStream(source, FileMode.Create, FileAccess.Write, FileShare.None, block.Length, FileOptions.Asynchronous)
                                                  For offset As Long = 0 To size - 1 Step block.Length
                                                      Await output.WriteAsync(block, 0, block.Length, token)
                                                  Next
                                              End Using
                                              Dim remote = OwnedCollection & "/large.bin"
                                              Dim uploadProgress As New LiveUploadProgressCollector()
                                              Await provider.UploadFileWithProgressAsync(remote, source, uploadProgress, token)
                                              Assert.That(uploadProgress.Latest.Phase, [Is].EqualTo(DmsTransferPhase.Completed))
                                              Assert.That(uploadProgress.Latest.BytesTransferred, [Is].EqualTo(size))
                                              Assert.That(uploadProgress.Latest.TotalBytes, [Is].EqualTo(size))
                                              Dim selected = Await provider.ListRemoteItemAsync(remote, token)
                                              Assert.That(selected.ContentLength, [Is].EqualTo(size))
                                              Await provider.DownloadFileAsync(selected, downloaded, token)
                                              Assert.That(New FileInfo(downloaded).Length, [Is].EqualTo(size))
                                              Assert.That(HashFile(downloaded), [Is].EqualTo(HashFile(source)))
                                              RecordLargeObservation("Native live file-backed upload and ID-based download verified: " & size.ToString(Globalization.CultureInfo.InvariantCulture) & " bytes with matching SHA-256.")

                                              Using cancellation = CancellationTokenSource.CreateLinkedTokenSource(token)
                                                  Dim operation = provider.DownloadFileAsync(selected, partialTarget, cancellation.Token)
                                                  Dim monitorFailure As Exception = Nothing
                                                  Try
                                                      While Not operation.IsCompleted AndAlso New FileInfo(partialTarget).Length < 1048576L
                                                          Await Task.WhenAny(operation, Task.Delay(25, token))
                                                          token.ThrowIfCancellationRequested()
                                                      End While
                                                  Catch ex As Exception
                                                      monitorFailure = ex
                                                  End Try
                                                  Dim observed = New FileInfo(partialTarget).Length
                                                  Dim active = Not operation.IsCompleted AndAlso observed >= 1048576L AndAlso observed < size
                                                  cancellation.Cancel()
                                                  Dim canceled As Boolean
                                                  Dim downloadFailure As Exception = Nothing
                                                  Try
                                                      Await operation
                                                  Catch ex As OperationCanceledException
                                                      canceled = True
                                                  Catch ex As Exception
                                                      downloadFailure = ex
                                                  End Try
                                                  If monitorFailure IsNot Nothing Then ExceptionDispatchInfo.Capture(monitorFailure).Throw()
                                                  If downloadFailure IsNot Nothing Then ExceptionDispatchInfo.Capture(downloadFailure).Throw()
                                                  Assert.That(active, [Is].True, "The download completed before active cancellation could be observed; this run does not establish active cancellation.")
                                                  Assert.That(canceled, [Is].True)
                                                  Using unlocked As New FileStream(partialTarget, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                                                      Assert.That(unlocked.Length, [Is].GreaterThanOrEqualTo(1048576L).And.LessThan(size))
                                                      RecordLargeObservation("Native live download cancellation verified after " & unlocked.Length.ToString(Globalization.CultureInfo.InvariantCulture) & " of " & size.ToString(Globalization.CultureInfo.InvariantCulture) & " bytes; OperationCanceledException and exclusive target handle release verified.")
                                                  End Using
                                              End Using
                                          Finally
                                              File.Delete(source)
                                              File.Delete(downloaded)
                                              File.Delete(partialTarget)
                                          End Try
                                      End Function, allowLocalBoundary:=True, localExecutionMinutes:=20, localCleanupMinutes:=5)
    End Function

    Private Shared Sub RecordLargeObservation(observation As String)
        Dim directory = Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_EVIDENCE_DIRECTORY")
        If Not String.IsNullOrWhiteSpace(directory) Then
            File.AppendAllText(Path.Combine(directory, "large-transfer-observations.txt"), observation & Environment.NewLine, New Text.UTF8Encoding(False))
        End If
        TestContext.Progress.WriteLine(observation)
    End Sub

    Private Shared Function HashFile(path As String) As Byte()
        Using stream = File.OpenRead(path), hash = SHA256.Create()
            Return hash.ComputeHash(stream)
        End Using
    End Function

    Friend Shared Function WithOwnedCollectionAsync(body As Func(Of ScopevisioTeamworkDmsProvider, CancellationToken, Task), Optional allowLocalBoundary As Boolean = False, Optional localExecutionMinutes As Integer = 2, Optional localCleanupMinutes As Integer = 2) As Task
        Return ScopevisioLiveTestResourceScope.WithOwnedCollectionAsync(body, allowLocalBoundary, localExecutionMinutes, localCleanupMinutes)
    End Function

    Friend Shared Sub ValidateExclusiveCi(ci As String, coordinated As String)
        ScopevisioLiveTestResourceScope.ValidateExclusiveCi(ci, coordinated)
    End Sub

    Friend Shared Sub ValidateExclusiveLocalBoundary(machine As String, ci As String, windowEnd As String, now As DateTimeOffset)
        ScopevisioLiveTestResourceScope.ValidateExclusiveLocalBoundary(machine, ci, windowEnd, now)
    End Sub

    Friend Shared Sub ThrowNativeLiveFailures(failures As IList(Of Exception))
        ScopevisioLiveTestResourceScope.ThrowNativeLiveFailures(failures)
    End Sub

    Friend Shared Function RemoveOwnedCollectionAsync(provider As ScopevisioTeamworkDmsProvider, token As CancellationToken) As Task
        Return ScopevisioLiveTestResourceScope.RemoveOwnedCollectionAsync(provider, token)
    End Function

    Private Shared Function RequiredEnvironment(name As String) As String
        Return ScopevisioLiveTestResourceScope.RequiredEnvironment(name)
    End Function
    Private Class LiveUploadProgressCollector
        Implements IProgress(Of DmsTransferProgress)
        Friend Latest As DmsTransferProgress
        Public Sub Report(value As DmsTransferProgress) Implements IProgress(Of DmsTransferProgress).Report
            Latest = value
        End Sub
    End Class
End Class
