Imports System.Runtime.CompilerServices
Imports System.Globalization
Imports System.Resources
Imports System.Threading.Tasks
Imports System.Windows.Forms

Partial Public Class DmsBrowser
    Private ReadOnly FailureContexts As New ConditionalWeakTable(Of Exception, DmsBrowserFailureEventArgs)
    Private Shared ReadOnly UserMessages As ResourceManager() = {
        New ResourceManager("CompuMaster.Dms.BrowserUI.UiStrings", GetType(DmsBrowser).Assembly),
        New ResourceManager("CompuMaster.Dms.ProviderStrings", GetType(Providers.BaseDmsProvider).Assembly)
    }

    ''' <summary>Reports failures caught by the browser to explicitly attached host diagnostics.</summary>
    ''' <remarks>The original exception is supplied synchronously on the handling thread before the standard error dialog. Normal UI handlers consume failures; startup failures cancel and close the browser. Public provider APIs and task-returning operations retain their original exception propagation. Expected user cancellation is not reported. Subscriber exceptions do not replace the original failure. No diagnostics are automatically persisted; subscribers must protect sensitive exception data.</remarks>
    Public Event OperationFailed As EventHandler(Of DmsBrowserFailureEventArgs)

    ''' <summary>Notifies host diagnostics of a browser failure.</summary>
    ''' <param name="e">The original exception and browser context. Subscribers may suppress the standard error dialog.</param>
    Protected Overridable Sub OnOperationFailed(e As DmsBrowserFailureEventArgs)
        'A broken diagnostics subscriber must not replace the original failure or interrupt cleanup.
        Dim subscribers = OperationFailedEvent
        If subscribers Is Nothing Then Return
        For Each subscriber As EventHandler(Of DmsBrowserFailureEventArgs) In subscribers.GetInvocationList()
            Try
                subscriber.Invoke(Me, e)
            Catch
                'Do not automatically log subscriber exceptions, which may also contain secrets.
            End Try
        Next
    End Sub

    ''' <summary>Presents a concise browser error without exposing provider diagnostics.</summary>
    ''' <param name="message">The localized user-facing message.</param>
    ''' <param name="title">The dialog title.</param>
    ''' <param name="icon">The dialog severity icon.</param>
    Protected Overridable Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
        MessageBox.Show(Me, message, title, MessageBoxButtons.OK, icon)
    End Sub

    Private Sub RememberFailure(exception As Exception, phase As DmsBrowserFailurePhase, operation As String)
        FailureContexts.GetValue(exception, Function(key) New DmsBrowserFailureEventArgs(key, phase, operation))
    End Sub

    Friend Sub HandleBrowserFailure(exception As Exception, Optional phase As DmsBrowserFailurePhase = DmsBrowserFailurePhase.Action,
                                    Optional title As String = Nothing, Optional icon As MessageBoxIcon = MessageBoxIcon.Error,
                                    Optional showDialog As Boolean = True, <CallerMemberName> Optional operation As String = Nothing)
        Dim failure As DmsBrowserFailureEventArgs = Nothing
        If FailureContexts.TryGetValue(exception, failure) Then
            FailureContexts.Remove(exception)
        Else
            failure = New DmsBrowserFailureEventArgs(exception, phase, operation)
        End If
        Me.OnOperationFailed(failure)
        If showDialog AndAlso Not failure.SuppressDialog AndAlso Not Me.IsDisposed AndAlso Not Me.Disposing Then
            Me.ShowFailureDialog(FailureUserMessage(exception), If(title, Me.Text), icon)
        End If
    End Sub

    Private Shared Function FailureUserMessage(exception As Exception) As String
        If TypeOf exception Is Data.DmsUserInputInvalidException OrElse TypeOf exception Is Data.DmsUserErrorMessageException Then
            'Retain approved static validation messages, never arbitrary provider/request text.
            For Each messageResources As ResourceManager In UserMessages
                For Each entry As Collections.DictionaryEntry In messageResources.GetResourceSet(CultureInfo.CurrentUICulture, True, True)
                    Dim message = TryCast(entry.Value, String)
                    If message IsNot Nothing AndAlso Not message.Contains("{"c) AndAlso String.Equals(message, exception.Message, StringComparison.Ordinal) Then Return message
                Next
            Next
        End If
        Return UiStrings.GetText("BrowserOperationFailed")
    End Function

    Private Async Function CaptureFailurePhaseAsync(operation As Func(Of Task), phase As DmsBrowserFailurePhase,
                                                   <CallerMemberName> Optional name As String = Nothing) As Task
        Try
            Await operation()
        Catch ex As Exception
            RememberFailure(ex, phase, name)
            Throw
        End Try
    End Function

    Private Async Function CaptureFailurePhaseAsync(Of T)(operation As Func(Of Task(Of T)), phase As DmsBrowserFailurePhase,
                                                         <CallerMemberName> Optional name As String = Nothing) As Task(Of T)
        Try
            Return Await operation()
        Catch ex As Exception
            RememberFailure(ex, phase, name)
            Throw
        End Try
    End Function
End Class
