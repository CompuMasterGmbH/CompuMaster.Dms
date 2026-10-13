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
End Class
