Option Explicit On
Option Strict On

Imports System.IO
Imports System.Runtime.ExceptionServices
Imports System.Security.Cryptography
Imports System.Threading.Tasks
Imports NUnit.Framework

<TestFixture, Category("RemoteDms"), Category("TestLevel2"), Category("ScopevisioSynchronousUploadComparison"), NonParallelizable>
Public Class ScopevisioSynchronousUploadComparisonTest
    <Test>
    Public Async Function ObservesEightAndTwelveMiBThroughTheSynchronousPublicUpload() As Task
        Dim evidenceDirectory = Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_EVIDENCE_DIRECTORY")
        If String.IsNullOrWhiteSpace(evidenceDirectory) Then Throw New InvalidOperationException("The synchronous comparison requires retained evidence before server access.")
        Directory.CreateDirectory(evidenceDirectory)
        Dim requestedSize = Environment.GetEnvironmentVariable("NATIVE_SCOPEVISIO_SYNC_TRANSFER_MIB")
        Dim sizes = ComparisonSizes(requestedSize)
        Dim large = Not String.IsNullOrWhiteSpace(requestedSize)
        Await ScopevisioLiveTestResourceScope.WithOwnedCollectionAsync(Async Function(provider, token)
                                                                         For Each mib In sizes
                                                                             token.ThrowIfCancellationRequested()
                                                                             Dim size = CLng(mib) * 1024L * 1024L
                                                                             Dim local = Path.GetTempFileName()
                                                                             Dim downloaded = Path.GetTempFileName()
                                                                             Try
                                                                                 Dim block(65535) As Byte
                                                                                 For index = 0 To block.Length - 1
                                                                                     block(index) = CByte(index Mod 251)
                                                                                 Next
                                                                                 Using output = File.Create(local)
                                                                                     For offset As Long = 0 To size - 1 Step block.Length
                                                                                         output.Write(block, 0, block.Length)
                                                                                     Next
                                                                                 End Using
                                                                                 Dim remote = ScopevisioLiveTestResourceScope.OwnedCollection & "/sync-boundary-" & mib.ToString(Globalization.CultureInfo.InvariantCulture) & ".bin"
                                                                                 Dim failure As Exception = Nothing
                                                                                 Try
                                                                                     provider.UploadFile(remote, local)
                                                                                 Catch ex As Exception
                                                                                     failure = ex
                                                                                 End Try
                                                                                 If failure Is Nothing Then
                                                                                     Dim selected = provider.ListRemoteItem(remote)
                                                                                     Assert.That(selected.ContentLength, [Is].EqualTo(size))
                                                                                     provider.DownloadFile(selected, downloaded)
                                                                                     Assert.That(New FileInfo(downloaded).Length, [Is].EqualTo(size))
                                                                                     Assert.That(HashFile(downloaded), [Is].EqualTo(HashFile(local)))
                                                                                     WriteObservation(evidenceDirectory, "Public synchronous comparison: " & size.ToString(Globalization.CultureInfo.InvariantCulture) & " bytes accepted; selected-ID downloaded length and SHA-256 verified.")
                                                                                 Else
                                                                                     Dim rejected = TryCast(failure, Global.CenterDevice.Rest.Exceptions.BadRequestException)
                                                                                     If rejected Is Nothing Then ExceptionDispatchInfo.Capture(failure).Throw()
                                                                                     WriteObservation(evidenceDirectory, "Public synchronous comparison: " & size.ToString(Globalization.CultureInfo.InvariantCulture) & " bytes rejected; HTTP 400; backend code " & If(rejected.ErrorResponse Is Nothing, "unknown", rejected.ErrorResponse.Code.ToString(Globalization.CultureInfo.InvariantCulture)) & ". No unchanged replay.")
                                                                                     If mib = 8 OrElse large Then ExceptionDispatchInfo.Capture(failure).Throw()
                                                                                 End If
                                                                             Finally
                                                                                 File.Delete(local)
                                                                                 File.Delete(downloaded)
                                                                             End Try
                                                                         Next
                                                                         Await Task.CompletedTask
                                                                     End Function, allowLocalBoundary:=True, localExecutionMinutes:=If(large, 20, 2), localCleanupMinutes:=If(large, 5, 2))
    End Function

    Friend Shared Function ComparisonSizes(requested As String) As Integer()
        If String.IsNullOrWhiteSpace(requested) Then Return New Integer() {8, 12}
        Dim size = Integer.Parse(requested, Globalization.CultureInfo.InvariantCulture)
        If size < 256 OrElse size > 4096 Then Throw New InvalidOperationException("The explicit synchronous large-transfer fixture must be between 256 and 4096 MiB.")
        Return New Integer() {size}
    End Function

    Private Shared Sub WriteObservation(directory As String, observation As String)
        File.AppendAllText(Path.Combine(directory, "synchronous-upload-observations.txt"), observation & Environment.NewLine, New Text.UTF8Encoding(False))
    End Sub

    Private Shared Function HashFile(path As String) As Byte()
        Using stream = File.OpenRead(path), hash = SHA256.Create()
            Return hash.ComputeHash(stream)
        End Using
    End Function
End Class
