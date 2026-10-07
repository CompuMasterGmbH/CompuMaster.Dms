Option Explicit On
Option Strict On

Imports System.IO
Imports System.Net
Imports System.Runtime.ExceptionServices
Imports System.Security.Cryptography
Imports System.Threading
Imports System.Threading.Tasks
Imports NUnit.Framework

<TestFixture, Category("RemoteDms"), Category("TestLevel2"), Category("ScopevisioNativeUploadBoundary"), NonParallelizable>
Public Class ScopevisioNativeUploadBoundaryTest
    <Test>
    Public Async Function ObservesEightAndTwelveMiBRequestsWithoutCompletingTheLargeTransferGate() As Task
        Dim evidenceDirectory = Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_EVIDENCE_DIRECTORY")
        If String.IsNullOrWhiteSpace(evidenceDirectory) Then Throw New InvalidOperationException("The upload-boundary diagnostic requires a retained evidence directory before server access.")
        Directory.CreateDirectory(evidenceDirectory)
        Await ScopevisioNativeLiveTest.WithOwnedCollectionAsync(Async Function(provider, token)
                                                                  For Each mib In New Integer() {8, 12}
                                                                      Dim local = Path.GetTempFileName()
                                                                      Dim downloaded = Path.GetTempFileName()
                                                                      Try
                                                                          Dim size = CLng(mib) * 1024L * 1024L
                                                                          Dim block(65535) As Byte
                                                                          For index = 0 To block.Length - 1
                                                                              block(index) = CByte(index Mod 251)
                                                                          Next
                                                                          Using output As New FileStream(local, FileMode.Create, FileAccess.Write, FileShare.None, block.Length, FileOptions.Asynchronous)
                                                                              For offset As Long = 0 To size - 1 Step block.Length
                                                                                  Await output.WriteAsync(block, 0, block.Length, token)
                                                                              Next
                                                                          End Using
                                                                          Dim remote = ScopevisioNativeLiveTest.OwnedCollection & "/boundary-" & mib.ToString(Globalization.CultureInfo.InvariantCulture) & ".bin"
                                                                          Dim failure As Exception = Nothing
                                                                          Try
                                                                              Await provider.UploadFileAsync(remote, local, token)
                                                                          Catch ex As Exception
                                                                              failure = ex
                                                                          End Try
                                                                          If failure Is Nothing Then
                                                                              Dim selected = Await provider.ListRemoteItemAsync(remote, token)
                                                                              Assert.That(selected.ContentLength, [Is].EqualTo(size))
                                                                              Await provider.DownloadFileAsync(selected, downloaded, token)
                                                                              Assert.That(New FileInfo(downloaded).Length, [Is].EqualTo(size))
                                                                              Assert.That(HashFile(downloaded), [Is].EqualTo(HashFile(local)))
                                                                              WriteObservation(evidenceDirectory, "Upload-boundary diagnostic: " & size.ToString(Globalization.CultureInfo.InvariantCulture) & " bytes accepted; downloaded length and SHA-256 verified.")
                                                                          Else
                                                                              If TypeOf failure Is OperationCanceledException Then ExceptionDispatchInfo.Capture(failure).Throw()
                                                                              Dim diagnostic = DescribeRejection(failure)
                                                                              If diagnostic Is Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
                                                                              WriteObservation(evidenceDirectory, "Upload-boundary diagnostic: " & size.ToString(Globalization.CultureInfo.InvariantCulture) & " bytes rejected; " & diagnostic & ". No unchanged replay.")
                                                                              'A successful smaller upload is required before interpreting a larger rejection as a boundary observation.
                                                                              If mib = 8 Then ExceptionDispatchInfo.Capture(failure).Throw()
                                                                          End If
                                                                      Finally
                                                                          File.Delete(local)
                                                                          File.Delete(downloaded)
                                                                      End Try
                                                                  Next
                                                                  WriteObservation(evidenceDirectory, "Diagnostic scope only: this does not verify the 256-MiB large-transfer or active-cancellation acceptance gates, or establish an exact universal limit.")
                                                              End Function, allowLocalBoundary:=True)
    End Function

    <Test>
    Public Async Function ObservesActiveDownloadCancellationOnEightMiBFixtureWithoutCompletingLargeGate() As Task
        Dim evidenceDirectory = Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_EVIDENCE_DIRECTORY")
        If String.IsNullOrWhiteSpace(evidenceDirectory) Then Throw New InvalidOperationException("The cancellation diagnostic requires a retained evidence directory before server access.")
        Directory.CreateDirectory(evidenceDirectory)
        Await ScopevisioNativeLiveTest.WithOwnedCollectionAsync(Async Function(provider, token)
                                                                  Const size As Long = 8L * 1024L * 1024L
                                                                  Dim local = Path.GetTempFileName()
                                                                  Dim partialTarget = Path.GetTempFileName()
                                                                  Try
                                                                      Dim block(65535) As Byte
                                                                      For index = 0 To block.Length - 1
                                                                          block(index) = CByte(index Mod 251)
                                                                      Next
                                                                      Using output As New FileStream(local, FileMode.Create, FileAccess.Write, FileShare.None, block.Length, FileOptions.Asynchronous)
                                                                          For offset As Long = 0 To size - 1 Step block.Length
                                                                              Await output.WriteAsync(block, 0, block.Length, token)
                                                                          Next
                                                                      End Using
                                                                      Dim remote = ScopevisioNativeLiveTest.OwnedCollection & "/cancellation-8.bin"
                                                                      Await provider.UploadFileAsync(remote, local, token)
                                                                      Dim selected = Await provider.ListRemoteItemAsync(remote, token)
                                                                      Assert.That(selected.ContentLength, [Is].EqualTo(size))
                                                                      Using cancellation = CancellationTokenSource.CreateLinkedTokenSource(token)
                                                                          Dim operation = provider.DownloadFileAsync(selected, partialTarget, cancellation.Token)
                                                                          Dim monitorFailure As Exception = Nothing
                                                                          Try
                                                                              While Not operation.IsCompleted AndAlso New FileInfo(partialTarget).Length < 1048576L
                                                                                  Await Task.WhenAny(operation, Task.Delay(1, token))
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
                                                                          Assert.That(active, [Is].True, "No active partial download was observed; this run does not establish cancellation.")
                                                                          Assert.That(canceled, [Is].True)
                                                                          Using unlocked As New FileStream(partialTarget, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                                                                              Assert.That(unlocked.Length, [Is].GreaterThanOrEqualTo(1048576L).And.LessThan(size))
                                                                              WriteObservation(evidenceDirectory, "Small-fixture cancellation diagnostic: OperationCanceledException after " & unlocked.Length.ToString(Globalization.CultureInfo.InvariantCulture) & " of 8388608 bytes; exclusive target handle reopened. No large-transfer acceptance.")
                                                                          End Using
                                                                      End Using
                                                                  Finally
                                                                      File.Delete(local)
                                                                      File.Delete(partialTarget)
                                                                  End Try
                                                              End Function, allowLocalBoundary:=True)
    End Function

    Friend Shared Sub WriteObservation(evidenceDirectory As String, observation As String)
        File.AppendAllText(Path.Combine(evidenceDirectory, "upload-boundary-observations.txt"), observation & Environment.NewLine, New Text.UTF8Encoding(False))
        TestContext.Progress.WriteLine(observation)
    End Sub

    Friend Shared Function DescribeRejection(failure As Exception) As String
        Dim badRequest = TryCast(failure, Global.CenterDevice.Rest.Exceptions.BadRequestException)
        If badRequest IsNot Nothing Then
            Return "HTTP 400; backend code " & If(badRequest.ErrorResponse Is Nothing, "unknown", badRequest.ErrorResponse.Code.ToString(Globalization.CultureInfo.InvariantCulture))
        End If
        Dim serverError = TryCast(failure, WebException)
        If serverError IsNot Nothing AndAlso TypeOf serverError.Data("CompuMaster.Scopevisio.Teamwork.HttpStatusCode") Is Integer Then
            Dim status = CInt(serverError.Data("CompuMaster.Scopevisio.Teamwork.HttpStatusCode"))
            If status >= 500 AndAlso status <= 599 Then Return "HTTP " & status.ToString(Globalization.CultureInfo.InvariantCulture)
        End If
        Return Nothing
    End Function

    Private Shared Function HashFile(path As String) As Byte()
        Using stream = File.OpenRead(path), hash = SHA256.Create()
            Return hash.ComputeHash(stream)
        End Using
    End Function
End Class
