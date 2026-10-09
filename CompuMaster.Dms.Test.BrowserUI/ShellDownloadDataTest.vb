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
                    Dim stat As New STATSTG()
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
