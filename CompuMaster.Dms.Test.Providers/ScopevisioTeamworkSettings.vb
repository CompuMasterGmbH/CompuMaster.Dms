Imports System.Text
Imports NUnit.Framework

<TestFixture> Public NotInheritable Class ScopevisioTeamworkSettings
    Inherits SettingsBase

    Public Overrides ReadOnly Property AppTitleInBufferFile As String
        Get
            Return "Scopevisio.Teamwork.Test"
        End Get
    End Property

    Public Overrides ReadOnly Property AppTitleInEnvironmentVariable As String
        Get
            Return "ScopevisioTeamwork".ToUpperInvariant
        End Get
    End Property

End Class