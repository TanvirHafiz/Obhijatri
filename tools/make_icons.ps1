# Draws the placeholder app icon (green tile, red disc, white sail) and writes:
#   src\Obhijatri.App\Assets\Icons\Obhijatri.ico          (16 to 256 px, for the exe and the window)
#   src\Obhijatri.App\Assets\Icons\*.png                  (the tile and logo sizes the MSIX manifest names)
# Replace the drawing with the real logo later; keep the file names.
#   powershell -ExecutionPolicy Bypass -File tools\make_icons.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'src\Obhijatri.App\Assets\Icons'
New-Item -ItemType Directory -Force $out | Out-Null

$green = [System.Drawing.ColorTranslator]::FromHtml('#0B3D2C')
$red = [System.Drawing.ColorTranslator]::FromHtml('#F42A41')

function New-Icon([int]$width, [int]$height, [double]$padding) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded green tile.
    $r = [Math]::Min($width, $height) * 0.18
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($width - 2 * $r - 1, 0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($width - 2 * $r - 1, $height - 2 * $r - 1, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc(0, $height - 2 * $r - 1, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $green), $path)

    # Red disc, slightly left of the middle (the flag), and a white sail on it (the voyager).
    $d = [Math]::Min($width, $height) * (1 - 2 * $padding)
    $cx = $width / 2 - $d * 0.04
    $cy = $height / 2
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $red), $cx - $d / 2, $cy - $d / 2, $d, $d)
    $sail = @(
        (New-Object System.Drawing.PointF ($cx - $d * 0.02), ($cy - $d * 0.30)),
        (New-Object System.Drawing.PointF ($cx + $d * 0.24), ($cy + $d * 0.14)),
        (New-Object System.Drawing.PointF ($cx - $d * 0.02), ($cy + $d * 0.14))
    )
    $g.FillPolygon([System.Drawing.Brushes]::White, $sail)
    $g.FillRectangle([System.Drawing.Brushes]::White, $cx - $d * 0.30, $cy + $d * 0.19, $d * 0.56, $d * 0.05)
    $g.Dispose()
    return $bmp
}

function Save-Png($bmp, [string]$name) { $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png) }

# Logo sizes named in the MSIX manifest.
foreach ($size in @(@('Square44x44Logo.png', 44, 44), @('Square150x150Logo.png', 150, 150), @('StoreLogo.png', 50, 50), @('Square71x71Logo.png', 71, 71))) {
    $b = New-Icon $size[1] $size[2] 0.16; Save-Png $b $size[0]; $b.Dispose()
}
$wide = New-Icon 310 150 0.14; Save-Png $wide 'Wide310x150Logo.png'; $wide.Dispose()
$splash = New-Icon 620 300 0.16; Save-Png $splash 'SplashScreen.png'; $splash.Dispose()

# The .ico: PNG images at several sizes in one container.
$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($s in $sizes) {
    $b = New-Icon $s $s 0.12
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    , $ms.ToArray()
}
$file = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $file
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $bytes = $images[$i]
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s }))); $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0); $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$bytes.Length); $w.Write([uint32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $images) { $w.Write($bytes) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $out 'Obhijatri.ico'), $file.ToArray())
Get-ChildItem $out | Select-Object Name, Length | Format-Table -AutoSize
