Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms
Imports CompuMaster.Dms.BrowserUI
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class LocalizedFormLayoutTest
    <TestCase("fr-FR"), TestCase("es-ES"), TestCase("zh-CN"), TestCase("zh-TW"), TestCase("ja-JP"), TestCase("ar-SA"), TestCase("he-IL"), TestCase("hi-IN")>
    Public Sub UnicodeFormsKeepTheirExistingDirectionAndRenderWithoutServerAccess(cultureName As String)
        Dim previous = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName)
            Using browser As New PreviewBrowser(), links As New PreviewLinks(), shares As New PreviewShares(), details As New PreviewDetails(), progress As New UploadProgressDialog({"Présentation — 文件 — दस्तावेज़.txt"})
                Dim forms As Form() = {browser, links, shares, details, progress}
                For Each form In forms
                    Assert.That(form.RightToLeftLayout, [Is].False, "RTL layout implementation is excluded from this change.")
                    Assert.That(form.RightToLeft, [Is].Not.EqualTo(RightToLeft.Yes))
                    'Preview subclasses suppress Load; actual controls render without authorization or I/O.
                    form.Show()
                    form.Refresh()
                    For Each button In Descendants(form).OfType(Of Button)()
                        Assert.That(button.Width, [Is].GreaterThanOrEqualTo(button.GetPreferredSize(Drawing.Size.Empty).Width), cultureName & " " & button.Name)
                    Next
                    Using bitmap As New Drawing.Bitmap(form.Width, form.Height)
                        form.DrawToBitmap(bitmap, New Drawing.Rectangle(0, 0, form.Width, form.Height))
                        Dim output = Environment.GetEnvironmentVariable("DMS_LOCALIZATION_PREVIEW_DIR")
                        If Not String.IsNullOrEmpty(output) Then
                            System.IO.Directory.CreateDirectory(output)
                            bitmap.Save(System.IO.Path.Combine(output, cultureName & "-" & form.GetType().Name & ".png"), Drawing.Imaging.ImageFormat.Png)
                        End If
                    End Using
                    form.Hide()
                Next
            End Using
        Finally
            CultureInfo.CurrentUICulture = previous
        End Try
    End Sub

    Private Shared Iterator Function Descendants(parent As Control) As IEnumerable(Of Control)
        For Each child As Control In parent.Controls
            Yield child
            For Each nested In Descendants(child)
                Yield nested
            Next
        Next
    End Function

    Private Class PreviewBrowser
        Inherits DmsBrowser
        Friend Sub New()
            MyBase.New(New NoDmsProvider())
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
        Friend Sub New()
            MyBase.New(CType(Nothing, Global.CompuMaster.Dms.Data.DmsShareForUser), New NoDmsProvider(), New List(Of String)())
        End Sub
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
    Private Class PreviewDetails
        Inherits DmsItemSharings
        Protected Overrides Sub OnLoad(e As EventArgs)
        End Sub
    End Class
End Class
