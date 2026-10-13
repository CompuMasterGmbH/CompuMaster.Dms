Option Explicit On
Option Strict On

Imports NUnit.Framework

<TestFixture>
Public NotInheritable Class ReadOnlyServerDiagnosticTest
    <Test>
    Public Sub CandidateInventoryNeverIncludesPermanentOrUnrelatedNames()
        Dim candidate = "ZZZ_UnitTests_CM.Dms_CollectionMove_" & New String("a"c, 32) & "_Source"
        Dim names = {Nothing, "ZZZ_UnitTests_CM.Dms", "Test-Temp", "Eingangsrechnungen", candidate, candidate,
                     candidate & vbLf, candidate & "/child", candidate.Replace("CollectionMove", "Other"), "prefix" & candidate}
        Assert.That(ReadOnlyDiagnosticSupport.CollectionMoveCandidates(names), [Is].EqualTo({candidate}))
    End Sub

    <Test>
    Public Sub CandidateInventoryIsBoundedAndSorted()
        Dim names = Enumerable.Range(0, 100).Select(Function(index) "ZZZ_UnitTests_CM.Dms_CollectionMove_" & index.ToString("x32") & "_Target")
        Dim actual = ReadOnlyDiagnosticSupport.CollectionMoveCandidates(names.Reverse())
        Assert.That(actual, Has.Length.EqualTo(20))
        Assert.That(actual, [Is].EqualTo(names.Take(20).ToArray()))
    End Sub

    <Test>
    Public Sub ReadOnlyFixturesCannotInheritMutatingProviderLifecycle()
        For Each fixture In {GetType(OwnCloudReadOnlyDiagnostic), GetType(ScopevisioReadOnlyDiagnostic)}
            Assert.That(GetType(BaseDmsProviderTestBase).IsAssignableFrom(fixture), [Is].False)
            Assert.That(fixture.IsDefined(GetType(ExplicitAttribute), False), [Is].True)
            For Each method In fixture.GetMethods()
                Assert.That(method.IsDefined(GetType(SetUpAttribute), True) OrElse method.IsDefined(GetType(OneTimeSetUpAttribute), True) OrElse
                            method.IsDefined(GetType(TearDownAttribute), True) OrElse method.IsDefined(GetType(OneTimeTearDownAttribute), True), [Is].False)
            Next
        Next
    End Sub

    <Test>
    Public Sub ReadFailureSummaryOmitsOriginalSecretsAndServerContent()
        Dim failure As New IO.InvalidDataException("secret URL/body/account")
        failure.Data("Authorization") = "secret bearer"
        Assert.That(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", "Collections", failure), [Is].EqualTo("Read-only Scopevisio phase=Collections; failure=Protocol; HTTP=unknown."))
    End Sub

    <Test>
    Public Sub AdmissionRejectsLocalMissingExpiredAndAmbiguousWindows()
        Dim now As New DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero)
        Dim valid = now.AddHours(1).ToString("o")
        Assert.Throws(Of InvalidOperationException)(Sub() ReadOnlyDiagnosticSupport.ValidateCiWindow(False, True, valid, now))
        Assert.Throws(Of InvalidOperationException)(Sub() ReadOnlyDiagnosticSupport.ValidateCiWindow(True, False, valid, now))
        For Each invalid In {Nothing, "", "2026-10-13T11:00:00", now.AddMinutes(-1).ToString("o"), now.AddMinutes(19).ToString("o"), "invalid+00:00"}
            Assert.Throws(Of InvalidOperationException)(Sub() ReadOnlyDiagnosticSupport.ValidateCiWindow(True, True, invalid, now))
        Next
        Assert.DoesNotThrow(Sub() ReadOnlyDiagnosticSupport.ValidateCiWindow(True, True, valid, now))
        Assert.DoesNotThrow(Sub() ReadOnlyDiagnosticSupport.ValidateCiWindow(True, True, "2026-10-13T12:00:00+02:00", now))
    End Sub

    <TestCase(500), TestCase(404), TestCase(403), TestCase(401), TestCase(400)>
    Public Sub TypedSdkHttpErrorRetainsKnownStatusWithoutExposingPayload(status As Integer)
        Dim failure As Exception
        Select Case status
            Case 500 : failure = New CenterDevice.Rest.Exceptions.InternalServerErrorException("secret response body", Nothing)
            Case 404 : failure = New CenterDevice.Rest.Exceptions.NotFoundException("secret response body", Nothing)
            Case 403 : failure = New CenterDevice.Rest.Exceptions.ForbiddenException("secret response body", Nothing)
            Case 401 : failure = New CenterDevice.Rest.Exceptions.UnauthorizedException("secret response body", Nothing)
            Case Else : failure = New CenterDevice.Rest.Exceptions.BadRequestException("secret response body", Nothing)
        End Select
        failure.Data("Authorization") = "secret bearer"
        Assert.That(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", "CollectionsSync", failure),
                    [Is].EqualTo("Read-only Scopevisio phase=CollectionsSync; failure=CenterDeviceHttp; HTTP=" & status.ToString() & "."))
    End Sub

    <Test>
    Public Sub WrappedScopevisioApiFailureRetainsKnownAuthorizationStatus()
        Dim failure = CreateApiFailure(401)
        failure.Data("Authorization") = "secret bearer"
        Dim wrapper As New Exception("secret account URL", failure)
        Assert.That(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", "Authorize", wrapper),
                    [Is].EqualTo("Read-only Scopevisio phase=Authorize; failure=ScopevisioApi; HTTP=401."))
    End Sub

    <TestCase(0), TestCase(99), TestCase(600)>
    Public Sub ApiErrorCodesOutsideHttpRangeRemainUnknown(status As Integer)
        Assert.That(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", "Authorize", CreateApiFailure(status)),
                    [Is].EqualTo("Read-only Scopevisio phase=Authorize; failure=ScopevisioApi; HTTP=unknown."))
    End Sub

    <Test>
    Public Sub BridgeStructuredStatusSurvivesWhenNoInnerHttpExceptionExists()
        Dim failure As New Net.WebException("secret bridge response")
        failure.Data("CompuMaster.Scopevisio.Teamwork.HttpStatusCode") = 500
        failure.Data("Authorization") = "secret bearer"
        Assert.That(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", "CollectionsSync", failure),
                    [Is].EqualTo("Read-only Scopevisio phase=CollectionsSync; failure=TeamworkHttp; HTTP=500."))
    End Sub

    <TestCase("500"), TestCase(0), TestCase(600)>
    Public Sub BridgeStatusRequiresTheExactKeyAndABoundedInteger(value As Object)
        Dim failure As New Net.WebException("secret response HTTP500")
        failure.Data("HttpStatusCode") = 500
        failure.Data("CompuMaster.Scopevisio.Teamwork.HttpStatusCode") = value
        Assert.That(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", "CollectionsSync", failure),
                    [Is].EqualTo("Read-only Scopevisio phase=CollectionsSync; failure=WebRequest; HTTP=unknown."))
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub AuthoritativeResponseStatusOverridesConflictingTransportCause(bridge As Boolean)
        Dim constructor = GetType(Net.Http.HttpRequestException).GetConstructor({GetType(String), GetType(Exception), GetType(Nullable(Of Net.HttpStatusCode))})
        Dim inner As Exception = If(constructor Is Nothing, New Net.Http.HttpRequestException("secret transport cause"),
            DirectCast(constructor.Invoke(New Object() {"secret transport cause", Nothing, CType(Net.HttpStatusCode.Forbidden, Nullable(Of Net.HttpStatusCode))}), Exception))
        Dim failure As Exception
        If bridge Then
            failure = New Net.WebException("secret bridge response", inner)
            failure.Data("CompuMaster.Scopevisio.Teamwork.HttpStatusCode") = 500
        Else
            failure = New CenterDevice.Rest.Exceptions.InternalServerErrorException("secret SDK response", inner)
        End If
        Dim kind = If(bridge, "TeamworkHttp", "CenterDeviceHttp")
        Assert.That(ReadOnlyDiagnosticSupport.SafeFailure("Scopevisio", "CollectionsSync", failure),
                    [Is].EqualTo("Read-only Scopevisio phase=CollectionsSync; failure=" & kind & "; HTTP=500."))
    End Sub

    Private Shared Function CreateApiFailure(status As Integer) As Exception
        Dim constructor = GetType(Global.CompuMaster.Scopevisio.OpenApi.Client.ApiException).GetConstructor(
            Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic, Nothing, {GetType(Integer), GetType(String)}, Nothing)
        Return DirectCast(constructor.Invoke(New Object() {status, "secret response body"}), Exception)
    End Function
End Class
