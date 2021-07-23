Imports System
Imports System.Runtime
Imports Microsoft.Win32
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Drawing.Drawing2D

Friend NotInheritable Class SystemIconsImageListWrapper

    Public Sub New()
        Me.SIImageList = New ImageList()
        Me.ExtensionSIImageListIndexZuordnung = New Dictionary(Of String, Integer)()
    End Sub

    Public Sub New(ByVal imageList As ImageList, ByVal defaultIconIndex As Integer, ByVal defaultSharedIconIndex As Integer)
        Me.SIImageList = imageList
        Me.ExtensionSIImageListIndexZuordnung = New Dictionary(Of String, Integer)()
        Me.DefaultIconIndex = defaultIconIndex
        Me.DefaultSharedIconIndex = defaultSharedIconIndex
    End Sub

    Public Property SIImageList As ImageList
    Private ReadOnly ExtensionSIImageListIndexZuordnung As Dictionary(Of String, Integer)
    Private ReadOnly DefaultIconIndex As Integer = 0
    Private ReadOnly DefaultSharedIconIndex As Integer = 0

    Public Function GetSIImageListIndexForFileExtension(ByVal extension As String, isShared As Boolean) As Integer
        Select Case System.Environment.OSVersion.Platform
            Case PlatformID.Win32NT, PlatformID.WinCE, PlatformID.Xbox, PlatformID.Win32Windows, PlatformID.Win32S
                Try
                    Dim RValue As Integer = 0
                    If Not extension.StartsWith(".") Then extension = "." & extension

                    If Me.ExtensionSIImageListIndexZuordnung.ContainsKey(isShared & "|" & extension) Then
                        Me.ExtensionSIImageListIndexZuordnung.TryGetValue(isShared & "|" & extension, RValue)
                    Else
                        Me.SIImageList.Images.Add(Me.GetIconForFileExtension(extension, isShared))
                        Me.ExtensionSIImageListIndexZuordnung.Add(isShared & "|" & extension, Me.SIImageList.Images.Count - 1)
                        Me.ExtensionSIImageListIndexZuordnung.TryGetValue(isShared & "|" & extension, RValue)
                    End If

                    Return RValue
                Catch ex As Exception
                    ex.ToString()
                    If isShared Then
                        Return Me.DefaultSharedIconIndex
                    Else
                        Return Me.DefaultIconIndex
                    End If
                End Try
            Case Else
                If isShared Then
                    Return Me.DefaultSharedIconIndex
                Else
                    Return Me.DefaultIconIndex
                End If
        End Select
    End Function

    Private Function GetIconForFileExtension(ByVal extension As String, isShared As Boolean) As Icon
#Disable Warning CA1820 ' Test for empty strings using string length
        If extension = "" OrElse extension = "." Then Return ImageToIcon(Me.SIImageList.Images(Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex)))
#Enable Warning CA1820 ' Test for empty strings using string length
        Dim QryRS As KeyValuePair(Of String, Integer) = Me.GetIconPathForExtension(extension, isShared)
        If Not String.IsNullOrEmpty(QryRS.Key) Then
            Return Me.GetIconFromDLL(QryRS.Key, QryRS.Value, isShared)
        Else
            Return ImageToIcon(Me.SIImageList.Images(Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex)))
        End If
    End Function

    Private Function GetIconPathForExtension(ByVal extension As String, isShared As Boolean) As KeyValuePair(Of String, Integer)
        If Not extension.StartsWith(".") Then extension = "." & extension
        Try
            Dim ClassRootKey As RegistryKey = Registry.ClassesRoot
            Dim FileExtSubKeyName As String = ClassRootKey.OpenSubKey(extension)?.GetValue("")?.ToString()
            If FileExtSubKeyName Is Nothing Then
                Return New KeyValuePair(Of String, Integer)(String.Empty, Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex))
            Else
                Dim IconPathRaw As String = ClassRootKey.OpenSubKey(FileExtSubKeyName).OpenSubKey("DefaultIcon").GetValue("").ToString()
                Return New KeyValuePair(Of String, Integer)(IconPathRaw.Split(","c)(0), Integer.Parse(IconPathRaw.Split(","c)(1)))
            End If
        Catch ex As Exception
            Return New KeyValuePair(Of String, Integer)(String.Empty, Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex))
        End Try
    End Function

    Private Function GetIconFromDLL(ByVal pathToDLL As String, ByVal iconIndex As Integer, isShared As Boolean) As Icon
        Dim EigenesProzessHandle As IntPtr = Process.GetCurrentProcess().Handle
        Dim DLLIconPointer As IntPtr = ExtractIcon(EigenesProzessHandle, pathToDLL, iconIndex)
        Dim Result As Icon = Icon.FromHandle(DLLIconPointer)
        If isShared Then
            Result = OverlaySharedIcon(Result)
        End If
        Return Result
    End Function

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode)> Private Shared Function ExtractIcon(ByVal hInst As IntPtr, ByVal lpszExeFileName As String, ByVal nIconIndex As Integer) As IntPtr
    End Function

#Disable Warning IDE0060 ' Nicht verwendete Parameter entfernen
    ''' <summary>
    ''' Draw an overlay symbolizing a shared file item on top of the icon
    ''' </summary>
    ''' <param name="source"></param>
    ''' <returns></returns>
    Private Function OverlaySharedIcon(source As Icon) As Icon
#Enable Warning IDE0060 ' Nicht verwendete Parameter entfernen
        'WORKAROUND: until fully implemented, just return the default shared icon
        Return ImageToIcon(Me.SIImageList.Images(Me.DefaultSharedIconIndex))
    End Function

    Private Shared Function ImageToIcon(ByVal img As Image) As Icon
        Dim size As Integer = 24

        Using square As New Bitmap(size, size)
            Dim g As Graphics = Graphics.FromImage(square)
            Dim x As Integer
            Dim y As Integer
            Dim w As Integer
            Dim h As Integer
            Dim r As Single = CSng(img.Width) / CSng(img.Height)

            If r > 1 Then
                w = size
                h = CInt((CSng(size) / r))
                x = 0
                y = CType((size - h) / 2, Integer)
            Else
                w = CInt((CSng(size) * r))
                h = size
                y = 0
                x = CType((size - w) / 2, Integer)
            End If

            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.DrawImage(img, x, y, w, h)
            g.Flush()
            Return Icon.FromHandle(square.GetHicon())
        End Using
    End Function

End Class