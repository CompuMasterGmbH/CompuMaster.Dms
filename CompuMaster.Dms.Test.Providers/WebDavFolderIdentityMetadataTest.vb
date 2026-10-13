Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Xml.Linq
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class WebDavFolderIdentityMetadataTest
    <TestCase(False), TestCase(True)>
    Public Sub SuccessfulFolderIdUsesExistingListingResponse(asynchronous As Boolean)
        Using handler As New MetadataHandler("<oc:fileid>17</oc:fileid>", 200)
            Using client As New HttpClient(handler)
                Dim provider = CreateProvider(client)
                Dim item = Read(provider, asynchronous, False)
                Assert.That(item.ExtendedInfosFolderID, [Is].EqualTo("17"))
                Assert.That(item.ExtendedInfosFileID, [Is].Null)
                Assert.That(handler.Requests, [Is].EqualTo(1))
                Assert.That(XDocument.Parse(handler.RequestBody).Descendants(XName.Get("fileid", "http://owncloud.org/ns")).Count(), [Is].EqualTo(1))
                Assert.That(handler.Depth, [Is].EqualTo("1"))
                item = Read(provider, asynchronous, True)
                Assert.That(item.ExtendedInfosFolderID, [Is].EqualTo("17"))
                Assert.That(handler.Requests, [Is].EqualTo(2))
                Assert.That(handler.Depth, [Is].EqualTo("0"))
            End Using
        End Using
    End Sub

    <TestCase("", 200), TestCase("<oc:fileid></oc:fileid>", 200), TestCase("<oc:fileid> </oc:fileid>", 200)>
    <TestCase("<oc:fileid>17</oc:fileid>", 404), TestCase("<oc:fileid>17</oc:fileid>", 403)>
    <TestCase("<oc:fileid><oc:value>17</oc:value></oc:fileid>", 200)>
    <TestCase("<oc:fileid>17</oc:fileid><oc:fileid>18</oc:fileid>", 200)>
    Public Sub UnavailableOrAmbiguousFolderIdentityStaysUnknown(properties As String, status As Integer)
        Using handler As New MetadataHandler(properties, status), client As New HttpClient(handler)
            Dim item = Read(CreateProvider(client), True, True)
            Assert.That(item.ExtendedInfosFolderID, [Is].Null)
            Assert.That(item.ExtendedInfosFileID, [Is].Null)
            Assert.That(handler.Requests, [Is].EqualTo(1))
        End Using
    End Sub

    <Test>
    Public Sub RenameAndEtagChangesDoNotInventANewFolderIdentity()
        Using handler As New MetadataHandler("<oc:fileid>17</oc:fileid>", 200), client As New HttpClient(handler)
            Dim provider = CreateProvider(client)
            Dim before = provider.ListRemoteItem("item")
            handler.ResourcePath = "renamed"
            handler.ETag = "new-content-version"
            Dim after = provider.ListRemoteItemAsync("renamed").GetAwaiter().GetResult()
            Assert.That(before.ExtendedInfosFolderID, [Is].EqualTo("17"))
            Assert.That(after.ExtendedInfosFolderID, [Is].EqualTo(before.ExtendedInfosFolderID))
            Assert.That(after.FullName, [Is].Not.EqualTo(before.FullName))
            Assert.That(after.ProviderSpecificHashOrETag, [Is].Not.EqualTo(before.ProviderSpecificHashOrETag))
            Assert.That(handler.Requests, [Is].EqualTo(2))
        End Using
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub ExistingFileAndRootMappingRemainCompatible(root As Boolean)
        Using handler As New MetadataHandler("<oc:fileid>17</oc:fileid>", 200), client As New HttpClient(handler)
            handler.IsFolder = root
            handler.ResourcePath = If(root, "", "item")
            Dim item = CreateProvider(client).ListRemoteItem(handler.ResourcePath)
            If root Then
                Assert.That(item.ExtendedInfosFileID, [Is].Null)
            Else
                Assert.That(item.ExtendedInfosFileID, [Is].EqualTo("17"))
            End If
            Assert.That(item.ExtendedInfosFolderID, [Is].Null)
            Assert.That(handler.Requests, [Is].EqualTo(1))
        End Using
    End Sub

    Private Shared Function Read(provider As WebDavDmsProvider, asynchronous As Boolean, direct As Boolean) As Data.DmsResourceItem
        If direct Then Return If(asynchronous, provider.ListRemoteItemAsync("item").GetAwaiter().GetResult(), provider.ListRemoteItem("item"))
        Return If(asynchronous, provider.ListAllRemoteItemsAsync("", BaseDmsProvider.SearchItemType.Folders).GetAwaiter().GetResult().Single(), provider.ListAllRemoteItems("", BaseDmsProvider.SearchItemType.Folders).Single())
    End Function

    Friend Shared Function CreateProvider(client As HttpClient) As WebDavDmsProvider
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = "https://fixture.invalid/"}
        GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(provider, New WebDav.WebDavClient(client))
        Return provider
    End Function

    Friend Class MetadataHandler
        Inherits HttpMessageHandler
        Friend Properties As String
        Private ReadOnly Status As Integer
        Friend ResourcePath As String = "item"
        Friend IsFolder As Boolean = True
        Friend ETag As String = "initial-version"
        Friend Requests As Integer
        Friend RequestBody As String
        Friend Depth As String
        Friend Sub New(properties As String, status As Integer)
            Me.Properties = properties
            Me.Status = status
        End Sub
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Requests += 1
            Assert.That(request.Method.Method, [Is].EqualTo("PROPFIND"))
            RequestBody = Await request.Content.ReadAsStringAsync()
            Depth = request.Headers.GetValues("Depth").Single()
            Dim body = "<d:multistatus xmlns:d=""DAV:"" xmlns:oc=""http://owncloud.org/ns""><d:response><d:href>/" & ResourcePath & If(IsFolder, "/", "") & "</d:href>" &
                "<d:propstat><d:prop><d:resourcetype>" & If(IsFolder, "<d:collection/>", "") & "</d:resourcetype><d:getetag>" & ETag & "</d:getetag></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>" &
                "<d:propstat><d:prop>" & Properties & "</d:prop><d:status>HTTP/1.1 " & Status.ToString(Globalization.CultureInfo.InvariantCulture) & " Status</d:status></d:propstat></d:response></d:multistatus>"
            Return New HttpResponseMessage(CType(207, HttpStatusCode)) With {.Content = New StringContent(body, Text.Encoding.UTF8, "application/xml")}
        End Function
    End Class
End Class
