param(
    [ValidateSet('Full', 'Sample')][string]$Coverage = 'Full',
    [Parameter(Mandatory = $true)][string]$TestProject
)
$ErrorActionPreference = 'Stop'
if ($Coverage -eq 'Full') { return 'TestCategory!=RemoteDms' }
$names = switch ([IO.Path]::GetFileName($TestProject)) {
    'CenterDevice.Rest.AsyncTests.csproj' { @('CancellationReachesAnActiveHttpRequestAndReleasesItsAllowance', 'MutationsAreNeverReplayedAfterAnAmbiguousServerError') }
    'CompuMaster.Scopevisio.OpenApi.AsyncTests.csproj' { @('CancellationReachesActiveTokenAndAccountRequests') }
    'Scopevisio.Teamwork.AsyncTests.csproj' { @('AuthorizationFactoryUsesTheSharedTransportAndInstallsTheFreshIdentity', 'CancellationReachesTheActiveAccountRequest') }
    'CompuMaster.Dms.Test.Providers.vbproj' { return & (Join-Path $PSScriptRoot 'get-isolated-dms-test-filter.ps1') -Coverage Sample }
    default { throw 'Unrecognized native sample test project.' }
}
$selected = ($names | ForEach-Object { "FullyQualifiedName~.$_" }) -join '|'
"TestCategory!=RemoteDms&($selected)"
