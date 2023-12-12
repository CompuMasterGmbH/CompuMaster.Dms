Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types

    Public Structure DmsUser
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

        ''' <summary>
        ''' The user name or ID
        ''' </summary>
        ''' <returns></returns>
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