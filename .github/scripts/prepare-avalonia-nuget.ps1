param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')][string]$Version,
    [string]$Configuration = 'Release',
    [string]$PublishedProvidersVersion = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Invoke-Dotnet([string[]]$Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($Arguments[0]) failed with exit code $LASTEXITCODE." }
}
function Read-Package([string]$Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
        if ($entry.Count -ne 1) { throw 'Expected one package manifest.' }
        $reader = [IO.StreamReader]::new($entry[0].Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $manifest.package.metadata
        [pscustomobject]@{
            Id = [string]$metadata.id; Version = [string]$metadata.version
            Authors = [string]$metadata.authors; Description = [string]$metadata.description
            License = [string]$metadata.license.'#text'; ProjectUrl = [string]$metadata.projectUrl
            Repository = [string]$metadata.repository.url; RepositoryCommit = [string]$metadata.repository.commit
            Tags = [string]$metadata.tags; ReleaseNotes = [string]$metadata.releaseNotes
            Dependencies = @($metadata.SelectNodes(".//*[local-name()='dependency']") | ForEach-Object {
                [pscustomobject]@{ Id = [string]$_.id; Version = [string]$_.version }
            })
            Files = @($archive.Entries.FullName); Path = $Path
            Sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        }
    } finally { $archive.Dispose() }
}
Push-Location $repoRoot
try {
    $revision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source revision.' }
    if (@(& git status --porcelain).Count -ne 0) { throw 'Commit package inputs before preparing immutable package evidence.' }
    $bootstrap = ![string]::IsNullOrWhiteSpace($PublishedProvidersVersion)
    if ($bootstrap -and !$Version.Contains('-')) { throw 'Using published providers here is reserved for an explicitly authorized prerelease package-ID bootstrap.' }
    $versionParts = $Version.Split('-', 2)
    $versionSuffix = if ($versionParts.Count -gt 1) { $versionParts[1] } else { '' }
    $runId = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $workRoot = Join-Path $repoRoot "artifacts/avalonia-nuget-$Version-$($revision.Substring(0, 7))-$runId"
    $feed = Join-Path $workRoot 'feed'
    $consumer = Join-Path $workRoot 'consumer'
    if (Test-Path -LiteralPath $workRoot) { throw 'Preparation directory already exists.' }
    New-Item -ItemType Directory -Path $feed, $consumer | Out-Null
    $projects = @('CompuMaster.Dms.BrowserUI.Avalonia/CompuMaster.Dms.BrowserUI.Avalonia.csproj')
    if (!$bootstrap) { $projects = @('CompuMaster.Dms.Providers/CompuMaster.Dms.Providers.vbproj') + $projects }
    foreach ($project in $projects) {
        $packArguments = @('pack', $project, '-c', $Configuration, "-p:VersionPrefix=$($versionParts[0])",
            "-p:VersionSuffix=$versionSuffix", '-p:ContinuousIntegrationBuild=true', '-o', $feed, '--verbosity', 'quiet')
        if ($bootstrap) { $packArguments += "-p:DmsProvidersPackageVersion=$PublishedProvidersVersion" }
        Invoke-Dotnet $packArguments
    }
    $packages = @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' | ForEach-Object { Read-Package $_.FullName })
    if ($packages.Count -ne $projects.Count) { throw 'Unexpected number of prepared packages.' }
    $avalonia = @($packages | Where-Object Id -EQ 'CompuMaster.Dms.BrowserUI.Avalonia')
    $provider = @($packages | Where-Object Id -EQ 'CompuMaster.Dms.Providers')
    if ($avalonia.Count -ne 1 -or $provider.Count -ne [int](!$bootstrap)) { throw 'Unexpected package IDs.' }
    $expectedProviders = if ($bootstrap) { $PublishedProvidersVersion } else { $provider[0].Version }
    $providerDependency = @($avalonia[0].Dependencies | Where-Object Id -EQ 'CompuMaster.Dms.Providers')
    if ($providerDependency.Count -ne 1 -or $providerDependency[0].Version -ne $expectedProviders) { throw 'Unexpected provider dependency version.' }
    foreach ($package in $packages) {
        if ($package.RepositoryCommit -ne $revision) { throw 'Package source revision does not match the prepared head.' }
        $symbols = [IO.Path]::ChangeExtension($package.Path, '.snupkg')
        if (!(Test-Path -LiteralPath $symbols)) { throw 'Missing symbol package.' }
    }
    foreach ($file in @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.txt', 'logo_64x64.png',
        'lib/net8.0/CompuMaster.Dms.BrowserUI.Avalonia.dll', 'lib/net8.0/CompuMaster.Dms.BrowserUI.Avalonia.xml')) {
        if ($avalonia[0].Files -notcontains $file) { throw "Missing Avalonia package entry: $file" }
    }
    if ($avalonia[0].License -ne 'MIT') { throw 'Expected the MIT package license.' }
    if ($avalonia[0].Dependencies.Id -contains 'CompuMaster.Dms.BrowserUI') { throw 'Avalonia must not depend on WinForms BrowserUI.' }
    Copy-Item -LiteralPath (Join-Path $repoRoot '.github/avalonia-package-consumer/Consumer.csproj') -Destination $consumer
    Copy-Item -LiteralPath (Join-Path $repoRoot '.github/avalonia-package-consumer/PackageTests.cs') -Destination $consumer
    $config = Join-Path $consumer 'NuGet.Config'
    $feedXml = [Security.SecurityElement]::Escape($feed)
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key='prepared' value='$feedXml'/><add key='nuget.org' value='https://api.nuget.org/v3/index.json'/></packageSources></configuration>", [Text.UTF8Encoding]::new($true))
    $project = Join-Path $consumer 'Consumer.csproj'
    $packageVersion = $avalonia[0].Version
    Invoke-Dotnet @('restore', $project, "-p:DmsPackageVersion=$packageVersion", '--configfile', $config,
        '--packages', (Join-Path $workRoot 'packages'), '--verbosity', 'quiet')
    Invoke-Dotnet @('test', $project, "-p:DmsPackageVersion=$packageVersion", '--no-restore', '--verbosity', 'quiet',
        '--logger', 'trx', '--results-directory', (Join-Path $workRoot 'results'))
    $manifest = [ordered]@{
        PreparedAtUtc = [DateTime]::UtcNow.ToString('o'); Host = [Environment]::MachineName
        SourceCommit = $revision; RequestedVersion = $Version; ConsumerTestPassed = $true
        PublicationPerformed = $false; SourceMustBeRebuiltFromMergedMain = !$bootstrap
        Purpose = $(if ($bootstrap) { 'Explicitly authorized prerelease package-ID bootstrap' } else { 'Release preparation only' })
        Packages = $packages
        SymbolPackages = @(Get-ChildItem -LiteralPath $feed -Filter '*.snupkg' | ForEach-Object {
            [pscustomobject]@{ Path = $_.FullName; Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
        })
    }
    [IO.File]::WriteAllText((Join-Path $workRoot 'preparation.json'), ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($true))
    $commands = @(
        '# PREVIEW ONLY: preparation never executes these publishing commands.',
        '# Stable production releases require merged main, successful post-merge checks and immediate release approval.',
        '# Normal publishing is triggered by creating the GitHub release; see PUBLISHING.md.',
        '# The key must allow every listed ID and creation of the new Avalonia package ID.',
        '# Matching .snupkg files remain beside their .nupkg files for symbol publishing.'
    )
    if ($provider.Count) { $commands += 'dotnet nuget push "{0}" --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY --skip-duplicate' -f $provider[0].Path }
    $commands += 'dotnet nuget push "{0}" --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY --skip-duplicate' -f $avalonia[0].Path
    [IO.File]::WriteAllText((Join-Path $workRoot 'push-preview.txt'), ($commands -join "`r`n") + "`r`n", [Text.UTF8Encoding]::new($true))
    Write-Host "Prepared packages and consumer-test evidence: $workRoot"
    Write-Host 'No NuGet push, API-key lookup, Git tag or GitHub release was performed.'
} finally { Pop-Location }
