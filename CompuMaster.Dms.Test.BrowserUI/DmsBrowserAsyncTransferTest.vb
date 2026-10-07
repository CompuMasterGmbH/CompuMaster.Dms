Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Apartment(ApartmentState.STA)>
Public Class DmsBrowserAsyncTransferTest

    <TestCase(0)>
    <TestCase(1)>
    <TestCase(2)>
    Public Sub DelayedAsyncDownloadRestoresBusyStateAfterCompletionFailureOrCancellation(outcome As Integer)
        Dim provider As New DelayedDownloadProvider()
        Dim remoteFile As New DmsResourceItem With {
            .ItemType = DmsResourceItem.ItemTypes.File,
            .Name = "report.txt",
            .FullName = "folder/report.txt"
        }

        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Dim transfer As Task = browser.RunTransferAsync(Function() Global.CompuMaster.Dms.BrowserUI.DmsBrowser.DownloadFileForUiAsync(provider, remoteFile, "destination.txt"))

            ClassicAssert.IsFalse(transfer.IsCompleted)
            ClassicAssert.IsFalse(browser.Enabled)
            ClassicAssert.IsTrue(browser.UseWaitCursor)
            ClassicAssert.AreEqual(remoteFile.FullName, provider.RequestedPath)
            ClassicAssert.AreEqual(0, provider.SynchronousDownloadCount)

            Dim duplicate As Task = browser.RunTransferAsync(Function() Task.CompletedTask)
            Assert.Throws(Of InvalidOperationException)(Sub() duplicate.GetAwaiter().GetResult())

            Select Case outcome
                Case 0
                    provider.Complete()
                Case 1
                    provider.CompleteWithFailure()
                Case 2
                    provider.CompleteWithCancellation()
            End Select
            PumpMessagesUntilComplete(transfer)

            Select Case outcome
                Case 0
                    Assert.DoesNotThrow(Sub() transfer.GetAwaiter().GetResult())
                Case 1
                    Assert.Throws(Of InvalidOperationException)(Sub() transfer.GetAwaiter().GetResult())
                Case 2
                    Assert.Throws(Of TaskCanceledException)(Sub() transfer.GetAwaiter().GetResult())
            End Select
            ClassicAssert.IsTrue(browser.Enabled)
            ClassicAssert.IsFalse(browser.UseWaitCursor)
        End Using
    End Sub

    <TestCase(True)>
    <TestCase(False)>
    Public Sub UploadBatchAwaitsEachAsyncFileBeforeStartingTheNext(nativeAsync As Boolean)
        Dim provider As New DelayedUploadProvider(nativeAsync)
        Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(provider)
            Dim transfer As Task = browser.RunTransferAsync(Function() Global.CompuMaster.Dms.BrowserUI.DmsBrowser.UploadFilesForUiAsync(provider, "folder", {"first.txt", "second.txt"}))

            ClassicAssert.IsFalse(transfer.IsCompleted)
            ClassicAssert.AreEqual(1, provider.RequestedPaths.Count)
            ClassicAssert.IsFalse(browser.Enabled)

            provider.CompleteFirst()
            PumpMessagesUntil(Function() provider.RequestedPaths.Count = 2)
            ClassicAssert.IsFalse(transfer.IsCompleted)
            ClassicAssert.IsFalse(browser.Enabled)

            provider.CompleteSecond()
            PumpMessagesUntilComplete(transfer)
            Assert.DoesNotThrow(Sub() transfer.GetAwaiter().GetResult())
            ClassicAssert.IsTrue(browser.Enabled)
            ClassicAssert.IsFalse(browser.UseWaitCursor)
            ClassicAssert.AreEqual(0, provider.SynchronousUploadCount)
            StringAssert.EndsWith("first.txt", provider.RequestedPaths(0))
            StringAssert.EndsWith("second.txt", provider.RequestedPaths(1))
        End Using
    End Sub

    Private Shared Sub PumpMessagesUntilComplete(operation As Task)
        PumpMessagesUntil(Function() operation.IsCompleted)
    End Sub

    Private Shared Sub PumpMessagesUntil(condition As Func(Of Boolean))
        Dim deadline As DateTime = DateTime.UtcNow.AddSeconds(5)
        While Not condition() AndAlso DateTime.UtcNow < deadline
            Application.DoEvents()
            Thread.Sleep(1)
        End While
        ClassicAssert.IsTrue(condition(), "The pending UI continuation did not complete.")
    End Sub

    Private Class DelayedDownloadProvider
        Inherits NoDmsProvider

        Private ReadOnly completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)

        Public Property RequestedPath As String
        Public Property SynchronousDownloadCount As Integer

        Public Overrides ReadOnly Property SupportsAsynchronousIo As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides Function DownloadFileAsync(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?, Optional cancellationToken As CancellationToken = Nothing) As Task
            Me.RequestedPath = remoteFilePath
            Return completion.Task
        End Function

        Public Overrides Sub DownloadFile(remoteFile As DmsResourceItem, localFilePath As String)
            Me.SynchronousDownloadCount += 1
        End Sub

        Public Sub Complete()
            completion.SetResult(True)
        End Sub

        Public Sub CompleteWithFailure()
            completion.SetException(New InvalidOperationException("Transfer failed."))
        End Sub

        Public Sub CompleteWithCancellation()
            completion.SetCanceled()
        End Sub
    End Class

    Private Class DelayedUploadProvider
        Inherits NoDmsProvider

        Private ReadOnly nativeAsync As Boolean
        Private ReadOnly firstCompletion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Private ReadOnly secondCompletion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)

        Public ReadOnly Property RequestedPaths As New List(Of String)
        Public Property SynchronousUploadCount As Integer

        Public Sub New(nativeAsync As Boolean)
            Me.nativeAsync = nativeAsync
        End Sub

        Public Overrides ReadOnly Property SupportsAsynchronousIo As Boolean
            Get
                Return nativeAsync
            End Get
        End Property

        Public Overrides Function UploadFileAsync(remoteFilePath As String, localFilePath As String, Optional cancellationToken As CancellationToken = Nothing) As Task
            RequestedPaths.Add(remoteFilePath)
            Return If(RequestedPaths.Count = 1, firstCompletion.Task, secondCompletion.Task)
        End Function

        Public Overrides Sub UploadFile(remoteFilePath As String, localFilePath As String)
            SynchronousUploadCount += 1
        End Sub

        Public Sub CompleteFirst()
            firstCompletion.SetResult(True)
        End Sub

        Public Sub CompleteSecond()
            secondCompletion.SetResult(True)
        End Sub
    End Class

End Class
