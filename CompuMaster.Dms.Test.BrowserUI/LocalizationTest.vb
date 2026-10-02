Imports System.Collections
Imports System.Globalization
Imports System.Reflection
Imports System.Resources
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

#Disable Warning BC40000

<TestFixture>
<NonParallelizable>
<Apartment(ApartmentState.STA)>
Public Class LocalizationTest

    Private Shared ReadOnly UiResourceManager As New ResourceManager("CompuMaster.Dms.BrowserUI.UiStrings", GetType(Global.CompuMaster.Dms.BrowserUI.DmsBrowser).Assembly)

    <Test>
    Public Sub NeutralAndUnsupportedCulturesUseEnglishResources()
        ClassicAssert.AreEqual("&Cancel", UiResourceManager.GetString("ActionCancel", CultureInfo.InvariantCulture))
        ClassicAssert.AreEqual("&Cancel", UiResourceManager.GetString("ActionCancel", CultureInfo.GetCultureInfo("en-US")))
        ClassicAssert.AreEqual("&Cancel", UiResourceManager.GetString("ActionCancel", CultureInfo.GetCultureInfo("fr-FR")))
    End Sub

    <Test>
    Public Sub GermanCultureUsesGermanResources()
        ClassicAssert.AreEqual("&Abbrechen", UiResourceManager.GetString("ActionCancel", CultureInfo.GetCultureInfo("de-DE")))
        ClassicAssert.AreEqual("Aktualisieren", UiResourceManager.GetString("ActionRefreshFiles", CultureInfo.GetCultureInfo("de-AT")))
    End Sub

    <Test>
    Public Sub GermanResourceContainsEveryNeutralResourceKey()
        Dim neutralKeys As HashSet(Of String) = GetResourceKeys(CultureInfo.InvariantCulture)
        Dim germanKeys As HashSet(Of String) = GetResourceKeys(CultureInfo.GetCultureInfo("de"))

        CollectionAssert.AreEquivalent(neutralKeys, germanKeys)
    End Sub

    <TestCase("en-US", "&Cancel", "Refresh", "General settings", "&Close", "Sharings with internal users/groups")>
    <TestCase("de-DE", "&Abbrechen", "Aktualisieren", "Allgemeine Einstellungen", "&Schließen", "Freigaben an interne Benutzer/Gruppen")>
    <TestCase("fr-FR", "&Cancel", "Refresh", "General settings", "&Close", "Sharings with internal users/groups")>
    Public Sub FormsApplyCurrentUICulture(cultureName As String, expectedCancel As String, expectedRefresh As String, expectedGeneralSettings As String, expectedClose As String, expectedInternalSharings As String)
        RunWithCulture(cultureName,
            Sub()
                Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
                    ClassicAssert.AreEqual(expectedCancel, GetField(Of Button)(browser, "ButtonCancel").Text)
                    ClassicAssert.AreEqual(expectedRefresh, GetField(Of ToolStripButton)(browser, "ToolStripButtonRefreshFilesList").Text)
                End Using

                Using linkSetup As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
                    ClassicAssert.AreEqual(expectedCancel, GetField(Of Button)(linkSetup, "ButtonCancel").Text)
                    ClassicAssert.AreEqual(expectedGeneralSettings, GetField(Of GroupBox)(linkSetup, "GroupBoxGeneral").Text)
                End Using

                Using sharingSetup As New Global.CompuMaster.Dms.BrowserUI.DmsStandardShareSetup()
                    ClassicAssert.AreEqual(expectedCancel, GetField(Of Button)(sharingSetup, "ButtonCancel").Text)
                    ClassicAssert.AreEqual(expectedGeneralSettings, GetField(Of GroupBox)(sharingSetup, "GroupBoxGeneral").Text)
                End Using

                Using sharings As New Global.CompuMaster.Dms.BrowserUI.DmsItemSharings()
                    ClassicAssert.AreEqual(expectedClose, GetField(Of Button)(sharings, "ButtonCancel").Text)
                    ClassicAssert.AreEqual(expectedInternalSharings, GetField(Of GroupBox)(sharings, "GroupBoxInternalSharings").Text)
                End Using
            End Sub)
    End Sub

    <TestCase("en-US")>
    <TestCase("de-DE")>
    Public Sub LocalizedFixedWidthButtonsFitTheirText(cultureName As String)
        RunWithCulture(cultureName,
            Sub()
                Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser()
                    AssertButtonFits(GetField(Of Button)(browser, "ButtonCancel"))
                    AssertButtonFits(GetField(Of Button)(browser, "ButtonOkay"))
                    AssertButtonFits(GetField(Of Button)(browser, "ButtonCreateNewFolder"))
                End Using

                Using linkSetup As New Global.CompuMaster.Dms.BrowserUI.DmsLinkShareSetup()
                    AssertButtonFits(GetField(Of Button)(linkSetup, "ButtonCancel"))
                    AssertButtonFits(GetField(Of Button)(linkSetup, "ButtonSave"))
                End Using

                Using sharingSetup As New Global.CompuMaster.Dms.BrowserUI.DmsStandardShareSetup()
                    AssertButtonFits(GetField(Of Button)(sharingSetup, "ButtonCancel"))
                    AssertButtonFits(GetField(Of Button)(sharingSetup, "ButtonSave"))
                End Using

                Using sharings As New Global.CompuMaster.Dms.BrowserUI.DmsItemSharings()
                    AssertButtonFits(GetField(Of Button)(sharings, "ButtonCancel"))
                End Using
            End Sub)
    End Sub

    Private Shared Sub AssertButtonFits(button As Button)
        Dim preferredWidth As Integer = button.GetPreferredSize(Drawing.Size.Empty).Width
        Assert.That(button.ClientSize.Width, [Is].GreaterThanOrEqualTo(preferredWidth), $"{button.Name} is too narrow for '{button.Text}'.")
    End Sub

    Private Shared Function GetField(Of T)(instance As Object, memberName As String) As T
        Dim propertyInfo As PropertyInfo = instance.GetType().GetProperty(memberName, BindingFlags.Instance Or BindingFlags.NonPublic Or BindingFlags.Public)
        ClassicAssert.IsNotNull(propertyInfo, memberName)
        Return DirectCast(propertyInfo.GetValue(instance), T)
    End Function

    Private Shared Function GetResourceKeys(culture As CultureInfo) As HashSet(Of String)
        Dim result As New HashSet(Of String)(StringComparer.Ordinal)
        Dim resourceSet As ResourceSet = UiResourceManager.GetResourceSet(culture, True, True)
        For Each entry As DictionaryEntry In resourceSet
            result.Add(DirectCast(entry.Key, String))
        Next
        Return result
    End Function

    Private Shared Sub RunWithCulture(cultureName As String, action As Action)
        Dim originalCulture As CultureInfo = CultureInfo.CurrentCulture
        Dim originalUICulture As CultureInfo = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName)
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            action()
        Finally
            CultureInfo.CurrentCulture = originalCulture
            CultureInfo.CurrentUICulture = originalUICulture
        End Try
    End Sub

End Class

#Enable Warning BC40000
