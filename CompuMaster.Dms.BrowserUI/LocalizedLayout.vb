Imports System.Drawing
Imports System.Windows.Forms

Friend NotInheritable Class LocalizedLayout
    Private Shared ReadOnly Rows As New Runtime.CompilerServices.ConditionalWeakTable(Of GroupBox, RowGroups)

    Friend Shared Sub Bind(owner As Form, arrange As Action)
        Dim running As Boolean
        Dim update As EventHandler = Sub(sender, e)
                                         If running OrElse owner.IsDisposed OrElse owner.Disposing Then Return
                                         running = True
                                         Try
                                             arrange()
                                         Finally
                                             running = False
                                         End Try
                                     End Sub
        AddHandler owner.FontChanged, update
        AddHandler owner.SizeChanged, update
        AddHandler owner.Shown, update
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
            If TypeOf child Is Button Then
                Dim preferred = child.GetPreferredSize(Size.Empty)
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
        Dim minimumWidth = fieldLeft + If(toggles, 24, 0) + 160 + 8
        group.Width = Math.Max(group.Width, minimumWidth)
        For Each field As Control In group.Controls
            If TypeOf field Is TextBox OrElse TypeOf field Is DateTimePicker OrElse TypeOf field Is ComboBox Then
                Dim hasToggle = toggles AndAlso group.Controls.OfType(Of CheckBox)().Any(Function(check) Math.Abs(check.Top - field.Top) < field.Height)
                field.Left = fieldLeft + If(hasToggle, 24, 0)
                field.Width = group.ClientSize.Width - field.Left - 8
            ElseIf TypeOf field Is CheckBox AndAlso String.IsNullOrEmpty(field.Text) Then
                field.Left = fieldLeft
            End If
        Next
        Return group.Width
    End Function

    Friend Shared Function MinimumFieldWidth(group As GroupBox, Optional toggles As Boolean = False) As Integer
        Return group.Controls.OfType(Of Label)().Max(Function(label) label.Left + label.GetPreferredSize(Size.Empty).Width) + 12 + If(toggles, 24, 0) + 168
    End Function

    Friend Shared Function MinimumPermissionWidth(group As GroupBox) As Integer
        Return Rows.GetValue(group, Function(key) New RowGroups(key)).Items.Max(Function(row) row.Sum(Function(control) control.GetPreferredSize(Size.Empty).Width + 16)) + 7
    End Function
End Class
