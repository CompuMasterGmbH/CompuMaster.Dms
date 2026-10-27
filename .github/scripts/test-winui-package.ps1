param(
    [ValidateSet('Debug', 'Release', 'CI_CD')][string]$Configuration = 'CI_CD',
    [string]$Version = '',
    [string]$PublishedProvidersVersion = '',
    [switch]$RequireClean
)
$ErrorActionPreference = 'Stop'
if (![OperatingSystem]::IsWindows()) { throw 'Native WinUI package verification requires Windows.' }
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
if (!$Version) { $Version = "0.0.0-winui-check.$runId" }
if ($PublishedProvidersVersion -and !$Version.Contains('-')) { throw 'Published provider bootstrap is reserved for prerelease package-ID creation.' }
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Read-Package([string]$Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $reader = [IO.StreamReader]::new(($archive.Entries | Where-Object FullName -Like '*.nuspec').Open())
        try { [xml]$document = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $document.package.metadata
        [pscustomobject]@{
            Id = [string]$metadata.id; Version = [string]$metadata.version; License = [string]$metadata.license.'#text'
            RepositoryCommit = [string]$metadata.repository.commit; Files = @($archive.Entries.FullName)
            Dependencies = @($metadata.SelectNodes(".//*[local-name()='dependency']") | ForEach-Object { [pscustomobject]@{Id=[string]$_.id;Version=[string]$_.version} })
            Path = $Path; Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        }
    } finally { $archive.Dispose() }
}
Push-Location $repoRoot
try {
    $revision = (& git rev-parse HEAD).Trim()
    $dirty = @(& git status --porcelain).Count -ne 0
    if ($RequireClean -and $dirty) { throw 'Commit package inputs before preparing immutable publication evidence.' }
    $workRoot = Join-Path $repoRoot "artifacts/winui-package-$runId"
    if (Test-Path -LiteralPath $workRoot) { throw 'Package-check workspace already exists.' }
    $feed = Join-Path $workRoot 'feed'; $consumer = Join-Path $workRoot 'consumer'
    New-Item -ItemType Directory -Path $feed, $consumer | Out-Null
    $projects = @('CompuMaster.Dms.BrowserUI.WinUI/CompuMaster.Dms.BrowserUI.WinUI.csproj')
    if (!$PublishedProvidersVersion) { $projects = @('CompuMaster.Dms.Providers/CompuMaster.Dms.Providers.vbproj') + $projects }
    foreach ($project in $projects) {
        $pack = @('pack', $project, '-c', $Configuration, "-p:Version=$Version", '-p:ContinuousIntegrationBuild=true', '-o', $feed, '--verbosity', 'quiet')
        if ($PublishedProvidersVersion) { $pack += "-p:DmsProvidersPackageVersion=$PublishedProvidersVersion" }
        Invoke-Dotnet $pack
    }
    $packages = @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' | ForEach-Object { Read-Package $_.FullName })
    $winui = @($packages | Where-Object Id -EQ 'CompuMaster.Dms.BrowserUI.WinUI')
    if ($winui.Count -ne 1 -or $packages.Count -ne $projects.Count) { throw 'Unexpected prepared package IDs/count.' }
    foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.txt', 'WINDOWS-APP-SDK-LICENSE.txt', 'logo_64x64.png',
        'lib/net8.0-windows10.0.19041/CompuMaster.Dms.BrowserUI.WinUI.dll', 'lib/net8.0-windows10.0.19041/CompuMaster.Dms.BrowserUI.WinUI.pri', 'lib/net8.0-windows10.0.19041/CompuMaster.Dms.BrowserUI.WinUI.xml')) {
        if ($winui[0].Files -notcontains $file) { throw "Missing native package entry: $file" }
    }
    if ($winui[0].License -ne 'MIT' -or $winui[0].Dependencies.Id -contains 'CompuMaster.Dms.BrowserUI' -or $winui[0].Dependencies.Id -contains 'CompuMaster.Dms.BrowserUI.Avalonia') { throw 'Unexpected component license or UI dependencies.' }
    foreach ($package in $packages) {
        if (!(Test-Path -LiteralPath ([IO.Path]::ChangeExtension($package.Path, '.snupkg')))) { throw 'Matching symbols are missing.' }
        if ($RequireClean -and $package.RepositoryCommit -ne $revision) { throw 'Package source revision does not match the prepared head.' }
    }
    Copy-Item -Path (Join-Path $repoRoot '.github/winui-package-consumer/*') -Destination $consumer
    Copy-Item -LiteralPath (Join-Path $repoRoot 'CompuMaster.Dms.TestDemo.WinUI/app.manifest') -Destination $consumer
    Copy-Item -LiteralPath (Join-Path $repoRoot 'CompuMaster.Dms.TestDemo.Avalonia/DemoDmsProvider.cs') -Destination $consumer
    $config = Join-Path $consumer 'NuGet.Config'; $feedXml = [Security.SecurityElement]::Escape($feed)
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key='prepared' value='$feedXml'/><add key='nuget.org' value='https://api.nuget.org/v3/index.json'/></packageSources></configuration>", [Text.UTF8Encoding]::new($true))
    $project = Join-Path $consumer 'Consumer.csproj'; $normalizedVersion = $winui[0].Version
    Invoke-Dotnet @('restore', $project, "-p:DmsPackageVersion=$normalizedVersion", '--configfile', $config, '--packages', (Join-Path $workRoot 'packages'), '--verbosity', 'quiet')
    Invoke-Dotnet @('build', $project, '-c', $Configuration, "-p:DmsPackageVersion=$normalizedVersion", '--no-restore', '--verbosity', 'quiet')
    $executable = Join-Path $consumer "bin/$Configuration/net8.0-windows10.0.19041.0/win-x64/Consumer.exe"
    Invoke-Dotnet @('build', 'CompuMaster.Dms.Test.GuiNoticeHost', '-c', $Configuration, '--verbosity', 'quiet')
    $noticeHost = Join-Path $repoRoot "CompuMaster.Dms.Test.GuiNoticeHost/bin/$Configuration/net8.0-windows/CompuMaster.Dms.Test.GuiNoticeHost.exe"
    & $noticeHost $executable | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Independent native WinUI consumer failed.' }
    $success = Join-Path (Split-Path $executable) 'consumer-success.json'
    if (!(Test-Path -LiteralPath $success)) { throw 'Native consumer did not record completed assertions.' }
    $evidence = [ordered]@{SourceCommit=$revision;SourceDirty=$dirty;RequestedVersion=$Version;PreparedAtUtc=[DateTime]::UtcNow.ToString('o');ConsumerTestPassed=$true;PublicationPerformed=$false;Packages=$packages;Consumer=$consumer;WorkRoot=$workRoot}
    [IO.File]::WriteAllText((Join-Path $workRoot 'preparation.json'), ($evidence | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($true))
    Write-Host "Verified native WinUI package and clean consumer: $workRoot"
    [pscustomobject]$evidence
} finally { Pop-Location }
