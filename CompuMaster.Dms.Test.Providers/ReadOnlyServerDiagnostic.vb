Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports CompuMaster.Dms.Providers
Imports System.Text.RegularExpressions

'Separate fixtures deliberately have no inherited mutating setup or teardown.
<TestFixture, Explicit("Run only through the coordinated CI read-only diagnostic mode."), Category("RemoteDms"), Category("OwnCloud"), Category("ReadOnlyDiagnostic"), Category("TestLevel1")>
Public NotInheritable Class OwnCloudReadOnlyDiagnostic
    <Test>
    Public Sub InspectSharingDiscoveryWithoutCreatingResources()
        Dim provider As WebDavDmsProvider = Nothing
        Dim phase = "CiAdmission"
        Try
            ReadOnlyDiagnosticSupport.RequireCiWindow({"TEST_CMOWNCLOUD_SERVERURL", "TEST_CMOWNCLOUD_USERNAME", "TEST_CMOWNCLOUD_PASSWORD"})
            phase = "Authorize"
            Dim settings As New OwnCloudWebDavSettings()
            Dim username = settings.InputLine("username")
            Dim profile As New DmsLoginProfile With {
                .DmsProvider = BaseDmsProvider.DmsProviders.WebDAV,
                .BaseUrl = settings.NormalizeServerUrl(settings.InputLine("server url"), username),
                .Username = username,
                .Password = settings.InputLine("password")}
            provider = DirectCast(Dms.Providers.CreateAuthorizedDmsProviderInstance(profile), WebDavDmsProvider)
        Catch ex As Exception
            Assert.Fail(ReadOnlyDiagnosticSupport.SafeFailure("OwnCloud", phase, ex))
        End Try
        Dim summary = provider.OcsSharingDiscoveryForTesting.ToSafeSummary()
        Console.WriteLine(summary)
        Assert.That(provider.SupportsSharingSetup, [Is].True, "Required ownCloud sharing discovery failed. " & summary)
    End Sub
End Class

<TestFixture, Explicit("Run only through the coordinated CI read-only diagnostic mode."), Category("RemoteDms"), Category("ScopevisioTeamwork"), Category("ReadOnlyDiagnostic"), Category("TestLevel1")>
Public NotInheritable Class ScopevisioReadOnlyDiagnostic
    <Test>
    Public Async Function CompareDirectBearerAndSdkCollectionRead() As System.Threading.Tasks.Task
        Dim phase = "CiAdmission"
        Try
            ReadOnlyDiagnosticSupport.RequireCiWindow({"TEST_SCOPEVISIOTEAMWORK_USERNAME", "TEST_SCOPEVISIOTEAMWORK_CUSTOMERNO", "TEST_SCOPEVISIOTEAMWORK_PASSWORD", "TEST_SCOPEVISIOTEAMWORK_ORGANISATIONID", "TEST_SCOPEVISIOTEAMWORK_EXPECTEDTEAMWORKTENANTID"})
            Dim profile = New ScopevisioTeamworkSettings().CreateLiveTestProfile()
            profile.Validate()
            Dim configuration = New AuthorizationProbeProvider().CreateConfiguration(profile)
            Dim client As New Global.CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient(configuration)
            phase = "DirectToken"
            Dim issued = Await client.AuthorizationApi.TokenAsyncWithHttpInfo("password", configuration.ClientNumber,
                username:=configuration.Username, password:=configuration.Password, organisationId:=profile.OrganisationId)
            Dim identity = ScopevisioExplicitIdentity.FromToken(issued.Data, issued.RawJsonContent, profile.OrganisationId.Value, profile.ExpectedTeamworkTenantId)
            client.Token = issued.Data
            phase = "DirectAccount"
            Dim account = Await client.AdditionalApi.GetApplicationContextAsyncWithHttpInfo()
            identity.ValidateAccount(account.Data, account.RawJsonContent)
            phase = "DirectCollections"
            Using http = CenterDevice.Rest.Clients.CenterDeviceHttpTransport.CreateHttpClient(New System.Net.Http.HttpClientHandler With {.AllowAutoRedirect = False})
                Using request As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, "https://appload.scopevisio.com/rest/teamworkbridge/collections")
                    request.Headers.Authorization = New System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", issued.Data.AccessToken)
                    request.Headers.Accept.Add(New System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"))
                    Using response = Await http.SendAsync(request)
                        Dim body = Await response.Content.ReadAsStringAsync()
                        Console.WriteLine("Teamwork direct probe: HTTP=" & CInt(response.StatusCode).ToString(Globalization.CultureInfo.InvariantCulture) &
                            "; selectedIdentityVerified=True; bearerPresent=True; redirect=" & (response.Headers.Location IsNot Nothing).ToString() &
                            "; responseCategory=" & ReadOnlyDiagnosticSupport.ResponseCategory(body) &
                            "; responseSignals=" & ReadOnlyDiagnosticSupport.ResponseSignals(body) & ".")
                        Dim admittedStatus = response.StatusCode
                        If admittedStatus = System.Net.HttpStatusCode.Unauthorized Then
                            For Each force In {False, True}
                                phase = "InternalTeamworkToken"
                                Using tokenRequest As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,
                                    "https://appload.scopevisio.com/rest/teamworktoken?force=" & force.ToString().ToLowerInvariant())
                                    tokenRequest.Headers.Authorization = New System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", issued.Data.AccessToken)
                                    Using internalToken = Await http.SendAsync(tokenRequest)
                                        Dim tokenBody = Await internalToken.Content.ReadAsStringAsync()
                                        Console.WriteLine("Teamwork internal token probe: force=" & force.ToString() & "; HTTP=" &
                                            CInt(internalToken.StatusCode).ToString(Globalization.CultureInfo.InvariantCulture) & "; responseCategory=" &
                                            If(internalToken.IsSuccessStatusCode, "TokenRetrieved", ReadOnlyDiagnosticSupport.ResponseCategory(tokenBody)) &
                                            "; responseSignals=" & If(internalToken.IsSuccessStatusCode, "omitted", ReadOnlyDiagnosticSupport.ResponseSignals(tokenBody)) & ".")
                                    End Using
                                End Using
                                phase = "CollectionsAfterInternalToken"
                                Using retry As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, request.RequestUri)
                                    retry.Headers.Authorization = New System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", issued.Data.AccessToken)
                                    retry.Headers.Accept.Add(New System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"))
                                    Using retried = Await http.SendAsync(retry)
                                        Dim retryBody = Await retried.Content.ReadAsStringAsync()
                                        admittedStatus = retried.StatusCode
                                        Console.WriteLine("Teamwork after internal token probe: force=" & force.ToString() & "; HTTP=" &
                                            CInt(admittedStatus).ToString(Globalization.CultureInfo.InvariantCulture) & "; responseCategory=" &
                                            If(retried.IsSuccessStatusCode, "CollectionsRetrieved", ReadOnlyDiagnosticSupport.ResponseCategory(retryBody)) & ".")
                                    End Using
                                End Using
                                If admittedStatus <> System.Net.HttpStatusCode.Unauthorized Then Exit For
                            Next
                        End If
                        phase = "SdkCollections"
                        Try
                            Dim sdk As New Global.CompuMaster.Scopevisio.CenterDeviceApi.TeamworkRestClient(client)
                            Dim collections = Await sdk.Collections.GetCollectionsAsync(issued.Data.Uid)
                            Console.WriteLine("Teamwork SDK probe: collectionsRead=" & (collections IsNot Nothing).ToString() & ".")
                        Catch failure As Exception
                            Console.WriteLine(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", phase, failure))
                        End Try
                        phase = "DirectCollectionsResult"
                        Assert.That(admittedStatus, [Is].EqualTo(System.Net.HttpStatusCode.OK), "Direct collection read failed; only the safe status/category is logged.")
                    End Using
                End Using
            End Using
        Catch failure As Exception
            Assert.Fail(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", phase, failure))
        End Try
    End Function

    Private NotInheritable Class AuthorizationProbeProvider
        Inherits ScopevisioTeamworkDmsProvider

        Friend Function CreateConfiguration(profile As ScopevisioLoginCredentials) As Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration
            Return MyBase.CreateAuthorizationConfiguration(profile, False)
        End Function
    End Class

    <Test>
    Public Sub InspectCollectionsAndExistingScopeWithoutMutatingResources()
        Dim phase = "CiAdmission"
        Try
            ReadOnlyDiagnosticSupport.RequireCiWindow({"TEST_SCOPEVISIOTEAMWORK_USERNAME", "TEST_SCOPEVISIOTEAMWORK_CUSTOMERNO", "TEST_SCOPEVISIOTEAMWORK_PASSWORD", "TEST_SCOPEVISIOTEAMWORK_ORGANISATIONID", "TEST_SCOPEVISIOTEAMWORK_EXPECTEDTEAMWORKTENANTID"})
            phase = "HistoricalWitnessAdmission"
            Dim witness = HistoricalReadOnlyPreflight.ValidateWitness(Environment.GetEnvironmentVariable("DMS_DIAGNOSTIC_TEMPORARY_NAME"))
            phase = "Authorize"
            Dim settings As New ScopevisioTeamworkSettings()
            Dim profile = settings.CreateLiveTestProfile()
            Dim provider = DirectCast(Dms.Providers.CreateAuthorizedDmsProviderInstance(profile), ScopevisioTeamworkDmsProvider)
            Dim io = provider.IOClient
            phase = "HistoricalIdentity"
            Dim userId = io.CurrentAuthenticationContextUserID
            Dim tenantId = provider.CurrentDmsInstance?.ID
            Dim account = provider.ApplicationContext
            Dim bridgeDmsUserId = provider.CurrentContextUserID
            HistoricalReadOnlyPreflight.ValidateIdentity(userId, account?.User?.Uid, tenantId, account?.Organisation?.TeamworkTenantId, bridgeDmsUserId)
            Console.WriteLine("Historical identity: authenticatedScopeUserFingerprint=" & HistoricalReadOnlyPreflight.Fingerprint(userId) &
                              "; bridgeDmsUserFingerprint=" & HistoricalReadOnlyPreflight.Fingerprint(bridgeDmsUserId) &
                              "; configuredAccountFingerprint=" & HistoricalReadOnlyPreflight.Fingerprint(profile.CustomerInstance) &
                              "; authenticatedTenantFingerprint=" & HistoricalReadOnlyPreflight.Fingerprint(tenantId) &
                              "; scopeUserMatchesAccount=True; tenantMatchesAccount=True; accountOwnershipUnproven=True.")
            phase = "CollectionsSync"
            Dim collections = io.ApiClient.Collections.GetCollections(io.CurrentAuthenticationContextUserID, Nothing, True)?.Collections
            If collections Is Nothing Then Throw New System.IO.InvalidDataException()
            Dim candidates = ReadOnlyDiagnosticSupport.CollectionMoveCandidates(collections.Select(Function(item) item.Name))
            Console.WriteLine("Read-only Scopevisio: collection read completed; candidate names are not proof of current-run ownership.")
            Console.WriteLine("Reported CollectionMove candidate count=" & candidates.Length.ToString(Globalization.CultureInfo.InvariantCulture) & " (at most 20).")
            For Each candidate In candidates
                Console.WriteLine("CollectionMove candidate: " & candidate)
            Next
            Console.WriteLine("Candidate inventory is bounded to 20 names; exact historical owned absence remains unverified.")
            phase = "HistoricalScopeReads"
            HistoricalReadOnlyPreflight.Inspect(collections, witness,
                Function(id) io.ApiClient.Collection.GetCollection(userId, id),
                Function(collectionId, parentId) io.ApiClient.Folders.GetFolders(userId, collectionId, If(parentId, CenterDevice.Rest.RestApiConstants.NONE), Nothing, Nothing, Nothing, {"id", "name", "collection", "parent"}).Folders,
                Function(collectionId, folderId) io.ApiClient.Documents.Get(Of CenterDevice.Rest.Clients.Documents.Metadata.DocumentFullMetadata)(userId, collectionId, Nothing, If(folderId, CenterDevice.Rest.RestApiConstants.NONE), 0, HistoricalReadOnlyPreflight.MaximumDocuments + 1),
                AddressOf Console.WriteLine)
        Catch ex As Exception
            Assert.Fail(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", phase, ex))
        End Try
    End Sub
End Class

Friend NotInheritable Class ReadOnlyDiagnosticSupport
    Friend Shared Function ResponseSignals(body As String) As String
        'Emit only these fixed labels, never matched server values or arbitrary error descriptions.
        Dim labels = {"expired", "refresh", "tenant", "permission", "license", "organisation", "client", "token", "teamwork", "credentials", "denied"}
        Dim selected = labels.Where(Function(label) Regex.IsMatch(If(body, ""), "\b" & label & "\b", RegexOptions.IgnoreCase Or RegexOptions.CultureInvariant)).ToArray()
        Return If(selected.Length = 0, "none", String.Join(",", selected))
    End Function

    Friend Shared Function ResponseCategory(body As String) As String
        Dim text = If(body, "").Trim()
        If text = "Unknown or expired token" Then Return "UnknownOrExpiredToken"
        If text = "Tenant has expired" Then Return "TenantExpired"
        If text.StartsWith("<", StringComparison.Ordinal) Then Return "Html"
        Try
            Dim errorValue = Newtonsoft.Json.Linq.JObject.Parse(text)("error")
            If errorValue Is Nothing OrElse errorValue.Type <> Newtonsoft.Json.Linq.JTokenType.String Then Return "OtherJson"
            Dim errorCode = CStr(errorValue)
            Select Case errorCode
                Case "invalid_token", "invalid_grant", "access_denied", "unauthorized"
                    Return errorCode
                Case Else
                    Return "OtherJson"
            End Select
        Catch failure As Newtonsoft.Json.JsonException
            Return "Other"
        Catch failure As ArgumentException
            Return "Other"
        End Try
    End Function

    Private Shared ReadOnly CandidatePattern As New Regex("\AZZZ_UnitTests_CM\.Dms_CollectionMove_[0-9a-fA-F]{32}_(?:Source|Target)\z", RegexOptions.CultureInvariant)

    Friend Shared Sub RequireCiWindow(requiredVariables As String())
        ValidateCiWindow(Environment.GetEnvironmentVariable("GITHUB_ACTIONS") = "true",
                         Environment.GetEnvironmentVariable("DMS_READ_ONLY_DIAGNOSTICS") = "true",
                         Environment.GetEnvironmentVariable("DMS_DIAGNOSTIC_WINDOW_UNTIL"), DateTimeOffset.UtcNow)
        If requiredVariables.Any(Function(name) String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))) Then
            Throw New InvalidOperationException("Required CI authentication configuration is missing.")
        End If
    End Sub

    Friend Shared Sub ValidateCiWindow(isCi As Boolean, isDiagnostic As Boolean, until As String, now As DateTimeOffset)
        Dim expiry As DateTimeOffset
        If Not isCi OrElse Not isDiagnostic OrElse until Is Nothing OrElse Not Regex.IsMatch(until, "(?:Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant) OrElse
            Not DateTimeOffset.TryParse(until, Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, expiry) OrElse
            expiry - now < TimeSpan.FromMinutes(20) Then
            Throw New InvalidOperationException("Read-only diagnostics require an explicit CI-only confirmed exclusive window.")
        End If
    End Sub

    Friend Shared Function CollectionMoveCandidates(names As IEnumerable(Of String)) As String()
        If names Is Nothing Then Return New String() {}
        Return names.Where(Function(name) name IsNot Nothing AndAlso CandidatePattern.IsMatch(name)).
            Distinct(StringComparer.Ordinal).OrderBy(Function(name) name, StringComparer.Ordinal).Take(20).ToArray()
    End Function

    Friend Shared Function SafeFailure(server As String, phase As String, failure As Exception) As String
        Dim diagnostic = OcsSharingDiscoveryDiagnostic.Create(OcsSharingDiscoveryOutcome.Unavailable, OcsSharingDiscoveryPhase.Capabilities, failure)
        Dim kind = diagnostic.FailureKind
        Dim status = diagnostic.HttpStatus
        Dim current = failure
        For depth = 0 To 7
            If current Is Nothing Then Exit For
            Dim knownStatus As Integer? = Nothing
            If TypeOf current Is CenterDevice.Rest.Exceptions.InternalServerErrorException Then
                knownStatus = 500
            ElseIf TypeOf current Is CenterDevice.Rest.Exceptions.NotFoundException Then
                knownStatus = 404
            ElseIf TypeOf current Is CenterDevice.Rest.Exceptions.ForbiddenException Then
                knownStatus = 403
            ElseIf TypeOf current Is CenterDevice.Rest.Exceptions.UnauthorizedException Then
                knownStatus = 401
            ElseIf TypeOf current Is CenterDevice.Rest.Exceptions.BadRequestException Then
                knownStatus = 400
            End If
            If knownStatus.HasValue Then
                kind = "CenterDeviceHttp"
            ElseIf TypeOf current Is Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException Then
                kind = "ScopevisioApi"
                knownStatus = DirectCast(current, Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException).ErrorCode
            ElseIf TypeOf current Is Net.WebException Then
                Try
                    'The published Teamwork handler sets this exact integer key, including when the transport cause is null.
                    Dim value = current.Data("CompuMaster.Scopevisio.Teamwork.HttpStatusCode")
                    If TypeOf value Is Integer AndAlso CInt(value) >= 100 AndAlso CInt(value) <= 599 Then
                        knownStatus = CInt(value)
                        kind = "TeamworkHttp"
                    End If
                Catch
                    'An optional metadata probe must never break safe reporting.
                End Try
            End If
            If knownStatus.HasValue AndAlso knownStatus.Value >= 100 AndAlso knownStatus.Value <= 599 Then
                status = knownStatus
                Exit For
            End If
            current = current.InnerException
        Next
        'The callers supply fixed phase/server labels; the original exception is never rethrown.
        Return "Read-only " & server & " phase=" & phase & "; failure=" & kind &
            "; HTTP=" & If(status.HasValue, status.Value.ToString(Globalization.CultureInfo.InvariantCulture), "unknown") & "."
    End Function
End Class
