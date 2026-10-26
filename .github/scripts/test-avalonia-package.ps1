param([string]$Configuration = 'CI_CD')
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runId = [Guid]::NewGuid().ToString('N')
$packageVersion = "0.0.0-avalonia-check.$runId"
$workRoot = Join-Path $repoRoot "artifacts/avalonia-package-$runId"
$feed = Join-Path $workRoot 'feed'
$consumer = Join-Path $workRoot 'consumer'
if (Test-Path -LiteralPath $workRoot) { throw 'Package-check workspace already exists.' }
New-Item -ItemType Directory -Path $feed, $consumer | Out-Null
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}
Push-Location $repoRoot
try {
    Invoke-Dotnet @('pack', 'CompuMaster.Dms.Providers/CompuMaster.Dms.Providers.vbproj', '-c', $Configuration, "-p:PackageVersion=$packageVersion", '-o', $feed, '--verbosity', 'quiet')
    Invoke-Dotnet @('pack', 'CompuMaster.Dms.BrowserUI.Avalonia/CompuMaster.Dms.BrowserUI.Avalonia.csproj', '-c', $Configuration, "-p:PackageVersion=$packageVersion", '-o', $feed, '--verbosity', 'quiet')
    Copy-Item -LiteralPath (Join-Path $repoRoot '.github/avalonia-package-consumer/Consumer.csproj') -Destination $consumer
    Copy-Item -LiteralPath (Join-Path $repoRoot '.github/avalonia-package-consumer/PackageTests.cs') -Destination $consumer
    $project = Join-Path $consumer 'Consumer.csproj'
    $config = Join-Path $consumer 'NuGet.Config'
    $feedXml = [Security.SecurityElement]::Escape($feed)
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key='local' value='$feedXml'/><add key='nuget.org' value='https://api.nuget.org/v3/index.json'/></packageSources></configuration>", [Text.UTF8Encoding]::new($true))
    Invoke-Dotnet @('restore', $project, "-p:DmsPackageVersion=$packageVersion", '--configfile', $config, '--verbosity', 'quiet')
    Invoke-Dotnet @('test', $project, "-p:DmsPackageVersion=$packageVersion", '--no-restore', '--verbosity', 'quiet', '--logger', 'trx', '--results-directory', (Join-Path $workRoot 'results'))
    Write-Host 'Avalonia package installation, resources, localization and dependency isolation passed.'
    Write-Host "Local package evidence: $workRoot"
} finally { Pop-Location }
