Option Explicit On
Option Strict On

Imports CompuMaster.Dms.Providers
Imports NUnit.Framework

<TestFixture>
Public Class FileVersionCapabilityTest

    <TestCase("None"), TestCase("Manual"), TestCase("WebDAV"), TestCase("CenterDevice"), TestCase("Scopevisio")>
    <Category("TestLevel1")>
    Public Sub BuiltInProvidersDoNotAdvertiseUnimplementedVersionWorkflows(providerName As String)
        Dim provider As BaseDmsProvider
        Select Case providerName
            Case "None" : provider = New NoDmsProvider()
            Case "Manual" : provider = New ManualUrlDmsProvider()
            Case "WebDAV" : provider = New WebDavDmsProvider()
            Case "CenterDevice" : provider = New CenterDeviceDmsProvider()
            Case "Scopevisio" : provider = New ScopevisioTeamworkDmsProvider()
            Case Else : Throw New ArgumentException(NameOf(providerName))
        End Select

        Dim implemented As Boolean = False
#If CENTERDEVICE_FILE_VERSIONS Then
        implemented = providerName = "CenterDevice" OrElse providerName = "Scopevisio"
#End If
        Assert.That(provider.SupportsFileVersionHistory, [Is].EqualTo(implemented))
        Assert.That(provider.SupportsFileVersionRestore, [Is].EqualTo(implemented))
    End Sub

    <TestCase(False, False), TestCase(True, False), TestCase(False, True), TestCase(True, True)>
    <Category("TestLevel1")>
    Public Sub DerivedProvidersCanOptIntoEachWorkflowIndependently(history As Boolean, restore As Boolean)
        Dim provider As BaseDmsProvider = New VersionProvider(history, restore)
        Assert.That(provider.SupportsFileVersionHistory, [Is].EqualTo(history))
        Assert.That(provider.SupportsFileVersionRestore, [Is].EqualTo(restore))
    End Sub

    Private NotInheritable Class VersionProvider
        Inherits NoDmsProvider

        Private ReadOnly HistorySupported As Boolean
        Private ReadOnly RestoreSupported As Boolean

        Public Sub New(history As Boolean, restore As Boolean)
            Me.HistorySupported = history
            Me.RestoreSupported = restore
        End Sub

        Public Overrides ReadOnly Property SupportsFileVersionHistory As Boolean
            Get
                Return Me.HistorySupported
            End Get
        End Property

        Public Overrides ReadOnly Property SupportsFileVersionRestore As Boolean
            Get
                Return Me.RestoreSupported
            End Get
        End Property
    End Class
End Class
