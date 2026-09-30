Imports System.Linq
Imports NUnit.Framework

<TestFixture>
Public NotInheritable Class TestPartitioningTest

    Private Shared ReadOnly RemoteServerCategories As String() = {
        "ScopevisioTeamwork",
        "GenericWebDav",
        "OwnCloudWebDav",
        "NextcloudWebDav"
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
                    Assert.That(Categories.Intersect(RemoteServerCategories).Count(), [Is].EqualTo(1), RemoteFixture.FullName & " must identify exactly one physical remote test server.")
                Next
            End Sub)
    End Sub

End Class
