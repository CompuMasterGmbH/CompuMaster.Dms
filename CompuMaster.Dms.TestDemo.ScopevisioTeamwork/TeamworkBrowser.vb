Public Class TeamworkBrowser

    ''' <summary>Creates the designer-only demo browser with the provider-independent default icon.</summary>
    <Obsolete("Use overload instead")>
    <System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>
    Public Sub New()
#Disable Warning BC40000 ' Typ oder Element ist veraltet
        MyBase.New()
#Enable Warning BC40000 ' Typ oder Element ist veraltet

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        Dim dummy = MyBase.DmsProvider
    End Sub

    ''' <summary>Creates the demo browser with the specified icon.</summary>
    ''' <param name="formIcon">The window icon, or <see langword="Nothing"/> to use the provider-independent default icon.</param>
    Public Sub New(formIcon As System.Drawing.Icon)
#Disable Warning BC40000
        Me.New()
#Enable Warning BC40000
        If formIcon IsNot Nothing Then Me.Icon = formIcon
    End Sub


End Class
