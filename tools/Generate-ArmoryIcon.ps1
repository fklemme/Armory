# Generates the Armory main-tab icon and ModIcon.
#
# Final design: a balanced crossed sword and rifle, with no frame or background.
# Both weapons use a 45-degree diagonal so the silhouette reads as a centered X
# rather than a rotated emblem. The rifle points left before rotation, so its
# magazine stays below the barrel. The enlarged, similarly weighted weapons fill
# the canvas while retaining enough padding for clean downsampling.
#
# The 512px transparent glyph is written to About/ModIcon.png for the mod list
# and loading screen; the 64px tab icon is the same artwork downsampled.
#
#   powershell -ExecutionPolicy Bypass -File .\tools\Generate-ArmoryIcon.ps1
#
# Outputs:
#   Textures/UI/Buttons/MainButtons/IconArmory.png
#   About/ModIcon.png

param(
    [string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) "Textures\UI\Buttons\MainButtons\IconArmory.png"),
    [string]$ModIconOut = (Join-Path (Split-Path -Parent $PSScriptRoot) "About\ModIcon.png"),
    [string]$Fill = "#BBBBBB"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Hex([string]$value)
{
    return [System.Drawing.ColorTranslator]::FromHtml($value)
}

function New-SwordPath
{
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(0, -88),
        [System.Drawing.PointF]::new(12, 34),
        [System.Drawing.PointF]::new(32, 34),
        [System.Drawing.PointF]::new(32, 44),
        [System.Drawing.PointF]::new(8, 44),
        [System.Drawing.PointF]::new(8, 72),
        [System.Drawing.PointF]::new(14, 72),
        [System.Drawing.PointF]::new(0, 90),
        [System.Drawing.PointF]::new(-14, 72),
        [System.Drawing.PointF]::new(-8, 72),
        [System.Drawing.PointF]::new(-8, 44),
        [System.Drawing.PointF]::new(-32, 44),
        [System.Drawing.PointF]::new(-32, 34),
        [System.Drawing.PointF]::new(-12, 34)
    ))
    $p.CloseFigure()
    return $p
}

function New-RiflePaths
{
    $paths = New-Object System.Collections.Generic.List[System.Drawing.Drawing2D.GraphicsPath]

    $stock = New-Object System.Drawing.Drawing2D.GraphicsPath
    $stock.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(-108, -12),
        [System.Drawing.PointF]::new(-108, 28),
        [System.Drawing.PointF]::new(-78, 22),
        [System.Drawing.PointF]::new(-58, 8),
        [System.Drawing.PointF]::new(-58, -8)
    ))
    $stock.CloseFigure()
    $paths.Add($stock)

    $receiver = New-Object System.Drawing.Drawing2D.GraphicsPath
    $receiver.AddRectangle([System.Drawing.RectangleF]::new(-62, -14, 90, 26))
    $paths.Add($receiver)

    $barrel = New-Object System.Drawing.Drawing2D.GraphicsPath
    $barrel.AddRectangle([System.Drawing.RectangleF]::new(24, -4, 96, 8))
    $paths.Add($barrel)

    $handguard = New-Object System.Drawing.Drawing2D.GraphicsPath
    $handguard.AddRectangle([System.Drawing.RectangleF]::new(24, -9, 44, 18))
    $paths.Add($handguard)

    $magazine = New-Object System.Drawing.Drawing2D.GraphicsPath
    $magazine.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(0, 12),
        [System.Drawing.PointF]::new(15, 12),
        [System.Drawing.PointF]::new(28, 44),
        [System.Drawing.PointF]::new(10, 44)
    ))
    $magazine.CloseFigure()
    $paths.Add($magazine)

    return $paths
}

function Move-Path($path, [float]$scale, [float]$angle, [float]$tx, [float]$ty)
{
    $scaleMatrix = New-Object System.Drawing.Drawing2D.Matrix
    $scaleMatrix.Scale($scale, $scale)
    $path.Transform($scaleMatrix)
    $scaleMatrix.Dispose()

    $rotateMatrix = New-Object System.Drawing.Drawing2D.Matrix
    $rotateMatrix.Rotate($angle)
    $path.Transform($rotateMatrix)
    $rotateMatrix.Dispose()

    $translateMatrix = New-Object System.Drawing.Drawing2D.Matrix
    $translateMatrix.Translate($tx, $ty)
    $path.Transform($translateMatrix)
    $translateMatrix.Dispose()
}

function Move-PathMirroredX($path, [float]$scale, [float]$angle, [float]$tx, [float]$ty)
{
    $mirror = New-Object System.Drawing.Drawing2D.Matrix
    $mirror.Scale(-1, 1)
    $path.Transform($mirror)
    $mirror.Dispose()
    Move-Path $path $scale $angle $tx $ty
}

function Draw-SimpleRifleLeft($g, [float]$scale, [float]$angle, [float]$cx, [float]$cy, $color)
{
    $parts = New-RiflePaths
    $brush = New-Object System.Drawing.SolidBrush($color)
    for ($i = 0; $i -lt 5; $i++)
    {
        $path = $parts[$i]
        Move-PathMirroredX $path $scale $angle $cx $cy
        $g.FillPath($brush, $path)
        $path.Dispose()
    }
    $brush.Dispose()
    for ($i = 5; $i -lt $parts.Count; $i++)
    {
        $parts[$i].Dispose()
    }
}

$color = Hex $Fill
$bmp = New-Object System.Drawing.Bitmap(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)

# A symmetric 45-degree cross with a restrained cutout keeps both silhouettes
# legible, including at the 64px main-tab size.
Draw-SimpleRifleLeft $g 2.38 45 260 246 $color

$sword = New-SwordPath
Move-Path $sword 3.08 45 260 246
$gap = $sword.Clone()
$gapPen = New-Object System.Drawing.Pen([System.Drawing.Color]::Black, 18)
$gap.Widen($gapPen)
$gapPen.Dispose()
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$transparent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Transparent)
$g.FillPath($transparent, $gap)
$transparent.Dispose()
$g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
$gap.Dispose()

$swordBrush = New-Object System.Drawing.SolidBrush($color)
$g.FillPath($swordBrush, $sword)
$swordBrush.Dispose()
$sword.Dispose()
$g.Dispose()

$modDir = Split-Path -Parent $ModIconOut
if (-not (Test-Path $modDir))
{
    New-Item -ItemType Directory -Force -Path $modDir | Out-Null
}
$bmp.Save($ModIconOut, [System.Drawing.Imaging.ImageFormat]::Png)

$small = New-Object System.Drawing.Bitmap(64, 64, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [System.Drawing.Graphics]::FromImage($small)
$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$sg.DrawImage($bmp, 0, 0, 64, 64)
$sg.Dispose()
$bmp.Dispose()

$outDir = Split-Path -Parent $Out
if (-not (Test-Path $outDir))
{
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}
$small.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$small.Dispose()

Write-Host "Wrote $ModIconOut" -ForegroundColor Green
Write-Host "Wrote $Out" -ForegroundColor Green
