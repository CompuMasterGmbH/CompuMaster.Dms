Imports System.Drawing
Imports System.Windows.Forms

Friend NotInheritable Class LocalizedLayout
    Private Shared ReadOnly Rows As New Runtime.CompilerServices.ConditionalWeakTable(Of GroupBox, RowGroups)

    Friend Shared Sub Bind(owner As Form, arrange As Action)
        InitialWindowDpi.Bind(owner)
        Dim running As Boolean
        Dim dpiPending As Boolean
        Dim update As EventHandler = Sub(sender, e)
                                         If running OrElse dpiPending OrElse owner.IsDisposed OrElse owner.Disposing Then Return
                                         running = True
                                         Try
                                             arrange()
                                         Finally
                                             running = False
                                         End Try
                                     End Sub
        AddHandler owner.FontChanged, update
        AddHandler owner.SizeChanged, update
        AddHandler owner.DpiChanged,
            Sub(sender, e)
                dpiPending = True
                owner.BeginInvoke(New MethodInvoker(
                    Sub()
                        dpiPending = False
                        update(owner, EventArgs.Empty)
                    End Sub))
            End Sub
        AddHandler owner.Shown,
            Sub(sender, e)
                InitialWindowDpi.Synchronize(owner)
                update(sender, e)
            End Sub
        AddHandler owner.Layout, Sub(sender, e) update(sender, e)
        update(owner, EventArgs.Empty)
    End Sub

    Friend Shared Sub FitRows(group As GroupBox, Optional horizontal As Boolean = False)
        Dim rowGroups = Rows.GetValue(group, Function(key) New RowGroups(key))
        Dim top = TextRenderer.MeasureText(group.Text, group.Font).Height + 7
        For Each row In rowGroups.Items
            Dim height = row.Max(Function(control) control.GetPreferredSize(Size.Empty).Height)
            Dim left As Integer = 7
            For Each control In row
                If TypeOf control Is Label OrElse TypeOf control Is CheckBox Then control.Size = control.GetPreferredSize(Size.Empty)
                control.Top = top + Math.Max(0, (height - control.Height) \ 2)
                If horizontal Then
                    control.Left = left
                    left = control.Right + 16
                End If
            Next
            If horizontal Then group.Width = Math.Max(group.Width, left)
            top += height + 8
        Next
        group.Height = Math.Max(group.Height, top)
    End Sub

    Private NotInheritable Class RowGroups
        Friend ReadOnly Items As New List(Of Control())
        Friend Sub New(group As GroupBox)
            Dim current As New List(Of Control)
            Dim top As Integer = -100
            For Each child In group.Controls.Cast(Of Control)().OrderBy(Function(control) control.Top).ThenBy(Function(control) control.Left)
                If child.Top > top + 8 Then
                    If current.Count <> 0 Then Items.Add(current.OrderBy(Function(control) control.Left).ToArray())
                    current.Clear()
                    top = child.Top
                End If
                current.Add(child)
            Next
            If current.Count <> 0 Then Items.Add(current.OrderBy(Function(control) control.Left).ToArray())
        End Sub
    End Class

    Friend Shared Sub FitButtons(parent As Control)
        For Each child As Control In parent.Controls
            If TypeOf child Is Button OrElse (TypeOf child Is CheckBox AndAlso DirectCast(child, CheckBox).Appearance = Appearance.Button) Then
                Dim preferred = child.GetPreferredSize(Size.Empty)
                If TypeOf child Is CheckBox Then
                    'Button-style checkboxes reserve extra text insets. Preferred size
                    'alone can still wrap German captions at native 200% DPI.
                    Dim caption = TextRenderer.MeasureText(child.Text, child.Font, Size.Empty, TextFormatFlags.SingleLine)
                    preferred.Width = Math.Max(preferred.Width, caption.Width + child.Padding.Horizontal * 2 + Math.Max(caption.Height, child.Font.Height))
                End If
                child.Size = New Size(Math.Max(child.Width, preferred.Width), Math.Max(child.Height, preferred.Height))
            End If
            FitButtons(child)
        Next
    End Sub

    Friend Shared Sub FitColumnHeaders(list As ListView)
        For Each column As ColumnHeader In list.Columns
            column.Width = Math.Max(column.Width, TextRenderer.MeasureText(column.Text, list.Font).Width + 16)
        Next
    End Sub

    Friend Shared Function FitFieldColumns(group As GroupBox, Optional toggles As Boolean = False) As Integer
        Dim labels = group.Controls.OfType(Of Label)().ToArray()
        Dim fieldLeft = labels.Max(Function(label) label.Left + label.GetPreferredSize(Size.Empty).Width) + 12
        Dim toggleSpace = If(toggles, ToggleFieldSpace(group), 0)
        Dim minimumWidth = fieldLeft + toggleSpace + 160 + 8
        group.Width = Math.Max(group.Width, minimumWidth)
        For Each field As Control In group.Controls
            If TypeOf field Is TextBox OrElse TypeOf field Is DateTimePicker OrElse TypeOf field Is ComboBox Then
                Dim hasToggle = toggles AndAlso group.Controls.OfType(Of CheckBox)().Any(Function(check) Math.Abs(check.Top - field.Top) < field.Height)
                'This layout owns the horizontal bounds; native right anchoring must
                'not subsequently apply the previous width delta (.NET Framework).
                field.Anchor = AnchorStyles.Top Or AnchorStyles.Left
                Dim left = fieldLeft + If(hasToggle, toggleSpace, 0)
                field.SetBounds(left, field.Top, group.ClientSize.Width - left - 8, field.Height)
            ElseIf TypeOf field Is CheckBox AndAlso String.IsNullOrEmpty(field.Text) Then
                field.Left = fieldLeft
            End If
        Next
        Return group.Width
    End Function

    Friend Shared Function MinimumFieldWidth(group As GroupBox, Optional toggles As Boolean = False) As Integer
        Return group.Controls.OfType(Of Label)().Max(Function(label) label.Left + label.GetPreferredSize(Size.Empty).Width) + 12 + If(toggles, ToggleFieldSpace(group), 0) + 168
    End Function

    Private Shared Function ToggleFieldSpace(group As GroupBox) As Integer
        Return group.Controls.OfType(Of CheckBox)().Where(Function(check) String.IsNullOrEmpty(check.Text)).Select(
            Function(check) Math.Max(check.Width, check.GetPreferredSize(Size.Empty).Width)).DefaultIfEmpty(16).Max() + 8
    End Function

    Friend Shared Function MinimumPermissionWidth(group As GroupBox) As Integer
        Return Rows.GetValue(group, Function(key) New RowGroups(key)).Items.Max(Function(row) row.Sum(Function(control) control.GetPreferredSize(Size.Empty).Width + 16)) + 7
    End Function
End Class
