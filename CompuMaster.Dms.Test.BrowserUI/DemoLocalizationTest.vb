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
    <TestCase("fr-FR", "Nom d’&utilisateur", "Se &connecter")>
    <TestCase("es-ES", "Nombre de &usuario", "Iniciar &sesión")>
    <TestCase("zh-CN", "用户名(&U)", "登录(&S)")>
    <TestCase("zh-TW", "使用者名稱(&U)", "登入(&S)")>
    <TestCase("ja-JP", "ユーザー名", "ログイン")>
    <TestCase("ar-SA", "اسم المستخدم", "تسجيل الدخول")>
    <TestCase("he-IL", "שם משתמש", "כניסה")>
    <TestCase("hi-IN", "उपयोगकर्ता नाम", "साइन इन करें")>
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
                    Dim exitButton = CType(form.Controls.Find("Cancel", True).Single(), Button)
                    Assert.That(button.Right, [Is].LessThan(exitButton.Left), "Localized sign-in and exit buttons must not overlap.")
                    Assert.That(form.Controls.Find("PasswordTextBox", True).Single().Text, [Is].Empty, "Constructing a localized form must not load persisted credentials.")
                    Dim manager As New ResourceManager(form.GetType().Namespace & ".DemoStrings", form.GetType().Assembly)
                    Assert.That(manager.GetString("ServerUrl", CultureInfo.GetCultureInfo("de")), [Is].EqualTo("WebDAV-Server-&URL"))
                    Dim satelliteCulture = CultureInfo.CurrentUICulture
                    While Not satelliteCulture.IsNeutralCulture AndAlso satelliteCulture.Parent IsNot CultureInfo.InvariantCulture
                        satelliteCulture = satelliteCulture.Parent
                    End While
                    If satelliteCulture.Name <> "en" AndAlso satelliteCulture.Name <> "ko" Then
                        Dim neutral = manager.GetResourceSet(CultureInfo.InvariantCulture, True, False)
                        Dim translated = manager.GetResourceSet(satelliteCulture, True, False)
                        Assert.That(translated, [Is].Not.Null, satelliteCulture.Name)
                        For Each entry As Collections.DictionaryEntry In neutral
                            Dim key = CStr(entry.Key)
                            Assert.That(translated.GetString(key), [Is].Not.Null.And.Not.Empty, key)
                            Dim pattern = "\{\d+(?:[^}]*)\}"
                            Assert.That(Text.RegularExpressions.Regex.Matches(translated.GetString(key), pattern).Cast(Of Text.RegularExpressions.Match)().Select(Function(m) m.Value).OrderBy(Function(v) v).ToArray(),
                                        [Is].EqualTo(Text.RegularExpressions.Regex.Matches(CStr(entry.Value), pattern).Cast(Of Text.RegularExpressions.Match)().Select(Function(m) m.Value).OrderBy(Function(v) v).ToArray()), key)
                        Next
                    End If
                Next
            End Using
        Finally
            CultureInfo.CurrentUICulture = original
        End Try
    End Sub
End Class
