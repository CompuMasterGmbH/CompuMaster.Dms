Option Explicit On
Option Strict On

Imports System.Text.Json
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

Namespace DmsProviderTests

    <TestFixture>
    Public Class CenterDeviceFolderMetadataTest

        <Test>
        Public Sub FolderListingPreservesKnownAndUnknownChildFlags()
            Dim Json As String = "{""folders"":[{""id"":""empty"",""has-subfolders"":false}," &
                                 "{""id"":""nested"",""has-subfolders"":true},{""id"":""unknown""}]}"
            Dim Response As FolderMetadataResponse = JsonSerializer.Deserialize(Of FolderMetadataResponse)(Json)

            ClassicAssert.AreEqual(3, Response.Folders.Count)
            ClassicAssert.IsFalse(Response.Folders(0).HasSubFoldersMetadata.Value)
            ClassicAssert.IsTrue(Response.Folders(1).HasSubFoldersMetadata.Value)
            ClassicAssert.IsFalse(Response.Folders(2).HasSubFoldersMetadata.HasValue)
            StringAssert.Contains("has-subfolders", CenterDeviceFolderMetadataClient.ListingFields)
        End Sub

        <Test>
        Public Sub MetadataClientCanReuseTheConfiguredCenterDeviceConnection()
            Dim ApiClient As New CenterDeviceClient(Nothing, New LocalRestClientConfiguration, Nothing)

            ClassicAssert.IsNotNull(CenterDeviceFolderMetadataClient.Create(ApiClient))
        End Sub

        Private NotInheritable Class LocalRestClientConfiguration
            Implements IRestClientConfiguration

            Public ReadOnly Property BaseAddress As String Implements IRestClientConfiguration.BaseAddress
                Get
                    Return "https://example.invalid/"
                End Get
            End Property

            Public ReadOnly Property UserAgent As String Implements IRestClientConfiguration.UserAgent
                Get
                    Return "CompuMaster.Dms.Test"
                End Get
            End Property
        End Class

    End Class

End Namespace
