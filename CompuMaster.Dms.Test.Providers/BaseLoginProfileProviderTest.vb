Option Explicit On
Option Strict On

Imports NUnit.Framework

Public MustInherit Class BaseLoginProfileProviderTest
    Inherits BaseDmsProviderTestBase

    Protected MustOverride Function CreateLoginProfile() As DmsLoginProfile

    <Test> Public Sub ProviderMatchTestingProfile()
        Assert.AreEqual(Me.TestedProvider, Me.CreateLoginProfile.DmsProvider)
    End Sub

    Protected Overrides Function UninitializedDmsProvider() As Dms.Providers.BaseDmsProvider
        Static Result As Dms.Providers.BaseDmsProvider
        If Result Is Nothing Then
            Result = Dms.Providers.CreateDmsProviderInstance(Me.CreateLoginProfile.DmsProvider)
        End If
        Return Result
    End Function

    Protected Overrides Function LoggedInDmsProvider() As Dms.Providers.BaseDmsProvider
        Static Result As Dms.Providers.BaseDmsProvider
        If Result Is Nothing Then
            Result = Dms.Providers.CreateAuthorizedDmsProviderInstance(Me.CreateLoginProfile)
        End If
        Return Result
    End Function

End Class