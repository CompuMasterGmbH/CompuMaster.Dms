Option Explicit On
Option Strict On

Imports System.Drawing
Imports System.Threading
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("LocalizationExtended")>
Public Class LocalizedFontAcceptanceTest
    Public Shared Iterator Function FontCases() As IEnumerable(Of TestCaseData)
        For Each culture In {"en-US", "de-DE", "fr-FR", "es-ES", "zh-CN", "zh-TW", "ja-JP", "ar-SA", "he-IL", "hi-IN"}
            For Each family In {"Segoe UI", "Arial", "Tahoma"}
                Yield New TestCaseData(culture, family)
            Next
        Next
    End Function

    <TestCaseSource(NameOf(FontCases))>
    Public Sub InstalledFontsFitLocalizedDialogs(culture As String, family As String)
        If Environment.GetEnvironmentVariable("DMS_LOCALIZATION_EXTENDED_TESTS") <> "1" Then
            Assert.Ignore("Opt in with DMS_LOCALIZATION_EXTENDED_TESTS=1 for extended workstation localization checks.")
        End If
        Using installed As New Text.InstalledFontCollection()
            If Not installed.Families.Any(Function(font) font.Name = family) Then Assert.Ignore("Font is not installed: " & family)
        End Using
        LocalizedLayoutAcceptanceTest.VerifyLayouts(culture, 1.5F, family)
    End Sub
End Class
