$ErrorActionPreference = 'Stop'
$remote = Join-Path $PSScriptRoot 'get-remote-dms-test-filter.ps1'
foreach ($server in @('ScopevisioTeamwork', 'OwnCloud', 'WebDav', 'Nextcloud')) {
    $full = & $remote -ServerCategory $server -TestLevel 2
    if ($full -match 'FullyQualifiedName' -or !$full.Contains('TestCategory=TestLevel2')) { throw 'Full remote coverage was narrowed.' }
    $sample = & $remote -ServerCategory $server -TestLevel 2 -Coverage Sample
    if (!$sample.Contains("TestCategory=$server") -or !$sample.Contains('TestCategory!=ReadOnlyDiagnostic') -or !$sample.Contains('BasicFileRoundTripAndCleanup')) { throw 'Sample lost its server partition, diagnostic exclusion or owned lifecycle.' }
    $expected = if ($server -eq 'WebDav') { 3 } else { 4 }
    if ([regex]::Matches($sample, 'FullyQualifiedName[=~]').Count -ne $expected) { throw 'Remote sample size changed.' }
    $rejected = $false
    try { $null = & $remote -ServerCategory $server -Coverage Sample -ReadOnlyDiagnostics } catch { $rejected = $true }
    if (!$rejected) { throw 'Sample selection must not broaden a read-only diagnostic.' }
}
$isolated = & (Join-Path $PSScriptRoot 'get-isolated-dms-test-filter.ps1') -Coverage Sample
if (!$isolated.StartsWith('TestCategory!=RemoteDms&(')) { throw 'Isolated sample lost its remote exclusion.' }
foreach ($project in @('CenterDevice.Rest.AsyncTests.csproj', 'CompuMaster.Scopevisio.OpenApi.AsyncTests.csproj', 'Scopevisio.Teamwork.AsyncTests.csproj', 'CompuMaster.Dms.Test.Providers.vbproj')) {
    $filter = & (Join-Path $PSScriptRoot 'get-native-ci-test-filter.ps1') -Coverage Sample -TestProject $project
    if (!$filter.StartsWith('TestCategory!=RemoteDms&(')) { throw 'Native sample lost isolation or selected an empty filter.' }
}
# Validate the result guard against real TRX schema shapes, including zero/all-skipped/failure results.
$temporary = [IO.Path]::GetTempFileName()
try {
    foreach ($counts in @(@(0, 0, 0, $false), @(0, 3, 0, $false), @(2, 1, 1, $false), @(2, 0, 0, $false), @(2, 0, 2, $true))) {
        [IO.File]::WriteAllText($temporary, ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><ResultSummary><Counters executed="{0}" failed="{1}" passed="{2}" notExecuted="3" /></ResultSummary></TestRun>' -f $counts[0], $counts[1], $counts[2]))
        $accepted = $true
        try { & (Join-Path $PSScriptRoot 'assert-dms-test-results.ps1') -Path $temporary } catch { $accepted = $false }
        if ($accepted -ne $counts[3]) { throw 'Empty/skipped/failed TRX acceptance guard failed.' }
    }
} finally { Remove-Item -LiteralPath $temporary }
Write-Host 'Isolated full/sample selectors and non-empty result guards passed; no credentials or server access.'
