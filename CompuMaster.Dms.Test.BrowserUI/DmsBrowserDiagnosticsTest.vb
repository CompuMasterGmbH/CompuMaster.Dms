Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, NonParallelizable, Apartment(ApartmentState.STA)>
Public Class DmsBrowserDiagnosticsTest
    <TestCase(DmsBrowserFailurePhase.ScopedRootLookup, False)>
    <TestCase(DmsBrowserFailurePhase.ScopedRootLookup, True)>
    <TestCase(DmsBrowserFailurePhase.ChildListing, False)>
    <TestCase(DmsBrowserFailurePhase.ChildListing, True)>
    Public Sub StartupReportsOriginalFailureAndClosesWithOrWithoutSubscriber(phase As DmsBrowserFailurePhase, subscribe As Boolean)
        Dim original = CreateFailure()
        Dim provider As New FailureProvider With {.Failure = original, .FailurePhase = phase}
        Dim received As DmsBrowserFailureEventArgs = Nothing
        Dim reports As Integer
        Using dispatcher As New UiTestDispatcher, browser As New CapturingBrowser(provider)
            If subscribe Then AddHandler browser.OperationFailed, Sub(sender, args)
                                                                      reports += 1
                                                                      received = args
                                                                  End Sub
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.DialogResult, [Is].EqualTo(DialogResult.Cancel))
            Assert.That(browser.UseWaitCursor, [Is].False)
            Assert.That(browser.DialogCount, [Is].EqualTo(1))
            Assert.That(browser.LastMessage, Does.Not.Contain("secret"))
            Assert.That(browser.LastMessage, Does.Not.Contain("Bearer"))
            Assert.That(browser.LastMessage, Does.Not.Contain(NameOf(InvalidOperationException)))
            If subscribe Then
                Assert.That(reports, [Is].EqualTo(1))
                Assert.That(received.Exception, [Is].SameAs(original))
                Assert.That(received.Exception.InnerException, [Is].SameAs(original.InnerException))
                Assert.That(received.Exception.StackTrace, Does.Contain(NameOf(CreateFailure)))
                Assert.That(received.Phase, [Is].EqualTo(phase))
                Assert.That(received.Operation, Does.Not.Contain("secret"))
            End If
        End Using
    End Sub

    <Test>
    Public Sub SubscriberCanSuppressTheDialogWithoutPreventingStartupCleanup()
        Using dispatcher As New UiTestDispatcher, browser As New CapturingBrowser(New FailureProvider With {.Failure = CreateFailure()})
            AddHandler browser.OperationFailed, Sub(sender, args) args.SuppressDialog = True
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(browser.DialogCount, [Is].Zero)
            Assert.That(browser.DialogResult, [Is].EqualTo(DialogResult.Cancel))
            Assert.That(browser.UseWaitCursor, [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub SubscriberFailureDoesNotHideTheOriginalOrPreventOtherSubscribers()
        Dim original = CreateFailure()
        Dim received As Exception = Nothing
        Using dispatcher As New UiTestDispatcher, browser As New CapturingBrowser(New FailureProvider With {.Failure = original})
            AddHandler browser.OperationFailed, Sub(sender, args)
                                                    Throw New InvalidOperationException("subscriber secret")
                                                End Sub
            AddHandler browser.OperationFailed, Sub(sender, args) received = args.Exception
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(received, [Is].SameAs(original))
            Assert.That(browser.DialogCount, [Is].EqualTo(1))
            Assert.That(browser.DialogResult, [Is].EqualTo(DialogResult.Cancel))
        End Using
    End Sub

    <TestCase(DmsBrowserFailurePhase.Refresh)>
    <TestCase(DmsBrowserFailurePhase.Action)>
    <TestCase(DmsBrowserFailurePhase.Authentication)>
    <TestCase(DmsBrowserFailurePhase.MetadataEnrichment)>
    Public Sub TaskFailuresPreserveTheirContextAndOriginalPropagation(phase As DmsBrowserFailurePhase)
        Dim original = CreateFailure()
        Dim received As DmsBrowserFailureEventArgs = Nothing
        Using dispatcher As New UiTestDispatcher, browser As New CapturingBrowser(New FailureProvider)
            AddHandler browser.OperationFailed, Sub(sender, args) received = args
            Dim caught = Assert.Throws(Of InvalidOperationException)(Sub() dispatcher.Finish(browser.RunTransferAsync(Function() Task.FromException(original), phase)))
            Assert.That(caught, [Is].SameAs(original))
            browser.HandleBrowserFailure(caught)
            Assert.That(received.Exception, [Is].SameAs(original))
            Assert.That(received.Phase, [Is].EqualTo(phase))
            Assert.That(browser.UseWaitCursor, [Is].False)
        End Using
    End Sub

    <Test>
    Public Sub RefreshEventReportsOnceAndRestoresTheBrowserControls()
        Dim provider As New FailureProvider
        Dim received As DmsBrowserFailureEventArgs = Nothing
        Dim reports As Integer
        Using dispatcher As New UiTestDispatcher, browser As New CapturingBrowser(provider)
            browser.Show()
            dispatcher.WaitForEvents()
            AddHandler browser.OperationFailed, Sub(sender, args)
                                                    reports += 1
                                                    received = args
                                                End Sub
            provider.FileFailure = CreateFailure()
            browser.ToolStripButtonRefreshFilesList.PerformClick()
            dispatcher.WaitForEvents()
            Assert.That(reports, [Is].EqualTo(1))
            Assert.That(received.Exception, [Is].SameAs(provider.FileFailure))
            Assert.That(received.Phase, [Is].EqualTo(DmsBrowserFailurePhase.Refresh))
            Assert.That(browser.Enabled, [Is].True)
            Assert.That(browser.UseWaitCursor, [Is].False)
            browser.Close()
        End Using
    End Sub

    <Test>
    Public Sub InitialSelectionFailureRemainsObservableWithoutClosingTheBrowser()
        Dim provider As New FailureProvider With {.FileFailure = CreateFailure()}
        Dim received As DmsBrowserFailureEventArgs = Nothing
        Using dispatcher As New UiTestDispatcher, browser As New CapturingBrowser(provider, "Child")
            AddHandler browser.OperationFailed, Sub(sender, args) received = args
            browser.Show()
            dispatcher.WaitForEvents()
            Assert.That(received.Exception, [Is].SameAs(provider.FileFailure))
            Assert.That(received.Phase, [Is].EqualTo(DmsBrowserFailurePhase.InitialSelection))
            Assert.That(browser.DialogResult, [Is].Not.EqualTo(DialogResult.Cancel))
            Assert.That(browser.Visible, [Is].True)
            Assert.That(browser.UseWaitCursor, [Is].False)
            browser.Close()
        End Using
    End Sub

    <Test>
    Public Sub ApprovedValidationMessagesRemainUsefulButArbitraryUserExceptionTextIsHidden()
        Using browser As New CapturingBrowser(New FailureProvider)
            Dim safeMessage = UiStrings.GetText("InvalidDestinationName")
            browser.HandleBrowserFailure(New DmsUserInputInvalidException(safeMessage))
            Assert.That(browser.LastMessage, [Is].EqualTo(safeMessage))
            browser.HandleBrowserFailure(New DmsUserErrorMessageException("password=secret"))
            Assert.That(browser.LastMessage, Does.Not.Contain("secret"))
        End Using
    End Sub

    Private Shared Function CreateFailure() As Exception
        Try
            Throw New InvalidOperationException("password=secret; Bearer secret-token; https://user:secret@example.test/", New ArgumentException("inner secret"))
        Catch ex As Exception
            Return ex
        End Try
    End Function

    Private Class CapturingBrowser
        Inherits DmsBrowser
        Public DialogCount As Integer
        Public LastMessage As String
        Public Sub New(provider As BaseDmsProvider, Optional selectedFolder As String = Nothing)
            MyBase.New(provider, Nothing, "Diagnostics test", Nothing, "Parent", selectedFolder, BrowseModes.FoldersAndFiles, FileOrFolderActions.AllowUploadFiles, DialogOperationModes.NoResults, "", "", "")
            Me.ShowInTaskbar = False
            Me.Opacity = 0
        End Sub
        Protected Overrides Sub ShowFailureDialog(message As String, title As String, icon As MessageBoxIcon)
            DialogCount += 1
            LastMessage = message
        End Sub
    End Class

    Private Class FailureProvider
        Inherits NoDmsProvider
        Public Failure As Exception
        Public FileFailure As Exception
        Public FailurePhase As DmsBrowserFailurePhase = DmsBrowserFailurePhase.ScopedRootLookup
        Public Overrides Function ListRemoteItemAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of DmsResourceItem)
            If Failure IsNot Nothing AndAlso FailurePhase = DmsBrowserFailurePhase.ScopedRootLookup Then Return Task.FromException(Of DmsResourceItem)(Failure)
            Return Task.FromResult(New DmsResourceItem With {.FullName = path, .Name = path, .ItemType = DmsResourceItem.ItemTypes.Collection, .HasChildDirectories = True})
        End Function
        Public Overrides Function ListEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            If Failure IsNot Nothing Then Return Task.FromException(Of List(Of DmsResourceItem))(Failure)
            Return Task.FromResult(New List(Of DmsResourceItem) From {New DmsResourceItem With {.FullName = CombinePath(path, "Child"), .Name = "Child", .ItemType = DmsResourceItem.ItemTypes.Folder, .HasChildDirectories = False}})
        End Function
        Public Overrides Function ListDirectoryEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            Return ListEntriesAsync(path, cancellationToken)
        End Function
        Public Overrides Function ListFileEntriesAsync(path As String, Optional cancellationToken As CancellationToken = Nothing) As Task(Of List(Of DmsResourceItem))
            If FileFailure IsNot Nothing Then Return Task.FromException(Of List(Of DmsResourceItem))(FileFailure)
            Return Task.FromResult(New List(Of DmsResourceItem))
        End Function
        Public Overrides Function ResetCachesForRemoteItemsAsync(path As String, searchType As SearchItemType, Optional cancellationToken As CancellationToken = Nothing) As Task
            Return Task.CompletedTask
        End Function
    End Class
End Class
