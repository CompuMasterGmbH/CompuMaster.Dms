param([ValidateSet('Debug', 'Release', 'CI_CD')][string]$Configuration = 'CI_CD')
$ErrorActionPreference = 'Stop'
if (![OperatingSystem]::IsWindows()) { throw 'WinUI builds require Windows.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach ($demo in @('WinUI', 'WebDav.WinUI', 'Nextcloud.WinUI', 'OwnCloudClassic.WinUI', 'ScopevisioTeamwork.WinUI')) {
    $name = "CompuMaster.Dms.TestDemo.$demo"
    & dotnet build (Join-Path $repoRoot "$name/$name.csproj") -c $Configuration --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "WinUI demo build failed: $name" }
}
