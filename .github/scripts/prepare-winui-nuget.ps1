param(
    [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+\.\d+\.\d+(-[0-9A-Za-z]+([.-][0-9A-Za-z]+)*)?$')][string]$Version,
    [ValidateSet('Debug', 'Release', 'CI_CD')][string]$Configuration = 'Release',
    [string]$PublishedProvidersVersion = ''
)
$ErrorActionPreference = 'Stop'
$evidence = & (Join-Path $PSScriptRoot 'test-winui-package.ps1') -Version $Version -Configuration $Configuration -PublishedProvidersVersion $PublishedProvidersVersion -RequireClean
$commands = @('# PREVIEW ONLY. Preparation never performs a push or reads an API key.', '# Production publication uses the approved merged-main GitHub release and the publish workflow.')
foreach ($package in $evidence.Packages | Sort-Object @{Expression={if($_.Id -eq 'CompuMaster.Dms.Providers'){0}else{1}}}) {
    $commands += 'dotnet nuget push "{0}" --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY --skip-duplicate' -f $package.Path
}
[IO.File]::WriteAllText((Join-Path $evidence.WorkRoot 'push-preview.txt'), ($commands -join "`r`n"), [Text.UTF8Encoding]::new($true))
Write-Host "Prepared immutable WinUI publication evidence: $($evidence.WorkRoot)"
Write-Host 'No NuGet push, API-key lookup, Git tag or GitHub release was performed.'
