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

        Protected Friend OcsClient As CompuMaster.Ocs.Client

        Public Overloads Sub Authorize(loginCredentials As OcsLoginCredentials)
            Dim WebDavCredentials As New WebDavLoginCredentials() With
                {
                .BaseUrl = loginCredentials.BaseUrl,
                .DmsProvider = loginCredentials.DmsProvider,
                .CustomerInstance = loginCredentials.CustomerInstance,
                .EncryptionProvider = loginCredentials.EncryptionProvider,
                .Username = loginCredentials.Username,
                .Password = loginCredentials.Password
                }
            MyBase.Authorize(WebDavCredentials)
            OcsClient = New CompuMaster.Ocs.Client(loginCredentials.BaseUrl, loginCredentials.Username, loginCredentials.Password)
            Try
                If OcsClient.Exists(Me.BrowseInRootFolderName) Then
                    Throw New CompuMaster.Dms.Data.DirectoryNotFoundException(Me.BrowseInRootFolderName)
                End If
                'Catch ex As CompuMaster.Ocs.Exceptions.OCSResponseError
                '    If ex.StatusCode = "401" Then
                '        Throw New CompuMaster.Dms.Data.DmsUserAuthenticationException("Authentification failed via OCS API with status code")
                '    Else
                '        Throw New CompuMaster.Dms.Data.DmsUserErrorMessageException("Access to root directory failed via OCS API with status code " & ex.StatusCode)
                '    End If
            Catch ex As CompuMaster.Ocs.Exceptions.ResponseError
                If ex.StatusCode = "401" Then
                    Throw New CompuMaster.Dms.Data.DmsUserAuthenticationException("Authentification failed via OCS API with status code")
                Else
                    Throw New CompuMaster.Dms.Data.DmsUserErrorMessageException("Access to root directory failed via OCS API with status code " & ex.StatusCode)
                End If
            Catch ex As Exception
                Throw New CompuMaster.Dms.Data.DmsUserErrorMessageException("Access to root directory failed via OCS API")
            End Try
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
            Dim Result As New List(Of DmsGroup)
            Dim QueriedGroups As List(Of String) = Me.OcsClient.GetUserGroups(Me.OcsClient.AuthorizedUserID)
            For MyCounter As Integer = 0 To QueriedGroups.Count - 1
                Result.Add(New DmsGroup() With
                           {
                           .ID = QueriedGroups(MyCounter)
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
                           .EMailAddress = QueriedUsers(MyCounter)
                           })
            Next
            Return Result
        End Function

        Public Overrides ReadOnly Property CurrentContextUserID As String
            Get
                Return Me._AuthorizedUser
            End Get
        End Property

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