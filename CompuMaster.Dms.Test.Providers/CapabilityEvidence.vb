Imports System.IO

'CI-only reports contain explicitly selected capability fields, never identities or response bodies.
Friend Module CapabilityEvidence
    Friend Sub Record(name As String, observation As Object)
        Dim workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
        If String.IsNullOrEmpty(workspace) Then Return
        Dim folder = Path.Combine(workspace, "test-results", "capabilities")
        Directory.CreateDirectory(folder)
        Dim json = Newtonsoft.Json.JsonConvert.SerializeObject(observation, Newtonsoft.Json.Formatting.Indented)
        File.WriteAllText(Path.Combine(folder, name & ".json"), json.Replace(vbCrLf, vbLf).Replace(vbLf, vbCrLf), New System.Text.UTF8Encoding(True))
    End Sub
End Module
