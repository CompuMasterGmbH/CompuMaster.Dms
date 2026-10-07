Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Xml.Linq
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
Public Class WebDavChildDirectoryMetadataTest

    <TestCase("0", 200, 0)>
    <TestCase("3", 200, 3)>
    <TestCase(Nothing, 200, -1)>
    <TestCase("", 200, -1)>
    <TestCase("invalid", 200, -1)>
    <TestCase("-1", 200, -1)>
    <TestCase("2147483648", 200, -1)>
    <TestCase("0", 404, -1)>
    Public Sub FolderListingsPreserveKnownAndUnknownChildCounts(value As String, statusCode As Integer, expectedCount As Integer)
        Using handler As New MetadataHandler(value, statusCode)
            Using client As New HttpClient(handler)
                Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = "https://example.test/"}
                GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(provider, New WebDav.WebDavClient(client))
                For Each asynchronous As Boolean In New Boolean() {False, True}
                    Dim folder = If(asynchronous, provider.ListAllRemoteItemsAsync("", BaseDmsProvider.SearchItemType.Folders).GetAwaiter().GetResult().Single(), provider.ListAllRemoteItems("", BaseDmsProvider.SearchItemType.Folders).Single())
                    AssertMetadata(folder, expectedCount)
                    ClassicAssert.AreEqual("1", handler.Depth)
                    AssertRequest(handler.RequestBody)

                    folder = If(asynchronous, provider.ListRemoteItemAsync("folder").GetAwaiter().GetResult(), provider.ListRemoteItem("folder"))
                    AssertMetadata(folder, expectedCount)
                    ClassicAssert.AreEqual("0", handler.Depth)
                    AssertRequest(handler.RequestBody)
                Next
                Assert.That(handler.RequestCount, [Is].EqualTo(4), "Metadata must use the listing response without extra per-folder requests.")
            End Using
        End Using
    End Sub

    Private Shared Sub AssertMetadata(folder As CompuMaster.Dms.Data.DmsResourceItem, expectedCount As Integer)
        If expectedCount < 0 Then
            ClassicAssert.IsFalse(folder.ChildDirectoryCount.HasValue)
            ClassicAssert.IsFalse(folder.HasChildDirectories.HasValue)
        Else
            ClassicAssert.AreEqual(expectedCount, folder.ChildDirectoryCount.Value)
            ClassicAssert.AreEqual(expectedCount > 0, folder.HasChildDirectories.Value)
        End If
    End Sub

    Private Shared Sub AssertRequest(body As String)
        Dim document = XDocument.Parse(body)
        ClassicAssert.IsTrue(document.Descendants(XName.Get("prop", "DAV:")).Any())
        ClassicAssert.IsFalse(document.Descendants(XName.Get("allprop", "DAV:")).Any())
        ClassicAssert.IsTrue(document.Descendants(XName.Get("contained-folder-count", "http://nextcloud.org/ns")).Any())
    End Sub

    Private Class MetadataHandler
        Inherits HttpMessageHandler

        Private ReadOnly Value As String
        Private ReadOnly StatusCode As Integer
        Public Property RequestBody As String
        Public Property Depth As String
        Public Property RequestCount As Integer

        Public Sub New(value As String, statusCode As Integer)
            Me.Value = value
            Me.StatusCode = statusCode
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Me.RequestCount += 1
            Me.RequestBody = Await request.Content.ReadAsStringAsync()
            Me.Depth = request.Headers.GetValues("Depth").Single()
            Dim xml = <d:multistatus xmlns:d="DAV:" xmlns:nc="http://nextcloud.org/ns">
                          <d:response>
                              <d:href>/folder/</d:href>
                              <d:propstat>
                                  <d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop>
                                  <d:status>HTTP/1.1 200 OK</d:status>
                              </d:propstat>
                              <d:propstat>
                                  <d:prop><nc:contained-folder-count><%= Me.Value %></nc:contained-folder-count></d:prop>
                                  <d:status><%= "HTTP/1.1 " & Me.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture) & " Status" %></d:status>
                              </d:propstat>
                          </d:response>
                      </d:multistatus>
            If Me.Value Is Nothing Then xml.Descendants(XName.Get("contained-folder-count", "http://nextcloud.org/ns")).Single().Parent.Parent.Remove()
            Return New HttpResponseMessage(CType(207, HttpStatusCode)) With {.Content = New StringContent(xml.ToString(), System.Text.Encoding.UTF8, "application/xml")}
        End Function
    End Class
End Class
