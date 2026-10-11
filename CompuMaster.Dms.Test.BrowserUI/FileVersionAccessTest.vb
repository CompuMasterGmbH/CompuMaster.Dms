Imports CompuMaster.Dms.BrowserUI
Imports NUnit.Framework

<TestFixture>
Public Class FileVersionAccessTest
    <Test>
    Public Sub EveryPermissionAndProviderCapabilityCombinationIsIndependent()
        Dim flags = {DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory,
                     DmsBrowser.FileOrFolderActions.AllowDownloadFiles,
                     DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions,
                     DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions}
        For permissions As Integer = 0 To 15
            Dim actions As DmsBrowser.FileOrFolderActions = DmsBrowser.FileOrFolderActions.AllowSelectOnly
            For bit As Integer = 0 To 3
                If (permissions And (1 << bit)) <> 0 Then actions = actions Or flags(bit)
            Next
            For capabilities As Integer = 0 To 15
                Dim access As New FileVersionAccess(actions, (capabilities And 1) <> 0, (capabilities And 2) <> 0,
                                                    (capabilities And 4) <> 0, (capabilities And 8) <> 0)
                Dim history = (permissions And 1) <> 0 AndAlso (capabilities And 1) <> 0
                Dim scope = $"permissions={permissions}, capabilities={capabilities}"
                Assert.That(access.CanViewHistory, [Is].EqualTo(history), scope)
                Assert.That(access.CanDownload, [Is].EqualTo(history AndAlso (permissions And 2) <> 0 AndAlso (capabilities And 2) <> 0), scope)
                Assert.That(access.CanRestore, [Is].EqualTo(history AndAlso (permissions And 4) <> 0 AndAlso (capabilities And 4) <> 0), scope)
                Assert.That(access.CanRemove, [Is].EqualTo(history AndAlso (permissions And 8) <> 0 AndAlso (capabilities And 8) <> 0), scope)
            Next
        Next
    End Sub

    <Test>
    Public Sub RestorationDoesNotRequireUploadOrDownloadRights()
        Dim access As New FileVersionAccess(DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory Or
                                           DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions, True, True, True, True)
        Assert.That(access.CanRestore, [Is].True)
        Assert.That(access.CanDownload, [Is].False)
        Assert.That(access.CanRemove, [Is].False)
    End Sub

    <Test>
    Public Sub OrdinaryFileDeletionDoesNotPermitDeletingHistory()
        Dim access As New FileVersionAccess(DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory Or
                                           DmsBrowser.FileOrFolderActions.AllowDeleteFiles, True, True, True, True)
        Assert.That(access.CanRemove, [Is].False)
        Dim historyRemoval As New FileVersionAccess(DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory Or
                                                   DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions, True, True, True, True)
        Assert.That(historyRemoval.CanRemove, [Is].True)
        Assert.That(CInt(DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions), [Is].EqualTo(1024))
    End Sub
End Class
