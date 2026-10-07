Option Explicit On
Option Strict On

Imports System.IO
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.Documents
Imports CenterDevice.Rest.Clients.OAuth
Imports Newtonsoft.Json.Linq
Imports NUnit.Framework
Imports RestSharp

<TestFixture>
Public Class CenterDeviceMultipartRequestContractTest
    <TestCase(False), TestCase(True)>
    Public Sub SynchronousPublicSdkProducesACompleteTwelveMiBMultipartRequestWithoutAClientSizeCeiling(fromFile As Boolean)
        Dim bytes(12 * 1024 * 1024 - 1) As Byte
        For index = 0 To bytes.Length - 1
            bytes(index) = CByte(index Mod 251)
        Next
        Dim path = IO.Path.GetTempFileName()
        Try
            File.WriteAllBytes(path, bytes)
            Dim capture As New CaptureTransport(bytes)
            Using http As New HttpClient(capture), replacement As New RestClient(http, New RestClientOptions("https://fixture.invalid/"))
                Dim client As New DocumentsRestClient(New Authorization(), New Configuration(), Nothing, Nothing, "")
                client.DisableOfflineModeSimulation()
                Dim field = GetType(CenterDeviceRestClient).GetField("client", BindingFlags.Instance Or BindingFlags.NonPublic)
                CType(field.GetValue(client), RestClient).Dispose()
                field.SetValue(client, replacement)
                If fromFile Then
                    client.UploadDocument("user", "fixture.bin", path, "collection", "folder", CancellationToken.None)
                Else
                    client.UploadDocument("user", "fixture.bin", Function() New MemoryStream(bytes, False), "collection", "folder", CancellationToken.None)
                End If
                Assert.That(capture.Calls, [Is].EqualTo(1), "A failed upload must not replay its write.")
            End Using
        Finally
            File.Delete(path)
        End Try
    End Sub

    Private Class CaptureTransport
        Inherits HttpMessageHandler
        Private ReadOnly Expected As Byte()
        Friend Calls As Integer

        Friend Sub New(expected As Byte())
            Me.Expected = expected
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Calls += 1
            Assert.That(request.RequestUri.AbsolutePath, [Is].EqualTo("/documents"))
            Assert.That(request.Content.Headers.ContentType.MediaType, [Is].EqualTo("multipart/form-data"))
            Dim parts = CType(request.Content, MultipartFormDataContent).ToArray()
            Assert.That(parts.Select(Function(part) part.Headers.ContentDisposition.Name.Trim(""""c)), [Is].EquivalentTo(New String() {"metadata", "document"}))
            Dim metadata = parts.Single(Function(part) part.Headers.ContentDisposition.Name.Trim(""""c) = "metadata")
            Assert.That(metadata.Headers.ContentType.MediaType, [Is].EqualTo("application/json"))
            Dim json = JObject.Parse(Await metadata.ReadAsStringAsync())
            Assert.That(CLng(json("metadata")("document")("size")), [Is].EqualTo(CLng(Expected.Length)))
            Assert.That(CStr(json("metadata")("document")("filename")), [Is].EqualTo("fixture.bin"))
            Dim document = parts.Single(Function(part) part.Headers.ContentDisposition.Name.Trim(""""c) = "document")
            Assert.That(document.Headers.ContentType.MediaType, [Is].EqualTo("application/octet-stream"))
            Assert.That(Await document.ReadAsByteArrayAsync(), [Is].EqualTo(Expected))
            'Rewind after observation so serialization tests the whole payload, not an exhausted stream.
            Dim stream = Await document.ReadAsStreamAsync()
            If stream.CanSeek Then stream.Position = 0
            Dim serialized = Await request.Content.ReadAsByteArrayAsync()
            Dim boundary = request.Content.Headers.ContentType.Parameters.Single(Function(parameter) parameter.Name = "boundary").Value.Trim(""""c)
            Dim first = Text.Encoding.ASCII.GetBytes("--" & boundary & vbCrLf)
            Dim last = Text.Encoding.ASCII.GetBytes(vbCrLf & "--" & boundary & "--" & vbCrLf)
            Assert.That(serialized.Take(first.Length), [Is].EqualTo(first))
            Assert.That(serialized.Skip(serialized.Length - last.Length), [Is].EqualTo(last))
            Assert.That(serialized.Length, [Is].GreaterThan(Expected.Length))
            Return New HttpResponseMessage(HttpStatusCode.Created) With {.Content = New StringContent("{""id"":""fixture-document""}", Text.Encoding.UTF8, "application/json")}
        End Function
    End Class

    Private Class Authorization
        Implements IOAuthInfoProvider
        Public Function GetOAuthInfo(userId As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
            Return New OAuthInfo With {.UserId = userId, .access_token = "fixture-token"}
        End Function
    End Class

    Private Class Configuration
        Implements IRestClientConfiguration
        Public ReadOnly Property BaseAddress As String Implements IRestClientConfiguration.BaseAddress
            Get
                Return "https://fixture.invalid/"
            End Get
        End Property
        Public ReadOnly Property UserAgent As String Implements IRestClientConfiguration.UserAgent
            Get
                Return "Isolated multipart contract fixture"
            End Get
        End Property
    End Class
End Class
