Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Runtime.ConstrainedExecution
Imports System.Security.Claims
Imports CompuMaster.Ocs.Core
Imports CompuMaster.Ocs.Types
Imports System.Xml.Linq
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports WebDav

Namespace Providers

    ''' <summary>
    ''' WebDAV
    ''' </summary>
    Public Class WebDavDmsProvider
        Inherits BaseDmsProvider

        Private Shared ReadOnly ChildFolderCountProperty As System.Xml.Linq.XName = System.Xml.Linq.XName.Get("contained-folder-count", "http://nextcloud.org/ns")

        Private WebDavClient As Global.WebDav.WebDavClient
        Private OcsSharingClient As IOcsSharingClient
        Private OcsRootPath As String = "/"

        Friend Property OcsSharingClientForTesting As IOcsSharingClient
            Get
                Return Me.OcsSharingClient
            End Get
            Set(value As IOcsSharingClient)
                Me.OcsSharingClient = value
            End Set
        End Property

        Friend Property OcsRootPathForTesting As String
            Get
                Return Me.OcsRootPath
            End Get
            Set(value As String)
                Me.OcsRootPath = value
            End Set
        End Property

        Private Shared ReadOnly OwnCloudOwnerId As XName = XName.Get("owner-id", "http://owncloud.org/ns")
        Private Shared ReadOnly OwnCloudOwnerDisplayName As XName = XName.Get("owner-display-name", "http://owncloud.org/ns")
        Private Shared ReadOnly DavOwner As XName = XName.Get("owner", "DAV:")

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsAsynchronousIo As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.WebDAV
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "WebDAV"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlMustBeCustomized
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType
            Get
                Return UserCustomerReferenceType.WithoutCustomerReference
            End Get
        End Property

        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Return loginCredentials.BaseUrl 'e.g. https://extranet.compumaster.de/owncloud/remote.php/dav/files/gitlab-runner-bierdeckel/
        End Function

        Public Overrides ReadOnly Property BrowseInRootFolderName() As String = ""

        ''' <summary>
        ''' The webdav url that is considered as root directory (always contains a trailing slash "/")
        ''' </summary>
        ''' <returns></returns>
        Public Property CustomWebApiUrl As String

        Private Shared Function CreateHttpClient(ignoreSslErrors As Boolean, ByVal params As WebDavClientParams) As System.Net.Http.HttpClient
            Dim Handler As HttpClientHandler
            If ignoreSslErrors = True Then
                Handler = New HttpClientHandler() With {
                    .ServerCertificateCustomValidationCallback = Function(message, cert, chain, errors) True
                    }
            Else
                Handler = New HttpClientHandler()
            End If
            Return CreateConfiguredHttpClient(Handler, params)
        End Function

        Private Shared Function CreateConfiguredHttpClient(httpHandler As HttpClientHandler, ByVal params As WebDavClientParams) As System.Net.Http.HttpClient
            With httpHandler
                .AutomaticDecompression = DecompressionMethods.Deflate Or DecompressionMethods.GZip
                .PreAuthenticate = params.PreAuthenticate
                .UseDefaultCredentials = params.UseDefaultCredentials
                .UseProxy = params.UseProxy
            End With

            If params.Credentials IsNot Nothing Then
                httpHandler.Credentials = params.Credentials
                httpHandler.UseDefaultCredentials = False
            End If

            If params.Proxy IsNot Nothing Then
                httpHandler.Proxy = params.Proxy
            End If

            Dim httpClient = New HttpClient(New ServiceRequestPolicy(params.BaseAddress, httpHandler), True) With {
                .BaseAddress = params.BaseAddress
            }

            If params.Timeout.HasValue Then
                httpClient.Timeout = params.Timeout.Value
            End If

            For Each header In params.DefaultRequestHeaders
                httpClient.DefaultRequestHeaders.Add(header.Key, header.Value)
            Next

            Return httpClient
        End Function

        Public Overloads Sub Authorize(loginCredentials As WebDavLoginCredentials)
            Me.Authorize(loginCredentials, False)
        End Sub

        Public Overloads Sub Authorize(loginCredentials As WebDavLoginCredentials, ignoreSslErrors As Boolean)
            Dim Url As String = Me.CustomizedWebApiUrl(loginCredentials)
            Dim ClientParams As New Global.WebDav.WebDavClientParams() With
            {
            .BaseAddress = New System.Uri(Url),
            .Credentials = New System.Net.NetworkCredential(loginCredentials.Username, loginCredentials.Password)
            }
            Dim HttpClient = CreateHttpClient(True, ClientParams)
            Me.WebDavClient = New Global.WebDav.WebDavClient(HttpClient) 'uses copy of method ConfiguredHttpClient from WebDavClient 
            'Me.WebDavClient = New Global.WebDav.WebDavClient(ClientParams) 'uses internal method ConfiguredHttpClient from WebDavClient
            Me._AuthorizedUser = loginCredentials.Username
            If Url.EndsWith("/") Then
                Me.CustomWebApiUrl = Url
            Else
                Me.CustomWebApiUrl = Url & "/"
            End If
            'Force request to evaluate correct credentials already on runtime of this method
            Dim PropfindTask As Task(Of Global.WebDav.PropfindResponse) = Me.WebDavClient.Propfind(ClientParams.BaseAddress)
            PropfindTask.Wait()
            If PropfindTask.IsCompleted AndAlso PropfindTask.Result.IsSuccessful Then
                Me.TryInitializeOcsSharingClient(Url, loginCredentials.Username, loginCredentials.Password)
            Else
                If PropfindTask.Exception IsNot Nothing Then
                    Throw New InvalidOperationException(ProviderStrings.Format("AuthentificationForUserFailed", loginCredentials.Username, PropfindTask.Exception.Message))
                ElseIf PropfindTask.Result.StatusCode = 401 Then
                    Throw New Data.DmsUserAuthenticationException(ProviderStrings.Format("AuthentificationForUserFailed2", loginCredentials.Username, PropfindTask.Result.StatusCode, PropfindTask.Result.Description))
                Else
                    Throw New InvalidOperationException(ProviderStrings.Format("AuthentificationForUserFailed2", loginCredentials.Username, PropfindTask.Result.StatusCode, PropfindTask.Result.Description))
                End If
            End If
        End Sub

        Private Sub TryInitializeOcsSharingClient(webDavUrl As String, userID As String, password As String)
            Me.OcsSharingClient = Nothing
            Me.OcsRootPath = "/"

            Dim OcsBaseUrl As String = Nothing
            Dim RemoteRootPath As String = Nothing
            If Not TryGetOcsConnectionInfo(webDavUrl, OcsBaseUrl, RemoteRootPath) Then
                Return
            End If

            Try
                Dim Candidate As IOcsSharingClient = New OcsSharingClientAdapter(OcsBaseUrl, userID, password)
                Candidate.ProbeCapabilities()
                If Candidate.Capabilities IsNot Nothing AndAlso Candidate.Capabilities.SupportsAnySharing Then
                    Me.OcsSharingClient = Candidate
                    Me.OcsRootPath = RemoteRootPath
                End If
            Catch
                'The authenticated endpoint is still a valid generic WebDAV endpoint when OCS is unavailable.
                Me.OcsSharingClient = Nothing
                Me.OcsRootPath = "/"
            End Try
        End Sub

        Friend Shared Function TryGetOcsConnectionInfo(webDavUrl As String, ByRef ocsBaseUrl As String, ByRef remoteRootPath As String) As Boolean
            ocsBaseUrl = Nothing
            remoteRootPath = Nothing

            Dim WebDavUri As Uri = Nothing
            If Not Uri.TryCreate(webDavUrl, UriKind.Absolute, WebDavUri) Then
                Return False
            End If

            Dim AbsolutePath As String = WebDavUri.AbsolutePath
            Dim MatchedMarker As String = Nothing
            Dim MarkerPosition As Integer = -1
            Dim RelativeRootPath As String

            For Each CandidateMarker As String In New String() {"/remote.php/dav/files/", "/dav/files/"}
                MarkerPosition = AbsolutePath.IndexOf(CandidateMarker, StringComparison.OrdinalIgnoreCase)
                If MarkerPosition >= 0 Then
                    MatchedMarker = CandidateMarker
                    Exit For
                End If
            Next

            If MarkerPosition >= 0 Then
                Dim PathBehindMarker As String = AbsolutePath.Substring(MarkerPosition + MatchedMarker.Length)
                Dim FirstSeparatorPosition As Integer = PathBehindMarker.IndexOf("/"c)
                If FirstSeparatorPosition < 0 Then
                    RelativeRootPath = String.Empty
                Else
                    RelativeRootPath = PathBehindMarker.Substring(FirstSeparatorPosition + 1)
                End If
            Else
                For Each CandidateMarker As String In New String() {"/remote.php/webdav", "/webdav"}
                    MarkerPosition = FindPathMarker(AbsolutePath, CandidateMarker)
                    If MarkerPosition >= 0 Then
                        MatchedMarker = CandidateMarker
                        Exit For
                    End If
                Next
                If MatchedMarker Is Nothing Then
                    Return False
                End If
                RelativeRootPath = AbsolutePath.Substring(MarkerPosition + MatchedMarker.Length).TrimStart("/"c)
            End If

            Dim BaseUriBuilder As New UriBuilder(WebDavUri) With {
                .Path = AbsolutePath.Substring(0, MarkerPosition).TrimEnd("/"c),
                .Query = String.Empty,
                .Fragment = String.Empty
            }
            ocsBaseUrl = BaseUriBuilder.Uri.AbsoluteUri.TrimEnd("/"c)

            RelativeRootPath = WebUtility.UrlDecode(RelativeRootPath).Trim("/"c)
            If RelativeRootPath.Length = 0 Then
                remoteRootPath = "/"
            Else
                remoteRootPath = "/" & RelativeRootPath
            End If
            Return True
        End Function

        Private Shared Function FindPathMarker(absolutePath As String, marker As String) As Integer
            Dim MarkerPosition As Integer = absolutePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase)
            While MarkerPosition >= 0
                Dim PositionBehindMarker As Integer = MarkerPosition + marker.Length
                If PositionBehindMarker = absolutePath.Length OrElse absolutePath(PositionBehindMarker) = "/"c Then
                    Return MarkerPosition
                End If
                MarkerPosition = absolutePath.IndexOf(marker, MarkerPosition + 1, StringComparison.OrdinalIgnoreCase)
            End While
            Return -1
        End Function

        Private _AuthorizedUser As String

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
            'no caches present, so nothing to do here
        End Sub

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            If remotePath Is Nothing Then
                Throw New ArgumentNullException(NameOf(remotePath))
            End If
            Dim PropfindParams As Global.WebDav.PropfindParameters = CreateResourcePropfindParameters(Global.WebDav.ApplyTo.Propfind.ResourceOnly)
            Dim PropfindTask As Task(Of Global.WebDav.PropfindResponse) = Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remotePath, PropfindParams)
            PropfindTask.Wait()
            If PropfindTask.Result.StatusCode = 400 OrElse PropfindTask.Result.StatusCode = 501 Then
                PropfindTask = Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remotePath, New Global.WebDav.PropfindParameters With {.ApplyTo = Global.WebDav.ApplyTo.Propfind.ResourceOnly})
                PropfindTask.Wait()
            End If
            If PropfindTask.IsCompleted AndAlso PropfindTask.Result.IsSuccessful Then
                Dim Res As Global.WebDav.WebDavResource = PropfindTask.Result.Resources(0)
                Dim Result As DmsResourceItem = Me.CreateDmsResourceItem(Res)
                Me.ApplyOcsSharingMetadata(Result, Me.TryLoadOcsShares(Result.FullName, includeSubFiles:=False))
                Return Result
            Else
                If PropfindTask.Exception IsNot Nothing Then
                    Throw New InvalidOperationException(ProviderStrings.Format("ListingOfWebDAVResourceAtFailed", remotePath, PropfindTask.Exception.Message))
                ElseIf PropfindTask.Result.StatusCode = 404 Then
                    'Directory/File not found
                    Return Nothing
                Else
                    Throw New InvalidOperationException(ProviderStrings.Format("ListingOfWebDAVResourceAtFailed2", remotePath, PropfindTask.Result.StatusCode, PropfindTask.Result.Description))
                End If
            End If
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function ListRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            If remotePath Is Nothing Then Throw New ArgumentNullException(NameOf(remotePath))
            Dim response = Await Me.PropfindResourceAsync(remotePath, Global.WebDav.ApplyTo.Propfind.ResourceOnly, cancellationToken).ConfigureAwait(False)
            If response.IsSuccessful Then
                Dim result = Me.CreateDmsResourceItem(response.Resources(0))
                Dim shares = Await Me.TryLoadOcsSharesAsync(result.FullName, False, cancellationToken).ConfigureAwait(False)
                Me.ApplyOcsSharingMetadata(result, shares)
                Return result
            End If
            If response.StatusCode = 404 Then Return Nothing
            Throw New InvalidOperationException(ProviderStrings.Format("ListingOfWebDAVResourceAtFailed2", remotePath, response.StatusCode, response.Description),
                                                New ResponseStatusCodeException(response.StatusCode, response.Description))
        End Function

        Public Overrides Function FindCollectionById(id As String) As DmsResourceItem
            Throw New NotImplementedException
        End Function

        Public Overrides Function FindFolderById(id As String) As DmsResourceItem
            Throw New NotImplementedException
        End Function

        Public Overrides Function FindFileById(id As String) As DmsResourceItem
            Throw New NotImplementedException
        End Function

        Private Function CreateDmsResourceItem(res As Global.WebDav.WebDavResource) As DmsResourceItem
            Dim Result As New DmsResourceItem With {
                        .Name = res.DisplayName,
                        .CreatedOnLocalTime = res.CreationDate.GetValueOrDefault,
                        .LastModificationOnLocalTime = res.LastModifiedDate.GetValueOrDefault,
                        .IsHidden = res.IsHidden,
                        .ContentLength = res.ContentLength.GetValueOrDefault,
                        .ProviderSpecificHashOrETag = res.ETag
                    }
            'Assign additional fields
            If res.Uri.ToString.StartsWith(Me.CustomWebApiUrl) Then
                Result.FullName = res.Uri.ToString.Substring(Me.CustomWebApiUrl.Length)
            ElseIf res.Uri.ToString.StartsWith(New System.Uri(Me.CustomWebApiUrl).PathAndQuery) Then
                Result.FullName = res.Uri.ToString.Substring(New System.Uri(Me.CustomWebApiUrl).PathAndQuery.Length)
                If Result.FullName.EndsWith(Me.DirectorySeparator) Then
                    Result.FullName = Result.FullName.Substring(0, Result.FullName.Length - 1)
                End If
            Else
                Throw New InvalidOperationException(ProviderStrings.Format("SubItemsOfFoundOutsideOfThisPath", Me.CustomWebApiUrl, res.Uri.ToString))
            End If
            If Result.Name = Nothing AndAlso Result.FullName <> Nothing Then
                Result.Name = Result.FullName.Substring(Result.FullName.LastIndexOf(Me.DirectorySeparator) + 1)
            End If
            If res.IsCollection AndAlso (Result.FullName = Nothing OrElse Result.FullName = Me.DirectorySeparator) Then
                Result.ItemType = DmsResourceItem.ItemTypes.Root
            ElseIf res.IsCollection Then
                Result.ItemType = DmsResourceItem.ItemTypes.Folder
            Else
                Result.ItemType = DmsResourceItem.ItemTypes.File
            End If
            If res.IsCollection AndAlso res.Properties IsNot Nothing Then
                For Each prop As Global.WebDav.WebDavProperty In res.Properties
                    If prop.Name <> ChildFolderCountProperty Then Continue For
                    If res.PropertyStatuses IsNot Nothing AndAlso res.PropertyStatuses.Any(Function(status) status.Name = ChildFolderCountProperty AndAlso Not status.IsSuccessful) Then Continue For
                    Dim ChildCount As Integer
                    If Integer.TryParse(prop.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, ChildCount) AndAlso ChildCount >= 0 Then
                        Result.ChildDirectoryCount = ChildCount
                        Result.HasChildDirectories = ChildCount > 0
                    End If
                Next
            End If
            'Normalize field content
            If Result.ItemType = DmsResourceItem.ItemTypes.Root OrElse Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(res.Uri.ToString) = Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(Me.CustomWebApiUrl) Then
                Result.Name = ""
            ElseIf Result.Name <> Nothing AndAlso Result.Name.EndsWith(Me.DirectorySeparator) Then
                Result.Name = Result.Name.Substring(0, Result.Name.Length - 1)
            End If
            If Result.FullName.EndsWith(Me.DirectorySeparator) Then
                Result.FullName = Result.FullName.Substring(0, Result.FullName.Length - 1)
            End If
            'Assign calculated fields
            Dim LastDirSeparatorPosition As Integer = Result.FullName.LastIndexOf(Me.DirectorySeparator)
            If LastDirSeparatorPosition >= 0 Then
                Result.Folder = Result.FullName.Substring(0, LastDirSeparatorPosition)
            Else
                Result.Folder = ""
            End If
            'Decoded URLs
            Result.Name = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.Name))
            Result.Folder = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.Folder))
            Result.Collection = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.Collection))
            Result.FullName = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.FullName))
            Result.ExtendedInfosOwner = ResourceOwner(res)
            Return Result
        End Function

        Private Function TryLoadOcsShares(remotePath As String, includeSubFiles As Boolean) As List(Of OcsShareRecord)
            If Me.OcsSharingClient Is Nothing Then
                Return Nothing
            End If

            Try
                Return Me.OcsSharingClient.GetShares(Me.ToOcsPath(remotePath), includeReshares:=True, includeSubFiles:=includeSubFiles)
            Catch
                'Sharing metadata is supplemental and must not make otherwise valid WebDAV browsing fail.
                Return Nothing
            End Try
        End Function

        Private Async Function TryLoadOcsSharesAsync(remotePath As String, includeSubFiles As Boolean, cancellationToken As CancellationToken) As Task(Of List(Of OcsShareRecord))
            cancellationToken.ThrowIfCancellationRequested()
            If Me.OcsSharingClient Is Nothing Then Return Nothing
            Dim result As List(Of OcsShareRecord) = Nothing
            'The current OCS client is synchronous. Keep it off the UI thread and serialize it with other fallback operations.
            Await Me.RunSynchronousFallbackAsync(Sub() result = Me.TryLoadOcsShares(remotePath, includeSubFiles), cancellationToken).ConfigureAwait(False)
            cancellationToken.ThrowIfCancellationRequested()
            Return result
        End Function

        Private Async Function PropfindResourceAsync(remotePath As String, depth As Global.WebDav.ApplyTo.Propfind, cancellationToken As CancellationToken) As Task(Of Global.WebDav.PropfindResponse)
            cancellationToken.ThrowIfCancellationRequested()
            Dim parameters = CreateResourcePropfindParameters(depth)
            parameters.CancellationToken = cancellationToken
            Dim response = Await Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remotePath, parameters).ConfigureAwait(False)
            If response.StatusCode = 400 OrElse response.StatusCode = 501 Then
                cancellationToken.ThrowIfCancellationRequested()
                parameters = New Global.WebDav.PropfindParameters With {.ApplyTo = depth, .CancellationToken = cancellationToken}
                response = Await Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remotePath, parameters).ConfigureAwait(False)
            End If
            cancellationToken.ThrowIfCancellationRequested()
            Return response
        End Function

        Private Shared Function CreateResourcePropfindParameters(depth As Global.WebDav.ApplyTo.Propfind) As Global.WebDav.PropfindParameters
            'Some servers silently omit requested owner properties from allprop/include responses.
            Return New Global.WebDav.PropfindParameters With {
                .ApplyTo = depth,
                .RequestType = Global.WebDav.PropfindRequestType.NamedProperties,
                .CustomProperties = New XName() {
                    XName.Get("lockdiscovery", "DAV:"),
                    XName.Get("getcontentlanguage", "DAV:"),
                    XName.Get("getcontentlength", "DAV:"),
                    XName.Get("getcontenttype", "DAV:"),
                    XName.Get("creationdate", "DAV:"),
                    XName.Get("displayname", "DAV:"),
                    XName.Get("getetag", "DAV:"),
                    XName.Get("getlastmodified", "DAV:"),
                    XName.Get("ishidden", "DAV:"),
                    XName.Get("iscollection", "DAV:"),
                    XName.Get("resourcetype", "DAV:"),
                    ChildFolderCountProperty,
                    OwnCloudOwnerId,
                    OwnCloudOwnerDisplayName,
                    DavOwner
                }
            }
        End Function

        Private Shared Function ResourceOwner(resource As Global.WebDav.WebDavResource) As DmsUser
            Dim ownerId As String = PropertyText(resource, OwnCloudOwnerId)
            Dim displayName As String = PropertyText(resource, OwnCloudOwnerDisplayName)
            If ownerId Is Nothing Then
                ownerId = DavOwnerPrincipal(resource)
            End If
            If ownerId Is Nothing AndAlso displayName Is Nothing Then Return Nothing
            Return New DmsUser With {.ID = ownerId, .DisplayName = displayName}
        End Function

        Private Shared Function PropertyText(resource As Global.WebDav.WebDavResource, name As XName) As String
            Dim propertyValue As Global.WebDav.WebDavProperty = resource.Properties.FirstOrDefault(Function(item) item.Name = name)
            If propertyValue Is Nothing Then Return Nothing
            Try
                Dim propertyXml As XElement = XElement.Parse("<root>" & propertyValue.Value & "</root>")
                If propertyXml.HasElements Then Return Nothing
                Dim value As String = propertyXml.Value.Trim()
                Return If(value.Length = 0, Nothing, value)
            Catch ex As System.Xml.XmlException
                Return Nothing
            End Try
        End Function

        Private Function ToOcsPath(remotePath As String) As String
            Dim RelativePath As String = Tools.NotNullOrEmptyStringValue(remotePath).Trim("/"c)
            Dim RootPath As String = Tools.NotNullOrEmptyStringValue(Me.OcsRootPath).Trim("/"c)
            If RootPath.Length = 0 AndAlso RelativePath.Length = 0 Then
                Return "/"
            ElseIf RootPath.Length = 0 Then
                Return "/" & RelativePath
            ElseIf RelativePath.Length = 0 Then
                Return "/" & RootPath
            Else
                Return "/" & RootPath & "/" & RelativePath
            End If
        End Function

        Friend Sub ApplyOcsSharingMetadata(dmsResource As DmsResourceItem, shares As IEnumerable(Of OcsShareRecord))
            dmsResource.ExtendedInfosLinks = New List(Of DmsLink)
            dmsResource.ExtendedInfosGroupSharings = New List(Of DmsShareForGroup)
            dmsResource.ExtendedInfosUserSharings = New List(Of DmsShareForUser)
            dmsResource.ExtendedInfosHasGroupSharings = False
            dmsResource.ExtendedInfosHasHiddenGroupSharings = False
            dmsResource.ExtendedInfosHasUserSharings = False
            dmsResource.ExtendedInfosHasHiddenUserSharings = False
            dmsResource.ExtendedInfosIsShared = False

            If shares Is Nothing Then
                Return
            End If

            Dim ExpectedPath As String = NormalizeOcsPath(Me.ToOcsPath(dmsResource.FullName))
            For Each ShareInfo As OcsShareRecord In shares
                If ShareInfo Is Nothing OrElse Not String.Equals(NormalizeOcsPath(ShareInfo.TargetPath), ExpectedPath, StringComparison.Ordinal) Then
                    Continue For
                End If

                dmsResource.ExtendedInfosIsShared = True
                If dmsResource.ExtendedInfosOwner.ID = Nothing AndAlso ShareInfo.AdvancedProperties IsNot Nothing AndAlso ShareInfo.AdvancedProperties.Owner <> Nothing Then
                    dmsResource.ExtendedInfosOwner = New DmsUser With {
                        .ID = ShareInfo.AdvancedProperties.Owner,
                        .DisplayName = ShareInfo.AdvancedProperties.DisplaynameOwner
                    }
                End If

                Select Case ShareInfo.Type
                    Case OcsShareType.User
                        dmsResource.ExtendedInfosUserSharings.Add(Me.CreateDmsUserShare(dmsResource, ShareInfo))
                    Case OcsShareType.Group
                        dmsResource.ExtendedInfosGroupSharings.Add(Me.CreateDmsGroupShare(dmsResource, ShareInfo))
                    Case OcsShareType.Link
                        dmsResource.ExtendedInfosLinks.Add(Me.CreateDmsLink(dmsResource, ShareInfo))
                End Select
            Next

            dmsResource.ExtendedInfosHasUserSharings = dmsResource.ExtendedInfosUserSharings.Count > 0
            dmsResource.ExtendedInfosHasGroupSharings = dmsResource.ExtendedInfosGroupSharings.Count > 0
        End Sub

        Private Shared Function NormalizeOcsPath(path As String) As String
            Dim Result As String = WebUtility.UrlDecode(Tools.NotNullOrEmptyStringValue(path)).Trim()
            If Not Result.StartsWith("/", StringComparison.Ordinal) Then
                Result = "/" & Result
            End If
            If Result.Length > 1 Then
                Result = Result.TrimEnd("/"c)
            End If
            Return Result
        End Function

        Private Function CreateDmsUserShare(dmsResource As DmsResourceItem, shareInfo As OcsShareRecord) As DmsShareForUser
            Dim Permissions As Integer = Convert.ToInt32(shareInfo.Permissions)
            Return New DmsShareForUser(
                dmsResource,
                New DmsUser With {
                    .ID = shareInfo.SharedWith,
                    .DisplayName = shareInfo.AdvancedProperties?.SharedWithDisplayname
                },
                HasOcsPermission(Permissions, OcsPermission.Read),
                HasOcsPermission(Permissions, OcsPermission.Read),
                HasOcsPermission(Permissions, OcsPermission.Update),
                HasOcsPermission(Permissions, OcsPermission.Create),
                HasOcsPermission(Permissions, OcsPermission.Delete),
                HasOcsPermission(Permissions, OcsPermission.Share))
        End Function

        Private Function CreateDmsGroupShare(dmsResource As DmsResourceItem, shareInfo As OcsShareRecord) As DmsShareForGroup
            Dim Permissions As Integer = Convert.ToInt32(shareInfo.Permissions)
            Return New DmsShareForGroup(
                dmsResource,
                New DmsGroup With {
                    .ID = shareInfo.SharedWith,
                    .Name = shareInfo.AdvancedProperties?.SharedWithDisplayname
                },
                HasOcsPermission(Permissions, OcsPermission.Read),
                HasOcsPermission(Permissions, OcsPermission.Read),
                HasOcsPermission(Permissions, OcsPermission.Update),
                HasOcsPermission(Permissions, OcsPermission.Create),
                HasOcsPermission(Permissions, OcsPermission.Delete),
                HasOcsPermission(Permissions, OcsPermission.Share))
        End Function

        Private Function CreateDmsLink(dmsResource As DmsResourceItem, shareInfo As OcsShareRecord) As DmsLink
            Dim Result As New DmsLink(dmsResource, shareInfo.ShareId.ToString(Globalization.CultureInfo.InvariantCulture), Me, AddressOf FillOcsLinkDetails)
            Me.InitializeDmsLink(Result, shareInfo, password:=Nothing)
            Return Result
        End Function

        Private Sub InitializeDmsLink(dmsLink As DmsLink, shareInfo As OcsShareRecord, password As String)
            Dim Permissions As Integer = Convert.ToInt32(shareInfo.Permissions)
            dmsLink.Name = shareInfo.Name
            dmsLink.Initialize(
                password,
                shareInfo.Expiration,
                Nothing,
                Nothing,
                Nothing,
                Nothing,
                Nothing,
                Nothing,
                Nothing,
                shareInfo.Url,
                Nothing,
                Nothing,
                HasOcsPermission(Permissions, OcsPermission.Read),
                HasOcsPermission(Permissions, OcsPermission.Read),
                HasOcsPermission(Permissions, OcsPermission.Update),
                HasOcsPermission(Permissions, OcsPermission.Create),
                HasOcsPermission(Permissions, OcsPermission.Delete),
                HasOcsPermission(Permissions, OcsPermission.Share))
        End Sub

        Private Shared Sub FillOcsLinkDetails(provider As Object, id As String, dmsLink As DmsLink)
            Dim WebDavProvider As WebDavDmsProvider = CType(provider, WebDavDmsProvider)
            Dim ShareID As Integer = ParseOcsShareID(id)
            Dim Shares As List(Of OcsShareRecord) = WebDavProvider.RequireOcsSharingClient().GetShares(WebDavProvider.ToOcsPath(dmsLink.ParentDmsResourceItem.FullName), True, False)
            Dim MatchingShare As OcsShareRecord = Nothing
            For Each ShareInfo As OcsShareRecord In Shares
                If ShareInfo.ShareId = ShareID AndAlso ShareInfo.Type = OcsShareType.Link Then
                    MatchingShare = ShareInfo
                    Exit For
                End If
            Next
            If MatchingShare Is Nothing Then
                Throw New KeyNotFoundException(ProviderStrings.Format("OCSLinkShareWasNotFound", id))
            End If
            WebDavProvider.InitializeDmsLink(dmsLink, MatchingShare, password:=Nothing)
        End Sub

        Private Shared Function HasOcsPermission(permissions As Integer, permission As OcsPermission) As Boolean
            Dim PermissionValue As Integer = Convert.ToInt32(permission)
            Return (permissions And PermissionValue) = PermissionValue
        End Function

        Private Shared Function DavOwnerPrincipal(resource As Global.WebDav.WebDavResource) As String
            Dim ownerProperty As Global.WebDav.WebDavProperty = resource.Properties.FirstOrDefault(Function(item) item.Name = DavOwner)
            If ownerProperty Is Nothing OrElse String.IsNullOrWhiteSpace(ownerProperty.Value) Then Return Nothing
            Try
                Dim ownerXml As XElement = XElement.Parse("<root>" & ownerProperty.Value & "</root>")
                Dim principalHref As XElement = ownerXml.Descendants(XName.Get("href", "DAV:")).FirstOrDefault()
                If principalHref Is Nothing Then Return Nothing
                Return If(String.IsNullOrWhiteSpace(principalHref.Value), Nothing, principalHref.Value.Trim())
            Catch ex As System.Xml.XmlException
                Return Nothing
            End Try
        End Function

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Dim PropfindParams As Global.WebDav.PropfindParameters = CreateResourcePropfindParameters(Global.WebDav.ApplyTo.Propfind.ResourceAndChildren)
            Dim PropfindTask As Task(Of Global.WebDav.PropfindResponse) = Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remoteFolderPath, PropfindParams)
            PropfindTask.Wait()
            If PropfindTask.Result.StatusCode = 400 OrElse PropfindTask.Result.StatusCode = 501 Then
                PropfindTask = Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remoteFolderPath, New Global.WebDav.PropfindParameters With {.ApplyTo = Global.WebDav.ApplyTo.Propfind.ResourceAndChildren})
                PropfindTask.Wait()
            End If
            If PropfindTask.IsCompleted AndAlso PropfindTask.Result.IsSuccessful Then
                Dim Result As New List(Of DmsResourceItem)
                Dim Shares As List(Of OcsShareRecord) = Me.TryLoadOcsShares(remoteFolderPath, includeSubFiles:=True)
                For Each res In PropfindTask.Result.Resources
                    Dim AddThisItem As Boolean = False
                    Select Case searchType
                        Case SearchItemType.AllItems
                            AddThisItem = True
                        Case SearchItemType.Collections
                            AddThisItem = False
                        Case SearchItemType.Folders
                            AddThisItem = res.IsCollection
                        Case SearchItemType.Files
                            AddThisItem = Not res.IsCollection
                    End Select
                    If AddThisItem Then
                        Dim NewItem As DmsResourceItem = Me.CreateDmsResourceItem(res)
                        If NewItem.ItemType = DmsResourceItem.ItemTypes.Folder AndAlso Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(NewItem.FullName) = Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(remoteFolderPath) Then
                            'don't list folder item
                        Else
                            Me.ApplyOcsSharingMetadata(NewItem, Shares)
                            Result.Add(NewItem)
                        End If
                    End If
                Next
                Return Result
            Else
                If PropfindTask.Exception IsNot Nothing Then
                    Throw New InvalidOperationException(ProviderStrings.Format("ListingOfWebDAVResourceAtFailed", remoteFolderPath, PropfindTask.Exception.Message), PropfindTask.Exception)
                ElseIf PropfindTask.Result.StatusCode = 404 Then
                    Throw New DirectoryNotFoundException(remoteFolderPath, New ResponseStatusCodeException(PropfindTask.Result.StatusCode, PropfindTask.Result.Description))
                Else
                    Throw New InvalidOperationException(ProviderStrings.Format("ListingOfWebDAVResourceAtFailed2", remoteFolderPath, PropfindTask.Result.StatusCode, PropfindTask.Result.Description), New ResponseStatusCodeException(PropfindTask.Result.StatusCode, PropfindTask.Result.Description))
                End If
            End If
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function ListAllRemoteItemsAsync(remoteFolderPath As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Dim response = Await Me.PropfindResourceAsync(remoteFolderPath, Global.WebDav.ApplyTo.Propfind.ResourceAndChildren, cancellationToken).ConfigureAwait(False)
            If Not response.IsSuccessful Then
                Dim failure As New ResponseStatusCodeException(response.StatusCode, response.Description)
                If response.StatusCode = 404 Then Throw New DirectoryNotFoundException(remoteFolderPath, failure)
                Throw New InvalidOperationException(ProviderStrings.Format("ListingOfWebDAVResourceAtFailed2", remoteFolderPath, response.StatusCode, response.Description), failure)
            End If
            Dim result As New List(Of DmsResourceItem)
            Dim shares = Await Me.TryLoadOcsSharesAsync(remoteFolderPath, True, cancellationToken).ConfigureAwait(False)
            For Each resource In response.Resources
                cancellationToken.ThrowIfCancellationRequested()
                Dim include As Boolean
                Select Case searchType
                    Case SearchItemType.AllItems
                        include = True
                    Case SearchItemType.Folders
                        include = resource.IsCollection
                    Case SearchItemType.Files
                        include = Not resource.IsCollection
                    Case SearchItemType.Collections
                        include = False
                    Case Else
                        Throw New ArgumentOutOfRangeException(NameOf(searchType))
                End Select
                If include Then
                    Dim item As DmsResourceItem = Me.CreateDmsResourceItem(resource)
                    If item.ItemType <> DmsResourceItem.ItemTypes.Folder OrElse Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(item.FullName) <> Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(remoteFolderPath) Then
                        Me.ApplyOcsSharingMetadata(item, shares)
                        result.Add(item)
                    End If
                End If
            Next
            Return result
        End Function

        Private Function PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(path As String) As String
            If path = Nothing OrElse path = "/" Then
                Return ""
            ElseIf path.EndsWith(Me.DirectorySeparator) Then
                Return path
            Else
                Return path & Me.DirectorySeparator
            End If
        End Function

        'Public Overrides Function ListAllCollectionItems(remoteFolderPath As String) As List(Of DmsResourceItem)
        '    Return MyBase.ListAllCollectionItems(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllCollectionNames(remoteFolderPath As String) As List(Of String)
        '    Return MyBase.ListAllCollectionNames(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFileItems(remoteFolderPath As String) As List(Of DmsResourceItem)
        '    Return MyBase.ListAllFileItems(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFileNames(remoteFolderPath As String) As List(Of String)
        '    Return MyBase.ListAllFileNames(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFolderItems(remoteFolderPath As String) As List(Of DmsResourceItem)
        '    Return MyBase.ListAllFolderItems(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFolderNames(remoteFolderPath As String) As List(Of String)
        '    Return MyBase.ListAllFolderNames(remoteFolderPath)
        'End Function

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Return New WebDavLoginCredentials
        End Function

        ''' <inheritdoc/>
        Public Overrides Sub UploadFile(remoteFilePath As String, localFilePath As String)
            Dim PutParams As New Global.WebDav.PutFileParameters
            Dim fs As System.IO.FileStream = Nothing
            Try
                fs = System.IO.File.OpenRead(localFilePath)
                Dim UploadTask = Me.WebDavClient.PutFile(Me.CustomWebApiUrl & remoteFilePath, fs, PutParams)
                UploadTask.Wait()
                CheckTaskResultForErrors(UploadTask, Nothing, remoteFilePath, ProviderStrings.GetText("UploadFailed"), ExceptionTypeForItemType.File)
            Finally
                If fs IsNot Nothing Then
                    fs.Close()
                    fs.Dispose()
                End If
            End Try
        End Sub

        ''' <inheritdoc/>
        Public Overrides Function UploadFileWithProgressAsync(remoteFilePath As String, localFilePath As String, progress As IProgress(Of DmsTransferProgress), Optional cancellationToken As CancellationToken = Nothing) As Task
            If progress Is Nothing Then Return Me.UploadFileAsync(remoteFilePath, localFilePath, cancellationToken)
            Return Me.UploadFileWithProgressAsync(remoteFilePath, Function() New System.IO.FileStream(localFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read, 81920, System.IO.FileOptions.Asynchronous Or System.IO.FileOptions.SequentialScan), progress, cancellationToken)
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function UploadFileAsync(remoteFilePath As String, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Using input As System.IO.FileStream = System.IO.File.OpenRead(localFilePath)
                Dim parameters As New Global.WebDav.PutFileParameters With {.CancellationToken = cancellationToken}
                Dim request = Me.WebDavClient.PutFile(Me.CustomWebApiUrl & remoteFilePath, input, parameters)
                Await request.ConfigureAwait(False)
                Await CheckTaskResultForErrorsAsync(request.Result, Nothing, remoteFilePath, ProviderStrings.GetText("UploadFailed"), ExceptionTypeForItemType.File, cancellationToken).ConfigureAwait(False)
            End Using
        End Function

        ''' <inheritdoc/>
        Public Overrides Async Function UploadFileAsync(remoteFilePath As String, binaryData As Func(Of System.IO.Stream), Optional cancellationToken As CancellationToken = Nothing) As Task
            If binaryData Is Nothing Then Throw New ArgumentNullException(NameOf(binaryData))
            Using input As System.IO.Stream = binaryData()
                Dim parameters As New Global.WebDav.PutFileParameters With {.CancellationToken = cancellationToken}
                Dim request = Me.WebDavClient.PutFile(Me.CustomWebApiUrl & remoteFilePath, input, parameters)
                Await request.ConfigureAwait(False)
                Await CheckTaskResultForErrorsAsync(request.Result, Nothing, remoteFilePath, ProviderStrings.GetText("UploadFailed"), ExceptionTypeForItemType.File, cancellationToken).ConfigureAwait(False)
            End Using
        End Function

        ''' <inheritdoc/>
        Public Overrides Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream))
            Dim PutParams As New Global.WebDav.PutFileParameters
            Dim UploadTask = Me.WebDavClient.PutFile(Me.CustomWebApiUrl & remoteFilePath, binaryData(), PutParams)
            UploadTask.Wait()
            CheckTaskResultForErrors(UploadTask, Nothing, remoteFilePath, ProviderStrings.GetText("UploadFailed"), ExceptionTypeForItemType.File)
        End Sub


        Protected Shared Function StreamToByteArray(inputStream As System.IO.Stream) As Byte()
            Dim bytes = New Byte(16383) {}
            Using memoryStream = New System.IO.MemoryStream()
                Dim count As Integer
                Do
                    count = inputStream.Read(bytes, 0, bytes.Length)
                    memoryStream.Write(bytes, 0, count)
                Loop While count > 0
                Return memoryStream.ToArray()
            End Using
        End Function

        ''' <summary>Writes the successful response contents to a local file.</summary>
        ''' <param name="response">The pending response whose stream is read.</param>
        ''' <param name="localFilePath">The local file to create or overwrite.</param>
        ''' <exception cref="System.IO.IOException">Reading or writing fails, including platform-dependent path conflicts.</exception>
        ''' <exception cref="UnauthorizedAccessException">The local file is inaccessible or its path denotes a directory on a runtime that reports access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">The local parent directory is absent.</exception>
        ''' <exception cref="InvalidOperationException">The response did not complete successfully.</exception>
        Protected Overridable Sub WriteResponseStreamToDisk(response As Task(Of Global.WebDav.WebDavStreamResponse), localFilePath As String)
            response.Wait()
            If response.IsCompleted = False OrElse response.Result.IsSuccessful = False Then Throw New InvalidOperationException(ProviderStrings.GetText("DownloadFailedNotCompletedSuccessfull"))
            Dim FileData As Byte() = StreamToByteArray(response.Result.Stream)
            System.IO.File.WriteAllBytes(localFilePath, FileData)
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?)
            Using response = Me.WebDavClient.GetRawFile(Me.CustomWebApiUrl & remoteFilePath) ' get a file without processing from the server
                response.Wait()
                If response.Result.StatusCode = 404 Then Throw New FileNotFoundException(remoteFilePath, New ResponseStatusCodeException(response.Result.StatusCode, response.Result.Description))
                WriteResponseStreamToDisk(response, localFilePath)
                If lastModificationDateOnLocalTime.HasValue AndAlso lastModificationDateOnLocalTime.Value <> Nothing Then System.IO.File.SetLastWriteTime(localFilePath, lastModificationDateOnLocalTime.Value)
            End Using
        End Sub

        ''' <inheritdoc/>
        Public Overrides Async Function DownloadFileAsync(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?, Optional cancellationToken As CancellationToken = Nothing) As Task
            Dim parameters As New Global.WebDav.GetFileParameters With {.CancellationToken = cancellationToken}
            Dim temporaryPath As String = localFilePath & ".dms-download-" & Guid.NewGuid().ToString("N") & ".tmp"
            Try
                Using response = Await Me.WebDavClient.GetRawFile(Me.CustomWebApiUrl & remoteFilePath, parameters).ConfigureAwait(False)
                    If response.StatusCode = 404 Then Throw New FileNotFoundException(remoteFilePath, New ResponseStatusCodeException(response.StatusCode, response.Description))
                    If Not response.IsSuccessful Then Throw New System.IO.IOException(ProviderStrings.GetText("DownloadFailed"), New ResponseStatusCodeException(response.StatusCode, response.Description))
                    Using output As New System.IO.FileStream(temporaryPath, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None, 81920, True)
                        Await response.Stream.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(False)
                    End Using
                End Using
                cancellationToken.ThrowIfCancellationRequested()
                If System.IO.File.Exists(localFilePath) Then
                    System.IO.File.Replace(temporaryPath, localFilePath, Nothing)
                Else
                    System.IO.File.Move(temporaryPath, localFilePath)
                End If
                If lastModificationDateOnLocalTime.HasValue AndAlso lastModificationDateOnLocalTime.Value <> Nothing Then System.IO.File.SetLastWriteTime(localFilePath, lastModificationDateOnLocalTime.Value)
            Finally
                If System.IO.File.Exists(temporaryPath) Then System.IO.File.Delete(temporaryPath)
            End Try
        End Function

        ''' <summary>Downloads a server-processed remote file to a local destination.</summary>
        ''' <param name="remoteFilePath">The remote source path.</param>
        ''' <param name="localFilePath">The local file to create or overwrite.</param>
        ''' <exception cref="System.IO.IOException">Transfer, staging, or final replacement fails; a directory at the file destination can cause a platform-dependent conflict.</exception>
        ''' <exception cref="UnauthorizedAccessException">Local access is denied, including path conflicts reported as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory is absent.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds platform or runtime limits.</exception>
        ''' <exception cref="ArgumentException">The local destination path is invalid.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        Public Overridable Sub DownloadProcessedFile(remoteFilePath As String, localFilePath As String)
            Using response = Me.WebDavClient.GetProcessedFile(Me.CustomWebApiUrl & remoteFilePath) ' get a file that can be processed by the server
                response.Wait()
                If response.Result.StatusCode = 404 Then Throw New FileNotFoundException(remoteFilePath, New ResponseStatusCodeException(response.Result.StatusCode, response.Result.Description))
                WriteResponseStreamToDisk(response, localFilePath)
            End Using
        End Sub

        ''' <summary>Downloads a server-processed remote file asynchronously.</summary>
        ''' <param name="remoteFilePath">The remote source path.</param>
        ''' <param name="localFilePath">The local destination path.</param>
        ''' <param name="cancellationToken">Cancels the request or response transfer.</param>
        ''' <returns>A task that completes when the processed file is saved.</returns>
        ''' <exception cref="System.IO.IOException">Transfer, staging, or final replacement fails; a directory at the file destination can cause a platform-dependent conflict.</exception>
        ''' <exception cref="UnauthorizedAccessException">Local access is denied, including path conflicts reported as access denied.</exception>
        ''' <exception cref="System.IO.DirectoryNotFoundException">A required local parent directory is absent.</exception>
        ''' <exception cref="System.IO.PathTooLongException">The local path exceeds platform or runtime limits.</exception>
        ''' <exception cref="ArgumentException">The local destination path is invalid.</exception>
        ''' <exception cref="NotSupportedException">The runtime does not support the local path format.</exception>
        Public Overridable Async Function DownloadProcessedFileAsync(remoteFilePath As String, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Dim parameters As New Global.WebDav.GetFileParameters With {.CancellationToken = cancellationToken}
            Dim temporaryPath As String = localFilePath & ".dms-download-" & Guid.NewGuid().ToString("N") & ".tmp"
            Try
                Using response = Await Me.WebDavClient.GetProcessedFile(Me.CustomWebApiUrl & remoteFilePath, parameters).ConfigureAwait(False)
                    If response.StatusCode = 404 Then Throw New FileNotFoundException(remoteFilePath, New ResponseStatusCodeException(response.StatusCode, response.Description))
                    If Not response.IsSuccessful Then Throw New System.IO.IOException(ProviderStrings.GetText("DownloadFailed"), New ResponseStatusCodeException(response.StatusCode, response.Description))
                    Using output As New System.IO.FileStream(temporaryPath, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None, 81920, True)
                        Await response.Stream.CopyToAsync(output, 81920, cancellationToken).ConfigureAwait(False)
                    End Using
                End Using
                cancellationToken.ThrowIfCancellationRequested()
                If System.IO.File.Exists(localFilePath) Then
                    System.IO.File.Replace(temporaryPath, localFilePath, Nothing)
                Else
                    System.IO.File.Move(temporaryPath, localFilePath)
                End If
            Finally
                If System.IO.File.Exists(temporaryPath) Then System.IO.File.Delete(temporaryPath)
            End Try
        End Function

        ''' <summary>
        ''' Check for successful run of a task
        ''' </summary>
        ''' <param name="completedTask"></param>
        ''' <param name="remoteSourcePath">Optional value, required for actions requiring existance of source item</param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <param name="ioExceptionMessage"></param>
        ''' <param name="conflictItemType">Decides on exception type if a HTTP status code 409 is reported</param>
        Protected Sub CheckTaskResultForErrors(completedTask As Task(Of WebDav.WebDavResponse), remoteSourcePath As String, remoteDestinationPath As String, ioExceptionMessage As String, conflictItemType As ExceptionTypeForItemType)
            If remoteDestinationPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
            Select Case completedTask.Status
                Case TaskStatus.Faulted, TaskStatus.RanToCompletion
                    'ok - continue checks below
                Case TaskStatus.Created, TaskStatus.WaitingForActivation
                    Throw New InvalidOperationException(ProviderStrings.GetText("TaskNotStarted"))
                Case TaskStatus.WaitingForChildrenToComplete, TaskStatus.WaitingToRun, TaskStatus.Running
                    Throw New InvalidOperationException(ProviderStrings.GetText("TaskNotFinished"))
                Case TaskStatus.Canceled
                    Throw New TaskCanceledException()
                Case Else
                    Throw New InvalidOperationException(ProviderStrings.GetText("TaskStatusInvalid"))
            End Select
            If completedTask.Status <> TaskStatus.RanToCompletion OrElse completedTask.IsFaulted OrElse completedTask.Result.IsSuccessful = False Then
                If completedTask.Result Is Nothing Then
                    Throw New System.IO.IOException(ioExceptionMessage)
                ElseIf completedTask.Result.StatusCode = 404 Then
                    '404 Not found
                    Dim statusError As New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description)
                    If remoteSourcePath IsNot Nothing AndAlso Not Me.RemoteItemExists(remoteSourcePath) Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileNotFoundException(remoteSourcePath, statusError)
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryNotFoundException(remoteSourcePath, statusError)
                            Case Else
                                Throw New RessourceNotFoundException(remoteSourcePath, statusError)
                        End Select
                    End If
                    Throw New RessourceNotFoundException(remoteDestinationPath, statusError)
                ElseIf completedTask.Result.StatusCode = 409 Then
                    '409 Conflict
                    If remoteDestinationPath = Nothing Then
                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                    ElseIf Me.RemoteItemExists(remoteDestinationPath) Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileAlreadyExistsException(remoteDestinationPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryAlreadyExistsException(remoteDestinationPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case Else
                                Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        End Select
                    Else
                        Dim ParentPath As String = Me.ParentDirectoryPath(remoteDestinationPath)
                        If ParentPath <> Nothing AndAlso Me.RemoteItemExists(ParentPath) = False Then
                            Throw New DirectoryNotFoundException(ParentPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        Else
                            Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        End If
                    End If
                ElseIf completedTask.Result.StatusCode = 412 Then
                    '412 Precondition failed
                    If Me.RemoteItemExists(remoteSourcePath) = False Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case Else
                                Throw New RessourceNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        End Select
                    ElseIf remoteSourcePath <> Nothing Then
                        Throw New System.IO.IOException(ProviderStrings.Format("ButRemoteItemExists", ioExceptionMessage, remoteSourcePath), New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                    Else
                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                    End If
                ElseIf completedTask.Exception IsNot Nothing Then
                    Throw New System.IO.IOException(ioExceptionMessage, completedTask.Exception)
                Else
                    Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                End If
            End If
        End Sub

        Private Async Function CheckTaskResultForErrorsAsync(response As WebDav.WebDavResponse, remoteSourcePath As String, remoteDestinationPath As String, ioExceptionMessage As String, conflictItemType As ExceptionTypeForItemType, cancellationToken As CancellationToken) As Task
            If response.IsSuccessful Then Return
            Dim statusError As New ResponseStatusCodeException(response.StatusCode, response.Description)
            Select Case response.StatusCode
                Case 404
                    If remoteSourcePath IsNot Nothing AndAlso Await Me.ListRemoteItemAsync(remoteSourcePath, cancellationToken).ConfigureAwait(False) Is Nothing Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileNotFoundException(remoteSourcePath, statusError)
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryNotFoundException(remoteSourcePath, statusError)
                            Case Else
                                Throw New RessourceNotFoundException(remoteSourcePath, statusError)
                        End Select
                    End If
                    Throw New RessourceNotFoundException(remoteDestinationPath, statusError)
                Case 409
                    Dim destination = Await Me.ListRemoteItemAsync(remoteDestinationPath, cancellationToken).ConfigureAwait(False)
                    If destination IsNot Nothing Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileAlreadyExistsException(remoteDestinationPath, statusError)
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryAlreadyExistsException(remoteDestinationPath, statusError)
                        End Select
                    End If
                    Dim parent As String = Me.ParentDirectoryPath(remoteDestinationPath)
                    If parent <> Nothing AndAlso Await Me.ListRemoteItemAsync(parent, cancellationToken).ConfigureAwait(False) Is Nothing Then Throw New DirectoryNotFoundException(parent, statusError)
                Case 412
                    If remoteSourcePath IsNot Nothing AndAlso Await Me.ListRemoteItemAsync(remoteSourcePath, cancellationToken).ConfigureAwait(False) Is Nothing Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileNotFoundException(remoteSourcePath, statusError)
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryNotFoundException(remoteSourcePath, statusError)
                            Case Else
                                Throw New RessourceNotFoundException(remoteSourcePath, statusError)
                        End Select
                    End If
            End Select
            Throw New System.IO.IOException(ioExceptionMessage, statusError)
        End Function

        Protected Overrides Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.Overwrite = allowOverwrite.GetValueOrDefault
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            CopyTask.Wait()
            CheckTaskResultForErrors(CopyTask, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("CopyTaskFailed"), ExceptionTypeForItemType.File)
        End Sub

        Protected Overrides Async Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.Overwrite = allowOverwrite.GetValueOrDefault
            CopyParams.CancellationToken = Me.CurrentAsyncCancellationToken
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            Await CopyTask
            Await CheckTaskResultForErrorsAsync(CopyTask.Result, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("CopyTaskFailed"), ExceptionTypeForItemType.File, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
        End Function

        Protected Overrides Sub CopyDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.ApplyTo = Global.WebDav.ApplyTo.Copy.ResourceAndAncestors
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            CopyTask.Wait()
            CheckTaskResultForErrors(CopyTask, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("CopyTaskFailed"), ExceptionTypeForItemType.Directory)
        End Sub

        Protected Overrides Async Function CopyDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.ApplyTo = Global.WebDav.ApplyTo.Copy.ResourceAndAncestors
            CopyParams.CancellationToken = Me.CurrentAsyncCancellationToken
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            Await CopyTask
            Await CheckTaskResultForErrorsAsync(CopyTask.Result, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("CopyTaskFailed"), ExceptionTypeForItemType.Directory, Me.CurrentAsyncCancellationToken).ConfigureAwait(False)
        End Function

        Protected Overrides Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim MoveTask = Me.WebDavClient.Move(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, New Global.WebDav.MoveParameters() With {.Overwrite = allowOverwrite.GetValueOrDefault})
            MoveTask.Wait()
            CheckTaskResultForErrors(MoveTask, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("MoveFailed"), ExceptionTypeForItemType.File)
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Async Function MoveFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?, cancellationToken As CancellationToken) As Task
            Dim parameters As New Global.WebDav.MoveParameters With {.Overwrite = allowOverwrite.GetValueOrDefault(), .CancellationToken = cancellationToken}
            Dim request = Me.WebDavClient.Move(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, parameters)
            Await request.ConfigureAwait(False)
            Await CheckTaskResultForErrorsAsync(request.Result, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("MoveFailed"), ExceptionTypeForItemType.File, cancellationToken).ConfigureAwait(False)
        End Function

        Protected Overrides Sub MoveDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim MoveTask = Me.WebDavClient.Move(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, New Global.WebDav.MoveParameters() With {.Overwrite = False})
            MoveTask.Wait()
            CheckTaskResultForErrors(MoveTask, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("MoveFailed"), ExceptionTypeForItemType.Directory)
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Async Function MoveDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String, cancellationToken As CancellationToken) As Task
            Dim parameters As New Global.WebDav.MoveParameters With {.Overwrite = False, .CancellationToken = cancellationToken}
            Dim request = Me.WebDavClient.Move(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, parameters)
            Await request.ConfigureAwait(False)
            Await CheckTaskResultForErrorsAsync(request.Result, remoteSourcePath, remoteDestinationPath, ProviderStrings.GetText("MoveFailed"), ExceptionTypeForItemType.Directory, cancellationToken).ConfigureAwait(False)
        End Function

        Public Overrides Sub DeleteRemoteItem(remoteFilePath As String)
            Dim DelTask = Me.WebDavClient.Delete(Me.CustomWebApiUrl & remoteFilePath)
            DelTask.Wait()
            CheckTaskResultForErrors(DelTask, Nothing, remoteFilePath, ProviderStrings.GetText("DeleteFailed"), ExceptionTypeForItemType.Unspecified)
        End Sub

        ''' <inheritdoc/>
        Public Overrides Async Function DeleteRemoteItemAsync(remotePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Dim request = Me.WebDavClient.Delete(Me.CustomWebApiUrl & remotePath, New Global.WebDav.DeleteParameters With {.CancellationToken = cancellationToken})
            Await request.ConfigureAwait(False)
            Await CheckTaskResultForErrorsAsync(request.Result, Nothing, remotePath, ProviderStrings.GetText("DeleteFailed"), ExceptionTypeForItemType.Unspecified, cancellationToken).ConfigureAwait(False)
        End Function

        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            Dim DelTask = Me.WebDavClient.Delete(Me.CustomWebApiUrl & remoteItem.FullName)
            DelTask.Wait()
            CheckTaskResultForErrors(DelTask, Nothing, remoteItem.FullName, ProviderStrings.GetText("DeleteFailed"), ExceptionTypeForItemType.Unspecified)
        End Sub

        Public Overrides Sub CreateFolder(remoteFilePath As String)
            Dim CreateTask = Me.WebDavClient.Mkcol(Me.CustomWebApiUrl & remoteFilePath)
            CreateTask.Wait()
            CheckTaskResultForErrors(CreateTask, Nothing, remoteFilePath, ProviderStrings.GetText("CreateFolderFailed"), ExceptionTypeForItemType.Directory)
        End Sub

        ''' <inheritdoc/>
        Public Overrides Async Function CreateFolderAsync(remoteDirectoryPath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            Dim request = Me.WebDavClient.Mkcol(Me.CustomWebApiUrl & remoteDirectoryPath, New Global.WebDav.MkColParameters With {.CancellationToken = cancellationToken})
            Await request.ConfigureAwait(False)
            Await CheckTaskResultForErrorsAsync(request.Result, Nothing, remoteDirectoryPath, ProviderStrings.GetText("CreateFolderFailed"), ExceptionTypeForItemType.Directory, cancellationToken).ConfigureAwait(False)
        End Function

        Public Overrides Sub CreateDirectory(remoteDirectoryPath As String)
            Me.CreateFolder(remoteDirectoryPath)
        End Sub

        Public Overrides Sub CreateCollection(remoteCollectionName As String)
            Throw New NotSupportedException(ProviderStrings.GetText("CollectionsAreNotSupportedByWebDAV"))
        End Sub

        Public Overrides ReadOnly Property DirectorySeparator As Char
            Get
                Return "/"c
            End Get
        End Property

        Public Overrides Sub Authorize(dmsProfile As Data.IDmsLoginProfile)
            Dim Credentials As WebDavLoginCredentials = CType(Me.CreateNewCredentialsInstance(), WebDavLoginCredentials)
            Credentials.BaseUrl = dmsProfile.ServerAddress
            Credentials.Username = dmsProfile.UserName
            Credentials.Password = dmsProfile.Password
            Me.Authorize(Credentials, dmsProfile.IgnoreSslErrors)
        End Sub

        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return Me.OcsSharingClient IsNot Nothing AndAlso
                    Me.OcsSharingClient.Capabilities IsNot Nothing AndAlso
                    Me.OcsSharingClient.Capabilities.SupportsAnySharing
            End Get
        End Property

        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            If Not Client.Capabilities.SupportsLinkShares Then
                Throw New NotSupportedException(ProviderStrings.GetText("LinkSharingIsNotSupportedByThisOCS"))
            End If
            If shareInfo.AllowUpload AndAlso Not Client.Capabilities.SupportsPublicUpload Then
                Throw New NotSupportedException(ProviderStrings.GetText("PublicUploadsAreNotSupportedByThisOCS"))
            End If
            If shareInfo.AllowUpload AndAlso dmsResource.ItemType = DmsResourceItem.ItemTypes.File Then
                Throw New NotSupportedException(ProviderStrings.GetText("PublicUploadsCanOnlyBeEnabledForFolders"))
            End If

            Dim Permissions As Integer = ToOcsPermissions(shareInfo)
            Dim CreatedShare As PublicShare = TryCast(Client.CreateLink(Me.ToOcsPath(dmsResource.FullName), Permissions, shareInfo.AllowUpload, shareInfo.Name, shareInfo.ExpiryDateLocalTime, EmptyStringToNothing(shareInfo.Password)), PublicShare)
            If CreatedShare Is Nothing Then
                Throw New InvalidOperationException(ProviderStrings.GetText("TheOCSServerReturnedAnUnexpectedShareType"))
            End If

            shareInfo.ParentDmsResourceItem = dmsResource
            shareInfo.DmsProvider = Me
            shareInfo.FillLinkDetails = AddressOf FillOcsLinkDetails
            Dim Record As OcsShareRecord = OcsShareRecord.FromLegacy(CreatedShare)
            Dim Result As DmsLink = Me.CreateDmsLink(dmsResource, Record)
            Me.InitializeDmsLink(Result, Record, shareInfo.Password)
            Return Result
        End Function

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            If Not Client.Capabilities.SupportsGroupShares Then
                Throw New NotSupportedException(ProviderStrings.GetText("GroupSharingIsNotSupportedByThisOCS"))
            End If
            If shareInfo.Group.ID = Nothing Then
                Throw New ArgumentException(ProviderStrings.GetText("AGroupIDIsRequired"), NameOf(shareInfo))
            End If
            Client.CreateGroupShare(Me.ToOcsPath(dmsResource.FullName), shareInfo.Group.ID, ToOcsPermissions(shareInfo))
        End Sub

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            If Not Client.Capabilities.SupportsUserShares Then
                Throw New NotSupportedException(ProviderStrings.GetText("UserSharingIsNotSupportedByThisOCS"))
            End If
            If shareInfo.User.ID = Nothing Then
                Throw New ArgumentException(ProviderStrings.GetText("AUserIDIsRequired"), NameOf(shareInfo))
            End If
            Client.CreateUserShare(Me.ToOcsPath(dmsResource.FullName), shareInfo.User.ID, ToOcsPermissions(shareInfo))
        End Sub

        Public Overrides Sub UpdateLink(shareInfo As DmsLink)
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            If Not Client.Capabilities.SupportsLinkShares Then
                Throw New NotSupportedException(ProviderStrings.GetText("LinkSharingIsNotSupportedByThisOCS"))
            End If
            If shareInfo.AllowUpload AndAlso Not Client.Capabilities.SupportsPublicUpload Then
                Throw New NotSupportedException(ProviderStrings.GetText("PublicUploadsAreNotSupportedByThisOCS"))
            End If
            If shareInfo.ParentDmsResourceItem Is Nothing Then
                Throw New ArgumentException(ProviderStrings.GetText("TheParentDMSResourceItemIsRequired"), NameOf(shareInfo))
            End If
            If shareInfo.AllowUpload AndAlso shareInfo.ParentDmsResourceItem.ItemType = DmsResourceItem.ItemTypes.File Then
                Throw New NotSupportedException(ProviderStrings.GetText("PublicUploadsCanOnlyBeEnabledForFolders"))
            End If

            Client.UpdateLink(
                ParseOcsShareID(shareInfo.ID),
                ToOcsPermissions(shareInfo),
                shareInfo.AllowUpload,
                shareInfo.Name,
                shareInfo.ExpiryDateLocalTime,
                clearExpiration:=Not shareInfo.ExpiryDateLocalTime.HasValue,
                password:=EmptyStringToNothing(shareInfo.Password))
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForGroup)
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            If Not Client.Capabilities.SupportsGroupShares Then
                Throw New NotSupportedException(ProviderStrings.GetText("GroupSharingIsNotSupportedByThisOCS"))
            End If
            Client.UpdateSharePermissions(Me.FindShareID(shareInfo.ParentDmsResourceItem, OcsShareType.Group, shareInfo.Group.ID), ToOcsPermissions(shareInfo))
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForUser)
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            If Not Client.Capabilities.SupportsUserShares Then
                Throw New NotSupportedException(ProviderStrings.GetText("UserSharingIsNotSupportedByThisOCS"))
            End If
            Client.UpdateSharePermissions(Me.FindShareID(shareInfo.ParentDmsResourceItem, OcsShareType.User, shareInfo.User.ID), ToOcsPermissions(shareInfo))
        End Sub

        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            Me.RequireOcsSharingClient().DeleteShare(ParseOcsShareID(shareInfo.ID))
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForGroup)
            Me.RequireOcsSharingClient().DeleteShare(Me.FindShareID(shareInfo.ParentDmsResourceItem, OcsShareType.Group, shareInfo.Group.ID))
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForUser)
            Me.RequireOcsSharingClient().DeleteShare(Me.FindShareID(shareInfo.ParentDmsResourceItem, OcsShareType.User, shareInfo.User.ID))
        End Sub

        Public Overrides Function GetAllGroups() As List(Of DmsGroup)
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            Dim Result As New List(Of DmsGroup)
            If Client.Capabilities.SupportsShareeDiscovery Then
                For Each ShareeInfo As Sharee In Client.FindSharees(String.Empty, "file")
                    If ShareeInfo.ShareType = OcsShareType.Group AndAlso Not Result.Exists(Function(item) item.ID = ShareeInfo.ShareWith) Then
                        Result.Add(New DmsGroup With {.ID = ShareeInfo.ShareWith, .Name = ShareeInfo.ShareWithDisplayName})
                    End If
                Next
            Else
                For Each GroupID As String In Client.SearchGroups()
                    Result.Add(New DmsGroup With {.ID = GroupID, .Name = GroupID})
                Next
            End If
            Return Result
        End Function

        Public Overrides Function GetAllUsers() As List(Of DmsUser)
            Dim Client As IOcsSharingClient = Me.RequireOcsSharingClient()
            Dim Result As New List(Of DmsUser)
            If Client.Capabilities.SupportsShareeDiscovery Then
                For Each ShareeInfo As Sharee In Client.FindSharees(String.Empty, "file")
                    If ShareeInfo.ShareType = OcsShareType.User AndAlso Not Result.Exists(Function(item) item.ID = ShareeInfo.ShareWith) Then
                        Result.Add(New DmsUser With {.ID = ShareeInfo.ShareWith, .DisplayName = If(String.IsNullOrWhiteSpace(ShareeInfo.ShareWithDisplayName), ShareeInfo.Label, ShareeInfo.ShareWithDisplayName)})
                    End If
                Next
            Else
                For Each UserID As String In Client.SearchUsers()
                    Result.Add(New DmsUser With {.ID = UserID})
                Next
            End If
            Return Result
        End Function

        Private Function RequireOcsSharingClient() As IOcsSharingClient
            If Me.OcsSharingClient Is Nothing OrElse Me.OcsSharingClient.Capabilities Is Nothing OrElse Not Me.OcsSharingClient.Capabilities.SupportsAnySharing Then
                Throw New NotSupportedException(ProviderStrings.GetText("SharingIsNotSupportedByThisWebDAVServer"))
            End If
            Return Me.OcsSharingClient
        End Function

        Private Shared Function ToOcsPermissions(shareInfo As DmsShareBase) As Integer
            If shareInfo.AllowView <> shareInfo.AllowDownload Then
                Throw New NotSupportedException(ProviderStrings.GetText("ThisOCSAPICannotRepresentViewAndDownload"))
            End If

            Dim Result As Integer
            If shareInfo.AllowView Then Result = Result Or Convert.ToInt32(OcsPermission.Read)
            If shareInfo.AllowEdit Then Result = Result Or Convert.ToInt32(OcsPermission.Update)
            If shareInfo.AllowUpload Then Result = Result Or Convert.ToInt32(OcsPermission.Create)
            If shareInfo.AllowDelete Then Result = Result Or Convert.ToInt32(OcsPermission.Delete)
            If shareInfo.AllowShare Then Result = Result Or Convert.ToInt32(OcsPermission.Share)
            If Result = 0 Then
                Throw New ArgumentException(ProviderStrings.GetText("AtLeastOneSharingPermissionIsRequired"), NameOf(shareInfo))
            End If
            Return Result
        End Function

        Private Function FindShareID(dmsResource As DmsResourceItem, shareType As OcsShareType, shareWithID As String) As Integer
            If dmsResource Is Nothing Then
                Throw New ArgumentNullException(NameOf(dmsResource))
            End If

            Dim Result As Integer?
            For Each ShareInfo As OcsShareRecord In Me.RequireOcsSharingClient().GetShares(Me.ToOcsPath(dmsResource.FullName), True, False)
                Dim FoundShareWithID As String = Nothing
                If shareType = OcsShareType.User Then
                    FoundShareWithID = ShareInfo.SharedWith
                ElseIf shareType = OcsShareType.Group Then
                    FoundShareWithID = ShareInfo.SharedWith
                End If

                If ShareInfo.Type = shareType AndAlso String.Equals(FoundShareWithID, shareWithID, StringComparison.Ordinal) Then
                    If Result.HasValue Then
                        Throw New InvalidOperationException(ProviderStrings.GetText("MoreThanOneMatchingOCSShareWasFound"))
                    End If
                    Result = ShareInfo.ShareId
                End If
            Next

            If Not Result.HasValue Then
                Throw New KeyNotFoundException(ProviderStrings.GetText("TheMatchingOCSShareWasNotFound"))
            End If
            Return Result.Value
        End Function

        Private Shared Function ParseOcsShareID(id As String) As Integer
            Dim Result As Integer
            If Not Integer.TryParse(id, Globalization.NumberStyles.None, Globalization.CultureInfo.InvariantCulture, Result) OrElse Result <= 0 Then
                Throw New ArgumentException(ProviderStrings.GetText("APositiveNumericOCSShareIDIsRequired"), NameOf(id))
            End If
            Return Result
        End Function

        Private Shared Function EmptyStringToNothing(value As String) As String
            If value = String.Empty Then
                Return Nothing
            Else
                Return value
            End If
        End Function

        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Return Me._AuthorizedUser
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsSubFolderConfiguration As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes
            Get
                Return RuntimeAccessTypes.ConfigurationAndRuntimeAccess
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsNonUniqueRemoteItems As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace
