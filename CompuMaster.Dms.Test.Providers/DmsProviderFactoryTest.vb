Imports NUnit.Framework
Imports CompuMaster.Dms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Namespace DmsProviderTests

    <TestFixture, Category("Dms")> Public Class DmsProviderFactoryTest

        Private Function AllDmsProviders() As BaseDmsProvider.DmsProviders()
            Return CType([Enum].GetValues(GetType(BaseDmsProvider.DmsProviders)), BaseDmsProvider.DmsProviders())
        End Function

        <Test> Public Sub CreateDmsProviderInstanceTest()
            Dim UniqueIDs As New List(Of Integer)
            For Each ProviderID As BaseDmsProvider.DmsProviders In AllDmsProviders()
                Dim Provider As BaseDmsProvider
                Provider = CompuMaster.Dms.Providers.CreateDmsProviderInstance(ProviderID)
                Assert.IsNotNull(Provider, "Provider: " & ProviderID.ToString)
                Assert.AreEqual(ProviderID, Provider.DmsProviderID)
                Select Case Provider.WebApiUrlCustomization
                    Case BaseDmsProvider.UrlCustomizationType.WebApiUrlMustBeCustomized
                        Assert.IsNull(Provider.WebApiDefaultUrl, "Provider: " & Provider.Name & " (" & ProviderID.ToString & ")")
                    Case BaseDmsProvider.UrlCustomizationType.WebApiUrlCanBeCustomized
                        Assert.IsNotNull(Provider.WebApiDefaultUrl, "Provider: " & Provider.Name & " (" & ProviderID.ToString & ")")
                    Case BaseDmsProvider.UrlCustomizationType.WebApiUrlNotCustomizable
                        If ProviderID <> BaseDmsProvider.DmsProviders.None Then
                            Assert.IsNotNull(Provider.WebApiDefaultUrl, "Provider: " & Provider.Name & " (" & ProviderID.ToString & ")")
                        End If
                End Select
            Next
        End Sub

        <Test> Public Sub UniqueDmsProviderIDs()
            Dim UniqueIDs As New List(Of Integer)
            For Each ProviderID As BaseDmsProvider.DmsProviders In AllDmsProviders()
                Dim Provider As BaseDmsProvider
                Provider = CompuMaster.Dms.Providers.CreateDmsProviderInstance(ProviderID)
                Assert.IsFalse(UniqueIDs.Contains(ProviderID))
                UniqueIDs.Add(ProviderID)
            Next
        End Sub

    End Class

End Namespace