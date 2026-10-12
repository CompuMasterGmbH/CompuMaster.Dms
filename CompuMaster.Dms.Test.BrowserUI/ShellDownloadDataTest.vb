Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Runtime.InteropServices.ComTypes
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class ShellDownloadDataTest
    <TestCase(0), TestCase(1), TestCase(2)>
    Public Sub ItemDragStartsForItsOriginAndPreservesAnExistingMultiSelection(selection As Integer)
        Using browser As New DmsBrowser(New NoDmsProvider())
            browser.BrowseMode = DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowDownloadFiles
            Dim list = browser.ListViewDmsFiles
            Dim handle = list.Handle
            Dim first = list.Items.Add(New ListViewItem("first") With {.Tag = Item("first-id", "first.txt")})
            Dim second = list.Items.Add(New ListViewItem("second") With {.Tag = Item("second-id", "second.txt")})
            first.Selected = selection = 2
            second.Selected = selection > 0
            Dim calls As Integer
            Dim count As Integer
            Dim caption As String = Nothing
            Dim blocked As Boolean
            browser.ShellDragOperation = Function(data, source)
                                             calls += 1
                                             blocked = Not browser.CanDragDownload()
                                             Dim format = ShellDownloadData.FileFormat(ShellDownloadData.DescriptorFormat, -1, TYMED.TYMED_HGLOBAL)
                                             Dim medium As New STGMEDIUM()
                                             data.GetData(format, medium)
                                             Dim memory = ShellDownloadData.GlobalLock(medium.unionmember)
                                             Try
                                                 count = Marshal.ReadInt32(memory)
                                                 caption = Text.Encoding.Unicode.GetString(ReadBytes(memory, 4 + 72, 520)).TrimEnd(ChrW(0))
                                             Finally
                                                 ShellDownloadData.GlobalUnlock(medium.unionmember)
                                                 ShellDownloadData.ReleaseStgMedium(medium)
                                             End Try
                                             Return &H40101
                                         End Function
            GetType(ListView).GetMethod("OnItemDrag", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).Invoke(list, {New ItemDragEventArgs(MouseButtons.Left, first)})
            Assert.That(calls, [Is].EqualTo(1))
            Assert.That(blocked, [Is].True)
            Assert.That(count, [Is].EqualTo(If(selection = 2, 2, 1)))
            Assert.That(caption, [Is].EqualTo("first.txt"))
            Assert.That(browser.CanDragDownload(), [Is].True)
        End Using
    End Sub

    Private Shared Function ReadBytes(pointer As IntPtr, offset As Integer, length As Integer) As Byte()
        Dim bytes(length - 1) As Byte
        Marshal.Copy(IntPtr.Add(pointer, offset), bytes, 0, length)
        Return bytes
    End Function

    <TestCase(1), TestCase(2)>
    Public Sub NativeShellFolderAcceptsAndCopiesVirtualFilesWithoutFetchingOnHover(count As Integer)
        Dim root = Path.Combine(Path.GetTempPath(), "dms-shell-target-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root), [Is].False)
        Directory.CreateDirectory(root)
        Dim shellItem As IntPtr
        Dim target As IntPtr
        Dim pointer As IntPtr
        Try
            Dim itemID As New Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(root, IntPtr.Zero, itemID, shellItem))
            Dim bind = DirectCast(Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(shellItem), 3 * IntPtr.Size), GetType(BindCallback)), BindCallback)
            Dim handlerID As New Guid("3981e225-f559-11d3-8e3a-00c04f6837d5")
            Dim targetID As New Guid("00000122-0000-0000-C000-000000000046")
            Marshal.ThrowExceptionForHR(bind(shellItem, IntPtr.Zero, handlerID, targetID, target))
            Dim calls As Integer
            Dim files = Enumerable.Range(1, count).Select(Function(index) Item("selected-" & index, "file-" & index & ".txt")).ToArray()
            Using data As New ShellDownloadData(files, Sub(item, path)
                                                          calls += 1
                                                          File.WriteAllText(path, item.ExtendedInfosFileID)
                                                      End Sub)
                pointer = Marshal.GetComInterfaceForObject(data, GetType(System.Runtime.InteropServices.ComTypes.IDataObject))
                Dim enter = DirectCast(Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(target), 3 * IntPtr.Size), GetType(DragEnterCallback)), DragEnterCallback)
                Dim effect = 1
                Marshal.ThrowExceptionForHR(enter(target, pointer, 1, 0L, effect))
                Assert.That(effect, [Is].EqualTo(1), "The Windows shell must offer Copy for the virtual files.")
                Assert.That(calls, [Is].Zero, "Hover negotiation must not fetch remote content.")
                Dim drop = DirectCast(Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(target), 6 * IntPtr.Size), GetType(DragEnterCallback)), DragEnterCallback)
                Marshal.ThrowExceptionForHR(drop(target, pointer, 0, 0L, effect))
                Assert.That(effect, [Is].EqualTo(1))
                Assert.That(calls, [Is].EqualTo(count))
                For Each selectedFile As DmsResourceItem In files
                    Assert.That(File.ReadAllText(Path.Combine(root, selectedFile.Name)), [Is].EqualTo(selectedFile.ExtendedInfosFileID))
                Next
            End Using
        Finally
            If pointer <> IntPtr.Zero Then Marshal.Release(pointer)
            If target <> IntPtr.Zero Then Marshal.Release(target)
            If shellItem <> IntPtr.Zero Then Marshal.Release(shellItem)
            Directory.Delete(root, True)
        End Try
    End Sub

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function SHCreateItemFromParsingName(path As String, context As IntPtr, ByRef iid As Guid, ByRef item As IntPtr) As Integer
    End Function
    <UnmanagedFunctionPointer(CallingConvention.StdCall)>
    Private Delegate Function BindCallback(instance As IntPtr, context As IntPtr, ByRef handler As Guid, ByRef iid As Guid, ByRef result As IntPtr) As Integer
    <UnmanagedFunctionPointer(CallingConvention.StdCall)>
    Private Delegate Function DragEnterCallback(instance As IntPtr, data As IntPtr, keys As Integer, point As Long, ByRef effect As Integer) As Integer
    <UnmanagedFunctionPointer(CallingConvention.StdCall)>
    Private Delegate Function DragLeaveCallback(instance As IntPtr) As Integer

    <Test>
    Public Sub DescriptorsDoNotDownloadAndDisambiguateCollidingUnicodeNames()
        Dim calls As Integer
        Using data As New ShellDownloadData({Item("one", "Résumé.txt"), Item("two", "Résumé.txt")}, Sub(item, path) calls += 1)
            Dim bytes = data.Descriptors()
            Assert.That(BitConverter.ToInt32(bytes, 0), [Is].EqualTo(2))
            Assert.That(bytes.Length, [Is].EqualTo(4 + 2 * 592))
            Assert.That(Text.Encoding.Unicode.GetString(bytes, 4 + 72, 520).TrimEnd(ChrW(0)), [Is].EqualTo("Résumé.txt"))
            Assert.That(Text.Encoding.Unicode.GetString(bytes, 4 + 592 + 72, 520).TrimEnd(ChrW(0)), [Is].EqualTo("Résumé (2).txt"))
            Assert.That(calls, [Is].Zero)
            Assert.That(Directory.Exists(data.StagingDirectory), [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub FileContentsUseTheSelectedIdentityAndAreCachedUntilCleanup()
        Dim calls As Integer
        Dim ids As New List(Of String)
        Dim root As String
        Dim selected = {Item("one", "same.txt"), Item("two", "same.txt")}
        Using data As New ShellDownloadData(selected, Sub(item, path)
                                                       calls += 1
                                                       ids.Add(item.ExtendedInfosFileID)
                                                       File.WriteAllBytes(path, New Byte() {1, 2, 3, 4})
                                                   End Sub)
            root = data.StagingDirectory
            Dim format = ShellDownloadData.FileFormat(ShellDownloadData.ContentsFormat, 1, TYMED.TYMED_ISTREAM)
            For attempt = 0 To 1
                Dim medium As New STGMEDIUM()
                data.GetData(format, medium)
                Try
                    Dim stream = DirectCast(Marshal.GetObjectForIUnknown(medium.unionmember), IStream)
                    Dim result(3) As Byte
                    stream.Read(result, 4, IntPtr.Zero)
                    Assert.That(result, [Is].EqualTo(New Byte() {1, 2, 3, 4}))
                    Dim stat As New System.Runtime.InteropServices.ComTypes.STATSTG()
                    stream.Stat(stat, 1)
                    Assert.That(stat.cbSize, [Is].EqualTo(4))
                Finally
                    ShellDownloadData.ReleaseStgMedium(medium)
                End Try
            Next
            Assert.That(calls, [Is].EqualTo(1))
            Assert.That(ids, [Is].EqualTo({"two"}))
        End Using
        Assert.That(Directory.Exists(root), [Is].False)
    End Sub

    <Test>
    Public Sub NativeComGetDataReturnsTheShellDescriptorMemory()
        Using data As New ShellDownloadData({Item("id", "file.txt")}, Sub(item, path) Assert.Fail("Metadata must not trigger download."))
            Dim pointer = Marshal.GetComInterfaceForObject(data, GetType(System.Runtime.InteropServices.ComTypes.IDataObject))
            Dim formatPointer = Marshal.AllocHGlobal(Marshal.SizeOf(GetType(FORMATETC)))
            Dim mediumPointer = Marshal.AllocHGlobal(Marshal.SizeOf(GetType(STGMEDIUM)))
            Try
                Dim format = ShellDownloadData.FileFormat(ShellDownloadData.DescriptorFormat, -1, TYMED.TYMED_HGLOBAL)
                Marshal.StructureToPtr(format, formatPointer, False)
                Dim vtable = Marshal.ReadIntPtr(pointer)
                Dim getData = DirectCast(Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size), GetType(GetDataCallback)), GetDataCallback)
                Assert.That(getData(pointer, formatPointer, mediumPointer), [Is].Zero)
                Dim medium = DirectCast(Marshal.PtrToStructure(mediumPointer, GetType(STGMEDIUM)), STGMEDIUM)
                Dim memory = ShellDownloadData.GlobalLock(medium.unionmember)
                Try
                    Assert.That(Marshal.ReadInt32(memory), [Is].EqualTo(1))
                Finally
                    ShellDownloadData.GlobalUnlock(medium.unionmember)
                    ShellDownloadData.ReleaseStgMedium(medium)
                End Try
            Finally
                Marshal.FreeHGlobal(formatPointer)
                Marshal.FreeHGlobal(mediumPointer)
                Marshal.Release(pointer)
            End Try
        End Using
    End Sub

    <Test>
    Public Sub DropSourceSupportsItsComInterfaceWithoutExposingAProviderSpecificPublicApi()
        Dim sourceType = GetType(DmsBrowser).GetNestedType("CopyDragSource", Reflection.BindingFlags.NonPublic)
        Using source = DirectCast(Activator.CreateInstance(sourceType, True), IDisposable)
            Dim pointer = DirectCast(sourceType.GetProperty("Pointer", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).GetValue(source), IntPtr)
            Assert.That(pointer, [Is].Not.EqualTo(IntPtr.Zero))
            Dim vtable = Marshal.ReadIntPtr(pointer)
            Dim callback = Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vtable, 3 * IntPtr.Size), sourceType.GetNestedType("ContinueCallback", Reflection.BindingFlags.NonPublic))
            Assert.That(callback.DynamicInvoke(pointer, False, 1), [Is].EqualTo(0))
            Assert.That(callback.DynamicInvoke(pointer, False, 0), [Is].EqualTo(&H40100))
            Assert.That(callback.DynamicInvoke(pointer, True, 1), [Is].EqualTo(&H40101))
        End Using
    End Sub

    <Test>
    Public Sub FailedContentRequestCleansPartialStagingAndNeverReturnsAStream()
        Dim root As String
        Using data As New ShellDownloadData({Item("id", "file")}, Sub(item, path)
                                                       File.WriteAllText(path, "partial")
                                                       Throw New IOException("Connection reset")
                                                   End Sub)
            root = data.StagingDirectory
            Dim format = ShellDownloadData.FileFormat(ShellDownloadData.ContentsFormat, 0, TYMED.TYMED_ISTREAM)
            Dim medium As New STGMEDIUM()
            Assert.Throws(Of IOException)(Sub() data.GetData(format, medium))
            Assert.That(medium.unionmember, [Is].EqualTo(IntPtr.Zero))
        End Using
        Assert.That(Directory.Exists(root), [Is].False)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub DownloadDragRespectsThePermissionFlagAndBrowseMode(allow As Boolean)
        Using browser As New DmsBrowser(New NoDmsProvider())
            browser.BrowseMode = DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = If(allow, DmsBrowser.FileOrFolderActions.AllowDownloadFiles, CType(0, DmsBrowser.FileOrFolderActions))
            Assert.That(browser.CanDragDownload(), [Is].EqualTo(allow))
            browser.BrowseMode = DmsBrowser.BrowseModes.Folders
            Assert.That(browser.CanDragDownload(), [Is].False)
        End Using
    End Sub

    Private Shared Function Item(id As String, name As String) As DmsResourceItem
        Return New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = name, .FullName = "directory/" & name, .ExtendedInfosFileID = id}
    End Function
    <UnmanagedFunctionPointer(CallingConvention.StdCall)>
    Private Delegate Function GetDataCallback(instance As IntPtr, format As IntPtr, medium As IntPtr) As Integer
End Class
