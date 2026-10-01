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

    Private Shared Function CreateProviderReturningNotFound() As WebDavDmsProvider
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = "https://example.test/"}
        Dim client As New WebDav.WebDavClient(New HttpClient(New NotFoundHandler()))
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

End Class
