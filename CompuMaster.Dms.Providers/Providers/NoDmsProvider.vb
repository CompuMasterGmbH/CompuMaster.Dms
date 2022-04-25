Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Namespace Providers

    Public Class NoDmsProvider
        Inherits BaseDmsProvider

        <Obsolete("Implementation in wrong solution")> Public Overrides ReadOnly Property DocumentationGuideFiBuUploadsFileName As String
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.None
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "None"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlNotCustomizable
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType
            Get
                Return UserCustomerReferenceType.WithoutCustomerReference
            End Get
        End Property

        Public Overrides ReadOnly Property DirectorySeparator As Char
            Get
                Return "/"c
            End Get
        End Property

        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Throw New NotSupportedException()
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsSubFolderConfiguration As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes
            Get
                Return RuntimeAccessTypes.None
            End Get
        End Property

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub UploadFile(remoteFilePath As String, localFilePath As String)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream))
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As Date?)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub Copy(remoteSourcePath As String, remoteDestinationPath As String, recursive As Boolean, overwrite As Boolean)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub Move(remoteSourcePath As String, remoteDestinationPath As String)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub DeleteRemoteItem(remotePath As String)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub CreateFolder(remoteFilePath As String)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub CreateCollection(remoteCollectionName As String)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub Authorize(dmsProfile As IDmsLoginProfile)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub UpdateLink(shareInfo As DmsLink)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForGroup)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForUser)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForGroup)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForUser)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Throw New NotSupportedException()
        End Function

        Public Overrides Function FindCollectionById(id As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        Public Overrides Function FindFolderById(id As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        Public Overrides Function FindFileById(id As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Throw New NotSupportedException()
        End Function

        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            Throw New NotSupportedException()
        End Function

        Public Overrides Function GetAllGroups() As List(Of DmsGroup)
            Throw New NotSupportedException()
        End Function

        Public Overrides Function GetAllUsers() As List(Of DmsUser)
            Throw New NotSupportedException()
        End Function

        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Throw New NotSupportedException
        End Function
    End Class

End Namespace
