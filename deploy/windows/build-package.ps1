<#
.SYNOPSIS  Builds a deployable ERP package: the API (framework-dependent, win-x64) with the compiled Angular app in wwwroot.
.EXAMPLE   .\build-package.ps1 -OutDir D:\ERP\deploy\packages\pkg-local
           The CI pipeline produces the same layout on a hosted runner (see .github/workflows/cicd.yml).
#>
[CmdletBinding()]
param(
    [string]$SourceRoot,
    [Parameter(Mandatory)][string]$OutDir,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
if (-not $SourceRoot) { $SourceRoot = (Resolve-Path (Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\..')).Path }
function Invoke-Step([string]$What, [scriptblock]$Do) {
    Write-Host "==> $What"
    $ErrorActionPreference = 'Continue'   # native tools write progress/warnings to stderr; judge by exit code
    & $Do
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)" }
}
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
$app = Join-Path $OutDir 'app'
New-Item -ItemType Directory -Path $app -Force | Out-Null

Push-Location (Join-Path $SourceRoot 'backend')
try {
    Invoke-Step 'dotnet build' { dotnet build ERP.sln -c Release }
    if (-not $SkipTests) { Invoke-Step 'dotnet test' { dotnet test ERP.sln -c Release --no-build } }
    Invoke-Step 'dotnet publish' { dotnet publish src/ERP.Api/ERP.Api.csproj -c Release -r win-x64 --self-contained false -o $app }
} finally { Pop-Location }

Push-Location (Join-Path $SourceRoot 'frontend')
try {
    Invoke-Step 'npm ci' { npm ci --no-audit --no-fund }
    Invoke-Step 'ng build' { npx ng build --configuration production }
    $browser = Join-Path (Get-Location) 'dist\erp-web\browser'
    $www = Join-Path $app 'wwwroot'
    New-Item -ItemType Directory -Path $www -Force | Out-Null
    Copy-Item -Path (Join-Path $browser '*') -Destination $www -Recurse -Force
} finally { Pop-Location }

$sha = (git -C $SourceRoot rev-parse HEAD).Trim()
$dirty = [bool](git -C $SourceRoot status --porcelain)
@{ sha = $sha; dirty = $dirty; builtAt = (Get-Date).ToUniversalTime().ToString('o'); builtBy = 'build-package.ps1' } |
    ConvertTo-Json | Set-Content -Path (Join-Path $OutDir 'release.json') -Encoding utf8
Write-Host "Package ready: $OutDir (sha $sha, dirty=$dirty)"
