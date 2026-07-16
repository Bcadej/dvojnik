<#
.SYNOPSIS
    Generates the Dvojnik app icon: Resources\Dvojnik.ico and Resources\Dvojnik-256.png.

.DESCRIPTION
    The .ico (16..256px frames) is used for the executable and window chrome, where the
    Windows shell picks a sensible frame.

    The .png exists because WPF cannot be trusted to pick an .ico frame: IconBitmapDecoder
    decodes the *smallest* frame and upscales it, even when DecodePixelWidth asks for 256,
    which looks blurry. Anywhere WPF renders the logo at size (the About dialog) uses the
    PNG instead.

    Re-run this after changing the artwork:
        pwsh tools\make-icon.ps1
#>
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$resDir = Join-Path $root 'FileExplorerClone\Resources'
$icoPath = Join-Path $resDir 'Dvojnik.ico'
$pngPath = Join-Path $resDir 'Dvojnik-256.png'

$sizes = @(16, 32, 48, 64, 128, 256)

function New-RoundedPath([System.Drawing.RectangleF]$r, [float]$radius) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $u = $S / 256.0   # scale unit: design on a 256 grid

    # --- Rounded background, blue vertical gradient ---
    $bgRect = New-Object System.Drawing.RectangleF(($u*8), ($u*8), ($u*240), ($u*240))
    $bgPath = New-RoundedPath $bgRect ($u*46)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, ($u*8))),
        (New-Object System.Drawing.PointF(0, ($u*248))),
        [System.Drawing.Color]::FromArgb(255, 41, 121, 214),
        [System.Drawing.Color]::FromArgb(255, 22, 71, 140))
    $g.FillPath($brush, $bgPath)
    $brush.Dispose()

    # --- Two panes ---
    $paneY = $u*66; $paneH = $u*124
    $leftX = $u*38;  $rightX = $u*134
    $paneW = $u*84

    $light = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 245, 249, 255))
    $accent = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 126, 211, 156))

    $lp = New-RoundedPath (New-Object System.Drawing.RectangleF($leftX, $paneY, $paneW, $paneH)) ($u*10)
    $g.FillPath($light, $lp)
    $rp = New-RoundedPath (New-Object System.Drawing.RectangleF($rightX, $paneY, $paneW, $paneH)) ($u*10)
    $g.FillPath($accent, $rp)

    # --- Title bars on each pane ---
    $dim = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 41, 121, 214))
    $g.FillRectangle($dim, $leftX, $paneY, $paneW, ($u*16))
    $dim2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 58, 158, 106))
    $g.FillRectangle($dim2, $rightX, $paneY, $paneW, ($u*16))

    # --- Content rows (only when big enough to read) ---
    if ($S -ge 48) {
        $row = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 168, 192, 224))
        $row2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 60, 140, 100))
        for ($i = 0; $i -lt 4; $i++) {
            $y = $paneY + ($u*30) + ($i * $u*22)
            $g.FillRectangle($row,  ($leftX + $u*12),  $y, ($paneW - $u*24), ($u*10))
            $g.FillRectangle($row2, ($rightX + $u*12), $y, ($paneW - $u*24), ($u*10))
        }
        $row.Dispose(); $row2.Dispose()
    }

    $light.Dispose(); $accent.Dispose(); $dim.Dispose(); $dim2.Dispose()
    $g.Dispose()
    return $bmp
}

# --- Standalone 256px PNG for WPF ---
$big = New-IconBitmap 256
$big.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$big.Dispose()
Write-Output "Wrote $pngPath"

# --- ICO container with PNG-compressed entries ---
$streams = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $streams += ,@($s, $ms.ToArray())
    $ms.Dispose(); $bmp.Dispose()
}

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([UInt16]0)                  # reserved
$bw.Write([UInt16]1)                  # type: icon
$bw.Write([UInt16]$streams.Count)     # image count

$offset = 6 + (16 * $streams.Count)
foreach ($entry in $streams) {
    $size = $entry[0]; $data = $entry[1]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $bw.Write([Byte]$dim)             # width  (0 means 256)
    $bw.Write([Byte]$dim)             # height
    $bw.Write([Byte]0)                # palette count
    $bw.Write([Byte]0)                # reserved
    $bw.Write([UInt16]1)              # colour planes
    $bw.Write([UInt16]32)             # bits per pixel
    $bw.Write([UInt32]$data.Length)   # bytes in resource
    $bw.Write([UInt32]$offset)        # offset
    $offset += $data.Length
}
foreach ($entry in $streams) { $bw.Write($entry[1]) }

$bw.Flush(); $bw.Dispose(); $fs.Dispose()
Write-Output "Wrote $icoPath ($((Get-Item $icoPath).Length) bytes, $($streams.Count) sizes)"
