Option Explicit On
Option Strict On

Imports System.Drawing
Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class LocalizedLayoutAcceptanceTest
    Public Shared Iterator Function LayoutCases() As IEnumerable(Of TestCaseData)
        For Each culture In {"en-US", "de-DE", "fr-FR", "es-ES", "zh-CN", "zh-TW", "ja-JP", "ar-SA", "he-IL", "hi-IN"}
            For Each scale In {1.0F, 1.5F, 2.0F}
                Yield New TestCaseData(culture, scale)
            Next
        Next
    End Function

    <TestCaseSource(NameOf(LayoutCases))>
    Public Sub LocalizedControlsFitWithoutClippingOrOverlap(cultureName As String, fontScale As Single)
        Dim previous = CultureInfo.CurrentUICulture
        Dim failures As New List(Of String)
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Dim input As TextBox = Nothing
            Dim forms As Form() = {New PreviewWebDav(), New PreviewOwnCloud(), New PreviewNextcloud(), New PreviewScopevisio(), New PreviewBrowser(False), New PreviewBrowser(True), New PreviewLinks(), New PreviewShares(False), New PreviewShares(True), New PreviewDetails(), New DmsInstanceSelectionDialog({New DmsInstanceInfo("sample", "Sample instance", True)}, Nothing), New UploadProgressDialog({"Présentation — 文件 — दस्तावेज़.txt"}), UITools.CreateInputDialog("Input", UiStrings.Format("NewFolderPrompt", "sample/" & New String("X"c, 80)), "", input)}
            For Each form In forms
                Using form
                    form.ShowInTaskbar = False
                    form.Opacity = 0
                    If fontScale <> 1.0F Then form.Font = New Font(form.Font.FontFamily, form.Font.Size * fontScale)
                    form.Show()
                    form.PerformLayout()
                    Inspect(form, failures, form.GetType().Name & " scale=" & fontScale.ToString(CultureInfo.InvariantCulture))
                    If form.FormBorderStyle = FormBorderStyle.Sizable Then
                        Dim normal = form.Size
                        form.Size = form.MinimumSize
                        form.PerformLayout()
                        Inspect(form, failures, form.GetType().Name & " minimum scale=" & fontScale.ToString(CultureInfo.InvariantCulture))
                        form.Size = New Size(normal.Width + 240, normal.Height + 100)
                        form.PerformLayout()
                        Inspect(form, failures, form.GetType().Name & " wide scale=" & fontScale.ToString(CultureInfo.InvariantCulture))
                        form.Size = normal
                    End If
                    If TypeOf form Is UploadProgressDialog Then
                        Dim progress = DirectCast(form, UploadProgressDialog)
                        Dim files = {"Présentation — 文件 — दस्तावेज़.txt", New String("X"c, 120) & ".txt"}
                        For Each state In {UploadFileState.Waiting, UploadFileState.Transferring, UploadFileState.Finalizing, UploadFileState.Completed, UploadFileState.Failed, UploadFileState.Cancelled}
                            progress.Report(New UploadBatchSnapshot(files, {UploadFileState.Completed, state}, {New DmsTransferProgress(Long.MaxValue, Long.MaxValue, DmsTransferPhase.Completed), New DmsTransferProgress(Long.MaxValue \ 2, Long.MaxValue, DmsTransferPhase.Transferring)}, 1))
                            progress.PerformLayout()
                            Inspect(progress, failures, "Progress " & state.ToString() & " scale=" & fontScale.ToString(CultureInfo.InvariantCulture))
                        Next
                    End If
                    Dim output = Environment.GetEnvironmentVariable("DMS_LOCALIZATION_PREVIEW_DIR")
                    If Not String.IsNullOrEmpty(output) Then
                        System.IO.Directory.CreateDirectory(output)
                        Using bitmap As New Bitmap(form.Width, form.Height)
                            form.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
                            bitmap.Save(System.IO.Path.Combine(output, cultureName & "-" & form.GetType().Name & "-" & fontScale.ToString(CultureInfo.InvariantCulture) & ".png"), Imaging.ImageFormat.Png)
                        End Using
                    End If
                    form.Hide()
                End Using
            Next
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
        Assert.That(failures, [Is].Empty, cultureName & Environment.NewLine & String.Join(Environment.NewLine, failures))
    End Sub

    Private Shared Sub Inspect(parent As Control, failures As List(Of String), path As String)
        Dim children = parent.Controls.Cast(Of Control)().Where(Function(c) c.Visible).ToArray()
        For Each child In children
            Dim childPath = path & "/" & If(String.IsNullOrEmpty(child.Name), child.GetType().Name, child.Name)
            If Not parent.ClientRectangle.Contains(child.Bounds) Then failures.Add(childPath & " outside parent " & child.Bounds.ToString())
            If TypeOf child Is ButtonBase OrElse TypeOf child Is Label Then
                Dim preferred = child.GetPreferredSize(If(TypeOf child Is Label AndAlso Not DirectCast(child, Label).AutoSize, New Size(child.ClientSize.Width, 0), Size.Empty))
                If preferred.Width > child.Width OrElse preferred.Height > child.Height Then failures.Add(childPath & " clipped: actual=" & child.Size.ToString() & "; preferred=" & preferred.ToString() & "; text=" & child.Text)
            ElseIf TypeOf child Is GroupBox Then
                If TextRenderer.MeasureText(child.Text, child.Font).Width + 16 > child.Width Then failures.Add(childPath & " clipped group caption: " & child.Text)
            End If
            If TypeOf child Is ListView Then
                For Each column As ColumnHeader In DirectCast(child, ListView).Columns
                    If column.Width < TextRenderer.MeasureText(column.Text, child.Font).Width + 12 Then failures.Add(childPath & " clipped column: " & column.Text)
                Next
            End If
            Inspect(child, failures, childPath)
        Next
        For left As Integer = 0 To children.Length - 1
            For right As Integer = left + 1 To children.Length - 1
                If children(left).Bounds.IntersectsWith(children(right).Bounds) Then failures.Add(path & " overlap: " & children(left).Name & children(left).Bounds.ToString() & " / " & children(right).Name & children(right).Bounds.ToString())
            Next
        Next
    End Sub

    Private Class PreviewBrowser
        Inherits DmsBrowser
        Friend Sub New(returnSelection As Boolean)
            MyBase.New(New LayoutProvider(), Function(owner, instances) "sample", "Layout test", Nothing, Nothing, Nothing, BrowseModes.FoldersAndFiles, CType(255, FileOrFolderActions), If(returnSelection, DialogOperationModes.ReturnSelectedItems, DialogOperationModes.NoResults), Nothing, Nothing, Nothing)
            Me.InitializeDmsInstanceSwitching()
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
    Private Class PreviewLinks
        Inherits DmsLinkShareSetup
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
    Private Class PreviewShares
        Inherits DmsStandardShareSetup
        Friend Sub New(groupSharing As Boolean)
            MyBase.New(CType(Nothing, DmsShareForUser), New NoDmsProvider(), New List(Of String)())
            If groupSharing Then Me.DialogObjectMode = DialogObjectModes.GroupSharing
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
    Private Class PreviewDetails
        Inherits DmsItemSharings
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
    Private Class PreviewWebDav
        Inherits Global.CompuMaster.Dms.TestDemo.WebDav.LoginForm
        Friend Sub New()
            MyBase.New(Nothing)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
        Protected Overrides Sub OnClosing(e As System.ComponentModel.CancelEventArgs)
        End Sub
    End Class
    Private Class PreviewOwnCloud
        Inherits Global.CompuMaster.Dms.TestDemo.OwnCloudClassic.LoginForm
        Friend Sub New()
            MyBase.New(Nothing)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
        Protected Overrides Sub OnClosing(e As System.ComponentModel.CancelEventArgs)
        End Sub
    End Class
    Private Class PreviewNextcloud
        Inherits Global.CompuMaster.Dms.TestDemo.Nextcloud.LoginForm
        Friend Sub New()
            MyBase.New(Nothing)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
        Protected Overrides Sub OnClosing(e As System.ComponentModel.CancelEventArgs)
        End Sub
    End Class
    Private Class PreviewScopevisio
        Inherits Global.CompuMaster.Dms.TestDemo.ScopevisioTeamwork.LoginForm
        Friend Sub New()
            MyBase.New(Nothing)
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
        Protected Overrides Sub OnClosing(e As System.ComponentModel.CancelEventArgs)
        End Sub
    End Class
    Private Class LayoutProvider
        Inherits NoDmsProvider
        Implements IDmsInstanceProvider
        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property CurrentDmsInstance As DmsInstanceInfo Implements IDmsInstanceProvider.CurrentDmsInstance
            Get
                Return New DmsInstanceInfo("sample", "Preview instance", True)
            End Get
        End Property
        Public Function ListAvailableDmsInstances() As IReadOnlyList(Of DmsInstanceInfo) Implements IDmsInstanceProvider.ListAvailableDmsInstances
            Return {Me.CurrentDmsInstance, New DmsInstanceInfo("other", "Other instance", False)}
        End Function
        Public Sub SelectDmsInstance(id As String) Implements IDmsInstanceProvider.SelectDmsInstance
        End Sub
    End Class
End Class
