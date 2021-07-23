Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers

Namespace Data

    Public Interface IDmsLoginProfile

        ''' <summary>
        ''' A (display) name of the profile
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property ProfileName As String
        ''' <summary>
        ''' A provider ID
        ''' </summary>
        ReadOnly Property ProviderID As BaseDmsProvider.DmsProviders
        ''' <summary>
        ''' The username for login
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property UserName As String
        ''' <summary>
        ''' The password for login
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property Password As String
        ''' <summary>
        ''' An optional reference to the customer instance, e.g. a client number
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property CustomerInstance As String
        ''' <summary>
        ''' An optional reference to a server
        ''' </summary>
        ''' <returns></returns>
        ReadOnly Property ServerAddress As String

    End Interface

End Namespace