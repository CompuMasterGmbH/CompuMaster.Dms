Option Explicit On
Option Strict On

Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class LocalFileSystemContractTest
    Private OwnedRoot As String

    <SetUp>
    Public Sub PrepareOwnedLocalDirectory()
        OwnedRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "filesystem-contract-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(OwnedRoot) OrElse File.Exists(OwnedRoot), [Is].False, "The owned fixture must be absent before creation.")
        Directory.CreateDirectory(OwnedRoot)
    End Sub

    <TearDown>
    Public Sub RemoveOwnedLocalDirectory()
        If Directory.Exists(OwnedRoot) Then Directory.Delete(OwnedRoot, True)
        Assert.That(Directory.Exists(OwnedRoot) OrElse File.Exists(OwnedRoot), [Is].False, "The owned fixture must be removed.")
    End Sub

    <TestCase(False, False), TestCase(False, True), TestCase(True, False), TestCase(True, True)>
    Public Sub DirectoryAtDownloadDestinationReportsAFileSystemConflict(asynchronous As Boolean, processed As Boolean)
        Dim target = Path.Combine(OwnedRoot, "destination")
        Directory.CreateDirectory(target)
        Using handler As New SuccessfulDownloadHandler(), client As New HttpClient(handler)
            Dim provider = CreateProvider(client)
            Dim failure As Exception = Assert.Catch(Of Exception)(
                Sub()
                    If asynchronous Then
                        If processed Then
                            provider.DownloadProcessedFileAsync("file", target).GetAwaiter().GetResult()
                        Else
                            provider.DownloadFileAsync("file", target, Nothing).GetAwaiter().GetResult()
                        End If
                    ElseIf processed Then
                        provider.DownloadProcessedFile("file", target)
                    Else
                        provider.DownloadFile("file", target, Nothing)
                    End If
                End Sub)
            Assert.That(TypeOf failure Is IOException OrElse TypeOf failure Is UnauthorizedAccessException, [Is].True, failure.ToString())
            TestContext.WriteLine("Directory-at-file-target: " & Environment.OSVersion.Platform.ToString() & ", async=" & asynchronous.ToString() & ", processed=" & processed.ToString() & ": " & failure.GetType().FullName)
            CapabilityEvidence.Record("filesystem-" & asynchronous.ToString() & "-" & processed.ToString(), New With {
                .Platform = Environment.OSVersion.Platform.ToString(), .Framework = AppContext.TargetFrameworkName,
                .Asynchronous = asynchronous, .Processed = processed, .ExceptionType = failure.GetType().FullName})
            Assert.That(Directory.Exists(target), [Is].True)
            Assert.That(Directory.GetFiles(OwnedRoot), [Is].Empty, "Failed finalization must remove the temporary download.")
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub MissingLocalDownloadParentIsReportedWithoutCreatingIt(asynchronous As Boolean)
        Dim target = Path.Combine(OwnedRoot, "missing", "file")
        Using handler As New SuccessfulDownloadHandler(), client As New HttpClient(handler)
            Dim provider = CreateProvider(client)
            Assert.Catch(Of System.IO.DirectoryNotFoundException)(
                Sub()
                    If asynchronous Then
                        provider.DownloadFileAsync("file", target, Nothing).GetAwaiter().GetResult()
                    Else
                        provider.DownloadFile("file", target, Nothing)
                    End If
                End Sub)
            Assert.That(Directory.Exists(Path.GetDirectoryName(target)), [Is].False)
        End Using
    End Sub

    Private Shared Function CreateProvider(client As HttpClient) As WebDavDmsProvider
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = "https://example.test/"}
        GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(provider, New WebDav.WebDavClient(client))
        Return provider
    End Function

    Private Class SuccessfulDownloadHandler
        Inherits HttpMessageHandler

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(New Byte() {1, 2, 3})})
        End Function
    End Class
End Class
