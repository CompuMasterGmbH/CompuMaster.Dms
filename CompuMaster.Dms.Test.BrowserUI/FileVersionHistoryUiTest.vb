Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class FileVersionHistoryUiTest
    Private Shared ReadOnly AllVersionActions As DmsBrowser.FileOrFolderActions =
        DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory Or DmsBrowser.FileOrFolderActions.AllowDownloadFiles Or
        DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions Or DmsBrowser.FileOrFolderActions.AllowRemoveFileVersions

    <Test>
    Public Sub BrowserEntryRequiresSupportRightsSingleFileAndIdleState()
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider, browser As New PreviewBrowser(provider)
            browser.Show()
            browser.ToolbarActions = Global.CompuMaster.Dms.BrowserUI.BrowserToolbarActions.All
            browser.BrowseMode = DmsBrowser.BrowseModes.FoldersAndFiles
            browser.AllowedActions = AllVersionActions
            Assert.That(browser.VersionHistoryButton.Available, [Is].True)
            Assert.That(browser.VersionHistoryButton.Enabled, [Is].False)
            browser.ListViewDmsFiles.Items.Add(New ListViewItem("report") With {.Tag = provider.RemoteFile, .Selected = True})
            Assert.That(browser.CanViewSelectedFileVersions(), [Is].True)
            browser.ListViewDmsFiles.Items.Add(New ListViewItem("second") With {.Tag = provider.RemoteFile, .Selected = True})
            Assert.That(browser.CanViewSelectedFileVersions(), [Is].False)
            browser.ListViewDmsFiles.Items(1).Selected = False
            provider.HistorySupported = False
            browser.AllowedActions = AllVersionActions
            Assert.That(browser.VersionHistoryButton.Available, [Is].False)
            Assert.That(browser.VersionHistoryContext.Available, [Is].False)
            Assert.Throws(Of InvalidOperationException)(Sub() browser.CreateFileVersionDialog())
            provider.HistorySupported = True
            browser.AllowedActions = DmsBrowser.FileOrFolderActions.AllowDownloadFiles
            Assert.That(browser.VersionHistoryButton.Available, [Is].False)
            browser.AllowedActions = AllVersionActions
            Dim pending As New System.Threading.Tasks.TaskCompletionSource(Of Boolean)
            Dim work = browser.RunTransferAsync(Function() pending.Task)
            Try
                Assert.That(browser.CanViewSelectedFileVersions(), [Is].False)
                Assert.That(browser.VersionHistoryContext.Enabled, [Is].False)
            Finally
                pending.SetResult(True)
                dispatcher.Finish(work)
            End Try
            Assert.That(browser.VersionHistoryButton.Enabled, [Is].True)
            browser.ToolbarActions = Global.CompuMaster.Dms.BrowserUI.BrowserToolbarActions.All
            browser.BrowseMode = DmsBrowser.BrowseModes.Folders
            Assert.That(browser.VersionHistoryButton.Available, [Is].False)
            Assert.That(browser.VersionHistoryContext.Available, [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub IdentityMetadataAndBrowserIconReachTheHistoryWindow()
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider, dialog = CreateDialog(provider)
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            Assert.That(dialog.Icon, [Is].SameAs(SystemIcons.Application))
            Assert.That(dialog.Text, [Is].EqualTo("Version history — test remote"))
            Assert.That(dialog.VersionsList.Items(0).Text, [Is].EqualTo("opaque/version-α"))
            Assert.That(dialog.VersionsList.Items(0).Tag, [Is].SameAs(provider.History(0)))
            Assert.That(dialog.VersionsList.Items(0).SubItems(2).Text, Does.Contain("KiB"))
            Assert.That(dialog.VersionsList.Items(0).SubItems(3).Text, [Is].EqualTo("Author — 作者"))
            Assert.That(dialog.VersionsList.Items(1).SubItems(1).Text, [Is].EqualTo(UiStrings.GetText("VersionUnknown")))
            Assert.That(dialog.VersionsList.Items(1).SubItems(2).Text, [Is].EqualTo(UiStrings.GetText("VersionUnknown")))
        End Using
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub ConfirmedMutationUsesExactSnapshotAndReloadsHistory(restore As Boolean)
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider, dialog = CreateDialog(provider)
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            Dim selected = provider.History(0)
            dialog.VersionsList.Items(0).Selected = True
            dialog.Confirm = Function(question) True
            dispatcher.Finish(If(restore, dialog.RestoreSelectedAsync(), dialog.RemoveSelectedAsync()))
            Assert.That(provider.LastVersion, [Is].SameAs(selected))
            Assert.That(provider.RestoreCalls, [Is].EqualTo(If(restore, 1, 0)))
            Assert.That(provider.RemoveCalls, [Is].EqualTo(If(restore, 0, 1)))
            Assert.That(provider.ListCalls, [Is].EqualTo(2))
            Assert.That(dialog.CurrentFileChanged, [Is].True)
        End Using
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub RejectionOrPermissionRevocationDuringConfirmationPreventsMutation(revoke As Boolean)
        Dim permissions = AllVersionActions
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider,
              dialog = CreateDialog(provider, Function() permissions)
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            dialog.VersionsList.Items(0).Selected = True
            dialog.Confirm = Function(question)
                                 If revoke Then permissions = DmsBrowser.FileOrFolderActions.AllowViewFileVersionHistory
                                 Return revoke
                             End Function
            If revoke Then
                Assert.Throws(Of InvalidOperationException)(Sub() dispatcher.Finish(dialog.RestoreSelectedAsync()))
            Else
                dispatcher.Finish(dialog.RestoreSelectedAsync())
            End If
            Assert.That(provider.RestoreCalls, [Is].Zero)
            Assert.That(provider.RemoveCalls, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub ForeignOrUnselectedSnapshotIsNeverInvoked()
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider, dialog = CreateDialog(provider)
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            dialog.VersionsList.Items(0).Selected = True
            dialog.VersionsList.Items(0).Tag = New DmsFileVersion(provider.RemoteFile, "opaque/version-α")
            Assert.Throws(Of InvalidOperationException)(Sub() dispatcher.Finish(dialog.RemoveSelectedAsync()))
            Assert.That(provider.RemoveCalls, [Is].Zero)
            dialog.VersionsList.Items(0).Selected = False
            Assert.Throws(Of InvalidOperationException)(Sub() dispatcher.Finish(dialog.RestoreSelectedAsync()))
            Assert.That(provider.RestoreCalls, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub PendingHistoryKeepsWindowEnabledBlocksCloseAndDiscardsRevokedResult()
        Dim current = True
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider,
              dialog = CreateDialog(provider, isCurrent:=Function() current)
            provider.BlockListing = True
            Try
                dialog.Show()
                dispatcher.PumpUntil(Function() provider.Started.IsSet)
                Assert.That(dialog.Enabled, [Is].True)
                Assert.That(dialog.UseWaitCursor, [Is].True)
                Assert.That(dialog.DownloadButton.Enabled, [Is].False)
                dialog.Close()
                Assert.That(dialog.IsDisposed, [Is].False)
                current = False
            Finally
                provider.Release.Set()
                dispatcher.WaitForEvents()
            End Try
            Assert.That(dialog.VersionsList.Items.Count, [Is].Zero)
            Assert.That(dialog.Status.Text, [Is].EqualTo(UiStrings.GetText("VersionActionUnavailable")))
            Assert.That(dialog.UseWaitCursor, [Is].False)
            Assert.That(dialog.CloseButton.Enabled, [Is].True)
        End Using
    End Sub

    <Test>
    Public Sub ProviderFailureIsReportedOnceAndDisplayedWithoutCallingItEmptyHistory()
        Dim reported As New List(Of Exception)
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider,
              dialog = CreateDialog(provider, report:=Sub(ex) reported.Add(ex))
            provider.ListFailure = New IOException("history access denied", New UnauthorizedAccessException("remote detail"))
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            Assert.That(reported.Count, [Is].EqualTo(1))
            Assert.That(reported(0), [Is].SameAs(provider.ListFailure))
            Assert.That(dialog.Status.Text, Does.Contain("history access denied"))
            Assert.That(dialog.Status.Text, [Is].Not.EqualTo(UiStrings.GetText("FileVersionHistoryEmpty")))
            Assert.That(dialog.UseWaitCursor, [Is].False)
            Assert.That(dialog.CloseButton.Enabled, [Is].True)
        End Using
    End Sub

    <Test>
    Public Sub HistoricalDownloadUsesExactSnapshotAndDoesNotChangeCurrentFile()
        Dim root = Path.Combine(Path.GetTempPath(), "cm-dms-history-ui-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root), [Is].False)
        Directory.CreateDirectory(root)
        Try
            Using dispatcher As New UiTestDispatcher, provider As New VersionProvider, dialog = CreateDialog(provider)
                dialog.Show()
                dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
                dispatcher.WaitForEvents()
                dialog.VersionsList.Items(0).Selected = True
                Dim destination = Path.Combine(root, "historical.txt")
                dispatcher.Finish(dialog.DownloadSelectedAsync(destination, False))
                Assert.That(provider.LastVersion, [Is].SameAs(provider.History(0)))
                Assert.That(File.ReadAllBytes(destination), [Is].EqualTo({CByte(42)}))
                Assert.That(dialog.CurrentFileChanged, [Is].False)
                Assert.That(provider.RestoreCalls + provider.RemoveCalls, [Is].Zero)
                Assert.That(provider.ListCalls, [Is].EqualTo(1))
            End Using
        Finally
            Directory.Delete(root, True)
        End Try
    End Sub

    <Test>
    Public Sub VersionUploadTimestampIsDisplayedButNeverWrittenAsFileModificationTime()
        Dim root = Path.Combine(Path.GetTempPath(), "cm-dms-history-date-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(root), [Is].False)
        Directory.CreateDirectory(root)
        Try
            Using dispatcher As New UiTestDispatcher, provider As New VersionProvider, dialog = CreateDialog(provider)
                Dim uploaded As New DateTime(2001, 1, 1, 12, 34, 56, DateTimeKind.Utc)
                provider.History.Clear()
                provider.History.Add(New DmsFileVersion(provider.RemoteFile, "opaque-id", Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, uploaded))
                dialog.Show()
                dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
                dispatcher.WaitForEvents()
                Assert.That(dialog.VersionsList.Items(0).SubItems(1).Text, Does.Contain("2001"))
                dialog.VersionsList.Items(0).Selected = True
                Dim destination = Path.Combine(root, "historical.txt")
                dispatcher.Finish(dialog.DownloadSelectedAsync(destination, False))
                Assert.That(File.GetLastWriteTimeUtc(destination).Year, [Is].Not.EqualTo(2001))
            End Using
        Finally
            Directory.Delete(root, True)
        End Try
    End Sub

    <Test>
    Public Sub RestoreSuccessStillRefreshesCurrentMetadataWhenHistoryRefreshFails()
        Dim reported As New List(Of Exception)
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider,
              dialog = CreateDialog(provider, report:=Sub(ex) reported.Add(ex))
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            dialog.VersionsList.Items(0).Selected = True
            dialog.Confirm = Function(question) True
            provider.ListFailure = New IOException("refresh interrupted")
            dispatcher.Finish(dialog.RunWithErrorAsync(Function() dialog.RestoreSelectedAsync()))
            Assert.That(provider.RestoreCalls, [Is].EqualTo(1))
            Assert.That(dialog.CurrentFileChanged, [Is].True)
            Assert.That(reported, Has.Count.EqualTo(1))
            Assert.That(reported(0), [Is].SameAs(provider.ListFailure))
        End Using
    End Sub

    <Test>
    Public Sub PermissionRevocationClearsHistoryAndPreventsDirectActions()
        Dim permissions = AllVersionActions
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider,
              dialog = CreateDialog(provider, Function() permissions)
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            dialog.VersionsList.Items(0).Selected = True
            permissions = DmsBrowser.FileOrFolderActions.AllowRestoreFileVersions
            dialog.UpdateAccess()
            Assert.That(dialog.VersionsList.Items.Count, [Is].Zero)
            Assert.That(dialog.DownloadButton.Visible, [Is].False)
            Assert.That(dialog.RestoreButton.Visible, [Is].False)
            Assert.That(dialog.RemoveButton.Visible, [Is].False)
            Assert.Throws(Of InvalidOperationException)(Sub() dispatcher.Finish(dialog.RestoreSelectedAsync()))
            Assert.That(provider.RestoreCalls, [Is].Zero)
        End Using
    End Sub

    <Test>
    Public Sub ResultForAnotherFileIsRejectedBeforeItCanBeSelected()
        Dim reported As New List(Of Exception)
        Using dispatcher As New UiTestDispatcher, provider As New VersionProvider,
              dialog = CreateDialog(provider, report:=Sub(ex) reported.Add(ex))
            provider.History.Clear()
            provider.History.Add(New DmsFileVersion(New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "other", .FullName = "/other", .ExtendedInfosFileID = "different-id"}, "1"))
            dialog.Show()
            dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
            dispatcher.WaitForEvents()
            Assert.That(dialog.VersionsList.Items.Count, [Is].Zero)
            Assert.That(reported, Has.Count.EqualTo(1))
            Assert.That(reported(0), [Is].InstanceOf(Of InvalidDataException)())
        End Using
    End Sub

    <TestCase("en-US"), TestCase("de-DE"), TestCase("fr-FR"), TestCase("es-ES"), TestCase("zh-CN"), TestCase("zh-TW"),
     TestCase("ja-JP"), TestCase("ar-SA"), TestCase("he-IL"), TestCase("hi-IN")>
    Public Sub LocalizedActionsFitAndDoNotOverlapAtMinimumWidth(cultureName As String)
        Dim previous = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Using dispatcher As New UiTestDispatcher, provider As New VersionProvider, dialog = CreateDialog(provider),
                  font As New Font(SystemFonts.MessageBoxFont.FontFamily, 14)
                dialog.Font = font
                dialog.Show()
                dispatcher.PumpUntil(Function() provider.ListCalls > 0 AndAlso Not dialog.UseWaitCursor)
                dispatcher.WaitForEvents()
                dialog.Size = dialog.MinimumSize
                dialog.PerformLayout()
                Application.DoEvents()
                Dim buttons = {dialog.DownloadButton, dialog.RestoreButton, dialog.RemoveButton, dialog.RefreshButton, dialog.CloseButton}
                For Each button In buttons
                    Assert.That(button.Width, [Is].GreaterThanOrEqualTo(button.GetPreferredSize(Size.Empty).Width), cultureName & " " & button.Text)
                    Assert.That(button.Parent.ClientRectangle.Contains(button.Bounds), [Is].True, cultureName & " " & button.Text)
                    Assert.That(dialog.ClientRectangle.Contains(dialog.RectangleToClient(button.RectangleToScreen(button.ClientRectangle))), [Is].True, cultureName & " form " & button.Text)
                    For Each other In buttons
                        If other IsNot button Then Assert.That(button.Bounds.IntersectsWith(other.Bounds), [Is].False, cultureName)
                    Next
                Next
                Assert.That(dialog.VersionsList.Height, [Is].GreaterThanOrEqualTo(80))
                Assert.That(dialog.ClientRectangle.Contains(dialog.RectangleToClient(dialog.VersionsList.RectangleToScreen(dialog.VersionsList.ClientRectangle))), [Is].True, cultureName & " history viewport")
                Assert.That(dialog.RightToLeftLayout, [Is].False)
                Dim output = Environment.GetEnvironmentVariable("DMS_VERSION_PREVIEW_DIR")
                If Not String.IsNullOrEmpty(output) Then
                    Directory.CreateDirectory(output)
                    Using bitmap As New Bitmap(dialog.Width, dialog.Height)
                        dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
                        bitmap.Save(Path.Combine(output, "versions-" & cultureName & ".png"))
                    End Using
                End If
            End Using
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
    End Sub

    Private Shared Function CreateDialog(provider As VersionProvider, Optional permissions As Func(Of DmsBrowser.FileOrFolderActions) = Nothing,
                                         Optional isCurrent As Func(Of Boolean) = Nothing, Optional report As Action(Of Exception) = Nothing) As FileVersionHistoryDialog
        Return New FileVersionHistoryDialog(provider, provider.RemoteFile, If(permissions, Function() AllVersionActions), If(isCurrent, Function() True),
                                            SystemIcons.Application, "Version history — test remote", Nothing, Nothing, If(report, Sub(ex) Assert.Fail(ex.ToString())))
    End Function

    Private Class PreviewBrowser
        Inherits DmsBrowser
        Friend Sub New(provider As BaseDmsProvider)
            MyBase.New(provider)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class

    Private Class VersionProvider
        Inherits NoDmsProvider
        Implements IDisposable
        Friend ReadOnly RemoteFile As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File, .Name = "Report — 文件.txt", .FullName = "/documents/Report — 文件.txt", .ExtendedInfosFileID = "stable-file-id"}
        Friend ReadOnly History As List(Of DmsFileVersion)
        Friend HistorySupported As Boolean = True
        Friend ListCalls As Integer
        Friend RestoreCalls As Integer
        Friend RemoveCalls As Integer
        Friend LastVersion As DmsFileVersion
        Friend ListFailure As Exception
        Friend BlockListing As Boolean
        Friend ReadOnly Started As New ManualResetEventSlim
        Friend ReadOnly Release As New ManualResetEventSlim

        Friend Sub New()
            History = New List(Of DmsFileVersion) From {
                New DmsFileVersion(RemoteFile, "opaque/version-α", New DateTime(2026, 10, 7, 12, 34, 56, DateTimeKind.Utc), 2048, "author-id", "Author — 作者", "Reviewed — geprüft"),
                New DmsFileVersion(RemoteFile, "older/β")}
        End Sub
        Public Overrides ReadOnly Property SupportsFileVersionHistory As Boolean
            Get
                Return HistorySupported
            End Get
        End Property
        Public Overrides ReadOnly Property SupportsFileVersionDownload As Boolean = True
        Public Overrides ReadOnly Property SupportsFileVersionRestore As Boolean = True
        Public Overrides ReadOnly Property SupportsFileVersionRemoval As Boolean = True
        Public Overrides Function ListFileVersions(remoteFile As DmsResourceItem) As List(Of DmsFileVersion)
            ListCalls += 1
            If BlockListing Then
                Started.Set()
                If Not Release.Wait(TimeSpan.FromSeconds(5)) Then Throw New TimeoutException("Test listing was not released.")
            End If
            If ListFailure IsNot Nothing Then Throw ListFailure
            Return History.ToList()
        End Function
        Public Overrides Sub RestoreFileVersion(version As DmsFileVersion)
            LastVersion = version
            RestoreCalls += 1
        End Sub
        Public Overrides Sub DownloadFileVersion(version As DmsFileVersion, destination As Stream)
            LastVersion = version
            destination.WriteByte(42)
        End Sub
        Public Overrides Sub RemoveFileVersion(version As DmsFileVersion)
            LastVersion = version
            RemoveCalls += 1
        End Sub
        Public Sub Dispose() Implements IDisposable.Dispose
            Release.Set()
            Started.Dispose()
            Release.Dispose()
        End Sub
    End Class
End Class
