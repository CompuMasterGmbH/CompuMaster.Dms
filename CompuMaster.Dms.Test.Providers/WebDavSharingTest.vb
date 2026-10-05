Option Explicit On
Option Strict On

Imports System.Linq
Imports System.Reflection
Imports System.Xml.Linq
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Ocs
Imports CompuMaster.Ocs.Core
Imports CompuMaster.Ocs.Types
Imports NUnit.Framework

<TestFixture>
Public Class WebDavSharingTest

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

        Provider.ApplyOcsSharingMetadata(Item, Shares)

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
        Provider.ApplyOcsSharingMetadata(Item, ParseShares(ShareElement(1, OcsShareType.User, "/file.txt", 1, "recipient", Nothing)))
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

        Public Sub ProbeCapabilities() Implements IOcsSharingClient.ProbeCapabilities
        End Sub

        Public Function GetShares(path As String, includeReshares As Boolean, includeSubFiles As Boolean) As List(Of Share) Implements IOcsSharingClient.GetShares
            Me.LastPath = path
            Return Me.Shares
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
