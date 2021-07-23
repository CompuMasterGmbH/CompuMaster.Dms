Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data

Namespace Providers

    Public MustInherit Class BaseDmsProvider

        Public Enum DmsProviders As Integer
            <System.ComponentModel.Description("URL (manueller Transfer)")>
            ManualUrl = -1
            None = 0
            <System.ComponentModel.Description("WebDAV (OwnCloud, NextCloud, etc.)")>
            WebDAV = 1
            <System.ComponentModel.Description("Scopevisio Teamwork")>
            Scopevisio = 20
            CenterDevice = 21
        End Enum

        Public MustOverride ReadOnly Property DocumentationGuideFiBuUploadsFileName As String

        Public MustOverride ReadOnly Property DmsProviderID As DmsProviders

        Public MustOverride ReadOnly Property Name As String

        ''' <summary>
        ''' The url to access the web API endpoint 
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property WebApiDefaultUrl As String

        ''' <summary>
        ''' Available configuration options for customer order administration
        ''' </summary>
        Public Enum UrlCustomizationType As Byte
            ''' <summary>
            ''' Webservice endpoint exactly matches to WebApiDefaultUrl
            ''' </summary>
            WebApiUrlNotCustomizable = 1
            WebApiUrlMustBeCustomized = 2
            WebApiUrlCanBeCustomized = 3
        End Enum

        Public MustOverride ReadOnly Property WebApiUrlCustomization As UrlCustomizationType

        ''' <summary>
        ''' Available configuration options for customer reference in user credentials
        ''' </summary>
        Public Enum UserCustomerReferenceType As Byte
            ''' <summary>
            ''' Webservice endpoint exactly matches to WebApiDefaultUrl
            ''' </summary>
            WithoutCustomerReference = 1
            CustomerReferenceRequired = 2
            CustomerReferenceOptional = 3
        End Enum

        Public MustOverride ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType

        Protected MustOverride Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String

        Public Enum SearchItemType As Byte
            AllItems = 0
            Collections = 1
            Folders = 2
            Files = 3
        End Enum

        Public MustOverride Function ListRemoteItem(remotePath As String) As DmsResourceItem

        Public Overridable Function RemoteItemExists(remotePath As String) As Boolean
            Dim RemoteItem As DmsResourceItem
            RemoteItem = Me.ListRemoteItem(remotePath)
            Return RemoteItem IsNot Nothing
        End Function

        Public MustOverride Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)

        Public MustOverride Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)

        Public Overridable Function ListAllCollectionItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Item In Me.ListAllRemoteItems(remoteFolderPath, SearchItemType.Collections)
                If Item.ItemType = DmsResourceItem.ItemTypes.Collection Then
                    Result.Add(Item)
                End If
            Next
            Return Result
        End Function

        Public Overridable Function ListAllFolderItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Item In Me.ListAllRemoteItems(remoteFolderPath, SearchItemType.Folders)
                If Item.ItemType = DmsResourceItem.ItemTypes.Folder Then
                    Result.Add(Item)
                End If
            Next
            Return Result
        End Function

        Public Overridable Function ListAllFileItems(remoteFolderPath As String) As List(Of DmsResourceItem)
            Dim Result As New List(Of DmsResourceItem)
            For Each Item In Me.ListAllRemoteItems(remoteFolderPath, SearchItemType.Files)
                If Item.ItemType = DmsResourceItem.ItemTypes.File Then
                    Result.Add(Item)
                End If
            Next
            Return Result
        End Function

        Public Overridable Function ListAllCollectionNames(remoteFolderPath As String) As List(Of String)
            Dim Result As New List(Of String)
            For Each Item In Me.ListAllCollectionItems(remoteFolderPath)
                If Item.ItemType = DmsResourceItem.ItemTypes.Collection Then
                    Result.Add(Item.Name)
                End If
            Next
            Return Result
        End Function

        Public Overridable Function ListAllFolderNames(remoteFolderPath As String) As List(Of String)
            Dim Result As New List(Of String)
            For Each Item In Me.ListAllFolderItems(remoteFolderPath)
                If Item.ItemType = DmsResourceItem.ItemTypes.Folder Then
                    Result.Add(Item.Name)
                End If
            Next
            Return Result
        End Function

        Public Overridable Function ListAllFileNames(remoteFolderPath As String) As List(Of String)
            Dim Result As New List(Of String)
            For Each Item In Me.ListAllFileItems(remoteFolderPath)
                If Item.ItemType = DmsResourceItem.ItemTypes.File Then
                    Result.Add(Item.Name)
                End If
            Next
            Return Result
        End Function

        Public MustOverride Function FindCollectionById(id As String) As DmsResourceItem

        Public MustOverride Function FindFolderById(id As String) As DmsResourceItem

        Public MustOverride Function FindDocumentById(id As String) As DmsResourceItem

        Public MustOverride Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials

        Public MustOverride Sub UploadFile(remoteFilePath As String, localFilePath As String)

        Public MustOverride Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?)

        Public MustOverride Sub Copy(remoteSourcePath As String, remoteDestinationPath As String, recursive As Boolean, overwrite As Boolean)

        Public MustOverride Sub Move(remoteSourcePath As String, remoteDestinationPath As String)

        ''' <summary>
        ''' Delete a remote item (folder, collection or file)
        ''' </summary>
        ''' <param name="remotePath"></param>
        Public MustOverride Sub DeleteRemoteItem(remotePath As String)

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remotePath"></param>
        ''' <param name="expectedItemType"></param>
        Public Overridable Sub DeleteRemoteItem(remotePath As String, expectedItemType As DmsResourceItem.ItemTypes)
            Dim Item As DmsResourceItem = Me.ListRemoteItem(remotePath)
            Me.DeleteRemoteItem(Item, expectedItemType)
        End Sub

        ''' <summary>
        ''' Delete a remote item (folder, collection or file)
        ''' </summary>
        ''' <param name="remoteItem"></param>
        Public MustOverride Sub DeleteRemoteItem(remoteItem As DmsResourceItem)

        ''' <summary>
        ''' Delete a remote item if its item type matches with the expected item type
        ''' </summary>
        ''' <param name="remoteItem"></param>
        ''' <param name="expectedItemType"></param>
        Public Overridable Sub DeleteRemoteItem(remoteItem As DmsResourceItem, expectedItemType As DmsResourceItem.ItemTypes)
            If remoteItem.ItemType <> expectedItemType Then
                Throw New ArgumentException("ItemType " & expectedItemType.ToString & " expected, but was " & remoteItem.ItemType.ToString, NameOf(remoteItem))
            Else
                Me.DeleteRemoteItem(remoteItem)
            End If
        End Sub

        Public MustOverride Sub CreateFolder(remoteFilePath As String)

        Public MustOverride Sub CreateCollection(remoteCollectionName As String)

        Public MustOverride ReadOnly Property DirectorySeparator As Char

        ''' <summary>
        ''' The name of the root element to request a folder/collection listing at root
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property BrowseInRootFolderName() As String

        Public MustOverride Sub Authorize(dmsProfile As IDmsLoginProfile)

        ''' <summary>
        ''' Combines folder names to a path
        ''' </summary>
        ''' <param name="basePath"></param>
        ''' <param name="paths"></param>
        ''' <returns></returns>
        Public Overridable Function CombinePath(basePath As String, ParamArray paths As String()) As String
            Dim Result As String = basePath
            For Each Path In paths
                If Path = Me.DirectorySeparator Then
                    Result = ""
                ElseIf Path.StartsWith(Me.DirectorySeparator) Then
                    Result = Path.Substring(1)
                ElseIf Result = "" Then
                    Result = Path
                ElseIf Result.EndsWith(Me.DirectorySeparator) Then
                    Result &= Path
                Else
                    Result &= Me.DirectorySeparator & Path
                End If
            Next
            Return Result
        End Function

        ''' <summary>
        ''' The parent folder name for a path
        ''' </summary>
        ''' <param name="path"></param>
        ''' <returns></returns>
        Public Overridable Function ParentDirectoryPath(path As String) As String
            If path = Nothing OrElse path = Me.DirectorySeparator Then
                Return Nothing
            ElseIf path.Contains(Me.DirectorySeparator) = False Then
                Return ""
            ElseIf path.EndsWith(Me.DirectorySeparator) Then
                Return path.Substring(0, path.LastIndexOf(Me.DirectorySeparator, path.Length - 1))
            Else
                Return path.Substring(0, path.LastIndexOf(Me.DirectorySeparator))
            End If
        End Function

        ''' <summary>
        ''' DMS provider supports collections (concept of collections can be understood as "intelligent folders", see CenterDevice/Scopevisio Teamwork)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsCollections As Boolean

        ''' <summary>
        ''' DMS provider supports sharing API
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsSharingSetup As Boolean

        ''' <summary>
        ''' DMS provider supports configuration of root folder and subfolders for the different purposes (input files, reports)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsSubFolderConfiguration As Boolean

        Public Enum RuntimeAccessTypes As Byte
            ''' <summary>
            ''' No configuration, no runtime access
            ''' </summary>
            None = 0
            ''' <summary>
            ''' Configuration available, but no runtime access
            ''' </summary>
            ConfigurationOnly = 1
            ''' <summary>
            ''' Configuration available with access on runtime to remote server using API calls
            ''' </summary>
            ConfigurationAndRuntimeAccess = 2
            ''' <summary>
            ''' Configuration available with manual access on runtime to remote server (starts e.g. a browser to open a 3rd party remote DMS system)
            ''' </summary>
            ConfigurationAndStartSeparateProcessWithDmsServerAddress = 3
        End Enum

        ''' <summary>
        ''' DMS provider supports authentication and transfer of files on runtime (False indicates a configuration-only provider)
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes

        Public MustOverride Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
        Public MustOverride Sub UpdateLink(shareInfo As DmsLink)
        Public MustOverride Sub DeleteLink(shareInfo As DmsLink)

        Public MustOverride Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
        Public MustOverride Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
        Public MustOverride Sub UpdateSharing(shareInfo As DmsShareForGroup)
        Public MustOverride Sub DeleteSharing(shareInfo As DmsShareForGroup)
        Public MustOverride Sub UpdateSharing(shareInfo As DmsShareForUser)
        Public MustOverride Sub DeleteSharing(shareInfo As DmsShareForUser)

        Public MustOverride Function GetAllGroups() As List(Of DmsGroup)
        Public MustOverride Function GetAllUsers() As List(Of DmsUser)

        ''' <summary>
        ''' A runtime variable which contains the user ID after login at remote DMS system
        ''' </summary>
        ''' <returns></returns>
        Public MustOverride ReadOnly Property CurrentContextUserID As String

    End Class

End Namespace