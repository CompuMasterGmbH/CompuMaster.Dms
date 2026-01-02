Imports NUnit.Framework
Imports NUnit.Framework.Legacy

<TestFixture>
<Parallelizable(ParallelScope.All)>
Public Class ToolsTest

    <Test> Public Sub ByteSizeToUIDisplayText()
        Dim CurCulture As System.Globalization.CultureInfo = System.Globalization.CultureInfo.CurrentCulture
        Try
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture
            ClassicAssert.AreEqual("0 Bytes", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(0))
            ClassicAssert.AreEqual("999 Bytes", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(999))
            ClassicAssert.AreEqual("1,001 Bytes", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1001))
            ClassicAssert.AreEqual("1 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300))
            ClassicAssert.AreEqual("1 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1301))
            ClassicAssert.AreEqual("2 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(2300))
            ClassicAssert.AreEqual("10 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(10300))
            ClassicAssert.AreEqual("98 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(100300))
            ClassicAssert.AreEqual("127 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(130000))
            ClassicAssert.AreEqual("1 MB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000))
            ClassicAssert.AreEqual("1 GB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000))
            ClassicAssert.AreEqual("1 TB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000 ^ 2))
            ClassicAssert.AreEqual("1 PB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000 ^ 3))
            ClassicAssert.AreEqual("1,155 PB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000 ^ 4))
        Finally
            System.Globalization.CultureInfo.CurrentCulture = CurCulture
        End Try
    End Sub

    <Test> Public Sub IsParentDirectory()
        Select Case System.Environment.OSVersion.Platform
            Case PlatformID.Win32NT
                ClassicAssert.IsTrue(Tools.IsParentDirectory("D:\", "D:\"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("D:\", "D:\Test\SubDir"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("D:\Test", "D:\Test\"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("D:\Test\", "D:\Test\"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("D:\Test", "D:\Test"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("D:\Test", "D:\Test\SubDir"))
                ClassicAssert.IsFalse(Tools.IsParentDirectory("D:\Test\SubDir", "D:\Test"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("D:\Test\SubDir", "D:\Test\SubDir"))
                ClassicAssert.IsFalse(Tools.IsParentDirectory("E:\", "D:\Test\SubDir"))
            Case Else 'Unix/Mac/Linux
                ClassicAssert.IsTrue(Tools.IsParentDirectory("/tmp", "/tmp/"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("/tmp", "/tmp/Test/SubDir"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("/tmp/Test", "/tmp/Test/"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("/tmp/Test/", "/tmp/Test/"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("/tmp/Test", "/tmp/Test"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("/tmp/Test", "/tmp/Test/SubDir"))
                ClassicAssert.IsFalse(Tools.IsParentDirectory("/tmp/Test/SubDir", "/tmp/Test"))
                ClassicAssert.IsTrue(Tools.IsParentDirectory("/tmp/Test/SubDir", "/tmp/Test/SubDir"))
                ClassicAssert.IsFalse(Tools.IsParentDirectory("/root/", "/tmp/Test\SubDir"))
        End Select
        ClassicAssert.Throws(Of ArgumentNullException)(
            Sub()
                Tools.IsParentDirectory("", "/tmp/Test\SubDir")
            End Sub)
    End Sub

End Class
