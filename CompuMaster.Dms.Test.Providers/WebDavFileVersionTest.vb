#If OCS_FILE_VERSIONS Then
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class WebDavFileVersionTest
    Private Const BaseUrl As String = "https://fixture.test:8443/prefix/remote.php/dav/files/protocol%20id/Folder/"
    Private Shared Function Capabilities(enabled As String, deletion As String) As String
        Return "{""ocs"":{""meta"":{""statuscode"":100},""data"":{""capabilities"":{""files"":{""versioning"":" & enabled & ",""version_deletion"":" & deletion & "}}}}}"
    End Function
    Private Shared Function SelectedFile() As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "report+ one.txt", .FullName = "report+ one.txt", .ExtendedInfosFileID = "stable-file"}
    End Function

    <TestCase(0, "true", "true", False, False)>
    <TestCase(1, "true", "true", True, True)>
    <TestCase(1, "true", "false", True, False)>
    <TestCase(1, "true", "null", True, False)>
    <TestCase(1, "false", "true", False, False)>
    <TestCase(1, "null", "true", False, False)>
    <TestCase(2, "true", "true", True, False)>
    <TestCase(3, "true", "null", True, False)>
    Public Sub EnabledCapabilitiesAndImplementedProtocolDecideActions(family As Integer, enabled As String, deletion As String, history As Boolean, remove As Boolean)
        Using wire As New Wire(), http As New HttpClient(wire)
            Dim provider As New WebDavDmsProvider()
            provider.ConfigureFileVersions(New Global.WebDav.WebDavClient(http), BaseUrl, CType(family, OcsServerFamily), Capabilities(enabled, deletion))
            Assert.That(provider.SupportsFileVersionHistory, [Is].EqualTo(history))
            Assert.That(provider.SupportsFileVersionDownload, [Is].EqualTo(history))
            Assert.That(provider.SupportsFileVersionRestore, [Is].EqualTo(history))
            Assert.That(provider.SupportsFileVersionRemoval, [Is].EqualTo(remove))
            Assert.That(wire.Requests, [Is].Empty, "Capabilities do not create test resources.")
        End Using
    End Sub

    <TestCase(1, "MOVE"), TestCase(2, "COPY"), TestCase(3, "COPY")>
    Public Sub ProviderPreservesIdentityEncodingHistoricalBytesAndRestoreMapping(family As Integer, method As String)
        Using wire As New Wire With {.Family = family}, http As New HttpClient(wire), output As New MemoryStream()
            Dim provider As New WebDavDmsProvider()
            provider.ConfigureFileVersions(New Global.WebDav.WebDavClient(http), BaseUrl, CType(family, OcsServerFamily), Capabilities("true", "true"))
            Dim selected = SelectedFile()
            Dim version = provider.ListFileVersions(selected).Single()
            selected.FullName = "another-file"
            Assert.That(version.FileID, [Is].EqualTo("stable-file"))
            Assert.That(version.FileFullName, [Is].EqualTo("report+ one.txt"))
            provider.DownloadFileVersion(version, output)
            Assert.That(output.ToArray(), [Is].EqualTo(New Byte() {3, 1, 7}))
            Assert.That(output.CanWrite, [Is].True)
            provider.RestoreFileVersion(version)
            Assert.That(wire.Requests.Last(), Does.StartWith(method & " "))
            Assert.That(wire.LastDestination, [Is].EqualTo(If(family = 1, "https://fixture.test:8443/prefix/remote.php/dav/versions/protocol%20id/restore/target", BaseUrl & "report%2B%20one.txt")))
            If family = 1 Then
                provider.RemoveFileVersion(version)
                Assert.That(wire.Requests.Last(), Does.StartWith("DELETE "))
            Else
                Assert.Throws(Of NotSupportedException)(Sub() provider.RemoveFileVersion(version))
            End If
        End Using
    End Sub

    <Test>
    Public Sub StaleIdentityPermissionErrorsAndAccountChangesRemainFailures()
        Using wire As New Wire(), http As New HttpClient(wire)
            Dim provider As New WebDavDmsProvider()
            Dim transport As New Global.WebDav.WebDavClient(http)
            provider.ConfigureFileVersions(transport, BaseUrl, OcsServerFamily.Nextcloud, Capabilities("true", "true"))
            Dim version = provider.ListFileVersions(SelectedFile()).Single()
            wire.FileID = "replacement"
            Dim stale = Assert.Throws(Of FileActionFailedException)(Sub() provider.RestoreFileVersion(version))
            Assert.That(stale.InnerException, [Is].TypeOf(Of InvalidOperationException)())
            wire.FileID = "stable-file"
            wire.Status = 403
            Dim denied = Assert.Throws(Of FileActionFailedException)(Sub() provider.ListFileVersions(SelectedFile()))
            Assert.That(DirectCast(denied.InnerException, CompuMaster.Ocs.Exceptions.ResponseException).HttpStatusCode, [Is].EqualTo(HttpStatusCode.Forbidden))
            Assert.That(denied.RemotePathSource, [Is].EqualTo("report+ one.txt"))
            wire.Status = 207
            provider.ConfigureFileVersions(transport, BaseUrl, OcsServerFamily.Nextcloud, Capabilities("true", "true"))
            Dim foreign = Assert.Throws(Of FileActionFailedException)(Sub() provider.RestoreFileVersion(version))
            Assert.That(foreign.InnerException, [Is].TypeOf(Of ArgumentException)())
            Assert.That(wire.Requests.Any(Function(request) request.StartsWith("MOVE ")), [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub FailedAccountAuthorizationCannotRetainPreviousVersionSession()
        Using wire As New Wire(), http As New HttpClient(wire)
            Dim provider As New WebDavDmsProvider()
            provider.ConfigureFileVersions(New Global.WebDav.WebDavClient(http), BaseUrl, OcsServerFamily.Nextcloud, Capabilities("true", "true"))
            Dim version = provider.ListFileVersions(SelectedFile()).Single()
            Assert.Catch(Sub() provider.Authorize(New WebDavLoginCredentials With {.BaseUrl = "invalid-absolute-url", .Username = "fixture", .Password = "fixture"}))
            Assert.That(provider.SupportsFileVersionHistory, [Is].False)
            Assert.That(provider.SupportsFileVersionRestore, [Is].False)
            Assert.Throws(Of NotSupportedException)(Sub() provider.RestoreFileVersion(version))
            Assert.That(wire.Requests.Any(Function(request) request.StartsWith("MOVE ")), [Is].False)
        End Using
    End Sub

    Private Class Wire
        Inherits HttpMessageHandler
        Friend Family As Integer = 1
        Friend FileID As String = "stable-file"
        Friend Status As Integer = 207
        Friend LastDestination As String
        Friend ReadOnly Requests As New List(Of String)
        Protected Overrides Function SendAsync(request As HttpRequestMessage, token As CancellationToken) As Task(Of HttpResponseMessage)
            Requests.Add(request.Method.Method & " " & request.RequestUri.AbsoluteUri)
            If request.Headers.Contains("Destination") Then LastDestination = request.Headers.GetValues("Destination").Single()
            If request.Method = HttpMethod.Get Then Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.OK) With {.Content = New ByteArrayContent(New Byte() {3, 1, 7})})
            If request.Method.Method <> "PROPFIND" Then Return Task.FromResult(New HttpResponseMessage(HttpStatusCode.NoContent))
            If Status <> 207 Then Return Task.FromResult(New HttpResponseMessage(CType(Status, HttpStatusCode)) With {.Content = New StringContent("DAV failure")})
            Dim file = request.RequestUri.AbsoluteUri.StartsWith(BaseUrl, StringComparison.Ordinal)
            Dim path As String = request.RequestUri.AbsolutePath
            Dim props = If(file, "<oc:fileid>" & FileID & "</oc:fileid><d:getetag>&quot;current&quot;</d:getetag><d:resourcetype/>", "<d:resourcetype><d:collection/></d:resourcetype>")
            Dim xml = "<d:multistatus xmlns:d=""DAV:"" xmlns:oc=""http://owncloud.org/ns""><d:response><d:href>" & path & "</d:href><d:propstat><d:prop>" & props & "</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>"
            If Not file Then xml &= "<d:response><d:href>" & path & "opaque-revision</d:href><d:propstat><d:prop><d:resourcetype/><d:getcontentlength>3</d:getcontentlength></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>"
            Return Task.FromResult(New HttpResponseMessage(CType(207, HttpStatusCode)) With {.Content = New StringContent(xml & "</d:multistatus>")})
        End Function
    End Class
End Class
#End If
