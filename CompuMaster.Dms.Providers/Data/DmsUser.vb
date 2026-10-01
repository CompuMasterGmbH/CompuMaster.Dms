Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types

    Public Structure DmsUser
        Public Property ID As String

        Private _Name As String
        ''' <summary>Gets or sets the legacy display name of the user.</summary>
        ''' <remarks>Use <see cref="DisplayName"/> in new code. This property remains for existing consumers.</remarks>
        Public Property Name As String
            Get
                If _Name = Nothing AndAlso Me.ID <> Nothing AndAlso Me.Provider IsNot Nothing Then
                    Dim resolver As GetDisplayNameFromId = If(Me.GetDisplayName, Me.GetName)
                    If resolver IsNot Nothing Then _Name = resolver(Me.Provider, Me.ID)
                End If
                Return _Name
            End Get
            Set(value As String)
                _Name = value
            End Set
        End Property

        Public Provider As BaseDmsProvider
        ''' <summary>Resolves a user's display name from the provider-specific ID when needed.</summary>
        Public GetDisplayName As GetDisplayNameFromId
        ''' <summary>Provides the legacy display-name resolver.</summary>
        ''' <remarks>Use <see cref="GetDisplayName"/> for new provider implementations.</remarks>
        Public GetName As GetDisplayNameFromId
        ''' <summary>Represents a provider-specific display-name lookup.</summary>
        ''' <param name="provider">The provider that owns the user.</param>
        ''' <param name="id">The provider-specific user ID.</param>
        ''' <returns>The user's display name, or <see langword="Nothing"/> when unavailable.</returns>
        Public Delegate Function GetDisplayNameFromId(provider As BaseDmsProvider, id As String) As String

        ''' <summary>Gets or sets the user's display label.</summary>
        ''' <remarks>Falls back to the provider-specific ID when no display name is available.</remarks>
        Public Property DisplayName As String
            Get
                Dim resolvedName As String = Me.Name
                If Not String.IsNullOrWhiteSpace(resolvedName) Then
                    Return resolvedName.Trim()
                End If
                If Me.ID <> Nothing Then
                    Return Me.ID
                Else
                    Return Nothing
                End If
            End Get
            Set(value As String)
                Me.Name = value
            End Set
        End Property

        Private _EMailAddress As String
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

        Public GetEMailAddress As GetEMailAddressFromId
        Public Delegate Function GetEMailAddressFromId(provider As BaseDmsProvider, id As String) As String

        Public Overrides Function ToString() As String
            Return Me.DisplayName
        End Function

    End Structure
#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace
