<#
.SYNOPSIS
  Build and publish cmux (Cmux + Cmux.Cli) for Windows.

.DESCRIPTION
  Reproduces the three publish flavors documented in README.md:
    1) Framework-dependent  -> publish/cmux-win-x64       (smallest, needs .NET 10 Desktop Runtime)
    2) Self-contained dir   -> publish/cmux-win-x64-sc    (folder, runs anywhere on win-x64)
    3) CLI                  -> publish/cmux-cli           (self-contained, drop into PATH)

  WPF + ConPTY does NOT play well with PublishSingleFile, so the single-file
  flavor is intentionally omitted. Use the self-contained folder instead.

.PARAMETER Config
  Build configuration. Default: Release.

.PARAMETER Rid
  Target runtime identifier. Default: win-x64.

.PARAMETER Flavor
  Which artifact to produce. Default: All.
    Framework    -> flavor 1
    SelfContained-> flavor 2
    Cli          -> flavor 3
    All          -> 1 + 2 + 3

.PARAMETER OutputRoot
  Output root directory. Default: <repo>\publish.

.EXAMPLE
  pwsh ./scripts/publish.ps1
  pwsh ./scripts/publish.ps1 -Flavor SelfContained
  pwsh ./scripts/publish.ps1 -Flavor Cli -Rid win-arm64
  pwsh ./scripts/publish.ps1 -Config Debug -Flavor Framework
#>

[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Config = 'Release',

    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Rid = 'win-x64',

    [ValidateSet('All', 'Framework', 'SelfContained', 'Cli')]
    [string]$Flavor = 'All',

    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'

# --- locate repo root (script lives in <repo>/scripts) -----------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = Resolve-Path (Join-Path $ScriptDir '..')
if ($OutputRoot) {
    $OutputRoot = (Resolve-Path $OutputRoot).Path
} else {
    $OutputRoot = Join-Path $RepoRoot 'publish'
}

$MainProj = Join-Path $RepoRoot 'src/Cmux/Cmux.csproj'
$CliProj  = Join-Path $RepoRoot 'src/Cmux.Cli/Cmux.Cli.csproj'

function Invoke-DotnetPublish {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string[]]$Args
    )
    Write-Host ""
    Write-Host ">> dotnet publish $Project $($Args -join ' ')" -ForegroundColor Cyan
    & dotnet @('publish', $Project) @Args
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $Project (exit $LASTEXITCODE)"
    }
}

function Ensure-Dir([string]$path) {
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Path $path -Force | Out-Null }
}

# --- preflight: clean WPF temp csproj cache that survives `dotnet clean` ----------
# Without this, a stale Cmux_*_wpftmp.csproj in obj/ can cause XAML code-behind
# fields (ContentArea, PaneCountText, SurfaceTabBarControl, AgentMessagesList...)
# to be reported as missing on a second build.
$cacheDirs = @(
    (Join-Path $RepoRoot 'src/Cmux/obj'),
    (Join-Path $RepoRoot 'src/Cmux/bin'),
    (Join-Path $RepoRoot 'src/Cmux.Core/obj'),
    (Join-Path $RepoRoot 'src/Cmux.Core/bin')
)
foreach ($d in $cacheDirs) {
    if (Test-Path $d) {
        Write-Host ">> cleaning $d"
        Remove-Item -Recurse -Force $d
    }
}

Ensure-Dir $OutputRoot

$stamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
Write-Host "=== cmux publish === Config=$Config Rid=$Rid Flavor=$Flavor Time=$stamp ===" -ForegroundColor Yellow

$ran = @()

if ($Flavor -in @('All', 'Framework')) {
    $out = Join-Path $OutputRoot "cmux-$Rid"
    Invoke-DotnetPublish -Project $MainProj -Args @(
        '-c', $Config,
        '-r', $Rid,
        '--self-contained', 'false',
        '-o', $out
    )
    $ran += "Framework      -> $out\cmuxw.exe"
}

if ($Flavor -in @('All', 'SelfContained')) {
    $out = Join-Path $OutputRoot "cmux-$Rid-sc"
    Invoke-DotnetPublish -Project $MainProj -Args @(
        '-c', $Config,
        '-r', $Rid,
        '--self-contained', 'true',
        '-o', $out
    )
    $ran += "SelfContained  -> $out\cmuxw.exe"
}

if ($Flavor -in @('All', 'Cli')) {
    $out = Join-Path $OutputRoot "cmux-cli"
    Invoke-DotnetPublish -Project $CliProj -Args @(
        '-c', $Config,
        '-r', $Rid,
        '--self-contained', 'true',
        '-o', $out
    )
    $ran += "Cli            -> $out\cmux.exe"
}

Write-Host ""
Write-Host "=== done ===" -ForegroundColor Green
foreach ($r in $ran) { Write-Host "  $r" }