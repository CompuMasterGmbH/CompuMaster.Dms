''' <summary>Defines immutable opaque appearance colors using #RRGGBB notation.</summary>
''' <remarks>Hosts must provide readable contrast. Windows high contrast takes precedence.</remarks>
Public NotInheritable Class BrowserPalette
    ''' <summary>Creates a palette using opaque hexadecimal colors.</summary>
    ''' <param name="background">The window background.</param>
    ''' <param name="surface">The background of lists, fields and buttons.</param>
    ''' <param name="foreground">The normal text color.</param>
    ''' <param name="accent">The accent and selection background.</param>
    ''' <param name="accentForeground">The text color on accent backgrounds.</param>
    ''' <param name="border">The control border color.</param>
    Public Sub New(background As String, surface As String, foreground As String, accent As String, accentForeground As String, border As String)
        Me.Background = Validate(background) : Me.Surface = Validate(surface) : Me.Foreground = Validate(foreground)
        Me.Accent = Validate(accent) : Me.AccentForeground = Validate(accentForeground) : Me.Border = Validate(border)
    End Sub
    Private Shared Function Validate(color As String) As String
        If color Is Nothing OrElse Not System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$") Then Throw New ArgumentException("Colors must use #RRGGBB notation.", NameOf(color))
        Return color
    End Function
    ''' <summary>Gets the window background.</summary>
    Public ReadOnly Property Background As String
    ''' <summary>Gets the background of lists, fields and buttons.</summary>
    Public ReadOnly Property Surface As String
    ''' <summary>Gets the normal text color.</summary>
    Public ReadOnly Property Foreground As String
    ''' <summary>Gets the accent and selection background.</summary>
    Public ReadOnly Property Accent As String
    ''' <summary>Gets the text color on accent backgrounds.</summary>
    Public ReadOnly Property AccentForeground As String
    ''' <summary>Gets the control border color.</summary>
    Public ReadOnly Property Border As String
End Class
