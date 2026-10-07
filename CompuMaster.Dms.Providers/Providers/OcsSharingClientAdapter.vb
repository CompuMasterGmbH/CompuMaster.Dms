Option Explicit On
Option Strict On

Imports CompuMaster.Ocs
Imports CompuMaster.Ocs.Core
Imports CompuMaster.Ocs.Types
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports Newtonsoft.Json.Linq

Namespace Providers

    'The upstream Share.TargetPath only retains file_target (the recipient's path).
    'Keep the current user's source path separately when reading share lists.
    Friend NotInheritable Class OcsShareRecord
        Public Property ShareId As Integer
        Public Property Type As OcsShareType
        Public Property TargetPath As String
        Public Property Permissions As OcsPermission
        Public Property Expiration As DateTime?
        Public Property Name As String
        Public Property Url As String
        Public Property SharedWith As String
        Public Property AdvancedProperties As AdvancedShareProperties

        Public Shared Function FromLegacy(value As Share) As OcsShareRecord
            Return New OcsShareRecord With {
                .ShareId = value.ShareId, .Type = value.Type, .TargetPath = value.TargetPath,
                .Permissions = value.Permissions, .Expiration = value.Expiration, .Name = value.Name,
                .Url = TryCast(value, PublicShare)?.Url,
                .SharedWith = If(TryCast(value, UserShare)?.SharedWith, TryCast(value, GroupShare)?.SharedWith),
                .AdvancedProperties = value.AdvancedProperties}
        End Function
    End Class

    Friend Enum OcsServerFamily As Byte
        Unknown = 0
        Nextcloud = 1
        OwnCloudServer = 2
        OwnCloudInfiniteScale = 3
    End Enum

    Friend NotInheritable Class OcsSharingCapabilities

        Public Sub New(serverFamily As OcsServerFamily,
                       supportsUserShares As Boolean,
                       supportsGroupShares As Boolean,
                       supportsLinkShares As Boolean,
                       supportsPublicUpload As Boolean,
                       supportsShareeDiscovery As Boolean)
            Me.ServerFamily = serverFamily
            Me.SupportsUserShares = supportsUserShares
            Me.SupportsGroupShares = supportsGroupShares
            Me.SupportsLinkShares = supportsLinkShares
            Me.SupportsPublicUpload = supportsPublicUpload
            Me.SupportsShareeDiscovery = supportsShareeDiscovery
        End Sub

        Public ReadOnly Property ServerFamily As OcsServerFamily
        Public ReadOnly Property SupportsUserShares As Boolean
        Public ReadOnly Property SupportsGroupShares As Boolean
        Public ReadOnly Property SupportsLinkShares As Boolean
        Public ReadOnly Property SupportsPublicUpload As Boolean
        Public ReadOnly Property SupportsShareeDiscovery As Boolean

        Public ReadOnly Property SupportsAnySharing As Boolean
            Get
                Return Me.SupportsUserShares OrElse Me.SupportsGroupShares OrElse Me.SupportsLinkShares
            End Get
        End Property

    End Class

    Friend Interface IOcsSharingClient

        ReadOnly Property Capabilities As OcsSharingCapabilities

        Sub ProbeCapabilities()

        Function GetShares(path As String, includeReshares As Boolean, includeSubFiles As Boolean) As List(Of OcsShareRecord)

        Function CreateLink(path As String, permissions As Integer, publicUpload As Boolean, name As String, expiration As DateTime?, password As String) As Share

        Function CreateUserShare(path As String, userID As String, permissions As Integer) As Share

        Function CreateGroupShare(path As String, groupID As String, permissions As Integer) As Share

        Sub UpdateSharePermissions(shareID As Integer, permissions As Integer)

        Sub UpdateLink(shareID As Integer, permissions As Integer, publicUpload As Boolean, name As String, expiration As DateTime?, clearExpiration As Boolean, password As String)

        Sub DeleteShare(shareID As Integer)

        Function FindSharees(search As String, itemType As String) As List(Of Sharee)

        Function SearchUsers() As List(Of String)

        Function SearchGroups() As List(Of String)

    End Interface

    Friend NotInheritable Class OcsSharingClientAdapter
        Implements IOcsSharingClient

        Private ReadOnly Client As OcsClient
        Private ReadOnly ReadJson As Func(Of String, String)
        Private _Capabilities As OcsSharingCapabilities

        Public Sub New(baseUrl As String, userID As String, password As String)
            Me.Client = New OcsClient(baseUrl, userID, password)
            Me.ReadJson = Function(endpoint) LoadJson(baseUrl, userID, password, endpoint)
        End Sub

        Public ReadOnly Property Capabilities As OcsSharingCapabilities Implements IOcsSharingClient.Capabilities
            Get
                Return Me._Capabilities
            End Get
        End Property

        Public Sub ProbeCapabilities() Implements IOcsSharingClient.ProbeCapabilities
            'The config endpoint is not a prerequisite for sharing on every server.
            Dim Config As Config = Nothing
            Try
                Config = Me.Client.GetConfig()
            Catch
                'Family detection is diagnostic only; capabilities decide support.
            End Try
            Dim CapabilityJson As String = Me.ReadJson("cloud/capabilities")
            Me.GetShares(Nothing, False, False)

            Dim SupportsShareeDiscovery As Boolean
            Try
                Me.Client.Sharees(String.Empty, False, "file")
                SupportsShareeDiscovery = True
            Catch
                SupportsShareeDiscovery = False
            End Try

            Me._Capabilities = ParseCapabilities(CapabilityJson, DetectServerFamily(Config), SupportsShareeDiscovery)
        End Sub

        Private Shared Function LoadJson(baseUrl As String, userID As String, password As String, endpoint As String) As String
            'Do not forward credentials through redirects to another endpoint.
            Using Handler As New HttpClientHandler With {.AllowAutoRedirect = False},
                  Http As New HttpClient(Handler) With {.Timeout = TimeSpan.FromSeconds(30)},
                  Request As New HttpRequestMessage(HttpMethod.Get, baseUrl.TrimEnd("/"c) & "/ocs/v1.php/" & endpoint & If(endpoint.Contains("?"), "&", "?") & "format=json")
                Request.Headers.Authorization = New AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(userID & ":" & password)))
                Request.Headers.Add("OCS-APIRequest", "true")
                Request.Headers.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
                Using Response As HttpResponseMessage = Http.SendAsync(Request).GetAwaiter().GetResult()
                    Response.EnsureSuccessStatusCode()
                    Return Response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                End Using
            End Using
        End Function

        Friend Shared Function ParseCapabilities(json As String, family As OcsServerFamily, shareeDiscovery As Boolean) As OcsSharingCapabilities
            Dim Document As JObject = JObject.Parse(json)
            Dim Status As String = CStr(Document.SelectToken("ocs.meta.statuscode"))
            If Status <> "100" AndAlso Status <> "200" Then Throw New InvalidOperationException(ProviderStrings.GetText("OCSCapabilityDiscoveryFailed"))
            Dim Sharing As JToken = Document.SelectToken("ocs.data.capabilities.files_sharing")
            Dim Enabled As Boolean = CapabilityFlag(Sharing, "api_enabled", False) AndAlso CapabilityFlag(Sharing, "can_share", True)
            Dim Links As Boolean = Enabled AndAlso CapabilityFlag(Sharing, "public.enabled", False) AndAlso CapabilityFlag(Sharing, "public.can_create_public_link", True)
            'Nextcloud uses group.enabled; ownCloud Classic and older Nextcloud use group_sharing.
            Dim Groups As Boolean = CapabilityFlag(Sharing, "group.enabled", CapabilityFlag(Sharing, "group_sharing", False))
            Return New OcsSharingCapabilities(family,
                supportsUserShares:=Enabled,
                supportsGroupShares:=Enabled AndAlso Groups,
                supportsLinkShares:=Links,
                supportsPublicUpload:=Links AndAlso CapabilityFlag(Sharing, "public.upload", False),
                supportsShareeDiscovery:=Enabled AndAlso shareeDiscovery)
        End Function

        Private Shared Function CapabilityFlag(root As JToken, path As String, missing As Boolean) As Boolean
            Dim Value As JToken = root?.SelectToken(path)
            If Value Is Nothing OrElse Value.Type = JTokenType.Null Then Return missing
            Return Value.Type = JTokenType.Boolean AndAlso CBool(Value)
        End Function

        Private Shared Function DetectServerFamily(config As Config) As OcsServerFamily
            Dim Fingerprint As String = String.Join(" ", New String() {
                config?.Website,
                config?.Host,
                config?.Version
            }).ToLowerInvariant()

            If Fingerprint.Contains("nextcloud") Then
                Return OcsServerFamily.Nextcloud
            ElseIf Fingerprint.Contains("infinite scale") OrElse Fingerprint.Contains("ocis") Then
                Return OcsServerFamily.OwnCloudInfiniteScale
            ElseIf Fingerprint.Contains("owncloud") Then
                Return OcsServerFamily.OwnCloudServer
            Else
                Return OcsServerFamily.Unknown
            End If
        End Function

        Public Function GetShares(path As String, includeReshares As Boolean, includeSubFiles As Boolean) As List(Of OcsShareRecord) Implements IOcsSharingClient.GetShares
            Dim Endpoint As String = "apps/files_sharing/api/v1/shares?reshares=" & If(includeReshares, "true", "false") & "&subfiles=" & If(includeSubFiles, "true", "false")
            If Not String.IsNullOrEmpty(path) Then Endpoint &= "&path=" & Uri.EscapeDataString(path)
            Return ParseShareRecords(Me.ReadJson(Endpoint))
        End Function

        Friend Shared Function ParseShareRecords(json As String) As List(Of OcsShareRecord)
            Dim Document As JObject = JObject.Parse(json)
            Dim Status As String = CStr(Document.SelectToken("ocs.meta.statuscode"))
            If Status <> "100" AndAlso Status <> "200" Then Throw New InvalidOperationException(ProviderStrings.GetText("OCSShareListingFailed"))
            Dim Entries As JArray = TryCast(Document.SelectToken("ocs.data"), JArray)
            If Entries Is Nothing Then Throw New InvalidOperationException(ProviderStrings.GetText("OCSShareListingDidNotReturnAnArray"))
            Dim Result As New List(Of OcsShareRecord)
            For Each Entry As JObject In Entries
                Dim SourcePath As String = CStr(Entry("path"))
                If String.IsNullOrEmpty(SourcePath) Then SourcePath = CStr(Entry("file_target"))
                Dim Expiration As DateTime? = Nothing
                Dim ExpirationText As String = CStr(Entry("expiration"))
                If Not String.IsNullOrEmpty(ExpirationText) Then Expiration = DateTime.Parse(ExpirationText, Globalization.CultureInfo.InvariantCulture)
                Result.Add(New OcsShareRecord With {
                    .ShareId = CInt(Entry("id")), .Type = CType(CInt(Entry("share_type")), OcsShareType),
                    .TargetPath = SourcePath, .Permissions = CType(CInt(Entry("permissions")), OcsPermission),
                    .Expiration = Expiration, .Name = If(CStr(Entry("name")), CStr(Entry("label"))),
                    .Url = CStr(Entry("url")), .SharedWith = CStr(Entry("share_with")),
                    .AdvancedProperties = New AdvancedShareProperties With {
                        .Owner = CStr(Entry("uid_owner")), .DisplaynameOwner = CStr(Entry("displayname_owner")),
                        .SharedWithDisplayname = CStr(Entry("share_with_displayname"))}})
            Next
            Return Result
        End Function

        Public Function CreateLink(path As String, permissions As Integer, publicUpload As Boolean, name As String, expiration As DateTime?, password As String) As Share Implements IOcsSharingClient.CreateLink
            Return Me.Client.CreateShareWithLink(path, CType(permissions, OcsPermission), ToOcsBoolParam(publicUpload), name, expiration, password)
        End Function

        Public Function CreateUserShare(path As String, userID As String, permissions As Integer) As Share Implements IOcsSharingClient.CreateUserShare
            Return Me.Client.CreateShareWithUser(path, userID, CType(permissions, OcsPermission), Nothing)
        End Function

        Public Function CreateGroupShare(path As String, groupID As String, permissions As Integer) As Share Implements IOcsSharingClient.CreateGroupShare
            Return Me.Client.CreateShareWithGroup(path, groupID, CType(permissions, OcsPermission), Nothing)
        End Function

        Public Sub UpdateSharePermissions(shareID As Integer, permissions As Integer) Implements IOcsSharingClient.UpdateSharePermissions
            Me.Client.UpdateShare(shareID, CType(permissions, OcsPermission))
        End Sub

        Public Sub UpdateLink(shareID As Integer, permissions As Integer, publicUpload As Boolean, name As String, expiration As DateTime?, clearExpiration As Boolean, password As String) Implements IOcsSharingClient.UpdateLink
            Dim ExpirationValue As DateTime? = expiration
            If clearExpiration Then
                ExpirationValue = DateTime.MinValue
            End If
            Me.Client.UpdateShare(shareID, CType(permissions, OcsPermission), ToOcsBoolParam(publicUpload), name, ExpirationValue, password, Nothing)
        End Sub

        Public Sub DeleteShare(shareID As Integer) Implements IOcsSharingClient.DeleteShare
            Me.Client.DeleteShare(shareID)
        End Sub

        Public Function FindSharees(search As String, itemType As String) As List(Of Sharee) Implements IOcsSharingClient.FindSharees
            Return Me.Client.Sharees(search, False, itemType)
        End Function

        Public Function SearchUsers() As List(Of String) Implements IOcsSharingClient.SearchUsers
            Return Me.Client.SearchUsers()
        End Function

        Public Function SearchGroups() As List(Of String) Implements IOcsSharingClient.SearchGroups
            Return Me.Client.SearchGroups(String.Empty)
        End Function

        Private Shared Function ToOcsBoolParam(value As Boolean) As OcsBoolParam
            If value Then
                Return OcsBoolParam.True
            Else
                Return OcsBoolParam.False
            End If
        End Function

    End Class

End Namespace
