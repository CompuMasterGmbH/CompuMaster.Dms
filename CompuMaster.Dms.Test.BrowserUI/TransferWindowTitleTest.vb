Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class TransferWindowTitleTest
    <TestCase("en-US", False), TestCase("en-US", True), TestCase("de-DE", False), TestCase("de-DE", True)>
    Public Sub ScopevisioDialogsShowTheProviderNameAndKeepTheLocalizedOperation(culture As String, download As Boolean)
        Dim previous = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
            Using browser As New PreviewBrowser(New ScopevisioTeamworkDmsProvider()),
                  dialog = browser.CreateTransferDialog({"file.txt"}, download)
                Assert.That(dialog.Text, [Is].EqualTo(UiStrings.GetText(If(download, "DownloadTitle", "UploadTitle")) & " — Scopevisio Teamwork"))
            End Using
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
    End Sub

    <TestCase("https://dav.example:8443/tenant/remote.php/dav/", "https://dav.example:8443/tenant/remote.php/dav/")>
    <TestCase("https://user:password@cloud.example/nextcloud/?token=secret#private", "https://cloud.example/nextcloud/")>
    <TestCase("http://cloud.example/owncloud/", "http://cloud.example/owncloud/")>
    Public Sub ConfiguredWebDavEndpointsRetainTheirBasePathWithoutUrlCredentials(address As String, displayAddress As String)
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = address}
        Dim profile As New WebDavLoginCredentials With {.BaseUrl = "https://other.example/"}
        Assert.That(TransferWindowTitle.WithTarget("Upload", provider, profile), [Is].EqualTo("Upload — " & displayAddress), "The authorized endpoint takes precedence over profile defaults.")
        Using browser As New PreviewBrowser(provider), preparation = browser.CreateTransferDialog(New String() {}),
              download = browser.CreateTransferDialog({"file.txt"}, True)
            Assert.That(preparation.Text, Does.EndWith(" — " & displayAddress))
            Assert.That(download.Text, Does.EndWith(" — " & displayAddress))
        End Using
    End Sub

    <Test>
    Public Sub OtherCustomizableProvidersUseTheProfileAddressOrTheirDefaultEndpoint()
        Dim provider As New CustomProvider()
        Dim profile As New WebDavLoginCredentials With {.BaseUrl = "https://profile.example/tenant/"}
        Assert.That(TransferWindowTitle.WithTarget("Upload", provider, profile), [Is].EqualTo("Upload — https://profile.example/tenant/"))
        Assert.That(TransferWindowTitle.WithTarget("Upload", provider), [Is].EqualTo("Upload — https://default.example/base/"))
    End Sub

    <TestCase(""), TestCase("invalid endpoint"), TestCase("ftp://user:password@server.example/private/")>
    Public Sub UnavailableOrUnsupportedEndpointsFallBackToTheProviderName(address As String)
        Dim provider As New WebDavDmsProvider With {.CustomWebApiUrl = address}
        Assert.That(TransferWindowTitle.WithTarget("Upload", provider), [Is].EqualTo("Upload — WebDAV"))
        Assert.That(TransferWindowTitle.WithTarget("Upload", Nothing), [Is].EqualTo("Upload"))
    End Sub

    Private Class PreviewBrowser
        Inherits DmsBrowser
        Friend Sub New(provider As BaseDmsProvider)
            MyBase.New(provider)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class

    Private Class CustomProvider
        Inherits NoDmsProvider
        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlCanBeCustomized
            End Get
        End Property
        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return "https://default.example/base/"
            End Get
        End Property
    End Class
End Class
