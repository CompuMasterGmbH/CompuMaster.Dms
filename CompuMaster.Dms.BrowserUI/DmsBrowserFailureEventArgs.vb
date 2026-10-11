''' <summary>Identifies the browser phase in which an operation failed.</summary>
Public Enum DmsBrowserFailurePhase
    ''' <summary>Initializes the browser view.</summary>
    Startup
    ''' <summary>Authorizes the configured provider.</summary>
    Authentication
    ''' <summary>Selects or changes the provider instance.</summary>
    InstanceSelection
    ''' <summary>Looks up the configured remote root.</summary>
    ScopedRootLookup
    ''' <summary>Reads or presents additional resource metadata.</summary>
    MetadataEnrichment
    ''' <summary>Loads child folders or collections.</summary>
    ChildListing
    ''' <summary>Selects the initially requested folder.</summary>
    InitialSelection
    ''' <summary>Refreshes the current files or folder tree.</summary>
    Refresh
    ''' <summary>Performs a user-requested resource or transfer action.</summary>
    Action
End Enum

''' <summary>Provides the original browser failure to an explicitly attached diagnostics subscriber.</summary>
''' <remarks>The exception may contain sensitive provider data. Subscribers must redact secrets before persisting or forwarding diagnostics. BrowserUI does not automatically log this exception.</remarks>
Public NotInheritable Class DmsBrowserFailureEventArgs
    Inherits EventArgs

    Friend Sub New(exception As Exception, phase As DmsBrowserFailurePhase, operation As String)
        If exception Is Nothing Then Throw New ArgumentNullException(NameOf(exception))
        Me.Exception = exception
        Me.Phase = phase
        Me.Operation = operation
    End Sub

    ''' <summary>Gets the original exception, including its stack trace, inner exceptions and data.</summary>
    Public ReadOnly Property Exception As Exception

    ''' <summary>Gets the most specific browser phase known at the failure boundary.</summary>
    ''' <remarks>Provider-internal work is identified by the browser request boundary; provider-specific subphases are not inferred.</remarks>
    Public ReadOnly Property Phase As DmsBrowserFailurePhase

    ''' <summary>Gets the browser operation name without request URLs, paths or credentials.</summary>
    Public ReadOnly Property Operation As String

    ''' <summary>Gets or sets whether the standard browser error dialog is suppressed for this failure.</summary>
    ''' <remarks>The default is false. Suppression does not change cleanup, startup cancellation, exception propagation or the separate transfer-progress view.</remarks>
    Public Property SuppressDialog As Boolean
End Class
