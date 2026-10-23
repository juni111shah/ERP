<#
.SYNOPSIS  Operate the staging/production environments on this PC.
.EXAMPLE   .\erp-env.ps1 status
.EXAMPLE   .\erp-env.ps1 restart -Environment production
.EXAMPLE   .\erp-env.ps1 tunnel-restart -Environment staging     # new trycloudflare URL
.EXAMPLE   .\erp-env.ps1 start -Environment all                   # after a reboot: apps + tunnels
.EXAMPLE   .\erp-env.ps1 restart -Environment production -AllowSelfSignup   # one-off: create the first workspace, then restart without it
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)][ValidateSet('status', 'start', 'stop', 'restart', 'url', 'tunnel-restart', 'tunnel-stop')][string]$Action,
    [ValidateSet('staging', 'production', 'all')][string]$Environment = 'all',
    [switch]$AllowSelfSignup
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ErpOps.psm1') -Force -DisableNameChecking
if ($Environment -eq 'all') { $names = @('staging', 'production') } else { $names = @($Environment) }
if ($AllowSelfSignup -and $Environment -eq 'all') { throw '-AllowSelfSignup needs a single -Environment.' }

foreach ($n in $names) {
    $cfg = Get-ErpEnv -Environment $n
    switch ($Action) {
        'status' {
            $apiPid = Get-ErpListenerPid $cfg.Port
            $tun = Get-ErpTunnelProcess $cfg
            $url = $null; if (Test-Path $cfg.TunnelUrlFile) { $url = (Get-Content $cfg.TunnelUrlFile -Raw).Trim() }
            $rel = Get-ErpCurrentRelease $cfg; if ($rel) { $rel = Split-Path -Leaf $rel }
            [pscustomobject]@{
                Environment = $n; Local = $cfg.LocalUrl; ApiPid = $apiPid; Release = $rel; Database = $cfg.Database
                TunnelPid = $(if ($tun) { $tun.ProcessId } else { $null }); PublicUrl = $(if ($cfg.PublicUrl) { $cfg.PublicUrl } elseif ($tun) { $url } else { $null })
            }
        }
        'start' {
            if (-not (Get-ErpListenerPid $cfg.Port)) {
                [void](Start-ErpApp $cfg -AllowSelfSignup:$AllowSelfSignup)
                if (-not (Wait-ErpHealthy $cfg)) { throw "$n did not become healthy; see $($cfg.Logs)" }
            }
            Write-Host "$n API: $($cfg.LocalUrl)"
            Write-Host "$n public: $(Start-ErpTunnel $cfg)"
        }
        'stop' { Stop-ErpApp $cfg; Stop-ErpTunnel $cfg; Write-Host "$n stopped (API and tunnel)" }
        'restart' {
            Stop-ErpApp $cfg
            [void](Start-ErpApp $cfg -AllowSelfSignup:$AllowSelfSignup)
            if (-not (Wait-ErpHealthy $cfg)) { throw "$n did not become healthy; see $($cfg.Logs)" }
            if ($AllowSelfSignup) { Write-Warning "$n is running WITH self-signup enabled. Create the first workspace, then run: .\erp-env.ps1 restart -Environment $n" }
            Write-Host "$n public: $(Start-ErpTunnel $cfg)"
        }
        'url' { if ($cfg.PublicUrl) { "${n}: $($cfg.PublicUrl) (named tunnel)" } elseif ((Get-ErpTunnelProcess $cfg) -and (Test-Path $cfg.TunnelUrlFile)) { "${n}: " + (Get-Content $cfg.TunnelUrlFile -Raw).Trim() } else { "${n}: no tunnel running" } }
        'tunnel-restart' { Write-Host "$n public: $(Start-ErpTunnel $cfg -Restart)" }
        'tunnel-stop' { Stop-ErpTunnel $cfg; Write-Host "$n tunnel stopped" }
    }
}
