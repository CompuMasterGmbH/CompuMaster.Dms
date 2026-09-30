Option Explicit On
Option Strict On

Imports NUnit.Framework

<TestFixture, Category("RemoteDms"), Category("Nextcloud")>
Public NotInheritable Class NextcloudWebDavProviderTest
    Inherits WebDavProviderTestBase

    Protected Overrides Function CreateSettings() As SettingsBase
        Return New NextcloudWebDavSettings
    End Function
End Class
