Option Explicit On
Option Strict On

Imports System.Runtime.InteropServices
Imports System.Windows.Forms

'Shared by the library forms and the standalone demos, without changing process awareness.
Friend NotInheritable Class InitialWindowDpi
    Friend Shared Sub Bind(owner As Form)
#If NETFRAMEWORK Then
        AddHandler owner.HandleCreated,
            Sub(sender, e)
                owner.BeginInvoke(New MethodInvoker(
                    Sub()
                        If owner.IsDisposed OrElse owner.Disposing OrElse Not owner.IsHandleCreated Then Return
                        Synchronize(owner)
                    End Sub))
            End Sub
#End If
    End Sub

    Friend Shared Sub Synchronize(owner As Form)
#If NETFRAMEWORK Then
        'Framework can retain the system DPI when initially showing a form on a
        'secondary monitor. Reconcile only an actual native/managed DPI mismatch.
        Dim nativeDpi As UInteger
        Try
            nativeDpi = GetDpiForWindow(owner.Handle)
        Catch ex As EntryPointNotFoundException
            Return 'Older Windows versions retain their existing scaling behavior.
        End Try
        If nativeDpi = 0 OrElse nativeDpi = CUInt(owner.DeviceDpi) Then Return
        Dim factor = nativeDpi / CDbl(owner.DeviceDpi)
        Dim bounds As New NativeRectangle With {
            .Left = owner.Left, .Top = owner.Top,
            .Right = owner.Left + CInt(Math.Round(owner.ClientSize.Width * factor)) + owner.Width - owner.ClientSize.Width,
            .Bottom = owner.Top + CInt(Math.Round(owner.ClientSize.Height * factor)) + owner.Height - owner.ClientSize.Height}
        Dim pointer = Marshal.AllocHGlobal(Marshal.SizeOf(Of NativeRectangle)())
        Try
            Marshal.StructureToPtr(bounds, pointer, False)
            'Use WinForms' normal DPI transition, including its managed DeviceDpi
            'and child scaling, with the real DPI already reported by Windows.
            SendMessage(owner.Handle, &H2E0, New IntPtr(CLng(nativeDpi Or (nativeDpi << 16))), pointer)
        Finally
            Marshal.FreeHGlobal(pointer)
        End Try
#End If
    End Sub

#If NETFRAMEWORK Then
    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRectangle
        Friend Left As Integer
        Friend Top As Integer
        Friend Right As Integer
        Friend Bottom As Integer
    End Structure

    <DllImport("user32.dll")>
    Private Shared Function GetDpiForWindow(handle As IntPtr) As UInteger
    End Function

    <DllImport("user32.dll", EntryPoint:="SendMessageW")>
    Private Shared Function SendMessage(handle As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function
#End If
End Class
