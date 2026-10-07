$ErrorActionPreference = 'Stop'
# Level 2 alone must never grant native server access. Check the workflow's
# explicit opt-in independently of the repository-level parser checks below.
$workflow = [IO.File]::ReadAllText((Join-Path $PSScriptRoot '../workflows/BuildAndTest.yml'))
$nativeJob = [regex]::Match($workflow, '(?ms)^  native-scopevisio-live:\r?\n(?<job>.*)\z').Groups['job'].Value
$nativeGuard = [regex]::Match($nativeJob, '(?m)^    if: (?<guard>.*)$').Groups['guard'].Value.Trim()
if ($nativeGuard -ne "github.event_name == 'workflow_dispatch' && inputs.run_native_scopevisio && needs.prepare-remote-test-matrix.outputs.level == '2'") {
    throw 'Native live jobs must require manual dispatch, explicit native opt-in, and effective Level 2.'
}
$reader = Join-Path $PSScriptRoot 'read-test-level.ps1'
$temporaryFile = [IO.Path]::GetTempFileName()
try {
    foreach ($expected in @('1', '2')) {
        [IO.File]::WriteAllText($temporaryFile, "# Level description and assignment examples`n`nTEST_LEVEL=$expected`n", [Text.UTF8Encoding]::new($true))
        $actual = & $reader -ConfigurationPath $temporaryFile
        if ($actual -ne $expected) { throw 'Repository test-level selection failed.' }
        foreach ($override in @('1', '2')) {
            if ((& $reader -ConfigurationPath $temporaryFile -RequestedLevel $override) -ne $override) { throw 'Manual override failed.' }
        }
    }
    foreach ($invalid in @('', '# Missing setting', 'TEST_LEVEL=3', "TEST_LEVEL=1`nTEST_LEVEL=2", "TEST_LEVEL=1`nUNKNOWN=2", 'TEST_LEVEL=1; Write-Output unexpected')) {
        [IO.File]::WriteAllText($temporaryFile, $invalid, [Text.UTF8Encoding]::new($true))
        $rejected = $false
        try { $null = & $reader -ConfigurationPath $temporaryFile } catch { $rejected = $true }
        if (!$rejected) { throw 'Invalid or ambiguous test-level configuration was accepted.' }
    }
    $configured = & $reader
    Write-Host "Test-level configuration/override/rejection checks passed; repository level: $configured."
} finally {
    Remove-Item -LiteralPath $temporaryFile
}
