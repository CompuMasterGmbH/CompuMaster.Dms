Option Explicit On
Option Strict On

Imports CompuMaster.Dms.BrowserUI
Imports NUnit.Framework

<TestFixture>
Public Class FileVersionPermissionTest

    <TestCase("AllowSelectOnly", 0), TestCase("AllowSwitchBrowseMode", 1), TestCase("AllowCreateFolders", 2),
     TestCase("AllowUploadFiles", 4), TestCase("AllowDownloadFiles", 8), TestCase("AllowDeleteFiles", 16),
     TestCase("AllowCopyRenameMoveFiles", 32), TestCase("AllowSharings", 64), TestCase("AllowSwitchDmsInstance", 128)>
    Public Sub ExistingActionValuesRemainCompatible(name As String, expected As Integer)
        Dim actual = CType([Enum].Parse(GetType(DmsBrowser.FileOrFolderActions), name), DmsBrowser.FileOrFolderActions)
        Assert.That(CInt(actual), [Is].EqualTo(expected))
    End Sub

    <Test>
    Public Sub ExistingFullPermissionMaskDoesNotOptIntoVersionActions()
        Dim existing = DmsBrowser.FileOrFolderActions.AllowSwitchBrowseMode Or
            DmsBrowser.FileOrFolderActions.AllowCreateFolders Or DmsBrowser.FileOrFolderActions.AllowUploadFiles Or
            DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or DmsBrowser.FileOrFolderActions.AllowDeleteFiles Or
            DmsBrowser.FileOrFolderActions.AllowCopyRenameMoveFiles Or DmsBrowser.FileOrFolderActions.AllowSharings Or
            DmsBrowser.FileOrFolderActions.AllowSwitchDmsInstance
        Assert.That(CInt(existing), [Is].EqualTo(255))
        Assert.That(existing And DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory, [Is].EqualTo(DmsBrowser.FileOrFolderActions.AllowSelectOnly))
        Assert.That(existing And DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions, [Is].EqualTo(DmsBrowser.FileOrFolderActions.AllowSelectOnly))
        Assert.That(existing And DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions, [Is].EqualTo(DmsBrowser.FileOrFolderActions.AllowSelectOnly))
    End Sub

    <Test>
    Public Sub VersionPermissionsAreIndependentAndDoNotGrantExistingActions()
        Dim history = DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory
        Dim restore = DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions
        Dim removal = DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions
        Assert.That(CInt(history), [Is].EqualTo(256))
        Assert.That(CInt(restore), [Is].EqualTo(512))
        Assert.That(CInt(removal), [Is].EqualTo(1024))
        Assert.That(history And restore, [Is].EqualTo(DmsBrowser.FileOrFolderActions.AllowSelectOnly))
        Assert.That(history And removal, [Is].EqualTo(DmsBrowser.FileOrFolderActions.AllowSelectOnly))
        Assert.That(restore And removal, [Is].EqualTo(DmsBrowser.FileOrFolderActions.AllowSelectOnly))
        Assert.That(CInt(history Or restore Or removal) And 255, [Is].Zero)
        Assert.That(GetType(DmsBrowser.FileOrFolderActions).IsDefined(GetType(FlagsAttribute), False), [Is].True)
    End Sub
End Class
