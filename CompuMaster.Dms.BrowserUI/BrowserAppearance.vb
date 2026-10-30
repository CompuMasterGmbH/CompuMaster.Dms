Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports Microsoft.Win32

''' <summary>Selects the appearance of a browser and its owned dialogs.</summary>
Public Enum BrowserTheme
    ''' <summary>Inherits the owner appearance or follows the Windows preference.</summary>
    System
    ''' <summary>Uses a light appearance.</summary>
    Light
    ''' <summary>Uses a dark appearance.</summary>
    Dark
End Enum

''' <summary>Applies scoped appearance without changing the application theme.</summary>
Public NotInheritable Class BrowserAppearance
    Private Shared ReadOnly States As New ConditionalWeakTable(Of Control, AppearanceState)
    Private Shared ReadOnly Surface As Color = Color.FromArgb(32, 32, 32)
    Private Shared ReadOnly Field As Color = Color.FromArgb(42, 42, 42)
    Private Shared ReadOnly TextColor As Color = Color.FromArgb(240, 240, 240)

    Private Sub New()
    End Sub

    ''' <summary>Applies the selected appearance to a control and its descendants on the UI thread.</summary>
    ''' <param name="root">The root of the appearance scope.</param>
    ''' <param name="theme">The appearance to request. High contrast always uses system colors.</param>
    Public Shared Sub SetTheme(root As Control, theme As BrowserTheme)
        If root Is Nothing Then Throw New ArgumentNullException(NameOf(root))
        If Not [Enum].IsDefined(GetType(BrowserTheme), theme) Then Throw New ArgumentOutOfRangeException(NameOf(theme))
        If TypeOf root Is Form Then Bind(DirectCast(root, Form)) Else BindControl(root)
        Dim state = States.GetOrCreateValue(root)
        state.Theme = theme
        state.ThemeAssigned = True
        For Each selector In root.Controls.Find("BrowserThemeSelector", False).OfType(Of ComboBox)()
            If selector.SelectedIndex <> CInt(theme) Then selector.SelectedIndex = CInt(theme)
        Next
        Refresh(root)
    End Sub

    ''' <summary>Gets the requested appearance for a scope.</summary>
    ''' <param name="root">The appearance scope.</param>
    ''' <returns>The requested appearance, defaulting to System.</returns>
    Public Shared Function GetTheme(root As Control) As BrowserTheme
        If root Is Nothing Then Throw New ArgumentNullException(NameOf(root))
        Dim state As AppearanceState = Nothing
        Return If(States.TryGetValue(root, state), state.Theme, BrowserTheme.System)
    End Function

    ''' <summary>Creates a localized appearance selector for a form.</summary>
    ''' <param name="root">The form whose appearance changes.</param>
    ''' <returns>A selector that can be placed in the host layout.</returns>
    Public Shared Function CreateSelector(root As Control) As ComboBox
        If TypeOf root Is Form Then Bind(DirectCast(root, Form)) Else BindControl(root)
        Dim selector As New ComboBox With {.Name = "BrowserThemeSelector", .DropDownStyle = ComboBoxStyle.DropDownList, .Width = 150, .AccessibleName = UiStrings.GetText("Appearance")}
        selector.Items.AddRange({UiStrings.GetText("ThemeSystem"), UiStrings.GetText("ThemeLight"), UiStrings.GetText("ThemeDark")})
        selector.SelectedIndex = CInt(GetTheme(root))
        AddHandler selector.SelectedIndexChanged, Sub(sender, e) SetTheme(root, CType(selector.SelectedIndex, BrowserTheme))
        Return selector
    End Function

    Friend Shared Sub Bind(owner As Form)
        BindControl(owner)
        Dim state = States.GetOrCreateValue(owner)
        If state.FormBound Then Return
        state.FormBound = True
        AddHandler owner.Load, Sub(sender, e) Refresh(owner)
        AddHandler owner.HandleCreated, Sub(sender, e) Refresh(owner)
        Dim preferenceChanged As UserPreferenceChangedEventHandler =
            Sub(sender, e)
                If owner.IsDisposed OrElse Not owner.IsHandleCreated Then Return
                Try
                    owner.BeginInvoke(New MethodInvoker(Sub() Refresh(owner)))
                Catch ex As InvalidOperationException
                    'The window may close while the system notification is queued.
                End Try
            End Sub
        AddHandler SystemEvents.UserPreferenceChanged, preferenceChanged
        AddHandler owner.Disposed, Sub(sender, e) RemoveHandler SystemEvents.UserPreferenceChanged, preferenceChanged
        Refresh(owner)
    End Sub

    ''' <summary>Sets optional scoped palettes; Nothing restores the default colors.</summary>
    ''' <param name="root">The root controlled by the palettes.</param>
    ''' <param name="light">The optional light palette.</param>
    ''' <param name="dark">The optional dark palette.</param>
    Public Shared Sub SetPalettes(root As Control, light As BrowserPalette, dark As BrowserPalette)
        If root Is Nothing Then Throw New ArgumentNullException(NameOf(root))
        If TypeOf root Is Form Then Bind(DirectCast(root, Form)) Else BindControl(root)
        Dim state = States.GetOrCreateValue(root)
        state.Light = light : state.Dark = dark
        state.PaletteAssigned = True
        Refresh(root)
    End Sub
    Friend Shared Function Palette(root As Control, dark As Boolean) As BrowserPalette
        Dim state = States.GetOrCreateValue(root)
        Dim result = If(dark, state.Dark, state.Light)
        If result Is Nothing AndAlso root.Parent IsNot Nothing Then Return Palette(root.Parent, dark)
        Dim form = TryCast(root, Form)
        If result Is Nothing AndAlso form IsNot Nothing AndAlso form.Owner IsNot Nothing Then Return Palette(form.Owner, dark)
        Return result
    End Function
    Private Shared Function Parse(value As String) As Color
        Return ColorTranslator.FromHtml(value)
    End Function

    Private Shared Function IsDark(root As Control) As Boolean
        If SystemInformation.HighContrast Then Return False
        Dim mode = GetTheme(root)
        If mode <> BrowserTheme.System Then Return mode = BrowserTheme.Dark
        If root.Parent IsNot Nothing Then Return IsDark(root.Parent)
        Dim form = TryCast(root, Form)
        If form IsNot Nothing AndAlso form.Owner IsNot Nothing Then Return IsDark(form.Owner)
        Try
            Return UsesDarkWindowsPreference(Registry.GetValue("HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1))
        Catch ex As System.Security.SecurityException
            Return False
        Catch ex As UnauthorizedAccessException
            Return False
        End Try
    End Function

    Friend Shared Function UsesDarkWindowsPreference(value As Object) As Boolean
        'An absent Personalize key returns Nothing even when a default value is supplied.
        Return value IsNot Nothing AndAlso Convert.ToInt32(value, Globalization.CultureInfo.InvariantCulture) = 0
    End Function

    Private Shared Sub Refresh(root As Control)
        If root.IsDisposed Then Return
        Apply(root, IsDark(root), Palette(root, IsDark(root)))
        Dim owner = TryCast(root, Form)
        If owner IsNot Nothing Then
            For Each child As Form In owner.OwnedForms
                If GetTheme(child) = BrowserTheme.System Then Refresh(child)
            Next
        End If
    End Sub

    Private Shared Function AppearanceRoot(control As Control) As Control
        Dim current = control
        While current IsNot Nothing
            Dim state = States.GetOrCreateValue(current)
            If state.ThemeAssigned OrElse state.PaletteAssigned OrElse state.FormBound Then Return current
            current = current.Parent
        End While
        Return control
    End Function

    Private Shared Sub BindControl(control As Control)
        Dim state = States.GetOrCreateValue(control)
        If state.Bound Then Return
        state.Bound = True
        state.Back = If(System.ComponentModel.TypeDescriptor.GetProperties(control)("BackColor").ShouldSerializeValue(control), control.BackColor, Color.Empty)
        state.Fore = If(System.ComponentModel.TypeDescriptor.GetProperties(control)("ForeColor").ShouldSerializeValue(control), control.ForeColor, Color.Empty)
        Dim button = TryCast(control, ButtonBase)
        If button IsNot Nothing Then state.Flat = button.FlatStyle : state.VisualStyles = button.UseVisualStyleBackColor
        Dim strip = TryCast(control, ToolStrip)
        If strip IsNot Nothing Then state.Renderer = strip.Renderer
        Dim tree = TryCast(control, TreeView)
        If tree IsNot Nothing Then
            state.TreeDrawMode = tree.DrawMode
            AddHandler tree.DrawNode,
                Sub(sender, e)
                    Dim root = If(DirectCast(tree.FindForm(), Control), tree)
                    Dim colors = Palette(root, IsDark(root))
                    Dim selected = (e.State And TreeNodeStates.Selected) <> 0
                    Dim back = If(selected, If(colors Is Nothing, SystemColors.Highlight, Parse(colors.Accent)), If(e.Node.BackColor.IsEmpty, tree.BackColor, e.Node.BackColor))
                    Dim fore = If(selected, If(colors Is Nothing, SystemColors.HighlightText, Parse(colors.AccentForeground)), If(e.Node.ForeColor.IsEmpty, tree.ForeColor, e.Node.ForeColor))
                    Using brush As New SolidBrush(back)
                        e.Graphics.FillRectangle(brush, e.Bounds)
                    End Using
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, If(e.Node.NodeFont, tree.Font), e.Bounds, fore, TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine Or TextFormatFlags.NoPrefix)
                    If (e.State And TreeNodeStates.Focused) <> 0 Then ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, fore, back)
                End Sub
        End If
        Dim list = TryCast(control, ListView)
        If list IsNot Nothing Then
            state.ListOwnerDraw = list.OwnerDraw
            AddHandler list.DrawColumnHeader,
                Sub(sender, e)
                    Using brush As New SolidBrush(list.BackColor)
                        e.Graphics.FillRectangle(brush, e.Bounds)
                    End Using
                    TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, e.Bounds, list.ForeColor, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine)
                End Sub
            AddHandler list.DrawItem, Sub(sender, e)
                                          If list.View <> View.Details Then e.DrawDefault = True
                                      End Sub
            AddHandler list.DrawSubItem,
                Sub(sender, e)
                    Dim palette = BrowserAppearance.Palette(If(DirectCast(list.FindForm(), Control), list), IsDark(If(DirectCast(list.FindForm(), Control), list)))
                    Dim selected = e.Item.Selected
                    Dim back = If(selected, If(palette Is Nothing, SystemColors.Highlight, Parse(palette.Accent)), e.SubItem.BackColor)
                    Dim fore = If(selected, If(palette Is Nothing, SystemColors.HighlightText, Parse(palette.AccentForeground)), e.SubItem.ForeColor)
                    Using brush As New SolidBrush(back)
                        e.Graphics.FillRectangle(brush, e.Bounds)
                    End Using
                    Dim bounds = e.Bounds
                    If e.ColumnIndex = 0 AndAlso list.SmallImageList IsNot Nothing AndAlso e.Item.ImageIndex >= 0 AndAlso e.Item.ImageIndex < list.SmallImageList.Images.Count Then
                        Dim icon = list.SmallImageList.Images(e.Item.ImageIndex)
                        e.Graphics.DrawImage(icon, bounds.Left + 2, bounds.Top + (bounds.Height - icon.Height) \ 2)
                        bounds.X += icon.Width + 4 : bounds.Width -= icon.Width + 4
                    End If
                    TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, bounds, fore, TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.SingleLine)
                End Sub
        End If
        AddHandler control.ControlAdded,
            Sub(sender, e)
                BindControl(e.Control)
                Refresh(AppearanceRoot(control))
            End Sub
        AddHandler control.HandleCreated, Sub(sender, e) Refresh(AppearanceRoot(control))
        If control.ContextMenuStrip IsNot Nothing Then
            AddHandler control.ContextMenuStrip.Opening, Sub(sender, e) Apply(control.ContextMenuStrip, IsDark(If(control.FindForm(), control)), Palette(If(control.FindForm(), control), IsDark(If(control.FindForm(), control))))
        End If
        For Each child As Control In control.Controls
            BindControl(child)
        Next
    End Sub

    Private Shared Sub Apply(control As Control, dark As Boolean, Optional palette As BrowserPalette = Nothing)
        BindControl(control)
        Dim state = States.GetOrCreateValue(control)
        If state.ThemeAssigned Then dark = IsDark(control)
        If state.ThemeAssigned OrElse state.PaletteAssigned Then palette = BrowserAppearance.Palette(control, dark)
        Dim contrast = SystemInformation.HighContrast
        If contrast Then palette = Nothing
        Dim input = TypeOf control Is TextBoxBase OrElse TypeOf control Is ListView OrElse TypeOf control Is TreeView OrElse TypeOf control Is ComboBox OrElse TypeOf control Is NumericUpDown OrElse TypeOf control Is DataGridView
        control.BackColor = If(contrast, If(input, SystemColors.Window, SystemColors.Control), If(dark, If(input, Field, Surface), state.Back))
        control.ForeColor = If(contrast, If(input, SystemColors.WindowText, SystemColors.ControlText), If(dark, TextColor, state.Fore))
        If Not contrast AndAlso Not dark AndAlso state.ThemeAssigned AndAlso control.Parent IsNot Nothing Then
            control.BackColor = If(input, SystemColors.Window, SystemColors.Control)
            control.ForeColor = If(input, SystemColors.WindowText, SystemColors.ControlText)
        End If
        If palette IsNot Nothing AndAlso Not contrast Then
            control.BackColor = Parse(If(input OrElse TypeOf control Is ButtonBase, palette.Surface, palette.Background))
            control.ForeColor = Parse(palette.Foreground)
        End If
        If palette Is Nothing AndAlso control.Name = "ReceivedSharingSummaryPanel" AndAlso dark Then control.BackColor = Color.FromArgb(23, 58, 82)
        Dim button = TryCast(control, ButtonBase)
        If button IsNot Nothing Then
            button.FlatStyle = If(dark OrElse palette IsNot Nothing, FlatStyle.Flat, state.Flat)
            button.UseVisualStyleBackColor = Not dark AndAlso palette Is Nothing AndAlso state.VisualStyles
            If dark OrElse palette IsNot Nothing Then button.FlatAppearance.BorderColor = If(palette Is Nothing, Color.FromArgb(100, 100, 100), Parse(palette.Border))
        End If
        Dim tree = TryCast(control, TreeView)
        If tree IsNot Nothing Then tree.DrawMode = If(Not contrast AndAlso (dark OrElse palette IsNot Nothing), TreeViewDrawMode.OwnerDrawText, state.TreeDrawMode)
        Dim list = TryCast(control, ListView)
        If list IsNot Nothing Then list.OwnerDraw = If(contrast, state.ListOwnerDraw, dark OrElse palette IsNot Nothing OrElse state.ListOwnerDraw)
        Dim grid = TryCast(control, DataGridView)
        If grid IsNot Nothing Then
            grid.EnableHeadersVisualStyles = Not dark AndAlso palette Is Nothing
            grid.BackgroundColor = control.BackColor
            For Each style In {grid.DefaultCellStyle, grid.ColumnHeadersDefaultCellStyle, grid.RowHeadersDefaultCellStyle}
                style.BackColor = control.BackColor : style.ForeColor = control.ForeColor
                style.SelectionBackColor = If(palette Is Nothing OrElse contrast, SystemColors.Highlight, Parse(palette.Accent)) : style.SelectionForeColor = If(palette Is Nothing OrElse contrast, SystemColors.HighlightText, Parse(palette.AccentForeground))
            Next
            grid.GridColor = If(palette IsNot Nothing AndAlso Not contrast, Parse(palette.Border), If(dark, Color.FromArgb(85, 85, 85), SystemColors.ControlDark))
        End If
        Dim strip = TryCast(control, ToolStrip)
        If strip IsNot Nothing Then
            strip.Renderer = If(Not contrast AndAlso (dark OrElse palette IsNot Nothing), New DarkRenderer(dark, palette), state.Renderer)
            ApplyItems(strip.Items, dark, palette)
        End If
        If control.IsHandleCreated Then
            If TypeOf control Is ListView OrElse TypeOf control Is TreeView OrElse TypeOf control Is ComboBox Then SetWindowTheme(control.Handle, If(dark, "DarkMode_Explorer", Nothing), Nothing)
            If TypeOf control Is Form Then
                Dim enabled As Integer = If(dark, 1, 0)
                Try
                    DwmSetWindowAttribute(control.Handle, 20, enabled, 4)
                Catch ex As DllNotFoundException
                Catch ex As EntryPointNotFoundException
                End Try
            End If
        End If
        For Each child As Control In control.Controls
            Apply(child, dark, palette)
        Next
        control.Invalidate()
    End Sub

    Private Shared Sub ApplyItems(items As ToolStripItemCollection, dark As Boolean, Optional palette As BrowserPalette = Nothing)
        For Each item As ToolStripItem In items
            item.BackColor = If(palette Is Nothing, If(dark, Surface, SystemColors.Control), Parse(palette.Surface))
            item.ForeColor = If(palette Is Nothing, If(dark, TextColor, SystemColors.ControlText), Parse(palette.Foreground))
            Dim dropdown = TryCast(item, ToolStripDropDownItem)
            If dropdown IsNot Nothing Then
                dropdown.DropDown.Renderer = If(dark OrElse palette IsNot Nothing, New DarkRenderer(dark, palette), New ToolStripProfessionalRenderer())
                ApplyItems(dropdown.DropDownItems, dark, palette)
            End If
        Next
    End Sub

    Private NotInheritable Class AppearanceState
        Public Sub New()
        End Sub
        Friend Theme As BrowserTheme
        Friend Light, Dark As BrowserPalette
        Friend Bound, FormBound, ThemeAssigned, PaletteAssigned As Boolean
        Friend Back, Fore As Color
        Friend Flat As FlatStyle
        Friend VisualStyles, ListOwnerDraw As Boolean
        Friend TreeDrawMode As TreeViewDrawMode
        Friend Renderer As ToolStripRenderer
    End Class
    Private NotInheritable Class DarkRenderer
        Inherits ToolStripProfessionalRenderer
        Private Shared ReadOnly Images As New ConditionalWeakTable(Of Image, Bitmap)
        Private ReadOnly DarkImages As Boolean
        Private ReadOnly Colors As BrowserPalette
        Friend Sub New(dark As Boolean, palette As BrowserPalette)
            MyBase.New(New DarkColorTable(palette))
            DarkImages = dark : Colors = palette
        End Sub
        Protected Overrides Sub OnRenderItemText(e As ToolStripItemTextRenderEventArgs)
            If Colors IsNot Nothing AndAlso e.Item.Selected Then e.TextColor = Parse(Colors.AccentForeground)
            MyBase.OnRenderItemText(e)
        End Sub
        Protected Overrides Sub OnRenderItemImage(e As ToolStripItemImageRenderEventArgs)
            If e.Image Is Nothing Then Return
            If Not DarkImages Then
                MyBase.OnRenderItemImage(e)
                Return
            End If
            Dim image = Images.GetValue(e.Image,
                Function(source)
                    Dim result As New Bitmap(source)
                    For y As Integer = 0 To result.Height - 1
                        For x As Integer = 0 To result.Width - 1
                            Dim pixel = result.GetPixel(x, y)
                            If pixel.A > 0 AndAlso Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) < 96 Then
                                result.SetPixel(x, y, Color.FromArgb(pixel.A, 230, 230, 230))
                            End If
                        Next
                    Next
                    Return result
                End Function)
            MyBase.OnRenderItemImage(New ToolStripItemImageRenderEventArgs(e.Graphics, e.Item, image, e.ImageRectangle))
        End Sub
    End Class
    Private NotInheritable Class DarkColorTable
        Inherits ProfessionalColorTable
        Private ReadOnly MenuBack As Color
        Private ReadOnly SelectedBack As Color
        Private ReadOnly Edge As Color
        Friend Sub New(palette As BrowserPalette)
            MenuBack = If(palette Is Nothing, Surface, Parse(palette.Surface))
            SelectedBack = If(palette Is Nothing, Color.FromArgb(65, 65, 65), Parse(palette.Accent))
            Edge = If(palette Is Nothing, Color.Gray, Parse(palette.Border))
        End Sub
        Public Overrides ReadOnly Property ToolStripGradientBegin As Color = MenuBack
        Public Overrides ReadOnly Property ToolStripGradientMiddle As Color = MenuBack
        Public Overrides ReadOnly Property ToolStripGradientEnd As Color = MenuBack
        Public Overrides ReadOnly Property MenuStripGradientBegin As Color = MenuBack
        Public Overrides ReadOnly Property MenuStripGradientEnd As Color = MenuBack
        Public Overrides ReadOnly Property ToolStripDropDownBackground As Color = MenuBack
        Public Overrides ReadOnly Property ImageMarginGradientBegin As Color = MenuBack
        Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color = MenuBack
        Public Overrides ReadOnly Property ImageMarginGradientEnd As Color = MenuBack
        Public Overrides ReadOnly Property MenuItemSelected As Color = SelectedBack
        Public Overrides ReadOnly Property MenuItemBorder As Color = Edge
        Public Overrides ReadOnly Property MenuItemSelectedGradientBegin As Color = SelectedBack
        Public Overrides ReadOnly Property MenuItemSelectedGradientEnd As Color = SelectedBack
        Public Overrides ReadOnly Property ButtonSelectedGradientBegin As Color = SelectedBack
        Public Overrides ReadOnly Property ButtonSelectedGradientEnd As Color = SelectedBack
        Public Overrides ReadOnly Property ButtonPressedGradientBegin As Color = SelectedBack
        Public Overrides ReadOnly Property ButtonPressedGradientEnd As Color = SelectedBack
    End Class
    <DllImport("uxtheme.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function SetWindowTheme(handle As IntPtr, subApp As String, subId As String) As Integer
    End Function
    <DllImport("dwmapi.dll")>
    Private Shared Function DwmSetWindowAttribute(handle As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function
End Class
