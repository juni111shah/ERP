# Shared helpers for the ERP Windows deploy scripts. Works in Windows PowerShell 5.1 and PowerShell 7.
# Secrets: the per-environment JWT signing key lives in Windows Credential Manager (generic credential
# "ERP:<env>:SigningKey" of the user running the deploy). It is never written to disk or printed.
Set-StrictMode -Version 2.0

$script:Here = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not ('ErpCredMan' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ErpCredMan {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct CREDENTIAL {
        public int Flags; public int Type; public string TargetName; public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize; public IntPtr CredentialBlob; public int Persist;
        public int AttributeCount; public IntPtr Attributes; public string TargetAlias; public string UserName;
    }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredReadW(string target, int type, int flags, out IntPtr cred);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CredWriteW(ref CREDENTIAL cred, int flags);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr cred);
    public static string Read(string target) {
        IntPtr p;
        if (!CredReadW(target, 1, 0, out p)) return null;
        try {
            var c = (CREDENTIAL)Marshal.PtrToStructure(p, typeof(CREDENTIAL));
            if (c.CredentialBlobSize == 0) return "";
            return Marshal.PtrToStringUni(c.CredentialBlob, c.CredentialBlobSize / 2);
        } finally { CredFree(p); }
    }
    public static void Write(string target, string user, string secret) {
        var bytes = Encoding.Unicode.GetBytes(secret);
        var c = new CREDENTIAL();
        c.Type = 1; c.TargetName = target; c.UserName = user; c.Persist = 2; // CRED_PERSIST_LOCAL_MACHINE
        c.CredentialBlobSize = bytes.Length;
        c.CredentialBlob = Marshal.AllocHGlobal(bytes.Length);
        try {
            Marshal.Copy(bytes, 0, c.CredentialBlob, bytes.Length);
            if (!CredWriteW(ref c, 0)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        } finally { Marshal.FreeHGlobal(c.CredentialBlob); }
    }
}
'@
}

function Get-ErpSettings {
    $s = Import-PowerShellDataFile -Path (Join-Path $script:Here 'environments.psd1')
    if ($env:ERP_ENVS_ROOT) { $s.BaseRoot = $env:ERP_ENVS_ROOT }
    if ($env:ERP_CLOUDFLARED) { $s.Cloudflared = $env:ERP_CLOUDFLARED }
    return $s
}

function Get-ErpEnv {
    param([Parameter(Mandatory)][ValidateSet('staging', 'production')][string]$Environment)
    $s = Get-ErpSettings
    $e = $s.Environments[$Environment]
    $root = Join-Path $s.BaseRoot $Environment
    [pscustomobject]@{
        Name              = $Environment
        Port              = [int]$e.Port
        Database          = $e.Database
        AspNetEnvironment = $e.AspNetEnvironment
        BackupBeforeDeploy = [bool]$e.BackupBeforeDeploy
        Root              = $root
        Releases          = Join-Path $root 'releases'
        Current           = Join-Path $root 'current'
        Storage           = Join-Path $root 'storage'
        Logs              = Join-Path $root 'logs'
        TunnelUrlFile     = Join-Path $root 'tunnel-url.txt'
        TunnelPidFile     = Join-Path $root 'tunnel.pid'
        LocalUrl          = "http://localhost:$($e.Port)"
        Cloudflared       = $s.Cloudflared
        KeepReleases      = [int]$s.KeepReleases
        SecretTarget      = "ERP:${Environment}:SigningKey"
        # Set PublicUrl in environments.psd1 once a named tunnel serves this environment; the quick tunnel is then retired.
        PublicUrl         = $(if ($e.ContainsKey('PublicUrl') -and $e['PublicUrl']) { [string]$e['PublicUrl'] } else { $null })
    }
}

function Initialize-ErpDirs([pscustomobject]$Cfg) {
    foreach ($d in @($Cfg.Root, $Cfg.Releases, $Cfg.Storage, $Cfg.Logs)) {
        if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
    }
}

function Test-ErpSecret([pscustomobject]$Cfg) { return ($null -ne [ErpCredMan]::Read($Cfg.SecretTarget)) }

function New-ErpSecret {
    param([pscustomobject]$Cfg, [switch]$Rotate)
    if ((Test-ErpSecret $Cfg) -and -not $Rotate) { return $false }
    $bytes = New-Object byte[] 64
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes); $rng.Dispose()
    [ErpCredMan]::Write($Cfg.SecretTarget, 'erp-jwt', [Convert]::ToBase64String($bytes))
    return $true
}

# ---- processes ----
function Get-ErpListenerPid([int]$Port) {
    $c = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($c) { return [int]$c.OwningProcess }
    return $null
}

# Starts a process detached from the caller (WMI: parent is WmiPrvSE), so it survives the end of a CI job or
# remote shell, and the Actions runner's orphan-process cleanup does not kill it. Returns the PID.
function Start-ErpDetached {
    param([string]$CommandLine, [string]$WorkingDirectory, [hashtable]$Environment = @{})
    $vars = @{}
    foreach ($item in (Get-ChildItem Env:)) { $vars[$item.Name] = $item.Value }
    foreach ($k in @('RUNNER_TRACKING_ID')) { $vars.Remove($k) }
    foreach ($k in @($vars.Keys)) { if ($k -like 'GITHUB_*' -or $k -like 'ACTIONS_*' -or $k -like 'INPUT_*') { $vars.Remove($k) } }
    foreach ($k in $Environment.Keys) { $vars[$k] = $Environment[$k] }
    $envList = [string[]]@($vars.Keys | Sort-Object | ForEach-Object { "$_=$($vars[$_])" })
    $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ ShowWindow = [uint16]0; EnvironmentVariables = $envList }
    $r = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
        CommandLine = $CommandLine; CurrentDirectory = $WorkingDirectory; ProcessStartupInformation = $startup }
    $envList = $null; $vars = $null
    if ($r.ReturnValue -ne 0) { throw "Win32_Process.Create failed with code $($r.ReturnValue)" }
    return [int]$r.ProcessId
}

function Stop-ErpApp([pscustomobject]$Cfg) {
    $listener = Get-ErpListenerPid $Cfg.Port
    if ($listener) {
        $p = Get-CimInstance Win32_Process -Filter "ProcessId=$listener" -ErrorAction SilentlyContinue
        if ($p -and $p.Name -notmatch '^(ERP\.Api|dotnet)\.exe$') { throw "Port $($Cfg.Port) is used by $($p.Name) (PID $listener), not the ERP API. Refusing to stop it." }
        Stop-Process -Id $listener -Force -ErrorAction SilentlyContinue
    }
    for ($i = 0; $i -lt 30; $i++) { if (-not (Get-ErpListenerPid $Cfg.Port)) { return }; Start-Sleep -Milliseconds 500 }
    throw "Port $($Cfg.Port) still in use after stopping."
}

function Start-ErpApp {
    param([pscustomobject]$Cfg, [switch]$AllowSelfSignup)
    if (-not (Test-Path (Join-Path $Cfg.Current 'ERP.Api.dll'))) { throw "No release deployed at $($Cfg.Current)." }
    $key = [ErpCredMan]::Read($Cfg.SecretTarget)
    if (-not $key) { throw "Signing key $($Cfg.SecretTarget) is missing. Run init-secrets.ps1 -Environment $($Cfg.Name)." }
    Initialize-ErpDirs $Cfg
    $vars = @{
        ASPNETCORE_ENVIRONMENT = $Cfg.AspNetEnvironment
        DOTNET_ENVIRONMENT     = $Cfg.AspNetEnvironment
        ASPNETCORE_URLS        = $Cfg.LocalUrl
        Storage__Root          = $Cfg.Storage
        Auth__SigningKey       = $key
    }
    if ($AllowSelfSignup) { $vars['Onboarding__AllowSelfSignup'] = 'true' }
    $exe = Join-Path $Cfg.Current 'ERP.Api.exe'
    if (Test-Path $exe) { $run = "`"$exe`"" } else { $run = "dotnet `"$(Join-Path $Cfg.Current 'ERP.Api.dll')`"" }
    $log = Join-Path $Cfg.Logs ("app-{0:yyyyMMdd}.log" -f (Get-Date))
    $cmd = "cmd.exe /d /s /c `"$run >> `"$log`" 2>&1`""
    $wrapper = Start-ErpDetached -CommandLine $cmd -WorkingDirectory $Cfg.Current -Environment $vars
    $key = $null; $vars = $null
    return $wrapper
}

function Wait-ErpHealthy {
    param([pscustomobject]$Cfg, [int]$TimeoutSeconds = 180)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $h = Invoke-WebRequest -Uri "$($Cfg.LocalUrl)/health" -UseBasicParsing -TimeoutSec 5
            if ($h.StatusCode -eq 200) {
                $root = Invoke-WebRequest -Uri "$($Cfg.LocalUrl)/" -UseBasicParsing -TimeoutSec 10
                if ($root.StatusCode -eq 200 -and $root.Content -match '<erp-root') { return $true }
                Write-Warning "GET / did not return the Angular app."
                return $false
            }
        } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

# ---- releases ----
function Get-ErpCurrentRelease([pscustomobject]$Cfg) {
    if (-not (Test-Path $Cfg.Current)) { return $null }
    $t = (Get-Item $Cfg.Current).Target
    if ($t -is [array]) { $t = $t[0] }
    return $t
}

function Set-ErpCurrentRelease([pscustomobject]$Cfg, [string]$ReleaseDir) {
    if (Test-Path $Cfg.Current) { cmd.exe /d /c "rmdir `"$($Cfg.Current)`"" | Out-Null }
    cmd.exe /d /c "mklink /J `"$($Cfg.Current)`" `"$ReleaseDir`"" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "mklink /J failed for $ReleaseDir" }
}

function Remove-ErpOldReleases([pscustomobject]$Cfg) {
    $current = Get-ErpCurrentRelease $Cfg
    $all = @(Get-ChildItem -Path $Cfg.Releases -Directory | Sort-Object Name -Descending)
    $all | Select-Object -Skip $Cfg.KeepReleases | Where-Object { $_.FullName -ne $current } |
        ForEach-Object { Remove-Item -Recurse -Force $_.FullName }
}

# ---- database ----
function Backup-ErpDatabase([pscustomobject]$Cfg) {
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $file = "$($Cfg.Database)_$stamp.bak"   # relative name: SQL Server's default backup directory
    $cs = "Server=.;Database=master;Integrated Security=True;TrustServerCertificate=True"
    $conn = New-Object System.Data.SqlClient.SqlConnection $cs
    try {
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandTimeout = 600
        $cmd.CommandText = "IF DB_ID(@db) IS NOT NULL BEGIN DECLARE @sql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(@db) + N' TO DISK = @f WITH COPY_ONLY, INIT'; EXEC sp_executesql @sql, N'@f nvarchar(400)', @f = @file; SELECT 1; END ELSE SELECT 0;"
        [void]$cmd.Parameters.AddWithValue('@db', $Cfg.Database)
        [void]$cmd.Parameters.AddWithValue('@file', $file)
        $done = $cmd.ExecuteScalar()
        if ($done -eq 1) { return $file }
        return $null
    } finally { $conn.Dispose() }
}

# ---- tunnels (Cloudflare quick tunnels: random https://*.trycloudflare.com URL, new URL on every restart) ----
function Get-ErpTunnelProcess([pscustomobject]$Cfg) {
    if (-not (Test-Path $Cfg.TunnelPidFile)) { return $null }
    $tpid = [int](Get-Content $Cfg.TunnelPidFile -Raw).Trim()
    $p = Get-CimInstance Win32_Process -Filter "ProcessId=$tpid" -ErrorAction SilentlyContinue
    if ($p -and $p.Name -eq 'cloudflared.exe' -and $p.CommandLine -match "localhost:$($Cfg.Port)\b") { return $p }
    return $null
}

function Stop-ErpTunnel([pscustomobject]$Cfg) {
    $p = Get-ErpTunnelProcess $Cfg
    if ($p) { Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue }
    Remove-Item $Cfg.TunnelPidFile, $Cfg.TunnelUrlFile -ErrorAction SilentlyContinue
}

function Start-ErpTunnel {
    param([pscustomobject]$Cfg, [switch]$Restart)
    if ($Cfg.PublicUrl) {
        # A named tunnel (run separately as a cloudflared service) serves this environment: no quick tunnel.
        if (Get-ErpTunnelProcess $Cfg) { Stop-ErpTunnel $Cfg }
        return $Cfg.PublicUrl
    }
    if ($Restart) { Stop-ErpTunnel $Cfg }
    $existing = Get-ErpTunnelProcess $Cfg
    if ($existing -and (Test-Path $Cfg.TunnelUrlFile)) { return (Get-Content $Cfg.TunnelUrlFile -Raw).Trim() }
    if ($existing) { Stop-ErpTunnel $Cfg }
    Initialize-ErpDirs $Cfg
    $log = Join-Path $Cfg.Logs ("tunnel-{0:yyyyMMdd-HHmmss}.log" -f (Get-Date))
    $cmd = "`"$($Cfg.Cloudflared)`" tunnel --no-autoupdate --logfile `"$log`" --url $($Cfg.LocalUrl)"
    $tpid = Start-ErpDetached -CommandLine $cmd -WorkingDirectory $Cfg.Root
    Set-Content -Path $Cfg.TunnelPidFile -Value $tpid -Encoding ascii
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 1
        if (Test-Path $log) {
            $m = Select-String -Path $log -Pattern 'https://[a-z0-9-]+\.trycloudflare\.com' | Select-Object -First 1
            if ($m) {
                $url = $m.Matches[0].Value
                Set-Content -Path $Cfg.TunnelUrlFile -Value $url -Encoding ascii
                return $url
            }
        }
    }
    throw "Tunnel for $($Cfg.Name) did not report a URL within 60s; see $log"
}

Export-ModuleMember -Function Get-ErpSettings, Get-ErpEnv, Initialize-ErpDirs, Test-ErpSecret, New-ErpSecret,
    Get-ErpListenerPid, Start-ErpDetached, Stop-ErpApp, Start-ErpApp, Wait-ErpHealthy, Get-ErpCurrentRelease, Set-ErpCurrentRelease,
    Remove-ErpOldReleases, Backup-ErpDatabase, Get-ErpTunnelProcess, Stop-ErpTunnel, Start-ErpTunnel
