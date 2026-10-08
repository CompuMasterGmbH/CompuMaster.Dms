Imports System.Drawing

'Linked into each demo assembly with the same embedded default artwork.
Friend NotInheritable Class DemoLoginArtwork
    Friend Shared Function CreateImage(customImage As Image) As Image
        If customImage IsNot Nothing Then Return New Bitmap(customImage)
        Using stream = GetType(DemoLoginArtwork).Assembly.GetManifestResourceStream("CompuMaster.Dms.DemoLoginArtwork.png")
            If stream Is Nothing Then Throw New InvalidOperationException("The embedded demo login artwork is missing.")
            Using source = Image.FromStream(stream)
                Return New Bitmap(source)
            End Using
        End Using
    End Function
End Class
