# 从 dsh-pet 的鲸鱼娘 logo 生成应用图标（透明背景、多尺寸 .ico）
# 用法：powershell -ExecutionPolicy Bypass -File .\make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = Join-Path $env:APPDATA 'npm\node_modules\dsh-pet\assets\pic\notify-done.png'
$outDir = $PSScriptRoot
$icoPath = Join-Path $outDir '鲸鱼娘桌宠助手.ico'
$previewPath = Join-Path $outDir 'icon-preview.png'

if (-not (Test-Path $source)) { throw "找不到素材：$source" }

# ---------- 1) 读入并转 32bpp ARGB ----------
$src = [System.Drawing.Bitmap]::FromFile($source)
$w = $src.Width; $h = $src.Height
$bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.DrawImage($src, 0, 0, $w, $h)
$g.Dispose(); $src.Dispose()

# ---------- 2) 只有背景不透明时才去白底（notify 图本身已透明） ----------
$corner = $bmp.GetPixel(0,0)
if ($corner.A -gt 8) {
$rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $data.Stride
$bytes = New-Object byte[] ($stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$bmp.UnlockBits($data)

function Is-Bg([int]$i) {
    # BGRA
    $b = $bytes[$i]; $g2 = $bytes[$i+1]; $r = $bytes[$i+2]
    return ($r -ge 232 -and $g2 -ge 232 -and $b -ge 232)
}

$visited = New-Object bool[] ($w * $h)
$queue = New-Object 'System.Collections.Generic.Queue[int]'
$bottom = $h - 1
$right = $w - 1
for ($x = 0; $x -lt $w; $x++) {
    $idx = $x; if (-not $visited[$idx]) { $visited[$idx] = $true; $queue.Enqueue($idx) }
    $idx = $bottom * $w + $x; if (-not $visited[$idx]) { $visited[$idx] = $true; $queue.Enqueue($idx) }
}
for ($y = 0; $y -lt $h; $y++) {
    $idx = $y * $w; if (-not $visited[$idx]) { $visited[$idx] = $true; $queue.Enqueue($idx) }
    $idx = $y * $w + $right; if (-not $visited[$idx]) { $visited[$idx] = $true; $queue.Enqueue($idx) }
}

while ($queue.Count -gt 0) {
    $idx = $queue.Dequeue()
    $x = $idx % $w; $y = [int](($idx - $x) / $w)
    $off = $y * $stride + $x * 4
    if ($bytes[$off + 3] -eq 0) { continue }
    if (-not (Is-Bg $off)) { continue }
    $bytes[$off + 3] = 0
    if ($x -gt 0)     { $n = $idx - 1; if (-not $visited[$n]) { $visited[$n] = $true; $queue.Enqueue($n) } }
    if ($x -lt $w-1)  { $n = $idx + 1; if (-not $visited[$n]) { $visited[$n] = $true; $queue.Enqueue($n) } }
    if ($y -gt 0)     { $n = $idx - $w; if (-not $visited[$n]) { $visited[$n] = $true; $queue.Enqueue($n) } }
    if ($y -lt $h-1)  { $n = $idx + $w; if (-not $visited[$n]) { $visited[$n] = $true; $queue.Enqueue($n) } }
}

# 写回像素
$data2 = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data2.Scan0, $bytes.Length)
$bmp.UnlockBits($data2)
}

# ---------- 3) 裁掉透明边，再留 6% 边距，居中到正方形 ----------
$minX = $w; $minY = $h; $maxX = -1; $maxY = -1
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        $a = $bmp.GetPixel($x, $y).A
        if ($a -gt 16) {
            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
if ($maxX -lt 0) { throw '去背景后没有剩下内容' }
$cw = $maxX - $minX + 1; $ch = $maxY - $minY + 1
$side = [int]([Math]::Max($cw, $ch) * 1.12)
$canvas = New-Object System.Drawing.Bitmap $side, $side, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gg = [System.Drawing.Graphics]::FromImage($canvas)
$gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gg.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$gg.Clear([System.Drawing.Color]::Transparent)
$dx = [int](($side - $cw) / 2); $dy = [int](($side - $ch) / 2)
$gg.DrawImage($bmp, (New-Object System.Drawing.Rectangle $dx, $dy, $cw, $ch), (New-Object System.Drawing.Rectangle $minX, $minY, $cw, $ch), [System.Drawing.GraphicsUnit]::Pixel)
$gg.Dispose(); $bmp.Dispose()

# 统一放大到 512 再缩，边缘更干净
$master = New-Object System.Drawing.Bitmap 512, 512, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gm = [System.Drawing.Graphics]::FromImage($master)
$gm.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$gm.Clear([System.Drawing.Color]::Transparent)
$gm.DrawImage($canvas, 0, 0, 512, 512)
$gm.Dispose(); $canvas.Dispose()
$master.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)

# ---------- 4) 生成多尺寸 ICO（PNG 压缩帧） ----------
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($s in $sizes) {
    $b = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gs = [System.Drawing.Graphics]::FromImage($b)
    $gs.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gs.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gs.Clear([System.Drawing.Color]::Transparent)
    $gs.DrawImage($master, 0, 0, $s, $s)
    $gs.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $b.Dispose()
    $frames += ,@($s, $ms.ToArray())
}
$master.Dispose()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type = icon
$bw.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $s = $f[0]; $png = $f[1]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$png.Length)
    $bw.Write([UInt32]$offset)
    $offset += $png.Length
}
foreach ($f in $frames) { $bw.Write($f[1]) }
$bw.Close(); $fs.Close()

Write-Host "已生成：$icoPath"
Write-Host "预览：$previewPath"
