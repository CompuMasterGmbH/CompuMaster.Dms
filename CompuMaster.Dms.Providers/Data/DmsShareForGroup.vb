Option Explicit On
Option Strict On

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types

    Public Class DmsShareForGroup
        Inherits DmsShareBase
        Implements ICloneable

        Public Sub New(parentDmsResourceItem As DmsResourceItem, group As DmsGroup, allowView As Boolean, allowDownload As Boolean, allowEdit As Boolean, allowUpload As Boolean, allowDelete As Boolean, allowShare As Boolean)
            MyBase.New(parentDmsResourceItem, allowView, allowDownload, allowEdit, allowUpload, allowDelete, allowShare)
            Me.Group = group
        End Sub

        ''' <summary>
        ''' The ID of a share
        ''' </summary>
        ''' <returns></returns>
        Public Property ID As String

        Public Property Group As DmsGroup

        Protected Overrides Sub Initialize()
        End Sub

        Public Function Clone() As Object Implements ICloneable.Clone
            Return Me.MemberwiseClone
        End Function

        Public Overrides Function ToString() As String
            Return Me.Group.ToString & " (" & String.Join("/", Me.AllowedActions.ToArray) & ")"
        End Function

    End Class

#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace