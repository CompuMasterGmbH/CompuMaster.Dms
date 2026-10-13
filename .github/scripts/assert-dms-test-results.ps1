param(
    [Parameter(Mandatory = $true)][string]$Path,
    [ValidateRange(1, 1000000)][int]$MinimumPassed = 1
)
$ErrorActionPreference = 'Stop'
[xml]$results = Get-Content -LiteralPath $Path -Raw
$counters = $results.TestRun.ResultSummary.Counters
if ($null -eq $counters -or [int]$counters.executed -le 0) { throw 'The selected test set executed no tests; empty or entirely skipped discovery is not acceptance.' }
if ([int]$counters.passed -lt $MinimumPassed) { throw 'The selected test set did not pass the required number of tests; missing, skipped or inconclusive cases are not acceptance.' }
if ([int]$counters.failed -ne 0) { throw 'The selected test set contains failures.' }
Write-Host "Executed $($counters.executed), passed $($counters.passed), failed $($counters.failed), not executed $($counters.notExecuted)."
