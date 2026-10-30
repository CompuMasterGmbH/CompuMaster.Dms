Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA)>
Public Class AppearanceTests
    <TestCase(Nothing, False), TestCase(0, True), TestCase(1, False)>
    Public Sub MissingWindowsPreferenceUsesTheLightDefault(value As Object, dark As Boolean)
        Assert.That(BrowserAppearance.UsesDarkWindowsPreference(value), [Is].EqualTo(dark))
    End Sub
    <Test>
    Public Sub EmbeddedScopesThemeDynamicChildrenWithoutChangingTheHost()
        Using host As New Form(), scope As New Panel(), input As New TextBox(), added As New TextBox()
            host.Controls.Add(scope) : scope.Controls.Add(input)
            Dim hostColor = host.BackColor
            BrowserAppearance.SetTheme(scope, BrowserTheme.Dark)
            scope.Controls.Add(added)
            Assert.That(input.BackColor.GetBrightness(), [Is].LessThan(0.3F))
            Assert.That(added.BackColor, [Is].EqualTo(input.BackColor))
            Assert.That(host.BackColor, [Is].EqualTo(hostColor))
            BrowserAppearance.SetTheme(scope, BrowserTheme.Light)
            Assert.That(scope.BackColor.GetBrightness(), [Is].GreaterThan(0.8F))
            Assert.That(added.BackColor.GetBrightness(), [Is].GreaterThan(0.8F))
        End Using
    End Sub
    <Test>
    Public Sub CustomPalettesAreScopedInheritedAndResettable()
        Dim palette As New BrowserPalette("#172334", "#24364A", "#EDF2FA", "#5399D5", "#081524", "#647A90")
        Using owner As New Form(), dialog As New Form(), input As New TextBox()
            dialog.Controls.Add(input)
            BrowserAppearance.Bind(owner) : BrowserAppearance.Bind(dialog)
            BrowserAppearance.SetTheme(owner, BrowserTheme.Dark)
            BrowserAppearance.SetPalettes(owner, Nothing, palette)
            owner.Show() : dialog.Show(owner)
            Assert.That(owner.BackColor, [Is].EqualTo(ColorTranslator.FromHtml(palette.Background)))
            Assert.That(input.BackColor, [Is].EqualTo(ColorTranslator.FromHtml(palette.Surface)))
            Assert.That(input.ForeColor, [Is].EqualTo(ColorTranslator.FromHtml(palette.Foreground)))
            BrowserAppearance.SetPalettes(owner, Nothing, Nothing)
            Assert.That(input.BackColor, [Is].Not.EqualTo(ColorTranslator.FromHtml(palette.Surface)))
            dialog.Close() : owner.Close()
        End Using
        Assert.Throws(Of ArgumentException)(Sub()
                                               Dim invalid = New BrowserPalette("invalid", "#000000", "#ffffff", "#000000", "#ffffff", "#000000")
                                           End Sub)
    End Sub
    <Test>
    Public Sub DarkPaletteRestoresOriginalColorsAndThemesDynamicControls()
        Using form As New Form(), panel As New Panel(), input As New TextBox(), strip As New ToolStrip()
            form.Controls.Add(panel) : panel.Controls.Add(input) : panel.Controls.Add(strip)
            Dim originalBack = input.BackColor
            Dim originalFore = input.ForeColor
            BrowserAppearance.SetTheme(form, BrowserTheme.Dark)
            Assert.That(input.BackColor.GetBrightness(), [Is].LessThan(0.3F))
            Assert.That(input.ForeColor.GetBrightness(), [Is].GreaterThan(0.8F))
            Using added As New TextBox()
                panel.Controls.Add(added)
                Assert.That(added.BackColor, [Is].EqualTo(input.BackColor))
                BrowserAppearance.SetTheme(form, BrowserTheme.Light)
                Assert.That(input.BackColor, [Is].EqualTo(originalBack))
                Assert.That(input.ForeColor, [Is].EqualTo(originalFore))
                Assert.That(added.BackColor, [Is].EqualTo(originalBack))
            End Using
        End Using
    End Sub
    <Test>
    Public Sub OwnedFormsInheritAppearanceAndRespectExplicitOverrides()
        Using owner As New Form(), dialog As New Form(), field As New TextBox()
            dialog.Controls.Add(field)
            BrowserAppearance.Bind(owner) : BrowserAppearance.Bind(dialog)
            BrowserAppearance.SetTheme(owner, BrowserTheme.Dark)
            owner.Show() : dialog.Show(owner)
            Assert.That(field.BackColor.GetBrightness(), [Is].LessThan(0.3F))
            BrowserAppearance.SetTheme(owner, BrowserTheme.Light)
            Assert.That(field.BackColor.GetBrightness(), [Is].GreaterThan(0.8F))
            BrowserAppearance.SetTheme(dialog, BrowserTheme.Dark)
            BrowserAppearance.SetTheme(owner, BrowserTheme.Light)
            Assert.That(field.BackColor.GetBrightness(), [Is].LessThan(0.3F))
            dialog.Close() : owner.Close()
        End Using
    End Sub
    <Test>
    Public Sub BrowserModeAndSelectorAreAdditiveAndRejectInvalidValues()
#Disable Warning BC40000
        Using browser As New DmsBrowser()
#Enable Warning BC40000
            Assert.That(browser.Appearance, [Is].EqualTo(BrowserTheme.System))
            browser.Appearance = BrowserTheme.Dark
            Assert.That(browser.BackColor.GetBrightness(), [Is].LessThan(0.3F))
            Assert.Throws(Of ArgumentOutOfRangeException)(Sub() browser.Appearance = CType(99, BrowserTheme))
            Assert.That(browser.Appearance, [Is].EqualTo(BrowserTheme.Dark))
            Using selector = BrowserAppearance.CreateSelector(browser)
                selector.SelectedIndex = CInt(BrowserTheme.Light)
                Assert.That(browser.Appearance, [Is].EqualTo(BrowserTheme.Light))
            End Using
        End Using
    End Sub
End Class
