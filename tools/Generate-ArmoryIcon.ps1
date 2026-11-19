# Generates the Armory main-tab icon and ModIcon.
#
# Final design: a balanced crossed sword and rifle, with no frame or background.
# Both weapons use a 45-degree diagonal and have the same diagonal span, so each
# touches the same top, bottom, left, and right boundaries. The rifle points left
# before rotation, so its magazine stays below the barrel. A stepped shaft,
# detachable magazine, grip, and raised scope make the foreground rifle readable
# at 64px. The shared bounds are optically centered with enough padding for clean
# downsampling.
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

    # A stepped stock, narrow wrist, and broad receiver keep the rifle readable
    # without relying on details that disappear at the main-tab size.
    $stock = New-Object System.Drawing.Drawing2D.GraphicsPath
    $stock.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(-82, -10),
        [System.Drawing.PointF]::new(-58, -8),
        [System.Drawing.PointF]::new(-58, 8),
        [System.Drawing.PointF]::new(-72, 17),
        [System.Drawing.PointF]::new(-88, 18),
        [System.Drawing.PointF]::new(-108, 12),
        [System.Drawing.PointF]::new(-108, -12)
    ))
    $stock.CloseFigure()
    $paths.Add($stock)

    $receiver = New-Object System.Drawing.Drawing2D.GraphicsPath
    $receiver.AddRectangle([System.Drawing.RectangleF]::new(-62, -12, 94, 24))
    $paths.Add($receiver)

    # The long, thin barrel and wider handguard form a clear stepped shaft.
    $handguard = New-Object System.Drawing.Drawing2D.GraphicsPath
    $handguard.AddRectangle([System.Drawing.RectangleF]::new(28, -9, 54, 18))
    $paths.Add($handguard)

    $barrel = New-Object System.Drawing.Drawing2D.GraphicsPath
    $barrel.AddRectangle([System.Drawing.RectangleF]::new(76, -3, 44, 6))
    $paths.Add($barrel)

    $muzzle = New-Object System.Drawing.Drawing2D.GraphicsPath
    $muzzle.AddRectangle([System.Drawing.RectangleF]::new(112, -4, 8, 8))
    $paths.Add($muzzle)

    $grip = New-Object System.Drawing.Drawing2D.GraphicsPath
    $grip.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(-48, 11),
        [System.Drawing.PointF]::new(-28, 11),
        [System.Drawing.PointF]::new(-31, 37),
        [System.Drawing.PointF]::new(-45, 42),
        [System.Drawing.PointF]::new(-52, 34)
    ))
    $grip.CloseFigure()
    $paths.Add($grip)

    $magazine = New-Object System.Drawing.Drawing2D.GraphicsPath
    $magazine.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(2, 11),
        [System.Drawing.PointF]::new(22, 11),
        [System.Drawing.PointF]::new(30, 38),
        [System.Drawing.PointF]::new(24, 48),
        [System.Drawing.PointF]::new(8, 45),
        [System.Drawing.PointF]::new(0, 24)
    ))
    $magazine.CloseFigure()
    $paths.Add($magazine)

    $triggerGuard = New-Object System.Drawing.Drawing2D.GraphicsPath
    $triggerGuard.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding
    $triggerGuard.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(-28, 11),
        [System.Drawing.PointF]::new(-2, 11),
        [System.Drawing.PointF]::new(-2, 18),
        [System.Drawing.PointF]::new(-10, 28),
        [System.Drawing.PointF]::new(-24, 28),
        [System.Drawing.PointF]::new(-30, 20)
    ))
    $triggerGuard.CloseFigure()
    # Reverse winding cuts the inner opening out of the combined silhouette.
    $triggerGuard.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(-25, 19),
        [System.Drawing.PointF]::new(-21, 22),
        [System.Drawing.PointF]::new(-12, 22),
        [System.Drawing.PointF]::new(-8, 17),
        [System.Drawing.PointF]::new(-23, 17)
    ))
    $triggerGuard.CloseFigure()
    $paths.Add($triggerGuard)

    # A raised scope with two mounts creates negative space that survives
    # downsampling and distinguishes the weapon from a generic stick.
    $scopeRail = New-Object System.Drawing.Drawing2D.GraphicsPath
    $scopeRail.AddRectangle([System.Drawing.RectangleF]::new(-48, -16, 80, 5))
    $paths.Add($scopeRail)

    foreach ($mountX in @(-28, 8))
    {
        $mount = New-Object System.Drawing.Drawing2D.GraphicsPath
        $mount.AddRectangle([System.Drawing.RectangleF]::new($mountX, -25, 8, 10))
        $paths.Add($mount)
    }

    $scopeTube = New-Object System.Drawing.Drawing2D.GraphicsPath
    $scopeTube.AddRectangle([System.Drawing.RectangleF]::new(-42, -34, 66, 11))
    $paths.Add($scopeTube)

    $rearScopeBell = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rearScopeBell.AddRectangle([System.Drawing.RectangleF]::new(-48, -36, 12, 15))
    $paths.Add($rearScopeBell)

    $frontScopeBell = New-Object System.Drawing.Drawing2D.GraphicsPath
    $frontScopeBell.AddRectangle([System.Drawing.RectangleF]::new(16, -36, 16, 15))
    $paths.Add($frontScopeBell)

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

function Draw-SimpleRifleLeft($g, [float]$scale, [float]$angle, [float]$cx, [float]$cy, $color, [float]$gapWidth = 0)
{
    # Fill one winding path instead of layering antialiased components. This
    # removes hairline seams where the stock, receiver, scope, and barrel join.
    $rifle = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rifle.FillMode = [System.Drawing.Drawing2D.FillMode]::Winding
    $parts = New-RiflePaths
    foreach ($path in $parts)
    {
        Move-PathMirroredX $path $scale $angle $cx $cy
        $rifle.AddPath($path, $false)
        $path.Dispose()
    }

    if ($gapWidth -gt 0)
    {
        $gapPen = New-Object System.Drawing.Pen([System.Drawing.Color]::Black, $gapWidth)
        $gapPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $transparent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::Transparent)
        $gap = $rifle.Clone()
        $gap.Widen($gapPen)
        $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g.FillPath($transparent, $gap)
        $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $gap.Dispose()
        $transparent.Dispose()
        $gapPen.Dispose()
    }

    $brush = New-Object System.Drawing.SolidBrush($color)
    $g.FillPath($brush, $rifle)
    $brush.Dispose()
    $rifle.Dispose()
}

$color = Hex $Fill
$bmp = New-Object System.Drawing.Bitmap(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)

# Normalize each weapon independently to the same approximately 437px square.
# Their axes still intersect at the canvas center, while their transformed
# bounding boxes align on all four sides. The rifle is foregrounded so its
# receiver, magazine, grip, and scope remain intact at the main-tab size.
$sword = New-SwordPath
Move-Path $sword 3.46 45 258.5 253.5
$swordBrush = New-Object System.Drawing.SolidBrush($color)
$g.FillPath($swordBrush, $sword)
$swordBrush.Dispose()
$sword.Dispose()

Draw-SimpleRifleLeft $g 2.53 45 260 260 $color 10
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
