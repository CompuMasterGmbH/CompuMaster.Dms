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
        Dim previousFocus As Control = CaptureFocusedControl(Owner)
        Dim controlStates As New Dictionary(Of Control, Boolean)
        Try
            Owner.UseWaitCursor = True
            If WasEnabled Then
                For Each control As Control In Owner.Controls
                    controlStates.Add(control, control.Enabled)
                    control.Enabled = False
                Next
            End If
            Await operation()
            Me.IsRunning = False
            onSuccess?.Invoke()
        Finally
            Me.IsRunning = False
            If Not Owner.IsDisposed Then
                For Each state In controlStates
                    If Not state.Key.IsDisposed Then state.Key.Enabled = state.Value
                Next
                Owner.UseWaitCursor = HadWaitCursor
                RestoreFocusedControl(Owner, previousFocus)
            End If
        End Try
    End Function

    Friend Shared Function CaptureFocusedControl(container As Control) As Control
        If Not container.ContainsFocus Then Return Nothing
        For Each child As Control In container.Controls
            If child.ContainsFocus Then Return CaptureFocusedControl(child)
        Next
        Return If(container.Focused AndAlso Not TypeOf container Is Form, container, Nothing)
    End Function

    Friend Shared Sub RestoreFocusedControl(owner As Form, previousFocus As Control)
        'Focus only within the still-active window; never activate it after the user switches away.
        If owner.IsDisposed OrElse owner.Disposing OrElse Not owner.Visible OrElse
           Form.ActiveForm IsNot owner Then Return
        If previousFocus Is Nothing OrElse previousFocus.IsDisposed OrElse previousFocus.Disposing OrElse
           Not owner.Contains(previousFocus) OrElse Not previousFocus.Visible OrElse
           Not previousFocus.Enabled OrElse Not previousFocus.CanFocus Then Return
        previousFocus.Focus()
    End Sub

    Private Sub PreventClosingDuringOperation(sender As Object, e As FormClosingEventArgs)
        If Me.IsRunning Then e.Cancel = True
    End Sub
End Class
