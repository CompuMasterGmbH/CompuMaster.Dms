Option Explicit On
Option Strict On

Imports System.Globalization
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable>
Public Class WebDavDownloadFailureContractTest
    <TestCase("en", "Download failed"), TestCase("de", "Download fehlgeschlagen")>
    Public Sub FailedHttpResponsesMatchAcrossAllDownloadPaths(culture As String, expected As String)
        Dim originalCulture = CultureInfo.CurrentUICulture
        Dim root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "download-errors-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root) OrElse File.Exists(root), [Is].False)
        Directory.CreateDirectory(root)
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            For Each status In New Integer() {400, 401, 403, 404, 409, 412, 429, 500, 503}
                Using handler As New StatusHandler(status), client As New HttpClient(handler)
                    Dim provider = CreateProvider(client)
                    For Each operation In New String() {"raw-sync", "processed-sync", "raw-async", "processed-async", "progress-async"}
                        Dim target = Path.Combine(root, "existing.txt")
                        File.WriteAllText(target, "original")
                        Dim failure = Assert.Catch(Of Exception)(Sub() Download(provider, operation, target))
                        If status = 404 Then
                            Assert.That(failure, [Is].TypeOf(Of CompuMaster.Dms.Data.FileNotFoundException)(), operation)
                            Assert.That(CType(failure, CompuMaster.Dms.Data.FileNotFoundException).RemotePath, [Is].EqualTo("remote.txt"))
                        Else
                            Assert.That(failure, [Is].TypeOf(Of IOException)(), operation & "/" & status.ToString())
                            Assert.That(failure.Message, [Is].EqualTo(expected))
                        End If
                        Dim httpFailure = TryCast(failure.InnerException, ResponseStatusCodeException)
                        Assert.That(httpFailure, [Is].Not.Null)
                        Assert.That(httpFailure.StatusCode, [Is].EqualTo(status))
                        Assert.That(File.ReadAllText(target), [Is].EqualTo("original"))
                        Assert.That(Directory.GetFiles(root), Has.Length.EqualTo(1), "No staging file may survive failure.")
                    Next
                End Using
            Next
        Finally
            CultureInfo.CurrentUICulture = originalCulture
            Directory.Delete(root, True)
            Assert.That(Directory.Exists(root), [Is].False)
        End Try
    End Sub

    Private Shared Sub Download(provider As WebDavDmsProvider, operation As String, target As String)
        Select Case operation
            Case "raw-sync" : provider.DownloadFile("remote.txt", target, Nothing)
            Case "processed-sync" : provider.DownloadProcessedFile("remote.txt", target)
            Case "raw-async" : provider.DownloadFileAsync("remote.txt", target, Nothing).GetAwaiter().GetResult()
            Case "processed-async" : provider.DownloadProcessedFileAsync("remote.txt", target).GetAwaiter().GetResult()
            Case Else : provider.DownloadFileWithProgressAsync(New DmsResourceItem With {.FullName = "remote.txt", .ItemType = DmsResourceItem.ItemTypes.File}, target, Nothing).GetAwaiter().GetResult()
        End Select
    End Sub

    Private Shared Function CreateProvider(client As HttpClient) As WebDavDmsProvider
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = "https://example.test/"}
        GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(provider, New WebDav.WebDavClient(client))
        Return provider
    End Function

    Private Class StatusHandler
        Inherits HttpMessageHandler
        Private ReadOnly Status As Integer
        Public Sub New(value As Integer)
            Status = value
        End Sub
        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(New HttpResponseMessage(CType(Status, HttpStatusCode)) With {.ReasonPhrase = "fixture diagnostic", .Content = New StringContent("")})
        End Function
    End Class
End Class
