$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:DMS_READ_ONLY_DIAGNOSTICS -ne 'true') {
    throw 'Read-only diagnostics require the explicit CI-only mode.'
}
$expiry = [DateTimeOffset]::MinValue
$value = $env:DMS_DIAGNOSTIC_WINDOW_UNTIL
if ($value -notmatch '(Z|[+-][0-9]{2}:[0-9]{2})$' -or
    ![DateTimeOffset]::TryParse($value, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$expiry) -or
    $expiry - [DateTimeOffset]::UtcNow -lt [TimeSpan]::FromMinutes(20)) {
    throw 'Read-only diagnostics require a confirmed offset-bearing exclusive window with at least twenty minutes remaining.'
}

# This value is never evaluated, echoed, converted to a path, or accepted as a prefix.
$witness = $env:DMS_DIAGNOSTIC_TEMPORARY_NAME
if ($witness -and $witness -cnotmatch '\A\.compumaster-dms-[0-9a-f]{32}\z') {
    throw 'Invalid exact historical temporary-name witness.'
}
