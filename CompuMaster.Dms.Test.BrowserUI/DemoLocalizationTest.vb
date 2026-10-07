Imports System.Globalization
Imports System.Resources
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class DemoLocalizationTest
    <TestCase("en-US", "&User name", "&Sign in")>
    <TestCase("de-DE", "&Benutzername", "&Anmelden")>
    <TestCase("de-AT", "&Benutzername", "&Anmelden")>
    <TestCase("ko-KR", "&User name", "&Sign in")>
    Public Sub EveryDemoLoadsSharedLoginResourcesWithoutReadingOrChangingCredentials(cultureName As String, userLabel As String, signIn As String)
        Dim original = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Using webdav As New Global.CompuMaster.Dms.TestDemo.WebDav.LoginForm(Nothing),
                  owncloud As New Global.CompuMaster.Dms.TestDemo.OwnCloudClassic.LoginForm(Nothing),
                  nextcloud As New Global.CompuMaster.Dms.TestDemo.Nextcloud.LoginForm(Nothing),
                  teamwork As New Global.CompuMaster.Dms.TestDemo.ScopevisioTeamwork.LoginForm(Nothing)
                For Each form As Form In New Form() {webdav, owncloud, nextcloud, teamwork}
                    Assert.That(form.Controls.Find("UsernameLabel", True).Single().Text, [Is].EqualTo(userLabel))
                    Dim button = CType(form.Controls.Find("OK", True).Single(), Button)
                    Assert.That(button.Text, [Is].EqualTo(signIn))
                    Assert.That(button.ClientSize.Width, [Is].GreaterThanOrEqualTo(button.GetPreferredSize(Drawing.Size.Empty).Width))
                    Assert.That(form.Controls.Find("PasswordTextBox", True).Single().Text, [Is].Empty, "Constructing a localized form must not load persisted credentials.")
                    Dim manager As New ResourceManager(form.GetType().Namespace & ".DemoStrings", form.GetType().Assembly)
                    Assert.That(manager.GetString("ServerUrl", CultureInfo.GetCultureInfo("de")), [Is].EqualTo("WebDAV-Server-&URL"))
                Next
            End Using
        Finally
            CultureInfo.CurrentUICulture = original
        End Try
    End Sub
End Class
