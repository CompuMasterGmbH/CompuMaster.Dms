Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.InteropServices.ComTypes
Imports System.Text
Imports CompuMaster.Dms.Data

'Windows shell virtual files: descriptors are cheap; contents are fetched only on demand.
<ComVisible(True), ClassInterface(ClassInterfaceType.None)>
Friend NotInheritable Class ShellDownloadData
    Implements System.Runtime.InteropServices.ComTypes.IDataObject, IDisposable

    Friend Shared ReadOnly DescriptorFormat As Short = ClipboardFormat("FileGroupDescriptorW")
    Friend Shared ReadOnly ContentsFormat As Short = ClipboardFormat("FileContents")
    Private Shared ReadOnly CopyFormat As Short = ClipboardFormat("Preferred DropEffect")
    Private ReadOnly Items As DmsResourceItem()
    Private ReadOnly Fetch As Action(Of DmsResourceItem, String)
    Private ReadOnly Root As String
    Private ReadOnly Files As New Dictionary(Of Integer, String)
    Private ReadOnly Failures As New Dictionary(Of Integer, Exception)
    Private ReadOnly Streams As New List(Of ShellFileStream)
    Private Disposed As Boolean

    Friend Sub New(items As DmsResourceItem(), fetch As Action(Of DmsResourceItem, String))
        Me.Items = CType(items.Clone(), DmsResourceItem())
        Me.Fetch = fetch
        Root = Path.Combine(Path.GetTempPath(), "dms-drag-" & Guid.NewGuid().ToString("N"))
        If Directory.Exists(Root) Then Throw New IOException("The drag staging directory already exists.")
    End Sub

    Friend ReadOnly Property StagingDirectory As String
        Get
            Return Root
        End Get
    End Property

    Friend Shared Function FileFormat(format As Short, index As Integer, medium As TYMED) As FORMATETC
        Return New FORMATETC With {.cfFormat = format, .dwAspect = DVASPECT.DVASPECT_CONTENT, .lindex = index, .tymed = medium}
    End Function

    Public Sub GetData(ByRef format As FORMATETC, ByRef medium As STGMEDIUM) Implements System.Runtime.InteropServices.ComTypes.IDataObject.GetData
        If Disposed Then Throw New ObjectDisposedException(NameOf(ShellDownloadData))
        Marshal.ThrowExceptionForHR(QueryGetData(format))
        medium = New STGMEDIUM()
        If format.cfFormat = DescriptorFormat Then
            medium.tymed = TYMED.TYMED_HGLOBAL
            medium.unionmember = Allocate(Descriptors())
        ElseIf format.cfFormat = CopyFormat Then
            medium.tymed = TYMED.TYMED_HGLOBAL
            medium.unionmember = Allocate(BitConverter.GetBytes(1))
        Else
            If format.lindex < 0 OrElse format.lindex >= Items.Length Then Throw New COMException("Invalid file index.", &H80040068)
            If Failures.ContainsKey(format.lindex) Then Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(Failures(format.lindex)).Throw()
            Dim path As String = Nothing
            If Not Files.TryGetValue(format.lindex, path) Then
                Directory.CreateDirectory(Root)
                path = System.IO.Path.Combine(Root, format.lindex.ToString(Globalization.CultureInfo.InvariantCulture) & ".download")
                Try
                    Fetch(Items(format.lindex), path)
                    If Not File.Exists(path) Then Throw New IOException("The selected download did not create a file.")
                Catch ex As Exception
                    Failures.Add(format.lindex, ex)
                    Throw
                End Try
                Files.Add(format.lindex, path)
            End If
            Dim stream As New ShellFileStream(path, Sub(value) Streams.Add(value))
            Streams.Add(stream)
            medium.tymed = TYMED.TYMED_ISTREAM
            medium.unionmember = Marshal.GetComInterfaceForObject(stream, GetType(IStream))
        End If
    End Sub

    Friend Function Descriptors() As Byte()
        Dim names As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Using memory As New MemoryStream(), writer As New BinaryWriter(memory, Encoding.Unicode)
            writer.Write(Items.Length)
            For Each item In Items
                Dim name = SafeName(item.Name)
                Dim original = name
                Dim number = 1
                While Not names.Add(name)
                    number += 1
                    Dim extension = Path.GetExtension(original)
                    Dim suffix = " (" & number.ToString(Globalization.CultureInfo.InvariantCulture) & ")" & extension
                    name = Path.GetFileNameWithoutExtension(original).Substring(0, Math.Min(Path.GetFileNameWithoutExtension(original).Length, Math.Max(1, 259 - suffix.Length))) & suffix
                End While
                writer.Write(&H80004004UI) 'Unicode, attributes, progress UI; size is determined from the actual stream.
                writer.Write(New Byte(31) {}) 'CLSID, SIZEL, POINTL.
                writer.Write(CUInt(FileAttributes.Normal))
                writer.Write(New Byte(31) {}) 'Timestamps and optional file size.
                Dim caption(519) As Byte
                Dim encoded = Encoding.Unicode.GetBytes(name)
                Array.Copy(encoded, caption, Math.Min(encoded.Length, 518))
                writer.Write(caption)
            Next
            Return memory.ToArray()
        End Using
    End Function

    Private Shared Function SafeName(name As String) As String
        name = If(name, "file")
        For Each character In Path.GetInvalidFileNameChars()
            name = name.Replace(character, "_"c)
        Next
        name = name.TrimEnd(" "c, "."c)
        If name.Length = 0 Then name = "file"
        Dim stem = Path.GetFileNameWithoutExtension(name).ToUpperInvariant()
        If {"CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"}.Contains(stem) Then name = "_" & name
        Return name.Substring(0, Math.Min(name.Length, 259))
    End Function

    Public Function QueryGetData(ByRef format As FORMATETC) As Integer Implements System.Runtime.InteropServices.ComTypes.IDataObject.QueryGetData
        If format.dwAspect <> DVASPECT.DVASPECT_CONTENT Then Return &H8004006B
        If (format.cfFormat = DescriptorFormat OrElse format.cfFormat = CopyFormat) AndAlso (format.tymed And TYMED.TYMED_HGLOBAL) <> 0 Then Return 0
        If format.cfFormat = ContentsFormat AndAlso (format.tymed And TYMED.TYMED_ISTREAM) <> 0 AndAlso format.lindex >= 0 AndAlso format.lindex < Items.Length Then Return 0
        Return &H80040064
    End Function
    Public Sub GetDataHere(ByRef format As FORMATETC, ByRef medium As STGMEDIUM) Implements System.Runtime.InteropServices.ComTypes.IDataObject.GetDataHere
        Throw New COMException("Caller-provided storage is unsupported.", &H80040064)
    End Sub
    Public Function GetCanonicalFormatEtc(ByRef input As FORMATETC, ByRef output As FORMATETC) As Integer Implements System.Runtime.InteropServices.ComTypes.IDataObject.GetCanonicalFormatEtc
        output = input
        output.ptd = IntPtr.Zero
        Return &H40130
    End Function
    Public Sub SetData(ByRef format As FORMATETC, ByRef medium As STGMEDIUM, release As Boolean) Implements System.Runtime.InteropServices.ComTypes.IDataObject.SetData
        'A target may report its performed effect; no source deletion is ever performed.
        If release Then ReleaseStgMedium(medium)
    End Sub
    Public Function EnumFormatEtc(direction As DATADIR) As IEnumFORMATETC Implements System.Runtime.InteropServices.ComTypes.IDataObject.EnumFormatEtc
        If direction <> DATADIR.DATADIR_GET Then Throw New COMException("Only get formats are available.", &H80004001)
        Dim formats As New List(Of FORMATETC) From {FileFormat(DescriptorFormat, -1, TYMED.TYMED_HGLOBAL), FileFormat(CopyFormat, -1, TYMED.TYMED_HGLOBAL)}
        For index = 0 To Items.Length - 1
            formats.Add(FileFormat(ContentsFormat, index, TYMED.TYMED_ISTREAM))
        Next
        Return New FormatEnumerator(formats.ToArray())
    End Function
    Public Function DAdvise(ByRef format As FORMATETC, flags As ADVF, sink As IAdviseSink, ByRef connection As Integer) As Integer Implements System.Runtime.InteropServices.ComTypes.IDataObject.DAdvise
        connection = 0
        Return &H80040003
    End Function
    Public Sub DUnadvise(connection As Integer) Implements System.Runtime.InteropServices.ComTypes.IDataObject.DUnadvise
        Throw New COMException("Advise is unsupported.", &H80040003)
    End Sub
    Public Function EnumDAdvise(ByRef enumerator As IEnumSTATDATA) As Integer Implements System.Runtime.InteropServices.ComTypes.IDataObject.EnumDAdvise
        enumerator = Nothing
        Return &H80040003
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If Disposed Then Return
        Disposed = True
        For Each stream In Streams
            stream.Dispose()
        Next
        If Directory.Exists(Root) Then Directory.Delete(Root, True)
    End Sub

    Private Shared Function ClipboardFormat(name As String) As Short
        Return BitConverter.ToInt16(BitConverter.GetBytes(RegisterClipboardFormat(name)), 0)
    End Function
    Private Shared Function Allocate(bytes As Byte()) As IntPtr
        Dim handle = GlobalAlloc(&H42UI, New UIntPtr(CUInt(bytes.Length)))
        If handle = IntPtr.Zero Then Throw New OutOfMemoryException()
        Dim pointer = GlobalLock(handle)
        If pointer = IntPtr.Zero Then
            GlobalFree(handle)
            Throw New OutOfMemoryException()
        End If
        Try
            Marshal.Copy(bytes, 0, pointer, bytes.Length)
        Finally
            GlobalUnlock(handle)
        End Try
        Return handle
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function RegisterClipboardFormat(name As String) As UInteger
    End Function
    <DllImport("kernel32.dll")>
    Private Shared Function GlobalAlloc(flags As UInteger, size As UIntPtr) As IntPtr
    End Function
    <DllImport("kernel32.dll")>
    Friend Shared Function GlobalLock(handle As IntPtr) As IntPtr
    End Function
    <DllImport("kernel32.dll")>
    Friend Shared Function GlobalUnlock(handle As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll")>
    Private Shared Function GlobalFree(handle As IntPtr) As IntPtr
    End Function
    <DllImport("ole32.dll")>
    Friend Shared Sub ReleaseStgMedium(ByRef medium As STGMEDIUM)
    End Sub

    <ComVisible(True), ClassInterface(ClassInterfaceType.None)>
    Private NotInheritable Class FormatEnumerator
        Implements IEnumFORMATETC
        Private ReadOnly Formats As FORMATETC()
        Private Position As Integer
        Friend Sub New(formats As FORMATETC())
            Me.Formats = formats
        End Sub
        Public Function [Next](count As Integer, output As FORMATETC(), fetched As Integer()) As Integer Implements IEnumFORMATETC.Next
            Dim copied As Integer
            While copied < count AndAlso Position < Formats.Length
                output(copied) = Formats(Position)
                Position += 1
                copied += 1
            End While
            If fetched IsNot Nothing Then fetched(0) = copied
            Return If(copied = count, 0, 1)
        End Function
        Public Function Skip(count As Integer) As Integer Implements IEnumFORMATETC.Skip
            Dim available = Formats.Length - Position
            Position += Math.Min(count, available)
            Return If(count <= available, 0, 1)
        End Function
        Public Function Reset() As Integer Implements IEnumFORMATETC.Reset
            Position = 0
            Return 0
        End Function
        Public Sub Clone(ByRef copy As IEnumFORMATETC) Implements IEnumFORMATETC.Clone
            copy = New FormatEnumerator(Formats) With {.Position = Position}
        End Sub
    End Class
End Class

<ComVisible(True), ClassInterface(ClassInterfaceType.None)>
Friend NotInheritable Class ShellFileStream
    Implements IStream, IDisposable
    Private ReadOnly Input As FileStream
    Private ReadOnly Register As Action(Of ShellFileStream)
    Friend Sub New(path As String, register As Action(Of ShellFileStream))
        Input = New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan)
        Me.Register = register
    End Sub
    Public Sub Read(buffer As Byte(), count As Integer, readCount As IntPtr) Implements IStream.Read
        Dim actual = Input.Read(buffer, 0, count)
        If readCount <> IntPtr.Zero Then Marshal.WriteInt32(readCount, actual)
    End Sub
    Public Sub Seek(offset As Long, origin As Integer, position As IntPtr) Implements IStream.Seek
        Dim actual = Input.Seek(offset, CType(origin, SeekOrigin))
        If position <> IntPtr.Zero Then Marshal.WriteInt64(position, actual)
    End Sub
    Public Sub Stat(ByRef info As System.Runtime.InteropServices.ComTypes.STATSTG, flags As Integer) Implements IStream.Stat
        info = New System.Runtime.InteropServices.ComTypes.STATSTG With {.type = 2, .cbSize = Input.Length, .grfMode = 0}
    End Sub
    Public Sub CopyTo(target As IStream, count As Long, readCount As IntPtr, writtenCount As IntPtr) Implements IStream.CopyTo
        Dim buffer(81919) As Byte
        Dim total As Long
        While total < count
            Dim actual = Input.Read(buffer, 0, CInt(Math.Min(buffer.Length, count - total)))
            If actual = 0 Then Exit While
            target.Write(buffer, actual, IntPtr.Zero)
            total += actual
        End While
        If readCount <> IntPtr.Zero Then Marshal.WriteInt64(readCount, total)
        If writtenCount <> IntPtr.Zero Then Marshal.WriteInt64(writtenCount, total)
    End Sub
    Public Sub Clone(ByRef copy As IStream) Implements IStream.Clone
        Dim stream As New ShellFileStream(Input.Name, Register)
        stream.Input.Position = Input.Position
        Register(stream)
        copy = stream
    End Sub
    Public Sub Write(buffer As Byte(), count As Integer, writtenCount As IntPtr) Implements IStream.Write
        Throw New COMException("Read-only stream.", &H80030005)
    End Sub
    Public Sub SetSize(size As Long) Implements IStream.SetSize
        Throw New COMException("Read-only stream.", &H80030005)
    End Sub
    Public Sub Commit(flags As Integer) Implements IStream.Commit
    End Sub
    Public Sub Revert() Implements IStream.Revert
        Throw New COMException("Read-only stream.", &H80030001)
    End Sub
    Public Sub LockRegion(offset As Long, count As Long, flags As Integer) Implements IStream.LockRegion
        Throw New COMException("Locking is unsupported.", &H80030001)
    End Sub
    Public Sub UnlockRegion(offset As Long, count As Long, flags As Integer) Implements IStream.UnlockRegion
        Throw New COMException("Locking is unsupported.", &H80030001)
    End Sub
    Public Sub Dispose() Implements IDisposable.Dispose
        Input.Dispose()
    End Sub
End Class
