param([ValidateSet('Full', 'Sample')][string]$Coverage = 'Full')
$ErrorActionPreference = 'Stop'
if ($Coverage -eq 'Full') { 'TestCategory!=RemoteDms' } else {
    # Provider-neutral existence/session contracts, classic cloud metadata, and partition safety.
    $fixtures = @('TestPartitioningTest', 'DirectoryExistenceContractTest', 'ScopevisioSessionRenewalTest', 'OcsCapabilityFamilyRegressionTest', 'ServiceRequestPolicyTest', 'CenterDeviceMixedRequestPolicyTest')
    $names = ($fixtures | ForEach-Object { "FullyQualifiedName~CompuMaster.Dms.Test.Providers.$_." }) -join '|'
    "TestCategory!=RemoteDms&($names)"
}
