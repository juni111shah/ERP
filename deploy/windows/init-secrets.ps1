<#
.SYNOPSIS  Creates the per-environment JWT signing key (64 random bytes) in Windows Credential Manager of the current user.
           Never prints it. Run as the account that runs the deploys / the Actions runner.
.EXAMPLE   .\init-secrets.ps1                       # both environments, only if missing
.EXAMPLE   .\init-secrets.ps1 -Environment production -Rotate   # new key: signs everyone out; restart the app afterwards
#>
[CmdletBinding()]
param([ValidateSet('staging', 'production', 'all')][string]$Environment = 'all', [switch]$Rotate)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ErpOps.psm1') -Force -DisableNameChecking
if ($Environment -eq 'all') { $names = @('staging', 'production') } else { $names = @($Environment) }
foreach ($n in $names) {
    $cfg = Get-ErpEnv -Environment $n
    if (New-ErpSecret $cfg -Rotate:$Rotate) { Write-Host "$n : signing key created in Credential Manager ($($cfg.SecretTarget))" }
    else { Write-Host "$n : signing key already present ($($cfg.SecretTarget)); use -Rotate to replace it" }
}
