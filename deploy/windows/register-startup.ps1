<#
.SYNOPSIS  No-admin auto start at logon: adds a shortcut to the current user's Startup folder that starts the
           staging/production APIs and their quick tunnels (and optionally the Actions runner in C:\actions-runner).
           Quick tunnel URLs change on every start; run .\erp-env.ps1 url to read the new ones.
.EXAMPLE   .\register-startup.ps1 -IncludeRunner
.EXAMPLE   .\register-startup.ps1 -Remove
#>
[CmdletBinding()]
param([switch]$IncludeRunner, [string]$RunnerDir = 'C:\actions-runner', [switch]$Remove)
$ErrorActionPreference = 'Stop'
$startup = [Environment]::GetFolderPath('Startup')
$links = @{ 'ERP environments.lnk' = $true; 'ERP actions runner.lnk' = [bool]$IncludeRunner }
if ($Remove) { foreach ($l in $links.Keys) { Remove-Item (Join-Path $startup $l) -ErrorAction SilentlyContinue }; Write-Host 'Removed.'; return }
$shell = New-Object -ComObject WScript.Shell
$s = $shell.CreateShortcut((Join-Path $startup 'ERP environments.lnk'))
$s.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$s.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$(Join-Path $PSScriptRoot 'erp-env.ps1')`" start -Environment all"
$s.WorkingDirectory = $PSScriptRoot
$s.WindowStyle = 7
$s.Save()
Write-Host "Created $(Join-Path $startup 'ERP environments.lnk')"
if ($IncludeRunner) {
    if (-not (Test-Path (Join-Path $RunnerDir 'run.cmd'))) { throw "$RunnerDir\run.cmd not found (configure the runner first)." }
    $r = $shell.CreateShortcut((Join-Path $startup 'ERP actions runner.lnk'))
    $r.TargetPath = Join-Path $RunnerDir 'run.cmd'
    $r.WorkingDirectory = $RunnerDir
    $r.WindowStyle = 7
    $r.Save()
    Write-Host "Created $(Join-Path $startup 'ERP actions runner.lnk')"
}
