Option Explicit On
Option Strict On

Imports System.Net
Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioNativeUploadBoundaryDiagnosticTest
    <Test>
    Public Sub ReportsOnlyBadRequestStatusAndNumericBackendCodeWithoutPrivateResponseText()
        Dim response As New Global.CenterDevice.Rest.Clients.DefaultErrorResponse With {.Code = 103, .Message = "private-response-text"}
        Dim failure As New Global.CenterDevice.Rest.Exceptions.BadRequestException("private-message", Nothing, response)
        Assert.That(ScopevisioNativeUploadBoundaryTest.DescribeRejection(failure), [Is].EqualTo("HTTP 400; backend code 103"))
    End Sub

    <TestCase(500), TestCase(502), TestCase(503)>
    Public Sub RetainsSpecificServerStatusWithoutTreatingItAsATimeout(status As Integer)
        Dim failure As New WebException("private-message")
        failure.Data("CompuMaster.Scopevisio.Teamwork.HttpStatusCode") = status
        Assert.That(ScopevisioNativeUploadBoundaryTest.DescribeRejection(failure), [Is].EqualTo("HTTP " & status.ToString(Globalization.CultureInfo.InvariantCulture)))
    End Sub

    <Test>
    Public Sub DoesNotTurnCancellationOrUnclassifiedFailuresIntoAcceptedRejectionEvidence()
        Assert.That(ScopevisioNativeUploadBoundaryTest.DescribeRejection(New OperationCanceledException()), [Is].Null)
        Assert.That(ScopevisioNativeUploadBoundaryTest.DescribeRejection(New IO.IOException("private-message")), [Is].Null)
        Assert.That(ScopevisioNativeUploadBoundaryTest.DescribeRejection(New WebException("private-message")), [Is].Null)
    End Sub

    <Test>
    Public Sub RetainsBothObservationsWhenTheConsoleLoggerOmitsSuccessfulTestOutput()
        Dim directory = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        IO.Directory.CreateDirectory(directory)
        Try
            ScopevisioNativeUploadBoundaryTest.WriteObservation(directory, "8388608 bytes accepted; SHA-256 verified.")
            ScopevisioNativeUploadBoundaryTest.WriteObservation(directory, "12582912 bytes rejected; HTTP 500. No unchanged replay.")
            Assert.That(IO.File.ReadAllLines(IO.Path.Combine(directory, "upload-boundary-observations.txt")), [Is].EqualTo(New String() {
                "8388608 bytes accepted; SHA-256 verified.",
                "12582912 bytes rejected; HTTP 500. No unchanged replay."
            }))
        Finally
            IO.File.Delete(IO.Path.Combine(directory, "upload-boundary-observations.txt"))
            IO.Directory.Delete(directory)
        End Try
    End Sub

    <Test>
    Public Sub LocalGateRequiresWks08AndAnExplicitUnexpiredCoordinatedWindowWithoutClaimingCi()
        Dim now = New DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero)
        Assert.DoesNotThrow(Sub() ScopevisioNativeLiveTest.ValidateExclusiveLocalBoundary("WKS08", Nothing, "2026-10-07T06:00:00+02:00", now))
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioNativeLiveTest.ValidateExclusiveLocalBoundary("OTHER", Nothing, "2026-10-07T06:00:00+02:00", now))
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioNativeLiveTest.ValidateExclusiveLocalBoundary("WKS08", "true", "2026-10-07T06:00:00+02:00", now))
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioNativeLiveTest.ValidateExclusiveLocalBoundary("WKS08", Nothing, "2026-10-07T06:00:00", now))
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioNativeLiveTest.ValidateExclusiveLocalBoundary("WKS08", Nothing, "2026-10-07T03:04:00Z", now))
    End Sub

    <Test>
    Public Sub LargeLocalExecutionRequiresItsWholeBoundedTestAndCleanupWindow()
        Dim now = New DateTimeOffset(2026, 10, 7, 3, 0, 0, TimeSpan.Zero)
        Assert.Throws(Of InvalidOperationException)(Sub() ScopevisioLiveTestResourceScope.ValidateExclusiveLocalBoundary("WKS08", Nothing, "2026-10-07T03:25:00Z", now, 26))
        Assert.DoesNotThrow(Sub() ScopevisioLiveTestResourceScope.ValidateExclusiveLocalBoundary("WKS08", Nothing, "2026-10-07T03:26:00Z", now, 26))
    End Sub
End Class
