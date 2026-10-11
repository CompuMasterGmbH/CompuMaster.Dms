Imports System.IO
Imports System.Text
Imports CompuMaster.Dms.BrowserUI
Imports NUnit.Framework

<TestFixture>
Public Class FileVersionDownloadDestinationTest
    Private Root As String

    <SetUp>
    Public Sub CreateOwnedDirectory()
        Root = Path.Combine(Path.GetTempPath(), "cm-dms-version-test-" & Guid.NewGuid().ToString("N"))
        Assert.That(Directory.Exists(Root), [Is].False)
        Directory.CreateDirectory(Root)
    End Sub

    <TearDown>
    Public Sub RemoveOwnedDirectory()
        If Directory.Exists(Root) Then Directory.Delete(Root, True)
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub FailedDownloadPreservesDestinationAndRemovesTemporaryFile(replace As Boolean)
        Dim destination = Path.Combine(Root, "report.txt")
        File.WriteAllText(destination, "original")
        Dim expected As New IOException("server failure")
        Dim actual = Assert.Throws(Of IOException)(
            Sub() FileVersionDownloadDestination.Save(destination, Root, replace,
                Sub(stream)
                    stream.WriteByte(42)
                    Throw expected
                End Sub, Nothing))
        Assert.That(actual, [Is].SameAs(expected))
        Assert.That(File.ReadAllText(destination), [Is].EqualTo("original"))
        Assert.That(Directory.GetFiles(Root), [Is].EqualTo({destination}))
    End Sub

    <Test>
    Public Sub FileAppearingDuringDownloadIsNotReplacedWithoutPermission()
        Dim destination = Path.Combine(Root, "report.txt")
        Assert.Throws(Of IOException)(
            Sub() FileVersionDownloadDestination.Save(destination, Root, False,
                Sub(stream)
                    stream.WriteByte(42)
                    File.WriteAllText(destination, "appeared")
                End Sub, Nothing))
        Assert.That(File.ReadAllText(destination), [Is].EqualTo("appeared"))
        Assert.That(Directory.GetFiles(Root), [Is].EqualTo({destination}))
    End Sub

    <TestCase(False)>
    <TestCase(True)>
    Public Sub SuccessfulDownloadPublishesContentAndVersionTimestamp(replace As Boolean)
        Dim destination = Path.Combine(Root, "report.txt")
        If replace Then File.WriteAllText(destination, "original")
        Dim timestamp As New DateTime(2026, 10, 7, 12, 34, 56, DateTimeKind.Utc)
        FileVersionDownloadDestination.Save(destination, Root, replace,
            Sub(stream)
                Dim bytes = Encoding.UTF8.GetBytes("historical content")
                stream.Write(bytes, 0, bytes.Length)
            End Sub, timestamp)
        Assert.That(File.ReadAllText(destination), [Is].EqualTo("historical content"))
        Assert.That(File.GetLastWriteTimeUtc(destination), [Is].EqualTo(timestamp))
        Assert.That(Directory.GetFiles(Root), [Is].EqualTo({destination}))
    End Sub

    <Test>
    Public Sub RequiredParentRejectsSiblingWithSamePrefixAndTraversal()
        For Each destination In {Path.Combine(Root & "-sibling", "report.txt"), Path.Combine(Root, "..", "outside.txt")}
            Assert.Throws(Of IOException)(Sub() FileVersionDownloadDestination.Validate(destination, Root))
        Next
        Assert.That(FileVersionDownloadDestination.Validate(Path.Combine(Root, "report.txt"), Root),
                    [Is].EqualTo(Path.Combine(Root, "report.txt")))
    End Sub
End Class
