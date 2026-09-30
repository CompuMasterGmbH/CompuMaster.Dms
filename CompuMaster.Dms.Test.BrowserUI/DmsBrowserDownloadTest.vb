Imports System.IO
Imports NUnit.Framework
Imports NUnit.Framework.Legacy
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

<TestFixture>
Public Class DmsBrowserDownloadTest

    ''' <summary>
    ''' Verifies that consecutive downloads of colliding file names use the selected resource IDs and return their distinct binary contents.
    ''' </summary>
    <Test>
    Public Sub DownloadUsesSelectedResourceContentWhenFileNamesCollide()
        Dim olderContent As Byte() = {1, 2, 3, 4, 5}
        Dim newerContent As Byte() = {10, 20, 30, 40, 50, 60, 70}
        Dim provider As New InMemoryCollisionDmsProvider(
            New Dictionary(Of String, Byte()) From {
                {"older-file-id", olderContent},
                {"newer-file-id", newerContent}
            })
        Dim olderFile As DmsResourceItem = CreateRemoteFile("older-file-id")
        Dim newerFile As DmsResourceItem = CreateRemoteFile("newer-file-id")

        Using newerDownload As New CompuMaster.IO.TemporaryFile(".bin"),
              olderDownload As New CompuMaster.IO.TemporaryFile(".bin")
            Global.CompuMaster.Dms.BrowserUI.DmsBrowser.DownloadFile(provider, newerFile, newerDownload.FilePath)
            Global.CompuMaster.Dms.BrowserUI.DmsBrowser.DownloadFile(provider, olderFile, olderDownload.FilePath)

            CollectionAssert.AreEqual(newerContent, File.ReadAllBytes(newerDownload.FilePath))
            CollectionAssert.AreEqual(olderContent, File.ReadAllBytes(olderDownload.FilePath))
            CollectionAssert.AreNotEqual(File.ReadAllBytes(newerDownload.FilePath), File.ReadAllBytes(olderDownload.FilePath))
        End Using
    End Sub

    Private Shared Function CreateRemoteFile(id As String) As DmsResourceItem
        Return New DmsResourceItem() With {
            .ItemType = DmsResourceItem.ItemTypes.File,
            .Name = "annual-report.xlsx",
            .FullName = "customer/annual-report.xlsx",
            .ExtendedInfosFileID = id,
            .ExtendedInfosCollisionDetected = True
        }
    End Function

    Private Class InMemoryCollisionDmsProvider
        Inherits NoDmsProvider

        Private ReadOnly FileContents As IReadOnlyDictionary(Of String, Byte())

        Public Sub New(fileContents As IReadOnlyDictionary(Of String, Byte()))
            Me.FileContents = fileContents
        End Sub

        Public Overrides Sub DownloadFile(remoteFile As DmsResourceItem, localFilePath As String)
            File.WriteAllBytes(localFilePath, Me.FileContents(remoteFile.ExtendedInfosFileID))
        End Sub
    End Class

End Class
