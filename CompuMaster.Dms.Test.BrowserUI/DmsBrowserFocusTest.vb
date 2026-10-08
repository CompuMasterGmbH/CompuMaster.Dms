Option Explicit On
Option Strict On

Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports System.Runtime.InteropServices
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA)>
Public Class DmsBrowserFocusTest
    <TestCase("tree", 0), TestCase("tree", 1), TestCase("tree", 2)>
    <TestCase("files", 0), TestCase("files", 1), TestCase("files", 2)>
    <TestCase("dialog", 0), TestCase("dialog", 1), TestCase("dialog", 2)>
    Public Sub BusyOperationRestoresTheFocusedDescendant(kind As String, outcome As Integer)
        RunInBrowser(Async Function(browser)
                         Dim target As Control = If(kind = "files", DirectCast(browser.ListViewDmsFiles, Control), browser.TreeViewDmsFolders)
                         Assert.That(target.Focus(), [Is].True)
                         Assert.That(UiAsyncOperation.CaptureFocusedControl(browser), [Is].SameAs(target), "Capture the actual focused descendant.")
                         Dim completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
                         Dim operation As Task = If(kind = "dialog", New UiAsyncOperation(browser).RunAsync(Function() completion.Task), browser.RunTransferAsync(Function() completion.Task))
                         Assert.That(browser.Enabled, [Is].True)
                         Assert.That(browser.UseWaitCursor, [Is].True)
                         Assert.That(browser.SplitContainer.Enabled, [Is].False)
                         If kind = "tree" Then
                             Dim node = browser.TreeViewDmsFolders.Nodes.Add("Selected folder")
                             browser.TreeViewDmsFolders.SelectedNode = node
                         End If
                         Await Task.Delay(25)
                         'Establish this case's active-owner precondition at I/O
                         'completion as well as at startup; unrelated desktop input
                         'during the delay belongs to the separate window-switch cases.
                         ActivateTestWindow(browser)
                         Select Case outcome
                             Case 0 : completion.SetResult(True)
                             Case 1 : completion.SetException(New InvalidOperationException("Expected failure"))
                             Case 2 : completion.SetCanceled()
                         End Select
                         Try
                             Await operation
                         Catch ex As InvalidOperationException When outcome = 1
                         Catch ex As OperationCanceledException When outcome = 2
                         End Try
                         Assert.That(target.Focused, [Is].True, $"Restore the focused descendant. ActiveForm={Form.ActiveForm?.GetType().Name}; contains={browser.ContainsFocus}; targetCanFocus={target.CanFocus}; targetVisible={target.Visible}; targetEnabled={target.Enabled}; targetContained={browser.Contains(target)}.")
                         Assert.That(browser.SplitContainer.Enabled, [Is].True)
                         Assert.That(browser.UseWaitCursor, [Is].False)
                         Assert.That(browser.TreeViewDmsFolders.HideSelection, [Is].False)
                         If kind = "tree" Then
                             Dim selected = browser.TreeViewDmsFolders.SelectedNode
                             browser.ButtonCreateNewFolder.Focus()
                             Assert.That(browser.TreeViewDmsFolders.SelectedNode, [Is].SameAs(selected))
                             Assert.That(browser.TreeViewDmsFolders.HideSelection, [Is].False, "Retain native selection highlighting when focus intentionally moves away.")
                         End If
                     End Function)
    End Sub

    <Test>
    Public Sub MissingPreviousFocusFallsBackToClose()
        RunInBrowser(Async Function(browser)
                         browser.ActiveControl = Nothing
                         browser.Focus()
                         Assert.That(UiAsyncOperation.CaptureFocusedControl(browser), [Is].Null)
                         Await browser.RunTransferAsync(Async Function()
                                                            Await Task.Delay(25)
                                                            ActivateTestWindow(browser)
                                                        End Function)
                         Assert.That(browser.ButtonClose.Focused, [Is].True)
                     End Function)
    End Sub

    <TestCase("removed"), TestCase("hidden"), TestCase("disabled"), TestCase("disposed")>
    Public Sub UnavailableOriginalControlDoesNotReceiveFocus(state As String)
        RunInBrowser(Async Function(browser)
                         Using target As New TextBox
                             browser.SplitContainer.Panel1.Controls.Add(target)
                             target.BringToFront()
                             Assert.That(target.Focus(), [Is].True)
                             Dim completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
                             Dim operation = browser.RunTransferAsync(Function() completion.Task)
                             Select Case state
                                 Case "removed" : browser.SplitContainer.Panel1.Controls.Remove(target)
                                 Case "hidden" : target.Visible = False
                                 Case "disabled" : target.Enabled = False
                                 Case "disposed" : target.Dispose()
                             End Select
                             completion.SetResult(True)
                             Await operation
                             Assert.That(target.Focused, [Is].False)
                             Assert.That(browser.SplitContainer.Enabled, [Is].True)
                         End Using
                     End Function)
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub AnotherActiveWindowKeepsItsFocus(modal As Boolean)
        RunInBrowser(Async Function(browser)
                         Assert.That(browser.TreeViewDmsFolders.Focus(), [Is].True)
                         Dim completion As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
                         Dim operation = browser.RunTransferAsync(Function() completion.Task)
                         Using other As New Form With {.ShowInTaskbar = False, .Opacity = 0},
                               input As New TextBox,
                               timer As New System.Windows.Forms.Timer With {.Interval = 25}
                             other.Controls.Add(input)
                             If modal Then
                                 Dim checked As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
                                 AddHandler timer.Tick,
                                     Async Sub()
                                         timer.Stop()
                                         Try
                                             Assert.That(input.Focus(), [Is].True)
                                             completion.SetResult(True)
                                             Await operation
                                             Assert.That(input.Focused, [Is].True)
                                             Assert.That(browser.TreeViewDmsFolders.Focused, [Is].False)
                                             checked.SetResult(True)
                                         Catch ex As Exception
                                             checked.SetException(ex)
                                         Finally
                                             other.Close()
                                         End Try
                                     End Sub
                                 timer.Start()
                                 other.ShowDialog(browser)
                                 Await checked.Task
                             Else
                                 other.Show()
                                 ActivateTestWindow(other)
                                 Assert.That(input.Focus(), [Is].True)
                                 Await Task.Delay(25)
                                 completion.SetResult(True)
                                 Await operation
                                 Assert.That(input.Focused, [Is].True)
                                 Assert.That(browser.TreeViewDmsFolders.Focused, [Is].False)
                                 other.Close()
                             End If
                         End Using
                     End Function)
    End Sub

    Private Shared Sub RunInBrowser(testBody As Func(Of FocusBrowser, Task))
        Dim previousContext = SynchronizationContext.Current
        Dim previousAutoInstall = WindowsFormsSynchronizationContext.AutoInstall
        SynchronizationContext.SetSynchronizationContext(Nothing)
        WindowsFormsSynchronizationContext.AutoInstall = True
        Try
            Using browser As New FocusBrowser With {.ShowInTaskbar = False, .Opacity = 0},
                  timer As New System.Windows.Forms.Timer With {.Interval = 25}
                Dim operation As Task = Nothing
                Dim started As Boolean
                Dim deadline = DateTime.UtcNow.AddSeconds(5)
                AddHandler timer.Tick,
                    Sub()
                        If Not started Then
                            started = True
                            Try
                                ActivateTestWindow(browser)
                                operation = testBody(browser)
                            Catch ex As Exception
                                operation = Task.FromException(ex)
                            End Try
                        ElseIf operation IsNot Nothing AndAlso operation.IsCompleted Then
                            browser.Close()
                        ElseIf DateTime.UtcNow >= deadline Then
                            browser.Dispose()
                        End If
                    End Sub
                timer.Start()
                browser.ShowDialog()
                timer.Stop()
                Assert.That(operation, [Is].Not.Null)
                Assert.That(operation.IsCompleted, [Is].True, "Focus checks must complete on the real modal WinForms message loop.")
                operation.GetAwaiter().GetResult()
            End Using
        Finally
            SynchronizationContext.SetSynchronizationContext(previousContext)
            WindowsFormsSynchronizationContext.AutoInstall = previousAutoInstall
        End Try
    End Sub

    Private Class FocusBrowser
        Inherits DmsBrowser
        Public Sub New()
            MyBase.New(New NoDmsProvider())
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
            'Focus regression tests own the operation and never contact a remote server.
        End Sub
    End Class

    'The focus fixture deliberately owns foreground input after the development
    'notice. Process-launch foreground restrictions otherwise leave Form.ActiveForm
    'unset and test the production "user switched away" branch accidentally.
    'Attach only during activation and always detach; production never uses this.
    Private Shared Sub ActivateTestWindow(window As Form)
        Dim foreground = GetForegroundWindow()
        Dim foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero)
        Dim currentThread = GetCurrentThreadId()
        Dim attached As Boolean
        Try
            If foregroundThread <> 0 AndAlso foregroundThread <> currentThread Then
                attached = AttachThreadInput(currentThread, foregroundThread, True)
                Assert.That(attached, [Is].True, "The GUI focus fixture must acquire its foreground input context.")
            End If
            SetForegroundWindow(window.Handle)
            window.Activate()
            Application.DoEvents()
            Assert.That(GetForegroundWindow(), [Is].EqualTo(window.Handle), "Focus tests must start in the actual foreground window.")
            Assert.That(Form.ActiveForm, [Is].SameAs(window))
        Finally
            If attached Then AttachThreadInput(currentThread, foregroundThread, False)
        End Try
    End Sub

    <DllImport("user32.dll")>
    Private Shared Function GetForegroundWindow() As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetWindowThreadProcessId(window As IntPtr, processId As IntPtr) As UInteger
    End Function
    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As UInteger
    End Function
    <DllImport("user32.dll")>
    Private Shared Function AttachThreadInput(first As UInteger, second As UInteger, attach As Boolean) As Boolean
    End Function
    <DllImport("user32.dll")>
    Private Shared Function SetForegroundWindow(window As IntPtr) As Boolean
    End Function
End Class
