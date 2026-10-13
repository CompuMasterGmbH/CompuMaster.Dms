param(
    [ValidateSet('1', '2')][string]$TestLevel = '1',
    [Parameter(Mandatory = $true)][ValidateSet('ScopevisioTeamwork', 'OwnCloud', 'WebDav', 'Nextcloud')][string]$ServerCategory,
    [switch]$ReadOnlyDiagnostics,
    [ValidateSet('Full', 'Sample')][string]$Coverage = 'Full'
)

$ErrorActionPreference = 'Stop'
if ($ReadOnlyDiagnostics) {
    if ($Coverage -ne 'Full') { throw 'Read-only diagnostic selection cannot be combined with a normal sample.' }
    $fixture = switch ($ServerCategory) {
        'ScopevisioTeamwork' { 'ScopevisioReadOnlyDiagnostic' }
        'OwnCloud' { 'OwnCloudReadOnlyDiagnostic' }
        default { throw 'Read-only diagnosis supports only Scopevisio and ownCloud.' }
    }
    "TestCategory=RemoteDms&TestCategory=ReadOnlyDiagnostic&TestCategory=$ServerCategory&FullyQualifiedName~CompuMaster.Dms.Test.Providers.$fixture."
} elseif ($Coverage -eq 'Sample') {
    $fixture = switch ($ServerCategory) {
        'ScopevisioTeamwork' { 'ScopevisioTeamworkProviderTest' }
        'OwnCloud' { 'OwnCloudWebDavProviderTest' }
        'WebDav' { 'WebDavProviderTest' }
        'Nextcloud' { 'NextcloudWebDavProviderTest' }
    }
    $methods = @('LoginAtRestApiWebservice', 'ListAllRemoteItems', 'BasicFileRoundTripAndCleanup')
    if ($ServerCategory -eq 'ScopevisioTeamwork') { $methods += 'ExplicitSessionRenewalPreservesIdentityAndSubsequentBrowsing' }
    if ($ServerCategory -in @('OwnCloud', 'Nextcloud')) { $methods += 'CloudOcsPublicLinkRoundTrip' }
    $names = ($methods | ForEach-Object {
        # NUnit's parameterized renewal cases include arguments in their fully qualified names.
        $operator = if ($_ -eq 'ExplicitSessionRenewalPreservesIdentityAndSubsequentBrowsing') { '~' } else { '=' }
        "FullyQualifiedName$operator" + "CompuMaster.Dms.Test.Providers.$fixture.$_"
    }) -join '|'
    "TestCategory=RemoteDms&TestCategory=$ServerCategory&TestCategory!=ReadOnlyDiagnostic&($names)"
} else {
    $levels = if ($TestLevel -eq '2') { '(TestCategory=TestLevel1|TestCategory=TestLevel2)' } else { 'TestCategory=TestLevel1' }
    "TestCategory=RemoteDms&TestCategory=$ServerCategory&TestCategory!=ReadOnlyDiagnostic&$levels"
}
