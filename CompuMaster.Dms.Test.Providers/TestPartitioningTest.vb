Imports System.Linq
Imports NUnit.Framework

<TestFixture>
Public NotInheritable Class TestPartitioningTest

    Private Shared ReadOnly RemotePartitionCategories As String() = {
        "ScopevisioTeamwork",
        "WebDav",
        "OwnCloud",
        "Nextcloud"
    }

    <Test>
    Public Sub RemoteProviderFixturesDeclareExactlyOneServerCategory()
        Dim RemoteFixtures As Type() = GetType(BaseDmsProviderTestBase).Assembly.GetTypes().
            Where(Function(TestType) TestType.IsAbstract = False AndAlso GetType(BaseDmsProviderTestBase).IsAssignableFrom(TestType)).
            ToArray()

        Assert.That(RemoteFixtures, [Is].Not.Empty)
        Assert.Multiple(
            Sub()
                For Each RemoteFixture As Type In RemoteFixtures
                    Dim Categories As String() = RemoteFixture.
                        GetCustomAttributes(GetType(CategoryAttribute), False).
                        Cast(Of CategoryAttribute)().
                        Select(Function(Category) Category.Name).
                        ToArray()

                    Assert.That(Categories, Does.Contain("RemoteDms"), RemoteFixture.FullName & " must be excluded from lock-free test jobs.")
                    Assert.That(Categories.Intersect(RemotePartitionCategories).Count(), [Is].EqualTo(1), RemoteFixture.FullName & " must identify exactly one logical remote test partition.")
                Next
            End Sub)
    End Sub

    <Test>
    Public Sub EveryDiscoveredRemoteTestHasExactlyOneLevelAndQuickJobsExcludeExtendedCases()
        'NUnit discovery resolves inherited methods, fixture categories, and test cases without running setup.
        Dim suite = New NUnit.Framework.Api.DefaultTestAssemblyBuilder().Build(GetType(BaseDmsProviderTestBase).Assembly, New Dictionary(Of String, Object)())
        Dim remoteTests As New List(Of KeyValuePair(Of String, String()))()
        CollectRemoteTests(suite, New String() {}, remoteTests)
        Assert.That(remoteTests, [Is].Not.Empty)
        For Each remoteTest In remoteTests
            Assert.That(remoteTest.Value.Count(Function(category) category = "TestLevel1" OrElse category = "TestLevel2"), [Is].EqualTo(1), remoteTest.Key)
            If remoteTest.Key.Contains("Copy_Directories") OrElse remoteTest.Key.Contains("Move_Directories") OrElse
                remoteTest.Key.Contains("Copy_Files") OrElse remoteTest.Key.Contains("Move_Files") OrElse
                remoteTest.Value.Any(Function(category) category.StartsWith("ScopevisioNative", StringComparison.Ordinal)) OrElse
                remoteTest.Value.Contains("ScopevisioSynchronousUploadComparison") Then
                Assert.That(remoteTest.Value, Does.Contain("TestLevel2"), remoteTest.Key)
            End If
        Next
        For Each serverPartition As String In RemotePartitionCategories
            Dim quick = remoteTests.Where(Function(test) test.Value.Contains(serverPartition) AndAlso test.Value.Contains("TestLevel1")).ToArray()
            Assert.That(quick.Any(Function(test) test.Key.EndsWith(".BasicFileRoundTripAndCleanup", StringComparison.Ordinal)), [Is].True, serverPartition)
            Assert.That(quick.Any(Function(test) test.Key.EndsWith(".LoginAtRestApiWebservice", StringComparison.Ordinal)), [Is].True, serverPartition)
            Assert.That(quick.Any(Function(test) test.Key.EndsWith(".ListAllRemoteItems", StringComparison.Ordinal)), [Is].True, serverPartition)
            Assert.That(remoteTests.Any(Function(test) test.Value.Contains(serverPartition) AndAlso test.Value.Contains("TestLevel2")), [Is].True, serverPartition)
        Next
    End Sub

    Private Shared Sub CollectRemoteTests(test As NUnit.Framework.Interfaces.ITest, inheritedCategories As String(), results As List(Of KeyValuePair(Of String, String())))
        Dim categories = inheritedCategories.Concat(test.Properties("Category").Cast(Of String)()).Distinct().ToArray()
        If test.IsSuite Then
            For Each child In test.Tests
                CollectRemoteTests(child, categories, results)
            Next
        ElseIf categories.Contains("RemoteDms") Then
            results.Add(New KeyValuePair(Of String, String())(test.FullName, categories))
        End If
    End Sub

End Class
