Option Explicit On
Option Strict On

Imports System.IO
Imports NUnit.Framework
Imports CompuMaster.Dms.Providers

<TestFixture>
Public Class ScopevisioDemoSelectionBufferTest
    Private DirectoryPath As String
    Private Buffer As DemoInputBuffer
    Private DirectoryAdmitted As Boolean
    Private ReadOnly TrackedFiles As New List(Of String)

    <SetUp>
    Public Sub SetupOwnedBuffer()
        Buffer = Nothing
        DirectoryAdmitted = False
        TrackedFiles.Clear()
        DirectoryPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.WorkDirectory, ".task-work", "unittests_demo_selector_" & Guid.NewGuid().ToString("N")))
        Assert.That(Directory.Exists(DirectoryPath), [Is].False, "The fresh private buffer directory must be absent.")
        Directory.CreateDirectory(DirectoryPath)
        DirectoryAdmitted = True
        Buffer = New DemoInputBuffer(DirectoryPath, "Scopevisio.Teamwork.Test", "SCOPEVISIOTEAMWORK", Function(name) Nothing)
        For Each field In {"username", "password", "customer no.", "start path", ScopevisioDemoSelection.OrganisationField, ScopevisioDemoSelection.TenantField}
            Dim file = Buffer.FilePath(field)
            Assert.That(Global.System.IO.File.Exists(file), [Is].False)
            TrackedFiles.Add(file)
        Next
    End Sub

    <TearDown>
    Public Sub RemoveOwnedBuffers()
        Try
            OwnedLocalTestCleanup.Cleanup(Nothing, Nothing, DirectoryPath, DirectoryAdmitted, TrackedFiles)
        Finally
            Buffer = Nothing
            DirectoryAdmitted = False
            TrackedFiles.Clear()
        End Try
    End Sub

    <Test>
    Public Sub BlankSelectorsRetainTheCommonLegacyProfile()
        Dim profile = ScopevisioDemoSelection.CreateProfile("fixture-login", "fixture-password", "fixture-customer", "", "")
        Assert.That(profile.GetType(), [Is].EqualTo(GetType(DmsLoginProfile)))
        Assert.That(profile.CustomerInstance, [Is].EqualTo("fixture-customer"))
    End Sub

    <Test>
    Public Sub ExplicitSelectionSetsDistinctSelectorGuardAndBothCustomerMappings()
        Dim profile = CType(ScopevisioDemoSelection.CreateProfile("fixture-login", "fixture-password", "fixture-customer", "9223372036854775807", "fixture-tenant"), ScopevisioLoginCredentials)
        Assert.That(profile.OrganisationId, [Is].EqualTo(Long.MaxValue))
        Assert.That(profile.ExpectedTeamworkTenantId, [Is].EqualTo("fixture-tenant"))
        Assert.That(profile.CustomerInstance, [Is].EqualTo("fixture-customer"))
        Assert.That(profile.ClientNumber, [Is].EqualTo("fixture-customer"))
    End Sub

    <Test>
    Public Sub GuardOnlyProfileDoesNotCoerceTheTenantToAnOrganisationSelector()
        Dim profile = CType(ScopevisioDemoSelection.CreateProfile("fixture-login", "fixture-password", "fixture-customer", "", "fixture-tenant"), ScopevisioLoginCredentials)
        Assert.That(profile.OrganisationId, [Is].Null)
        Assert.That(profile.ExpectedTeamworkTenantId, [Is].EqualTo("fixture-tenant"))
    End Sub

    <TestCase("0"), TestCase("-1"), TestCase("7.25"), TestCase("7e0"), TestCase("+7"), TestCase(" "), TestCase("tenant-guid"), TestCase("9223372036854775808")>
    Public Sub MalformedExplicitSelectionNeverReturnsTheLegacyProfile(value As String)
        Assert.Throws(Of ArgumentException)(Sub() ScopevisioDemoSelection.CreateProfile("fixture-login", "fixture-password", "fixture-customer", value, "fixture-tenant"))
    End Sub

    <TestCase(" "), TestCase("fixture" & vbCrLf)>
    Public Sub InvalidGuardIsRejectedBeforeAnyProviderIsConstructed(value As String)
        Assert.Throws(Of ArgumentException)(Sub() ScopevisioDemoSelection.CreateProfile("fixture-login", "fixture-password", "fixture-customer", "7", value))
    End Sub

    <Test>
    Public Sub BufferPreservesTheHistoricalFieldHashMapping()
        Assert.That(Path.GetFileName(Buffer.FilePath("password")), [Is].EqualTo("~Buffer.Scopevisio.Teamwork.Test.5F4DCC3B5AA765D61D8327DEB882CF99.tmp"))
    End Sub

    <Test>
    Public Sub SelectorBuffersRemainSeparateFromTheExistingCredentials()
        Buffer.Persist("password", "private-fixture-password")
        Buffer.Persist("customer no.", "private-fixture-customer")
        Buffer.Persist(ScopevisioDemoSelection.OrganisationField, "7")
        Buffer.Persist(ScopevisioDemoSelection.TenantField, "fixture-tenant")
        Assert.That(Buffer.Read(ScopevisioDemoSelection.OrganisationField), [Is].EqualTo("7"))
        Assert.That(Buffer.Read(ScopevisioDemoSelection.TenantField), [Is].EqualTo("fixture-tenant"))
        Buffer.Remove(ScopevisioDemoSelection.OrganisationField)
        Buffer.Remove(ScopevisioDemoSelection.TenantField)
        Assert.That(Buffer.Read("password"), [Is].EqualTo("private-fixture-password"))
        Assert.That(Buffer.Read("customer no."), [Is].EqualTo("private-fixture-customer"))
        Assert.That(Buffer.Exists(ScopevisioDemoSelection.OrganisationField), [Is].False)
    End Sub

    <TestCase("7.25", "fixture-tenant"), TestCase("7", "fixture" & vbCrLf)>
    Public Sub InvalidSelectionCannotOverwriteAnyExistingCredentialBuffer(organisation As String, tenant As String)
        Buffer.Persist("username", "existing-fixture-login")
        Buffer.Persist("password", "existing-fixture-password")
        Assert.Throws(Of ArgumentException)(Sub() ScopevisioDemoSelection.PersistInputs(Buffer, "changed-fixture-login", "changed-fixture-password", "fixture-customer", "/", organisation, tenant, "Invalid selector."))
        Assert.That(Buffer.Read("username"), [Is].EqualTo("existing-fixture-login"))
        Assert.That(Buffer.Read("password"), [Is].EqualTo("existing-fixture-password"))
        Assert.That(Buffer.Exists(ScopevisioDemoSelection.OrganisationField), [Is].False)
        Assert.That(Buffer.Exists("customer no."), [Is].False)
    End Sub

    <Test>
    Public Sub InjectedEnvironmentTakesPrecedenceWithoutReadingTheRealEnvironment()
        Buffer.Persist(ScopevisioDemoSelection.OrganisationField, "7")
        Dim names As New List(Of String)
        Dim injected As New DemoInputBuffer(DirectoryPath, "Scopevisio.Teamwork.Test", "SCOPEVISIOTEAMWORK",
            Function(name)
                names.Add(name)
                Return "8"
            End Function)
        Assert.That(injected.Read(ScopevisioDemoSelection.OrganisationField), [Is].EqualTo("8"))
        Assert.That(names, [Is].EqualTo(New String() {"TEST_SCOPEVISIOTEAMWORK_ORGANISATIONID"}))
        Assert.That(Buffer.Read(ScopevisioDemoSelection.OrganisationField), [Is].EqualTo("7"))
    End Sub
End Class
