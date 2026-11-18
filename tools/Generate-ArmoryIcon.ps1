# Generates the Armory main-tab icon.
#
# Style reference: Vanilla Textures Expanded main-button icons are 64x64,
# transparent, flat #BBBBBB glyphs with roughly a 47px bounding box. This script
# draws crossed swords in that style. Run only when the icon needs to change:
#
#   powershell -ExecutionPolicy Bypass -File .\tools\Generate-ArmoryIcon.ps1
#
# Output: Textures/UI/Buttons/MainButtons/IconArmory.png

param(
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) "Textures\UI\Buttons\MainButtons\IconArmory.png"),
    [string]$Fill = "#BBBBBB"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$size = 64
$color = [System.Drawing.ColorTranslator]::FromHtml($Fill)
$outDir = Split-Path -Parent $Out
if (-not (Test-Path $outDir))
{
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)
$brush = New-Object System.Drawing.SolidBrush($color)

function New-SwordPath
{
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    # Blade: tip at y=-32, base at y=13, 6px wide at the base.
    $path.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF(0, -32)),
        (New-Object System.Drawing.PointF(3, 13)),
        (New-Object System.Drawing.PointF(-3, 13))
    ))
    # Crossguard, grip, pommel.
    $path.AddRectangle((New-Object System.Drawing.RectangleF(-10, 13, 20, 3)))
    $path.AddRectangle((New-Object System.Drawing.RectangleF(-2, 16, 4, 11)))
    $path.AddEllipse((New-Object System.Drawing.RectangleF(-3.5, 27, 7, 7)))
    return $path
}

function Place-Sword($path, $angle)
{
    # 0.94 keeps the glyph near the 47px bounding box used by the reference set.
    $scale = New-Object System.Drawing.Drawing2D.Matrix
    $scale.Scale(0.94, 0.94)
    $path.Transform($scale)

    $rotate = New-Object System.Drawing.Drawing2D.Matrix
    $rotate.Rotate($angle)
    $path.Transform($rotate)

    $translate = New-Object System.Drawing.Drawing2D.Matrix
    $translate.Translate(32, 32)
    $path.Transform($translate)

    $scale.Dispose()
    $rotate.Dispose()
    $translate.Dispose()
}

$first = New-SwordPath
Place-Sword $first 45
$second = New-SwordPath
Place-Sword $second -45

# Leave a transparent gap around the second sword so the crossing reads as two
# blades instead of one solid X.
$gap = New-Object System.Drawing.Drawing2D.GraphicsPath
$gap.AddPath($second, $false)
$gapPen = New-Object System.Drawing.Pen([System.Drawing.Color]::Black, 3.0)
$gap.Widen($gapPen)
$gapPen.Dispose()

$firstRegion = New-Object System.Drawing.Region($first)
$firstRegion.Exclude((New-Object System.Drawing.Region($gap)))

$g.FillRegion($brush, $firstRegion)
$g.FillPath($brush, $second)

$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose()
$bmp.Dispose()
$firstRegion.Dispose()
$gap.Dispose()
$first.Dispose()
$second.Dispose()

Write-Host "Wrote $Out" -ForegroundColor Green
