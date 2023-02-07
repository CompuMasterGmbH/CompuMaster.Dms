Public Class TeamworkBrowser

    Public Sub New()
#Disable Warning BC40000 ' Typ oder Element ist veraltet
        MyBase.New()
#Enable Warning BC40000 ' Typ oder Element ist veraltet

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        Dim dummy = MyBase.DmsProvider
    End Sub


End Class