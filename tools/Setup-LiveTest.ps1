<#
.SYNOPSIS
    Prepares an isolated RimWorld save-data folder for a live Armory test.

.DESCRIPTION
    Copies the user's Config folder and one save into a throwaway save-data folder,
    enables Armory debug logging, disables automatic equipping, sets DevMode and a
    windowed resolution, and writes the armory-live.txt marker consumed by
    tools/LiveTestScaffold.cs.txt.

    The user's real Config and save are never modified. The game is only launched
    with -Launch, and is never force-closed without -Force.

.EXAMPLE
    .\tools\Setup-LiveTest.ps1 -SaveName "Armory Testing"

.EXAMPLE
    .\tools\Setup-LiveTest.ps1 -SaveName "Armory Testing" -Build -Launch -Wait

.NOTES
    Before -Build, copy tools\LiveTestScaffold.cs.txt to
    Source\Armory\LiveTestScaffold.cs. Delete that file and rebuild after testing.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$SaveName,

    [string]$SourceRoot = "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios",
    [string]$Root = "$env:TEMP\opencode\armory-live",
    [string]$Exe = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64.exe",
    [int]$Width = 1600,
    [int]$Height = 900,
    [int]$TimeoutSeconds = 300,

    [switch]$Build,
    [switch]$Launch,
    [switch]$Wait,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$saveName = [System.IO.Path]::GetFileNameWithoutExtension($SaveName)
$sourceSave = Join-Path (Join-Path $SourceRoot "Saves") ($saveName + ".rws")

if (-not (Test-Path $sourceSave))
{
    $available = Get-ChildItem (Join-Path $SourceRoot "Saves") -Filter "*.rws" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 10 |
        ForEach-Object { "  " + $_.BaseName }
    throw "Save not found: $sourceSave`nAvailable saves:`n$($available -join "`n")"
}

$existing = Get-Process RimWorldWin64 -ErrorAction SilentlyContinue
if ($existing -and -not $Force)
{
    throw "RimWorld is already running. Close it first, or pass -Force to stop it (only with explicit permission)."
}
if ($existing -and $Force)
{
    $existing | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 3
    Get-Process RimWorldWin64 -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

if (Test-Path $Root)
{
    Remove-Item $Root -Recurse -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $Root "Config"), (Join-Path $Root "Saves") | Out-Null
Copy-Item (Join-Path $SourceRoot "Config\*") (Join-Path $Root "Config") -Recurse -Force
Copy-Item $sourceSave (Join-Path $Root "Saves") -Force

function Set-XmlValue
{
    param($Document, $Parent, [string]$Name, [string]$Value)
    $node = $Parent.SelectSingleNode($Name)
    if ($null -eq $node)
    {
        $node = $Document.CreateElement($Name)
        [void]$Parent.AppendChild($node)
    }
    $node.InnerText = $Value
}

$prefsPath = Join-Path $Root "Config\Prefs.xml"
if (Test-Path $prefsPath)
{
    $doc = New-Object System.Xml.XmlDocument
    $doc.PreserveWhitespace = $true
    $doc.Load($prefsPath)
    $prefs = $doc.SelectSingleNode("PrefsData")
    if (-not $prefs)
    {
        $prefs = $doc.DocumentElement
    }
    if ($prefs)
    {
        Set-XmlValue $doc $prefs "devMode" "True"
        Set-XmlValue $doc $prefs "fullscreen" "False"
        Set-XmlValue $doc $prefs "screenWidth" "$Width"
        Set-XmlValue $doc $prefs "screenHeight" "$Height"
    }
    $doc.Save($prefsPath)
}

$armoryPath = Join-Path $Root "Config\Mod_Armory_ArmoryMod.xml"
if (-not (Test-Path $armoryPath))
{
    @'
<?xml version="1.0" encoding="utf-8"?>
<SettingsBlock>
  <ModSettings Class="Armory.ArmorySettings" />
</SettingsBlock>
'@ | Set-Content $armoryPath -Encoding UTF8
}

$armoryDoc = New-Object System.Xml.XmlDocument
$armoryDoc.PreserveWhitespace = $true
$armoryDoc.Load($armoryPath)
$armorySettings = $armoryDoc.SelectSingleNode("SettingsBlock/ModSettings")
if (-not $armorySettings)
{
    throw "Unexpected Armory settings file: $armoryPath"
}
Set-XmlValue $armoryDoc $armorySettings "autoEquipEnabled" "False"
Set-XmlValue $armoryDoc $armorySettings "manageSidearms" "False"
Set-XmlValue $armoryDoc $armorySettings "debugLogging" "True"
$armoryDoc.Save($armoryPath)

Set-Content (Join-Path $Root "armory-live.txt") $saveName -Encoding UTF8

$scaffold = Join-Path $repo "Source\Armory\LiveTestScaffold.cs"
$hasScaffold = Test-Path $scaffold

Write-Host "Prepared isolated test folder:" -ForegroundColor Cyan
Write-Host "  save:   $sourceSave"
Write-Host "  root:   $Root"
Write-Host "  marker: $(Join-Path $Root 'armory-live.txt') = $saveName"
Write-Host "  config: copied; DevMode on, windowed ${Width}x${Height}, Armory automation off, debug logging on"

if (-not $hasScaffold)
{
    Write-Warning "Temporary scaffold missing: $scaffold"
    Write-Host "Copy tools\LiveTestScaffold.cs.txt there, build, then run with -Launch." -ForegroundColor Yellow
}
else
{
    $dllPath = Join-Path $repo "Assemblies\Armory.dll"
    if (Test-Path $dllPath)
    {
        $sourceTime = (Get-Item $scaffold).LastWriteTime
        $dllTime = (Get-Item $dllPath).LastWriteTime
        if ($sourceTime -gt $dllTime)
        {
            if ($Launch -and -not $Build)
            {
                throw "Assemblies\Armory.dll is older than the scaffold. Build before launching."
            }
            Write-Warning "Assemblies\Armory.dll is older than the scaffold; the harness will not run until you rebuild."
        }
    }
}

if ($Build)
{
    if (-not $hasScaffold)
    {
        throw "Cannot build the live-test DLL without Source\Armory\LiveTestScaffold.cs."
    }
    Write-Host "Building Armory..." -ForegroundColor Cyan
    & dotnet build (Join-Path $repo "Source\Armory\Armory.csproj") -v m
    if ($LASTEXITCODE -ne 0)
    {
        throw "Armory build failed."
    }
}

$log = Join-Path $env:USERPROFILE "AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log"
$shot = Join-Path $Root "armory-live.png"

if (-not $Launch)
{
    Write-Host ""
    Write-Host "Next:" -ForegroundColor Cyan
    Write-Host "  1. Copy tools\LiveTestScaffold.cs.txt to Source\Armory\LiveTestScaffold.cs"
    Write-Host "  2. dotnet build Source\Armory\Armory.csproj"
    Write-Host "  3. .\tools\Setup-LiveTest.ps1 -SaveName `"$saveName`" -Launch -Wait"
    Write-Host "  4. Delete Source\Armory\LiveTestScaffold.cs and rebuild before committing"
    return
}

if (-not $hasScaffold)
{
    throw "Cannot launch without Source\Armory\LiveTestScaffold.cs."
}

if (-not (Test-Path $Exe))
{
    throw "RimWorld executable not found: $Exe"
}

if (Test-Path $log)
{
    Remove-Item $log -Force
}

Write-Host "Launching isolated quicktest..." -ForegroundColor Cyan
$proc = Start-Process -FilePath $Exe `
    -ArgumentList @("-quicktest", "-savedatafolder=`"$Root`"") `
    -PassThru

Write-Host "  pid: $($proc.Id)"

if (-not $Wait)
{
    Write-Host "  log: $log"
    Write-Host "  png: $shot"
    return
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$finished = $false
while ((Get-Date) -lt $deadline)
{
    Start-Sleep -Seconds 3
    if (Test-Path $log)
    {
        $text = Get-Content $log -Raw -ErrorAction SilentlyContinue
        if ($text -match "live test: quitting")
        {
            $finished = $true
            break
        }
    }
    if ($proc.HasExited)
    {
        break
    }
}

Write-Host ""
Write-Host "finished=$finished exited=$($proc.HasExited)" -ForegroundColor Cyan

if (Test-Path $log)
{
    Write-Host ""
    Write-Host "--- Armory lines ---" -ForegroundColor Green
    Select-String -Path $log -Pattern "\[Armory\]" -Encoding UTF8 |
        ForEach-Object { $_.Line }

    Write-Host ""
    Write-Host "--- Armory errors ---" -ForegroundColor Yellow
    Select-String -Path $log -Pattern "\[Armory\].*(Exception|Error|failed|missing)" -Encoding UTF8 |
        Select-Object -Last 20 |
        ForEach-Object { $_.Line }
}

if (Test-Path $shot)
{
    Write-Host ""
    Write-Host "Screenshot: $shot" -ForegroundColor Green
}

if (-not $proc.HasExited)
{
    Write-Host ""
    Write-Warning "The isolated game is still running (pid $($proc.Id)). The harness quits itself after the screenshot; if it did not, close that instance manually."
}
