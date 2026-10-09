Imports System.Runtime.InteropServices
Imports System.Runtime.InteropServices.ComTypes
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data

Partial Public Class DmsBrowser
    Private DragDownloadRunning As Boolean
    Private DragDownloadFailureDisplayed As Boolean

    Friend Function CanDragDownload() As Boolean
        Return Not DragDownloadRunning AndAlso Not TransferRunning AndAlso Not ResourceActionRunning AndAlso (AllowedActions And FileOrFolderActions.AllowDownloadFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles
    End Function

    Private Sub DragRemoteFiles(sender As Object, e As ItemDragEventArgs)
        If e.Button <> MouseButtons.Left OrElse Not CanDragDownload() Then Return
        Dim files = CurrentSelectedFiles.ToArray()
        If files.Length = 0 Then Return
        DragDownloadRunning = True
        DragDownloadFailureDisplayed = False
        Try
            'The OLE operation is synchronous: the shell consumes file streams before cleanup.
            'Copy is the only offered effect and never deletes a remote source.
            Using data As New ShellDownloadData(files, AddressOf SupplyDraggedFile)
                Dim initialized = InitializeOle(IntPtr.Zero)
                Marshal.ThrowExceptionForHR(initialized)
                Try
                    Dim effect As Integer
                    ListViewDmsFiles.Capture = False
                    Using source As New CopyDragSource()
                        Dim result = StartShellDrag(data, source.Pointer, 1, effect)
                        If result < 0 Then Marshal.ThrowExceptionForHR(result)
                    End Using
                Finally
                    UninitializeOle()
                End Try
            End Using
        Catch ex As OperationCanceledException
            Return
        Catch ex As Exception
            If Not DragDownloadFailureDisplayed Then MessageBox.Show(Me, UiStrings.Format("ErrorMessage", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            DragDownloadRunning = False
        End Try
    End Sub

    Private Sub SupplyDraggedFile(file As DmsResourceItem, path As String)
        If (AllowedActions And FileOrFolderActions.AllowDownloadFiles) = 0 Then Throw New UnauthorizedAccessException()
        Using dialog = CreateTransferDialog({file.Name}, True)
            dialog.Show(Me)
            Dim operation = RunTransferAsync(Function() DownloadBatchWithProgressAsync(DmsProvider, {file}, {path}, dialog, dialog.CancellationToken))
            'GetData is a synchronous COM callback; keep the STA message loop responsive.
            While Not operation.IsCompleted
                Application.DoEvents()
                Threading.Thread.Sleep(10)
            End While
            Try
                operation.GetAwaiter().GetResult()
            Catch ex As Exception
                DragDownloadFailureDisplayed = True
                dialog.SetFailure(ex)
                dialog.Finish()
                dialog.ShowDialog(Me)
                Throw
            Finally
                dialog.Finish()
            End Try
        End Using
    End Sub

    <DllImport("ole32.dll", EntryPoint:="OleInitialize")>
    Private Shared Function InitializeOle(reserved As IntPtr) As Integer
    End Function
    <DllImport("ole32.dll", EntryPoint:="OleUninitialize")>
    Private Shared Sub UninitializeOle()
    End Sub

    <DllImport("ole32.dll", EntryPoint:="DoDragDrop")>
    Private Shared Function StartShellDrag(data As System.Runtime.InteropServices.ComTypes.IDataObject, source As IntPtr, allowedEffects As Integer, ByRef effect As Integer) As Integer
    End Function

    'A scoped native IDropSource avoids adding a public COM-only interface to BrowserUI.
    Private NotInheritable Class CopyDragSource
        Implements IDisposable
        Private ReadOnly Functions As [Delegate]()
        Private Vtable As IntPtr
        Friend ReadOnly Property Pointer As IntPtr
        Private References As Integer = 1

        Friend Sub New()
            Functions = New [Delegate]() {New QueryCallback(AddressOf QueryInterface), New RefCallback(AddressOf AddRef), New RefCallback(AddressOf Release), New ContinueCallback(AddressOf QueryContinueDrag), New FeedbackCallback(AddressOf GiveFeedback)}
            Vtable = Marshal.AllocHGlobal(IntPtr.Size * Functions.Length)
            Pointer = Marshal.AllocHGlobal(IntPtr.Size)
            For index = 0 To Functions.Length - 1
                Marshal.WriteIntPtr(Vtable, index * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(Functions(index)))
            Next
            Marshal.WriteIntPtr(Pointer, Vtable)
        End Sub

        Private Function QueryInterface(instance As IntPtr, ByRef iid As Guid, ByRef result As IntPtr) As Integer
            If iid = New Guid("00000000-0000-0000-C000-000000000046") OrElse iid = New Guid("00000121-0000-0000-C000-000000000046") Then
                result = Pointer
                AddRef(instance)
                Return 0
            End If
            result = IntPtr.Zero
            Return &H80004002
        End Function
        Private Function AddRef(instance As IntPtr) As UInteger
            Return CUInt(Threading.Interlocked.Increment(References))
        End Function
        Private Function Release(instance As IntPtr) As UInteger
            Return CUInt(Math.Max(0, Threading.Interlocked.Decrement(References)))
        End Function
        Private Function QueryContinueDrag(instance As IntPtr, escapePressed As Boolean, keyState As Integer) As Integer
            If escapePressed Then Return &H40101 'DRAGDROP_S_CANCEL.
            Return If((keyState And 1) = 0, &H40100, 0) 'Drop after the left button is released.
        End Function
        Private Function GiveFeedback(instance As IntPtr, effect As Integer) As Integer
            Return &H40102 'Use the standard Windows copy/no-drop cursors.
        End Function
        Public Sub Dispose() Implements IDisposable.Dispose
            Marshal.FreeHGlobal(Pointer)
            Marshal.FreeHGlobal(Vtable)
            GC.KeepAlive(Functions)
        End Sub
        <UnmanagedFunctionPointer(CallingConvention.StdCall)>
        Private Delegate Function QueryCallback(instance As IntPtr, ByRef iid As Guid, ByRef result As IntPtr) As Integer
        <UnmanagedFunctionPointer(CallingConvention.StdCall)>
        Private Delegate Function RefCallback(instance As IntPtr) As UInteger
        <UnmanagedFunctionPointer(CallingConvention.StdCall)>
        Private Delegate Function ContinueCallback(instance As IntPtr, <MarshalAs(UnmanagedType.Bool)> escapePressed As Boolean, keyState As Integer) As Integer
        <UnmanagedFunctionPointer(CallingConvention.StdCall)>
        Private Delegate Function FeedbackCallback(instance As IntPtr, effect As Integer) As Integer
    End Class
End Class
