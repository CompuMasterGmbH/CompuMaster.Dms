Option Explicit On
Option Strict On

Imports NUnit.Framework

Public Class ScopevisioTeamworkProviderTest
    Inherits BaseDmsProviderTestBase

    Private Function CreateLoginProfile() As DmsLoginProfile
        Dim username As String = Settings.InputLine("username")
        Dim customerno As String = Settings.InputLine("customer no.")
        Dim password As String = Settings.InputLine("password")

        Return New DmsLoginProfile() With {
                            .DmsProvider = CompuMaster.Dms.Providers.BaseDmsProvider.DmsProviders.Scopevisio,
                            .CustomerInstance = customerno,
                            .Username = username,
                            .Password = password
                            }
    End Function

    Protected Overrides Function UninitializedDmsProvider() As Dms.Providers.BaseDmsProvider
        Static Result As Dms.Providers.BaseDmsProvider
        If Result Is Nothing Then
            Result = Dms.Providers.CreateDmsProviderInstance(Me.CreateLoginProfile.DmsProvider)
        End If
        Return Result
    End Function

    Protected Overrides Function LoggedInDmsProvider() As Dms.Providers.BaseDmsProvider
        Static Result As Dms.Providers.BaseDmsProvider
        If Result Is Nothing Then
            Result = Dms.Providers.CreateAuthorizedDmsProviderInstance(Me.CreateLoginProfile)
        End If
        Return Result
    End Function

    Protected Overrides ReadOnly Property IgnoreSslErrors As Boolean
        Get
            Return False
        End Get
    End Property

    Private Const TestDirName As String = "/ZZZ_UnitTests_CM.Dms"

    <OneTimeSetUp> Public Sub CreateTestDir()
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If Provider.CollectionExists(TestDirName) = False Then
            Provider.CreateCollection(TestDirName)
        End If
    End Sub

    <OneTimeTearDown> Public Sub CleanupTestDir()
        Dim Provider As Dms.Providers.BaseDmsProvider = Me.LoggedInDmsProvider
        If Provider.CollectionExists(TestDirName) Then
            Provider.DeleteRemoteItem(TestDirName)
        End If
    End Sub

    Public Overrides ReadOnly Property RemoteFilesMustExist As String() = New String() {}
    Public Overrides ReadOnly Property RemoteFoldersMustExist As String() = New String() {}
    Public Overrides ReadOnly Property RemoteCollectionsMustExist As String() = New String() {TestDirName}
    Public Overrides ReadOnly Property RemoteItemsMustNotExist As String() = New String() {"/gibt's nicht"}
    Public Overrides ReadOnly Property RemoteFoldersWithFiles As String() = New String() {}
    Public Overrides ReadOnly Property RemoteFoldersWithSubFolders As String() = New String() {}
    Public Overrides ReadOnly Property DownloadTestFilesText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overrides ReadOnly Property DownloadTestFilesBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}
    Public Overrides ReadOnly Property UploadTestFilesAndCleanupAgainText As KeyValuePair(Of String, String)() = New KeyValuePair(Of String, String)() {}
    Public Overrides ReadOnly Property UploadTestFilesAndCleanupAgainBinary As KeyValuePair(Of String, Byte())() = New KeyValuePair(Of String, Byte())() {}

End Class
