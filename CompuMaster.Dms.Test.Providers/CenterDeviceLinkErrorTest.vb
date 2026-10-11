Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Exceptions
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceLinkErrorTest
    <TestCase(False), TestCase(True)>
    Public Sub BadRequestRetainsItsCauseAndUsesTheOptionalServerExplanation(explanation As Boolean)
        Dim response As New DefaultErrorResponse With {.Code = 400, .Message = "original server diagnostic"}
        If explanation Then response.Data = New Dictionary(Of String, Object) From {{"explanation", "server explanation {0} 日本語"}}
        Dim cause As New BadRequestException("request failed", Nothing, response)
        Dim failure = CenterDeviceDmsProviderBase.CreateLinkServerFailure(cause)
        Assert.That(failure.Message, [Is].EqualTo(If(explanation, "server explanation {0} 日本語", "original server diagnostic")))
        Assert.That(failure.InnerException, [Is].SameAs(cause))
    End Sub

    <Test>
    Public Sub ForbiddenRetainsTheCauseAndPrefixesTheOriginalServerDiagnostic()
        Dim response As New DefaultErrorResponse With {.Code = 403, .Message = "server diagnostic {0} 日本語"}
        Dim cause As New ForbiddenException("request failed", Nothing, response)
        Dim failure = CenterDeviceDmsProviderBase.CreateLinkServerFailure(cause)
        Assert.That(failure.Message, [Is].EqualTo(Global.CompuMaster.Dms.ProviderStrings.Format("Forbidden", response.Message)))
        Assert.That(failure.InnerException, [Is].SameAs(cause))
    End Sub

    <Test>
    Public Sub MissingStructuredResponseRetainsTheOriginalDiagnostic()
        Dim cause As New RestClientException("original transport diagnostic")
        Dim failure = CenterDeviceDmsProviderBase.CreateLinkServerFailure(cause)
        Assert.That(failure.Message, [Is].EqualTo(cause.Message))
        Assert.That(failure.InnerException, [Is].SameAs(cause))
    End Sub
End Class
