Option Explicit On
Option Strict On

Imports System.Data
Imports System.Windows.Forms
Imports System.Drawing
Imports CompuMaster.VisualBasicCompatibility.Information
Imports CompuMaster.Dms.Data
Imports CompuMaster.Dms.Providers

Friend Module Tools

    Public Function ByteSizeToUIDisplayText(value As Long) As String
        If value < 0 Then
            Throw New ArgumentOutOfRangeException(NameOf(value), "Negative size values are not allowed")
        ElseIf value < 1300L Then
            'Output in Bytes
            Return value.ToString & " Bytes"
        ElseIf value < 1300L * 1000L Then
            'Output in KB
            Return (value / 1024).ToString("#,##0") & " KB"
        ElseIf value < 1300L * 1000L ^ 2 Then
            'Output in MB
            Return (value / 1024 ^ 2).ToString("#,##0") & " MB"
        ElseIf value < 1300L * 1000L ^ 3 Then
            'Output in GB
            Return (value / 1024 ^ 3).ToString("#,##0") & " GB"
        Else
            'Output in TB
            Return (value / 1024 ^ 4).ToString("#,##0") & " TB"
        End If
    End Function

    Public Function IIf(expression As Boolean, firstChoice As String, alternativeChoice As String) As String
        If expression Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Public Function IIf(Of T)(expression As Boolean, firstChoice As T, alternativeChoice As T) As T
        If expression Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Public Function FindKeyValuePairEntryIndex(Of T, V)(control As ComboBox, key As T, value As V, jitCreateEntryIfMissing As Boolean) As Integer
        For MyCounter As Integer = 0 To control.Items.Count - 1
            If CType(control.Items(MyCounter), KeyValuePair(Of T, V)).Key.Equals(key) Then
                Return MyCounter
            End If
        Next
        If jitCreateEntryIfMissing AndAlso value IsNot Nothing Then
            Return control.Items.Add(New KeyValuePair(Of T, V)(key, value))
        Else
            Return Nothing
        End If
    End Function

    'Public Function FindEntryBaseKeyValuePairEntryIndex(Of T As Data.EntryBase)(control As ComboBox, key As T, displayValue As String) As Integer
    '    For MyCounter As Integer = 0 To control.Items.Count - 1
    '        If CType(control.Items(MyCounter), KeyValuePair(Of T, String)).Key Is Nothing Then
    '            If (key Is Nothing OrElse key.ID = Nothing) AndAlso CType(control.Items(MyCounter), KeyValuePair(Of T, String)).Value = displayValue Then
    '                Return MyCounter
    '            End If
    '        ElseIf key IsNot Nothing AndAlso CType(control.Items(MyCounter), KeyValuePair(Of T, String)).Key.ID.Equals(key.ID) Then
    '            Return MyCounter
    '        End If
    '    Next
    '    Return Nothing
    'End Function

    'Public Function FindEntryBaseKeyValuePairEntryIndex(Of T As Data.EntryBase)(control As ComboBox, keyID As Guid, jitCreateEntryIfMissing As Boolean) As Integer
    '    For MyCounter As Integer = 0 To control.Items.Count - 1
    '        If CType(control.Items(MyCounter), KeyValuePair(Of T, String)).Key Is Nothing Then
    '            If keyID = Nothing AndAlso CType(control.Items(MyCounter), KeyValuePair(Of T, String)).Value = Nothing Then
    '                Return MyCounter
    '            ElseIf keyID <> Nothing AndAlso CType(control.Items(MyCounter), KeyValuePair(Of T, String)).Value = "{invalid:" & keyID.ToString & "}" Then
    '                Return 0 'not MyCounter since manual selection of this entry is not allowed any more
    '            End If
    '        ElseIf keyID <> Nothing AndAlso CType(control.Items(MyCounter), KeyValuePair(Of T, String)).Key.ID.Equals(keyID) Then
    '            Return MyCounter
    '        End If
    '    Next
    '    If jitCreateEntryIfMissing AndAlso keyID <> Nothing Then
    '        Return control.Items.Add(New KeyValuePair(Of T, String)(Nothing, "{invalid:" & keyID.ToString & "}"))
    '    Else
    '        Return Nothing
    '    End If
    'End Function

    Public Function FindEntryIndex(Of T)(control As ComboBox, value As T, jitCreateEntryIfMissing As Boolean) As Integer
        For MyCounter As Integer = 0 To control.Items.Count - 1
            If CType(control.Items(MyCounter), T).Equals(value) Then
                Return MyCounter
            End If
        Next
        If jitCreateEntryIfMissing AndAlso value IsNot Nothing Then
            Return control.Items.Add(value)
        Else
            Return Nothing
        End If
    End Function

    Public Sub SwitchToolStripVisibility(item As ToolStripItem, visible As Boolean, bold As Boolean)
        item.Available = visible
        If bold Then
            item.Font = New Font(item.Font, item.Font.Style Or FontStyle.Bold)
        Else
            item.Font = New Font(item.Font, item.Font.Style And FontStyle.Regular)
        End If
    End Sub

    Public Sub SwitchSeparatorLinesVisibility(menu As ToolStripItemCollection)
        Dim PreviousVisibleItemIsSeparator As Boolean = True
        'Switch separator visibility ON if required
        For MyCounter As Integer = 0 To menu.Count - 1
            Select Case menu.Item(MyCounter).GetType
                Case GetType(ToolStripSeparator)
                    If PreviousVisibleItemIsSeparator = True Then
                        'Current separator is not required
                        SwitchToolStripVisibility(menu.Item(MyCounter), False, False)
                    Else
                        'Current separator is required
                        SwitchToolStripVisibility(menu.Item(MyCounter), True, False)
                        PreviousVisibleItemIsSeparator = True
                    End If
                Case Else
                    If menu.Item(MyCounter).Available = True Then
                        'Reset PreviousVisibleItemIsSeparator status
                        PreviousVisibleItemIsSeparator = False
                    End If
            End Select
            'Recursive call
            If menu.Item(MyCounter).Available = True AndAlso GetType(ToolStripDropDownItem).IsInstanceOfType(menu.Item(MyCounter)) AndAlso DirectCast(menu.Item(MyCounter), System.Windows.Forms.ToolStripDropDownItem).DropDownItems.Count > 0 Then
                SwitchSeparatorLinesVisibility(DirectCast(menu.Item(MyCounter), System.Windows.Forms.ToolStripDropDownItem).DropDownItems)
            End If
        Next
        'Switch separator visibility OFF if last visibile item
        For MyCounter As Integer = menu.Count - 1 To 0 Step -1
            Select Case menu.Item(MyCounter).GetType
                Case GetType(ToolStripSeparator)
                    If menu.Item(MyCounter).Available = True Then
                        'Current separator is not required
                        SwitchToolStripVisibility(menu.Item(MyCounter), False, False)
                        Return
                    End If
                Case Else
                    If menu.Item(MyCounter).Available = True Then
                        'Another ToolStripItem type is visible, exit here
                        Return
                    End If
            End Select
        Next
    End Sub

    Public Sub AssignGridRowCellStyle(gridRow As DataGridViewRow, newStyle As DataGridViewCellStyle)
        If gridRow.DefaultCellStyle.BackColor <> newStyle.BackColor OrElse
                gridRow.DefaultCellStyle.ForeColor <> newStyle.ForeColor OrElse
                gridRow.DefaultCellStyle.SelectionBackColor = newStyle.SelectionBackColor OrElse
                gridRow.DefaultCellStyle.SelectionForeColor = newStyle.SelectionForeColor Then
            gridRow.DefaultCellStyle = newStyle
        End If
    End Sub

    Public Function AssignGridRowCellStyle(rowBaseStyle As DataGridViewCellStyle, selectionBackColor As Color, selectionForeColor As Color, backColor As Color, Optional foreColor As Color? = Nothing) As DataGridViewCellStyle
        Dim Result As New DataGridViewCellStyle(rowBaseStyle)
        Result.BackColor = backColor
        If foreColor.HasValue Then
            Result.ForeColor = foreColor.Value
        End If
        Result.SelectionBackColor = selectionBackColor
        Result.SelectionForeColor = selectionForeColor
        Return Result
    End Function

    Public Sub SetToolTipText(gridRow As System.Windows.Forms.DataGridViewRow, text As String, Optional additionalText As String = Nothing)
        For Each gridCell As System.Windows.Forms.DataGridViewCell In gridRow.Cells
            Dim ToolTipText As String
            If text <> Nothing AndAlso additionalText <> Nothing Then
                ToolTipText = text & System.Environment.NewLine & System.Environment.NewLine & additionalText
            ElseIf text = Nothing AndAlso additionalText = Nothing Then
                ToolTipText = Nothing
            Else
                ToolTipText = text & additionalText
            End If
            gridCell.ToolTipText = ToolTipText
        Next
    End Sub

    Public Class UploadLinkShareCredentials
        Public Property AvailableUploadUrl As String
        Public Property Password As String
        Public Property LinkTimeout As DateTime?
        ''' <summary>
        ''' In case there is an upload link but with a timeout in past or too close in future, the result leads to recommendation of upload link renewal
        ''' </summary>
        ''' <returns>True if there is an upload link in past or in too close future, False if no upload link has been found or if an upload link has been found with no timeout or timeout in far future</returns>
        Public Property IsAnotherUploadLinkAvailableWithTimeOutOrSoonTimeOut As Boolean
    End Class

    Public Class DownloadLinkShareCredentials
        Public Property AvailableDownloadUrl As String
        Public Property Password As String
        Public Property LinkTimeout As DateTime?
        ''' <summary>
        ''' In case there is a download link but with a timeout in past or too close in future, the result leads to recommendation of download link renewal
        ''' </summary>
        ''' <returns>True if there is a download link in past or in too close future, False if no download link has been found or if a download link has been found with no timeout or timeout in far future</returns>
        Public Property IsAnotherDownloadLinkAvailableWithTimeOutOrSoonTimeOut As Boolean
    End Class

    Public Class ValueOrException(Of T)
        Public Property Value As T
        Public Property CatchedException As Exception

        Public Sub New(value As T)
            Me.Value = value
        End Sub

        Public Sub New(ex As Exception)
            Me.CatchedException = ex
        End Sub
    End Class

    Public Class InternalSharingDetails
        Public Property SharingFolderName As String
        Public Property SharingFolderFullName As String
        Public Property SubFolderWithDataRelativePath As String
        Public Property SubFolderWithDataFullName As String
        ''' <summary>
        ''' In case there is not exact match, there will be a 2nd chance check for alternative sharings
        ''' </summary>
        ''' <returns>True if no exact match found and there is an alternative sharing setup, False if exact match has been found or there is no alterantive sharing setup</returns>
        Public Property IsAnotherSharingAvailableToOtherUsersOrGroups As Boolean
    End Class

    Private Function FilterForUploadLinks(allLinks As List(Of DmsLink)) As List(Of DmsLink)
        Dim Result As New List(Of DmsLink)
        If allLinks IsNot Nothing Then
            For Each Link As DmsLink In allLinks
                If Link.AllowUpload Then
                    Result.Add(Link)
                End If
            Next
        End If
        Return Result
    End Function

    Private Function FilterForDownloadLinks(allLinks As List(Of DmsLink)) As List(Of DmsLink)
        Dim Result As New List(Of DmsLink)
        If allLinks IsNot Nothing Then
            For Each Link As DmsLink In allLinks
                If Link.AllowDownload OrElse Link.AllowView Then
                    Result.Add(Link)
                End If
            Next
        End If
        Return Result
    End Function

    Public Enum DataGridViewContentType
        Value = 0
        FormattedValue = 1
    End Enum

    Public Function ConvertDataGridViewContentToDataTable(grid As DataGridView, contentType As DataGridViewContentType) As DataTable
        Return ConvertDataGridViewContentToDataTable(grid, contentType, False)
    End Function

    Public Function ConvertDataGridViewContentToDataTable(grid As DataGridView, contentType As DataGridViewContentType, visibleRowsOnly As Boolean) As DataTable
        Dim Result As New DataTable(grid.Name)
        For MyColCounter As Integer = 0 To grid.Columns.Count - 1
            Result.Columns.Add(grid.Columns(MyColCounter).Name, GetType(String))
            Result.Columns(MyColCounter).Caption = grid.Columns(MyColCounter).HeaderText
        Next
        For MyRowCounter As Integer = 0 To grid.Rows.Count - 1
            If visibleRowsOnly = False OrElse grid.Rows(MyRowCounter).Visible Then
                Dim Row As DataRow = Result.NewRow
                For MyColCounter As Integer = 0 To grid.Columns.Count - 1
                    Select Case contentType
                        Case DataGridViewContentType.Value
                            If IsDBNull(grid.Rows(MyRowCounter).Cells(MyColCounter).Value) Then
                                Row(MyColCounter) = DBNull.Value
                            Else
                                Row(MyColCounter) = grid.Rows(MyRowCounter).Cells(MyColCounter).Value
                            End If
                        Case DataGridViewContentType.FormattedValue
                            If IsDBNull(grid.Rows(MyRowCounter).Cells(MyColCounter).FormattedValue) Then
                                Row(MyColCounter) = DBNull.Value
                            Else
                                Row(MyColCounter) = grid.Rows(MyRowCounter).Cells(MyColCounter).FormattedValue
                            End If
                        Case Else
                            Throw New ArgumentOutOfRangeException(NameOf(contentType))
                    End Select
                Next
                Result.Rows.Add(Row)
            End If
        Next
        Return Result
    End Function

    Public Function SuggestedDropDownWidth(control As ComboBox) As Integer
        Dim Result As Integer = 0
        For Each obj In control.Items
            Result = System.Math.Max(Result, TextRenderer.MeasureText(control.GetItemText(obj), control.Font).Width)
        Next
        Return System.Math.Max(control.Width, Result + SystemInformation.VerticalScrollBarWidth)
    End Function

    Friend Function NotNaNOrAlternativeValue(firstChoice As Double, alternativeChoice As Double) As Double
        If Not Double.IsNaN(firstChoice) Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Friend Function NotNullOrEmptyStringValue(value As String) As String
        If value <> Nothing Then
            Return value
        Else
            Return String.Empty
        End If
    End Function

    Friend Function NotEmptyOrAlternativeValue(firstChoice As String, alternativeChoice As String) As String
        If firstChoice <> Nothing Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Friend Function NotEmptyOrAlternativeValue(firstChoice As DateTime, alternativeChoice As DateTime) As DateTime
        If firstChoice <> Nothing Then
            Return firstChoice
        Else
            Return alternativeChoice
        End If
    End Function

    Friend Function NotZeroOrAlternativeValueToString(value As Integer, alternativeChoice As String) As String
        If value = Nothing Then
            Return alternativeChoice
        Else
            Return value.ToString
        End If
    End Function

End Module
