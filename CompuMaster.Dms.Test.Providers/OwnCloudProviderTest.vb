﻿Option Explicit On
Option Strict On

Imports NUnit.Framework

Public Class OwnCloudProviderTest
    Inherits BaseOcsProviderTest

    Public Overrides ReadOnly Property TestedProvider As CompuMaster.Dms.Providers.BaseDmsProvider.DmsProviders = Dms.Providers.BaseDmsProvider.DmsProviders.OwnCloud

    Protected Overrides Function CreateLoginProfile() As DmsLoginProfile
        Dim Settings As New OwnCloudSettings
        Dim username As String = Settings.InputLine("username")
        Dim serverurl As String = Settings.InputLine("server url")
        Dim password As String = Settings.InputLine("password")

        Return New DmsLoginProfile() With {
                            .DmsProvider = Me.TestedProvider,
                            .BaseUrl = serverurl,
                            .Username = username,
                            .Password = password
                            }
    End Function

End Class