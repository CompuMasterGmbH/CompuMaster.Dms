Option Explicit On
Option Strict On

Imports System.Globalization
Imports System.Threading.Tasks
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable>
Public Class CenterDeviceCollectionNameTest
    Private Const EnglishMessage As String = "Cannot create collection ""{0}"". The ""/"" character is interpreted as a path separator. CenterDevice/Scopevisio collections can only be created directly in the root directory. Provide a single collection name without a path, and remove or replace ""/"" in the name."
    Private Shared ReadOnly GermanMessage As String = "Die Sammlung «{0}» kann nicht erstellt werden. Das Zeichen «/» wird als Pfadtrenner interpretiert. CenterDevice-/Scopevisio-Sammlungen können nur direkt im Stammverzeichnis erstellt werden. Übergeben Sie einen einzelnen Sammlungsnamen ohne Pfad und entfernen oder ersetzen Sie «/» im Namen.".Replace("«", ChrW(&H201E)).Replace("»", ChrW(&H201C))

    <TestCase("en-US"), TestCase("de-DE"), TestCase("de-AT"), TestCase("ko-KR")>
    Public Sub NestedCollectionDiagnosticExplainsTheInputAndRemedy(culture As String)
        Dim originalCulture = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            'No IO client is configured: rejection must happen before any server access.
            Dim provider As New ScopevisioTeamworkDmsProvider()
            Dim name = "Customer {0} \ Bereich / Heizung - Uploads 日本語"
            Dim failure = Assert.Throws(Of NotSupportedException)(Sub() provider.CreateCollection(name))
            Assert.That(failure.Message, [Is].EqualTo(String.Format(If(culture.StartsWith("de", StringComparison.Ordinal), GermanMessage, EnglishMessage), name)))
            Assert.That(failure.InnerException, [Is].Null)
        Finally
            CultureInfo.CurrentUICulture = originalCulture
        End Try
    End Sub

    <TestCase("Customer / Division", "Customer "), TestCase("collection/child/grandchild", "collection/child"), TestCase("Customer \ Division", "")>
    Public Sub CollectionPathInterpretationRetainsTheExistingSeparator(name As String, expectedParent As String)
        Dim provider As New ScopevisioTeamworkDmsProvider()
        Assert.That(provider.DirectorySeparator, [Is].EqualTo("/"c))
        Assert.That(provider.ParentDirectoryPath(name), [Is].EqualTo(expectedParent))
    End Sub

    <TestCase("en-US"), TestCase("de-DE"), TestCase("de-AT"), TestCase("ko-KR"), TestCase("fr"), TestCase("es"), TestCase("zh-Hans"), TestCase("zh-Hant"), TestCase("ja"), TestCase("ar"), TestCase("he"), TestCase("hi")>
    Public Sub NestedCollectionDiagnosticsMatchAcrossSyncAndAsync(culture As String)
        Dim originalCulture = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            Dim provider As New ScopevisioTeamworkDmsProvider()
            For Each name In New String() {"Customer {0} \ Bereich / Heizung - Uploads 日本語", "collection/child/grandchild"}
                Dim synchronous = Assert.Throws(Of NotSupportedException)(Sub() provider.CreateCollection(name))
                Dim asynchronous = Assert.ThrowsAsync(Of NotSupportedException)(CType(Async Function()
                                                                                         Await provider.CreateCollectionAsync(name)
                                                                                     End Function, Func(Of Task)))
                Assert.That(asynchronous.Message, [Is].EqualTo(synchronous.Message))
                Assert.That(asynchronous.Message, Does.Contain(name))
                Assert.That(asynchronous.Message, Does.Contain("/"))
                Assert.That(asynchronous.InnerException, [Is].Null)
            Next
        Finally
            CultureInfo.CurrentUICulture = originalCulture
        End Try
    End Sub
End Class
