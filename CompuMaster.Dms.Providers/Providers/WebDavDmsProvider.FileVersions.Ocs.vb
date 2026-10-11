Imports System.IO
Imports CompuMaster.Dms.Data
Imports CompuMaster.Ocs.Core
Imports Newtonsoft.Json.Linq

Namespace Providers
    Partial Public Class WebDavDmsProvider
        Private FileVersionClient As DavFileVersionClient
        Private FileVersionDiscoveryFailure As Exception

        Private Sub InitializeFileVersions(candidate As OcsSharingClientAdapter, baseUrl As String)
            Try
                Me.ConfigureFileVersions(Me.WebDavClient, baseUrl, candidate.ObservedServerFamily, candidate.ObservedVersionCapabilitiesJson)
            Catch ex As Exception
                'Optional discovery never breaks existing generic DAV authorization. Retain its cause for direct workflow calls.
                Me.FileVersionClient = Nothing
                Me.FileVersionDiscoveryFailure = ex
            End Try
        End Sub

        Friend Sub ConfigureFileVersions(transport As Global.WebDav.IWebDavClient, baseUrl As String, family As OcsServerFamily, capabilityJson As String)
            Me.FileVersionClient = Nothing
            Me.FileVersionDiscoveryFailure = Nothing
            If String.IsNullOrEmpty(capabilityJson) OrElse family = OcsServerFamily.Unknown Then Return
            Dim document = JObject.Parse(capabilityJson)
            Dim status = CStr(document.SelectToken("ocs.meta.statuscode"))
            If status <> "100" AndAlso status <> "200" Then Throw New InvalidDataException("Version capability discovery failed.")
            Dim enabled = document.SelectToken("ocs.data.capabilities.files.versioning")
            If enabled Is Nothing OrElse enabled.Type <> JTokenType.Boolean OrElse Not CBool(enabled) Then Return
            Dim address As New Uri(baseUrl, UriKind.Absolute)
            If address.UserInfo.Length <> 0 OrElse address.Query.Length <> 0 OrElse address.Fragment.Length <> 0 Then Return
            Dim match = System.Text.RegularExpressions.Regex.Match(address.AbsolutePath, "^(.*/(?:remote\.php/)?dav/)files/([^/]+)(?:/|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            'Legacy /webdav URLs cannot safely identify Nextcloud's protocol user; no login-name substitution.
            If Not match.Success Then Return
            Dim root As New Uri(address.GetLeftPart(UriPartial.Authority) & match.Groups(1).Value)
            Dim protocol As DavFileVersionProtocol
            Select Case family
                Case OcsServerFamily.Nextcloud : protocol = DavFileVersionProtocol.Nextcloud
                Case OcsServerFamily.OwnCloudServer : protocol = DavFileVersionProtocol.OwnCloudServer
                Case OcsServerFamily.OwnCloudInfiniteScale : protocol = DavFileVersionProtocol.OwnCloudInfiniteScale
                Case Else : Return
            End Select
            Dim deletionToken = document.SelectToken("ocs.data.capabilities.files.version_deletion")
            Dim deletion As Boolean? = If(deletionToken IsNot Nothing AndAlso deletionToken.Type = JTokenType.Boolean, CType(CBool(deletionToken), Boolean?), Nothing)
            Me.FileVersionClient = New DavFileVersionClient(transport, root, protocol, Uri.UnescapeDataString(match.Groups(2).Value), deletion)
            Me.CustomWebApiUrl = baseUrl.TrimEnd("/"c) & "/"
        End Sub

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionHistory As Boolean
            Get
                Return Me.FileVersionClient IsNot Nothing
            End Get
        End Property
        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionDownload As Boolean
            Get
                Return Me.FileVersionClient IsNot Nothing
            End Get
        End Property
        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionRestore As Boolean
            Get
                Return Me.FileVersionClient IsNot Nothing
            End Get
        End Property
        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFileVersionRemoval As Boolean
            Get
                Return Me.FileVersionClient IsNot Nothing AndAlso Me.FileVersionClient.CanRemoveVersions.GetValueOrDefault()
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides Function ListFileVersions(remoteFile As DmsResourceItem) As List(Of DmsFileVersion)
            Dim client = Me.RequireFileVersions()
            If remoteFile Is Nothing Then Throw New ArgumentNullException(NameOf(remoteFile))
            If remoteFile.ItemType <> DmsResourceItem.ItemTypes.File Then Throw New ArgumentException("A selected file is required.", NameOf(remoteFile))
            Dim result As New List(Of DmsFileVersion)
            Try
                Dim versions = client.ListVersions(Me.VersionFileAddress(remoteFile.FullName), remoteFile.ExtendedInfosFileID)
                For Each version In versions
                    If Not String.IsNullOrEmpty(remoteFile.ExtendedInfosFileID) AndAlso remoteFile.ExtendedInfosFileID <> version.FileId Then Throw New InvalidOperationException("The selected file identity changed before version listing.")
                    Dim fileSnapshot As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = remoteFile.Name, .FullName = remoteFile.FullName, .ExtendedInfosFileID = version.FileId}
                    result.Add(New DmsFileVersion(fileSnapshot, version.VersionId, version.LastModifiedUtc, version.ContentLength,
                        version.AuthorId, version.AuthorDisplayName, version.Label, version.ContentType, version.ETag) With {.ProviderState = version})
                Next
                Return result
            Catch ex As Exception
                Throw New FileActionFailedException("ListFileVersions", remoteFile.FullName, ex)
            End Try
        End Function

        Private Function VersionFileAddress(path As String) As Uri
            If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("A file path is required.", NameOf(path))
            Dim segments = path.TrimStart("/"c).Split("/"c)
            If segments.Any(Function(segment) segment = "." OrElse segment = ".." OrElse segment.Length = 0 OrElse segment.Contains("\") OrElse segment.Any(Function(c) Char.IsControl(c))) Then Throw New ArgumentException("The file path contains an invalid segment.", NameOf(path))
            Return New Uri(Me.CustomWebApiUrl & String.Join("/", segments.Select(Function(segment) Uri.EscapeDataString(segment))))
        End Function

        ''' <inheritdoc/>
        Public Overrides Sub DownloadFileVersion(version As DmsFileVersion, destination As Stream)
            Me.RunFileVersionAction("DownloadFileVersion", version, Sub(client, snapshot) client.DownloadVersion(snapshot, destination))
        End Sub
        ''' <inheritdoc/>
        Public Overrides Sub RestoreFileVersion(version As DmsFileVersion)
            Me.RunFileVersionAction("RestoreFileVersion", version, Sub(client, snapshot) client.RestoreVersion(snapshot))
        End Sub
        ''' <inheritdoc/>
        Public Overrides Sub RemoveFileVersion(version As DmsFileVersion)
            If Not Me.SupportsFileVersionRemoval Then Throw New NotSupportedException("Individual version removal is unsupported or unknown.")
            Me.RunFileVersionAction("RemoveFileVersion", version, Sub(client, snapshot) client.RemoveVersion(snapshot))
        End Sub

        Private Sub RunFileVersionAction(action As String, version As DmsFileVersion, operation As Action(Of DavFileVersionClient, DavFileVersion))
            Dim client = Me.RequireFileVersions()
            If version Is Nothing Then Throw New ArgumentNullException(NameOf(version))
            Dim snapshot = TryCast(version.ProviderState, DavFileVersion)
            If snapshot Is Nothing Then Throw New ArgumentException("Use a version returned by this provider.", NameOf(version))
            Try
                operation(client, snapshot)
            Catch ex As Exception
                Throw New FileActionFailedException(action, version.FileFullName, ex)
            End Try
        End Sub

        Private Function RequireFileVersions() As DavFileVersionClient
            If Me.FileVersionClient IsNot Nothing Then Return Me.FileVersionClient
            If Me.FileVersionDiscoveryFailure IsNot Nothing Then Throw New FileActionFailedException("VersionCapabilityDiscovery", Nothing, Me.FileVersionDiscoveryFailure)
            Throw New NotSupportedException("File versions require an implemented SDK and an observed enabled server capability.")
        End Function
    End Class
End Namespace
