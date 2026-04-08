# Creates a simple .ico file for the app (blue 2x2 grid on transparent background)
Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(230, 79, 193, 255))
$padding = 16
$gap = 24
$cellW = [int](($size - 2 * $padding - $gap) / 2)
$cellH = $cellW

$cells = @(
    [System.Drawing.Rectangle]::new($padding, $padding, $cellW, $cellH),
    [System.Drawing.Rectangle]::new($padding + $cellW + $gap, $padding, $cellW, $cellH),
    [System.Drawing.Rectangle]::new($padding, $padding + $cellH + $gap, $cellW, $cellH),
    [System.Drawing.Rectangle]::new($padding + $cellW + $gap, $padding + $cellH + $gap, $cellW, $cellH)
)

foreach ($cell in $cells) {
    $radius = 20
    $d = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($cell.X, $cell.Y, $d, $d, 180, 90)
    $path.AddArc($cell.Right - $d, $cell.Y, $d, $d, 270, 90)
    $path.AddArc($cell.Right - $d, $cell.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($cell.X, $cell.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)
}

$g.Dispose()

$icoPath = Join-Path $PSScriptRoot "..\src\WindowTaskSwitcher\Resources\app.ico"
$ms = New-Object System.IO.MemoryStream

# Write ICO header
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([Int16]0)      # reserved
$writer.Write([Int16]1)      # type: icon
$writer.Write([Int16]1)      # count

# Write ICO directory entry (256x256 PNG)
$pngMs = New-Object System.IO.MemoryStream
$bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = $pngMs.ToArray()

$writer.Write([byte]0)       # width (0 = 256)
$writer.Write([byte]0)       # height (0 = 256)
$writer.Write([byte]0)       # color palette
$writer.Write([byte]0)       # reserved
$writer.Write([Int16]1)      # color planes
$writer.Write([Int16]32)     # bits per pixel
$writer.Write([Int32]$pngBytes.Length)  # size
$writer.Write([Int32]22)     # offset (6 header + 16 entry)

$writer.Write($pngBytes)
$writer.Flush()

[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())

$bmp.Dispose()
Write-Host "Created icon at $icoPath"
