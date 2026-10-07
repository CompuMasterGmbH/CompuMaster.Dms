$ErrorActionPreference = 'Stop'
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
