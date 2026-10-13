Option Explicit On
Option Strict On

Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports System.Windows.Forms
Imports NUnit.Framework

<TestFixture, Apartment(ApartmentState.STA), NonParallelizable>
Public Class ScopevisioLoginSelectionGuiTest
    Private OwnedDirectory As String
    Private Buffer As Object
    Private BufferType As Type
    Private Window As Form
    Private ReadOnly Fields As String() = {"username", "password", "customer no.", "start path", "organisation id", "expected teamwork tenant id"}
    Private ReadOnly Values As String() = {"fixture-login", "fixture-password", "fixture-customer", "/", "7", "fixture-tenant"}
    Private OriginalCulture As CultureInfo
    Private DirectoryAdmitted As Boolean
    Private ReadOnly TrackedFiles As New List(Of String)
    Private ReadOnly RequestedEnvironmentNames As New List(Of String)

    <SetUp>
    Public Sub AdmitPrivateBuffer()
        OriginalCulture = CultureInfo.CurrentUICulture
        DirectoryAdmitted = False
        Window = Nothing
        Buffer = Nothing
        BufferType = Nothing
        TrackedFiles.Clear()
        RequestedEnvironmentNames.Clear()
        OwnedDirectory = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.WorkDirectory, ".task-work", "unittests_gui137_buffer_" & Guid.NewGuid().ToString("N")))
        Assert.That(Directory.Exists(OwnedDirectory), [Is].False)
        Directory.CreateDirectory(OwnedDirectory)
        DirectoryAdmitted = True
        Dim formType = GetType(Global.CompuMaster.Dms.TestDemo.ScopevisioTeamwork.LoginForm)
        BufferType = formType.Assembly.GetType(formType.Namespace & ".DemoInputBuffer", True)
        Dim environmentReader As Func(Of String, String) =
            Function(name)
                RequestedEnvironmentNames.Add(name)
                Return Nothing
            End Function
        Buffer = Activator.CreateInstance(BufferType, BindingFlags.Instance Or BindingFlags.Public Or BindingFlags.NonPublic,
            Nothing, New Object() {OwnedDirectory, "Scopevisio.Teamwork.Test", "SCOPEVISIOTEAMWORK", environmentReader}, CultureInfo.InvariantCulture)
        For index As Integer = 0 To Fields.Length - 1
            Dim path = OwnedFieldPath(Fields(index))
            Assert.That(Global.System.IO.Path.GetDirectoryName(path), [Is].EqualTo(OwnedDirectory))
            Assert.That(File.Exists(path), [Is].False)
            TrackedFiles.Add(path)
            BufferType.GetMethod("Persist", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Buffer, New Object() {Fields(index), Values(index)})
        Next
    End Sub

    <TestCase("en"), TestCase("de"), TestCase("fr"), TestCase("es"), TestCase("ar"), TestCase("he"),
     TestCase("hi"), TestCase("ja"), TestCase("zh-Hans"), TestCase("zh-Hant")>
    Public Sub InjectedSelectorsFitLocalizedLayoutAndPersistOnlyPrivateBuffers(culture As String)
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture)
        Dim formType = GetType(Global.CompuMaster.Dms.TestDemo.ScopevisioTeamwork.LoginForm)
        Dim constructor = formType.GetConstructor(BindingFlags.Instance Or BindingFlags.NonPublic, Nothing,
            New Type() {GetType(Icon), GetType(Image), BufferType}, Nothing)
        Assert.That(constructor, [Is].Not.Null, "The injected constructor must be selected before any Load event.")
        Window = DirectCast(constructor.Invoke(New Object() {Nothing, Nothing, Buffer}), Form)
        Window.AcceptButton = Nothing
        Window.ControlBox = False
        DirectCast(Control("OK"), Button).Enabled = False
        DirectCast(Control("Cancel"), Button).Enabled = False
        For Each input As TextBox In Window.Controls.OfType(Of TextBox)()
            input.ReadOnly = True
        Next
        DirectCast(Control("CheckboxPersistLoginCredentialsToDisk"), CheckBox).Enabled = False
        Window.Show()
        Application.DoEvents()
        Window.PerformLayout()
        Assert.That(Control("OrganisationIdTextBox").Text, [Is].EqualTo("7"))
        Assert.That(Control("ExpectedTeamworkTenantIdTextBox").Text, [Is].EqualTo("fixture-tenant"))
        Dim pairs = {New String() {"UsernameLabel", "UsernameTextBox"}, New String() {"PasswordLabel", "PasswordTextBox"},
            New String() {"OrganisationIdLabel", "OrganisationIdTextBox"}, New String() {"ExpectedTeamworkTenantIdLabel", "ExpectedTeamworkTenantIdTextBox"},
            New String() {"Label3", "StartPathTextBox"}}
        For Each pair In pairs
            Dim label = Control(pair(0))
            Dim input = Control(pair(1))
            Assert.That(label.Right, [Is].LessThanOrEqualTo(Window.ClientSize.Width - 12), pair(0))
            Assert.That(input.Right, [Is].LessThanOrEqualTo(Window.ClientSize.Width - 12), pair(1))
            Assert.That(label.Bottom, [Is].LessThan(input.Top), pair(0))
        Next
        Assert.That(Control("PasswordTextBox").Bottom, [Is].LessThan(Control("OrganisationIdLabel").Top))
        Assert.That(Control("OrganisationIdTextBox").Bottom, [Is].LessThan(Control("ExpectedTeamworkTenantIdLabel").Top))
        Assert.That(Control("ExpectedTeamworkTenantIdTextBox").Bottom, [Is].LessThan(Control("Label3").Top))
        Assert.That(RequestedEnvironmentNames, [Is].EquivalentTo(Fields.Select(Function(field) "TEST_SCOPEVISIOTEAMWORK_" & field.Replace(" ", "").Replace(".", "").ToUpperInvariant())))
        If culture = "de" OrElse culture = "hi" Then
            Dim artifactDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, ".task-work", "issue137-gui-proof")
            Directory.CreateDirectory(artifactDirectory)
            Dim imagePath = Path.Combine(artifactDirectory, "unittests_scope_login_" & culture & "_" & Guid.NewGuid().ToString("N") & ".png")
            Assert.That(File.Exists(imagePath), [Is].False)
            Using surface As New Bitmap(Window.Width, Window.Height)
                Window.DrawToBitmap(surface, New Rectangle(Point.Empty, surface.Size))
                surface.Save(imagePath)
            End Using
            TestContext.Progress.WriteLine("Private fixture image: " & imagePath)
        End If
        Window.Close()
        For index As Integer = 0 To Fields.Length - 1
            Assert.That(File.ReadAllText(OwnedFieldPath(Fields(index))), [Is].EqualTo(Values(index)))
        Next
    End Sub

    Private Function Control(name As String) As Control
        Return Window.Controls.Find(name, False).Single()
    End Function

    Private Function OwnedFieldPath(field As String) As String
        Return Path.GetFullPath(CStr(BufferType.GetMethod("FilePath", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(Buffer, New Object() {field})))
    End Function

    <TearDown>
    Public Sub CloseOnlyOwnedWindowsAndFiles()
        Try
            Dim close As Action = Nothing
            Dim dispose As Action = Nothing
            If Window IsNot Nothing Then
                close = Sub() Window.Close()
                dispose = Sub() Window.Dispose()
            End If
            OwnedLocalTestCleanup.Cleanup(close, dispose, OwnedDirectory, DirectoryAdmitted, TrackedFiles)
        Finally
            CultureInfo.CurrentUICulture = OriginalCulture
            Window = Nothing
            Buffer = Nothing
            BufferType = Nothing
            DirectoryAdmitted = False
            TrackedFiles.Clear()
        End Try
    End Sub
End Class
