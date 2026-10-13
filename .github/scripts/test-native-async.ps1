param(
    [string]$SdkRoot = '.upstream/CenterDevice.IO',
    [string]$OpenScopeRoot = '.upstream/OpenScope',
    [string]$TeamworkRoot = '.upstream/Teamwork',
    [ValidateSet('repository', '1', '2')][string]$TestLevel = 'repository',
    [ValidateSet('Full', 'Sample')][string]$Coverage = 'Full'
)

$ErrorActionPreference = 'Stop'
$TestLevel = & (Join-Path $PSScriptRoot 'read-test-level.ps1') -RequestedLevel $TestLevel
& (Join-Path $PSScriptRoot 'test-test-level-configuration.ps1')

$sdkProject = [IO.Path]::GetFullPath((Join-Path $SdkRoot 'CenterDevice.Rest/CenterDevice.Rest.csproj'))
$openScopeProject = [IO.Path]::GetFullPath((Join-Path $OpenScopeRoot 'src/CompuMaster.Scopevisio.OpenApi/CompuMaster.Scopevisio.OpenApi.csproj'))
$teamworkProject = [IO.Path]::GetFullPath((Join-Path $TeamworkRoot 'Scopevisio.Teamwork/CompuMaster.Scopevisio.Teamwork.csproj'))
foreach ($sourceProject in @($sdkProject, $openScopeProject, $teamworkProject)) {
    if (-not (Test-Path -LiteralPath $sourceProject -PathType Leaf)) {
        throw "Missing pinned native source project: $sourceProject"
    }
}
$sourceArguments = @(
    '-p:EnableNativeAsync=true',
    '-p:GeneratePackageOnBuild=false',
    "-p:CenterDeviceRestProject=$sdkProject",
    "-p:OpenScopeApiProject=$openScopeProject",
    "-p:TeamworkProject=$teamworkProject"
)

& dotnet build 'CompuMaster.Dms.Providers/CompuMaster.Dms.Providers.vbproj' -c CI_CD -v minimal @sourceArguments
if ($LASTEXITCODE -ne 0) { throw 'Native provider source build failed.' }

$frameworks = @('net8.0')
if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { $frameworks += 'net48' }
foreach ($framework in $frameworks) {
    $projects = @(
        (Join-Path $SdkRoot 'CenterDevice.Rest.AsyncTests/CenterDevice.Rest.AsyncTests.csproj'),
        (Join-Path $OpenScopeRoot 'src/CompuMaster.Scopevisio.OpenApi.AsyncTests/CompuMaster.Scopevisio.OpenApi.AsyncTests.csproj'),
        (Join-Path $TeamworkRoot 'Scopevisio.Teamwork.AsyncTests/Scopevisio.Teamwork.AsyncTests.csproj'),
        'CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj'
    )
    foreach ($testProject in $projects) {
        $effectiveCoverage = if ($framework -eq 'net48') { 'Sample' } else { $Coverage }
        $testFilter = & (Join-Path $PSScriptRoot 'get-native-ci-test-filter.ps1') -Coverage $effectiveCoverage -TestProject $testProject
        $resultDirectory = Join-Path 'native-isolated-results' ($framework + '/' + [IO.Path]::GetFileNameWithoutExtension($testProject))
        # Only dedicated fake-transport suites and explicitly isolated DMS tests run here.
        & dotnet test $testProject -c CI_CD --framework $framework --filter $testFilter --logger 'trx;LogFileName=selection.trx' --results-directory $resultDirectory -v minimal @sourceArguments
        if ($LASTEXITCODE -ne 0) { throw "Native isolated tests failed: $testProject ($framework)" }
        & (Join-Path $PSScriptRoot 'assert-dms-test-results.ps1') -Path (Join-Path $resultDirectory 'selection.trx')
    }
}
