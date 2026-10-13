Option Explicit On
Option Strict On

Imports System.Net
Imports System.Net.Http

Namespace Providers

    Friend Enum OcsSharingDiscoveryPhase
        EndpointRecognition
        Capabilities
        ShareListing
        CapabilityPolicy
        CapabilityFamily
    End Enum

    Friend Enum OcsSharingDiscoveryOutcome
        NotStarted
        EndpointUnrecognized
        Unavailable
        Disabled
        Available
    End Enum

    'Contains only fixed labels and a numeric status, never the original exception or response.
    Friend NotInheritable Class OcsSharingDiscoveryDiagnostic

        Private Sub New(outcome As OcsSharingDiscoveryOutcome, phase As OcsSharingDiscoveryPhase, failureKind As String, httpStatus As Integer?)
            Me.Outcome = outcome
            Me.Phase = phase
            Me.FailureKind = failureKind
            Me.HttpStatus = httpStatus
        End Sub

        Friend ReadOnly Property Outcome As OcsSharingDiscoveryOutcome
        Friend ReadOnly Property Phase As OcsSharingDiscoveryPhase
        Friend ReadOnly Property FailureKind As String
        Friend ReadOnly Property HttpStatus As Integer?

        Friend Shared Function Create(outcome As OcsSharingDiscoveryOutcome, phase As OcsSharingDiscoveryPhase, Optional failure As Exception = Nothing) As OcsSharingDiscoveryDiagnostic
            Dim failureKind = "None"
            Dim status As Integer? = Nothing
            Dim current = failure
            For depth = 0 To 7
                If current Is Nothing Then Exit For
                If TypeOf current Is HttpRequestException Then
                    failureKind = "HttpRequest"
                    'StatusCode is unavailable in the net48/netstandard2 reference assemblies.
                    Try
                        Dim statusProperty = GetType(HttpRequestException).GetProperty("StatusCode")
                        Dim value = statusProperty?.GetValue(current)
                        If TypeOf value Is HttpStatusCode Then status = CInt(DirectCast(value, HttpStatusCode))
                    Catch
                        'Optional diagnostics must not interrupt the generic WebDAV fallback.
                    End Try
                ElseIf TypeOf current Is WebException Then
                    failureKind = "WebRequest"
                    Try
                        Dim response = TryCast(DirectCast(current, WebException).Response, HttpWebResponse)
                        If response IsNot Nothing Then status = CInt(response.StatusCode)
                    Catch
                        'Closed responses can throw when their status is read. Keep any known status.
                    End Try
                ElseIf failureKind = "None" OrElse failureKind = "Other" Then
                    If TypeOf current Is Newtonsoft.Json.JsonException Then
                        failureKind = "JsonPayload"
                    ElseIf TypeOf current Is InvalidCastException Then
                        failureKind = "PayloadShape"
                    ElseIf TypeOf current Is OperationCanceledException Then
                        failureKind = "Cancellation"
                    ElseIf TypeOf current Is FormatException Then
                        failureKind = "Format"
                    ElseIf TypeOf current Is ArgumentException Then
                        failureKind = "Argument"
                    ElseIf TypeOf current Is IO.InvalidDataException OrElse TypeOf current Is InvalidOperationException Then
                        failureKind = "Protocol"
                    ElseIf failureKind = "None" Then
                        failureKind = "Other"
                    End If
                End If
                current = current.InnerException
            Next
            If status.HasValue AndAlso (status.Value < 100 OrElse status.Value > 599) Then status = Nothing
            Return New OcsSharingDiscoveryDiagnostic(outcome, phase, failureKind, status)
        End Function

        Friend Function ToSafeSummary() As String
            Return "OCS sharing discovery: outcome=" & Me.Outcome.ToString() & "; phase=" & Me.Phase.ToString() &
                "; failure=" & Me.FailureKind & "; HTTP=" & If(Me.HttpStatus.HasValue, Me.HttpStatus.Value.ToString(Globalization.CultureInfo.InvariantCulture), "unknown") & "."
        End Function
    End Class
End Namespace
