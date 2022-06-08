Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Namespace Providers

    ''' <summary>
    ''' WebDAV
    ''' </summary>
    Public Class WebDavDmsProvider
        Inherits BaseDmsProvider

        Private WebDavClient As Global.WebDav.WebDavClient

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.WebDAV
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "WebDAV"
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiDefaultUrl As String
            Get
                Return Nothing
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUrlCustomization As UrlCustomizationType
            Get
                Return UrlCustomizationType.WebApiUrlMustBeCustomized
            End Get
        End Property

        Public Overrides ReadOnly Property WebApiUserCustomerReferenceRequirement As UserCustomerReferenceType
            Get
                Return UserCustomerReferenceType.WithoutCustomerReference
            End Get
        End Property

        Protected Overrides Function CustomizedWebApiUrl(loginCredentials As BaseDmsLoginCredentials) As String
            Return loginCredentials.BaseUrl 'e.g. https://extranet.compumaster.de/owncloud/remote.php/dav/files/gitlab-runner-bierdeckel/
        End Function

        Public Overrides ReadOnly Property BrowseInRootFolderName() As String = ""

        ''' <summary>
        ''' The webdav url that is considered as root directory (always contains a trailing slash "/")
        ''' </summary>
        ''' <returns></returns>
        Public Property CustomWebApiUrl As String

        Public Overloads Sub Authorize(loginCredentials As WebDavLoginCredentials)
            Dim Url As String = Me.CustomizedWebApiUrl(loginCredentials)
            Dim ClientParams As New Global.WebDav.WebDavClientParams() With
            {
            .BaseAddress = New System.Uri(Url),
            .Credentials = New System.Net.NetworkCredential(loginCredentials.Username, loginCredentials.Password)
            }
            Me.WebDavClient = New Global.WebDav.WebDavClient(ClientParams)
            Me._AuthorizedUser = loginCredentials.Username
            If Url.EndsWith("/") Then
                Me.CustomWebApiUrl = Url
            Else
                Me.CustomWebApiUrl = Url & "/"
            End If
            'Force request to evaluate correct credentials already on runtime of this method
            Dim PropfindTask As Task(Of Global.WebDav.PropfindResponse) = Me.WebDavClient.Propfind(ClientParams.BaseAddress)
            PropfindTask.Wait()
            If PropfindTask.IsCompleted AndAlso PropfindTask.Result.IsSuccessful Then
            Else
                If PropfindTask.Exception IsNot Nothing Then
                    Throw New InvalidOperationException("Authentification for user """ & loginCredentials.Username & """ failed: " & PropfindTask.Exception.Message)
                ElseIf PropfindTask.Result.StatusCode = 401 Then
                    Throw New Data.DmsUserAuthenticationException("Authentification for user """ & loginCredentials.Username & """ failed: " & PropfindTask.Result.StatusCode & " " & PropfindTask.Result.Description)
                Else
                    Throw New InvalidOperationException("Authentification for user """ & loginCredentials.Username & """ failed: " & PropfindTask.Result.StatusCode & " " & PropfindTask.Result.Description)
                End If
            End If
        End Sub

        Protected _AuthorizedUser As String

        Public Overrides Sub ResetCachesForRemoteItems(remoteFolderPath As String, searchType As SearchItemType)
            'no caches present, so nothing to do here
        End Sub

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            If remotePath Is Nothing Then
                Throw New ArgumentNullException(NameOf(remotePath))
            End If
            Dim PropfindParams As New Global.WebDav.PropfindParameters With
            {
                .ApplyTo = Global.WebDav.ApplyTo.Propfind.ResourceOnly
            }
            Dim PropfindTask As Task(Of Global.WebDav.PropfindResponse) = Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remotePath, PropfindParams)
            PropfindTask.Wait()
            If PropfindTask.IsCompleted AndAlso PropfindTask.Result.IsSuccessful Then
                Dim Res As Global.WebDav.WebDavResource = PropfindTask.Result.Resources(0)
                Return Me.CreateDmsResourceItem(Res)
            Else
                If PropfindTask.Exception IsNot Nothing Then
                    Throw New InvalidOperationException("Listing of WebDAV resource at " & remotePath & " failed: " & PropfindTask.Exception.Message)
                ElseIf PropfindTask.Result.StatusCode = 404 Then
                    'Directory/File not found
                    Return Nothing
                Else
                    Throw New InvalidOperationException("Listing of WebDAV resource at " & remotePath & " failed: " & PropfindTask.Result.StatusCode & " " & PropfindTask.Result.Description)
                End If
            End If
        End Function

        Public Overrides Function FindCollectionById(id As String) As DmsResourceItem
            Throw New NotImplementedException
        End Function

        Public Overrides Function FindFolderById(id As String) As DmsResourceItem
            Throw New NotImplementedException
        End Function

        Public Overrides Function FindFileById(id As String) As DmsResourceItem
            Throw New NotImplementedException
        End Function

        Private Function CreateDmsResourceItem(res As Global.WebDav.WebDavResource) As DmsResourceItem
            Dim Result As New DmsResourceItem With {
                        .Name = res.DisplayName,
                        .CreatedOnLocalTime = res.CreationDate.GetValueOrDefault,
                        .LastModificationOnLocalTime = res.LastModifiedDate.GetValueOrDefault,
                        .IsHidden = res.IsHidden,
                        .ContentLength = res.ContentLength.GetValueOrDefault,
                        .ProviderSpecificHashOrETag = res.ETag
                    }
            'Assign additional fields
            If res.Uri.ToString.StartsWith(Me.CustomWebApiUrl) Then
                Result.FullName = res.Uri.ToString.Substring(Me.CustomWebApiUrl.Length)
            ElseIf res.Uri.ToString.StartsWith(New System.Uri(Me.CustomWebApiUrl).PathAndQuery) Then
                Result.FullName = res.Uri.ToString.Substring(New System.Uri(Me.CustomWebApiUrl).PathAndQuery.Length)
                If Result.FullName.EndsWith(Me.DirectorySeparator) Then
                    Result.FullName = Result.FullName.Substring(0, Result.FullName.Length - 1)
                End If
            Else
                Throw New InvalidOperationException("Sub items of " & Me.CustomWebApiUrl & " found outside of this path: " & res.Uri.ToString)
            End If
            If Result.Name = Nothing AndAlso Result.FullName <> Nothing Then
                Result.Name = Result.FullName.Substring(Result.FullName.LastIndexOf(Me.DirectorySeparator) + 1)
            End If
            If res.IsCollection AndAlso (Result.FullName = Nothing OrElse Result.FullName = Me.DirectorySeparator) Then
                Result.ItemType = DmsResourceItem.ItemTypes.Root
            ElseIf res.IsCollection Then
                Result.ItemType = DmsResourceItem.ItemTypes.Folder
            Else
                Result.ItemType = DmsResourceItem.ItemTypes.File
            End If
            'Normalize field content
            If Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(res.Uri.ToString) = Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(Me.CustomWebApiUrl) Then
                Result.Name = ""
            ElseIf Result.Name <> Nothing AndAlso Result.Name.EndsWith(Me.DirectorySeparator) Then
                Result.Name = Result.Name.Substring(0, Result.Name.Length - 1)
            End If
            If Result.FullName.EndsWith(Me.DirectorySeparator) Then
                Result.FullName = Result.FullName.Substring(0, Result.FullName.Length - 1)
            End If
            'Assign calculated fields
            Dim LastDirSeparatorPosition As Integer = Result.FullName.LastIndexOf(Me.DirectorySeparator)
            If LastDirSeparatorPosition >= 0 Then
                Result.Folder = Result.FullName.Substring(0, LastDirSeparatorPosition)
            Else
                Result.Folder = ""
            End If
            'Decoded URLs
            Result.Name = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.Name))
            Result.Folder = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.Folder))
            Result.Collection = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.Collection))
            Result.FullName = Tools.NotNullOrEmptyStringValue(System.Net.WebUtility.UrlDecode(Result.FullName))
            Return Result
        End Function

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Dim PropfindParams As New Global.WebDav.PropfindParameters With {
            .ApplyTo = Global.WebDav.ApplyTo.Propfind.ResourceAndChildren
        }
            Dim PropfindTask As Task(Of Global.WebDav.PropfindResponse) = Me.WebDavClient.Propfind(Me.CustomWebApiUrl & remoteFolderPath, PropfindParams)
            PropfindTask.Wait()
            If PropfindTask.IsCompleted AndAlso PropfindTask.Result.IsSuccessful Then
                Dim Result As New List(Of DmsResourceItem)
                For Each res In PropfindTask.Result.Resources
                    Dim AddThisItem As Boolean = False
                    Select Case searchType
                        Case SearchItemType.AllItems
                            AddThisItem = True
                        Case SearchItemType.Collections
                            AddThisItem = False
                        Case SearchItemType.Folders
                            AddThisItem = res.IsCollection
                        Case SearchItemType.Files
                            AddThisItem = Not res.IsCollection
                    End Select
                    If AddThisItem Then
                        Dim NewItem As DmsResourceItem = Me.CreateDmsResourceItem(res)
                        If NewItem.ItemType = DmsResourceItem.ItemTypes.Folder AndAlso Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(NewItem.FullName) = Me.PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(remoteFolderPath) Then
                            'don't list folder item
                        Else
                            Result.Add(NewItem)
                        End If
                    End If
                Next
                Return Result
            Else
                If PropfindTask.Exception IsNot Nothing Then
                    Throw New InvalidOperationException("Listing of WebDAV resource at " & remoteFolderPath & " failed: " & PropfindTask.Exception.Message)
                Else
                    Throw New InvalidOperationException("Listing of WebDAV resource at " & remoteFolderPath & " failed: " & PropfindTask.Result.StatusCode & " " & PropfindTask.Result.Description)
                End If
            End If
        End Function

        Private Function PathWithTrailingDirectorySeparatorExceptRootPathAlwaysReducedToEmptyString(path As String) As String
            If path = Nothing OrElse path = "/" Then
                Return ""
            ElseIf path.EndsWith(Me.DirectorySeparator) Then
                Return path
            Else
                Return path & Me.DirectorySeparator
            End If
        End Function

        'Public Overrides Function ListAllCollectionItems(remoteFolderPath As String) As List(Of DmsResourceItem)
        '    Return MyBase.ListAllCollectionItems(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllCollectionNames(remoteFolderPath As String) As List(Of String)
        '    Return MyBase.ListAllCollectionNames(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFileItems(remoteFolderPath As String) As List(Of DmsResourceItem)
        '    Return MyBase.ListAllFileItems(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFileNames(remoteFolderPath As String) As List(Of String)
        '    Return MyBase.ListAllFileNames(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFolderItems(remoteFolderPath As String) As List(Of DmsResourceItem)
        '    Return MyBase.ListAllFolderItems(remoteFolderPath)
        'End Function
        '
        'Public Overrides Function ListAllFolderNames(remoteFolderPath As String) As List(Of String)
        '    Return MyBase.ListAllFolderNames(remoteFolderPath)
        'End Function

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Return New WebDavLoginCredentials
        End Function

        Public Overrides Sub UploadFile(remoteFilePath As String, localFilePath As String)
            Dim PutParams As New Global.WebDav.PutFileParameters
            Dim fs As System.IO.FileStream = Nothing
            Try
                fs = System.IO.File.OpenRead(localFilePath)
                Dim UploadTask = Me.WebDavClient.PutFile(Me.CustomWebApiUrl & remoteFilePath, fs, PutParams)
                UploadTask.Wait()
                CheckTaskResultForErrors(UploadTask, Nothing, remoteFilePath, "Upload failed", ExceptionTypeForItemType.File)
            Finally
                If fs IsNot Nothing Then
                    fs.Close()
                    fs.Dispose()
                End If
            End Try
        End Sub

        Public Overrides Sub UploadFile(remoteFilePath As String, binaryData As Func(Of System.IO.Stream))
            Dim PutParams As New Global.WebDav.PutFileParameters
            Dim UploadTask = Me.WebDavClient.PutFile(Me.CustomWebApiUrl & remoteFilePath, binaryData(), PutParams)
            UploadTask.Wait()
            CheckTaskResultForErrors(UploadTask, Nothing, remoteFilePath, "Upload failed", ExceptionTypeForItemType.File)
        End Sub


        Protected Shared Function StreamToByteArray(inputStream As System.IO.Stream) As Byte()
            Dim bytes = New Byte(16383) {}
            Using memoryStream = New System.IO.MemoryStream()
                Dim count As Integer
                Do
                    count = inputStream.Read(bytes, 0, bytes.Length)
                    memoryStream.Write(bytes, 0, count)
                Loop While count > 0
                Return memoryStream.ToArray()
            End Using
        End Function

        Protected Overridable Sub WriteResponseStreamToDisk(response As Task(Of Global.WebDav.WebDavStreamResponse), localFilePath As String)
            response.Wait()
            If response.IsCompleted = False OrElse response.Result.IsSuccessful = False Then Throw New InvalidOperationException("Download failed: not completed/successfull")
            Dim FileData As Byte() = StreamToByteArray(response.Result.Stream)
            System.IO.File.WriteAllBytes(localFilePath, FileData)
        End Sub

        Public Overrides Sub DownloadFile(remoteFilePath As String, localFilePath As String, lastModificationDateOnLocalTime As DateTime?)
            Using response = Me.WebDavClient.GetRawFile(Me.CustomWebApiUrl & remoteFilePath) ' get a file without processing from the server
                WriteResponseStreamToDisk(response, localFilePath)
                If lastModificationDateOnLocalTime.HasValue AndAlso lastModificationDateOnLocalTime.Value <> Nothing Then System.IO.File.SetLastWriteTime(localFilePath, lastModificationDateOnLocalTime.Value)
            End Using
        End Sub

        Public Overridable Sub DownloadProcessedFile(remoteFilePath As String, localFilePath As String)
            Using response = Me.WebDavClient.GetProcessedFile(Me.CustomWebApiUrl & remoteFilePath) ' get a file that can be processed by the server
                WriteResponseStreamToDisk(response, localFilePath)
            End Using
        End Sub

        ''' <summary>
        ''' Check for successful run of a task
        ''' </summary>
        ''' <param name="completedTask"></param>
        ''' <param name="remoteSourcePath">Optional value, required for actions requiring existance of source item</param>
        ''' <param name="remoteDestinationPath"></param>
        ''' <param name="ioExceptionMessage"></param>
        ''' <param name="conflictItemType">Decides on exception type if a HTTP status code 409 is reported</param>
        Protected Sub CheckTaskResultForErrors(completedTask As Task(Of WebDav.WebDavResponse), remoteSourcePath As String, remoteDestinationPath As String, ioExceptionMessage As String, conflictItemType As ExceptionTypeForItemType)
            If remoteDestinationPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
            Select Case completedTask.Status
                Case TaskStatus.Faulted, TaskStatus.RanToCompletion
                    'ok - continue checks below
                Case TaskStatus.Created, TaskStatus.WaitingForActivation
                    Throw New InvalidOperationException("Task not started")
                Case TaskStatus.WaitingForChildrenToComplete, TaskStatus.WaitingToRun, TaskStatus.Running
                    Throw New InvalidOperationException("Task not finished")
                Case TaskStatus.Canceled
                    Throw New TaskCanceledException()
                Case Else
                    Throw New InvalidOperationException("Task status invalid")
            End Select
            If completedTask.Status <> TaskStatus.RanToCompletion OrElse completedTask.IsFaulted OrElse completedTask.Result.IsSuccessful = False Then
                If completedTask.Result Is Nothing Then
                    Throw New System.IO.IOException(ioExceptionMessage)
                ElseIf completedTask.Result.StatusCode = 404 Then
                    '404 Not found
                    Throw New RessourceNotFoundException(remoteDestinationPath)
                ElseIf completedTask.Result.StatusCode = 409 Then
                    '409 Conflict
                    If remoteDestinationPath = Nothing Then
                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                    ElseIf Me.RemoteItemExists(remoteDestinationPath) Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileAlreadyExistsException(remoteDestinationPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryAlreadyExistsException(remoteDestinationPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case Else
                                Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        End Select
                    Else
                        Dim ParentPath As String = Me.ParentDirectoryPath(remoteDestinationPath)
                        If ParentPath <> Nothing AndAlso Me.RemoteItemExists(ParentPath) = False Then
                            Throw New DirectoryNotFoundException(ParentPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        Else
                            Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        End If
                    End If
                ElseIf completedTask.Result.StatusCode = 412 Then
                    '412 Precondition failed
                    If Me.RemoteItemExists(remoteSourcePath) = False Then
                        Select Case conflictItemType
                            Case ExceptionTypeForItemType.File
                                Throw New FileNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case ExceptionTypeForItemType.Directory
                                Throw New DirectoryNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                            Case Else
                                Throw New RessourceNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                        End Select
                    ElseIf remoteSourcePath <> Nothing Then
                        Throw New System.IO.IOException(ioExceptionMessage & ", but remote item exists: " & remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                    Else
                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                    End If
                ElseIf completedTask.Exception IsNot Nothing Then
                    Throw New System.IO.IOException(ioExceptionMessage, completedTask.Exception)
                Else
                    Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
                End If
            End If
        End Sub

        Protected Overrides Sub CopyFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.Overwrite = allowOverwrite.GetValueOrDefault
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            CopyTask.Wait()
            CheckTaskResultForErrors(CopyTask, remoteSourcePath, remoteDestinationPath, "Copy task failed", ExceptionTypeForItemType.File)
        End Sub

        Protected Overrides Async Function CopyFileItemAsync(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?) As Task
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.Overwrite = allowOverwrite.GetValueOrDefault
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            Await CopyTask
            CheckTaskResultForErrors(CopyTask, remoteSourcePath, remoteDestinationPath, "Copy task failed", ExceptionTypeForItemType.File)
        End Function

        Protected Overrides Sub CopyDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.ApplyTo = Global.WebDav.ApplyTo.Copy.ResourceAndAncestors
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            CopyTask.Wait()
            CheckTaskResultForErrors(CopyTask, remoteSourcePath, remoteDestinationPath, "Copy task failed", ExceptionTypeForItemType.Directory)
        End Sub

        Protected Overrides Async Function CopyDirectoryItemAsync(remoteSourcePath As String, remoteDestinationPath As String) As Task
            Dim CopyParams As New Global.WebDav.CopyParameters()
            CopyParams.ApplyTo = Global.WebDav.ApplyTo.Copy.ResourceAndAncestors
            Dim CopyTask = Me.WebDavClient.Copy(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, CopyParams)
            Await CopyTask
            CheckTaskResultForErrors(CopyTask, remoteSourcePath, remoteDestinationPath, "Copy task failed", ExceptionTypeForItemType.Directory)
        End Function

        Protected Overrides Sub MoveFileItem(remoteSourcePath As String, remoteDestinationPath As String, allowOverwrite As Boolean?)
            Dim MoveTask = Me.WebDavClient.Move(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, New Global.WebDav.MoveParameters() With {.Overwrite = allowOverwrite.GetValueOrDefault})
            MoveTask.Wait()
            CheckTaskResultForErrors(MoveTask, remoteSourcePath, remoteDestinationPath, "Move failed", ExceptionTypeForItemType.File)
        End Sub

        Protected Overrides Sub MoveDirectoryItem(remoteSourcePath As String, remoteDestinationPath As String)
            Dim MoveTask = Me.WebDavClient.Move(Me.CustomWebApiUrl & remoteSourcePath, Me.CustomWebApiUrl & remoteDestinationPath, New Global.WebDav.MoveParameters() With {.Overwrite = False})
            MoveTask.Wait()
            CheckTaskResultForErrors(MoveTask, remoteSourcePath, remoteDestinationPath, "Move failed", ExceptionTypeForItemType.Directory)
        End Sub

        Public Overrides Sub DeleteRemoteItem(remoteFilePath As String)
            Dim DelTask = Me.WebDavClient.Delete(Me.CustomWebApiUrl & remoteFilePath)
            DelTask.Wait()
            CheckTaskResultForErrors(DelTask, Nothing, remoteFilePath, "Delete failed", ExceptionTypeForItemType.Unspecified)
        End Sub

        Public Overrides Sub DeleteRemoteItem(remoteItem As DmsResourceItem)
            Dim DelTask = Me.WebDavClient.Delete(Me.CustomWebApiUrl & remoteItem.FullName)
            DelTask.Wait()
            CheckTaskResultForErrors(DelTask, Nothing, remoteItem.FullName, "Delete failed", ExceptionTypeForItemType.Unspecified)
        End Sub

        Public Overrides Sub CreateFolder(remoteFilePath As String)
            Dim CreateTask = Me.WebDavClient.Mkcol(Me.CustomWebApiUrl & remoteFilePath)
            CreateTask.Wait()
            CheckTaskResultForErrors(CreateTask, Nothing, remoteFilePath, "Create folder failed", ExceptionTypeForItemType.Directory)
        End Sub

        Public Overrides Sub CreateDirectory(remoteDirectoryPath As String)
            Me.CreateFolder(remoteDirectoryPath)
        End Sub

        Public Overrides Sub CreateCollection(remoteCollectionName As String)
            Throw New NotSupportedException("Collections are not supported by WebDAV")
        End Sub

        Public Overrides ReadOnly Property DirectorySeparator As Char
            Get
                Return "/"c
            End Get
        End Property

        Public Overrides Sub Authorize(dmsProfile As Data.IDmsLoginProfile)
            Dim Credentials As WebDavLoginCredentials = CType(Me.CreateNewCredentialsInstance(), WebDavLoginCredentials)
            Credentials.BaseUrl = dmsProfile.ServerAddress
            Credentials.Username = dmsProfile.UserName
            Credentials.Password = dmsProfile.Password
            Me.Authorize(Credentials)
        End Sub

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

        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            Throw New NotImplementedException()
        End Function

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Sub UpdateLink(shareInfo As DmsLink)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForGroup)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForUser)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForGroup)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForUser)
            Throw New NotImplementedException()
        End Sub

        Public Overrides Function GetAllGroups() As List(Of DmsGroup)
            Throw New NotImplementedException()
        End Function

        Public Overrides Function GetAllUsers() As List(Of DmsUser)
            Throw New NotImplementedException()
        End Function

        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Return Me._AuthorizedUser
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsSubFolderConfiguration As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsRuntimeAccessToRemoteServer As RuntimeAccessTypes
            Get
                Return RuntimeAccessTypes.ConfigurationAndRuntimeAccess
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsFilesInRootFolder As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsNonUniqueRemoteItems As Boolean
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace