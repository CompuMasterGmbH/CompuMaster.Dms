Option Strict On

Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

'Synthetic credentials and loopback only; no shared DMS resources.
<TestFixture>
Public Class WebDavAuthenticationLoopbackTest
    <Test>
    Public Async Function UploadReusesTheAuthenticationEstablishedByAuthorize() As Task
        Dim listener As New TcpListener(IPAddress.Loopback, 0)
        listener.Start()
        Dim server As Task = Nothing
        Using deadline As New CancellationTokenSource(TimeSpan.FromSeconds(15))
            Try
                Dim origin = "http://127.0.0.1:" & CStr(DirectCast(listener.LocalEndpoint, IPEndPoint).Port) & "/dav/"
                Dim payload(1024 * 1024 - 1) As Byte
                For i = 0 To payload.Length - 1
                    payload(i) = CByte(i Mod 251)
                Next
                server = ServeAsync(listener, payload, deadline.Token)
                Dim provider As New WebDavDmsProvider()
                Await Task.Run(Sub() provider.Authorize(New WebDavLoginCredentials With {.BaseUrl = origin, .Username = "fixture", .Password = "synthetic"}))
                Await provider.UploadFileAsync("payload.bin", Function() New MemoryStream(payload, False), deadline.Token)
                Await server
            Finally
                deadline.Cancel()
                listener.Stop()
            End Try
        End Using
    End Function

    Private Shared Async Function ServeAsync(listener As TcpListener, payload As Byte(), token As CancellationToken) As Task
        Using stopAccept = token.Register(Sub() listener.Stop())
            For attempt = 0 To 2
                Using socket = Await listener.AcceptTcpClientAsync(), stream = socket.GetStream(), stopRead = token.Register(Sub() socket.Dispose())
                    Dim first = Await ReadLineAsync(stream, token)
                    Dim headers As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    Dim line = Await ReadLineAsync(stream, token)
                    While line.Length <> 0
                        Dim split = line.IndexOf(":"c)
                        If split <= 0 OrElse headers.Count >= 64 Then Throw New IOException("Invalid fixture header.")
                        headers.Add(line.Substring(0, split), line.Substring(split + 1).Trim())
                        line = Await ReadLineAsync(stream, token)
                    End While
                    If headers.ContainsKey("Expect") Then Await WriteAsync(stream, "HTTP/1.1 100 Continue" & vbCrLf & vbCrLf, token)
                    Dim size As Integer = 0
                    If headers.ContainsKey("Content-Length") Then size = Integer.Parse(headers("Content-Length"), Globalization.CultureInfo.InvariantCulture)
                    Assert.That(size, [Is].InRange(0, payload.Length))
                    Dim body(size - 1) As Byte
                    Dim read As Integer = 0
                    While read < size
                        Dim count = Await stream.ReadAsync(body, read, size - read, token)
                        If count = 0 Then Throw New EndOfStreamException()
                        read += count
                    End While
                    If attempt = 0 Then
                        Assert.That(first, Does.StartWith("PROPFIND "))
                        Assert.That(headers.ContainsKey("Authorization"), [Is].False)
                        Await WriteAsync(stream, "HTTP/1.1 401 Unauthorized" & vbCrLf & "WWW-Authenticate: Basic realm=""fixture""" & vbCrLf & "Content-Length: 0" & vbCrLf & "Connection: close" & vbCrLf & vbCrLf, token)
                    ElseIf attempt = 1 Then
                        Assert.That(first, Does.StartWith("PROPFIND "))
                        Assert.That(headers.ContainsKey("Authorization"), [Is].True)
                        Dim xml = "<d:multistatus xmlns:d=""DAV:""/>"
                        Await WriteAsync(stream, "HTTP/1.1 207 Multi-Status" & vbCrLf & "Content-Type: application/xml" & vbCrLf & "Content-Length: " & CStr(Encoding.UTF8.GetByteCount(xml)) & vbCrLf & "Connection: close" & vbCrLf & vbCrLf & xml, token)
                    Else
                        Assert.That(first, [Is].EqualTo("PUT /dav/payload.bin HTTP/1.1"))
                        Assert.That(headers.ContainsKey("Authorization"), [Is].True, "PUT must authenticate before sending its body, without a second challenge or replay.")
                        Assert.That(body, [Is].EqualTo(payload))
                        Await WriteAsync(stream, "HTTP/1.1 201 Created" & vbCrLf & "Content-Length: 0" & vbCrLf & "Connection: close" & vbCrLf & vbCrLf, token)
                    End If
                End Using
            Next
        End Using
    End Function

    Private Shared Async Function WriteAsync(stream As Stream, value As String, token As CancellationToken) As Task
        Dim bytes = Encoding.UTF8.GetBytes(value)
        Await stream.WriteAsync(bytes, 0, bytes.Length, token)
    End Function

    Private Shared Async Function ReadLineAsync(stream As Stream, token As CancellationToken) As Task(Of String)
        Dim bytes As New List(Of Byte)()
        Dim buffer(0) As Byte
        While True
            If Await stream.ReadAsync(buffer, 0, 1, token) <> 1 Then Throw New EndOfStreamException()
            bytes.Add(buffer(0))
            If bytes.Count > 8192 Then Throw New IOException("Fixture HTTP line too long.")
            If bytes.Count >= 2 AndAlso bytes(bytes.Count - 2) = 13 AndAlso bytes(bytes.Count - 1) = 10 Then Return Encoding.ASCII.GetString(bytes.Take(bytes.Count - 2).ToArray())
        End While
        Throw New IOException("Fixture HTTP line terminated unexpectedly.")
    End Function
End Class
