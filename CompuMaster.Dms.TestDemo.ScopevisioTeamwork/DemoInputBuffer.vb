Option Explicit On
Option Strict On

Imports System.IO
Imports System.Security.Cryptography
Imports System.Text

'The production wrapper supplies the usual directory/environment; tests inject private storage and a fake environment.
Friend NotInheritable Class DemoInputBuffer
    Private ReadOnly DirectoryPath As String
    Private ReadOnly AppTitle As String
    Private ReadOnly EnvironmentPrefix As String
    Private ReadOnly ReadEnvironment As Func(Of String, String)

    Friend Sub New(directoryPath As String, appTitle As String, environmentPrefix As String, readEnvironment As Func(Of String, String))
        If String.IsNullOrEmpty(directoryPath) Then Throw New ArgumentException("A buffer directory is required.", NameOf(directoryPath))
        If readEnvironment Is Nothing Then Throw New ArgumentNullException(NameOf(readEnvironment))
        Me.DirectoryPath = directoryPath
        Me.AppTitle = appTitle
        Me.EnvironmentPrefix = environmentPrefix
        Me.ReadEnvironment = readEnvironment
    End Sub

    Friend Function FilePath(fieldName As String) As String
        'Preserve the exact historical ASCII/MD5 field-key mapping and uppercase hexadecimal name.
        Using hash = MD5.Create()
            Dim suffix = String.Concat(hash.ComputeHash(Encoding.ASCII.GetBytes(fieldName)).Select(Function(value) value.ToString("X2")))
            Return Path.Combine(DirectoryPath, "~Buffer." & AppTitle & "." & suffix & ".tmp")
        End Using
    End Function

    Friend Function Read(fieldName As String) As String
        Dim name = "TEST_" & EnvironmentPrefix & "_" & fieldName.Replace(" ", "").Replace(".", "").ToUpperInvariant()
        Dim environmentValue = ReadEnvironment(name)
        If Not String.IsNullOrWhiteSpace(environmentValue) Then Return environmentValue
        Dim file = FilePath(fieldName)
        Return If(Global.System.IO.File.Exists(file), Global.System.IO.File.ReadAllText(file), Nothing)
    End Function

    Friend Sub Persist(fieldName As String, value As String)
        File.WriteAllText(FilePath(fieldName), value)
    End Sub

    Friend Function Exists(fieldName As String) As Boolean
        Return File.Exists(FilePath(fieldName))
    End Function

    Friend Sub Remove(fieldName As String)
        Dim file = FilePath(fieldName)
        If Global.System.IO.File.Exists(file) Then Global.System.IO.File.Delete(file)
    End Sub
End Class
