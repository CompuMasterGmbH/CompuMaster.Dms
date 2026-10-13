Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http
Imports NUnit.Framework
Imports CompuMaster.Dms.Providers

<TestFixture>
Public Class OcsSharingDiscoveryDiagnosticTest

    <Test>
    Public Sub FailureSummaryDoesNotExposeMessagesOrExceptionData()
        Dim failure As New InvalidOperationException("secret user URL token", New HttpRequestException("secret response body"))
        failure.Data("Authorization") = "secret bearer"
        Dim diagnostic = OcsSharingDiscoveryDiagnostic.Create(OcsSharingDiscoveryOutcome.Unavailable, OcsSharingDiscoveryPhase.ShareListing, failure)
        Assert.That(diagnostic.ToSafeSummary(), [Is].EqualTo("OCS sharing discovery: outcome=Unavailable; phase=ShareListing; failure=HttpRequest; HTTP=unknown."))
    End Sub

    <Test>
    Public Sub NumericHttpStatusSurvivesWrappedFailuresWhenSupportedByTheRuntime()
        Dim constructor = GetType(HttpRequestException).GetConstructor(New Type() {GetType(String), GetType(Exception), GetType(Nullable(Of HttpStatusCode))})
        If constructor Is Nothing Then
            Assert.That(OcsSharingDiscoveryDiagnostic.Create(OcsSharingDiscoveryOutcome.Unavailable, OcsSharingDiscoveryPhase.Capabilities,
                New HttpRequestException("secret")).HttpStatus.HasValue, [Is].False)
            Return
        End If
        Dim failure = DirectCast(constructor.Invoke(New Object() {"secret endpoint", Nothing, CType(HttpStatusCode.Forbidden, Nullable(Of HttpStatusCode))}), Exception)
        Dim diagnostic = OcsSharingDiscoveryDiagnostic.Create(OcsSharingDiscoveryOutcome.Unavailable, OcsSharingDiscoveryPhase.Capabilities,
            New Exception("secret wrapper", failure))
        Assert.That(diagnostic.HttpStatus, [Is].EqualTo(403))
        Assert.That(diagnostic.ToSafeSummary(), Does.Not.Contain("secret"))
    End Sub

    <Test>
    Public Sub UnavailableAndDisabledRemainDistinctWithoutEnablingSharing()
        Dim unavailable = OcsSharingDiscoveryDiagnostic.Create(OcsSharingDiscoveryOutcome.Unavailable, OcsSharingDiscoveryPhase.CapabilityPolicy,
            New IO.InvalidDataException("secret malformed response"))
        Dim disabled = OcsSharingDiscoveryDiagnostic.Create(OcsSharingDiscoveryOutcome.Disabled, OcsSharingDiscoveryPhase.CapabilityPolicy)
        Assert.That(unavailable.FailureKind, [Is].EqualTo("Protocol"))
        Assert.That(disabled.FailureKind, [Is].EqualTo("None"))
        Dim provider As New WebDavDmsProvider()
        Assert.That(provider.SupportsSharingSetup, [Is].False)
        Assert.That(provider.OcsSharingDiscoveryForTesting.Outcome, [Is].EqualTo(OcsSharingDiscoveryOutcome.NotStarted))
    End Sub

    <Test>
    Public Sub GenericEndpointRetainsWebDavFallbackAndReportsUnrecognizedDiscovery()
        Dim provider As New WebDavDmsProvider()
        Dim initialize = GetType(WebDavDmsProvider).GetMethod("TryInitializeOcsSharingClient", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
        'An unrecognized path returns before constructing a transport or sending any request.
        initialize.Invoke(provider, New Object() {"https://generic.example.test/directory/", "unused-user", "unused-password"})
        Assert.That(provider.SupportsSharingSetup, [Is].False)
        Assert.That(provider.OcsSharingClientForTesting, [Is].Null)
        Assert.That(provider.OcsSharingDiscoveryForTesting.Outcome, [Is].EqualTo(OcsSharingDiscoveryOutcome.EndpointUnrecognized))
        Assert.That(provider.OcsSharingDiscoveryForTesting.ToSafeSummary(), Does.Not.Contain("example.test"))
    End Sub

    <Test>
    Public Sub ClosedResponseCannotInterruptFallbackDiagnostics()
        'Creates and closes an empty response locally; no request or transport exists.
        Dim response = DirectCast(Activator.CreateInstance(GetType(HttpWebResponse)), HttpWebResponse)
        response.Close()
        Dim failure As New WebException("secret response", Nothing, WebExceptionStatus.ProtocolError, response)
        Dim diagnostic As OcsSharingDiscoveryDiagnostic = Nothing
        Assert.DoesNotThrow(Sub() diagnostic = OcsSharingDiscoveryDiagnostic.Create(OcsSharingDiscoveryOutcome.Unavailable, OcsSharingDiscoveryPhase.Capabilities, failure))
        Assert.That(diagnostic.FailureKind, [Is].EqualTo("WebRequest"))
        Assert.That(diagnostic.HttpStatus.HasValue, [Is].False)
        Assert.That(diagnostic.ToSafeSummary(), Does.Not.Contain("secret"))
    End Sub
End Class
