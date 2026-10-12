Option Explicit On
Option Strict On

Imports System.Net
Imports System.Text.Json
Imports CenterDevice.Rest
Imports CenterDevice.Rest.Clients
Imports CenterDevice.Rest.Clients.Folders
Imports CenterDevice.Rest.Clients.OAuth
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy
Imports RestSharp
Imports RestSharp.Serializers.Json

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

        <TestCase("true", True), TestCase("false", False), TestCase("null", Nothing), TestCase("omitted", Nothing)>
        Public Sub DefaultSerializerUsesBaseBindingOrPublishedSdkFallback(value As String, expected As Boolean?)
            Dim folder As FolderWithChildMetadata = ReadFolder(value)
            Assert.That(folder.HasSubFoldersMetadata, [Is].EqualTo(expected))
            Assert.That(folder.SubFolders, [Is].Null)
            Dim propertyInfo = GetType(Folder).GetProperty("HasSubFoldersServerInfo", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic)
            Dim baseBindsMetadata As Boolean = propertyInfo.IsDefined(GetType(Serialization.JsonIncludeAttribute), False)
            Assert.That(folder.HasSubFolders, [Is].EqualTo(If(baseBindsMetadata, expected, Nothing)))
            If value <> "omitted" Then
                Dim captured As Boolean = folder.UnboundMetadata IsNot Nothing AndAlso folder.UnboundMetadata.ContainsKey("has-subfolders")
                Assert.That(captured, [Is].EqualTo(Not baseBindsMetadata), "Only the published SDK needs the unbound metadata fallback.")
            End If
        End Sub

        <TestCase("true", False), TestCase("false", False), TestCase("null", False), TestCase("omitted", False),
         TestCase("true", True), TestCase("false", True), TestCase("null", True), TestCase("omitted", True)>
        Public Sub FetchedChildrenOverrideBothMetadataBindings(value As String, populated As Boolean)
            Dim folder As FolderWithChildMetadata = ReadFolder(value)
            Dim original As Boolean? = folder.HasSubFoldersMetadata
            folder.SubFolders = New List(Of Folder)
            If populated Then folder.SubFolders.Add(New Folder With {.Id = "child"})
            Assert.That(folder.HasSubFoldersMetadata, [Is].EqualTo(populated))
            folder.SubFolders = Nothing
            Assert.That(folder.HasSubFoldersMetadata, [Is].EqualTo(original))
        End Sub

        <TestCase("true", True), TestCase("false", False), TestCase("null", Nothing), TestCase("omitted", Nothing)>
        Public Sub DefaultRestSharpListingPreservesMetadataAndRequestFields(value As String, expected As Boolean?)
            Dim member As String = If(value = "omitted", "", ",""has-subfolders"":" & value)
            Dim json As String = "{""folders"":[{""id"":""selected"",""name"":""Folder"",""collection"":""collection"",""parent"":""parent"",""users"":{""visible"":[""user""],""not-visible-count"":1}" & member & "}]}"
            Dim client As New ListingClient(json)
            Dim result = client.GetFoldersWithMetadata("context", "collection", "parent")
            Assert.That(result.Count, [Is].EqualTo(1))
            Assert.That(result(0).HasSubFoldersMetadata, [Is].EqualTo(expected))
            Assert.That(result(0).SubFolders, [Is].Null)
            Assert.That(result(0).Collection, [Is].EqualTo("collection"))
            Assert.That(result(0).Parent, [Is].EqualTo("parent"))
            Assert.That(result(0).IsShared, [Is].True)
            Assert.That(client.Request.Method, [Is].EqualTo(Method.Get))
            Assert.That(client.Request.Resource, [Is].EqualTo("v2/folders/"))
            Assert.That(client.Request.Parameters.Single(Function(p) p.Name = "fields").Value,
                        [Is].EqualTo("collection,id,name,parent,users,groups,link,has-subfolders"))
            Assert.That(client.Request.Parameters.Single(Function(p) p.Name = "collection").Value, [Is].EqualTo("collection"))
            Assert.That(client.Request.Parameters.Single(Function(p) p.Name = "parent").Value, [Is].EqualTo("parent"))
        End Sub

        <Test>
        Public Sub ManuallySuppliedMetadataAlsoYieldsToFetchedChildren()
            Dim folder As New FolderWithChildMetadata With {.HasSubFoldersMetadata = True, .SubFolders = New List(Of Folder)}
            Assert.That(folder.HasSubFoldersMetadata, [Is].False)
            folder.SubFolders = Nothing
            Assert.That(folder.HasSubFoldersMetadata, [Is].True)
        End Sub

        Private Shared Function ReadFolder(value As String) As FolderWithChildMetadata
            Dim member As String = If(value = "omitted", "", ",""has-subfolders"":" & value)
            Return New SystemTextJsonSerializer().Deserialize(Of FolderWithChildMetadata)(
                New RestResponse With {.Content = "{""id"":""folder""" & member & "}", .ContentType = "application/json"})
        End Function

        Private NotInheritable Class ListingClient
            Inherits CenterDeviceFolderMetadataClient

            Private ReadOnly json As String
            Friend Request As RestRequest

            Friend Sub New(json As String)
                MyBase.New(New Authorization, New LocalRestClientConfiguration, Nothing, "v2/")
                Me.json = json
            End Sub

            Protected Overrides Function Execute(Of T As New)(info As OAuthInfo, request As RestRequest) As RestResponse(Of T)
                Me.Request = request
                Dim response As New RestResponse(request) With {.Content = json, .ContentType = "application/json", .StatusCode = HttpStatusCode.OK}
                Return New RestResponse(Of T)(request) With {.StatusCode = HttpStatusCode.OK, .Data = client.Serializers.DeserializeContent(Of T)(response)}
            End Function
        End Class

        Private NotInheritable Class Authorization
            Implements IOAuthInfoProvider

            Public Function GetOAuthInfo(userId As String) As OAuthInfo Implements IOAuthInfoProvider.GetOAuthInfo
                Return New OAuthInfo With {.UserId = userId, .access_token = "isolated-fixture"}
            End Function
        End Class

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
