param(
    [ValidateSet('1', '2')][string]$TestLevel = '1',
    [Parameter(Mandatory = $true)][ValidateSet('ScopevisioTeamwork', 'OwnCloud', 'WebDav', 'Nextcloud')][string]$ServerCategory,
    [switch]$ReadOnlyDiagnostics
)

$ErrorActionPreference = 'Stop'
if ($ReadOnlyDiagnostics) {
    $fixture = switch ($ServerCategory) {
        'ScopevisioTeamwork' { 'ScopevisioReadOnlyDiagnostic' }
        'OwnCloud' { 'OwnCloudReadOnlyDiagnostic' }
        default { throw 'Read-only diagnosis supports only Scopevisio and ownCloud.' }
    }
    "TestCategory=RemoteDms&TestCategory=ReadOnlyDiagnostic&TestCategory=$ServerCategory&FullyQualifiedName~CompuMaster.Dms.Test.Providers.$fixture."
} else {
    $levels = if ($TestLevel -eq '2') { '(TestCategory=TestLevel1|TestCategory=TestLevel2)' } else { 'TestCategory=TestLevel1' }
    "TestCategory=RemoteDms&TestCategory=$ServerCategory&TestCategory!=ReadOnlyDiagnostic&$levels"
}
