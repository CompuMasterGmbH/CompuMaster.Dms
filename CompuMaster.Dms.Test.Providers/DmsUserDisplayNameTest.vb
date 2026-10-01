Option Explicit On
Option Strict On

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
        Public Sub MissingUserNameFallsBackToId(name As String)
            Dim User As New DmsUser With {.ID = "user-id", .Name = name}

            ClassicAssert.AreEqual("user-id", User.DisplayName)
            Dim Sharing As New DmsShareForUser(Nothing, User, True, True, True, True, True, True)
            StringAssert.StartsWith("user-id (", Sharing.ToString())
        End Sub

        <Test>
        Public Sub NewDisplayNameResolverTakesPrecedenceOverLegacyResolver()
            Dim User As New DmsUser With {
                .ID = "user-id",
                .Provider = New NoDmsProvider,
                .GetDisplayName = AddressOf ResolveDisplayName,
                .GetName = AddressOf ResolveLegacyName
            }

            ClassicAssert.AreEqual("Alice Example", User.DisplayName)
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
        Public Sub LegacyNameResolverAndDisplayNameSetterRemainCompatible()
            Dim User As New DmsUser With {
                .ID = "user-id",
                .Provider = New NoDmsProvider,
                .GetName = AddressOf ResolveDisplayName
            }

            ClassicAssert.AreEqual("Alice Example", User.DisplayName)
            User.DisplayName = "Updated Name"
            ClassicAssert.AreEqual("Updated Name", User.Name)
        End Sub

        <Test>
        Public Sub LegacyNameValueStillSuppliesDisplayName()
            Dim User As New DmsUser With {.ID = "user-id", .Name = "Legacy Name"}

            ClassicAssert.AreEqual("Legacy Name", User.DisplayName)
        End Sub

        Private Shared Function ResolveDisplayName(provider As BaseDmsProvider, id As String) As String
            Return " Alice Example "
        End Function

        Private Shared Function ResolveBlankDisplayName(provider As BaseDmsProvider, id As String) As String
            Return " "
        End Function

        Private Shared Function ResolveLegacyName(provider As BaseDmsProvider, id As String) As String
            Return "Legacy Name"
        End Function

    End Class

End Namespace
