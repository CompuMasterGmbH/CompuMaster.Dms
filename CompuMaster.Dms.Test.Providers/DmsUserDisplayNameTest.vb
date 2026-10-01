Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Data
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

Namespace DmsProviderTests

    <TestFixture>
    Public Class DmsUserDisplayNameTest

        <TestCase(Nothing)>
        <TestCase("")>
        <TestCase(" ")>
        <TestCase(" " & vbTab & " ")>
        Public Sub MissingUserNameFallsBackToId(name As String)
            Dim User As New DmsUser With {.ID = "user-id", .Name = name}

            ClassicAssert.AreEqual("user-id", User.DisplayName)
            Dim Sharing As New DmsShareForUser(Nothing, User, True, True, True, True, True, True)
            StringAssert.StartsWith("user-id (", Sharing.ToString())
        End Sub

    End Class

End Namespace
