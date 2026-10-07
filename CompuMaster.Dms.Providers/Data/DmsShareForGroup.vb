Option Explicit On
Option Strict On

Namespace Data

#Disable Warning CA1034 ' Nested types should not be visible
#Disable Warning CA1815 ' Override equals and operator equals on value types

    ''' <summary>Represents permissions granted to a provider-owned group.</summary>
    Public Class DmsShareForGroup
        Inherits DmsShareBase
        Implements ICloneable

        ''' <summary>Initializes the group permission settings.</summary>
        ''' <param name="parentDmsResourceItem">The remote resource whose permissions are represented.</param>
        ''' <param name="group">The provider-owned group to authorize.</param>
        ''' <param name="allowView">Whether the view permission is enabled.</param>
        ''' <param name="allowDownload">Whether the download permission is enabled.</param>
        ''' <param name="allowEdit">Whether the edit permission is enabled.</param>
        ''' <param name="allowUpload">Whether the upload permission is enabled.</param>
        ''' <param name="allowDelete">Whether the delete permission is enabled.</param>
        ''' <param name="allowShare">Whether the share permission is enabled.</param>
        Public Sub New(parentDmsResourceItem As DmsResourceItem, group As DmsGroup, allowView As Boolean, allowDownload As Boolean, allowEdit As Boolean, allowUpload As Boolean, allowDelete As Boolean, allowShare As Boolean)
            MyBase.New(parentDmsResourceItem, allowView, allowDownload, allowEdit, allowUpload, allowDelete, allowShare)
            Me.Group = group
        End Sub

        ''' <summary>Gets or sets the provider-owned group to which these permissions are granted.</summary>
        Public Property Group As DmsGroup

        ''' <inheritdoc/>
        Protected Overrides Sub Initialize()
        End Sub

        ''' <summary>Creates a shallow copy of these sharing settings.</summary>
        ''' <returns>A copy retaining references to the provider, parent resource and lookup delegates.</returns>
        Public Function Clone() As Object Implements ICloneable.Clone
            Return Me.MemberwiseClone
        End Function

        ''' <inheritdoc/>
        Public Overrides Function ToString() As String
            Return Me.Group.ToString & " (" & String.Join("/", Me.AllowedActions.ToArray) & ")"
        End Function

    End Class

#Enable Warning CA1815 ' Override equals and operator equals on value types
#Enable Warning CA1034 ' Nested types should not be visible

End Namespace
