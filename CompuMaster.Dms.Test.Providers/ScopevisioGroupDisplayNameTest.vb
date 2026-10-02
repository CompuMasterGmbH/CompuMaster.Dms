Option Explicit On
Option Strict On

Imports System.Globalization
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
Public Class ScopevisioGroupDisplayNameTest

    Private Const PublicGroupId As String = "ALL_USERS_11111111-2222-4333-8444-555555555555"

    <TestCase("de-DE", "Alle Benutzer")>
    <TestCase("en-US", "All users")>
    Public Sub UnnamedPublicGroupUsesFriendlyName(cultureName As String, expectedName As String)
        Dim originalCulture As CultureInfo = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Dim provider As New InspectingScopevisioProvider
            ClassicAssert.AreEqual(expectedName, provider.ResolveGroupName(PublicGroupId, Nothing))
        Finally
            CultureInfo.CurrentUICulture = originalCulture
        End Try
    End Sub

    <Test>
    Public Sub ExistingGroupNameTakesPriority()
        Dim provider As New InspectingScopevisioProvider
        ClassicAssert.AreEqual("Team", provider.ResolveGroupName(PublicGroupId, "Team"))
    End Sub

    <TestCase("ALL_USERS_invalid")>
    <TestCase("TEAM_11111111-2222-4333-8444-555555555555")>
    <TestCase(Nothing)>
    Public Sub OtherUnnamedGroupsKeepExistingFallback(groupId As String)
        Dim provider As New InspectingScopevisioProvider
        ClassicAssert.IsNull(provider.ResolveGroupName(groupId, Nothing))
    End Sub

    Private NotInheritable Class InspectingScopevisioProvider
        Inherits ScopevisioTeamworkDmsProvider

        Public Function ResolveGroupName(groupId As String, groupName As String) As String
            Return Me.NormalizeGroupDisplayName(groupId, groupName)
        End Function
    End Class

End Class
