Imports System.Globalization
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable>
Public Class CenterDeviceLinkDiagnosticTest
    <TestCase("en"), TestCase("de"), TestCase("fr"), TestCase("es"), TestCase("zh-Hans"), TestCase("zh-Hant"), TestCase("ja"), TestCase("ar"), TestCase("he"), TestCase("hi")>
    Public Sub MissingUpdateIdentifierHasTheSameDiagnosticAcrossSyncAndAsync(culture As String)
        Dim originalCulture = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            Dim provider As New ScopevisioTeamworkDmsProvider()
            Dim link As New DmsLink(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.Collection}, provider)
            Dim syncFailure = Assert.Throws(Of InvalidOperationException)(Sub() provider.UpdateLink(link))
            Dim asyncFailure = Assert.ThrowsAsync(Of InvalidOperationException)(Async Function() As Task
                                                                                   Await provider.UpdateLinkAsync(link)
                                                                               End Function)
            Assert.That(asyncFailure.Message, [Is].EqualTo(syncFailure.Message))
        Finally
            CultureInfo.CurrentUICulture = originalCulture
        End Try
    End Sub
End Class
