Option Explicit On
Option Strict On

Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Sockets
Imports System.Reflection
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.OAuth
Imports Newtonsoft.Json.Linq
Imports NUnit.Framework
Imports RestSharp

'Only loopback with a synthetic token: no DMS server, credentials, or shared resources.
<TestFixture>
Public Class CenterDeviceMultipartLoopbackTest
    <TestCase(False), TestCase(True)>
    Public Async Function PublicSynchronousUploadTransmitsCompleteTwelveMiBOverRealHttp(fromFile As Boolean) As Task
        Dim bytes(12 * 1024 * 1024 - 1) As Byte
        For index = 0 To bytes.Length - 1
            bytes(index) = CByte(index Mod 251)
        Next
        Dim listener As New TcpListener(IPAddress.Loopback, 0)
        Dim path = IO.Path.GetTempFileName()
        Dim client As DocumentsRestClient = Nothing
        Dim received As Task(Of WireRequest) = Nothing
        listener.Start()
        Using deadline As New CancellationTokenSource(TimeSpan.FromSeconds(30))
            Try
                File.WriteAllBytes(path, bytes)
                Dim origin = "http://127.0.0.1:" & CStr(CType(listener.LocalEndpoint, IPEndPoint).Port) & "/"
                client = New DocumentsRestClient(New Authorization(), New Configuration(origin), Nothing, Nothing, "")
                client.DisableOfflineModeSimulation()
                received = ReceiveAndRespondAsync(listener, deadline.Token)
                Await Task.Run(Sub()
                                   If fromFile Then
                                       client.UploadDocument("user", "fixture.bin", path, "collection", "folder", deadline.Token)
                                   Else
                                       client.UploadDocument("user", "fixture.bin", Function() New MemoryStream(bytes, False), "collection", "folder", deadline.Token)
                                   End If
                               End Sub)
                Dim wire = Await received
                Assert.That(wire.FirstLine, [Is].EqualTo("POST /documents HTTP/1.1"))
                Assert.That(wire.Headers("Content-Type"), Does.StartWith("multipart/form-data;"))
                Assert.That(wire.Headers.ContainsKey("Content-Length") AndAlso wire.Headers.ContainsKey("Transfer-Encoding"), [Is].False, "HTTP framing must not provide conflicting lengths.")
                Dim match = Regex.Match(wire.Headers("Content-Type"), "boundary=""?([^"";]+)")
                Assert.That(match.Success, [Is].True)
                Dim boundary = match.Groups(1).Value
                Dim body = Encoding.ASCII.GetString(wire.Body)
                Assert.That(body, Does.StartWith("--" & boundary & vbCrLf).And.EndWith(vbCrLf & "--" & boundary & "--" & vbCrLf))
                Dim metadata = Regex.Match(body, "name=""?metadata""?(?:;|\r\n)")
                Dim document = Regex.Match(body, "name=""?document""?(?:;|\r\n)")
                Assert.That(metadata.Success AndAlso document.Success, [Is].True)
                Dim metadataStart = body.IndexOf(vbCrLf & vbCrLf, metadata.Index, StringComparison.Ordinal) + 4
                Dim metadataEnd = body.IndexOf(vbCrLf & "--" & boundary, metadataStart, StringComparison.Ordinal)
                Dim json = JObject.Parse(body.Substring(metadataStart, metadataEnd - metadataStart))
                Assert.That(json("metadata")("document")("size").Type, [Is].EqualTo(JTokenType.Integer))
                Assert.That(CLng(json("metadata")("document")("size")), [Is].EqualTo(CLng(bytes.Length)))
                Assert.That(CStr(json("metadata")("document")("filename")), [Is].EqualTo("fixture.bin"))
                Dim documentStart = body.IndexOf(vbCrLf & vbCrLf, document.Index, StringComparison.Ordinal) + 4
                Assert.That(wire.Body.Skip(documentStart).Take(bytes.Length), [Is].EqualTo(bytes))
                Assert.That(body.Substring(documentStart + bytes.Length), Does.StartWith(vbCrLf & "--" & boundary))
                Assert.That(Regex.Matches(body, "Content-Disposition: form-data;").Count, [Is].EqualTo(2))
                TestContext.Progress.WriteLine("Loopback: complete12MiB HTTP multipart received with " & If(wire.Headers.ContainsKey("Transfer-Encoding"), "chunked framing", "Content-Length framing") & "; metadata first=" & (metadata.Index < document.Index).ToString() & "; synthetic token only.")
            Finally
                listener.Stop()
                If client IsNot Nothing Then
                    Dim field = GetType(CenterDeviceRestClient).GetField("client", BindingFlags.NonPublic Or BindingFlags.Instance)
                    CType(field.GetValue(client), RestClient).Dispose()
                End If
                If received IsNot Nothing AndAlso received.IsFaulted Then
                    Dim ignored = received.Exception
                End If
                File.Delete(path)
            End Try
        End Using
    End Function

    Private Shared Async Function ReceiveAndRespondAsync(listener As TcpListener, token As CancellationToken) As Task(Of WireRequest)
        Using stopAccept = token.Register(Sub() listener.Stop()), socket = Await listener.AcceptTcpClientAsync()
            Using stream = socket.GetStream(), closeRead = token.Register(Sub() stream.Dispose())
                Dim request As New WireRequest With {.FirstLine = Await ReadLineAsync(stream, token)}
                Dim line = Await ReadLineAsync(stream, token)
                While line.Length > 0
                    Dim separator = line.IndexOf(":"c)
                    If separator <= 0 OrElse request.Headers.Count >= 32 Then Throw New IOException("Invalid loopback HTTP header.")
                    request.Headers.Add(line.Substring(0, separator), line.Substring(separator + 1).Trim())
                    line = Await ReadLineAsync(stream, token)
                End While
                If request.Headers.ContainsKey("Expect") Then
                    Dim continueResponse = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue" & vbCrLf & vbCrLf)
                    Await stream.WriteAsync(continueResponse, 0, continueResponse.Length, token)
                End If
                Using body As New MemoryStream()
                    If request.Headers.ContainsKey("Transfer-Encoding") Then
                        If request.Headers("Transfer-Encoding") <> "chunked" Then Throw New IOException("Unsupported loopback transfer encoding.")
                        While True
                            Dim chunk = Await ReadLineAsync(stream, token)
                            Dim length = Integer.Parse(chunk.Split(";"c)(0), Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture)
                            If length = 0 Then
                                If (Await ReadLineAsync(stream, token)).Length <> 0 Then Throw New IOException("Unexpected loopback trailer.")
                                Exit While
                            End If
                            Await ReadBodyAsync(stream, body, length, token)
                            If (Await ReadLineAsync(stream, token)).Length <> 0 Then Throw New IOException("Invalid chunk terminator.")
                        End While
                    Else
                        Await ReadBodyAsync(stream, body, Integer.Parse(request.Headers("Content-Length"), Globalization.CultureInfo.InvariantCulture), token)
                    End If
                    request.Body = body.ToArray()
                End Using
                Dim json = "{""id"":""fixture-document""}"
                Dim response = Encoding.ASCII.GetBytes("HTTP/1.1 201 Created" & vbCrLf & "Content-Type: application/json" & vbCrLf & "Content-Length: " & CStr(json.Length) & vbCrLf & "Connection: close" & vbCrLf & vbCrLf & json)
                Await stream.WriteAsync(response, 0, response.Length, token)
                Return request
            End Using
        End Using
    End Function

    Private Shared Async Function ReadLineAsync(stream As Stream, token As CancellationToken) As Task(Of String)
        Dim bytes As New List(Of Byte)()
        Dim singleByte(0) As Byte
        While True
            If Await stream.ReadAsync(singleByte, 0, 1, token) <> 1 Then Throw New EndOfStreamException()
            bytes.Add(singleByte(0))
            If bytes.Count > 8192 Then Throw New IOException("Loopback HTTP line too long.")
            If bytes.Count >= 2 AndAlso bytes(bytes.Count - 2) = 13 AndAlso bytes(bytes.Count - 1) = 10 Then
                Return Encoding.ASCII.GetString(bytes.Take(bytes.Count - 2).ToArray())
            End If
        End While
        Throw New IOException("Loopback line reader terminated unexpectedly.")
    End Function

    Private Shared Async Function ReadBodyAsync(source As Stream, destination As MemoryStream, count As Integer, token As CancellationToken) As Task
        If count < 0 OrElse destination.Length + count > 16L * 1024L * 1024L Then Throw New IOException("Loopback fixture body exceeds its bound.")
        Dim buffer(65535) As Byte
        While count > 0
            Dim read = Await source.ReadAsync(buffer, 0, Math.Min(buffer.Length, count), token)
            If read = 0 Then Throw New EndOfStreamException()
            destination.Write(buffer, 0, read)
            count -= read
        End While
    End Function

    Private Class WireRequest
        Friend FirstLine As String
        Friend ReadOnly Headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Friend Body As Byte()
    End Class

    Private Class Authorization
        Implements IOAuthInfoProvider
        Public Function GetOAuthInfo(userId As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
            Return New OAuthInfo With {.UserId = userId, .access_token = "fixture-token"}
        End Function
    End Class

    Private Class Configuration
        Implements IRestClientConfiguration
        Private ReadOnly Origin As String
        Friend Sub New(origin As String)
            Me.Origin = origin
        End Sub
        Public ReadOnly Property BaseAddress As String Implements IRestClientConfiguration.BaseAddress
            Get
                Return Origin
            End Get
        End Property
        Public ReadOnly Property UserAgent As String Implements IRestClientConfiguration.UserAgent
            Get
                Return "Isolated real HTTP multipart fixture"
            End Get
        End Property
    End Class
End Class
