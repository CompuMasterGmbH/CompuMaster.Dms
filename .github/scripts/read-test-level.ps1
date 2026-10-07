param(
    [ValidateSet('repository', '1', '2')][string]$RequestedLevel = 'repository',
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot '../../testing.ci-pipeline-attributes')
)

$ErrorActionPreference = 'Stop'
$entries = @(Get-Content -LiteralPath $ConfigurationPath | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' -and !$_.StartsWith('#') })
if ($entries.Count -ne 1 -or $entries[0] -notmatch '^TEST_LEVEL=([12])$') {
    throw 'Invalid test-level control file: require exactly one TEST_LEVEL=1 or TEST_LEVEL=2 entry; other lines must be comments or blank.'
}
$repositoryLevel = $Matches[1]
if ($RequestedLevel -eq 'repository') { $repositoryLevel } else { $RequestedLevel }
