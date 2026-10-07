Option Explicit On
Option Strict On

Imports CenterDevice.Rest.Clients.User
Imports CenterDevice.Rest.Exceptions
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture, Category("RemoteDms"), Category("TestLevel2"), Category("ScopevisioTeamwork")>
Public Class ScopevisioSharingPrincipalTest

    <Test>
    Public Sub AngeboteSharingPrincipalCanBeClassifiedWithoutExposingIdentity()
        Dim settings As New ScopevisioTeamworkSettings
        Dim credentials As New ScopevisioLoginCredentials With {
            .Username = settings.InputLine("username"),
            .ClientNumber = settings.InputLine("customer no."),
            .Password = settings.InputLine("password")
        }
        Dim provider As New InspectingScopevisioProvider
        provider.Authorize(credentials)

        Dim collection = provider.ListAllCollectionItems("/").SingleOrDefault(Function(item) item.Name = "Angebote")
        ClassicAssert.IsNotNull(collection, "The diagnostic collection is not visible to the test account.")
        ClassicAssert.IsNotNull(collection.ExtendedInfosUserSharings)
        ClassicAssert.IsNotEmpty(collection.ExtendedInfosUserSharings, "The diagnostic collection has no visible user sharings.")

        DescribePrincipal(provider, "owner", collection.ExtendedInfosOwner)
        For index As Integer = 0 To collection.ExtendedInfosUserSharings.Count - 1
            DescribePrincipal(provider, "sharing " & index.ToString(), collection.ExtendedInfosUserSharings(index).User)
        Next
    End Sub

    Private Shared Sub DescribePrincipal(provider As InspectingScopevisioProvider, label As String, principal As DmsUser)
        Dim user As BaseUserData = Nothing
        Dim userResult As String
        Try
            user = provider.ReadUser(principal.ID)
            userResult = "found"
        Catch ex As NotFoundException
            userResult = "not-found"
        Catch ex As ForbiddenException
            userResult = "forbidden"
        End Try

        Dim groupResult As String
        Try
            provider.ReadGroup(principal.ID)
            groupResult = "found"
        Catch ex As NotFoundException
            groupResult = "not-found"
        Catch ex As ForbiddenException
            groupResult = "forbidden"
        End Try

        ClassicAssert.AreEqual("found", userResult, label & " is not accessible as a user.")
        ClassicAssert.AreEqual("not-found", groupResult, label & " unexpectedly resolves as a group.")

        Dim sdkName As String = If(user.GetFullName(), String.Empty).Trim()
        Dim displayName As String = principal.DisplayName
        ClassicAssert.IsFalse(String.IsNullOrWhiteSpace(displayName), label & " has no display name.")
        ClassicAssert.AreNotEqual(principal.ID, displayName, label & " still displays the user ID despite the API name fields.")
        If Not String.IsNullOrWhiteSpace(sdkName) Then
            ClassicAssert.AreEqual(sdkName, displayName, label & " does not follow the SDK user-name result.")
        End If
        TestContext.Progress.WriteLine(label & ": user=" & userResult & ", group=" & groupResult &
            ", status=" & If(user.Status, "absent") & ", technical=" & If(user.TechnicalUser.HasValue, user.TechnicalUser.Value.ToString(), "absent") &
            ", role=" & If(user.Role, "absent") & ", guest=" & user.IsGuest().ToString() &
            ", firstName=" & (Not String.IsNullOrWhiteSpace(user.FirstName)).ToString() & ", lastName=" & (Not String.IsNullOrWhiteSpace(user.LastName)).ToString() &
            ", email=" & (Not String.IsNullOrWhiteSpace(user.Email)).ToString() & ", sdkName=" & (Not String.IsNullOrWhiteSpace(sdkName)).ToString() &
            ", idFallback=" & String.Equals(displayName, principal.ID, StringComparison.Ordinal))
    End Sub

    Private NotInheritable Class InspectingScopevisioProvider
        Inherits ScopevisioTeamworkDmsProvider

        Public Function ReadUser(id As String) As BaseUserData
            Return Me.IOClient.ApiClient.User.GetUserData(Me.IOClient.CurrentAuthenticationContextUserID, id)
        End Function

        Public Sub ReadGroup(id As String)
            Me.IOClient.ApiClient.Group.GetGroup(Me.IOClient.CurrentAuthenticationContextUserID, id)
        End Sub

    End Class

End Class
