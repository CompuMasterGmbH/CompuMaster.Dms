Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi.Model
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
Public Class ScopevisioDmsInstanceProviderTest

    <Test>
    Public Sub ListsUniqueTeamworkInstancesAndMarksCurrentInstance()
        Dim Provider As New TestableScopevisioProvider(
            New Organisation() With {.Id = 11, .Name = "Accounting A", .TeamworkTenantId = "tenant-a", .TeamworkTenantName = "Documents A"},
            New Organisation() With {.Id = 12, .Name = "Accounting A duplicate", .TeamworkTenantId = "tenant-a", .TeamworkTenantName = "Documents A"},
            New Organisation() With {.Id = 21, .Name = "Accounting B", .TeamworkTenantId = "tenant-b", .TeamworkTenantName = "Documents B"},
            New Organisation() With {.Id = 31, .Name = "No documents"}
        )
        Provider.CurrentOrganisationID = 21

        Dim Instances As IReadOnlyList(Of DmsInstanceInfo) = Provider.ListAvailableDmsInstances()

        ClassicAssert.AreEqual(2, Instances.Count)
        ClassicAssert.AreEqual("tenant-a", Instances(0).ID)
        ClassicAssert.AreEqual("Documents A", Instances(0).DisplayName)
        ClassicAssert.IsFalse(Instances(0).IsSelected)
        ClassicAssert.AreEqual("tenant-b", Instances(1).ID)
        ClassicAssert.AreEqual("Documents B", Instances(1).DisplayName)
        ClassicAssert.IsTrue(Instances(1).IsSelected)
    End Sub

    <Test>
    Public Sub ReturnsCurrentTeamworkInstance()
        Dim Provider As New TestableScopevisioProvider(
            New Organisation() With {.Id = 11, .Name = "Accounting A", .TeamworkTenantId = "tenant-a", .TeamworkTenantName = "Documents A"}
        )
        Provider.CurrentOrganisationID = 11

        Dim Instance As DmsInstanceInfo = Provider.CurrentDmsInstance

        ClassicAssert.AreEqual("tenant-a", Instance.ID)
        ClassicAssert.AreEqual("Documents A", Instance.DisplayName)
        ClassicAssert.IsTrue(Instance.IsSelected)
    End Sub

    <Test>
    Public Sub ListsCurrentInstanceWhenOrganisationEndpointReturnsNoInstances()
        Dim Provider As New TestableScopevisioProvider()
        Provider.CurrentOrganisationOverride = New Organisation() With {
            .Id = 11, .Name = "Default organisation", .TeamworkTenantId = "default-tenant"
        }

        Dim Instances As IReadOnlyList(Of DmsInstanceInfo) = Provider.ListAvailableDmsInstances()

        ClassicAssert.AreEqual(1, Instances.Count)
        ClassicAssert.AreEqual("default-tenant", Instances(0).ID)
        ClassicAssert.AreEqual("Default organisation", Instances(0).DisplayName)
        ClassicAssert.IsTrue(Instances(0).IsSelected)

        Provider.SelectDmsInstance(Instances(0).ID)
        ClassicAssert.IsNull(Provider.AppliedOrganisationID)
    End Sub

    <Test>
    Public Sub AddsCurrentInstanceWhenOrganisationEndpointOmitsItsTenant()
        Dim Provider As New TestableScopevisioProvider(
            New Organisation() With {.Id = 11, .Name = "Default organisation"}
        )
        Provider.CurrentOrganisationOverride = New Organisation() With {
            .Id = 11, .Name = "Default organisation", .TeamworkTenantId = "default-tenant"
        }

        Dim Instances As IReadOnlyList(Of DmsInstanceInfo) = Provider.ListAvailableDmsInstances()

        ClassicAssert.AreEqual(1, Instances.Count)
        ClassicAssert.AreEqual("default-tenant", Instances(0).ID)
        ClassicAssert.IsTrue(Instances(0).IsSelected)
    End Sub

    <Test>
    Public Sub MarksCurrentTenantSelectedWhenOrganisationIdsDiffer()
        Dim Provider As New TestableScopevisioProvider(
            New Organisation() With {.Id = 12, .Name = "Another organisation", .TeamworkTenantId = "default-tenant"}
        )
        Provider.CurrentOrganisationOverride = New Organisation() With {
            .Id = 11, .Name = "Default organisation", .TeamworkTenantId = "default-tenant"
        }

        Dim Instances As IReadOnlyList(Of DmsInstanceInfo) = Provider.ListAvailableDmsInstances()

        ClassicAssert.AreEqual(1, Instances.Count)
        ClassicAssert.IsTrue(Instances(0).IsSelected)
    End Sub

    <Test>
    Public Sub ResolvesMissingTenantIdsWithoutReauthorizingCurrentOrganisation()
        Dim Organisations As IList(Of Organisation) = New List(Of Organisation) From {
            New Organisation() With {.Id = 11, .Name = "Default organisation"},
            New Organisation() With {.Id = 21, .Name = "Another organisation"},
            New Organisation() With {.Id = 31, .Name = "Organisation without Teamwork"},
            New Organisation() With {.Id = 41, .Name = "Already resolved", .TeamworkTenantId = "tenant-c"}
        }
        Dim CurrentOrganisation As New Organisation() With {.Id = 11, .TeamworkTenantId = "tenant-a"}
        Dim RequestedIDs As New List(Of Long)

        Dim Result As IList(Of Organisation) = ScopevisioTeamworkDmsProvider.PopulateTeamworkTenantIds(
            Organisations,
            CurrentOrganisation,
            Function(OrganisationID As Long)
                RequestedIDs.Add(OrganisationID)
                If OrganisationID = 21 Then Return "tenant-b"
                Return Nothing
            End Function)

        ClassicAssert.AreSame(Organisations, Result)
        ClassicAssert.AreEqual("tenant-a", Result(0).TeamworkTenantId)
        ClassicAssert.AreEqual("tenant-b", Result(1).TeamworkTenantId)
        ClassicAssert.IsNull(Result(2).TeamworkTenantId)
        ClassicAssert.AreEqual("tenant-c", Result(3).TeamworkTenantId)
        CollectionAssert.AreEquivalent(New Long() {21, 31}, RequestedIDs)
    End Sub

    <Test>
    Public Sub SelectsOrganisationBackingRequestedTeamworkInstance()
        Dim Provider As New TestableScopevisioProvider(
            New Organisation() With {.Id = 11, .Name = "Accounting A", .TeamworkTenantId = "tenant-a", .TeamworkTenantName = "Documents A"},
            New Organisation() With {.Id = 21, .Name = "Accounting B", .TeamworkTenantId = "tenant-b", .TeamworkTenantName = "Documents B"}
        )
        Provider.CurrentOrganisationID = 11

        Provider.SelectDmsInstance("tenant-b")

        ClassicAssert.AreEqual(21, Provider.AppliedOrganisationID)
    End Sub

    <Test>
    Public Sub KeepsCurrentInstanceWithoutApplyingOrganisationAgain()
        Dim Provider As New TestableScopevisioProvider(
            New Organisation() With {.Id = 11, .Name = "Accounting A", .TeamworkTenantId = "tenant-a", .TeamworkTenantName = "Documents A"}
        )
        Provider.CurrentOrganisationID = 11

        Provider.SelectDmsInstance("tenant-a")

        ClassicAssert.IsNull(Provider.AppliedOrganisationID)
    End Sub

    <Test>
    Public Sub RejectsUnknownInstance()
        Dim Provider As New TestableScopevisioProvider(
            New Organisation() With {.Id = 11, .Name = "Accounting A", .TeamworkTenantId = "tenant-a", .TeamworkTenantName = "Documents A"}
        )

        ClassicAssert.Throws(Of ArgumentOutOfRangeException)(Sub() Provider.SelectDmsInstance("tenant-missing"))
    End Sub

    <Test>
    Public Sub GenericAuthorizationPreservesSpecializedScopevisioCredentials()
        Dim Provider As New TestableScopevisioProvider()
        Dim Credentials As New ScopevisioLoginCredentials() With {
            .Username = "user@example.test",
            .Password = "secret",
            .ClientNumber = "1234567",
            .OrganisationName = "Selected organisation",
            .IgnoreSslErrors = True
        }

        DirectCast(Provider, BaseDmsProvider).Authorize(Credentials)

        ClassicAssert.AreSame(Credentials, Provider.AuthorizedCredentials)
        ClassicAssert.AreEqual("Selected organisation", Provider.AuthorizedCredentials.OrganisationName)
        ClassicAssert.IsTrue(Provider.AuthorizedIgnoreSslErrors)
    End Sub

    Private NotInheritable Class TestableScopevisioProvider
        Inherits ScopevisioTeamworkDmsProvider

        Private ReadOnly _Organisations As IList(Of Organisation)

        Public Sub New(ParamArray organisations As Organisation())
            Me._Organisations = organisations.ToList()
        End Sub

        Public Property CurrentOrganisationID As Long?
        Public Property CurrentOrganisationOverride As Organisation
        Public Property AppliedOrganisationID As Long?
        Public Property AuthorizedCredentials As ScopevisioLoginCredentials
        Public Property AuthorizedIgnoreSslErrors As Boolean

        Protected Overrides Function LoadAvailableOrganisations() As IList(Of Organisation)
            Return Me._Organisations
        End Function

        Protected Overrides Function GetCurrentOrganisation() As Organisation
            If Me.CurrentOrganisationOverride IsNot Nothing Then Return Me.CurrentOrganisationOverride
            If Me.CurrentOrganisationID.HasValue = False Then Return Nothing
            Return Me._Organisations.FirstOrDefault(Function(Item) Item.Id = Me.CurrentOrganisationID.Value)
        End Function

        Protected Overrides Sub ApplyOrganisation(organisation As Organisation)
            Me.AppliedOrganisationID = organisation.Id
            Me.CurrentOrganisationID = organisation.Id
        End Sub

        Protected Overrides Sub AuthorizeCore(loginCredentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean)
            Me.AuthorizedCredentials = loginCredentials
            Me.AuthorizedIgnoreSslErrors = ignoreSslErrors
        End Sub

    End Class

End Class
