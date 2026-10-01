Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
Public Class WebDavMissingResourceTest

    <Test>
    Public Sub ListingDeletedDirectoryReportsItsPathAndHttpStatus()
        Dim provider As WebDavDmsProvider = CreateProviderReturningNotFound()

        Dim failure = Assert.Throws(Of DirectoryNotFoundException)(
            Sub() provider.ListAllRemoteItems("gone", BaseDmsProvider.SearchItemType.Folders))

        ClassicAssert.AreEqual("gone", failure.RemotePath)
        ClassicAssert.AreEqual(404, CType(failure.InnerException, ResponseStatusCodeException).StatusCode)
    End Sub

    <Test>
    Public Sub DownloadingDeletedFileReportsItsPathAndHttpStatus()
        Dim provider As WebDavDmsProvider = CreateProviderReturningNotFound()

        Dim failure = Assert.Throws(Of CompuMaster.Dms.Data.FileNotFoundException)(
            Sub() provider.DownloadFile("gone.txt", "unused.txt", Nothing))

        ClassicAssert.AreEqual("gone.txt", failure.RemotePath)
        ClassicAssert.AreEqual(404, CType(failure.InnerException, ResponseStatusCodeException).StatusCode)
    End Sub

    <Test>
    Public Sub AsyncListingDeletedDirectoryReportsItsPathAndHttpStatus()
        Dim provider As WebDavDmsProvider = CreateProviderReturningNotFound()
        Dim failure = Assert.ThrowsAsync(Of DirectoryNotFoundException)(
            Async Function() Await provider.ListAllRemoteItemsAsync("gone", BaseDmsProvider.SearchItemType.Folders))
        ClassicAssert.AreEqual("gone", failure.RemotePath)
        ClassicAssert.AreEqual(404, CType(failure.InnerException, ResponseStatusCodeException).StatusCode)
    End Sub

    <Test>
    Public Sub AsyncDownloadingDeletedFileReportsItsPathAndHttpStatus()
        Dim provider As WebDavDmsProvider = CreateProviderReturningNotFound()
        Dim failure = Assert.ThrowsAsync(Of CompuMaster.Dms.Data.FileNotFoundException)(
            Async Function() As Task
                Await provider.DownloadFileAsync("gone.txt", "unused.txt", Nothing)
            End Function)
        ClassicAssert.AreEqual("gone.txt", failure.RemotePath)
        ClassicAssert.AreEqual(404, CType(failure.InnerException, ResponseStatusCodeException).StatusCode)
    End Sub

    <Test>
    Public Sub FailedAsyncDownloadPreservesExistingLocalFile()
        Dim testDirectory As String = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "failed-download-" & Guid.NewGuid().ToString("N"))
        If System.IO.Directory.Exists(testDirectory) Then Throw New InvalidOperationException("The test directory already exists.")
        System.IO.Directory.CreateDirectory(testDirectory)
        Try
            Dim target As String = System.IO.Path.Combine(testDirectory, "existing.txt")
            System.IO.File.WriteAllText(target, "original")
            Dim provider As WebDavDmsProvider = CreateProvider(New FailingDownloadHandler())

            Assert.ThrowsAsync(Of System.IO.IOException)(Async Function() As Task
                                                             Await provider.DownloadFileAsync("remote.txt", target, Nothing)
                                                         End Function)
            ClassicAssert.AreEqual("original", System.IO.File.ReadAllText(target))
            Assert.That(System.IO.Directory.GetFiles(testDirectory), Has.Length.EqualTo(1))
        Finally
            System.IO.Directory.Delete(testDirectory, True)
            Assert.That(System.IO.Directory.Exists(testDirectory), [Is].False)
        End Try
    End Sub

    <Test>
    Public Async Function SuccessfulAsyncDownloadReplacesExistingLocalFile() As Task
        Dim testDirectory As String = System.IO.Path.Combine(TestContext.CurrentContext.WorkDirectory, "completed-download-" & Guid.NewGuid().ToString("N"))
        If System.IO.Directory.Exists(testDirectory) Then Throw New InvalidOperationException("The test directory already exists.")
        System.IO.Directory.CreateDirectory(testDirectory)
        Try
            Dim target As String = System.IO.Path.Combine(testDirectory, "existing.txt")
            System.IO.File.WriteAllText(target, "original")
            Dim provider As WebDavDmsProvider = CreateProvider(New StaticDownloadHandler())

            Await provider.DownloadFileAsync("remote.txt", target, Nothing)
            ClassicAssert.AreEqual("updated", System.IO.File.ReadAllText(target))
            Assert.That(System.IO.Directory.GetFiles(testDirectory), Has.Length.EqualTo(1))
        Finally
            System.IO.Directory.Delete(testDirectory, True)
            Assert.That(System.IO.Directory.Exists(testDirectory), [Is].False)
        End Try
    End Function

    Private Shared Function CreateProviderReturningNotFound() As WebDavDmsProvider
        Return CreateProvider(New NotFoundHandler())
    End Function

    Private Shared Function CreateProvider(handler As HttpMessageHandler) As WebDavDmsProvider
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = "https://example.test/"}
        Dim client As New WebDav.WebDavClient(New HttpClient(handler))
        Dim field As FieldInfo = GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic)
        ClassicAssert.IsNotNull(field)
        field.SetValue(provider, client)
        Return provider
    End Function

    Private Class NotFoundHandler
        Inherits HttpMessageHandler

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.NotFound) With {.ReasonPhrase = "Not Found"})
        End Function
    End Class

    Private Class FailingDownloadHandler
        Inherits HttpMessageHandler

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New StreamContent(New FailingStream())})
        End Function
    End Class

    Private Class StaticDownloadHandler
        Inherits HttpMessageHandler

        Protected Overrides Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("updated"))})
        End Function
    End Class

    Private Class FailingStream
        Inherits System.IO.Stream
        Private ReadOnce As Boolean

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
        Public Overrides Sub Flush()
        End Sub
        Public Overrides Function Seek(offset As Long, origin As System.IO.SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Sub Write(buffer As Byte(), offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub
        Public Overrides Function Read(buffer As Byte(), offset As Integer, count As Integer) As Integer
            If ReadOnce Then Throw New System.IO.IOException("Simulated connection failure.")
            ReadOnce = True
            buffer(offset) = 42
            Return 1
        End Function
        Public Overrides Function ReadAsync(buffer As Byte(), offset As Integer, count As Integer, cancellationToken As CancellationToken) As Task(Of Integer)
            Return Task.FromResult(Read(buffer, offset, count))
        End Function
    End Class

End Class
