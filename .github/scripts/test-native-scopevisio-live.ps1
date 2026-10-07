param(
    [string]$SdkRoot = '.upstream/CenterDevice.IO',
    [string]$OpenScopeRoot = '.upstream/OpenScope',
    [string]$TeamworkRoot = '.upstream/Teamwork',
    [ValidateRange(256, 4096)][int]$TransferMiB = 256,
    [Parameter(Mandatory = $true)][string]$ExclusiveWindowUntil,
    [string]$Mode = 'verification'
)

$ErrorActionPreference = 'Stop'
if ($env:CI -ne 'true' -or $env:NATIVE_SCOPEVISIO_EXCLUSIVE_CI -ne 'true') {
    throw 'Native live verification requires the serialized CI job and a coordinated cross-repository exclusive window.'
}
if ($ExclusiveWindowUntil -notmatch '(Z|[+-]\d{2}:\d{2})$') {
    throw 'The exclusive-window end must include an explicit UTC offset or Z.'
}
$windowEnd = [DateTimeOffset]::Parse($ExclusiveWindowUntil, [Globalization.CultureInfo]::InvariantCulture)
# Validate after this job acquired the account lock, not when the workflow was queued.
# The job timeout bounds setup, execution, and cleanup within the confirmed window.
if ($windowEnd -lt [DateTimeOffset]::UtcNow.AddMinutes(185)) {
    throw 'The confirmed exclusive window must cover at least another 185 minutes when the locked job starts. No server request was started.'
}
$env:NATIVE_SCOPEVISIO_TRANSFER_MIB = $TransferMiB.ToString([Globalization.CultureInfo]::InvariantCulture)
if ([string]::IsNullOrWhiteSpace($Mode)) { $Mode = 'verification' }
if ($Mode -notin @('verification', 'upload_boundary')) { throw 'Unsupported native live mode.' }
$env:NATIVE_SCOPEVISIO_EVIDENCE_DIRECTORY = [IO.Path]::GetFullPath('native-live-results')
[IO.Directory]::CreateDirectory($env:NATIVE_SCOPEVISIO_EVIDENCE_DIRECTORY) | Out-Null
$testFilter = if ($Mode -eq 'upload_boundary') { 'TestCategory=ScopevisioNativeUploadBoundary' } else { 'TestCategory=ScopevisioNativeAsync' }
if ($Mode -eq 'upload_boundary') { Write-Host 'Windows-only upload-boundary diagnostic; successful diagnostics do not satisfy large-transfer/cancellation acceptance.' }
$arguments = @(
    '-p:EnableNativeAsync=true',
    '-p:GeneratePackageOnBuild=false',
    "-p:CenterDeviceRestProject=$([IO.Path]::GetFullPath((Join-Path $SdkRoot 'CenterDevice.Rest/CenterDevice.Rest.csproj')))",
    "-p:OpenScopeApiProject=$([IO.Path]::GetFullPath((Join-Path $OpenScopeRoot 'src/CompuMaster.Scopevisio.OpenApi/CompuMaster.Scopevisio.OpenApi.csproj')))",
    "-p:TeamworkProject=$([IO.Path]::GetFullPath((Join-Path $TeamworkRoot 'Scopevisio.Teamwork/CompuMaster.Scopevisio.Teamwork.csproj')))"
)
& dotnet test 'CompuMaster.Dms.Test.Providers/CompuMaster.Dms.Test.Providers.vbproj' -c CI_CD --framework net8.0 --filter $testFilter --logger junit --results-directory native-live-results -v minimal @arguments
if ($LASTEXITCODE -ne 0) { throw 'Native source-mode Scopevisio live tests failed; inspect test output and cleanup diagnostics.' }
