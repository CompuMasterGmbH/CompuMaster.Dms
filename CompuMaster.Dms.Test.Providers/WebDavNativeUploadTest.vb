Option Strict On
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class WebDavNativeUploadTest
    Private Const BaseUrl As String = "https://fixture.test:8443/prefix/remote.php/dav/files/user%20id/Folder/"

    <TestCase("https://fixture.test:8443/prefix/remote.php/dav/files/user%20id/Folder/", "https://fixture.test:8443/prefix/remote.php/dav/uploads/user%20id/")>
    <TestCase("https://fixture.test/dav/files/uid/", "https://fixture.test/dav/uploads/uid/")>
    <TestCase("https://fixture.test/remote.php/webdav/", Nothing)>
    <TestCase("https://fixture.test/dav/files/uid/?token=secret", Nothing)>
    <TestCase("https://name:secret@fixture.test/dav/files/uid/", Nothing)>
    Public Sub NamespaceUsesTheEncodedProtocolUserAndSameOrigin(address As String, expected As String)
        Assert.That(WebDavDmsProvider.GetNativeUploadNamespace(address)?.AbsoluteUri, [Is].EqualTo(expected))
    End Sub

    <TestCase(1, 207, True)>
    <TestCase(2, 207, True)>
    <TestCase(0, 207, False)>
    <TestCase(3, 207, False)>
    <TestCase(1, 404, False)>
    <TestCase(2, 405, False)>
    <TestCase(2, 501, False)>
    Public Async Function LargeUploadSelectsVerifiedNativeSupport(family As Integer, capabilityStatus As Integer, native As Boolean) As Task
        Using handler As New DavFixture With {.CapabilityStatus = capabilityStatus}, http As New HttpClient(handler)
            Dim provider As New WebDavDmsProvider
            provider.ConfigureNativeUploadsForTesting(New Global.WebDav.WebDavClient(http), BaseUrl, CType(family, OcsServerFamily))
            Dim data(10 * 1024 * 1024 + 16) As Byte
            data(data.Length - 1) = 91
            Await provider.UploadFileAsync("large.bin", Function() New MemoryStream(data, False))
            Assert.That(handler.Methods.Contains("MOVE"), [Is].EqualTo(native))
            Assert.That(handler.Sizes, [Is].EqualTo(If(native, New Long() {10 * 1024 * 1024, 17}, New Long() {data.Length})))
            Assert.That(handler.LastByte, [Is].EqualTo(91))
            If native Then
                Assert.That(handler.Destinations.All(Function(value) value = BaseUrl & "large.bin"), [Is].True)
                Assert.That(handler.SessionExists, [Is].False)
            Else
                Assert.That(handler.Methods, Does.Not.Contain("MKCOL"))
            End If
        End Using
    End Function

    <TestCase(403, Nothing)>
    <TestCase(207, "MOVE")>
    Public Sub NativeFailuresAreNotReplayedAsPut(status As Integer, failMethod As String)
        Using handler As New DavFixture With {.CapabilityStatus = status, .FailMethod = failMethod}, http As New HttpClient(handler)
            Dim provider As New WebDavDmsProvider
            provider.ConfigureNativeUploadsForTesting(New Global.WebDav.WebDavClient(http), BaseUrl, OcsServerFamily.Nextcloud)
            Assert.ThrowsAsync(Of CompuMaster.Ocs.Exceptions.ResponseException)(Async Function()
                                                                                  Await provider.UploadFileAsync("large.bin", Function() New MemoryStream(New Byte(10 * 1024 * 1024) {}, False))
                                                                              End Function)
            Assert.That(handler.DirectPuts, [Is].Zero)
            Assert.That(handler.SessionExists, [Is].False)
        End Using
    End Sub

    <TestCase(0)>
    <TestCase(100)>
    Public Async Function SmallFilesDoNotProbeTheNativeNamespace(size As Integer) As Task
        Using handler As New DavFixture, http As New HttpClient(handler)
            Dim provider As New WebDavDmsProvider
            provider.ConfigureNativeUploadsForTesting(New Global.WebDav.WebDavClient(http), BaseUrl, OcsServerFamily.Nextcloud)
            Await provider.UploadFileAsync("small.bin", Function() New MemoryStream(New Byte(size - 1) {}, False))
            Assert.That(handler.Methods, [Is].EqualTo(New String() {"PUT"}))
        End Using
    End Function

    <TestCase(True), TestCase(False)>
    Public Async Function LocalProgressPreservesTimeAndCompletesAfterAssembly(native As Boolean) As Task
        Dim path = System.IO.Path.GetTempFileName()
        Try
            Using output = File.OpenWrite(path)
                output.SetLength(If(native, 10L * 1024 * 1024 + 17, 17))
            End Using
            Dim modified As New DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc)
            File.SetLastWriteTimeUtc(path, modified)
            Using handler As New DavFixture, http As New HttpClient(handler)
                Dim provider As New WebDavDmsProvider
                provider.ConfigureNativeUploadsForTesting(New Global.WebDav.WebDavClient(http), BaseUrl, OcsServerFamily.Nextcloud)
                Dim completed As Boolean
                Dim observer As New ImmediateProgress(Sub(value)
                                                          If value.Phase = DmsTransferPhase.Completed Then
                                                              Assert.That(handler.Methods.Contains("MOVE"), [Is].EqualTo(native))
                                                              Assert.That(handler.SessionExists, [Is].False)
                                                              completed = True
                                                          End If
                                                      End Sub)
                Await provider.UploadFileWithProgressAsync("dated.bin", path, observer)
                Assert.That(completed, [Is].True)
                Assert.That(handler.Modified, [Is].EqualTo(New DateTimeOffset(modified).ToUnixTimeSeconds().ToString(Globalization.CultureInfo.InvariantCulture)))
            End Using
        Finally
            File.Delete(path)
        End Try
    End Function

    Private Class ImmediateProgress
        Implements IProgress(Of DmsTransferProgress)
        Private ReadOnly Callback As Action(Of DmsTransferProgress)
        Public Sub New(callback As Action(Of DmsTransferProgress))
            Me.Callback = callback
        End Sub
        Public Sub Report(value As DmsTransferProgress) Implements IProgress(Of DmsTransferProgress).Report
            Callback(value)
        End Sub
    End Class

    <Test>
    Public Async Function UnknownLengthRetainsPutAndCancellationDoesNotOpenTheSource() As Task
        Using handler As New DavFixture, http As New HttpClient(handler), cancellation As New CancellationTokenSource
            Dim provider As New WebDavDmsProvider
            provider.ConfigureNativeUploadsForTesting(New Global.WebDav.WebDavClient(http), BaseUrl, OcsServerFamily.Nextcloud)
            Await provider.UploadFileAsync("unknown.bin", Function() New UnknownLengthSource)
            Assert.That(handler.Methods, [Is].EqualTo(New String() {"PUT"}))
            cancellation.Cancel()
            Dim opened As Boolean
            Assert.CatchAsync(Of OperationCanceledException)(Async Function()
                                                                 Await provider.UploadFileAsync("cancelled.bin", Function()
                                                                                                                     opened = True
                                                                                                                     Return New MemoryStream
                                                                                                                 End Function, cancellation.Token)
                                                             End Function)
            Assert.That(opened, [Is].False)
            Assert.That(handler.Methods.Count, [Is].EqualTo(1))
        End Using
    End Function

    Private Class UnknownLengthSource
        Inherits Stream
        Private ReadOnly Input As New MemoryStream(New Byte() {1, 2, 3}, False)
        Public Overrides ReadOnly Property CanRead As Boolean = True
        Public Overrides ReadOnly Property CanSeek As Boolean = False
        Public Overrides ReadOnly Property CanWrite As Boolean = False
        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property
        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException()
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property
        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            Return Input.Read(buffer, offset, count)
        End Function
        Public Overrides Function ReadAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)
            Return Input.ReadAsync(buffer, offset, count, cancellationToken)
        End Function
        Public Overrides Sub Flush()
        End Sub
        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then Input.Dispose()
            MyBase.Dispose(disposing)
        End Sub
    End Class

    Private Class DavFixture
        Inherits HttpMessageHandler
        Friend CapabilityStatus As Integer = 207
        Friend FailMethod As String
        Friend SessionExists As Boolean
        Friend ReadOnly Methods As New List(Of String)
        Friend ReadOnly Sizes As New List(Of Long)
        Friend ReadOnly Destinations As New List(Of String)
        Friend LastByte As Byte
        Friend DirectPuts As Integer
        Friend Modified As String
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, token As CancellationToken) As Task(Of HttpResponseMessage)
            Dim method = request.Method.Method
            Methods.Add(method)
            Dim root = request.RequestUri.AbsolutePath.EndsWith("/uploads/user%20id/", StringComparison.Ordinal)
            If method = "PROPFIND" Then
                Dim status = If(root, CapabilityStatus, If(SessionExists, 207, 404))
                Dim xml = "<d:multistatus xmlns:d=""DAV:""><d:response><d:href>" & request.RequestUri.AbsolutePath & "</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response></d:multistatus>"
                Return New HttpResponseMessage(CType(status, HttpStatusCode)) With {.Content = New StringContent(xml, System.Text.Encoding.UTF8, "application/xml")}
            End If
            Dim result = 201
            If method = "MKCOL" Then SessionExists = True
            If request.Headers.Contains("Destination") Then Destinations.Add(request.Headers.GetValues("Destination").Single())
            If method = "PUT" Then
                If request.Headers.Contains("X-OC-Mtime") Then Modified = request.Headers.GetValues("X-OC-Mtime").Single()
                If request.RequestUri.AbsolutePath.Contains("/files/") Then DirectPuts += 1
                Dim bytes = Await request.Content.ReadAsByteArrayAsync()
                Sizes.Add(bytes.LongLength)
                If bytes.Length > 0 Then LastByte = bytes(bytes.Length - 1)
            End If
            If method = "MOVE" Then
                If request.Headers.Contains("X-OC-Mtime") Then Modified = request.Headers.GetValues("X-OC-Mtime").Single()
                If FailMethod <> "MOVE" Then SessionExists = False
            End If
            If method = "DELETE" Then
                SessionExists = False
                result = 204
            End If
            If method = FailMethod Then result = 503
            Return New HttpResponseMessage(CType(result, HttpStatusCode)) With {.Content = New StringContent("")}
        End Function
    End Class
End Class
