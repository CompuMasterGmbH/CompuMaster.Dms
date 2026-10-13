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
    Public Sub InspectCollectionsAndExistingScopeWithoutMutatingResources()
        Dim phase = "CiAdmission"
        Try
            ReadOnlyDiagnosticSupport.RequireCiWindow({"TEST_SCOPEVISIOTEAMWORK_USERNAME", "TEST_SCOPEVISIOTEAMWORK_CUSTOMERNO", "TEST_SCOPEVISIOTEAMWORK_PASSWORD"})
            phase = "Authorize"
            Dim settings As New ScopevisioTeamworkSettings()
            Dim profile As New DmsLoginProfile With {
                .DmsProvider = BaseDmsProvider.DmsProviders.Scopevisio,
                .Username = settings.InputLine("username"),
                .CustomerInstance = settings.InputLine("customer no."),
                .Password = settings.InputLine("password")}
            Dim provider = DirectCast(Dms.Providers.CreateAuthorizedDmsProviderInstance(profile), ScopevisioTeamworkDmsProvider)
            phase = "CollectionsSync"
            Dim io = provider.IOClient
            Dim collections = io.ApiClient.Collections.GetCollections(io.CurrentAuthenticationContextUserID, Nothing, True)?.Collections
            If collections Is Nothing Then Throw New System.IO.InvalidDataException()
            Dim candidates = ReadOnlyDiagnosticSupport.CollectionMoveCandidates(collections.Select(Function(item) item.Name))
            Console.WriteLine("Read-only Scopevisio: collection read completed; candidate names are not proof of current-run ownership.")
            Console.WriteLine("Reported CollectionMove candidate count=" & candidates.Length.ToString(Globalization.CultureInfo.InvariantCulture) & " (at most 20).")
            For Each candidate In candidates
                Console.WriteLine("CollectionMove candidate: " & candidate)
            Next
            Console.WriteLine("Candidate inventory is bounded to 20 names; exact historical owned absence remains unverified.")
            phase = "FixtureRootSyncFacade"
            Dim rootPresent = provider.ListRemoteItem("ZZZ_UnitTests_CM.Dms") IsNot Nothing
            Console.WriteLine("Read-only Scopevisio: fixture root present=" & rootPresent.ToString() & ".")
            If rootPresent Then
                phase = "FixtureFolderSyncFacade"
                Dim folderPresent = provider.ListRemoteItem("ZZZ_UnitTests_CM.Dms/Folder") IsNot Nothing
                Console.WriteLine("Read-only Scopevisio: fixture folder present=" & folderPresent.ToString() & ".")
            Else
                Console.WriteLine("Read-only Scopevisio: missing fixture root was not created; folder probe not performed.")
            End If
        Catch ex As Exception
            Assert.Fail(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", phase, ex))
        End Try
    End Sub
End Class

Friend NotInheritable Class ReadOnlyDiagnosticSupport
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
