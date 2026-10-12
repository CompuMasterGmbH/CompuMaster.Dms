Imports System.Windows.Forms
Imports NUnit.Framework
Imports System.Threading

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class UiTestFailureCaptureTest
    <Test>
    Public Sub WindowCallbackExceptionsBecomeTestFailuresWithoutOpeningTheRuntimeDialog()
        Using dispatcher As New UiTestDispatcher()
            Dim marker As New InvalidOperationException("Owned isolated window-callback failure")
            Application.OnThreadException(marker)
            Dim failure = Assert.Throws(Of InvalidOperationException)(Sub() dispatcher.PumpUntil(Function() False))
            Assert.That(failure.InnerException, [Is].SameAs(marker))
        End Using
    End Sub
End Class
