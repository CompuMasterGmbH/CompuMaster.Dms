param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('WebDav', 'Nextcloud', 'OwnCloudClassic', 'ScopevisioTeamwork')][string]$Target,
    [string]$Configuration = 'CI_CD'
)

$ErrorActionPreference = 'Stop'
# Invoke only inside the existing server/account concurrency group. This check performs
# authorization and reads the configured root; it has no remote resource setup/cleanup.
$prefix = switch ($Target) {
    'WebDav' { 'WEBDAV' }
    'Nextcloud' { 'CMNEXTCLOUD' }
    'OwnCloudClassic' { 'CMOWNCLOUD' }
    'ScopevisioTeamwork' { 'SCOPEVISIOTEAMWORK' }
}
$required = @('USERNAME', 'PASSWORD', $(if ($Target -eq 'ScopevisioTeamwork') { 'CUSTOMERNO' } else { 'SERVERURL' }))
$configured = @($required | Where-Object { [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable("TEST_${prefix}_$_")) }).Count -eq 0
if (!$configured) {
    Write-Host "::notice::$Target Avalonia live login was not executed: the required CI configuration is unavailable."
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "$Target Avalonia live login: NOT EXECUTED (credentials/server configuration unavailable)." }
    return
}
$env:DMS_AVALONIA_LIVE_LOGIN = '1'
$env:DMS_AVALONIA_LIVE_TARGET = $Target
& dotnet test CompuMaster.Dms.Test.BrowserUI.Avalonia/CompuMaster.Dms.Test.BrowserUI.Avalonia.csproj -c $Configuration --filter 'FullyQualifiedName=CompuMaster.Dms.Test.BrowserUI.Avalonia.DemoLiveLoginTests.Cached_credentials_authorize_and_initialize_the_actual_browser_read_only' --verbosity quiet --logger trx --results-directory artifacts/avalonia-live-login
if ($LASTEXITCODE -ne 0) { throw "$Target Avalonia live login failed; credential and provider diagnostic values are omitted." }
$demoResults = @(Get-ChildItem -LiteralPath artifacts/avalonia-live-login -Filter '*.trx')
if ($demoResults.Count -ne 1) { throw 'Expected exactly one live-login result file.' }
[xml]$demoResult = Get-Content -LiteralPath $demoResults[0].FullName -Raw
$demoCounters = $demoResult.TestRun.ResultSummary.Counters
if ([int]$demoCounters.passed -ne 1 -or [int]$demoCounters.total -ne 1) { throw 'The live-login test did not execute and pass exactly once.' }
if ($env:GITHUB_STEP_SUMMARY) { Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value "$Target Avalonia live login: 1 PASSED (authorization and configured root initialization only; no resource mutations)." }
