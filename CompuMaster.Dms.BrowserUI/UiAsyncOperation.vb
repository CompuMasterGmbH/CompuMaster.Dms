Imports System.Threading.Tasks
Imports System.Windows.Forms

'Keeps a dialog alive until its server operation has completed.
Friend NotInheritable Class UiAsyncOperation
    Private ReadOnly Owner As Form

    Friend Sub New(owner As Form)
        Me.Owner = owner
        AddHandler owner.FormClosing, AddressOf PreventClosingDuringOperation
    End Sub

    Friend Property IsRunning As Boolean
        Get
            Return _IsRunning
        End Get
        Private Set(value As Boolean)
            _IsRunning = value
        End Set
    End Property
    Private _IsRunning As Boolean

    Friend Async Function RunAsync(operation As Func(Of Task), Optional onSuccess As Action = Nothing) As Task
        If Me.IsRunning Then Return
        Me.IsRunning = True
        Dim WasEnabled As Boolean = Owner.Enabled
        Dim HadWaitCursor As Boolean = Owner.UseWaitCursor
        Try
            Owner.Enabled = False
            Owner.UseWaitCursor = True
            Await operation()
            Me.IsRunning = False
            onSuccess?.Invoke()
        Finally
            Me.IsRunning = False
            If Not Owner.IsDisposed Then
                Owner.Enabled = WasEnabled
                Owner.UseWaitCursor = HadWaitCursor
            End If
        End Try
    End Function

    Private Sub PreventClosingDuringOperation(sender As Object, e As FormClosingEventArgs)
        If Me.IsRunning Then e.Cancel = True
    End Sub
End Class
