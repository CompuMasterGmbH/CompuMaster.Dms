Option Explicit On
Option Strict On

Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA)>
Public Class DemoLoginArtworkTest
    <TestCase(0), TestCase(1), TestCase(2), TestCase(3)>
    Public Sub DefaultArtworkIsEmbeddedAndFitsBesideTheFields(kind As Integer)
        Using form = CreateLogin(kind, Nothing)
            Dim picture = DirectCast(form.Controls.Find("LogoPictureBox", False).Single(), PictureBox)
            Assert.That(picture.Image, [Is].Not.Null)
            Assert.That(picture.Image.Height, [Is].EqualTo(picture.Image.Width * 2), "The common portrait is embedded in every demo assembly.")
            Assert.That(picture.SizeMode, [Is].EqualTo(PictureBoxSizeMode.Zoom))
            Assert.That(picture.TabStop, [Is].False)
            Assert.That(picture.Right, [Is].LessThan(form.Controls.Find("UsernameTextBox", False).Single().Left))
            Assert.That(picture.Bottom, [Is].LessThanOrEqualTo(form.ClientSize.Height))
            form.Scale(New SizeF(1.5F, 1.5F))
            form.PerformLayout()
            Assert.That(picture.Right, [Is].LessThan(form.Controls.Find("UsernameTextBox", False).Single().Left), "DPI scaling must retain the separate artwork and input areas.")
            Assert.That(form.Controls.Find("PasswordTextBox", False).Single().Text, [Is].Empty, "Artwork initialization must not read persisted credentials.")
            Using surface As New Bitmap(form.Width, form.Height)
                form.DrawToBitmap(surface, New Rectangle(Point.Empty, surface.Size))
            End Using
        End Using
    End Sub

    <TestCase(0), TestCase(1), TestCase(2), TestCase(3)>
    Public Sub CustomImagesAreCopiedAndResetRestoresTheCommonDefault(kind As Integer)
        Using supplied As New Bitmap(80, 40)
            supplied.SetPixel(0, 0, Color.Magenta)
            Using form = CreateLogin(kind, supplied)
                Dim propertyInfo = form.GetType().GetProperty("LoginImage")
                Dim displayed = DirectCast(propertyInfo.GetValue(form), Bitmap)
                Assert.That(Object.ReferenceEquals(displayed, supplied), [Is].False)
                Assert.That(displayed.Size, [Is].EqualTo(supplied.Size))
                Assert.That(displayed.GetPixel(0, 0).ToArgb(), [Is].EqualTo(Color.Magenta.ToArgb()))
                supplied.Dispose()
                Assert.That(displayed.GetPixel(0, 0).ToArgb(), [Is].EqualTo(Color.Magenta.ToArgb()), "The caller may dispose its original after construction.")
                Using replacement As New Bitmap(32, 64)
                    replacement.SetPixel(0, 0, Color.Lime)
                    propertyInfo.SetValue(form, replacement)
                    Assert.That(DirectCast(propertyInfo.GetValue(form), Bitmap).GetPixel(0, 0).ToArgb(), [Is].EqualTo(Color.Lime.ToArgb()))
                    Assert.That(IsDisposed(displayed), [Is].True, "Replacing artwork releases the previous form-owned copy.")
                    propertyInfo.SetValue(form, Nothing)
                    Assert.That(replacement.GetPixel(0, 0).ToArgb(), [Is].EqualTo(Color.Lime.ToArgb()), "Resetting must not dispose the caller's image.")
                End Using
                Dim resetImage = DirectCast(propertyInfo.GetValue(form), Image)
                Assert.That(resetImage.Height, [Is].EqualTo(resetImage.Width * 2))
                form.Dispose()
                Assert.That(IsDisposed(resetImage), [Is].True, "Disposing the login releases its final bitmap.")
            End Using
        End Using
    End Sub

    <TestCase(0), TestCase(1), TestCase(2), TestCase(3)>
    Public Sub SeparateLoginFormsDoNotShareDisposableImageInstances(kind As Integer)
        Using first = CreateLogin(kind, Nothing), second = CreateLogin(kind, Nothing)
            Dim firstImage = DirectCast(first.GetType().GetProperty("LoginImage").GetValue(first), Bitmap)
            Dim secondImage = DirectCast(second.GetType().GetProperty("LoginImage").GetValue(second), Bitmap)
            Assert.That(Object.ReferenceEquals(firstImage, secondImage), [Is].False)
            Dim expectedPixel = firstImage.GetPixel(0, 0).ToArgb()
            Assert.That(secondImage.GetPixel(0, 0).ToArgb(), [Is].EqualTo(expectedPixel))
            first.Dispose()
            Assert.That(secondImage.GetPixel(0, 0).ToArgb(), [Is].EqualTo(expectedPixel))
        End Using
    End Sub

    Private Shared Function IsDisposed(image As Image) As Boolean
        Try
            Dim size = image.Size
            Return False
        Catch ex As ArgumentException
            Return True
        End Try
    End Function

    Private Shared Function CreateLogin(kind As Integer, artwork As Image) As Form
        Select Case kind
            Case 0 : Return New Global.CompuMaster.Dms.TestDemo.WebDav.LoginForm(Nothing, artwork)
            Case 1 : Return New Global.CompuMaster.Dms.TestDemo.OwnCloudClassic.LoginForm(Nothing, artwork)
            Case 2 : Return New Global.CompuMaster.Dms.TestDemo.Nextcloud.LoginForm(Nothing, artwork)
            Case 3 : Return New Global.CompuMaster.Dms.TestDemo.ScopevisioTeamwork.LoginForm(Nothing, artwork)
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(kind))
        End Select
    End Function
End Class
