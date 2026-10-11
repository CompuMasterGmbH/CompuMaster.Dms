Imports System.Globalization
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable>
Public Class CenterDeviceCreationErrorTest
    <TestCase("en-US", "failed"), TestCase("de-DE", "fehlgeschlagen"), TestCase("de-AT", "fehlgeschlagen"), TestCase("ko-KR", "failed")>
    Public Sub CreationFailureRetainsTheOperationPathLanguageAndOriginalCause(culture As String, word As String)
        Dim originalCulture = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            Dim cause As New CenterDevice.Rest.Exceptions.RestClientException("original server diagnostic")
            For Each operation In New String() {"CreateFolder", "CreateDirectory", "CreateCollection"}
                Dim failure = CenterDeviceDmsProviderBase.DirectoryCreationFailure(operation, "Heizung {0} 日本語", cause)
                Assert.That(failure.Message, [Is].EqualTo(operation & " " & word & ": Heizung {0} 日本語"))
                Assert.That(failure.InnerException, [Is].SameAs(cause))
                Assert.That(cause.Message, [Is].EqualTo("original server diagnostic"))
            Next
        Finally
            CultureInfo.CurrentUICulture = originalCulture
        End Try
    End Sub
End Class
