param([ValidateSet('Debug', 'Release', 'CI_CD')][string]$Configuration = 'CI_CD')
$ErrorActionPreference = 'Stop'
if (![OperatingSystem]::IsWindows()) { throw 'Native WinUI tests require Windows.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $repoRoot
try {
    & dotnet build CompuMaster.Dms.Test.BrowserUI.WinUI -c $Configuration --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'WinUI test host build failed.' }
    New-Item -ItemType Directory -Path artifacts/winui-tests -Force | Out-Null
    $executable = Join-Path $repoRoot "CompuMaster.Dms.Test.BrowserUI.WinUI/bin/$Configuration/net8.0-windows10.0.19041.0/win-x64/CompuMaster.Dms.Test.BrowserUI.WinUI.exe"
    & $executable --workers=0 '--result=artifacts/winui-tests/native-results.xml;format=nunit3'
    if ($LASTEXITCODE -ne 0) { throw 'Native WinUI offline tests failed.' }
    [xml]$results = Get-Content -LiteralPath artifacts/winui-tests/native-results.xml -Raw
    if ($results.'test-run'.result -ne 'Passed' -or [int]$results.'test-run'.total -eq 0 -or [int]$results.'test-run'.skipped -ne 0) {
        throw 'WinUI test results did not establish a complete passing offline run.'
    }
} finally { Pop-Location }
