# Generates the Armory mod-list banner (About/Preview.png).
#
# The banner uses the same transparent crossed-weapons artwork as the tab and
# ModIcon, so the mod identity stays consistent. The author name is deliberately
# omitted; the footer names the supported game version instead.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\Generate-ArmoryBanner.ps1
#
# Output: About/Preview.png (640x360)

param(
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) "About\Preview.png"),
    [string]$Icon = (Join-Path (Split-Path -Parent $PSScriptRoot) "About\ModIcon.png"),
    [string]$Tagline = "The right weapon, in the right hands."
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Hex([string]$value)
{
    return [System.Drawing.ColorTranslator]::FromHtml($value)
}

function New-FittedFont($g, [string]$family, [float]$size, $style, [float]$maxWidth, [string]$text)
{
    $font = New-Object System.Drawing.Font($family, $size, $style)
    while ($size -gt 6 -and $g.MeasureString($text, $font).Width -gt $maxWidth)
    {
        $size -= 0.5
        $font.Dispose()
        $font = New-Object System.Drawing.Font($family, $size, $style)
    }
    return $font
}

$width = 640
$height = 360
$bmp = New-Object System.Drawing.Bitmap($width, $height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

# Background: deep slate with a subtle diagonal lift toward the icon.
$background = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    [System.Drawing.RectangleF]::new(0, 0, $width, $height),
    (Hex "#2A3542"), (Hex "#10161D"), 28.0)
$g.FillRectangle($background, 0, 0, $width, $height)
$background.Dispose()

# Brass rule across the top, matching the mod's gold accent.
$rule = New-Object System.Drawing.SolidBrush((Hex "#D9A441"))
$g.FillRectangle($rule, 0, 0, $width, 5)
$rule.Dispose()

$white = New-Object System.Drawing.SolidBrush((Hex "#F2F5F8"))
$gold = New-Object System.Drawing.SolidBrush((Hex "#E3B24C"))
$muted = New-Object System.Drawing.SolidBrush((Hex "#A6B2C0"))
$faint = New-Object System.Drawing.SolidBrush((Hex "#7F8C9B"))

$titleFont = New-Object System.Drawing.Font("Arial", 54, [System.Drawing.FontStyle]::Bold)
$taglineFont = New-FittedFont $g "Arial" 22 ([System.Drawing.FontStyle]::Regular) 330 $Tagline
$subtitleFont = New-Object System.Drawing.Font("Arial", 11, [System.Drawing.FontStyle]::Regular)
$featureFont = New-Object System.Drawing.Font("Arial", 8.5, [System.Drawing.FontStyle]::Bold)
$footerFont = New-Object System.Drawing.Font("Arial", 9, [System.Drawing.FontStyle]::Regular)

$g.DrawString("ARMORY", $titleFont, $white, 48, 64)
$g.DrawString($Tagline, $taglineFont, $gold, 49, 154)

$g.DrawString("Range-aware rankings, typed hand and backup slots,", $subtitleFont, $muted, 49, 196)
$g.DrawString("and a craft / research / buy plan for RimWorld 1.6.", $subtitleFont, $muted, 49, 216)

# Thin divider keeps the feature strip separate without adding a box.
$divider = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 217, 164, 65), 1)
$g.DrawLine($divider, 49, 266, 591, 266)
$divider.Dispose()

$dot = [string][char]0x00B7
$g.DrawString("RANGE-AWARE RANKINGS   $dot   HAND + BACKUP SLOTS   $dot   CRAFT / RESEARCH / BUY",
    $featureFont, $faint, 49, 284)

# Footer: game version, not the author name.
$footerFormat = New-Object System.Drawing.StringFormat
$footerFormat.Alignment = [System.Drawing.StringAlignment]::Far
$g.DrawString("RimWorld 1.6", $footerFont, $faint,
    [System.Drawing.RectangleF]::new(0, 322, 591, 22), $footerFormat)
$footerFormat.Dispose()

# Icon: same transparent artwork as About/ModIcon.png.
if (-not (Test-Path $Icon))
{
    throw "Missing icon: $Icon. Run tools/Generate-ArmoryIcon.ps1 first."
}
$iconImage = [System.Drawing.Image]::FromFile((Resolve-Path $Icon).Path)
$g.DrawImage($iconImage, 408, 70, 208, 208)
$iconImage.Dispose()

$titleFont.Dispose()
$taglineFont.Dispose()
$subtitleFont.Dispose()
$featureFont.Dispose()
$footerFont.Dispose()
$white.Dispose()
$gold.Dispose()
$muted.Dispose()
$faint.Dispose()
$g.Dispose()

$outDir = Split-Path -Parent $Out
if (-not (Test-Path $outDir))
{
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Host "Wrote $Out" -ForegroundColor Green
