Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    ''' <summary>
    ''' Provides optional discovery and selection of DMS instances after authorization.
    ''' </summary>
    ''' <remarks>
    ''' Consumers can test whether a provider implements this interface without changing the normal single-instance login flow.
    ''' </remarks>
    Public Interface IDmsInstanceProvider

        ''' <summary>
        ''' Gets the currently selected DMS instance.
        ''' </summary>
        ''' <returns>The current instance, or <see langword="Nothing"/> when the provider has no selected instance.</returns>
        ReadOnly Property CurrentDmsInstance As DmsInstanceInfo

        ''' <summary>
        ''' Lists the DMS instances available to the authorized user.
        ''' </summary>
        ''' <returns>A read-only list of available instances.</returns>
        ''' <exception cref="InvalidOperationException">The provider has not been authorized.</exception>
        Function ListAvailableDmsInstances() As IReadOnlyList(Of DmsInstanceInfo)

        ''' <summary>
        ''' Selects the DMS instance used for subsequent provider operations.
        ''' </summary>
        ''' <param name="instanceID">The identifier returned by <see cref="ListAvailableDmsInstances"/>.</param>
        ''' <exception cref="ArgumentException"><paramref name="instanceID"/> is empty.</exception>
        ''' <exception cref="ArgumentOutOfRangeException"><paramref name="instanceID"/> is not available to the authorized user.</exception>
        ''' <exception cref="InvalidOperationException">The provider has not been authorized.</exception>
        Sub SelectDmsInstance(instanceID As String)

    End Interface

End Namespace
