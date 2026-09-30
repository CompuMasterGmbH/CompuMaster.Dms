Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class ScopevisioAuthorizationExceptionTest

    <TestCase(Nothing)>
    <TestCase("{""status"":401,""message"":""Bad credentials""}")>
    <TestCase("{""status"":401,""message"":""Bad customer-credentials""}")>
    Public Sub Http401IsMappedToUserAuthenticationException(errorContent As String)
        Dim OriginalException As New InvalidOperationException("Error calling Token")

        Dim Result As Exception = ScopevisioTeamworkDmsProvider.CreateAuthorizationException(401, errorContent, OriginalException, True)

        Assert.That(Result, [Is].TypeOf(Of DmsUserAuthenticationException))
        Assert.That(Result.Message, [Is].EqualTo("Scopevisio user authentication failed."))
        Assert.That(Result.InnerException, [Is].SameAs(OriginalException))
    End Sub

    <Test>
    Public Sub Http401AfterTokenRequestIsNotMappedToUserAuthenticationException()
        Dim OriginalException As New InvalidOperationException("Later API request failed")

        Dim Result As Exception = ScopevisioTeamworkDmsProvider.CreateAuthorizationException(401, Nothing, OriginalException, False)

        Assert.That(Result, [Is].TypeOf(Of DmsSystemErrorException))
        Assert.That(Result.InnerException, [Is].SameAs(OriginalException))
    End Sub

    <Test>
    Public Sub Http403IsNotMappedToUserAuthenticationException()
        Dim OriginalException As New InvalidOperationException("Forbidden")

        Dim Result As Exception = ScopevisioTeamworkDmsProvider.CreateAuthorizationException(403, Nothing, OriginalException, True)

        Assert.That(Result, [Is].TypeOf(Of DmsSystemErrorException))
        Assert.That(Result.InnerException, [Is].SameAs(OriginalException))
    End Sub

End Class
