Option Explicit On
Option Strict On

Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text.Json
Imports CenterDevice.Rest.Clients.User
Imports CenterDevice.Rest.Exceptions
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture, Category("RemoteDms"), Category("ScopevisioTeamwork")>
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

        If user Is Nothing Then
            TestContext.Progress.WriteLine(label & ": user=" & userResult & ", group=" & groupResult)
            Return
        End If

        Dim sdkName As String = If(user.GetFullName(), String.Empty).Trim()
        Dim displayName As String = principal.DisplayName
        Dim expected As String = If(String.IsNullOrWhiteSpace(sdkName), principal.ID, sdkName)
        ClassicAssert.AreEqual(expected, displayName, label & " does not follow the SDK user-name result.")
        TestContext.Progress.WriteLine(label & ": user=" & userResult & ", group=" & groupResult &
            ", status=" & If(user.Status, "absent") & ", technical=" & If(user.TechnicalUser.HasValue, user.TechnicalUser.Value.ToString(), "absent") &
            ", role=" & If(user.Role, "absent") & ", guest=" & user.IsGuest().ToString() &
            ", firstName=" & (Not String.IsNullOrWhiteSpace(user.FirstName)).ToString() & ", lastName=" & (Not String.IsNullOrWhiteSpace(user.LastName)).ToString() &
            ", email=" & (Not String.IsNullOrWhiteSpace(user.Email)).ToString() & ", sdkName=" & (Not String.IsNullOrWhiteSpace(sdkName)).ToString() &
            ", idFallback=" & String.Equals(displayName, principal.ID, StringComparison.Ordinal) & ", " & provider.ReadRawUserNameFields(principal.ID))
    End Sub

    Private NotInheritable Class InspectingScopevisioProvider
        Inherits ScopevisioTeamworkDmsProvider

        Public Function ReadUser(id As String) As BaseUserData
            Return Me.IOClient.ApiClient.User.GetUserData(Me.IOClient.CurrentAuthenticationContextUserID, id)
        End Function

        Public Sub ReadGroup(id As String)
            Me.IOClient.ApiClient.Group.GetGroup(Me.IOClient.CurrentAuthenticationContextUserID, id)
        End Sub

        Public Function ReadRawUserNameFields(id As String) As String
            Dim openScopeClient = CType(Me.IOClient, CompuMaster.Scopevisio.Teamwork.TeamworkIOClient).TeamworkRestClient.OpenscopeClient
            Dim address As New Uri(New Uri(Me.WebApiDefaultUrl), "user/" & Uri.EscapeDataString(id))
            Using client As New HttpClient
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("Bearer", openScopeClient.Token.AccessToken)
                Using response = client.GetAsync(address).GetAwaiter().GetResult()
                    If Not response.IsSuccessStatusCode Then Return "rawStatus=" & CInt(response.StatusCode).ToString()
                    Dim json As String = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    Using document = JsonDocument.Parse(json)
                        Dim fields As New List(Of String)
                        CollectNameFields(document.RootElement, fields)
                        Return "rawStatus=" & CInt(response.StatusCode).ToString() & ", rawNameFields=" & String.Join(",", fields)
                    End Using
                End Using
            End Using
        End Function

        Private Shared Sub CollectNameFields(value As JsonElement, fields As List(Of String))
            If value.ValueKind = JsonValueKind.Object Then
                For Each propertyItem As JsonProperty In value.EnumerateObject()
                    If propertyItem.Name.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0 Then
                        Dim state As String = If(propertyItem.Value.ValueKind = JsonValueKind.String,
                            If(String.IsNullOrWhiteSpace(propertyItem.Value.GetString()), "empty", "value"), propertyItem.Value.ValueKind.ToString())
                        fields.Add(propertyItem.Name & "=" & state)
                    End If
                    CollectNameFields(propertyItem.Value, fields)
                Next
            ElseIf value.ValueKind = JsonValueKind.Array Then
                For Each child As JsonElement In value.EnumerateArray()
                    CollectNameFields(child, fields)
                Next
            End If
        End Sub
    End Class

End Class
