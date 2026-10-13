$ErrorActionPreference = 'Stop'
$filter = Join-Path $PSScriptRoot 'get-remote-dms-test-filter.ps1'
foreach ($server in @('ScopevisioTeamwork', 'OwnCloud', 'WebDav', 'Nextcloud')) {
    foreach ($level in @('1', '2')) {
        $normal = & $filter -ServerCategory $server -TestLevel $level
        if (!$normal.Contains('TestCategory!=ReadOnlyDiagnostic')) { throw 'Normal jobs must exclude diagnostic fixtures.' }
    }
}
foreach ($server in @('ScopevisioTeamwork', 'OwnCloud')) {
    $selected = & $filter -ServerCategory $server -ReadOnlyDiagnostics
    if (!$selected.Contains('TestCategory=ReadOnlyDiagnostic') -or !$selected.Contains('FullyQualifiedName~')) {
        throw 'Diagnostics must select the separate read-only fixture explicitly.'
    }
}
foreach ($server in @('WebDav', 'Nextcloud')) {
    $rejected = $false
    try { $null = & $filter -ServerCategory $server -ReadOnlyDiagnostics } catch { $rejected = $true }
    if (!$rejected) { throw 'Unplanned diagnostic backend was accepted.' }
}
$guard = Join-Path $PSScriptRoot 'assert-readonly-diagnostic-window.ps1'
$priorFlags = @{}
foreach ($name in @('GITHUB_ACTIONS', 'DMS_READ_ONLY_DIAGNOSTICS', 'DMS_DIAGNOSTIC_WINDOW_UNTIL')) {
    $priorFlags[$name] = [Environment]::GetEnvironmentVariable($name)
}
try {
    $env:GITHUB_ACTIONS = 'true'
    $env:DMS_READ_ONLY_DIAGNOSTICS = 'true'
    foreach ($invalid in @('', 'invalid', '2099-10-13T11:00:00', [DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o'), [DateTimeOffset]::UtcNow.AddMinutes(19).ToString('o'))) {
        $env:DMS_DIAGNOSTIC_WINDOW_UNTIL = $invalid
        $rejected = $false
        try { & $guard } catch { $rejected = $true }
        if (!$rejected) { throw 'Missing, ambiguous or insufficient diagnostic window accepted.' }
    }
    $env:DMS_DIAGNOSTIC_WINDOW_UNTIL = [DateTimeOffset]::UtcNow.AddHours(1).ToString('o')
    $env:GITHUB_ACTIONS = 'false'
    $rejected = $false
    try { & $guard } catch { $rejected = $true }
    if (!$rejected) { throw 'Local diagnostic execution accepted.' }
    $env:GITHUB_ACTIONS = 'true'
    $env:DMS_READ_ONLY_DIAGNOSTICS = 'false'
    $rejected = $false
    try { & $guard } catch { $rejected = $true }
    if (!$rejected) { throw 'Implicit diagnostic execution accepted.' }
    $env:DMS_READ_ONLY_DIAGNOSTICS = 'true'
    & $guard
} finally {
    foreach ($name in $priorFlags.Keys) { [Environment]::SetEnvironmentVariable($name, $priorFlags[$name]) }
}
Write-Host 'Isolated diagnostic selector and CI/expiry admission checks passed; no server or profile access.'
