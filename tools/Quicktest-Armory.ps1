<#
.SYNOPSIS
    Launches an isolated RimWorld quicktest for Armory and prints the Armory log.

.DESCRIPTION
    Uses -quicktest and -savedatafolder so the user's real Config/ModsConfig.xml,
    settings, and saves are not touched. The target folder must already contain a
    Config/ModsConfig.xml that enables Armory (and optionally Simple Sidearms).

    Default folder:
      C:\Users\mail\AppData\Local\Temp\opencode\armorytest

    For Simple Sidearms testing, create a second folder (e.g. armorytest-ss) with:
      ludeon.rimworld
      brrainz.harmony
      PeteTimesSix.SimpleSidearms
      kruecke.armory

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\Quicktest-Armory.ps1

.EXAMPLE
    .\tools\Quicktest-Armory.ps1 -Root "$env:TEMP\armorytest-ss" -KeepOpen
#>

param(
    [string]$Root = "C:\Users\mail\AppData\Local\Temp\opencode\armorytest",
    [string]$Exe = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe",
    [int]$TimeoutSeconds = 150,
    [switch]$KeepOpen,
    [switch]$Force,
    [switch]$IncludeSimpleSidearms
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Exe)) {
    throw "RimWorld executable not found: $Exe"
}

$existing = Get-Process RimWorldWin64 -ErrorAction SilentlyContinue
if ($existing -and -not $Force) {
    throw "RimWorld is already running. Close it first, or pass -Force to stop the existing process."
}
if ($existing -and $Force) {
    $existing | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 3
    Get-Process RimWorldWin64 -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

if (-not (Test-Path $Root)) {
    New-Item -ItemType Directory -Force -Path $Root | Out-Null
}

$modsConfig = Join-Path $Root "Config\ModsConfig.xml"
if (-not (Test-Path $modsConfig)) {
    New-Item -ItemType Directory -Force -Path (Split-Path $modsConfig) | Out-Null

    $active = @()
    if ($IncludeSimpleSidearms) {
        $active += "brrainz.harmony"
    }
    $active += "ludeon.rimworld"
    if ($IncludeSimpleSidearms) {
        $active += "PeteTimesSix.SimpleSidearms"
    }
    $active += "kruecke.armory"

    $activeLines = ($active | ForEach-Object { "    <li>$_</li>" }) -join "`r`n"
    $configText = @"
<?xml version="1.0" encoding="utf-8"?>
<ModsConfigData>
  <version>1.6.4871 rev591</version>
  <activeMods>
$activeLines
  </activeMods>
  <knownExpansions>
    <li>ludeon.rimworld.royalty</li>
    <li>ludeon.rimworld.ideology</li>
    <li>ludeon.rimworld.biotech</li>
    <li>ludeon.rimworld.anomaly</li>
    <li>ludeon.rimworld.odyssey</li>
  </knownExpansions>
</ModsConfigData>
"@
    Set-Content -Path $modsConfig -Value $configText -Encoding utf8
    Write-Host "Created isolated ModsConfig: $modsConfig" -ForegroundColor Cyan
}
elseif ($IncludeSimpleSidearms) {
    Write-Warning "ModsConfig already exists; make sure it enables Harmony and Simple Sidearms."
}

$log = Join-Path $env:USERPROFILE "AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log"
if (Test-Path $log) {
    Remove-Item $log -Force
}

Write-Host "Launching quicktest..." -ForegroundColor Cyan
Write-Host "  save data: $Root"
Write-Host "  log:       $log"

$proc = Start-Process -FilePath $Exe `
    -ArgumentList @("-quicktest", "-savedatafolder=`"$Root`"") `
    -PassThru

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$found = $false

while ((Get-Date) -lt $deadline) {
    Start-Sleep -Seconds 2
    if (Test-Path $log) {
        $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($text -match "\[Armory\]") {
            $found = $true
            break
        }
    }
    if ($proc.HasExited) {
        break
    }
}

# Let the UI and planner run for a few seconds after the first Armory line.
Start-Sleep -Seconds 5

Write-Host ""
Write-Host "pid=$($proc.Id) exited=$($proc.HasExited) armoryLog=$found" -ForegroundColor Cyan

if (Test-Path $log) {
    Write-Host ""
    Write-Host "--- Armory lines ---" -ForegroundColor Green
    Select-String -Path $log -Pattern "\[Armory\]" -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Line }

    Write-Host ""
    Write-Host "--- Errors (Armory / GUI / GameComponent) ---" -ForegroundColor Yellow
    Select-String -Path $log `
        -Pattern "Could not instantiate|GUI Error|Exception drawing|NullReference|ArgumentOutOfRange|Armory.*Exception|Armory.*error" `
        -ErrorAction SilentlyContinue |
        Select-Object -Last 25 |
        ForEach-Object { $_.Line }
}
else {
    Write-Warning "Player.log was not created."
}

if ($KeepOpen) {
    Write-Host ""
    Write-Host "Game left running (-KeepOpen)." -ForegroundColor Cyan
    return
}

if (-not $proc.HasExited) {
    $proc.CloseMainWindow() | Out-Null
    Start-Sleep -Seconds 4
    if (-not $proc.HasExited) {
        Stop-Process -Id $proc.Id -Force
    }
}
Write-Host "Closed." -ForegroundColor Cyan

