$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$assetsDirectory = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Path $assetsDirectory -Force | Out-Null

$iconPath = Join-Path $assetsDirectory 'xmlssort.ico'
$bannerPath = Join-Path $assetsDirectory 'InstallerBanner.bmp'
$dialogPath = Join-Path $assetsDirectory 'InstallerDialog.bmp'

$backgroundPrimary = [System.Drawing.Color]::FromArgb(15, 23, 42)
$backgroundSecondary = [System.Drawing.Color]::FromArgb(8, 145, 178)
$accentColor = [System.Drawing.Color]::FromArgb(14, 165, 233)
$textColor = [System.Drawing.Color]::White
$mutedTextColor = [System.Drawing.Color]::FromArgb(209, 213, 219)

function New-BrandBitmap {
    param(
        [int]$Width,
        [int]$Height,
        [string]$Title,
        [string]$Subtitle
    )

    $bitmap = [System.Drawing.Bitmap]::new($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

    $rectangle = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rectangle, $backgroundPrimary, $backgroundSecondary, 20)
    $graphics.FillRectangle($brush, $rectangle)

    $accentBrush = [System.Drawing.SolidBrush]::new($accentColor)
    $graphics.FillEllipse($accentBrush, [System.Drawing.Rectangle]::new([Math]::Max(12, $Width - 130), -20, 120, 120))

    $titleFontSize = if ($Height -le 80) { 20 } elseif ($Height -le 180) { 28 } else { 34 }
    $subtitleFontSize = if ($Height -le 80) { 10 } elseif ($Height -le 180) { 14 } else { 16 }
    $titleFont = [System.Drawing.Font]::new('Segoe UI Semibold', $titleFontSize, [System.Drawing.FontStyle]::Bold)
    $subtitleFont = [System.Drawing.Font]::new('Segoe UI', $subtitleFontSize, [System.Drawing.FontStyle]::Regular)
    $titleBrush = [System.Drawing.SolidBrush]::new($textColor)
    $subtitleBrush = [System.Drawing.SolidBrush]::new($mutedTextColor)

    $paddingLeft = if ($Height -le 80) { 18 } else { 28 }
    $paddingTop = if ($Height -le 80) { 10 } else { 26 }
    $graphics.DrawString($Title, $titleFont, $titleBrush, [System.Drawing.PointF]::new($paddingLeft, $paddingTop))
    $graphics.DrawString($Subtitle, $subtitleFont, $subtitleBrush, [System.Drawing.PointF]::new($paddingLeft, $paddingTop + $titleFontSize + 10))

    $brush.Dispose()
    $accentBrush.Dispose()
    $titleBrush.Dispose()
    $subtitleBrush.Dispose()
    $titleFont.Dispose()
    $subtitleFont.Dispose()
    $graphics.Dispose()

    return $bitmap
}

$bannerBitmap = New-BrandBitmap -Width 493 -Height 58 -Title 'xmlssort tools' -Subtitle 'XML sorting and diff utilities'
$bannerBitmap.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$bannerBitmap.Dispose()

$dialogBitmap = New-BrandBitmap -Width 493 -Height 312 -Title 'xmlssort installer' -Subtitle 'Install the XML UI and CLI tools with the options you choose.'
$dialogBitmap.Save($dialogPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$dialogBitmap.Dispose()

$iconBitmap = [System.Drawing.Bitmap]::new(256, 256)
$iconGraphics = [System.Drawing.Graphics]::FromImage($iconBitmap)
$iconGraphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$iconGraphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$iconGraphics.Clear($backgroundPrimary)
$iconBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Rectangle]::new(0, 0, 256, 256), $backgroundPrimary, $backgroundSecondary, 45)
$iconGraphics.FillEllipse($iconBrush, [System.Drawing.Rectangle]::new(18, 18, 220, 220))
$iconTextBrush = [System.Drawing.SolidBrush]::new($textColor)
$iconAccentBrush = [System.Drawing.SolidBrush]::new($accentColor)
$iconFont = [System.Drawing.Font]::new('Segoe UI Semibold', 94, [System.Drawing.FontStyle]::Bold)
$smallFont = [System.Drawing.Font]::new('Segoe UI', 22, [System.Drawing.FontStyle]::Bold)
$iconGraphics.DrawString('X', $iconFont, $iconTextBrush, [System.Drawing.PointF]::new(48, 68))
$iconGraphics.DrawString('S', $iconFont, $iconAccentBrush, [System.Drawing.PointF]::new(122, 68))
$iconGraphics.DrawString('xml', $smallFont, $iconTextBrush, [System.Drawing.PointF]::new(86, 34))
$icon = [System.Drawing.Icon]::FromHandle($iconBitmap.GetHicon())
$fileStream = [System.IO.File]::Open($iconPath, [System.IO.FileMode]::Create)
$icon.Save($fileStream)
$fileStream.Dispose()
$icon.Dispose()
$iconFont.Dispose()
$smallFont.Dispose()
$iconTextBrush.Dispose()
$iconAccentBrush.Dispose()
$iconBrush.Dispose()
$iconGraphics.Dispose()
$iconBitmap.Dispose()
