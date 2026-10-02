Option Explicit On
Option Strict On

Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Globalization
Imports System.Text.Json
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports CompuMaster.Scopevisio.OpenApi
Imports CompuMaster.Scopevisio.OpenApi.Api
Imports CompuMaster.Scopevisio.OpenApi.Client
Imports CompuMaster.Scopevisio.OpenApi.Model
Imports RestSharp

Namespace Providers

    ''' <summary>
    ''' Scopevision TeamWork REST API
    ''' </summary>
    ''' <inheritdoc path="https://www.openscope.de/api.html" />
    ''' <inheritdoc path="https://appload.scopevisio.com/static/browser/index.html#!/documentation"/>
    ''' <inheritdoc path="https://public.centerdevice.de/02bf3cfd-06c6-4d43-9cd4-3c18aab0020a"/>
    Public Class ScopevisioTeamworkDmsProvider
        Inherits CenterDeviceDmsProviderBase
        Implements IDmsInstanceProvider

        Private _OpenScopeClient As CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient

        Private Shared ReadOnly NameLookupClient As New HttpClient()
        Private Shared ReadOnly NameLookupClientIgnoringSslErrors As New HttpClient(New HttpClientHandler With {
            .ServerCertificateCustomValidationCallback = Function(sender, certificate, chain, sslPolicyErrors) True
        })
        Private _ignoreSslErrors As Boolean

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.Scopevisio
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "Scopevisio Teamwork"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return "https://appload.scopevisio.com/rest/teamworkbridge/"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlNotCustomizable
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType
            Get
                Return UserCustomerReferenceType.CustomerReferenceRequired
            End Get
        End Property

        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Return Me.WebApiDefaultUrl
        End Function

        Public Overloads Sub Authorize(loginCredentials As ScopevisioLoginCredentials)
            Me.Authorize(loginCredentials, False)
        End Sub
        Public Overloads Sub Authorize(loginCredentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean)
            Me.AuthorizeCore(loginCredentials, ignoreSslErrors)
        End Sub

        ''' <summary>
        ''' Authorizes the provider with Scopevisio credentials.
        ''' </summary>
        ''' <param name="loginCredentials">The Scopevisio login credentials.</param>
        ''' <param name="ignoreSslErrors">A value indicating whether TLS certificate validation errors are ignored.</param>
        Protected Overridable Sub AuthorizeCore(loginCredentials As ScopevisioLoginCredentials, ignoreSslErrors As Boolean)
            Dim IsTokenRequest As Boolean = False
            Try
                Dim OpenScopeConfig As New Global.CompuMaster.Scopevisio.OpenApi.Client.Configuration With {
                    .Username = loginCredentials.Username,
                    .Password = loginCredentials.Password,
                    .ClientNumber = loginCredentials.ClientNumber,
                    .OrganisationName = loginCredentials.OrganisationName
                }
                If ignoreSslErrors Then
                    Dim Handler As New System.Net.Http.HttpClientHandler() With {
                        .ServerCertificateCustomValidationCallback = Function(sender, certificate, chain, sslPolicyErrors) True
                        }
                    OpenScopeConfig.HttpClient = New System.Net.Http.HttpClient(Handler)
                End If
                Dim OpenScopeClient As New CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient(OpenScopeConfig)
                IsTokenRequest = True
                OpenScopeClient.AuthorizeWithUserCredentials()
                IsTokenRequest = False
                Me._OpenScopeClient = OpenScopeClient
                Me.IOClient = If(OpenScopeClient.Token Is Nothing OrElse String.IsNullOrWhiteSpace(OpenScopeClient.Token.TeamworkTenantId),
                                 Nothing,
                                 New CompuMaster.Scopevisio.Teamwork.TeamworkIOClient(OpenScopeClient))
                Me._ignoreSslErrors = ignoreSslErrors
            Catch ex As CompuMaster.Scopevisio.OpenApi.Client.ApiException
                Throw CreateAuthorizationException(ex.ErrorCode, ex.ErrorContent, ex, IsTokenRequest)
            End Try
        End Sub

        ''' <inheritdoc/>
        ''' <remarks>The bundled SDK does not map the API's hyphenated name fields, so this provider reads them when the SDK supplies no name.</remarks>
        Protected Overrides Function LookupUserDisplayName(userId As String) As String
            Dim sdkName As String = MyBase.LookupUserDisplayName(userId)
            If Not String.IsNullOrWhiteSpace(sdkName) Then Return sdkName

            Dim openScopeClient = CType(Me.IOClient, CompuMaster.Scopevisio.Teamwork.TeamworkIOClient).TeamworkRestClient.OpenscopeClient
            If openScopeClient.Token Is Nothing OrElse String.IsNullOrWhiteSpace(openScopeClient.Token.AccessToken) Then Return String.Empty

            Try
                Dim client As HttpClient = If(Me._ignoreSslErrors, NameLookupClientIgnoringSslErrors, NameLookupClient)
                Dim address As New Uri(New Uri(Me.WebApiDefaultUrl), "user/" & Uri.EscapeDataString(userId))
                Using request As New HttpRequestMessage(HttpMethod.Get, address)
                    request.Headers.Authorization = New AuthenticationHeaderValue("Bearer", openScopeClient.Token.AccessToken)
                    Using response = client.SendAsync(request).GetAwaiter().GetResult()
                        If Not response.IsSuccessStatusCode Then Return String.Empty
                        Return ParseUserDisplayName(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())
                    End Using
                End Using
            Catch ex As HttpRequestException
                Return String.Empty
            Catch ex As OperationCanceledException
                Return String.Empty
            Catch ex As JsonException
                Return String.Empty
            End Try
        End Function

        ''' <inheritdoc/>
        ''' <remarks>The Scopevisio public group has no name in the Teamwork API response.</remarks>
        Protected Overrides Function NormalizeGroupDisplayName(groupId As String, groupName As String) As String
            If Not String.IsNullOrWhiteSpace(groupName) Then Return groupName

            Const prefix As String = "ALL_USERS_"
            Dim groupGuid As Guid
            If groupId IsNot Nothing AndAlso groupId.StartsWith(prefix, StringComparison.Ordinal) AndAlso
                Guid.TryParseExact(groupId.Substring(prefix.Length), "D", groupGuid) Then
                Return If(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName = "de", "Alle Benutzer", "All users")
            End If

            Return MyBase.NormalizeGroupDisplayName(groupId, groupName)
        End Function

        Friend Shared Function ParseUserDisplayName(json As String) As String
            If String.IsNullOrWhiteSpace(json) Then Return String.Empty
            Using document = JsonDocument.Parse(json)
                If document.RootElement.ValueKind <> JsonValueKind.Object Then Return String.Empty
                Dim firstName As String = ReadUserNameField(document.RootElement, "first-name")
                Dim lastName As String = ReadUserNameField(document.RootElement, "last-name")
                Return (firstName & " " & lastName).Trim()
            End Using
        End Function

        Private Shared Function ReadUserNameField(user As JsonElement, fieldName As String) As String
            Dim field As JsonElement
            If Not user.TryGetProperty(fieldName, field) OrElse field.ValueKind <> JsonValueKind.String Then Return String.Empty
            Return If(field.GetString(), String.Empty).Trim()
        End Function

        Friend Shared Function CreateAuthorizationException(errorCode As Integer, errorContent As Object, originalException As Exception, isTokenRequest As Boolean) As Exception
            If isTokenRequest AndAlso errorCode = 401 Then
                Return New Data.DmsUserAuthenticationException("Scopevisio user authentication failed.", originalException)
            ElseIf errorCode = 403 AndAlso errorContent IsNot Nothing AndAlso errorContent.GetType Is GetType(String) AndAlso CType(errorContent, String).ToLowerInvariant.Contains("""message"":""no organisation found.""") Then
                Return New Data.DmsSystemErrorException("No organisation found, usually Scopevisio user authorizations are required: Rechteprofil Kontakte – alle Rechte oder CRM – alle Rechte", originalException)
            ElseIf errorCode = 403 AndAlso errorContent IsNot Nothing AndAlso errorContent.GetType Is GetType(String) AndAlso CType(errorContent, String).ToLowerInvariant.Contains("""message"":""customer is deleted""") Then
                Return New Data.DmsSystemErrorException("DMS-Instanz des Kunden wurde gelöscht", originalException)
            Else
                Return New Data.DmsSystemErrorException(originalException.Message, originalException)
            End If
        End Function

        Public ReadOnly Property ApplicationContext As CompuMaster.Scopevisio.OpenApi.Model.AccountInfo
            Get
                Return CType(Me.IOClient, CompuMaster.Scopevisio.Teamwork.TeamworkIOClient).TeamworkRestClient.ApplicationContext
            End Get
        End Property

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Return New ScopevisioLoginCredentials
        End Function

        Public Overrides Sub Authorize(dmsProfile As Data.IDmsLoginProfile)
            Dim Credentials As ScopevisioLoginCredentials = TryCast(dmsProfile, ScopevisioLoginCredentials)
            If Credentials Is Nothing Then
                Credentials = CType(Me.CreateNewCredentialsInstance(), ScopevisioLoginCredentials)
                Credentials.Username = dmsProfile.UserName
                Credentials.ClientNumber = dmsProfile.CustomerInstance
                Credentials.Password = dmsProfile.Password
            End If
            Me.Authorize(Credentials, dmsProfile.IgnoreSslErrors)
        End Sub

        ''' <inheritdoc/>
        Public ReadOnly Property CurrentDmsInstance As DmsInstanceInfo Implements IDmsInstanceProvider.CurrentDmsInstance
            Get
                Dim CurrentOrganisation As Organisation = Me.GetCurrentOrganisation()
                If CurrentOrganisation Is Nothing OrElse String.IsNullOrWhiteSpace(CurrentOrganisation.TeamworkTenantId) Then Return Nothing
                Return Me.CreateDmsInstanceInfo(CurrentOrganisation, True)
            End Get
        End Property

        ''' <inheritdoc/>
        Public Function ListAvailableDmsInstances() As IReadOnlyList(Of DmsInstanceInfo) Implements IDmsInstanceProvider.ListAvailableDmsInstances
            Dim Result As New List(Of DmsInstanceInfo)
            Dim InstanceIndexes As New Dictionary(Of String, Integer)(StringComparer.Ordinal)
            Dim CurrentOrganisation As Organisation = Me.GetCurrentOrganisation()
            Dim CurrentTenantID As String = If(CurrentOrganisation Is Nothing, Nothing, CurrentOrganisation.TeamworkTenantId)

            For Each AvailableOrganisation As Organisation In Me.LoadAvailableOrganisations()
                If AvailableOrganisation Is Nothing OrElse String.IsNullOrWhiteSpace(AvailableOrganisation.TeamworkTenantId) Then Continue For

                Dim IsSelected As Boolean = String.Equals(AvailableOrganisation.TeamworkTenantId, CurrentTenantID, StringComparison.Ordinal)
                Dim ExistingIndex As Integer
                If InstanceIndexes.TryGetValue(AvailableOrganisation.TeamworkTenantId, ExistingIndex) Then
                    If IsSelected AndAlso Result(ExistingIndex).IsSelected = False Then
                        Result(ExistingIndex) = Me.CreateDmsInstanceInfo(AvailableOrganisation, True)
                    End If
                Else
                    InstanceIndexes.Add(AvailableOrganisation.TeamworkTenantId, Result.Count)
                    Result.Add(Me.CreateDmsInstanceInfo(AvailableOrganisation, IsSelected))
                End If
            Next

            If CurrentOrganisation IsNot Nothing AndAlso
               Not String.IsNullOrWhiteSpace(CurrentOrganisation.TeamworkTenantId) AndAlso
               Not InstanceIndexes.ContainsKey(CurrentOrganisation.TeamworkTenantId) Then
                Result.Add(Me.CreateDmsInstanceInfo(CurrentOrganisation, True))
            End If

            Return Result.AsReadOnly()
        End Function

        ''' <inheritdoc/>
        Public Sub SelectDmsInstance(instanceID As String) Implements IDmsInstanceProvider.SelectDmsInstance
            If String.IsNullOrWhiteSpace(instanceID) Then Throw New ArgumentException("A DMS instance ID is required.", NameOf(instanceID))

            Dim CurrentOrganisation As Organisation = Me.GetCurrentOrganisation()
            If CurrentOrganisation IsNot Nothing AndAlso
               String.Equals(CurrentOrganisation.TeamworkTenantId, instanceID, StringComparison.Ordinal) Then Return

            Dim SelectedOrganisation As Organisation = Nothing
            For Each AvailableOrganisation As Organisation In Me.LoadAvailableOrganisations()
                If AvailableOrganisation IsNot Nothing AndAlso String.Equals(AvailableOrganisation.TeamworkTenantId, instanceID, StringComparison.Ordinal) Then
                    If CurrentOrganisation IsNot Nothing AndAlso AvailableOrganisation.Id = CurrentOrganisation.Id Then Return
                    If SelectedOrganisation Is Nothing Then SelectedOrganisation = AvailableOrganisation
                End If
            Next

            If SelectedOrganisation Is Nothing Then
                Throw New ArgumentOutOfRangeException(NameOf(instanceID), instanceID, "The DMS instance is not available to the authorized user.")
            End If

            Me.ApplyOrganisation(SelectedOrganisation)
        End Sub

        ''' <summary>
        ''' Loads the Scopevisio organisations available to the authorized user.
        ''' </summary>
        ''' <returns>The available Scopevisio organisations.</returns>
        Protected Overridable Function LoadAvailableOrganisations() As IList(Of Organisation)
            Dim OrganisationResult As Records(Of Organisation) = Me.GetOpenScopeClient().AdditionalApi.OrganisationJsonWithHttpInfo().Data
            If OrganisationResult Is Nothing OrElse OrganisationResult.Items Is Nothing Then Return New List(Of Organisation)
            ' The organisations endpoint can omit Teamwork tenant IDs; an organisation-specific token supplies them.
            Return PopulateTeamworkTenantIds(OrganisationResult.Items, Me.GetCurrentOrganisation(), AddressOf Me.RequestTeamworkTenantIdForOrganisation)
        End Function

        Friend Shared Function PopulateTeamworkTenantIds(organisations As IList(Of Organisation), currentOrganisation As Organisation, requestTenantId As Func(Of Long, String)) As IList(Of Organisation)
            For Each Organisation As Organisation In organisations
                If Organisation Is Nothing OrElse Not String.IsNullOrWhiteSpace(Organisation.TeamworkTenantId) Then Continue For

                If currentOrganisation IsNot Nothing AndAlso Organisation.Id = currentOrganisation.Id Then
                    Organisation.TeamworkTenantId = currentOrganisation.TeamworkTenantId
                Else
                    Organisation.TeamworkTenantId = requestTenantId(Organisation.Id)
                End If
            Next
            Return organisations
        End Function

        Private Function RequestTeamworkTenantIdForOrganisation(organisationID As Long) As String
            Dim OpenScopeClient As OpenScopeApiClient = Me.GetOpenScopeClient()
            Dim OpenScopeConfig As Configuration = OpenScopeClient.Config
            Dim OrganisationToken As TokenResponse = OpenScopeClient.AuthorizationApi.TokenWithHttpInfo(
                grantType:="password",
                customer:=OpenScopeConfig.ClientNumber,
                username:=OpenScopeConfig.Username,
                organisationId:=organisationID,
                password:=OpenScopeConfig.Password).Data
            Return If(OrganisationToken Is Nothing, Nothing, OrganisationToken.TeamworkTenantId)
        End Function

        ''' <summary>
        ''' Gets the currently selected Scopevisio organisation.
        ''' </summary>
        ''' <returns>The current organisation, or <see langword="Nothing"/> when no organisation is selected.</returns>
        Protected Overridable Function GetCurrentOrganisation() As Organisation
            Dim OpenScopeClient As CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient = Me._OpenScopeClient
            If OpenScopeClient Is Nothing Then Return Nothing
            If OpenScopeClient.Token Is Nothing OrElse String.IsNullOrWhiteSpace(OpenScopeClient.Token.TeamworkTenantId) Then Return Nothing
            Return New Organisation With {
                .Id = OpenScopeClient.Token.OrganisationId,
                .Name = OpenScopeClient.Token.OrganisationName,
                .TeamworkTenantId = OpenScopeClient.Token.TeamworkTenantId
            }
        End Function

        ''' <summary>
        ''' Changes the current Scopevisio organisation and refreshes the Teamwork client context.
        ''' </summary>
        ''' <param name="organisation">The Scopevisio organisation to select.</param>
        Protected Overridable Sub ApplyOrganisation(organisation As Organisation)
            Dim OpenScopeClient As CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient = Me.GetOpenScopeClient()
            OpenScopeClient.AuthorizationApi.ChangeOrganisationById(organisation.Id)
            OpenScopeClient.Token.OrganisationId = organisation.Id
            OpenScopeClient.Token.OrganisationName = organisation.Name
            OpenScopeClient.Token.TeamworkTenantId = organisation.TeamworkTenantId
            Me.IOClient = Nothing
            Me._AllUploadLinks = Nothing
            Me.IOClient = New CompuMaster.Scopevisio.Teamwork.TeamworkIOClient(OpenScopeClient)
        End Sub

        Private Function GetOpenScopeClient() As CompuMaster.Scopevisio.OpenApi.OpenScopeApiClient
            If Me._OpenScopeClient Is Nothing Then Throw New InvalidOperationException("The DMS provider must be authorized before accessing DMS instances.")
            Return Me._OpenScopeClient
        End Function

        Private Function CreateDmsInstanceInfo(organisation As Organisation, isSelected As Boolean) As DmsInstanceInfo
            Dim DisplayName As String = If(String.IsNullOrWhiteSpace(organisation.TeamworkTenantName), organisation.Name, organisation.TeamworkTenantName)
            If String.IsNullOrWhiteSpace(DisplayName) Then DisplayName = organisation.TeamworkTenantId
            Return New DmsInstanceInfo(organisation.TeamworkTenantId, DisplayName, isSelected)
        End Function

    End Class

End Namespace
