Imports System.Runtime.InteropServices
Imports System.Runtime.InteropServices.ComTypes
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Partial Public Class DmsBrowser
    Private DragDownloadRunning As Boolean
    Private DragDownloadFailureDisplayed As Boolean
    Friend ShellDragOperation As Func(Of System.Runtime.InteropServices.ComTypes.IDataObject, IntPtr, Integer)
    Private ActiveRemoteDrag As RemoteDragContext

    Private NotInheritable Class RemoteDragContext
        Friend ReadOnly Token As Guid = Guid.NewGuid()
        Friend ReadOnly Provider As BaseDmsProvider
        Friend ReadOnly Instance As String
        Friend ReadOnly Scope As String
        Friend ReadOnly Files As DmsResourceItem()
        Friend MoveTarget As TreeNode
        Friend MovePath As String
        Friend MoveIdentity As DmsResourceItem
        Friend HoverTarget As TreeNode
        Friend ValidationFailure As Exception
        Friend Sub New(provider As BaseDmsProvider, scope As String, files As DmsResourceItem())
            Me.Provider = provider
            Me.Instance = TryCast(provider, IDmsInstanceProvider)?.CurrentDmsInstance?.ID
            Me.Scope = scope
            Me.Files = files.Select(Function(file) New DmsResourceItem With {.ItemType = file.ItemType, .FullName = file.FullName, .Name = file.Name,
                .ExtendedInfosFileID = file.ExtendedInfosFileID, .ExtendedInfosAssignedFolderID = file.ExtendedInfosAssignedFolderID,
                .ExtendedInfosAssignedCollectionID = file.ExtendedInfosAssignedCollectionID, .ExtendedInfosCollisionDetected = file.ExtendedInfosCollisionDetected}).ToArray()
        End Sub
    End Class

    Friend Function CanDragDownload() As Boolean
        Return Not DragDownloadRunning AndAlso Not TransferRunning AndAlso Not ResourceActionRunning AndAlso (AllowedActions And FileOrFolderActions.AllowDownloadFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles
    End Function

    Private Async Sub DragRemoteFiles(sender As Object, e As ItemDragEventArgs)
        Dim canDownload = CanDragDownload()
        Dim canMove = CanStartRemoteMove()
        If e.Button <> MouseButtons.Left OrElse (Not canDownload AndAlso Not canMove) Then Return
        Dim files = DragDownloadSelection(TryCast(e.Item, ListViewItem))
        If files.Length = 0 Then Return
        DragDownloadRunning = True
        DragDownloadFailureDisplayed = False
        Dim context As RemoteDragContext = Nothing
        Dim dropped As Boolean
        Try
            context = New RemoteDragContext(DmsProvider, InitialFolder, files)
            ActiveRemoteDrag = context
            'The OLE operation is synchronous: the shell consumes file streams before cleanup.
            'External shell targets only consume virtual copies. A move commits
            'after OLE returns, and only for a validated internal tree target.
            Using data As New ShellDownloadData(files, AddressOf SupplyDraggedFile, If(canMove, context.Token.ToByteArray(), Nothing), canDownload)
                Dim initialized = InitializeOle(IntPtr.Zero)
                Marshal.ThrowExceptionForHR(initialized)
                Try
                    Dim effect As Integer
                    ListViewDmsFiles.Capture = False
                    Using source As New CopyDragSource(Function() context.HoverTarget IsNot Nothing AndAlso CanMoveTo(context, context.HoverTarget))
                        Dim allowed = If(canDownload, 1, 0) Or If(canMove, 2, 0)
                        Dim result = If(ShellDragOperation Is Nothing, StartShellDrag(data, source.Pointer, allowed, effect), ShellDragOperation(data, source.Pointer))
                        If result < 0 Then Marshal.ThrowExceptionForHR(result)
                        dropped = result = &H40100
                    End Using
                Finally
                    UninitializeOle()
                End Try
            End Using
            If context.ValidationFailure IsNot Nothing Then
                dropped = False
                Throw context.ValidationFailure
            End If
        Catch ex As OperationCanceledException
            Return
        Catch ex As Exception
            If Not DragDownloadFailureDisplayed Then Me.HandleBrowserFailure(ex)
        Finally
            DragDownloadRunning = False
            ActiveRemoteDrag = Nothing
            UpdateFolderDownloadAccess()
        End Try
        If dropped AndAlso context.MoveTarget IsNot Nothing Then Await MoveDroppedFilesAsync(context)
    End Sub

    Private Function CanStartRemoteMove() As Boolean
        Return Not DragDownloadRunning AndAlso Not TransferRunning AndAlso Not ResourceActionRunning AndAlso
            (AllowedActions And FileOrFolderActions.AllowCopyRenameMoveFiles) <> 0 AndAlso BrowseMode = BrowseModes.FoldersAndFiles
    End Function

    Private Function RemoteMoveContext(data As System.Windows.Forms.IDataObject) As RemoteDragContext
        Dim current = ActiveRemoteDrag
        If current Is Nothing OrElse data Is Nothing OrElse Not data.GetDataPresent(ShellDownloadData.InternalMoveFormatName, False) Then Return Nothing
        Try
            Dim value = data.GetData(ShellDownloadData.InternalMoveFormatName, False)
            Dim bytes = TryCast(value, Byte())
            If TypeOf value Is System.IO.MemoryStream Then bytes = DirectCast(value, System.IO.MemoryStream).ToArray()
            If bytes Is Nothing OrElse bytes.Length <> 16 OrElse New Guid(bytes) <> current.Token Then Return Nothing
            Return current
        Catch ex As System.Runtime.InteropServices.ExternalException
            Return Nothing
        End Try
    End Function

    Private Function CanMoveTo(context As RemoteDragContext, node As TreeNode, Optional ownsAction As Boolean = False) As Boolean
        If context Is Nothing OrElse IsDisposed OrElse DmsProvider IsNot context.Provider OrElse InitialFolder <> context.Scope OrElse
            Not HasMatchingRemoteInstance(context) OrElse
            TransferRunning OrElse (ResourceActionRunning AndAlso Not ownsAction) OrElse
            (AllowedActions And FileOrFolderActions.AllowCopyRenameMoveFiles) = 0 OrElse BrowseMode <> BrowseModes.FoldersAndFiles OrElse
            node Is Nothing OrElse node.TreeView IsNot TreeViewDmsFolders OrElse Not TreeViewDmsFolders.Enabled Then Return False
        Dim item = TryCast(node.Tag, NodeTagData)?.DmsResourceItem
        If Not TypeOf node.Tag Is NodeTagData OrElse item?.ItemType = DmsResourceItem.ItemTypes.File OrElse item?.ExtendedInfosCollisionDetected OrElse
            (item?.ItemType = DmsResourceItem.ItemTypes.Collection AndAlso Not DmsProvider.SupportsCollections) Then Return False
        Dim path = UploadDestination(node)
        If Not IsWithinBrowserRoot(path) OrElse Not CanAcceptDirectFiles(node) Then Return False
        Dim identities As New HashSet(Of String)(StringComparer.Ordinal)
        Dim names As New HashSet(Of String)(StringComparer.Ordinal)
        Dim changes As Boolean
        For Each file In context.Files
            If file.ItemType <> DmsResourceItem.ItemTypes.File OrElse String.IsNullOrEmpty(file.FullName) OrElse Not IsWithinBrowserRoot(file.FullName) OrElse
                Not IsValidResourceName(file.Name, DmsProvider.DirectorySeparator) OrElse
                (file.ExtendedInfosCollisionDetected AndAlso String.IsNullOrEmpty(file.ExtendedInfosFileID)) OrElse
                Not identities.Add(If(String.IsNullOrEmpty(file.ExtendedInfosFileID), "path:" & file.FullName, "id:" & file.ExtendedInfosFileID)) OrElse Not names.Add(file.Name) Then Return False
            If If(DmsProvider.ParentDirectoryPath(file.FullName), "").Trim(DmsProvider.DirectorySeparator) <> If(path, "").Trim(DmsProvider.DirectorySeparator) Then changes = True
        Next
        Return changes
    End Function

    Private Function HasMatchingRemoteInstance(context As RemoteDragContext) As Boolean
        If context.ValidationFailure IsNot Nothing Then Return False
        Try
            Return TryCast(DmsProvider, IDmsInstanceProvider)?.CurrentDmsInstance?.ID = context.Instance
        Catch ex As Exception
            'Native OLE feedback must reject the target without throwing across
            'the callback boundary. Display the original failure after OLE returns.
            context.ValidationFailure = ex
            Return False
        End Try
    End Function

    Private Sub DragRemoteMoveOver(sender As Object, e As DragEventArgs)
        Dim context = RemoteMoveContext(e.Data)
        If context Is Nothing Then Return
        Dim node = UploadDropNode(DirectCast(sender, Control), New Drawing.Point(e.X, e.Y))
        context.HoverTarget = If(sender Is TreeViewDmsFolders AndAlso (e.AllowedEffect And DragDropEffects.Move) <> 0 AndAlso CanMoveTo(context, node), node, Nothing)
        e.Effect = If(context.HoverTarget Is Nothing, DragDropEffects.None, DragDropEffects.Move)
    End Sub

    Private Sub DropRemoteMove(sender As Object, e As DragEventArgs)
        Dim context = RemoteMoveContext(e.Data)
        If context IsNot Nothing Then e.Effect = DragDropEffects.None
        If context Is Nothing OrElse sender IsNot TreeViewDmsFolders OrElse (e.AllowedEffect And DragDropEffects.Move) = 0 Then Return
        Dim node = UploadDropNode(DirectCast(sender, Control), New Drawing.Point(e.X, e.Y))
        If Not CanMoveTo(context, node) Then Return
        context.MoveTarget = node
        context.MovePath = UploadDestination(node)
        Dim identity = TryCast(node.Tag, NodeTagData)?.DmsResourceItem
        context.MoveIdentity = If(identity Is Nothing, Nothing, New DmsResourceItem With {.ItemType = identity.ItemType, .FullName = identity.FullName,
            .ExtendedInfosFolderID = identity.ExtendedInfosFolderID, .ExtendedInfosCollectionID = identity.ExtendedInfosCollectionID})
        context.HoverTarget = node
        e.Effect = DragDropEffects.Move
    End Sub

    Private Async Function MoveDroppedFilesAsync(context As RemoteDragContext) As Task
        Dim ownsAction As Boolean
        Try
            If Not CanMoveTo(context, context.MoveTarget) Then
                If context.ValidationFailure IsNot Nothing Then Throw context.ValidationFailure
                Return
            End If
            ResourceActionRunning = True
            ownsAction = True
            Dim canExecute As Func(Of Boolean) = Function()
                                                    Dim allowed = CanMoveTo(context, context.MoveTarget, True)
                                                    If context.ValidationFailure IsNot Nothing Then Throw context.ValidationFailure
                                                    If Not allowed OrElse UploadDestination(context.MoveTarget) <> context.MovePath Then Return False
                                                    Dim current = TryCast(context.MoveTarget.Tag, NodeTagData)?.DmsResourceItem
                                                    Return (current Is Nothing AndAlso context.MoveIdentity Is Nothing) OrElse SameTreeResource(context.MoveIdentity, current)
                                                End Function
            Await RunOperationAndRefreshAsync(
                Async Function()
                    For Each file In context.Files
                        If Not canExecute() Then Exit For
                        Dim destination = DmsProvider.CombinePath(context.MovePath, file.Name)
                        If file.FullName.Trim(DmsProvider.DirectorySeparator) = destination.Trim(DmsProvider.DirectorySeparator) Then Continue For
                        If Not Await ExecuteResourceActionAsync(file, destination, ResourceAction.Move, canExecute) Then Exit For
                    Next
                End Function,
                Async Function()
                    Await RefreshCurrentFolderAndFilesAsync()
                End Function)
        Catch ex As Exception
            ShowResourceActionError(ex)
        Finally
            If ownsAction Then
                ResourceActionRunning = False
                UpdateFolderDownloadAccess()
            End If
        End Try
    End Function

    Friend Function DragDownloadSelection(origin As ListViewItem) As DmsResourceItem()
        If origin Is Nothing OrElse origin.ListView IsNot ListViewDmsFiles Then Return New DmsResourceItem() {}
        Dim originFile = TryCast(origin.Tag, DmsResourceItem)
        If originFile Is Nothing OrElse originFile.ItemType <> DmsResourceItem.ItemTypes.File Then Return New DmsResourceItem() {}
        'ItemDrag identifies the originating row. Its selection notification may
        'not yet be reflected in SelectedItems; never silently lose that drag or
        'substitute a previously selected remote file.
        If Not origin.Selected Then Return {originFile}
        Return CurrentSelectedFiles().Where(Function(file) file IsNot Nothing AndAlso file.ItemType = DmsResourceItem.ItemTypes.File).ToArray()
    End Function

    Private Sub SupplyDraggedFile(file As DmsResourceItem, path As String)
        If (AllowedActions And FileOrFolderActions.AllowDownloadFiles) = 0 Then Throw New UnauthorizedAccessException()
        If ActiveRemoteDrag Is Nothing OrElse DmsProvider IsNot ActiveRemoteDrag.Provider OrElse InitialFolder <> ActiveRemoteDrag.Scope OrElse
            TryCast(DmsProvider, IDmsInstanceProvider)?.CurrentDmsInstance?.ID <> ActiveRemoteDrag.Instance Then Throw New InvalidOperationException("The remote drag context changed before downloading.")
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
                If Not (TypeOf ex Is OperationCanceledException AndAlso dialog.CancellationToken.IsCancellationRequested) Then Me.HandleBrowserFailure(ex, showDialog:=False)
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

        Private ReadOnly AllowMove As Func(Of Boolean)
        Private LastEffect As Integer
        Friend Sub New()
            Me.New(Nothing)
        End Sub
        Friend Sub New(allowMove As Func(Of Boolean))
            Me.AllowMove = allowMove
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
            If (keyState And 1) = 0 AndAlso LastEffect = 2 AndAlso (AllowMove Is Nothing OrElse Not AllowMove()) Then Return &H40101
            Return If((keyState And 1) = 0, &H40100, 0) 'Drop after the left button is released.
        End Function
        Private Function GiveFeedback(instance As IntPtr, effect As Integer) As Integer
            LastEffect = effect
            If effect = 2 AndAlso (AllowMove Is Nothing OrElse Not AllowMove()) Then
                Cursor.Current = Cursors.No
                Return 0
            End If
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
