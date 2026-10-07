Imports System.Collections
Imports System.Globalization
Imports System.Resources
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable>
Public Class ProviderLocalizationTest
    Private Shared ReadOnly Resources As New ResourceManager("CompuMaster.Dms.ProviderStrings", GetType(BaseDmsProvider).Assembly)

    <Test>
    Public Sub GermanSatelliteContainsEveryNeutralResourceWithoutFallback()
        Dim neutral = Resources.GetResourceSet(CultureInfo.InvariantCulture, True, False)
        Dim german = Resources.GetResourceSet(CultureInfo.GetCultureInfo("de"), True, False)
        Assert.That(german, [Is].Not.Null)
        Dim expected = neutral.Cast(Of DictionaryEntry)().Select(Function(entry) CStr(entry.Key)).OrderBy(Function(key) key).ToArray()
        Dim actual = german.Cast(Of DictionaryEntry)().Select(Function(entry) CStr(entry.Key)).OrderBy(Function(key) key).ToArray()
        Assert.That(actual, [Is].EqualTo(expected))
        For Each key In expected
            Dim original = CStr(neutral.GetString(key))
            Dim translated = CStr(german.GetString(key))
            Assert.That(translated, [Is].Not.Empty, key)
            Dim placeholders = Text.RegularExpressions.Regex.Matches(original, "\{\d+(?:[^}]*)\}").Cast(Of Text.RegularExpressions.Match)().Select(Function(match) match.Value).OrderBy(Function(value) value).ToArray()
            Dim translatedPlaceholders = Text.RegularExpressions.Regex.Matches(translated, "\{\d+(?:[^}]*)\}").Cast(Of Text.RegularExpressions.Match)().Select(Function(match) match.Value).OrderBy(Function(value) value).ToArray()
            Assert.That(translatedPlaceholders, [Is].EqualTo(placeholders), key)
        Next
    End Sub

    <TestCase("en-US", "File not found: fixture"), TestCase("de-DE", "Datei nicht gefunden: fixture"), TestCase("de-AT", "Datei nicht gefunden: fixture"), TestCase("ko-KR", "File not found: fixture")>
    Public Async Function ExceptionMessagesFollowUICultureAcrossAwaitAndRetainTheirContracts(culture As String, expected As String) As Task
        Dim originalCulture = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            Await Task.Yield()
            Dim inner As New InvalidOperationException("server diagnostic")
            Dim failure As New CompuMaster.Dms.Data.FileNotFoundException("fixture", inner)
            Assert.That(failure.Message, [Is].EqualTo(expected))
            Assert.That(failure.RemotePath, [Is].EqualTo("fixture"))
            Assert.That(failure.InnerException, [Is].SameAs(inner))
        Finally
            CultureInfo.CurrentUICulture = originalCulture
        End Try
    End Function

    <Test>
    Public Sub FormattingCultureIsIndependentFromMessageLanguage()
        Dim originalUI = CultureInfo.CurrentUICulture
        Dim original = CultureInfo.CurrentCulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en")
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE")
            Assert.That(Global.CompuMaster.Dms.ProviderStrings.Format("Failed", 1.5D, "diagnostic"), [Is].EqualTo("1,5 failed: diagnostic"))
        Finally
            CultureInfo.CurrentUICulture = originalUI
            CultureInfo.CurrentCulture = original
        End Try
    End Sub
End Class
