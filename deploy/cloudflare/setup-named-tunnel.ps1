<#
.SYNOPSIS
  Sets up the permanent Cloudflare named tunnel "erp" for the ERP deploy PC and switches staging/production off their
  quick (trycloudflare.com) tunnels. Idempotent: safe to re-run. Windows PowerShell 5.1. Supports -WhatIf.
.DESCRIPTION
  Hostnames (defaults, under -Domain):
    erp-dev.<domain>      -> http://localhost:4200  (ng serve; Host header rewritten to localhost)
    erp-dev-api.<domain>  -> http://localhost:5080  (OPTIONAL, only with -DevApiHost)
    erp-staging.<domain>  -> http://localhost:5180
    erp.<domain>          -> http://localhost:5280
    anything else         -> 404
  Run order (docs/devops/cloudflare-named-tunnels.md):
    1. Normal PowerShell : .\setup-named-tunnel.ps1 -Domain example.com                 (login, tunnel, DNS, config, validate)
    2. Elevated PowerShell: .\setup-named-tunnel.ps1 -Domain example.com -InstallService (Windows service; nothing else)
    3. Normal PowerShell : .\setup-named-tunnel.ps1 -Domain example.com                 (again: verifies each hostname and
                           switches staging/production to it; their quick tunnels stop. Dev quick tunnel stays unless
                           -StopDevQuickTunnel.)
  Writes nothing into the repo. Secrets (cert.pem, <tunnel-id>.json) stay in %USERPROFILE%\.cloudflared and, for the
  service, C:\Windows\System32\config\systemprofile\.cloudflared. Nothing secret is printed.
.EXAMPLE
  .\setup-named-tunnel.ps1 -Domain example.com -WhatIf
.EXAMPLE
  .\setup-named-tunnel.ps1 -Domain example.com -DevApiHost erp-dev-api -StopDevQuickTunnel
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^(?=.{3,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$')][string]$Domain,
    # A label (erp-dev) is placed under -Domain; a value containing a dot is used as the full hostname.
    [string]$DevHost = 'erp-dev',
    [string]$StagingHost = 'erp-staging',
    [string]$ProductionHost = 'erp',
    [string]$DevApiHost = '',
    [string]$TunnelName = 'erp',
    [string]$Cloudflared = '',
    [string]$ConfigDir = (Join-Path $env:USERPROFILE '.cloudflared'),
    [switch]$InstallService,
    [switch]$StopDevQuickTunnel,
    [switch]$OverwriteDns,
    [switch]$ForceLogin,
    [switch]$SkipSwitch
)
Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

# ---- ops module (repo: deploy\windows; installed copy: <BaseRoot>\tools) ----
$moduleCandidates = @(
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'windows\ErpOps.psm1'),   # repo checkout
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'ErpOps.psm1')            # <BaseRoot>\tools\cloudflare
)
$module = $moduleCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $module) { throw "ErpOps.psm1 not found next to this script ($($moduleCandidates -join '; '))." }
Import-Module $module -Force -DisableNameChecking
if (-not (Get-Command Get-ErpPublicUrl -ErrorAction SilentlyContinue)) { throw "$module is too old (no Get-ErpPublicUrl). Run this script from an up-to-date checkout." }
$settings = Get-ErpSettings
if (-not $Cloudflared) { $Cloudflared = $settings.Cloudflared }
$publicUrlsFile = Join-Path $settings.BaseRoot 'public-urls.psd1'

function Resolve-Fqdn([string]$Name) {
    if (-not $Name) { return $null }
    $n = $Name.Trim().TrimEnd('.').ToLowerInvariant()
    if ($n.Contains('.')) { return $n }
    return "$n.$Domain"
}
$hosts = [ordered]@{}
$hosts['dev'] = @{ Fqdn = (Resolve-Fqdn $DevHost); Port = 4200; HostHeader = 'localhost' }
if ($DevApiHost) { $hosts['dev-api'] = @{ Fqdn = (Resolve-Fqdn $DevApiHost); Port = 5080; HostHeader = $null } }
$hosts['staging'] = @{ Fqdn = (Resolve-Fqdn $StagingHost); Port = 5180; HostHeader = $null }
$hosts['production'] = @{ Fqdn = (Resolve-Fqdn $ProductionHost); Port = 5280; HostHeader = $null }
$dupes = @($hosts.Values | ForEach-Object { $_.Fqdn } | Group-Object | Where-Object { $_.Count -gt 1 })
if ($dupes.Count) { throw "Hostnames must be different: $($dupes[0].Name)" }

function Invoke-Cf {
    # Runs cloudflared; returns @{ Code; Out } (stdout+stderr as text; cloudflared logs to stderr).
    param([string[]]$Arguments, [switch]$StdoutOnly)
    $prev = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    try {
        if ($StdoutOnly) { $out = & $Cloudflared @Arguments 2>$null }
        else { $out = & $Cloudflared @Arguments 2>&1 | ForEach-Object { "$_" } }
    } finally { $ErrorActionPreference = $prev }
    return @{ Code = $LASTEXITCODE; Out = ($out -join "`n") }
}
function Write-Step([string]$Text) { Write-Host ''; Write-Host "== $Text" -ForegroundColor Cyan }
function Test-Elevated { return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
function Write-Utf8NoBom([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text, (New-Object Text.UTF8Encoding($false))) }

function New-TunnelConfig([string]$TunnelId, [string]$CredentialsFile) {
    $l = New-Object Collections.Generic.List[string]
    $l.Add('# Generated by deploy/cloudflare/setup-named-tunnel.ps1 (template: deploy/cloudflare/config.example.yml). Do not commit.')
    $l.Add("tunnel: $TunnelId")
    $l.Add("credentials-file: '$($CredentialsFile.Replace("'", "''"))'")
    $l.Add('no-autoupdate: true')
    $l.Add('ingress:')
    foreach ($k in $hosts.Keys) {
        $h = $hosts[$k]
        $l.Add("  # $k")
        $l.Add("  - hostname: $($h.Fqdn)")
        $l.Add("    service: http://localhost:$($h.Port)")
        if ($h.HostHeader) { $l.Add('    originRequest:'); $l.Add("      httpHostHeader: $($h.HostHeader)") }
    }
    $l.Add('  - service: http_status:404')
    return (($l -join "`n") + "`n")
}

function Get-TunnelId {
    $r = Invoke-Cf @('tunnel', 'list', '--output', 'json', '--name', $TunnelName) -StdoutOnly
    if ($r.Code -ne 0) { throw "cloudflared tunnel list failed (exit $($r.Code)). Is the login done (cert.pem in $ConfigDir)?" }
    $start = $r.Out.IndexOf('['); if ($start -lt 0) { return $null }
    $parsed = ConvertFrom-Json $r.Out.Substring($start)
    foreach ($t in @($parsed)) { if ($t -and $t.name -eq $TunnelName) { return [string]$t.id } }   # deleted tunnels are not listed
    return $null
}

function Test-PublicHost([string]$Url, [switch]$App) {
    # 200 from /health (API) or / (dev web). Retries for ~60s: DNS and the tunnel connection can lag.
    for ($i = 0; $i -lt 12; $i++) {
        try {
            if ($App) {
                $r = Invoke-WebRequest -Uri "$Url/" -UseBasicParsing -TimeoutSec 10
                if ($r.StatusCode -eq 200) { return $true }
            } else {
                $r = Invoke-WebRequest -Uri "$Url/health" -UseBasicParsing -TimeoutSec 10
                if ($r.StatusCode -eq 200) {
                    $root = Invoke-WebRequest -Uri "$Url/" -UseBasicParsing -TimeoutSec 10
                    if ($root.Content -match '<erp-root') { return $true }
                }
            }
        } catch { }
        Start-Sleep -Seconds 5
    }
    return $false
}

Write-Host 'ERP named tunnel' -ForegroundColor Cyan
foreach ($k in $hosts.Keys) { Write-Host ("  {0,-11} https://{1,-40} -> http://localhost:{2}" -f $k, $hosts[$k].Fqdn, $hosts[$k].Port) }
Write-Host '  (other)     -> 404'

# ---- 1. cloudflared present ----
Write-Step '1/9 cloudflared'
if (-not (Test-Path $Cloudflared)) {
    $c = Get-Command cloudflared.exe -ErrorAction SilentlyContinue
    if ($c) { $Cloudflared = $c.Source } else { throw "cloudflared not found at '$Cloudflared' or on PATH. Pass -Cloudflared <path>." }
}
$v = Invoke-Cf @('--version')
if ($v.Code -ne 0) { throw "cloudflared --version failed: $($v.Out)" }
Write-Host ($v.Out.Split("`n")[0])

if ($InstallService) {
    # ---- service install only (elevated) ----
    Write-Step 'Windows service'
    $cmdLine = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Domain $Domain -InstallService"
    if (-not (Test-Elevated)) { throw "-InstallService needs an elevated PowerShell (Run as administrator). Run:`n  $cmdLine" }
    $userConfig = Join-Path $ConfigDir 'config.yml'
    if (-not (Test-Path $userConfig)) { throw "No $userConfig. First run this script WITHOUT -InstallService in a normal PowerShell." }
    $tunnelLine = Select-String -Path $userConfig -Pattern '^tunnel:\s*(\S+)' | Select-Object -First 1
    $credLine = Select-String -Path $userConfig -Pattern "^credentials-file:\s*'?([^']+?)'?\s*$" | Select-Object -First 1
    if (-not $tunnelLine -or -not $credLine) { throw "$userConfig has no tunnel/credentials-file line; re-run without -InstallService." }
    $tunnelId = $tunnelLine.Matches[0].Groups[1].Value
    $userCred = $credLine.Matches[0].Groups[1].Value
    if (-not (Test-Path $userCred)) { throw "Credentials file $userCred missing; re-run without -InstallService." }
    $sysDir = Join-Path $env:SystemRoot 'System32\config\systemprofile\.cloudflared'
    $sysCred = Join-Path $sysDir "$tunnelId.json"
    $sysConfig = Join-Path $sysDir 'config.yml'
    if ($PSCmdlet.ShouldProcess($sysDir, 'Copy tunnel credentials and write service config.yml')) {
        if (-not (Test-Path $sysDir)) { New-Item -ItemType Directory -Path $sysDir | Out-Null }
        Copy-Item -Path $userCred -Destination $sysCred -Force
        if (Test-Path $sysConfig) { Copy-Item $sysConfig "$sysConfig.bak-$(Get-Date -Format yyyyMMddHHmmss)" }
        Write-Utf8NoBom $sysConfig (New-TunnelConfig -TunnelId $tunnelId -CredentialsFile $sysCred)
        $val = Invoke-Cf @('tunnel', '--config', $sysConfig, 'ingress', 'validate')
        if ($val.Code -ne 0) { throw "Service config invalid:`n$($val.Out)" }
    }
    $svc = Get-Service -Name cloudflared -ErrorAction SilentlyContinue
    if (-not $svc -and $PSCmdlet.ShouldProcess('cloudflared', 'Install Windows service')) {
        $r = Invoke-Cf @('service', 'install')
        if ($r.Code -ne 0) { throw "cloudflared service install failed:`n$($r.Out)" }
        $svc = Get-Service -Name cloudflared
    }
    # Pin the service to this config and tunnel explicitly (independent of cloudflared's default lookup).
    $binPath = "`"$Cloudflared`" tunnel --config `"$sysConfig`" run $TunnelName"
    if ($svc -and $PSCmdlet.ShouldProcess('cloudflared', "Set service command line and (re)start")) {
        Set-ItemProperty -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\cloudflared' -Name ImagePath -Value $binPath
        Set-Service -Name cloudflared -StartupType Automatic
        if ((Get-Service cloudflared).Status -eq 'Running') { Restart-Service cloudflared } else { Start-Service cloudflared }
        Start-Sleep -Seconds 5
        Write-Host "Service cloudflared: $((Get-Service cloudflared).Status)"
    }
    Write-Host ''
    Write-Host 'Service done. Now, in a NORMAL (not elevated) PowerShell, run the same command without -InstallService to verify'
    Write-Host 'the hostnames and switch staging/production to them.'
    return
}

# ---- 2. login ----
Write-Step '2/9 Cloudflare login'
$cert = Join-Path $ConfigDir 'cert.pem'
if ((Test-Path $cert) -and -not $ForceLogin) { Write-Host "Already logged in ($cert exists)." }
elseif ($PSCmdlet.ShouldProcess('Cloudflare account', 'cloudflared tunnel login (opens the browser; pick the zone ' + $Domain + ')')) {
    if ($ForceLogin -and (Test-Path $cert)) { Move-Item $cert "$cert.bak-$(Get-Date -Format yyyyMMddHHmmss)" }
    & $Cloudflared tunnel login
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $cert)) { throw 'cloudflared tunnel login did not complete.' }
}

# ---- 3. tunnel ----
Write-Step "3/9 tunnel '$TunnelName'"
$tunnelId = $null
if (Test-Path $cert) { $tunnelId = Get-TunnelId }
if ($tunnelId) { Write-Host "Exists: $tunnelId" }
elseif ($PSCmdlet.ShouldProcess($TunnelName, 'cloudflared tunnel create')) {
    $r = Invoke-Cf @('tunnel', 'create', $TunnelName)
    if ($r.Code -ne 0) { throw "cloudflared tunnel create failed:`n$($r.Out)" }
    $tunnelId = Get-TunnelId
    if (-not $tunnelId) { throw 'Tunnel created but not listed.' }
    Write-Host "Created: $tunnelId"
}
if (-not $tunnelId) { $tunnelId = '<TUNNEL_ID>' }

# ---- 4. credentials ----
Write-Step '4/9 tunnel credentials'
$credFile = Join-Path $ConfigDir "$tunnelId.json"
if (Test-Path $credFile) { Write-Host "Present: $credFile" }
elseif ($tunnelId -ne '<TUNNEL_ID>' -and $PSCmdlet.ShouldProcess($credFile, 'Recreate tunnel credentials file (cloudflared tunnel token --cred-file)')) {
    # Tunnel exists but its JSON is not on this PC (e.g. created elsewhere): re-download it. Written straight to disk, not printed.
    $r = Invoke-Cf @('tunnel', 'token', '--cred-file', $credFile, $TunnelName)
    if ($r.Code -ne 0 -or -not (Test-Path $credFile)) { throw "Credentials file $credFile missing and could not be recreated (exit $($r.Code))." }
    Write-Host "Recreated: $credFile"
}

# ---- 5. DNS ----
Write-Step '5/9 DNS routes'
foreach ($k in $hosts.Keys) {
    $fqdn = $hosts[$k].Fqdn
    if (-not $PSCmdlet.ShouldProcess($fqdn, "cloudflared tunnel route dns $TunnelName")) { continue }
    $a = @('tunnel', 'route', 'dns'); if ($OverwriteDns) { $a += '--overwrite-dns' }; $a += @($TunnelName, $fqdn)
    $r = Invoke-Cf $a
    if ($r.Code -eq 0) { Write-Host "  $fqdn ok" }
    elseif ($r.Out -match 'already exists') { Write-Warning "  ${fqdn}: a DNS record already exists and points elsewhere. Remove it in the Cloudflare dashboard or re-run with -OverwriteDns." }
    else { throw "route dns $fqdn failed:`n$($r.Out)" }
}

# ---- 6. config.yml (outside the repo) ----
Write-Step '6/9 config.yml'
$configPath = Join-Path $ConfigDir 'config.yml'
$yaml = New-TunnelConfig -TunnelId $tunnelId -CredentialsFile $credFile
if ((Test-Path $configPath) -and ((Get-Content $configPath -Raw) -replace "`r", '') -eq $yaml) { Write-Host "Unchanged: $configPath" }
elseif ($PSCmdlet.ShouldProcess($configPath, 'Write tunnel config')) {
    if (-not (Test-Path $ConfigDir)) { New-Item -ItemType Directory -Path $ConfigDir | Out-Null }
    if (Test-Path $configPath) { Copy-Item $configPath "$configPath.bak-$(Get-Date -Format yyyyMMddHHmmss)" }
    Write-Utf8NoBom $configPath $yaml
    Write-Host "Wrote: $configPath"
}
else { Write-Host $yaml }

# ---- 7. validate ----
Write-Step '7/9 validate ingress'
if (Test-Path $configPath) {
    $r = Invoke-Cf @('tunnel', '--config', $configPath, 'ingress', 'validate')
    if ($r.Code -ne 0) { throw "Ingress invalid:`n$($r.Out)" }
    Write-Host 'Ingress OK'
    foreach ($k in $hosts.Keys) {
        $r = Invoke-Cf @('tunnel', '--config', $configPath, 'ingress', 'rule', "https://$($hosts[$k].Fqdn)")
        Write-Host ("  https://{0} -> {1}" -f $hosts[$k].Fqdn, (($r.Out -split "`n" | Where-Object { $_ -match 'service' }) -join ' ').Trim())
    }
} else { Write-Host '(skipped: no config written)' }

# ---- 8. service ----
Write-Step '8/9 tunnel service'
$svc = Get-Service -Name cloudflared -ErrorAction SilentlyContinue
$installCmd = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Domain $Domain -InstallService"
if ($DevApiHost) { $installCmd += " -DevApiHost $DevApiHost" }
if ($DevHost -ne 'erp-dev') { $installCmd += " -DevHost $DevHost" }
if ($StagingHost -ne 'erp-staging') { $installCmd += " -StagingHost $StagingHost" }
if ($ProductionHost -ne 'erp') { $installCmd += " -ProductionHost $ProductionHost" }
if (-not $svc -or $svc.Status -ne 'Running') {
    Write-Host 'The cloudflared service is not running. In an ELEVATED PowerShell (Run as administrator) run:'
    Write-Host "  $installCmd" -ForegroundColor Yellow
    Write-Host 'then re-run this script (normal PowerShell) to switch staging/production. Or, to try it first without a service:'
    Write-Host "  & `"$Cloudflared`" tunnel --config `"$configPath`" run $TunnelName"
} else {
    Write-Host 'cloudflared service is running. If you changed hostnames since -InstallService, re-run -InstallService (elevated) to refresh its config.'
}

# ---- 9. switch environments ----
Write-Step '9/9 switch to permanent hostnames'
if ($SkipSwitch) { Write-Host '(skipped: -SkipSwitch)'; return }
if (Test-Elevated) { Write-Warning 'Elevated session: skipping the switch so the apps are not restarted as administrator. Re-run in a normal PowerShell.'; return }

$current = @{}
if (Test-Path $publicUrlsFile) { $current = Import-PowerShellDataFile -Path $publicUrlsFile }
foreach ($envName in @('staging', 'production')) {
    $url = "https://$($hosts[$envName].Fqdn)"
    if (-not (Test-PublicHost $url)) { Write-Warning "$envName : $url does not serve the app yet (tunnel/DNS/app not up); left on its quick tunnel."; continue }
    Write-Host "$envName : $url serves the app"
    if ($current.ContainsKey($envName) -and $current[$envName] -eq $url -and -not (Get-ErpTunnelProcess (Get-ErpEnv $envName))) { Write-Host "  already switched"; continue }
    if (-not $PSCmdlet.ShouldProcess($envName, "Set PublicUrl=$url in $publicUrlsFile, restart API, stop its quick tunnel")) { continue }
    $current[$envName] = $url
    $body = ($current.Keys | Sort-Object | ForEach-Object { "    '$_' = '$($current[$_])'" }) -join "`r`n"
    Set-Content -Path $publicUrlsFile -Encoding UTF8 -Value ("# Permanent public URLs of this machine's ERP environments (named tunnel). Written by setup-named-tunnel.ps1.`r`n# Read by ErpOps.psm1 (Get-ErpPublicUrl). Delete an entry to go back to that environment's quick tunnel.`r`n@{`r`n$body`r`n}")
    $cfg = Get-ErpEnv $envName
    # Restart so the API gets ERP_Hosting__PublicUrl (CORS origin + email links), then retire the quick tunnel.
    Stop-ErpApp $cfg
    [void](Start-ErpApp $cfg)
    if (-not (Wait-ErpHealthy $cfg)) { throw "$envName did not become healthy after restart; see $($cfg.Logs)" }
    Write-Host "  public: $(Start-ErpTunnel $cfg)  (quick tunnel stopped)"
}

# Keep <BaseRoot>\tools (used by erp-env.ps1 and startup tasks) in step, so a later `erp-env restart` keeps the named URLs.
$tools = Join-Path $settings.BaseRoot 'tools'
$toolsModule = Join-Path $tools 'ErpOps.psm1'
if ((Test-Path $toolsModule) -and -not (Select-String -Path $toolsModule -Pattern 'Get-ErpPublicUrl' -Quiet)) {
    $src = Split-Path -Parent $module
    if ($src -ne $tools -and $PSCmdlet.ShouldProcess($tools, "Update ops scripts from $src (older copy ignores public-urls.psd1)")) {
        Get-ChildItem -Path $src -File | Where-Object { $_.Extension -in '.ps1', '.psm1', '.psd1' } | Copy-Item -Destination $tools -Force
    }
}

$devUrl = "https://$($hosts['dev'].Fqdn)"
if (Test-PublicHost $devUrl -App) {
    Write-Host "dev : $devUrl serves ng serve"
    $devQuick = @(Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" | Where-Object { $_.CommandLine -match '--url\s+"?http://localhost:4200\b' })
    if ($StopDevQuickTunnel) {
        foreach ($p in $devQuick) { if ($PSCmdlet.ShouldProcess("PID $($p.ProcessId)", 'Stop dev quick tunnel')) { Stop-Process -Id $p.ProcessId -Force; Write-Host "  dev quick tunnel PID $($p.ProcessId) stopped" } }
    } elseif ($devQuick.Count) {
        Write-Host "  dev quick tunnel still running (PID $(($devQuick | ForEach-Object { $_.ProcessId }) -join ', ')). Tell SQA the new URL, then re-run with -StopDevQuickTunnel."
    }
} else { Write-Warning "dev : $devUrl not answering (is ng serve running on 4200?). Dev quick tunnel left alone." }

Write-Host ''
Write-Host 'Done. Check: D:\ERP\envs\tools\erp-env.ps1 status' -ForegroundColor Green
