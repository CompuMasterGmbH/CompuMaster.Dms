Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types
    Public Structure DmsGroup
        Public Property ID As String

        Private _Name As String
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

        Public Provider As BaseDmsProvider
        Public GetName As GetDisplayNameFromId
        Public Delegate Function GetDisplayNameFromId(provider As BaseDmsProvider, id As String) As String

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

        Public Overrides Function ToString() As String
            Return Me.DisplayName
        End Function

    End Structure
#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace