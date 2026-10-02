Option Explicit On
Option Strict On

Namespace Data

    ''' <summary>
    ''' Describes a selectable DMS instance exposed by a provider.
    ''' </summary>
    Public NotInheritable Class DmsInstanceInfo

        ''' <summary>
        ''' Initializes a DMS instance description.
        ''' </summary>
        ''' <param name="id">The provider-defined stable instance identifier.</param>
        ''' <param name="displayName">The name shown to users.</param>
        ''' <param name="isSelected">A value indicating whether the instance is currently selected.</param>
        ''' <exception cref="ArgumentException"><paramref name="id"/> or <paramref name="displayName"/> is empty.</exception>
        Public Sub New(id As String, displayName As String, isSelected As Boolean)
            If String.IsNullOrWhiteSpace(id) Then Throw New ArgumentException("A DMS instance ID is required.", NameOf(id))
            If String.IsNullOrWhiteSpace(displayName) Then Throw New ArgumentException("A DMS instance display name is required.", NameOf(displayName))

            Me.ID = id
            Me.DisplayName = displayName
            Me.IsSelected = isSelected
        End Sub

        ''' <summary>
        ''' Gets the provider-defined stable instance identifier.
        ''' </summary>
        Public ReadOnly Property ID As String

        ''' <summary>
        ''' Gets the name shown to users.
        ''' </summary>
        Public ReadOnly Property DisplayName As String

        ''' <summary>
        ''' Gets a value indicating whether the instance is currently selected.
        ''' </summary>
        Public ReadOnly Property IsSelected As Boolean

    End Class

End Namespace
