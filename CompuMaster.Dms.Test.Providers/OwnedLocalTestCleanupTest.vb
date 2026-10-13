Option Explicit On
Option Strict On

Imports System.IO
Imports NUnit.Framework

<TestFixture>
Public Class OwnedLocalTestCleanupTest
    Private OwnedDirectory As String
    Private DirectoryAdmitted As Boolean
    Private ReadOnly CreatedFiles As New List(Of String)

    <SetUp>
    Public Sub AdmitFreshPrivateDirectory()
        DirectoryAdmitted = False
        OwnedDirectory = Nothing
        CreatedFiles.Clear()
        OwnedDirectory = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.WorkDirectory, ".task-work", "unittests_cleanup137_" & Guid.NewGuid().ToString("N")))
        Assert.That(Directory.Exists(OwnedDirectory), [Is].False)
        Directory.CreateDirectory(OwnedDirectory)
        DirectoryAdmitted = True
    End Sub

    <TearDown>
    Public Sub RemoveOnlyExactFixtureFiles()
        Try
            For Each file In CreatedFiles
                If Global.System.IO.File.Exists(file) Then Global.System.IO.File.Delete(file)
            Next
            If DirectoryAdmitted AndAlso Directory.Exists(OwnedDirectory) Then Directory.Delete(OwnedDirectory, False)
        Finally
            DirectoryAdmitted = False
            OwnedDirectory = Nothing
            CreatedFiles.Clear()
        End Try
    End Sub

    Private Function CreateOwnedFile(name As String) As String
        Dim result = Path.Combine(OwnedDirectory, name)
        Assert.That(Global.System.IO.File.Exists(result), [Is].False)
        CreatedFiles.Add(result)
        Global.System.IO.File.WriteAllText(result, "fixture-only")
        Return result
    End Function

    <Test>
    Public Sub PartialSetupWithoutBufferStillRemovesAdmittedEmptyDirectory()
        OwnedLocalTestCleanup.Cleanup(Nothing, Nothing, OwnedDirectory, True, Array.Empty(Of String)())
        Assert.That(Directory.Exists(OwnedDirectory), [Is].False)
    End Sub

    <Test>
    Public Sub CloseFailureStillDisposesAndRemovesEveryTrackedResource()
        Dim file = CreateOwnedFile("known.tmp")
        Dim disposed As Boolean
        Dim original As New InvalidOperationException("fixture close failure")
        Dim failure = Assert.Throws(Of AggregateException)(Sub() OwnedLocalTestCleanup.Cleanup(
            Sub() Throw original, Sub() disposed = True, OwnedDirectory, True, {file}))
        Assert.That(disposed, [Is].True)
        Assert.That(failure.InnerExceptions, [Is].EquivalentTo({original}))
        Assert.That(Global.System.IO.File.Exists(file), [Is].False)
        Assert.That(Directory.Exists(OwnedDirectory), [Is].False)
    End Sub

    <Test>
    Public Sub BothWindowFailuresArePreservedAfterFileCleanup()
        Dim file = CreateOwnedFile("known.tmp")
        Dim closeFailure As New InvalidOperationException("fixture close failure")
        Dim disposeFailure As New InvalidOperationException("fixture dispose failure")
        Dim failure = Assert.Throws(Of AggregateException)(Sub() OwnedLocalTestCleanup.Cleanup(
            Sub() Throw closeFailure, Sub() Throw disposeFailure, OwnedDirectory, True, {file}))
        Assert.That(failure.InnerExceptions, [Is].EquivalentTo({closeFailure, disposeFailure}))
        Assert.That(Directory.Exists(OwnedDirectory), [Is].False)
    End Sub

    <Test>
    Public Sub FailedAdmissionNeverDeletesThePreexistingDirectoryOrFile()
        Dim file = CreateOwnedFile("foreign.tmp")
        OwnedLocalTestCleanup.Cleanup(Nothing, Nothing, OwnedDirectory, False, {file})
        Assert.That(Global.System.IO.File.ReadAllText(file), [Is].EqualTo("fixture-only"))
        Assert.That(Directory.Exists(OwnedDirectory), [Is].True)
    End Sub

    <Test>
    Public Sub WrongParentIsRejectedWithoutBlockingLaterTrackedFileCleanup()
        Dim ownFile = CreateOwnedFile("known.tmp")
        Dim foreignFile = Path.Combine(Path.GetDirectoryName(OwnedDirectory), "unittests_foreign137_" & Guid.NewGuid().ToString("N") & ".tmp")
        Assert.That(Global.System.IO.File.Exists(foreignFile), [Is].False)
        CreatedFiles.Add(foreignFile)
        Global.System.IO.File.WriteAllText(foreignFile, "fixture-only")
        Dim failure = Assert.Throws(Of AggregateException)(Sub() OwnedLocalTestCleanup.Cleanup(
            Nothing, Nothing, OwnedDirectory, True, {foreignFile, ownFile}))
        Assert.That(failure.InnerExceptions.Count, [Is].EqualTo(1))
        Assert.That(failure.InnerExceptions(0), [Is].TypeOf(Of InvalidOperationException)())
        Assert.That(Global.System.IO.File.ReadAllText(foreignFile), [Is].EqualTo("fixture-only"))
        Assert.That(Global.System.IO.File.Exists(ownFile), [Is].False)
        Assert.That(Directory.Exists(OwnedDirectory), [Is].False)
    End Sub

    <Test>
    Public Sub UntrackedChildPreventsParentRemovalInsteadOfSweepingIt()
        Dim foreignFile = CreateOwnedFile("untracked.tmp")
        Assert.Throws(Of AggregateException)(Sub() OwnedLocalTestCleanup.Cleanup(
            Nothing, Nothing, OwnedDirectory, True, Array.Empty(Of String)()))
        Assert.That(Global.System.IO.File.ReadAllText(foreignFile), [Is].EqualTo("fixture-only"))
        Assert.That(Directory.Exists(OwnedDirectory), [Is].True)
    End Sub

    <Test>
    Public Sub AlreadyAbsentTrackedChildAndParentAreAccepted()
        Dim file = CreateOwnedFile("known.tmp")
        Global.System.IO.File.Delete(file)
        Directory.Delete(OwnedDirectory, False)
        Assert.DoesNotThrow(Sub() OwnedLocalTestCleanup.Cleanup(Nothing, Nothing, OwnedDirectory, True, {file}))
    End Sub
End Class
