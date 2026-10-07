Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports NUnit.Framework

'Pumps continuations and tracks Async Sub event completion on a deterministic STA context.
Friend NotInheritable Class UiTestDispatcher
    Inherits SynchronizationContext
    Implements IDisposable

    Private ReadOnly PreviousContext As SynchronizationContext = SynchronizationContext.Current
    Private ReadOnly PreviousAutoInstall As Boolean = WindowsFormsSynchronizationContext.AutoInstall
    Private ReadOnly Callbacks As New System.Collections.Concurrent.ConcurrentQueue(Of Action)
    Private ActiveEvents As Integer

    Friend Sub New()
        WindowsFormsSynchronizationContext.AutoInstall = False
        SynchronizationContext.SetSynchronizationContext(Me)
    End Sub

    Public Overrides Sub Post(callback As SendOrPostCallback, state As Object)
        Callbacks.Enqueue(Sub() callback(state))
    End Sub

    Public Overrides Sub OperationStarted()
        Interlocked.Increment(ActiveEvents)
    End Sub

    Public Overrides Sub OperationCompleted()
        Interlocked.Decrement(ActiveEvents)
    End Sub

    Friend Sub WaitForEvents()
        PumpUntil(Function() ActiveEvents = 0)
    End Sub

    Friend Sub Finish(operation As Task)
        PumpUntil(Function() operation.IsCompleted)
        operation.GetAwaiter().GetResult()
    End Sub

    Friend Sub PumpUntil(condition As Func(Of Boolean))
        Dim Deadline As DateTime = DateTime.UtcNow.AddSeconds(5)
        While Not condition() AndAlso DateTime.UtcNow < Deadline
            Dim Callback As Action = Nothing
            While Callbacks.TryDequeue(Callback)
                Callback()
            End While
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        Assert.That(condition(), [Is].True, "The pending UI operation did not complete.")
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        SynchronizationContext.SetSynchronizationContext(PreviousContext)
        WindowsFormsSynchronizationContext.AutoInstall = PreviousAutoInstall
    End Sub
End Class
