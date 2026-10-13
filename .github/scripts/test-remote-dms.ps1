param(
    [ValidateSet('repository', '1', '2')][string]$TestLevel = 'repository',
    [Parameter(Mandatory = $true)][ValidateSet('ScopevisioTeamwork', 'OwnCloud', 'WebDav', 'Nextcloud')][string]$ServerCategory,
    [string]$Configuration = 'CI_CD',
    [switch]$ListTests,
    [switch]$ReadOnlyDiagnostics
)

$ErrorActionPreference = 'Stop'
$TestLevel = & (Join-Path $PSScriptRoot 'read-test-level.ps1') -RequestedLevel $TestLevel
if ($ReadOnlyDiagnostics) { & (Join-Path $PSScriptRoot 'assert-readonly-diagnostic-window.ps1') }
# Level 2 is cumulative. Unclassified remote tests must fail the isolated partition audit.
$testFilter = & (Join-Path $PSScriptRoot 'get-remote-dms-test-filter.ps1') -TestLevel $TestLevel -ServerCategory $ServerCategory -ReadOnlyDiagnostics:$ReadOnlyDiagnostics
Write-Host "Remote DMS test level $TestLevel; server partition $ServerCategory; filter: $testFilter"
# Measure execution separately from compilation, without interrupting teardown at the target duration.
& dotnet build 'CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj' --framework net8.0 --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Remote DMS test build failed.' }
$arguments = @('test', 'CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj',
    '--framework', 'net8.0', '--configuration', $Configuration, '--no-build', '--no-restore', '--filter', $testFilter)
if ($ListTests) {
    $arguments += '--list-tests'
} else {
    $arguments += @('--results-directory', 'test-results', '--logger', 'junit')
    if ($ReadOnlyDiagnostics) { $arguments += @('--logger', 'trx;LogFileName=read-only-diagnostics.trx') }
}
$execution = [Diagnostics.Stopwatch]::StartNew()
& dotnet @arguments
$testExitCode = $LASTEXITCODE
$execution.Stop()
if (!$ListTests) {
    $minutes = $execution.Elapsed.TotalMinutes.ToString('F2', [Globalization.CultureInfo]::InvariantCulture)
    $timing = if ($ReadOnlyDiagnostics) {
        "Read-only $ServerCategory diagnostic execution: $minutes minutes (no resource-mutating setup or cleanup; compilation and queue waits excluded)."
    } else {
        "Level $TestLevel / $ServerCategory test execution including setup and cleanup: $minutes minutes (excluding compilation and queue waits)."
    }
    Write-Host $timing
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value $timing }
    if (!$ReadOnlyDiagnostics -and $TestLevel -eq '1' -and $execution.Elapsed.TotalMinutes -gt 8) {
        Write-Warning 'Level 1 exceeded the accepted eight-minute range. Review the test scope and timing; cleanup was allowed to complete.'
    }
}
if ($testExitCode -ne 0) { throw "Remote DMS Level $TestLevel tests failed for $ServerCategory." }
