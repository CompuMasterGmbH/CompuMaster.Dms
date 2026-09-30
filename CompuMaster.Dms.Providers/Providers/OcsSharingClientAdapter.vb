Option Explicit On
Option Strict On

Imports CompuMaster.Ocs
Imports CompuMaster.Ocs.Core
Imports CompuMaster.Ocs.Types

Namespace Providers

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

        Function GetShares(path As String, includeReshares As Boolean, includeSubFiles As Boolean) As List(Of Share)

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
        Private _Capabilities As OcsSharingCapabilities

        Public Sub New(baseUrl As String, userID As String, password As String)
            Me.Client = New OcsClient(baseUrl, userID, password)
        End Sub

        Public ReadOnly Property Capabilities As OcsSharingCapabilities Implements IOcsSharingClient.Capabilities
            Get
                Return Me._Capabilities
            End Get
        End Property

        Public Sub ProbeCapabilities() Implements IOcsSharingClient.ProbeCapabilities
            Dim Config As Config = Me.Client.GetConfig()
            Me.Client.GetShares()

            Dim SupportsShareeDiscovery As Boolean
            Try
                Me.Client.Sharees(String.Empty, False, "file")
                SupportsShareeDiscovery = True
            Catch
                SupportsShareeDiscovery = False
            End Try

            Me._Capabilities = New OcsSharingCapabilities(
                DetectServerFamily(Config),
                supportsUserShares:=True,
                supportsGroupShares:=True,
                supportsLinkShares:=True,
                supportsPublicUpload:=True,
                supportsShareeDiscovery:=SupportsShareeDiscovery)
        End Sub

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

        Public Function GetShares(path As String, includeReshares As Boolean, includeSubFiles As Boolean) As List(Of Share) Implements IOcsSharingClient.GetShares
            Return Me.Client.GetShares(path, ToOcsBoolParam(includeReshares), ToOcsBoolParam(includeSubFiles))
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
