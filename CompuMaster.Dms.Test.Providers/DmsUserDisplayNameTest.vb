Option Explicit On
Option Strict On

Imports System.ComponentModel
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers
Imports NUnit.Framework
Imports NUnit.Framework.Legacy

Namespace DmsProviderTests

    <TestFixture>
    Public Class DmsUserDisplayNameTest

        <TestCase(Nothing)>
        <TestCase("")>
        <TestCase(" ")>
        <TestCase(" " & vbTab & " ")>
        Public Sub MissingDisplayNameFallsBackToId(name As String)
            Dim User As New DmsUser With {.ID = "user-id", .DisplayName = name}

            ClassicAssert.AreEqual("user-id", User.DisplayName)
            Dim Sharing As New DmsShareForUser(Nothing, User, True, True, True, True, True, True)
            StringAssert.StartsWith("user-id (", Sharing.ToString())
        End Sub

        <Test>
        Public Sub DisplayNameAndLoginNameResolveIndependently()
            Dim User As New DmsUser With {
                .ID = "user-id",
                .Provider = New NoDmsProvider,
                .GetDisplayName = AddressOf ResolveDisplayName,
                .GetLoginName = AddressOf ResolveLoginName
            }

            ClassicAssert.AreEqual("Alice Example", User.DisplayName)
            ClassicAssert.AreEqual("alice@example.org", User.LoginName)
        End Sub

        <Test>
        Public Sub BlankDisplayNameResolverFallsBackToId()
            Dim User As New DmsUser With {
                .ID = "user-id",
                .Provider = New NoDmsProvider,
                .GetDisplayName = AddressOf ResolveBlankDisplayName
            }

            ClassicAssert.AreEqual("user-id", User.DisplayName)
        End Sub

        <Test>
        Public Sub ExplicitDisplayNameOverridesLookup()
            Dim User As New DmsUser With {
                .ID = "user-id",
                .Provider = New NoDmsProvider,
                .GetDisplayName = AddressOf ResolveDisplayName
            }

            ClassicAssert.AreEqual("Alice Example", User.DisplayName)
            User.DisplayName = "Updated Name"
            ClassicAssert.AreEqual("Updated Name", User.DisplayName)
            User.DisplayName = Nothing
            ClassicAssert.AreEqual("Alice Example", User.DisplayName)
        End Sub

        <Test>
        Public Sub LoginNameDoesNotReplaceIdAsDisplayFallback()
            Dim User As New DmsUser With {.ID = "user-id", .LoginName = "alice@example.org"}

            ClassicAssert.AreEqual("user-id", User.DisplayName)
            User.LoginName = Nothing
            ClassicAssert.IsNull(User.LoginName)
        End Sub

        <Test>
        Public Sub LegacyMembersAreHiddenAndRejectNewSourceUse()
            For Each memberName As String In New String() {"Name", "GetName"}
                Dim member = GetType(DmsUser).GetProperty(memberName)
                ClassicAssert.IsNotNull(member)
                Dim obsolete = CType(Attribute.GetCustomAttribute(member, GetType(ObsoleteAttribute)), ObsoleteAttribute)
                ClassicAssert.IsTrue(obsolete.IsError)
                If memberName = "Name" Then ClassicAssert.AreEqual("Check ID, LoginName or DisplayName", obsolete.Message)
                Dim browse = CType(Attribute.GetCustomAttribute(member, GetType(EditorBrowsableAttribute)), EditorBrowsableAttribute)
                ClassicAssert.AreEqual(EditorBrowsableState.Never, browse.State)
            Next

            For Each memberName As String In New String() {"Provider", "GetDisplayName", "GetLoginName", "GetEMailAddress"}
                ClassicAssert.IsNotNull(GetType(DmsUser).GetProperty(memberName))
                ClassicAssert.IsNull(GetType(DmsUser).GetField(memberName))
            Next
        End Sub

        Private Shared Function ResolveDisplayName(provider As BaseDmsProvider, id As String) As String
            Return " Alice Example "
        End Function

        Private Shared Function ResolveBlankDisplayName(provider As BaseDmsProvider, id As String) As String
            Return " "
        End Function

        Private Shared Function ResolveLoginName(provider As BaseDmsProvider, id As String) As String
            Return " alice@example.org "
        End Function

    End Class

End Namespace
