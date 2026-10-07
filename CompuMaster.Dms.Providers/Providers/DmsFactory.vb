Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports System.Threading
Imports System.Threading.Tasks

Namespace Providers

    ''' <summary>Creates built-in DMS providers and optionally authorizes them.</summary>
    Public Module DmsFactory

        ''' <summary>Creates a built-in provider and authorizes it with the supplied login profile.</summary>
        ''' <param name="profile">The login profile used to select and authorize the provider.</param>
        ''' <returns>The authorized provider; authorization failures propagate to the caller.</returns>
        Public Function CreateAuthorizedDmsProviderInstance(profile As IDmsLoginProfile) As BaseDmsProvider
            Dim Result As BaseDmsProvider
            Result = CreateDmsProviderInstance(profile.ProviderID)
            Result.Authorize(profile)
            Return Result
        End Function

        ''' <summary>Creates and authorizes a DMS provider without blocking the calling thread during authorization.</summary>
        ''' <param name="profile">The login profile used to select and authorize the provider.</param>
        ''' <param name="cancellationToken">Cancels a queued authorization; active synchronous provider calls cannot be interrupted.</param>
        ''' <returns>The authorized provider instance.</returns>
        Public Async Function CreateAuthorizedDmsProviderInstanceAsync(profile As IDmsLoginProfile, Optional cancellationToken As CancellationToken = Nothing) As Task(Of BaseDmsProvider)
            If profile Is Nothing Then Throw New ArgumentNullException(NameOf(profile))
            Dim result As BaseDmsProvider = CreateDmsProviderInstance(profile.ProviderID)
            Await result.AuthorizeAsync(profile, cancellationToken).ConfigureAwait(False)
            Return result
        End Function

        ''' <summary>Creates an uninitialized built-in DMS provider.</summary>
        ''' <param name="provider">The provider that owns the resource or identity.</param>
        ''' <returns>The provider selected by the specified identifier.</returns>
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
                    Throw New NotImplementedException(ProviderStrings.Format("NotYetImplementedDMSProvider", provider.ToString))
            End Select
        End Function

    End Module

End Namespace
