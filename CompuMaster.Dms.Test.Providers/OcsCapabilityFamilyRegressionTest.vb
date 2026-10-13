Option Explicit On
Option Strict On

Imports NUnit.Framework
Imports Newtonsoft.Json.Linq
Imports CompuMaster.Dms.Providers

<TestFixture>
Public Class OcsCapabilityFamilyRegressionTest
    <TestCaseSource(NameOf(ClassicFamilyCases))>
    Public Sub OptionalSpacesMetadataDoesNotDisableClassicSharing(product As String, spacesJson As String, infiniteScale As Boolean)
        Dim document = ClassicCapabilities(product)
        If spacesJson IsNot Nothing Then DirectCast(document.SelectToken("ocs.data.capabilities"), JObject)("spaces") = JToken.Parse(spacesJson)
        Dim json = document.ToString()
        Dim family = OcsSharingClientAdapter.DetectServerFamily(Nothing, json)
        Dim classicFamily = If(product = "ownCloud", OcsServerFamily.OwnCloudServer, OcsServerFamily.Nextcloud)
        Assert.That(family, [Is].EqualTo(If(infiniteScale, OcsServerFamily.OwnCloudInfiniteScale, classicFamily)))
        Dim capabilities = OcsSharingClientAdapter.ParseCapabilities(json, family, False)
        Assert.That(capabilities.SupportsUserShares, [Is].True)
        Assert.That(capabilities.SupportsLinkShares, [Is].True)
    End Sub

    <TestCase("productname", "{}"), TestCase("productname", "[]")>
    <TestCase("product", "{}"), TestCase("product", "[]")>
    Public Sub NonStringOptionalProductMetadataUsesConservativeFamilyFallback(field As String, value As String)
        Dim document = ClassicCapabilities()
        DirectCast(document.SelectToken("ocs.data.version"), JObject)(field) = JToken.Parse(value)
        Dim family = OcsSharingClientAdapter.DetectServerFamily(Nothing, document.ToString())
        Assert.That(family, [Is].EqualTo(If(field = "productname", OcsServerFamily.Unknown, OcsServerFamily.OwnCloudServer)))
        Assert.That(OcsSharingClientAdapter.ParseCapabilities(document.ToString(), family, False).SupportsLinkShares, [Is].True)
    End Sub

    Private Shared Iterator Function ClassicFamilyCases() As IEnumerable(Of TestCaseData)
        For Each product In {"ownCloud", "Nextcloud"}
            For Each spaces In New String() {Nothing, "null", "{}", "{'enabled':null}", "{'enabled':false}", "{'enabled':'true'}", "{'enabled':{}}", "{'enabled':true}"}
                Yield New TestCaseData(product, spaces, spaces = "{'enabled':true}")
            Next
        Next
    End Function

    Private Shared Function ClassicCapabilities(Optional product As String = "ownCloud") As JObject
        Dim document = JObject.Parse("{'ocs':{'meta':{'statuscode':100},'data':{'version':{},'capabilities':{'files_sharing':{'api_enabled':true,'public':{'enabled':true}}}}}}")
        DirectCast(document.SelectToken("ocs.data.version"), JObject)("productname") = product
        Return document
    End Function
End Class
