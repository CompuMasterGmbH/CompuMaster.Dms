Option Explicit On
Option Strict On

Namespace Data
    ''' <summary>Identifies how a received grant addresses its recipient.</summary>
    Public Enum DmsReceivedShareKind
        ''' <summary>Indicates that the provider did not supply a supported recipient kind.</summary>
        Unknown = 0
        ''' <summary>Identifies access granted directly to a user.</summary>
        User = 1
        ''' <summary>Identifies access granted through a group.</summary>
        Group = 2
    End Enum

    ''' <summary>Describes a received grant without providing operations to change its source sharing.</summary>
    ''' <remarks>Permissions describe this grant, not necessarily the combined effective access. Unknown permissions must not be interpreted as a denial or authorization.</remarks>
    Public NotInheritable Class DmsReceivedShare
        Inherits DmsShareBase

        ''' <summary>Initializes an unbound received-grant snapshot with unknown permissions.</summary>
        Public Sub New()
            MyBase.New(Nothing, False, False, False, False, False, False)
        End Sub
        ''' <inheritdoc/>
        Protected Overrides Sub Initialize()
        End Sub
        ''' <summary>Gets or sets the instance-scoped grant identifier supplied by the provider.</summary>
        Public Property ID As String
        ''' <summary>Gets or sets the actual sharing user when supplied, independently of the resource owner.</summary>
        Public Property SharedBy As DmsUser
        ''' <summary>Gets or sets the resource owner reported for this grant.</summary>
        Public Property ResourceOwner As DmsUser
        ''' <summary>Gets or sets the kind of recipient addressed by this grant.</summary>
        Public Property RecipientKind As DmsReceivedShareKind
        ''' <summary>Gets or sets the provider's user or group recipient identifier when available.</summary>
        Public Property RecipientID As String
        ''' <summary>Gets or sets the provider-supplied recipient display name without replacing its identifier.</summary>
        Public Property RecipientDisplayName As String
        ''' <summary>Gets or sets the recipient-visible path of the originally shared resource when available.</summary>
        Public Property SharedPath As String
        ''' <summary>Gets or sets whether access is inherited from a verified ancestor, or Nothing when unknown.</summary>
        Public Property IsInherited As Boolean?
        ''' <summary>Gets or sets whether the server supplied this grant's permission values.</summary>
        Public Property PermissionsAvailable As Boolean
    End Class
End Namespace
