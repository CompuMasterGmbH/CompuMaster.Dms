Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Reflection
Imports System.Text
Imports System.Threading
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class WebDavOwnerMetadataTest

    <TestCase(False)>
    <TestCase(True)>
    Public Sub OwnCloudOwnerIdAndDisplayNameAreReadForFilesAndFolders(isFolder As Boolean)
        Dim handler As New PropfindHandler(OwnerProperties("<oc:owner-id>account-42</oc:owner-id><oc:owner-display-name>Resource Owner</oc:owner-display-name>", isFolder))
        Dim resource = CreateProvider(handler).ListRemoteItem("item")

        Assert.That(resource.ExtendedInfosOwner.ID, [Is].EqualTo("account-42"))
        Assert.That(resource.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Resource Owner"))
        Assert.That(handler.RequestBody, [Does].Contain("owner-id"))
        Assert.That(handler.RequestBody, [Does].Contain("owner-display-name"))
        Assert.That(handler.RequestBody, [Does].Contain("owner"))
    End Sub

    <Test>
    Public Sub OwnerIdWithoutDisplayNameRemainsKnown()
        Dim resource = CreateProvider(New PropfindHandler(OwnerProperties("<oc:owner-id>account-42</oc:owner-id>", False))).ListRemoteItem("item")

        Assert.That(resource.ExtendedInfosOwner.ID, [Is].EqualTo("account-42"))
        Assert.That(resource.ExtendedInfosOwner.DisplayName, [Is].EqualTo("account-42"))
    End Sub

    <Test>
    Public Sub DisplayNameWithoutIdRemainsKnown()
        Dim resource = CreateProvider(New PropfindHandler(OwnerProperties("<oc:owner-display-name>Resource Owner</oc:owner-display-name>", False))).ListRemoteItem("item")

        Assert.That(resource.ExtendedInfosOwner.ID, [Is].Null)
        Assert.That(resource.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Resource Owner"))
    End Sub

    <Test>
    Public Sub DavOwnerPrincipalIsUsedWhenOwnCloudMetadataIsUnavailable()
        Dim resource = CreateProvider(New PropfindHandler(OwnerProperties("<d:owner><d:href>/principals/users/account-42/</d:href></d:owner>", False))).ListRemoteItem("item")

        Assert.That(resource.ExtendedInfosOwner.ID, [Is].EqualTo("/principals/users/account-42/"))
    End Sub

    <Test>
    Public Sub OwnCloudIdTakesPrecedenceOverDavPrincipal()
        Dim properties = "<oc:owner-id>account-42</oc:owner-id><oc:owner-display-name>Resource Owner</oc:owner-display-name><d:owner><d:href>/principals/users/other/</d:href></d:owner>"
        Dim resource = CreateProvider(New PropfindHandler(OwnerProperties(properties, False))).ListRemoteItem("item")

        Assert.That(resource.ExtendedInfosOwner.ID, [Is].EqualTo("account-42"))
        Assert.That(resource.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Resource Owner"))
    End Sub

    <Test>
    Public Sub UnsupportedOwnerPropertiesAndLockOwnerDoNotImplyResourceOwner()
        Dim xml = OwnerProperties("<d:lockdiscovery><d:activelock><d:owner>Lock Creator</d:owner></d:activelock></d:lockdiscovery>", False, "<oc:owner-id/><d:owner/>", "<oc:owner-display-name/>")
        Dim resource = CreateProvider(New PropfindHandler(xml)).ListRemoteItem("item")

        Assert.That(resource.ExtendedInfosOwner.ID, [Is].Null)
        Assert.That(resource.ExtendedInfosOwner.DisplayName, [Is].Null)
    End Sub

    <Test>
    Public Sub ListingChildrenReadsOwnerProperties()
        Dim handler As New PropfindHandler(OwnerProperties("<oc:owner-id>account-42</oc:owner-id>", False))
        Dim resources = CreateProvider(handler).ListAllRemoteItems("", BaseDmsProvider.SearchItemType.AllItems)

        Assert.That(resources, Has.Count.EqualTo(1))
        Assert.That(resources(0).ExtendedInfosOwner.ID, [Is].EqualTo("account-42"))
        Assert.That(handler.RequestBody, [Does].Contain("owner-id"))
    End Sub

    <Test>
    Public Sub ServerIgnoringAllpropIncludeStillReturnsOwnerViaNamedProperties()
        Dim ownerResponse = OwnerProperties("<oc:owner-id>account-42</oc:owner-id><oc:owner-display-name>Resource Owner</oc:owner-display-name>", False)
        Dim handler As New PropfindHandler(ownerResponse, False, True)
        Dim resource = CreateProvider(handler).ListRemoteItem("item")

        Assert.That(handler.RequestBody, [Does].Not.Contain("allprop"))
        Assert.That(handler.RequestBody, [Does].Contain("resourcetype"))
        Assert.That(resource.ExtendedInfosOwner.ID, [Is].EqualTo("account-42"))
        Assert.That(resource.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Resource Owner"))
    End Sub

    <Test>
    Public Sub RootDisplayNameIsClearedWhenServerReturnsRelativeHref()
        Dim response = OwnerProperties("", True).
            Replace("https://example.test/item/", "/remote.php/dav/files/account/").
            Replace("<d:displayname>item</d:displayname>", "<d:displayname>Account Display</d:displayname>")
        Dim provider = CreateProvider(New PropfindHandler(response), "https://example.test/remote.php/dav/files/account/")

        Dim resource = provider.ListRemoteItem("")

        Assert.That(resource.ItemType, [Is].EqualTo(CompuMaster.Dms.Data.DmsResourceItem.ItemTypes.Root))
        Assert.That(resource.Name, [Is].Empty)
    End Sub

    <Test>
    Public Sub ServerRejectingNamedPropertiesFallsBackToExistingPropfind()
        Dim handler As New PropfindHandler(OwnerProperties("", False), True)
        Dim resource = CreateProvider(handler).ListRemoteItem("item")

        Assert.That(handler.RequestCount, [Is].EqualTo(2))
        Assert.That(resource.ExtendedInfosOwner.ID, [Is].Null)
        Assert.That(resource.ExtendedInfosOwner.DisplayName, [Is].Null)
    End Sub

    Private Shared Function OwnerProperties(successfulProperties As String, isFolder As Boolean, Optional missingProperties As String = Nothing, Optional forbiddenProperties As String = Nothing) As String
        Dim resourceType = If(isFolder, "<d:resourcetype><d:collection/></d:resourcetype>", "<d:resourcetype/>")
        Dim missing = If(missingProperties Is Nothing, String.Empty, "<d:propstat><d:prop>" & missingProperties & "</d:prop><d:status>HTTP/1.1 404 Not Found</d:status></d:propstat>")
        Dim forbidden = If(forbiddenProperties Is Nothing, String.Empty, "<d:propstat><d:prop>" & forbiddenProperties & "</d:prop><d:status>HTTP/1.1 403 Forbidden</d:status></d:propstat>")
        Return "<d:multistatus xmlns:d='DAV:' xmlns:oc='http://owncloud.org/ns'><d:response><d:href>https://example.test/item" & If(isFolder, "/", "") & "</d:href><d:propstat><d:prop><d:displayname>item</d:displayname>" & resourceType & successfulProperties & "</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>" & missing & forbidden & "</d:response></d:multistatus>"
    End Function

    Private Shared Function CreateProvider(handler As HttpMessageHandler, Optional apiUrl As String = "https://example.test/") As WebDavDmsProvider
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = apiUrl}
        Dim field As FieldInfo = GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic)
        Assert.That(field, [Is].Not.Null)
        field.SetValue(provider, New WebDav.WebDavClient(New HttpClient(handler)))
        Return provider
    End Function

    Private Class PropfindHandler
        Inherits HttpMessageHandler

        Private ReadOnly ResponseXml As String
        Private ReadOnly RejectNamedProperties As Boolean
        Private ReadOnly IgnoreIncludedProperties As Boolean
        Public Property RequestBody As String
        Public Property RequestCount As Integer

        Public Sub New(responseXml As String, Optional rejectNamedProperties As Boolean = False, Optional ignoreIncludedProperties As Boolean = False)
            Me.ResponseXml = responseXml
            Me.RejectNamedProperties = rejectNamedProperties
            Me.IgnoreIncludedProperties = ignoreIncludedProperties
        End Sub

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Me.RequestBody = Await request.Content.ReadAsStringAsync()
            Me.RequestCount += 1
            If Me.RejectNamedProperties AndAlso Me.RequestCount = 1 Then
                Return New HttpResponseMessage(HttpStatusCode.BadRequest) With {.Content = New StringContent(String.Empty)}
            End If
            If Me.IgnoreIncludedProperties AndAlso Me.RequestBody.Contains("allprop") Then
                Return New HttpResponseMessage(CType(207, HttpStatusCode)) With {.Content = New StringContent(OwnerProperties("", False), Encoding.UTF8, "application/xml")}
            End If
            Return New HttpResponseMessage(CType(207, HttpStatusCode)) With {.Content = New StringContent(Me.ResponseXml, Encoding.UTF8, "application/xml")}
        End Function
    End Class

End Class
