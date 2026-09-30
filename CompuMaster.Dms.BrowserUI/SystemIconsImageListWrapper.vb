Imports System
Imports Microsoft.Win32
Imports System.Windows.Forms
Imports System.Drawing
Imports System.Runtime.InteropServices
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging

Friend NotInheritable Class SystemIconsImageListWrapper

    Private Const NumberOfIconsToExtract As UInteger = 1UI

    Public Sub New()
        Me.SIImageList = New ImageList()
        Me.ExtensionSIImageListIndexZuordnung = New Dictionary(Of String, Integer)()
    End Sub

    Public Sub New(ByVal imageList As ImageList, ByVal defaultIconIndex As Integer, ByVal defaultSharedIconIndex As Integer)
        Me.SIImageList = imageList
        Me.ExtensionSIImageListIndexZuordnung = New Dictionary(Of String, Integer)()
        Me.DefaultIconIndex = defaultIconIndex
        Me.DefaultSharedIconIndex = defaultSharedIconIndex
        Me.InitializeDefaultSharedIcon()
    End Sub

    Public Property SIImageList As ImageList
    Private ReadOnly ExtensionSIImageListIndexZuordnung As Dictionary(Of String, Integer)
    Private ReadOnly DefaultIconIndex As Integer = 0
    Private ReadOnly DefaultSharedIconIndex As Integer = 0

    Private Sub InitializeDefaultSharedIcon()
        If Me.SIImageList Is Nothing Then Return
        If Me.DefaultIconIndex < 0 OrElse Me.DefaultIconIndex >= Me.SIImageList.Images.Count Then Return
        If Me.DefaultSharedIconIndex < 0 OrElse Me.DefaultSharedIconIndex >= Me.SIImageList.Images.Count Then Return

        Dim Key As String = Me.SIImageList.Images.Keys(Me.DefaultSharedIconIndex)
        Using SourceIcon As Icon = ImageToIcon(Me.SIImageList.Images(Me.DefaultIconIndex), Me.SIImageList.ImageSize),
            SharedIcon As Icon = Me.OverlaySharedIcon(SourceIcon)
            Dim SharedImage As Bitmap = SharedIcon.ToBitmap()
            If Me.DefaultSharedIconIndex = Me.SIImageList.Images.Count - 1 Then
                Me.SIImageList.Images.RemoveAt(Me.DefaultSharedIconIndex)
                Me.SIImageList.Images.Add(Key, SharedImage)
            Else
                Me.SIImageList.Images(Me.DefaultSharedIconIndex) = SharedImage
                Me.SIImageList.Images.SetKeyName(Me.DefaultSharedIconIndex, Key)
            End If
        End Using
    End Sub

    Public Function GetSIImageListIndexForFileExtension(ByVal extension As String, isShared As Boolean) As Integer
        Select Case System.Environment.OSVersion.Platform
            Case PlatformID.Win32NT, PlatformID.WinCE, PlatformID.Xbox, PlatformID.Win32Windows, PlatformID.Win32S
                Try
                    Dim RValue As Integer = 0
                    If Not extension.StartsWith(".") Then extension = "." & extension

                    If Me.ExtensionSIImageListIndexZuordnung.ContainsKey(isShared & "|" & extension) Then
                        Me.ExtensionSIImageListIndexZuordnung.TryGetValue(isShared & "|" & extension, RValue)
                    Else
                        Using icon As Icon = Me.GetIconForFileExtension(extension, isShared)
                            Me.SIImageList.Images.Add(icon)
                        End Using
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
        If extension = "" OrElse extension = "." Then Return ImageToIcon(Me.SIImageList.Images(Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex)), Me.SIImageList.ImageSize)
#Enable Warning CA1820 ' Test for empty strings using string length
        Dim QryRS As KeyValuePair(Of String, Integer) = Me.GetIconPathForExtension(extension, isShared)
        If Not String.IsNullOrEmpty(QryRS.Key) Then
            Return Me.GetIconFromDLL(QryRS.Key, QryRS.Value, isShared)
        Else
            Return ImageToIcon(Me.SIImageList.Images(Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex)), Me.SIImageList.ImageSize)
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
                Dim IconPathRaw As String = ClassRootKey.OpenSubKey(FileExtSubKeyName)?.OpenSubKey("DefaultIcon")?.GetValue("")?.ToString()
                If IconPathRaw Is Nothing Then
                    Return New KeyValuePair(Of String, Integer)(String.Empty, Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex))
                Else
                    Return New KeyValuePair(Of String, Integer)(IconPathRaw.Split(","c)(0), Integer.Parse(IconPathRaw.Split(","c)(1)))
                End If
            End If
        Catch ex As Exception
            Return New KeyValuePair(Of String, Integer)(String.Empty, Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex))
        End Try
    End Function

    Private Function GetIconFromDLL(ByVal pathToDLL As String, ByVal iconIndex As Integer, isShared As Boolean) As Icon
        Dim Result As Icon = ExtractIconAtSize(pathToDLL, iconIndex, Me.SIImageList.ImageSize)
        If Result Is Nothing Then
            Return ImageToIcon(Me.SIImageList.Images(Tools.IIf(Of Integer)(isShared, Me.DefaultSharedIconIndex, Me.DefaultIconIndex)), Me.SIImageList.ImageSize)
        End If
        If isShared Then
            Using Result
                Return OverlaySharedIcon(Result)
            End Using
        End If
        Return Result
    End Function

    Private Shared Function ExtractIconAtSize(pathToDLL As String, iconIndex As Integer, iconSize As Size) As Icon
        Dim iconHandles(0) As IntPtr
        Dim iconIdentifiers(0) As UInteger
        Dim expandedPath As String = Environment.ExpandEnvironmentVariables(pathToDLL.Trim().Trim(""""c))
        Dim extractedIconCount As UInteger = PrivateExtractIcons(expandedPath, iconIndex, iconSize.Width, iconSize.Height, iconHandles, iconIdentifiers, NumberOfIconsToExtract, 0UI)
        If extractedIconCount = 0UI OrElse extractedIconCount = UInteger.MaxValue OrElse iconHandles(0) = IntPtr.Zero Then Return Nothing

        Try
            Return CType(Icon.FromHandle(iconHandles(0)).Clone(), Icon)
        Finally
            DestroyIcon(iconHandles(0))
        End Try
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function PrivateExtractIcons(ByVal szFileName As String, ByVal nIconIndex As Integer, ByVal cxIcon As Integer, ByVal cyIcon As Integer, <Out> ByVal phicon As IntPtr(), <Out> ByVal piconid As UInteger(), ByVal nIcons As UInteger, ByVal flags As UInteger) As UInteger
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function DestroyIcon(ByVal hIcon As IntPtr) As Boolean
    End Function

    ' Integration contract for #13: the source icon already matches SIImageList.ImageSize.
    ' The shared overlay compositor must return that same size so ImageList does not rescale
    ' the finished composition. Pixel-designed overlay variants should be selected for the
    ' physical target size, with at most one high-quality resize as a fallback.
    ''' <summary>
    ''' Draw an overlay symbolizing a shared file item on top of the icon
    ''' </summary>
    ''' <param name="source"></param>
    ''' <returns></returns>
    Private Function OverlaySharedIcon(source As Icon) As Icon
        Dim targetSize As Size = Me.SIImageList.ImageSize
        Using Target As New Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(Target)
                g.Clear(Color.Transparent)
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.SmoothingMode = SmoothingMode.AntiAlias
                g.DrawIconUnstretched(source, New Rectangle(Point.Empty, targetSize))

                Dim OverlaySize As Integer = Math.Max(1, Math.Min(targetSize.Width, targetSize.Height) \ 2)
                Dim Padding As Integer = 3
                Dim OverlayX As Integer = targetSize.Width - OverlaySize - Padding
                Dim OverlayY As Integer = targetSize.Height - OverlaySize - Padding
                Using BackplateBrush As New SolidBrush(Color.FromArgb(235, Color.White))
                    g.FillEllipse(BackplateBrush, OverlayX - 2, OverlayY - 2, OverlaySize + 4, OverlaySize + 4)
                End Using
                Using BackplatePen As New Pen(Color.FromArgb(180, 160, 160, 160), 1.0F)
                    g.DrawEllipse(BackplatePen, OverlayX - 2, OverlayY - 2, OverlaySize + 4, OverlaySize + 4)
                End Using
                g.DrawImage(My.Resources.Resources.sharing, OverlayX, OverlayY, OverlaySize, OverlaySize)
                g.Flush()
            End Using
            Return ImageToIcon(Target, targetSize)
        End Using
    End Function

    Private Shared Function ImageToIcon(ByVal img As Image, targetSize As Size) As Icon
        Using square As New Bitmap(targetSize.Width, targetSize.Height, PixelFormat.Format32bppArgb)
            Using g As Graphics = Graphics.FromImage(square)
                Dim x As Integer
                Dim y As Integer
                Dim w As Integer
                Dim h As Integer
                Dim r As Single = CSng(img.Width) / CSng(img.Height)

                If r > 1 Then
                    w = targetSize.Width
                    h = CInt((CSng(targetSize.Width) / r))
                    x = 0
                    y = CType((targetSize.Height - h) / 2, Integer)
                Else
                    w = CInt((CSng(targetSize.Height) * r))
                    h = targetSize.Height
                    y = 0
                    x = CType((targetSize.Width - w) / 2, Integer)
                End If

                g.Clear(Color.Transparent)
                g.CompositingQuality = CompositingQuality.HighQuality
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.PixelOffsetMode = PixelOffsetMode.HighQuality
                g.SmoothingMode = SmoothingMode.HighQuality
                g.DrawImage(img, x, y, w, h)
            End Using

            Dim iconHandle As IntPtr = square.GetHicon()
            Try
                Return CType(Icon.FromHandle(iconHandle).Clone(), Icon)
            Finally
                DestroyIcon(iconHandle)
            End Try
        End Using
    End Function

End Class
