Option Explicit On
Option Strict On

Imports CenterDevice.Rest.Clients.Common
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class CenterDeviceSharingResponseTest
    <Test>
    Public Sub EmptyFailureListsIndicateSuccessfulSharing()
        CenterDeviceDmsProviderBase.ValidateSharingResponse(New SharingResponse With {.FailedUsers = New List(Of String)(), .FailedGroups = New List(Of String)()})
    End Sub

    <Test>
    Public Sub AbsentResponseRetainsNoContentSuccess()
        CenterDeviceDmsProviderBase.ValidateSharingResponse(Nothing)
    End Sub

    <Test>
    Public Sub AbsentFailureFieldsIndicateSuccessfulSharing()
        CenterDeviceDmsProviderBase.ValidateSharingResponse(New SharingResponse())
    End Sub

    <TestCase(False), TestCase(True)>
    Public Sub AnyFailedPrincipalRejectsPartialSharingSuccess(groupFailure As Boolean)
        Dim response As New SharingResponse With {.FailedUsers = New List(Of String)(), .FailedGroups = New List(Of String)()}
        If groupFailure Then
            response.FailedGroups.Add("failed-group")
        Else
            response.FailedUsers.Add("failed-user")
        End If
        Assert.Throws(Of InvalidOperationException)(Sub() CenterDeviceDmsProviderBase.ValidateSharingResponse(response))
    End Sub

    <Test>
    Public Sub FailedUserIsDetectedWhenTheOtherFailureListIsAbsent()
        Assert.Throws(Of InvalidOperationException)(Sub() CenterDeviceDmsProviderBase.ValidateSharingResponse(New SharingResponse With {.FailedUsers = New List(Of String) From {"failed-user"}}))
    End Sub
End Class
