Option Explicit On
Option Strict On

Imports System.ComponentModel
Imports CompuMaster.Dms.Providers

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types

    ''' <summary>Represents a user identified by a DMS provider.</summary>
    Public Structure DmsUser

        ''' <summary>Gets or sets the provider-specific user identifier used for DMS operations.</summary>
        Public Property ID As String

        Private _DisplayName As String

        ''' <summary>Gets or sets the legacy display name.</summary>
        ''' <remarks>Use <see cref="ID"/>, <see cref="LoginName"/>, or <see cref="DisplayName"/> instead.</remarks>
        <Obsolete("Check ID, LoginName or DisplayName", True), EditorBrowsable(EditorBrowsableState.Never)>
        Public Property Name As String
            Get
                Return ResolveDisplayName()
            End Get
            Set(value As String)
                Me.DisplayName = value
            End Set
        End Property

        ''' <summary>Gets or sets the provider used to resolve user details.</summary>
        Public Property Provider As BaseDmsProvider

        ''' <summary>Gets or sets the display-name lookup delegate.</summary>
        Public Property GetDisplayName As GetDisplayNameFromId

        ''' <summary>Gets or sets the legacy display-name lookup delegate.</summary>
        ''' <remarks>Use <see cref="GetDisplayName"/> instead.</remarks>
        <Obsolete("Use GetDisplayName instead.", True), EditorBrowsable(EditorBrowsableState.Never)>
        Public Property GetName As GetDisplayNameFromId
            Get
                Return Me.GetDisplayName
            End Get
            Set(value As GetDisplayNameFromId)
                Me.GetDisplayName = value
            End Set
        End Property

        ''' <summary>Represents a provider-specific display-name lookup.</summary>
        ''' <param name="provider">The provider that owns the user.</param>
        ''' <param name="id">The provider-specific user identifier.</param>
        ''' <returns>The user's display name, or <see langword="Nothing"/> when unavailable.</returns>
        Public Delegate Function GetDisplayNameFromId(provider As BaseDmsProvider, id As String) As String

        Private _LoginName As String

        ''' <summary>Gets or sets the login name when the provider supplies one.</summary>
        ''' <value>The login name, or an empty value when unavailable. It does not fall back to <see cref="ID"/>.</value>
        Public Property LoginName As String
            Get
                If _LoginName Is Nothing AndAlso Not String.IsNullOrWhiteSpace(Me.ID) AndAlso Me.GetLoginName IsNot Nothing AndAlso Me.Provider IsNot Nothing Then
                    _LoginName = If(Me.GetLoginName(Me.Provider, Me.ID), String.Empty).Trim()
                End If
                Return _LoginName
            End Get
            Set(value As String)
                If value Is Nothing Then
                    _LoginName = Nothing
                Else
                    _LoginName = value.Trim()
                End If
            End Set
        End Property

        ''' <summary>Gets or sets the login-name lookup delegate.</summary>
        Public Property GetLoginName As GetLoginNameFromId

        ''' <summary>Represents a provider-specific login-name lookup.</summary>
        ''' <param name="provider">The provider that owns the user.</param>
        ''' <param name="id">The provider-specific user identifier.</param>
        ''' <returns>The user's login name, or <see langword="Nothing"/> when unavailable.</returns>
        Public Delegate Function GetLoginNameFromId(provider As BaseDmsProvider, id As String) As String

        ''' <summary>Gets or sets the user's display name.</summary>
        ''' <remarks>Falls back to <see cref="ID"/> when no display name is available. The ID remains the identifier used for DMS operations.</remarks>
        Public Property DisplayName As String
            Get
                Dim resolvedName As String = ResolveDisplayName()
                If Not String.IsNullOrWhiteSpace(resolvedName) Then Return resolvedName.Trim()
                If Not String.IsNullOrWhiteSpace(Me.ID) Then Return Me.ID
                Return Nothing
            End Get
            Set(value As String)
                If value Is Nothing Then
                    _DisplayName = Nothing
                Else
                    _DisplayName = value.Trim()
                End If
            End Set
        End Property

        Private Function ResolveDisplayName() As String
            If _DisplayName Is Nothing AndAlso Not String.IsNullOrWhiteSpace(Me.ID) AndAlso Me.GetDisplayName IsNot Nothing AndAlso Me.Provider IsNot Nothing Then
                _DisplayName = If(Me.GetDisplayName(Me.Provider, Me.ID), String.Empty).Trim()
            End If
            Return _DisplayName
        End Function

        Private _EMailAddress As String

        ''' <summary>Gets or sets the user's email address when available.</summary>
        Public Property EMailAddress As String
            Get
                If _EMailAddress Is Nothing AndAlso Me.ID <> Nothing AndAlso Me.GetEMailAddress IsNot Nothing AndAlso Me.Provider IsNot Nothing Then
                    _EMailAddress = Me.GetEMailAddress(Me.Provider, Me.ID)
                End If
                Return _EMailAddress
            End Get
            Set(value As String)
                _EMailAddress = value
            End Set
        End Property

        ''' <summary>Gets or sets the email-address lookup delegate.</summary>
        Public Property GetEMailAddress As GetEMailAddressFromId

        ''' <summary>Represents a provider-specific email-address lookup.</summary>
        ''' <param name="provider">The provider that owns the user.</param>
        ''' <param name="id">The provider-specific user identifier.</param>
        ''' <returns>The user's email address, or <see langword="Nothing"/> when unavailable.</returns>
        Public Delegate Function GetEMailAddressFromId(provider As BaseDmsProvider, id As String) As String

        ''' <inheritdoc/>
        Public Overrides Function ToString() As String
            Return Me.DisplayName
        End Function

    End Structure

#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace
