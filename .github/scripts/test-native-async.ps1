param(
    [string]$SdkRoot = '.upstream/CenterDevice.IO',
    [string]$OpenScopeRoot = '.upstream/OpenScope',
    [string]$TeamworkRoot = '.upstream/Teamwork',
    [ValidateSet('repository', '1', '2')][string]$TestLevel = 'repository'
)

$ErrorActionPreference = 'Stop'
$TestLevel = & (Join-Path $PSScriptRoot 'read-test-level.ps1') -RequestedLevel $TestLevel
& (Join-Path $PSScriptRoot 'test-test-level-configuration.ps1')
$testFilter = if ($TestLevel -eq '2') { 'TestCategory!=RemoteDms' } else { 'TestCategory!=RemoteDms&TestCategory!=TestLevel2' }
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
        # Only dedicated fake-transport suites and explicitly isolated DMS tests run here.
        & dotnet test $testProject -c CI_CD --framework $framework --filter $testFilter -v minimal @sourceArguments
        if ($LASTEXITCODE -ne 0) { throw "Native isolated tests failed: $testProject ($framework)" }
    }
}
