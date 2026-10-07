Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Namespace Providers

    ''' <summary>Represents manual URL selection without remote DMS operations.</summary>
    Public Class ManualUrlDmsProvider
        Inherits BaseDmsProvider

        ''' <inheritdoc/>
#Disable Warning BC42356 ' In dieser asynchronen Methode fehlten die "Await"-Operatoren, sie wird daher synchron ausgeführt.
        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.ManualUrl
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property Name As String
            Get
                Return "URL (Manual transfer)"
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return ""
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlCanBeCustomized
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType
            Get
                Return UserCustomerReferenceType.CustomerReferenceOptional
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property DirectorySeparator As Char
            Get
                Return "/"c
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property BrowseInRootFolderName As String
            Get
                Return "/"
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsCollections As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Throw New NotSupportedException()
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsSubFolderConfiguration As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes
            Get
                Return RuntimeAccessTypes.ConfigurationAndStartSeparateProcessWithDmsServerAddress
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides ReadOnly Property SupportsNonUniqueRemoteItems As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <inheritdoc/>
        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub UploadFile(remoteFilePath As String, localFilePath As String)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream))
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As Date?)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Async Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Protected Overrides Sub CopyDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Async Function CopyDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Protected Overrides Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Protected Overrides Sub MoveDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteRemoteItem(remotePath As String)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub CreateDirectory(remoteDirectoryPath As String)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub CreateFolder(remoteFilePath As String)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub CreateCollection(remoteCollectionName As String)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub Authorize(dmsProfile As IDmsLoginProfile)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub UpdateLink(shareInfo As DmsLink)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForGroup)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForUser)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForGroup)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForUser)
            Throw New NotSupportedException()
        End Sub

        ''' <inheritdoc/>
        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function FindCollectionById(id As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function FindFolderById(id As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function FindFileById(id As String) As DmsResourceItem
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function GetAllGroups() As List(Of DmsGroup)
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Public Overrides Function GetAllUsers() As List(Of DmsUser)
            Throw New NotSupportedException()
        End Function

        ''' <inheritdoc/>
        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Throw New NotSupportedException
        End Function
#Enable Warning BC42356 ' In dieser asynchronen Methode fehlten die "Await"-Operatoren, sie wird daher synchron ausgeführt.

    End Class

End Namespace
