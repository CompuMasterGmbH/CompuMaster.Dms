Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class BaseDmsProviderPathTest

    <Test, Category("TestLevel1")>
    Public Sub CombinePathIgnoresNullSegments()
        Dim provider As New NoDmsProvider()

        Assert.That(provider.CombinePath("folder1/folder2", CType(Nothing, String)), [Is].EqualTo("folder1/folder2"))
        Assert.That(provider.CombinePath("folder1", Nothing, "folder2", Nothing, "folder3"), [Is].EqualTo("folder1/folder2/folder3"))
        Assert.That(provider.CombinePath("folder1/folder2", CType(Nothing, String())), [Is].EqualTo("folder1/folder2"))
    End Sub

End Class
