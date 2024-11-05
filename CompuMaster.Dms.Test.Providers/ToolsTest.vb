Imports NUnit.Framework

<TestFixture>
<Parallelizable(ParallelScope.All)>
Public Class ToolsTest

    <Test> Public Sub ByteSizeToUIDisplayText()
        Dim CurCulture As System.Globalization.CultureInfo = System.Globalization.CultureInfo.CurrentCulture
        Try
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture
            Assert.AreEqual("0 Bytes", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(0))
            Assert.AreEqual("999 Bytes", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(999))
            Assert.AreEqual("1,001 Bytes", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1001))
            Assert.AreEqual("1 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300))
            Assert.AreEqual("1 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1301))
            Assert.AreEqual("2 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(2300))
            Assert.AreEqual("10 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(10300))
            Assert.AreEqual("98 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(100300))
            Assert.AreEqual("127 KB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(130000))
            Assert.AreEqual("1 MB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000))
            Assert.AreEqual("1 GB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000))
            Assert.AreEqual("1 TB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000 ^ 2))
            Assert.AreEqual("1 PB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000 ^ 3))
            Assert.AreEqual("1,155 PB", CompuMaster.Dms.Tools.ByteSizeToUIDisplayText(1300000 * 1000 ^ 4))
        Finally
            System.Globalization.CultureInfo.CurrentCulture = CurCulture
        End Try
    End Sub

    <Test> Public Sub IsParentDirectory()
        Select Case System.Environment.OSVersion.Platform
            Case PlatformID.Win32NT
                Assert.IsTrue(Tools.IsParentDirectory("D:\", "D:\"))
                Assert.IsTrue(Tools.IsParentDirectory("D:\", "D:\Test\SubDir"))
                Assert.IsTrue(Tools.IsParentDirectory("D:\Test", "D:\Test\"))
                Assert.IsTrue(Tools.IsParentDirectory("D:\Test\", "D:\Test\"))
                Assert.IsTrue(Tools.IsParentDirectory("D:\Test", "D:\Test"))
                Assert.IsTrue(Tools.IsParentDirectory("D:\Test", "D:\Test\SubDir"))
                Assert.IsFalse(Tools.IsParentDirectory("D:\Test\SubDir", "D:\Test"))
                Assert.IsTrue(Tools.IsParentDirectory("D:\Test\SubDir", "D:\Test\SubDir"))
                Assert.IsFalse(Tools.IsParentDirectory("E:\", "D:\Test\SubDir"))
            Case Else 'Unix/Mac/Linux
                Assert.IsTrue(Tools.IsParentDirectory("/tmp", "/tmp/"))
                Assert.IsTrue(Tools.IsParentDirectory("/tmp", "/tmp/Test/SubDir"))
                Assert.IsTrue(Tools.IsParentDirectory("/tmp/Test", "/tmp/Test/"))
                Assert.IsTrue(Tools.IsParentDirectory("/tmp/Test/", "/tmp/Test/"))
                Assert.IsTrue(Tools.IsParentDirectory("/tmp/Test", "/tmp/Test"))
                Assert.IsTrue(Tools.IsParentDirectory("/tmp/Test", "/tmp/Test/SubDir"))
                Assert.IsFalse(Tools.IsParentDirectory("/tmp/Test/SubDir", "/tmp/Test"))
                Assert.IsTrue(Tools.IsParentDirectory("/tmp/Test/SubDir", "/tmp/Test/SubDir"))
                Assert.IsFalse(Tools.IsParentDirectory("/root/", "/tmp/Test\SubDir"))
        End Select
        Assert.Throws(Of ArgumentNullException)(
            Sub()
                Tools.IsParentDirectory("", "/tmp/Test\SubDir")
            End Sub)
    End Sub

End Class
