# Builds a clean Workshop release copy and swaps it into the game's Mods folder.
#
# RimWorld's in-game uploader sends Steam the entire mod root folder. Uploading
# the development workspace would therefore include .git, Source, docs, tools,
# obj and test files. This script stages only the runtime files, then optionally
# replaces the development junction at <RimWorld>/Mods/Armory with that clean
# copy so the in-game uploader sees only release content.
#
#   -Prepare   Build, stage, and put the clean copy in the game's Mods folder.
#   -Restore   Remove the release copy and recreate the development junction.
#
# Close RimWorld before running either switch. The script never pushes to git.
#
# After the first upload, keep About/PublishedFileId.txt with the release copy.
# For future releases, put the numeric ID in docs/workshop-id.txt and the script
# will seed it into the staged About folder automatically.

param(
    [switch]$Prepare,
    [switch]$Restore,
    [string]$Workspace = (Split-Path -Parent $PSScriptRoot),
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld",
    [string]$ReleaseRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "ArmoryRelease"),
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

if ($Prepare -eq $Restore)
{
    throw "Pass exactly one of -Prepare or -Restore."
}

$modsDir = Join-Path $GameDir "Mods"
$gameModDir = Join-Path $modsDir "Armory"
$stageDir = Join-Path $ReleaseRoot "Armory"
$markerPath = Join-Path $modsDir ".armory-release"
$aboutPath = Join-Path $Workspace "About\About.xml"
$projectPath = Join-Path $Workspace "Source\Armory\Armory.csproj"
$dllPath = Join-Path $Workspace "Assemblies\Armory.dll"
$idBackupPath = Join-Path $Workspace "docs\workshop-id.txt"

if (-not (Test-Path $aboutPath))
{
    throw "Missing About.xml: $aboutPath"
}
if (-not (Test-Path $projectPath))
{
    throw "Missing project: $projectPath"
}
if (-not (Test-Path $GameDir))
{
    throw "RimWorld folder not found: $GameDir"
}
if (Get-Process RimWorldWin64 -ErrorAction SilentlyContinue)
{
    throw "RimWorld is running. Close it before preparing or restoring the release copy."
}

[xml]$about = Get-Content $aboutPath -Raw
$version = $about.ModMetaData.modVersion
$packageId = $about.ModMetaData.packageId
if ($packageId -ne "kruecke.armory")
{
    throw "Unexpected packageId '$packageId'. Refusing to stage."
}

function Test-Junction([string]$path)
{
    if (-not (Test-Path $path))
    {
        return $false
    }
    $item = Get-Item $path -Force
    return (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
}

function Read-WorkshopId
{
    $candidates = @(
        $idBackupPath,
        (Join-Path $gameModDir "About\PublishedFileId.txt"),
        (Join-Path $stageDir "About\PublishedFileId.txt"),
        (Join-Path $Workspace "About\PublishedFileId.txt")
    )
    foreach ($candidate in $candidates)
    {
        if (-not (Test-Path $candidate))
        {
            continue
        }
        $value = (Get-Content $candidate -Raw).Trim()
        if ($value -match '^\d+$')
        {
            return $value
        }
    }
    return $null
}

if ($Prepare)
{
    Write-Host "Building $version ($Configuration)..." -ForegroundColor Cyan
    & dotnet build $projectPath -c $Configuration -v m
    if ($LASTEXITCODE -ne 0)
    {
        throw "Build failed."
    }
    if (-not (Test-Path $dllPath))
    {
        throw "Build output missing: $dllPath"
    }

    $workshopId = Read-WorkshopId
    if ($workshopId)
    {
        Write-Host "Workshop ID: $workshopId" -ForegroundColor Cyan
    }
    else
    {
        Write-Host "Workshop ID: none yet (the first upload will create one)" -ForegroundColor Yellow
    }

    Write-Host "Staging clean release copy..." -ForegroundColor Cyan
    if (Test-Path $stageDir)
    {
        Remove-Item $stageDir -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

    Copy-Item (Join-Path $Workspace "About") $stageDir -Recurse
    Copy-Item (Join-Path $Workspace "Defs") $stageDir -Recurse
    Copy-Item (Join-Path $Workspace "Textures") $stageDir -Recurse
    New-Item -ItemType Directory -Force -Path (Join-Path $stageDir "Assemblies") | Out-Null
    Copy-Item $dllPath (Join-Path $stageDir "Assemblies\Armory.dll")

    $stageAbout = Join-Path $stageDir "About"
    Remove-Item (Join-Path $stageAbout "PublishedFileId.txt") -Force -ErrorAction SilentlyContinue
    if ($workshopId)
    {
        Set-Content (Join-Path $stageAbout "PublishedFileId.txt") $workshopId -NoNewline -Encoding ASCII
    }

    $required = @(
        "About\About.xml",
        "About\Preview.png",
        "About\ModIcon.png",
        "Assemblies\Armory.dll",
        "Defs\MainButtonDefs.xml",
        "Textures\UI\Buttons\MainButtons\IconArmory.png",
        "Textures\UI\Icons\ArmoryBestStar.png"
    )
    foreach ($relative in $required)
    {
        if (-not (Test-Path (Join-Path $stageDir $relative)))
        {
            throw "Release copy is missing: $relative"
        }
    }

    $forbidden = @(".git", ".gitignore", "Source", "docs", "tools", "obj", "bin", "README.md")
    foreach ($name in $forbidden)
    {
        if (Test-Path (Join-Path $stageDir $name))
        {
            throw "Release copy unexpectedly contains: $name"
        }
    }

    Write-Host "Preparing game Mods folder..." -ForegroundColor Cyan
    if (Test-Path $gameModDir)
    {
        if (Test-Junction $gameModDir)
        {
            $targets = @((Get-Item $gameModDir -Force).Target | ForEach-Object { $_.TrimEnd("\") })
            if ($targets -notcontains $Workspace.TrimEnd("\"))
            {
                throw "Mods\Armory is a junction to an unexpected target: $($targets -join ", ")"
            }
            # rmdir removes a junction link only; it never deletes the target.
            & cmd /c rmdir "`"$gameModDir`""
            if ($LASTEXITCODE -ne 0)
            {
                throw "Could not remove the development junction."
            }
            Write-Host "Removed development junction (workspace untouched)." -ForegroundColor Cyan
        }
        elseif (Test-Path $markerPath)
        {
            $markerTarget = (Get-Content $markerPath -Raw).Trim()
            if ($markerTarget -ne $stageDir)
            {
                throw "Refusing to replace $gameModDir; the release marker points to $markerTarget"
            }
            Remove-Item $gameModDir -Recurse -Force
        }
        else
        {
            throw "Mods\Armory is a real folder that was not created by this script. Move it aside manually."
        }
    }

    New-Item -ItemType Directory -Force -Path $gameModDir | Out-Null
    Get-ChildItem $stageDir -Force | ForEach-Object {
        Copy-Item $_.FullName $gameModDir -Recurse -Force
    }
    Set-Content $markerPath $stageDir -Encoding UTF8

    Write-Host ""
    Write-Host "Release copy ready:" -ForegroundColor Green
    Write-Host "  version:    $version"
    Write-Host "  staged at:  $stageDir"
    Write-Host "  game copy:  $gameModDir"
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Green
    Write-Host "  1. Launch RimWorld through Steam."
    Write-Host "  2. Enable Dev Mode in Options."
    Write-Host "  3. Options > Mods > Armory > More actions > Upload to Steam Workshop."
    Write-Host "  4. After the upload, copy About\PublishedFileId.txt out of the game copy."
    Write-Host "  5. Run this script with -Restore to bring the development junction back."
    return
}

if ($Restore)
{
    if (-not (Test-Path $markerPath))
    {
        throw "No release marker found. The development junction may already be restored."
    }
    if (-not (Test-Path $gameModDir))
    {
        Remove-Item $markerPath -Force
        & cmd /c mklink /J "`"$gameModDir`"" "`"$Workspace`"" | Out-Null
        if ($LASTEXITCODE -ne 0)
        {
            throw "Release copy was already gone, and the development junction could not be recreated."
        }
        Write-Host "Release copy was already gone; development junction restored." -ForegroundColor Yellow
        return
    }
    if (Test-Junction $gameModDir)
    {
        Remove-Item $markerPath -Force
        Write-Host "Mods\Armory is already a junction; marker removed." -ForegroundColor Yellow
        return
    }

    $uploadedId = Join-Path $gameModDir "About\PublishedFileId.txt"
    if (Test-Path $uploadedId)
    {
        $value = (Get-Content $uploadedId -Raw).Trim()
        if ($value -match '^\d+$')
        {
            New-Item -ItemType Directory -Force -Path (Join-Path $stageDir "About") | Out-Null
            Set-Content (Join-Path $stageDir "About\PublishedFileId.txt") $value -NoNewline -Encoding ASCII
            Write-Host "Saved Workshop ID $value with the release copy." -ForegroundColor Green
            Write-Host "Copy it to docs\workshop-id.txt to seed future releases." -ForegroundColor Yellow
        }
    }

    Remove-Item $gameModDir -Recurse -Force
    & cmd /c mklink /J "`"$gameModDir`"" "`"$Workspace`"" | Out-Null
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not recreate the development junction."
    }
    Remove-Item $markerPath -Force

    Write-Host ""
    Write-Host "Development junction restored:" -ForegroundColor Green
    Write-Host "  $gameModDir -> $Workspace"
    Write-Host "The clean release copy remains at $stageDir."
}
