Imports System.Globalization
Imports System.Threading
Imports CompuMaster.Dms.Data
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class DmsBrowserOwnerPropertiesTest
    <TestCase(DmsResourceItem.ItemTypes.File, "Actual resource owner")>
    <TestCase(DmsResourceItem.ItemTypes.Folder, "Actual resource owner")>
    <TestCase(DmsResourceItem.ItemTypes.File, Nothing)>
    <TestCase(DmsResourceItem.ItemTypes.Folder, Nothing)>
    Public Sub PropertiesRenderTheResourceOwnerAndRetainUnknownMetadata(itemType As DmsResourceItem.ItemTypes, displayName As String)
        Dim original = CultureInfo.CurrentUICulture
        Try
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en")
            Dim item As New DmsResourceItem With {.ItemType = itemType, .Name = "fixture", .FullName = "fixture", .ExtendedInfosOwner = New DmsUser With {.DisplayName = displayName}}
            Using browser As New Global.CompuMaster.Dms.BrowserUI.DmsBrowser(New Global.CompuMaster.Dms.Providers.NoDmsProvider())
                Dim details = browser.PropertiesDetails(item)
                Assert.That(details, Does.Contain("Owner: " & If(displayName, "") & Environment.NewLine))
                Assert.That(item.ExtendedInfosOwner.ID, [Is].Null)
            End Using
        Finally
            CultureInfo.CurrentUICulture = original
        End Try
    End Sub
End Class
