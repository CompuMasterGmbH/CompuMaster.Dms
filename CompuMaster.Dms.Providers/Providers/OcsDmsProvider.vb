Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Namespace Providers

    ''' <summary>
    ''' Open Collaboration Service (OCS)
    ''' </summary>
    ''' <remarks>https://www.freedesktop.org/wiki/Specifications/open-collaboration-services-1.7/</remarks>
    Public Class OcsDmsProvider
        Inherits WebDavDmsProvider

        Public Overrides ReadOnly Property DmsProviderID As DmsProviders
            Get
                Return DmsProviders.OCS
            End Get
        End Property

        Public Overrides ReadOnly Property Name As String
            Get
                Return "OpenCollaborationService"
            End Get
        End Property

        Protected Friend OcsClient As CompuMaster.Ocs.OcsClient

        Public Overloads Sub Authorize(loginCredentials As OcsLoginCredentials)
            'Authorize for OCS API part
            OcsClient = New CompuMaster.Ocs.OcsClient(loginCredentials.BaseUrl, loginCredentials.Username, loginCredentials.Password)
            Try
                _AuthorizedUserEMailAddress = OcsClient.GetUserAttributes(OcsClient.AuthorizedUserID).EMail
            Catch ex As CompuMaster.Ocs.Exceptions.OcsResponseException
                If ex.HttpStatusCode = 401 Then
                    Throw New CompuMaster.Dms.Data.DmsUserAuthenticationException("Authentification failed via OCS API with HTTP status code " & ex.HttpStatusCode & " and OCS status code " & ex.OcsStatusCode & " (" & ex.OcsStatusText & ")", ex)
                Else
                    Throw New CompuMaster.Dms.Data.DmsUserErrorMessageException("Authentification failure via OCS API with HTTP status code " & ex.HttpStatusCode & " and OCS status code " & ex.OcsStatusCode & " (" & ex.OcsStatusText & ")", ex)
                End If
            Catch ex As CompuMaster.Ocs.Exceptions.ResponseError
                If ex.HttpStatusCode = 401 Then
                    Throw New CompuMaster.Dms.Data.DmsUserAuthenticationException("Authentification failed via OCS API with HTTP status code " & ex.HttpStatusCode, ex)
                Else
                    Throw New CompuMaster.Dms.Data.DmsUserErrorMessageException("Authentification failure via OCS API with HTTP status code " & ex.HttpStatusCode, ex)
                End If
            Catch ex As Exception
                Throw New CompuMaster.Dms.Data.DmsUserErrorMessageException("Authentification failure via OCS API", ex)
            End Try
            'Authorize for WebDAV part
            Dim WebDavCredentials As New WebDavLoginCredentials() With
                {
                .BaseUrl = OcsClient.WebDavBaseUrl,
                .DmsProvider = loginCredentials.DmsProvider,
                .CustomerInstance = loginCredentials.CustomerInstance,
                .EncryptionProvider = loginCredentials.EncryptionProvider,
                .Username = loginCredentials.Username,
                .Password = loginCredentials.Password
                }
            MyBase.Authorize(WebDavCredentials)
        End Sub

        Public Overrides Function CreateNewCredentialsInstance() As BaseDmsLoginCredentials
            Return New OcsLoginCredentials
        End Function

        Public Overrides Sub Authorize(dmsProfile As Data.IDmsLoginProfile)
            Dim Credentials As OcsLoginCredentials = CType(Me.CreateNewCredentialsInstance(), OcsLoginCredentials)
            Credentials.BaseUrl = dmsProfile.ServerAddress
            Credentials.Username = dmsProfile.UserName
            Credentials.Password = dmsProfile.Password
            Me.Authorize(Credentials)
        End Sub

        Public Overrides ReadOnly Property SupportsSharingSetup As Boolean
            Get
                Return True
            End Get
        End Property

        Private Function OcsPermissionSet(shareInfo As DmsShareBase) As CompuMaster.Ocs.Core.OcsPermission
            Dim Result As CompuMaster.Ocs.Core.OcsPermission
            If shareInfo.AllowDelete Then Result = Result Or Ocs.Core.OcsPermission.Delete
            If shareInfo.AllowDownload Then Result = Result Or Ocs.Core.OcsPermission.Read
            If shareInfo.AllowEdit Then Result = Result Or Ocs.Core.OcsPermission.Update
            If shareInfo.AllowShare Then Result = Result Or Ocs.Core.OcsPermission.Share
            If shareInfo.AllowUpload Then Result = Result Or Ocs.Core.OcsPermission.Create
            If shareInfo.AllowView Then Result = Result Or Ocs.Core.OcsPermission.Read
            Return Result
        End Function

        Public Overrides Function CreateLink(dmsResource As DmsResourceItem, shareInfo As DmsLink) As DmsLink
            'Throw New NotImplementedException("TODO in following line: argument clarification of: public_upload")
            Dim CreatedLink As Ocs.Types.PublicShare = Me.OcsClient.ShareWithLink(dmsResource.FullName, Me.OcsPermissionSet(shareInfo), shareInfo.Password, public_upload:=Ocs.Core.OcsBoolParam.None)
            Dim Result As New DmsLink(dmsResource, CreatedLink.ShareId.ToString, Me, Nothing)
            Result.WebUrl = CreatedLink.Url
            Result.ID = CreatedLink.ShareId.ToString
            Return Result
        End Function

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForGroup)
            Dim CreatedShare As Ocs.Types.GroupShare = Me.OcsClient.ShareWithGroup(dmsResource.FullName, shareInfo.Group.Name, Me.OcsPermissionSet(shareInfo))
            shareInfo.ID = CreatedShare.ShareId.ToString
            shareInfo.ParentDmsResourceItem.ExtendedInfosGroupSharings.Add(shareInfo)
        End Sub

        Public Overrides Sub CreateSharing(dmsResource As DmsResourceItem, shareInfo As DmsShareForUser)
            'Throw New NotImplementedException("TODO in following line: argument clarification of: remoteUser")
            Dim CreatedShare As Ocs.Types.UserShare = Me.OcsClient.ShareWithUser(dmsResource.FullName, shareInfo.User.Name, Me.OcsPermissionSet(shareInfo), remoteUser:=Ocs.Core.OcsBoolParam.None)
            shareInfo.ID = CreatedShare.ShareId.ToString
            shareInfo.ParentDmsResourceItem.ExtendedInfosUserSharings.Add(shareInfo)
        End Sub

        Public Overrides Sub UpdateLink(shareInfo As DmsLink)
            'Throw New NotImplementedException("TODO in following line: argument clarification of: public_upload")
            Me.OcsClient.UpdateShare(Integer.Parse(shareInfo.ID), Me.OcsPermissionSet(shareInfo), shareInfo.Password, public_upload:=Ocs.Core.OcsBoolParam.None)
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForGroup)
            Me.OcsClient.UpdateShare(Integer.Parse(shareInfo.ID), Me.OcsPermissionSet(shareInfo))
        End Sub

        Public Overrides Sub UpdateSharing(shareInfo As DmsShareForUser)
            Me.OcsClient.UpdateShare(Integer.Parse(shareInfo.ID), Me.OcsPermissionSet(shareInfo))
        End Sub

        Public Overrides Sub DeleteLink(shareInfo As DmsLink)
            Me.OcsClient.DeleteShare(Integer.Parse(shareInfo.ID))
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForGroup)
            Me.OcsClient.DeleteShare(Integer.Parse(shareInfo.ID))
        End Sub

        Public Overrides Sub DeleteSharing(shareInfo As DmsShareForUser)
            Me.OcsClient.DeleteShare(Integer.Parse(shareInfo.ID))
        End Sub

        Public Overrides Function GetAllGroups() As List(Of DmsGroup)
            Dim Result As New List(Of DmsGroup)
            Dim QueriedGroups As List(Of String) = Me.OcsClient.GetUserGroups(Me.OcsClient.AuthorizedUserID)
            For MyCounter As Integer = 0 To QueriedGroups.Count - 1
                Result.Add(New DmsGroup() With
                           {
                           .ID = QueriedGroups(MyCounter),
                           .Provider = Me
                           })
            Next
            Return Result
        End Function

        Public Overrides Function GetAllUsers() As List(Of DmsUser)
            Dim Result As New List(Of DmsUser)
            Dim QueriedUsers As List(Of String) = Me.OcsClient.SearchUsers()
            For MyCounter As Integer = 0 To QueriedUsers.Count - 1
                Result.Add(New DmsUser() With
                           {
                           .ID = QueriedUsers(MyCounter),
                           .EMailAddress = QueriedUsers(MyCounter),
                           .Provider = Me
                           })
            Next
            Return Result
        End Function

        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Return Me._AuthorizedUserID
            End Get
        End Property

        Public Overrides Function ListAllRemoteItems(remoteFolderPath As String, searchType As SearchItemType) As List(Of DmsResourceItem)
            Return MyBase.ListAllRemoteItems(remoteFolderPath, searchType)
        End Function

        Public Overrides Function ListRemoteItem(remotePath As String) As DmsResourceItem
            Dim Result As DmsResourceItem = MyBase.ListRemoteItem(remotePath)
            If Result IsNot Nothing Then
                Result.FillSharingInfos = Sub()
                                              Dim ShareInfos As List(Of CompuMaster.Ocs.Types.Share) = Me.OcsClient.GetShares(PathWithLeadingSlash(remotePath), Ocs.Core.OcsBoolParam.None, Ocs.Core.OcsBoolParam.False)
                                              FillDmsResourceItemShareData(Result, ShareInfos)
                                          End Sub
            End If
            Return Result
        End Function

        Sub FillDmsResourceItemShareData(item As DmsResourceItem, ocsShareData As List(Of CompuMaster.Ocs.Types.Share))
            item.ExtendedInfosUserSharings = New List(Of CompuMaster.Dms.Data.DmsShareForUser)
            item.ExtendedInfosGroupSharings = New List(Of CompuMaster.Dms.Data.DmsShareForGroup)
            item.ExtendedInfosLinks = New List(Of CompuMaster.Dms.Data.DmsLink)
            For Each ShareInfo As CompuMaster.Ocs.Types.Share In ocsShareData
                Throw New NotImplementedException("ShareInfo from CompuMaster.Ocs.Types.Share")
                If GetType(CompuMaster.Dms.Data.DmsShareForUser).IsInstanceOfType(ShareInfo) Then
                    ShareInfo.AdvancedProperties
                    item.ExtendedInfosUserSharings.Add(New CompuMaster.Dms.Data.DmsShareForUser(item, ShareInfo.AdvancedProperties.ShareWithUserID, ...))
                End If
            Next
        End Sub

        Private Shared Function PathWithLeadingSlash(remotePath As String) As String
            If remotePath <> Nothing Then
                Return "/" & remotePath
            Else
                Return remotePath
            End If

        End Function

        '''' <summary>
        '''' Check for successful run of a task
        '''' </summary>
        '''' <param name="completedTask"></param>
        '''' <param name="remoteSourcePath">Optional value, required for actions requiring existance of source item</param>
        '''' <param name="remoteDestinationPath"></param>
        '''' <param name="ioExceptionMessage"></param>
        '''' <param name="conflictItemType">Decides on exception type if a HTTP status code 409 is reported</param>
        'Protected Sub CheckTaskResultForErrors(completedTask As Task(Of WebDav.WebDavResponse), remoteSourcePath As String, remoteDestinationPath As String, ioExceptionMessage As String, conflictItemType As ExceptionTypeForItemType)
        '    If remoteDestinationPath = Nothing Then Throw New ArgumentNullException(NameOf(remoteDestinationPath))
        '    Select Case completedTask.Status
        '        Case TaskStatus.Faulted, TaskStatus.RanToCompletion
        '            'ok - continue checks below
        '        Case TaskStatus.Created, TaskStatus.WaitingForActivation
        '            Throw New InvalidOperationException("Task not started")
        '        Case TaskStatus.WaitingForChildrenToComplete, TaskStatus.WaitingToRun, TaskStatus.Running
        '            Throw New InvalidOperationException("Task not finished")
        '        Case TaskStatus.Canceled
        '            Throw New TaskCanceledException()
        '        Case Else
        '            Throw New InvalidOperationException("Task status invalid")
        '    End Select
        '    If completedTask.Status <> TaskStatus.RanToCompletion OrElse completedTask.IsFaulted OrElse completedTask.Result.IsSuccessful = False Then
        '        If completedTask.Result Is Nothing Then
        '            Throw New System.IO.IOException(ioExceptionMessage)
        '        ElseIf completedTask.Result.StatusCode = 404 Then
        '            '404 Not found
        '            Throw New RessourceNotFoundException(remoteDestinationPath)
        '        ElseIf completedTask.Result.StatusCode = 409 Then
        '            '409 Conflict
        '            If remoteDestinationPath = Nothing Then
        '                Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '            ElseIf Me.RemoteItemExists(remoteDestinationPath) Then
        '                Select Case conflictItemType
        '                    Case ExceptionTypeForItemType.File
        '                        Throw New FileAlreadyExistsException(remoteDestinationPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                    Case ExceptionTypeForItemType.Directory
        '                        Throw New DirectoryAlreadyExistsException(remoteDestinationPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                    Case Else
        '                        Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                End Select
        '            Else
        '                Dim ParentPath As String = Me.ParentDirectoryPath(remoteDestinationPath)
        '                If ParentPath <> Nothing AndAlso Me.RemoteItemExists(ParentPath) = False Then
        '                    Throw New DirectoryNotFoundException(ParentPath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                Else
        '                    Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                End If
        '            End If
        '        ElseIf completedTask.Result.StatusCode = 412 Then
        '            '412 Precondition failed
        '            If Me.RemoteItemExists(remoteSourcePath) = False Then
        '                Select Case conflictItemType
        '                    Case ExceptionTypeForItemType.File
        '                        Throw New FileNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                    Case ExceptionTypeForItemType.Directory
        '                        Throw New DirectoryNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                    Case Else
        '                        Throw New RessourceNotFoundException(remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '                End Select
        '            ElseIf remoteSourcePath <> Nothing Then
        '                Throw New System.IO.IOException(ioExceptionMessage & ", but remote item exists: " & remoteSourcePath, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '            Else
        '                Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '            End If
        '        ElseIf completedTask.Exception IsNot Nothing Then
        '            Throw New System.IO.IOException(ioExceptionMessage, completedTask.Exception)
        '        Else
        '            Throw New System.IO.IOException(ioExceptionMessage, New ResponseStatusCodeException(completedTask.Result.StatusCode, completedTask.Result.Description))
        '        End If
        '    End If
        'End Sub

    End Class

End Namespace