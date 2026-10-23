<#
.SYNOPSIS  Deploys an ERP package to the staging or production environment on this PC, or rolls back.
.DESCRIPTION
  Used by the CI pipeline (self-hosted runner) and for manual deploys. Steps:
    1. copy the package to <BaseRoot>\<env>\releases\<timestamp>-<sha7>
    2. production: COPY_ONLY backup of the database (SQL Server default backup folder)
    3. stop the running API, point the 'current' junction at the new release, start it
       (Database:AutoMigrate=true applies pending EF migrations on startup)
    4. wait for /health and for GET / to return the Angular app; on failure roll back to the previous release
    5. prune old releases, make sure the quick tunnel is running, print its URL
.EXAMPLE  .\deploy.ps1 -Environment staging -Package D:\ERP\deploy\packages\pkg-local
.EXAMPLE  .\deploy.ps1 -Environment production -Rollback
#>
[CmdletBinding(DefaultParameterSetName = 'Deploy')]
param(
    [Parameter(Mandatory)][ValidateSet('staging', 'production')][string]$Environment,
    [Parameter(Mandatory, ParameterSetName = 'Deploy')][string]$Package,
    [Parameter(Mandatory, ParameterSetName = 'Rollback')][switch]$Rollback,
    [Parameter(ParameterSetName = 'Rollback')][string]$To,
    [switch]$SkipBackup
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ErpOps.psm1') -Force -DisableNameChecking
$cfg = Get-ErpEnv -Environment $Environment
Initialize-ErpDirs $cfg
if (-not (Test-ErpSecret $cfg)) { throw "Signing key for $Environment is missing. Run: .\init-secrets.ps1 -Environment $Environment" }

$previous = Get-ErpCurrentRelease $cfg

if ($Rollback) {
    $releases = @(Get-ChildItem -Path $cfg.Releases -Directory | Sort-Object Name -Descending)
    if ($To) { $target = $releases | Where-Object { $_.Name -eq $To } | Select-Object -First 1 }
    else { $target = $releases | Where-Object { $previous -and $_.FullName -ne $previous -and $_.Name -lt (Split-Path $previous -Leaf) } | Select-Object -First 1 }
    if (-not $target) { throw "No earlier release to roll back to. Releases: $($releases.Name -join ', ')" }
    $release = $target.FullName
    Write-Host "Rolling back $Environment from $(Split-Path -Leaf "$previous") to $($target.Name)"
    Write-Host "Note: database migrations are not reverted. If the newer release changed the schema incompatibly, restore the pre-deploy backup."
} else {
    $pkg = $Package
    $tmp = $null
    if ($pkg -like '*.zip') {
        $tmp = Join-Path $env:TEMP ("erp-pkg-" + [guid]::NewGuid().ToString('N'))
        Expand-Archive -Path $pkg -DestinationPath $tmp
        $pkg = $tmp
    }
    $appDir = Join-Path $pkg 'app'
    if (-not (Test-Path (Join-Path $appDir 'ERP.Api.dll'))) { throw "Package $Package has no app\ERP.Api.dll" }
    if (-not (Test-Path (Join-Path $appDir 'wwwroot\index.html'))) { throw "Package $Package has no app\wwwroot\index.html (Angular build)" }
    $sha = 'unknown'
    $meta = Join-Path $pkg 'release.json'
    if (Test-Path $meta) { $sha = (Get-Content $meta -Raw | ConvertFrom-Json).sha }
    $name = '{0:yyyyMMdd-HHmmss}-{1}' -f (Get-Date), $sha.Substring(0, [Math]::Min(7, $sha.Length))
    $release = Join-Path $cfg.Releases $name
    Write-Host "Copying release $name"
    Copy-Item -Path $appDir -Destination $release -Recurse
    if (Test-Path $meta) { Copy-Item $meta (Join-Path $release 'release.json') }
    if ($tmp) { Remove-Item -Recurse -Force $tmp }

    if ($cfg.BackupBeforeDeploy -and -not $SkipBackup) {
        $bak = Backup-ErpDatabase $cfg
        if ($bak) { Write-Host "Database backup: $bak (SQL Server default backup folder)" } else { Write-Host "Database $($cfg.Database) does not exist yet; it will be created by the migrations." }
    }
}

function Switch-AndStart([string]$Dir) {
    Stop-ErpApp $cfg
    Set-ErpCurrentRelease $cfg $Dir
    [void](Start-ErpApp $cfg)
    return (Wait-ErpHealthy $cfg)
}

Write-Host "Switching $Environment to $(Split-Path -Leaf $release) on $($cfg.LocalUrl)"
if (-not (Switch-AndStart $release)) {
    $log = Get-ChildItem $cfg.Logs -Filter 'app-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    Write-Warning "New release failed its health check. Last log lines ($($log.FullName)):"
    if ($log) { Get-Content $log.FullName -Tail 40 | ForEach-Object { Write-Host "  $_" } }
    if ($previous -and (Test-Path $previous) -and $previous -ne $release) {
        Write-Warning "Rolling back to $(Split-Path -Leaf $previous)"
        if (Switch-AndStart $previous) { Write-Warning "Rolled back; previous release is serving." } else { Write-Warning "Previous release is also unhealthy." }
    }
    throw "Deploy of $Environment failed."
}
Remove-ErpOldReleases $cfg
# Keep a stable copy of these ops scripts next to the environments (CI checkouts move around).
$tools = Join-Path (Split-Path -Parent $cfg.Root) 'tools'
if (-not (Test-Path $tools)) { New-Item -ItemType Directory -Path $tools | Out-Null }
if ((Resolve-Path $PSScriptRoot).Path -ne (Resolve-Path $tools).Path) {
    Get-ChildItem -Path $PSScriptRoot -File | Where-Object { $_.Extension -in '.ps1', '.psm1', '.psd1' } | Copy-Item -Destination $tools -Force
}
$url = Start-ErpTunnel $cfg
Write-Host "Deployed $Environment : $(Split-Path -Leaf $release)"
Write-Host "Local : $($cfg.LocalUrl)"
Write-Host "Public: $url"
if ($env:GITHUB_STEP_SUMMARY) {
    Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value "### $Environment deployed`n- Release: ``$(Split-Path -Leaf $release)```n- Local: $($cfg.LocalUrl)`n- Public (quick tunnel): $url"
}
