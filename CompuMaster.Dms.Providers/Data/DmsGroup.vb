Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

    ''' <summary>Represents a provider-owned group and its optional display-name lookup.</summary>
#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types
    Public Structure DmsGroup
        ''' <summary>Gets or sets the provider-owned identifier used for remote operations.</summary>
        Public Property ID As String

        Private _Name As String
        ''' <summary>Gets or sets the display name supplied by the provider or caller.</summary>
        ''' <remarks>The first read can synchronously invoke GetName when an ID and provider are available. DisplayName falls back to the ID if no name is supplied.</remarks>
        Public Property Name As String
            Get
                If _Name = Nothing AndAlso Me.ID <> Nothing AndAlso Me.GetName IsNot Nothing AndAlso Me.Provider IsNot Nothing Then
                    _Name = Me.GetName(Me.Provider, Me.ID)
                End If
                Return _Name
            End Get
            Set(value As String)
                _Name = value
            End Set
        End Property

        ''' <summary>Gets or sets the provider used to resolve this identity.</summary>
        Public Provider As BaseDmsProvider
        ''' <summary>Gets or sets the optional delegate used to resolve a group name from its identifier.</summary>
        Public GetName As GetDisplayNameFromId
        ''' <summary>Resolves a group display name using its provider-owned identifier.</summary>
        ''' <param name="provider">The provider that owns the resource or identity.</param>
        ''' <param name="id">The provider-owned identifier.</param>
        ''' <returns>The provider-supplied display name, or Nothing when unavailable.</returns>
        Public Delegate Function GetDisplayNameFromId(provider As BaseDmsProvider, id As String) As String

        ''' <summary>Gets the resolved group name, falling back to the provider-owned ID when no name is available.</summary>
        Public ReadOnly Property DisplayName As String
            Get
                If Me.Name <> Nothing Then
                    Return Me.Name
                ElseIf Me.ID <> Nothing Then
                    Return Me.ID
                Else
                    Return Nothing
                End If
            End Get
        End Property

        ''' <inheritdoc/>
        ''' <returns>The resolved display name, or Nothing when both name and ID are unavailable.</returns>
        Public Overrides Function ToString() As String
            Return Me.DisplayName
        End Function

    End Structure
#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace
