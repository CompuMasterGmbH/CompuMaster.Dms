Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    Public Module DmsFactory

        Public Function CreateAuthorizedDmsProviderInstance(profile As IDmsLoginProfile) As BaseDmsProvider
            Dim Result As BaseDmsProvider
            Result = CreateDmsProviderInstance(profile.ProviderID)
            Result.Authorize(profile)
            Return Result
        End Function

        Public Function CreateDmsProviderInstance(provider As BaseDmsProvider.DmsProviders) As BaseDmsProvider
            Select Case provider
                Case Providers.BaseDmsProvider.DmsProviders.None
                    Return New NoDmsProvider
                Case Providers.BaseDmsProvider.DmsProviders.ManualUrl
                    Return New ManualUrlDmsProvider
                Case Providers.BaseDmsProvider.DmsProviders.CenterDevice
                    Return New CenterDeviceDmsProvider
                Case Providers.BaseDmsProvider.DmsProviders.Scopevisio
                    Return New ScopevisioTeamworkDmsProvider
                Case Providers.BaseDmsProvider.DmsProviders.WebDAV
                    Return New WebDavDmsProvider
                Case Else
                    Throw New NotImplementedException("Not yet implemented DMS provider: " & provider.ToString)
            End Select
        End Function

    End Module

End Namespace