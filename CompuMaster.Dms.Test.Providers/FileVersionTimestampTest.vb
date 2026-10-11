Imports CompuMaster.Dms.Data
Imports NUnit.Framework

<TestFixture, Category("TestLevel1")>
Public Class FileVersionTimestampTest
    <Test>
    Public Sub VersionUploadDateDoesNotClaimAHistoricalModificationTime()
        Dim file As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File}
        Dim uploaded As New DateTime(2026, 10, 12, 0, 12, 0, DateTimeKind.Utc)
        Dim version As New DmsFileVersion(file, "opaque-id", Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, Nothing, uploaded)
        Assert.That(version.VersionDateUtc, [Is].EqualTo(uploaded))
        Assert.That(version.LastModificationUtc, [Is].Null)
    End Sub

    <Test>
    Public Sub ExistingConstructorSignatureAndModificationDateRemainCompatible()
        Dim file As New DmsResourceItem With {.ItemType = DmsResourceItem.ItemTypes.File}
        Dim modified As New DateTime(2026, 10, 7, 12, 34, 56, DateTimeKind.Utc)
        Dim version As New DmsFileVersion(file, "opaque-id", modified)
        Assert.That(version.LastModificationUtc, [Is].EqualTo(modified))
        Assert.That(version.VersionDateUtc, [Is].Null)
        Dim signature As Type() = {GetType(DmsResourceItem), GetType(String), GetType(DateTime?), GetType(Long?),
                                  GetType(String), GetType(String), GetType(String), GetType(String), GetType(String)}
        Assert.That(GetType(DmsFileVersion).GetConstructor(signature), [Is].Not.Null)
    End Sub
End Class
