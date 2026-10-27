param([ValidateSet('Debug', 'Release', 'CI_CD')][string]$Configuration = 'CI_CD')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$projects = @('Avalonia', 'WebDav.Avalonia', 'Nextcloud.Avalonia', 'OwnCloudClassic.Avalonia', 'ScopevisioTeamwork.Avalonia')
foreach ($demo in $projects) {
    $name = "CompuMaster.Dms.TestDemo.$demo"
    Write-Host "Building $name"
    & dotnet build (Join-Path $repoRoot "$name/$name.csproj") -c $Configuration --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Avalonia demo build failed: $name" }
}
