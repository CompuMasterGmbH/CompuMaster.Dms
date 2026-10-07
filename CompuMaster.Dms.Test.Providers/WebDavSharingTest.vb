Option Explicit On
Option Strict On

Imports System.Linq
Imports System.Reflection
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Xml.Linq
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Ocs
Imports CompuMaster.Ocs.Core
Imports CompuMaster.Ocs.Types
Imports NUnit.Framework

<TestFixture>
Public Class WebDavSharingTest

    <TestCase(False, 0), TestCase(True, 0), TestCase(False, 400), TestCase(True, 400), TestCase(False, 501), TestCase(True, 501)>
    Public Async Function AsyncListingsRetainSyncOwnerAndNestedSharingMetadata(children As Boolean, rejectStatus As Integer) As Task
        Dim client = ListingClient()
        Dim handler As New ListingHandler(children, rejectStatus)
        Dim provider = ListingProvider(client, handler)
        Dim expected As List(Of DmsResourceItem)
        Dim actual As List(Of DmsResourceItem)
        If children Then
            expected = provider.ListAllRemoteItems("Nested", BaseDmsProvider.SearchItemType.AllItems)
            actual = Await provider.ListAllRemoteItemsAsync("Nested", BaseDmsProvider.SearchItemType.AllItems)
        Else
            expected = New List(Of DmsResourceItem) From {provider.ListRemoteItem("Nested/report.txt")}
            actual = New List(Of DmsResourceItem) From {Await provider.ListRemoteItemAsync("Nested/report.txt")}
        End If
        Assert.That(actual.Select(Function(item) item.FullName), [Is].EqualTo(expected.Select(Function(item) item.FullName)))
        Dim file = actual.Single(Function(item) item.ItemType = DmsResourceItem.ItemTypes.File)
        Dim reference = expected.Single(Function(item) item.ItemType = DmsResourceItem.ItemTypes.File)
        Assert.That(file.ExtendedInfosOwner.ID, [Is].EqualTo(reference.ExtendedInfosOwner.ID))
        Assert.That(file.ExtendedInfosOwner.DisplayName, [Is].EqualTo(reference.ExtendedInfosOwner.DisplayName))
        Assert.That(file.ExtendedInfosLinks.Select(Function(link) link.ID), [Is].EqualTo(New String() {"42"}))
        Assert.That(file.ExtendedInfosUserSharings.Single().User.ID, [Is].EqualTo("recipient"))
        Assert.That(file.ExtendedInfosGroupSharings.Single().Group.ID, [Is].EqualTo("reviewers"))
        Assert.That(file.ExtendedInfosIsShared, [Is].True)
        Assert.That(client.LastPath, [Is].EqualTo(If(children, "/Projects/Nested", "/Projects/Nested/report.txt")))
        Assert.That(client.LastIncludeSubFiles, [Is].EqualTo(children))
        Assert.That(client.GetSharesCallCount, [Is].EqualTo(2))
        Assert.That(handler.RequestCount, [Is].EqualTo(If(rejectStatus = 0, 2, 4)))
        Assert.That(handler.Bodies(If(rejectStatus = 0, 1, 2)), Does.Contain("owner-id"))
        If rejectStatus = 0 Then Assert.That(file.ExtendedInfosOwner.ID, [Is].EqualTo("dav-owner"))
    End Function

    <TestCase(BaseDmsProvider.SearchItemType.Files, 1), TestCase(BaseDmsProvider.SearchItemType.Folders, 1), TestCase(BaseDmsProvider.SearchItemType.Collections, 0)>
    Public Async Function AsyncChildFiltersPreserveTheSharingSnapshot(searchType As BaseDmsProvider.SearchItemType, expectedCount As Integer) As Task
        Dim client = ListingClient()
        Dim provider = ListingProvider(client, New ListingHandler(True, 0))
        Dim items = Await provider.ListAllRemoteItemsAsync("Nested", searchType)
        Assert.That(items.Count, [Is].EqualTo(expectedCount))
        If items.Any(Function(item) item.ItemType = DmsResourceItem.ItemTypes.File) Then Assert.That(items.Single().ExtendedInfosHasLinks, [Is].True)
        Assert.That(client.GetSharesCallCount, [Is].EqualTo(1))
    End Function

    <Test>
    Public Async Function SynchronousOcsMetadataRunsOffTheCallingThreadAndQueuedCancellationDoesNotDispatch() As Task
        Dim owner = ListingClient()
        Dim queued = ListingClient()
        Dim entered As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Using release As New ManualResetEventSlim(False), cancellation As New CancellationTokenSource()
            owner.BeforeGetShares = Sub()
                                        entered.TrySetResult(True)
                                        If Not release.Wait(TimeSpan.FromSeconds(5)) Then Throw New TimeoutException("Isolated OCS fixture was not released.")
                                    End Sub
            Dim ownerProvider = ListingProvider(owner, New ListingHandler(False, 0))
            Dim queuedProvider = ListingProvider(queued, New ListingHandler(False, 0))
            Dim active As Task(Of DmsResourceItem) = Nothing
            Try
                active = ownerProvider.ListRemoteItemAsync("Nested/report.txt")
                Assert.That(active.IsCompleted, [Is].False, "The synchronous OCS fixture must not block the caller until completion.")
                Assert.That(Await Task.WhenAny(entered.Task, Task.Delay(3000)), [Is].SameAs(entered.Task))
                Dim waiting = queuedProvider.ListRemoteItemAsync("Nested/report.txt", cancellation.Token)
                cancellation.Cancel()
                Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                           Await waiting
                                                                       End Function, Func(Of Task)))
                Assert.That(queued.GetSharesCallCount, [Is].Zero)
            Finally
                release.Set()
                If active IsNot Nothing Then active.GetAwaiter().GetResult()
            End Try
        End Using
    End Function

    <Test>
    Public Sub CancellationAfterActiveOcsMetadataDoesNotPublishItsSnapshot()
        Dim client = ListingClient()
        Using cancellation As New CancellationTokenSource()
            client.BeforeGetShares = Sub() cancellation.Cancel()
            Dim provider = ListingProvider(client, New ListingHandler(False, 0))
            Assert.CatchAsync(Of OperationCanceledException)(CType(Async Function()
                                                                       Await provider.ListRemoteItemAsync("Nested/report.txt", cancellation.Token)
                                                                   End Function, Func(Of Task)))
            Assert.That(client.GetSharesCallCount, [Is].EqualTo(1))
        End Using
    End Sub

    Private Shared Function ListingClient() As FakeOcsSharingClient
        Return New FakeOcsSharingClient(New OcsSharingCapabilities(OcsServerFamily.Nextcloud, True, True, True, True, True), New List(Of Share)) With {
            .Records = OcsSharingClientAdapter.ParseShareRecords("{'ocs':{'meta':{'statuscode':100},'data':[" &
                "{'id':42,'share_type':3,'permissions':1,'path':'/Projects/Nested/report.txt','file_target':'/report.txt','url':'https://fixture.invalid/share','uid_owner':'ocs-owner'}," &
                "{'id':43,'share_type':0,'permissions':1,'path':'/Projects/Nested/report.txt','file_target':'/report.txt','share_with':'recipient'}," &
                "{'id':44,'share_type':1,'permissions':1,'path':'/Projects/Nested/report.txt','file_target':'/report.txt','share_with':'reviewers'}," &
                "{'id':45,'share_type':3,'permissions':1,'path':'/Projects/Other/report.txt','file_target':'/report.txt','url':'https://fixture.invalid/other'}]}}")
        }
    End Function

    Private Shared Function ListingProvider(client As IOcsSharingClient, handler As HttpMessageHandler) As WebDavDmsProvider
        Dim provider = CreateProvider(client, "/Projects")
        provider.CustomWebApiUrl = "https://example.test/remote.php/dav/files/account/Projects/"
        GetType(WebDavDmsProvider).GetField("WebDavClient", BindingFlags.Instance Or BindingFlags.NonPublic).SetValue(provider, New WebDav.WebDavClient(New HttpClient(handler)))
        Return provider
    End Function

    Private Class ListingHandler
        Inherits HttpMessageHandler
        Private ReadOnly Children As Boolean
        Private ReadOnly RejectStatus As Integer
        Public ReadOnly Bodies As New List(Of String)()
        Public RequestCount As Integer
        Public Sub New(children As Boolean, rejectStatus As Integer)
            Me.Children = children
            Me.RejectStatus = rejectStatus
        End Sub
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            RequestCount += 1
            Dim body = Await request.Content.ReadAsStringAsync()
            Bodies.Add(body)
            Dim named = body.Contains("owner-id")
            If named AndAlso RejectStatus <> 0 Then Return New HttpResponseMessage(CType(RejectStatus, HttpStatusCode)) With {.Content = New StringContent(String.Empty)}
            Dim ownerProperties = If(named, "<oc:owner-id>dav-owner</oc:owner-id><oc:owner-display-name>DAV Owner</oc:owner-display-name>", String.Empty)
            Dim xml = "<d:multistatus xmlns:d='DAV:' xmlns:oc='http://owncloud.org/ns'>"
            If Children Then xml &= ListingResource("Nested/", True, ownerProperties) & ListingResource("Nested/child/", True, ownerProperties)
            xml &= ListingResource("Nested/report.txt", False, ownerProperties) & "</d:multistatus>"
            Return New HttpResponseMessage(CType(207, HttpStatusCode)) With {.Content = New StringContent(xml, System.Text.Encoding.UTF8, "application/xml")}
        End Function
        Private Shared Function ListingResource(path As String, folder As Boolean, owner As String) As String
            Return "<d:response><d:href>https://example.test/remote.php/dav/files/account/Projects/" & path & "</d:href><d:propstat><d:prop><d:resourcetype>" & If(folder, "<d:collection/>", String.Empty) &
                "</d:resourcetype>" & owner & "</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>"
        End Function
    End Class

    <TestCase(0)>
    <TestCase(1)>
    <TestCase(3)>
    Public Sub NestedShareUsesSourcePathRatherThanRecipientTarget(shareType As Integer)
        Dim Json As String = "{'ocs':{'meta':{'statuscode':100},'data':[" &
            "{'id':42,'share_type':" & shareType & ",'permissions':1,'path':'/Projects/Nested/report.txt','file_target':'/report.txt','share_with':'recipient','label':'Report','url':'https://example.test/s/token','expiration':'2030-01-02 00:00:00'}," &
            "{'id':43,'share_type':" & shareType & ",'permissions':1,'path':'/Projects/Other/report.txt','file_target':'/report.txt','share_with':'other'}]}}"
        Dim Records As List(Of OcsShareRecord) = OcsSharingClientAdapter.ParseShareRecords(Json)
        Assert.That(Records(0).TargetPath, [Is].EqualTo("/Projects/Nested/report.txt"))
        Assert.That(Records(0).Expiration, [Is].EqualTo(New DateTime(2030, 1, 2)))
        Dim Provider As New WebDavDmsProvider With {.OcsRootPathForTesting = "/Projects"}
        Dim Item As New DmsResourceItem With {.FullName = "Nested/report.txt", .ItemType = DmsResourceItem.ItemTypes.File}
        Provider.ApplyOcsSharingMetadata(Item, Records)
        Assert.That(Item.ExtendedInfosIsShared, [Is].True)
        Select Case shareType
            Case 0
                Assert.That(Item.ExtendedInfosUserSharings.Single().User.ID, [Is].EqualTo("recipient"))
            Case 1
                Assert.That(Item.ExtendedInfosGroupSharings.Single().Group.ID, [Is].EqualTo("recipient"))
            Case 3
                Assert.That(Item.ExtendedInfosLinks.Single().ID, [Is].EqualTo("42"))
                Assert.That(Item.ExtendedInfosLinks.Single().Name, [Is].EqualTo("Report"))
        End Select
    End Sub

    <Test>
    Public Sub LegacyShareResponseWithoutSourcePathRetainsTargetFallback()
        Dim Records As List(Of OcsShareRecord) = OcsSharingClientAdapter.ParseShareRecords("{'ocs':{'meta':{'statuscode':200},'data':[{'id':'42','share_type':3,'permissions':1,'file_target':'/report.txt','expiration':null}]}}")
        Assert.That(Records.Single().TargetPath, [Is].EqualTo("/report.txt"))
        Assert.That(Records.Single().Expiration, [Is].Null)
        Assert.Throws(Of InvalidOperationException)(Sub() OcsSharingClientAdapter.ParseShareRecords("{'ocs':{'meta':{'statuscode':404},'data':[]}}"))
        Assert.Throws(Of InvalidOperationException)(Sub() OcsSharingClientAdapter.ParseShareRecords("{'ocs':{'meta':{'statuscode':100},'data':{}}}"))
    End Sub

    <TestCase("https://cloud.example.test/remote.php/dav/files/alice/", "https://cloud.example.test", "/")>
    <TestCase("https://cloud.example.test/nextcloud/remote.php/dav/files/alice/Projects/2026/", "https://cloud.example.test/nextcloud", "/Projects/2026")>
    <TestCase("https://cloud.example.test/owncloud/remote.php/webdav/Shared/", "https://cloud.example.test/owncloud", "/Shared")>
    <TestCase("https://cloud.example.test/dav/files/alice/Personal/", "https://cloud.example.test", "/Personal")>
    <TestCase("https://cloud.example.test/webdav/Personal/", "https://cloud.example.test", "/Personal")>
    Public Sub OcsConnectionInfoRecognizesKnownWebDavEndpoints(webDavUrl As String, expectedBaseUrl As String, expectedRootPath As String)
        Dim BaseUrl As String = Nothing
        Dim RootPath As String = Nothing

        Dim Found As Boolean = WebDavDmsProvider.TryGetOcsConnectionInfo(webDavUrl, BaseUrl, RootPath)

        Assert.That(Found, [Is].True)
        Assert.That(BaseUrl, [Is].EqualTo(expectedBaseUrl))
        Assert.That(RootPath, [Is].EqualTo(expectedRootPath))
    End Sub

    <TestCase("https://dav.example.test/documents/")>
    <TestCase("https://cloud.example.test/webdav-backup/")>
    <TestCase("https://cloud.example.test/remote.php/webdav2/")>
    Public Sub OcsConnectionInfoLeavesGenericWebDavUnchanged(webDavUrl As String)
        Dim BaseUrl As String = Nothing
        Dim RootPath As String = Nothing

        Dim Found As Boolean = WebDavDmsProvider.TryGetOcsConnectionInfo(webDavUrl, BaseUrl, RootPath)

        Assert.That(Found, [Is].False)
        Assert.That(BaseUrl, [Is].Null)
        Assert.That(RootPath, [Is].Null)
    End Sub

    <Test>
    Public Sub InfiniteScaleSpaceEndpointRequiresLibreGraphAdapter()
        Dim BaseUrl As String = Nothing
        Dim RootPath As String = Nothing

        Dim Found As Boolean = WebDavDmsProvider.TryGetOcsConnectionInfo("https://cloud.example.test/remote.php/dav/spaces/space-id/Folder/", BaseUrl, RootPath)

        Assert.That(Found, [Is].False)
    End Sub

    <Test>
    Public Sub SharingMetadataMapsOcsSharesAndPermissions()
        Dim Provider As New WebDavDmsProvider With {.OcsRootPathForTesting = "/Projects"}
        Dim Item As New DmsResourceItem With {
            .FullName = "Planning/roadmap.txt",
            .ItemType = DmsResourceItem.ItemTypes.File
        }
        Dim Shares As List(Of Share) = ParseShares(
            ShareElement(11, OcsShareType.User, "/Projects/Planning/roadmap.txt", 31, "alice", "Alice Example") &
            ShareElement(12, OcsShareType.Group, "/Projects/Planning/roadmap.txt", 1, "reviewers", "Reviewers") &
            ShareElement(13, OcsShareType.Link, "/Projects/Planning/roadmap.txt", 5, Nothing, Nothing, "https://cloud.example.test/s/public", "Roadmap") &
            ShareElement(14, OcsShareType.User, "/Projects/Planning/other.txt", 31, "ignored", "Ignored"))

        Provider.ApplyOcsSharingMetadata(Item, Shares.Select(AddressOf OcsShareRecord.FromLegacy))

        Assert.Multiple(Sub()
                            Assert.That(Item.ExtendedInfosIsShared, [Is].True)
                            Assert.That(Item.ExtendedInfosHasUserSharings, [Is].True)
                            Assert.That(Item.ExtendedInfosHasGroupSharings, [Is].True)
                            Assert.That(Item.ExtendedInfosUserSharings, Has.Count.EqualTo(1))
                            Assert.That(Item.ExtendedInfosUserSharings(0).User.DisplayName, [Is].EqualTo("Alice Example"))
                            Assert.That(Item.ExtendedInfosUserSharings(0).User.ID, [Is].EqualTo("alice"))
                            Assert.That(Item.ExtendedInfosUserSharings(0).User.LoginName, [Is].Null.Or.Empty)
                            Assert.That(Item.ExtendedInfosOwner.ID, [Is].EqualTo("owner"))
                            Assert.That(Item.ExtendedInfosOwner.DisplayName, [Is].EqualTo("Owner"))
                            Assert.That(Item.ExtendedInfosOwner.LoginName, [Is].Null.Or.Empty)
                            Assert.That(Item.ExtendedInfosUserSharings(0).AllowedActions(), [Is].EquivalentTo(New String() {"View", "Download", "Edit", "Upload", "Delete", "Share"}))
                            Assert.That(Item.ExtendedInfosGroupSharings, Has.Count.EqualTo(1))
                            Assert.That(Item.ExtendedInfosGroupSharings(0).AllowedActions(), [Is].EquivalentTo(New String() {"View", "Download"}))
                            Assert.That(Item.ExtendedInfosLinks, Has.Count.EqualTo(1))
                            Assert.That(Item.ExtendedInfosLinks(0).ID, [Is].EqualTo("13"))
                            Assert.That(Item.ExtendedInfosLinks(0).Name, [Is].EqualTo("Roadmap"))
                            Assert.That(Item.ExtendedInfosLinks(0).WebUrl, [Is].EqualTo("https://cloud.example.test/s/public"))
                            Assert.That(Item.ExtendedInfosLinks(0).AllowUpload, [Is].True)
                        End Sub)
    End Sub

    <Test>
    Public Sub OcsMetadataPreservesWebDavOwner()
        Dim Provider As New WebDavDmsProvider
        Dim Item As New DmsResourceItem With {.FullName = "file.txt", .ExtendedInfosOwner = New DmsUser With {.ID = "dav-owner", .DisplayName = "DAV Owner"}}
        Provider.ApplyOcsSharingMetadata(Item, ParseShares(ShareElement(1, OcsShareType.User, "/file.txt", 1, "recipient", Nothing)).Select(AddressOf OcsShareRecord.FromLegacy))
        Assert.That(Item.ExtendedInfosOwner.ID, [Is].EqualTo("dav-owner"))
        Assert.That(Item.ExtendedInfosOwner.DisplayName, [Is].EqualTo("DAV Owner"))
        Assert.That(Item.ExtendedInfosUserSharings(0).User.DisplayName, [Is].EqualTo("recipient"))
    End Sub

    <TestCase("Alice Example", "Alice (alice)", "Alice Example")>
    <TestCase(Nothing, "Alice Example", "Alice Example")>
    <TestCase(Nothing, Nothing, "opaque-user-id")>
    Public Sub ShareeIdentityDoesNotInventLoginName(displayName As String, label As String, expectedDisplayName As String)
        Dim Element As XElement = XElement.Parse("<element><value><shareType>0</shareType><shareWith>opaque-user-id</shareWith></value></element>")
        Dim Constructor As ConstructorInfo = GetType(Sharee).GetConstructors(BindingFlags.Instance Or BindingFlags.NonPublic).Single()
        Dim Recipient As Sharee = CType(Constructor.Invoke(New Object() {OcsShareType.User, Element}), Sharee)
        Recipient.ShareWithDisplayName = displayName
        Recipient.Label = label
        Dim Client As New FakeOcsSharingClient(New OcsSharingCapabilities(OcsServerFamily.Nextcloud, True, True, True, True, True), New List(Of Share))
        Client.ShareesResult.Add(Recipient)
        Dim User As DmsUser = CreateProvider(Client, "/").GetAllUsers().Single()
        Assert.That(User.ID, [Is].EqualTo("opaque-user-id"))
        Assert.That(User.DisplayName, [Is].EqualTo(expectedDisplayName))
        Assert.That(User.LoginName, [Is].Null.Or.Empty)
    End Sub

    <TestCase("{}", False, False, False, False)>
    <TestCase("{'api_enabled':false,'group_sharing':true,'public':{'enabled':true,'upload':true}}", False, False, False, False)>
    <TestCase("{'api_enabled':true,'can_share':false,'group_sharing':true,'public':{'enabled':true,'upload':true}}", False, False, False, False)>
    <TestCase("{'api_enabled':true,'group_sharing':true,'public':{'enabled':true,'upload':true}}", True, True, True, True)>
    <TestCase("{'api_enabled':true,'group_sharing':true,'group':{'enabled':false},'public':{'enabled':false,'upload':true}}", True, False, False, False)>
    <TestCase("{'api_enabled':true,'group':{'enabled':true},'public':{'enabled':true,'upload':false}}", True, True, True, False)>
    <TestCase("{'api_enabled':true,'public':{'enabled':true,'can_create_public_link':false,'upload':true}}", True, False, False, False)>
    <TestCase("{'api_enabled':'true','public':{'enabled':true}}", False, False, False, False)>
    Public Sub CapabilitiesRespectDeploymentPolicy(sharingJson As String, users As Boolean, groups As Boolean, links As Boolean, upload As Boolean)
        Dim Json As String = "{'ocs':{'meta':{'statuscode':100},'data':{'capabilities':{'files_sharing':" & sharingJson & "}}}}"
        For Each Family As OcsServerFamily In [Enum].GetValues(GetType(OcsServerFamily))
            Dim Capabilities As OcsSharingCapabilities = OcsSharingClientAdapter.ParseCapabilities(Json, Family, True)
            Assert.Multiple(Sub()
                                Assert.That(Capabilities.SupportsUserShares, [Is].EqualTo(users))
                                Assert.That(Capabilities.SupportsGroupShares, [Is].EqualTo(groups))
                                Assert.That(Capabilities.SupportsLinkShares, [Is].EqualTo(links))
                                Assert.That(Capabilities.SupportsPublicUpload, [Is].EqualTo(upload))
                            End Sub)
        Next
    End Sub

    <Test>
    Public Sub CapabilityFailureIsNotTreatedAsSupport()
        Assert.Throws(Of InvalidOperationException)(Sub() OcsSharingClientAdapter.ParseCapabilities("{'ocs':{'meta':{'statuscode':401}}}", OcsServerFamily.Nextcloud, True))
    End Sub

    <Test>
    Public Sub GenericWebDavDoesNotAdvertiseSharing()
        Dim Provider As New WebDavDmsProvider

        Assert.That(Provider.SupportsSharingSetup, [Is].False)
        Assert.Throws(Of NotSupportedException)(Sub() Provider.GetAllUsers())
    End Sub

    <Test>
    Public Sub ClassicOcsManagementMapsPermissionsAndFindsExistingShareIDs()
        Dim ExistingShares As List(Of Share) = ParseShares(ShareElement(21, OcsShareType.User, "/Team/report.docx", 1, "alice", "Alice"))
        Dim FakeClient As New FakeOcsSharingClient(
            New OcsSharingCapabilities(OcsServerFamily.OwnCloudServer, True, True, True, True, False),
            ExistingShares)
        Dim Provider As WebDavDmsProvider = CreateProvider(FakeClient, "/Team")
        Dim Item As New DmsResourceItem With {.FullName = "report.docx", .ItemType = DmsResourceItem.ItemTypes.File}
        Dim UserShare As New DmsShareForUser(Item, New DmsUser With {.ID = "alice"}, True, True, True, True, True, True)

        Provider.CreateSharing(Item, UserShare)
        Provider.UpdateSharing(UserShare)
        Provider.DeleteSharing(UserShare)

        Assert.Multiple(Sub()
                            Assert.That(FakeClient.LastPath, [Is].EqualTo("/Team/report.docx"))
                            Assert.That(FakeClient.LastShareWithID, [Is].EqualTo("alice"))
                            Assert.That(FakeClient.LastPermissions, [Is].EqualTo(31))
                            Assert.That(FakeClient.LastUpdatedShareID, [Is].EqualTo(21))
                            Assert.That(FakeClient.LastDeletedShareID, [Is].EqualTo(21))
                        End Sub)
    End Sub

    <Test>
    Public Sub NextcloudOcsAdapterRejectsViewWithoutDownloadInsteadOfLosingShareAttributes()
        Dim FakeClient As New FakeOcsSharingClient(
            New OcsSharingCapabilities(OcsServerFamily.Nextcloud, True, True, True, True, True),
            New List(Of Share))
        Dim Provider As WebDavDmsProvider = CreateProvider(FakeClient, "/")
        Dim Item As New DmsResourceItem With {.FullName = "document.pdf", .ItemType = DmsResourceItem.ItemTypes.File}
        Dim Link As New DmsLink(Item, Provider) With {
            .AllowView = True,
            .AllowDownload = False
        }

        Dim Failure As NotSupportedException = Assert.Throws(Of NotSupportedException)(Sub() Provider.CreateLink(Item, Link))

        Assert.That(Failure.Message, Does.Contain("view and download"))
        Assert.That(FakeClient.CreateLinkCallCount, [Is].Zero)
    End Sub

    <Test>
    Public Sub GroupShareRoundTripUsesGroupIdentifier()
        Dim Client As New FakeOcsSharingClient(New OcsSharingCapabilities(OcsServerFamily.Nextcloud, True, True, True, True, True),
            ParseShares(ShareElement(22, OcsShareType.Group, "/Team/report.docx", 1, "reviewer-id", "Reviewers")))
        Dim Provider As WebDavDmsProvider = CreateProvider(Client, "/Team")
        Dim Item As New DmsResourceItem With {.FullName = "report.docx", .ItemType = DmsResourceItem.ItemTypes.File}
        Dim Sharing As New DmsShareForGroup(Item, New DmsGroup With {.ID = "reviewer-id", .Name = "Reviewers"}, True, True, False, False, False, False)
        Provider.CreateSharing(Item, Sharing)
        Provider.UpdateSharing(Sharing)
        Provider.DeleteSharing(Sharing)
        Assert.That(Client.LastShareWithID, [Is].EqualTo("reviewer-id"))
        Assert.That(Client.LastPermissions, [Is].EqualTo(1))
        Assert.That(Client.LastUpdatedShareID, [Is].EqualTo(22))
        Assert.That(Client.LastDeletedShareID, [Is].EqualTo(22))
    End Sub

    <Test>
    Public Sub DisabledGroupPolicyRejectsMutationsBeforeCallingServer()
        Dim Client As New FakeOcsSharingClient(New OcsSharingCapabilities(OcsServerFamily.Nextcloud, True, False, True, False, True), New List(Of Share))
        Dim Provider As WebDavDmsProvider = CreateProvider(Client, "/")
        Dim Item As New DmsResourceItem With {.FullName = "report.docx"}
        Dim Sharing As New DmsShareForGroup(Item, New DmsGroup With {.ID = "reviewer-id"}, True, True, False, False, False, False)
        Assert.Throws(Of NotSupportedException)(Sub() Provider.CreateSharing(Item, Sharing))
        Assert.Throws(Of NotSupportedException)(Sub() Provider.UpdateSharing(Sharing))
        Assert.That(Client.LastShareWithID, [Is].Null)
        Assert.That(Client.LastUpdatedShareID, [Is].Zero)
    End Sub

    <Test>
    Public Sub InfiniteScaleProfileCanDisableUnsupportedPublicUpload()
        Dim FakeClient As New FakeOcsSharingClient(
            New OcsSharingCapabilities(OcsServerFamily.OwnCloudInfiniteScale, True, True, True, False, True),
            New List(Of Share))
        Dim Provider As WebDavDmsProvider = CreateProvider(FakeClient, "/")
        Dim Item As New DmsResourceItem With {.FullName = "incoming", .ItemType = DmsResourceItem.ItemTypes.Folder}
        Dim Link As New DmsLink(Item, Provider) With {
            .AllowView = True,
            .AllowDownload = True,
            .AllowUpload = True
        }

        Dim Failure As NotSupportedException = Assert.Throws(Of NotSupportedException)(Sub() Provider.CreateLink(Item, Link))

        Assert.That(Failure.Message, Does.Contain("Public uploads"))
        Assert.That(FakeClient.CreateLinkCallCount, [Is].Zero)
    End Sub

    <Test>
    Public Sub ShareeDiscoveryAndProvisioningFallbackRemainSeparateCapabilities()
        Dim Sharees As New List(Of Sharee)
        Dim ShareeClient As New FakeOcsSharingClient(
            New OcsSharingCapabilities(OcsServerFamily.Nextcloud, True, True, True, True, True),
            New List(Of Share)) With {
            .ShareesResult = Sharees
        }
        Dim ProvisioningClient As New FakeOcsSharingClient(
            New OcsSharingCapabilities(OcsServerFamily.OwnCloudServer, True, True, True, True, False),
            New List(Of Share)) With {
            .SearchUsersResult = New List(Of String) From {"admin", "alice"},
            .SearchGroupsResult = New List(Of String) From {"reviewers"}
        }

        Assert.Multiple(Sub()
                            Assert.That(CreateProvider(ShareeClient, "/").GetAllUsers(), [Is].Empty)
                            Assert.That(ShareeClient.FindShareesCallCount, [Is].EqualTo(1))
                            Assert.That(ShareeClient.SearchUsersCallCount, [Is].Zero)
                            Assert.That(CreateProvider(ProvisioningClient, "/").GetAllUsers().Select(Function(item) item.ID), [Is].EquivalentTo(New String() {"admin", "alice"}))
                            Assert.That(CreateProvider(ProvisioningClient, "/").GetAllGroups().Select(Function(item) item.ID), [Is].EquivalentTo(New String() {"reviewers"}))
                        End Sub)
    End Sub

    Private Shared Function CreateProvider(client As IOcsSharingClient, rootPath As String) As WebDavDmsProvider
        Return New WebDavDmsProvider With {
            .OcsSharingClientForTesting = client,
            .OcsRootPathForTesting = rootPath
        }
    End Function

    Private Shared Function ParseShares(elements As String) As List(Of Share)
        Dim Result As New List(Of Share)
        For Each Element As XElement In XDocument.Parse("<data>" & elements & "</data>").Root.Elements("element")
            Dim ShareType As OcsShareType = CType(Integer.Parse(Element.Element("share_type").Value, Globalization.CultureInfo.InvariantCulture), OcsShareType)
            Dim RuntimeType As Type
            Select Case ShareType
                Case OcsShareType.User
                    RuntimeType = GetType(UserShare)
                Case OcsShareType.Group
                    RuntimeType = GetType(GroupShare)
                Case OcsShareType.Link
                    RuntimeType = GetType(PublicShare)
                Case Else
                    RuntimeType = GetType(Share)
            End Select
            Dim Constructor As ConstructorInfo = RuntimeType.GetConstructors(BindingFlags.Instance Or BindingFlags.NonPublic).Single()
            Result.Add(CType(Constructor.Invoke(New Object() {ShareType, Element}), Share))
        Next
        Return Result
    End Function

    Private Shared Function ShareElement(id As Integer, shareType As OcsShareType, path As String, permissions As Integer, shareWith As String, displayName As String, Optional publicUrl As String = Nothing, Optional name As String = Nothing) As String
        Return "<element>" &
            "<id>" & id.ToString(Globalization.CultureInfo.InvariantCulture) & "</id>" &
            "<share_type>" & Convert.ToInt32(shareType).ToString(Globalization.CultureInfo.InvariantCulture) & "</share_type>" &
            "<file_target>" & path & "</file_target>" &
            "<permissions>" & permissions.ToString(Globalization.CultureInfo.InvariantCulture) & "</permissions>" &
            "<uid_owner>owner</uid_owner><displayname_owner>Owner</displayname_owner>" &
            If(shareWith Is Nothing, String.Empty, "<share_with>" & shareWith & "</share_with>") &
            If(displayName Is Nothing, String.Empty, "<share_with_displayname>" & displayName & "</share_with_displayname>") &
            If(publicUrl Is Nothing, String.Empty, "<url>" & publicUrl & "</url>") &
            If(name Is Nothing, String.Empty, "<name>" & name & "</name>") &
            "</element>"
    End Function

    Private NotInheritable Class FakeOcsSharingClient
        Implements IOcsSharingClient

        Public Sub New(capabilities As OcsSharingCapabilities, shares As List(Of Share))
            Me.Capabilities = capabilities
            Me.Shares = shares
        End Sub

        Public ReadOnly Property Capabilities As OcsSharingCapabilities Implements IOcsSharingClient.Capabilities
        Public Property Shares As List(Of Share)
        Public Property ShareesResult As New List(Of Sharee)
        Public Property SearchUsersResult As New List(Of String)
        Public Property SearchGroupsResult As New List(Of String)
        Public Property LastPath As String
        Public Property LastShareWithID As String
        Public Property LastPermissions As Integer
        Public Property LastUpdatedShareID As Integer
        Public Property LastDeletedShareID As Integer
        Public Property CreateLinkCallCount As Integer
        Public Property FindShareesCallCount As Integer
        Public Property SearchUsersCallCount As Integer
        Public Property Records As List(Of OcsShareRecord)
        Public Property BeforeGetShares As Action
        Public Property LastIncludeSubFiles As Boolean
        Public Property GetSharesCallCount As Integer

        Public Sub ProbeCapabilities() Implements IOcsSharingClient.ProbeCapabilities
        End Sub

        Public Function GetShares(path As String, includeReshares As Boolean, includeSubFiles As Boolean) As List(Of OcsShareRecord) Implements IOcsSharingClient.GetShares
            Me.LastPath = path
            Me.LastIncludeSubFiles = includeSubFiles
            Me.GetSharesCallCount += 1
            If Me.BeforeGetShares IsNot Nothing Then Me.BeforeGetShares.Invoke()
            If Me.Records IsNot Nothing Then Return Me.Records
            Return Me.Shares.Select(AddressOf OcsShareRecord.FromLegacy).ToList()
        End Function

        Public Function CreateLink(path As String, permissions As Integer, publicUpload As Boolean, name As String, expiration As DateTime?, password As String) As Share Implements IOcsSharingClient.CreateLink
            Me.CreateLinkCallCount += 1
            Me.LastPath = path
            Me.LastPermissions = permissions
            Return Me.Shares.FirstOrDefault(Function(item) item.Type = OcsShareType.Link)
        End Function

        Public Function CreateUserShare(path As String, userID As String, permissions As Integer) As Share Implements IOcsSharingClient.CreateUserShare
            Me.LastPath = path
            Me.LastShareWithID = userID
            Me.LastPermissions = permissions
            Return Me.Shares.FirstOrDefault(Function(item) item.Type = OcsShareType.User)
        End Function

        Public Function CreateGroupShare(path As String, groupID As String, permissions As Integer) As Share Implements IOcsSharingClient.CreateGroupShare
            Me.LastPath = path
            Me.LastShareWithID = groupID
            Me.LastPermissions = permissions
            Return Me.Shares.FirstOrDefault(Function(item) item.Type = OcsShareType.Group)
        End Function

        Public Sub UpdateSharePermissions(shareID As Integer, permissions As Integer) Implements IOcsSharingClient.UpdateSharePermissions
            Me.LastUpdatedShareID = shareID
            Me.LastPermissions = permissions
        End Sub

        Public Sub UpdateLink(shareID As Integer, permissions As Integer, publicUpload As Boolean, name As String, expiration As DateTime?, clearExpiration As Boolean, password As String) Implements IOcsSharingClient.UpdateLink
            Me.LastUpdatedShareID = shareID
            Me.LastPermissions = permissions
        End Sub

        Public Sub DeleteShare(shareID As Integer) Implements IOcsSharingClient.DeleteShare
            Me.LastDeletedShareID = shareID
        End Sub

        Public Function FindSharees(search As String, itemType As String) As List(Of Sharee) Implements IOcsSharingClient.FindSharees
            Me.FindShareesCallCount += 1
            Return Me.ShareesResult
        End Function

        Public Function SearchUsers() As List(Of String) Implements IOcsSharingClient.SearchUsers
            Me.SearchUsersCallCount += 1
            Return Me.SearchUsersResult
        End Function

        Public Function SearchGroups() As List(Of String) Implements IOcsSharingClient.SearchGroups
            Return Me.SearchGroupsResult
        End Function

    End Class

End Class
