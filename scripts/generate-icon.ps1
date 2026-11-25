param(
    [string]$OutPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "Awll\app.ico")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $Radius * 2.0
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $Width - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $Width - $d, $Y + $Height - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Render-IconPngBytes {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.Clear([System.Drawing.Color]::Transparent)

        $pad = if ($Size -le 16) { 0.5 } else { [float]($Size * 0.04) }
        $boxSize = [float]($Size - (2.0 * $pad))
        $radius = [float]($boxSize * 0.24)

        $bgPath = New-RoundedRectPath -X $pad -Y $pad -Width $boxSize -Height $boxSize -Radius $radius
        $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.PointF]::new($pad, $pad),
            [System.Drawing.PointF]::new($pad, $pad + $boxSize),
            [System.Drawing.Color]::FromArgb(255, 196, 84, 49),
            [System.Drawing.Color]::FromArgb(255, 168, 66, 36)
        )
        $g.FillPath($bgBrush, $bgPath)

        # Subtle inner specular rim
        if ($Size -ge 32) {
            $rimPad = $pad + [float]($Size * 0.025)
            $rimSize = [float]($Size - (2.0 * $rimPad))
            $rimRadius = [float]($rimSize * 0.22)
            $rimPath = New-RoundedRectPath -X $rimPad -Y $rimPad -Width $rimSize -Height $rimSize -Radius $rimRadius
            $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(48, 255, 255, 255), [float]([Math]::Max(1.0, $Size * 0.015)))
            $g.DrawPath($rimPen, $rimPath)
            $rimPen.Dispose()
            $rimPath.Dispose()
        }

        $bgBrush.Dispose()
        $bgPath.Dispose()

        # Crisp geometric Swiss 'A' chevron + horizontal datum bar in warm paper white #FAF9F5
        $strokeWidth = [float]([Math]::Max(1.8, $Size * 0.11))
        $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 250, 249, 245), $strokeWidth)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

        $cx = [float]($Size * 0.5)
        $topY = [float]($Size * 0.24)
        $botY = [float]($Size * 0.74)
        $leftX = [float]($Size * 0.27)
        $rightX = [float]($Size * 0.73)

        $chevPts = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($leftX, $botY),
            [System.Drawing.PointF]::new($cx, $topY),
            [System.Drawing.PointF]::new($rightX, $botY)
        )
        $g.DrawLines($pen, $chevPts)

        # Horizontal Swiss bar across lower third
        $barY = [float]($Size * 0.59)
        $barLeft = [float]($Size * 0.34)
        $barRight = [float]($Size * 0.66)
        $barPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 250, 249, 245), [float]([Math]::Max(1.5, $Size * 0.095)))
        $barPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $barPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($barPen, $barLeft, $barY, $barRight, $barY)

        $barPen.Dispose()
        $pen.Dispose()

        $ms = New-Object System.IO.MemoryStream
        try {
            $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            return $ms.ToArray()
        } finally {
            $ms.Dispose()
        }
    } finally {
        $g.Dispose()
        $bmp.Dispose()
    }
}

$sizes = @(16, 32, 48, 256)
$frames = @()
foreach ($s in $sizes) {
    $frames += ,@($s, (Render-IconPngBytes -Size $s))
}

$outDir = Split-Path -Parent $OutPath
if ($outDir -and -not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

$fs = [System.IO.File]::Open($OutPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$bw = New-Object System.IO.BinaryWriter($fs)
try {
    # ICONDIR header: Reserved(2)=0, Type(2)=1, Count(2)
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)
    foreach ($f in $frames) {
        $sz = [int]$f[0]
        [byte[]]$bytes = $f[1]
        [byte]$dimByte = if ($sz -ge 256) { 0 } else { [byte]$sz }

        $bw.Write($dimByte)            # bWidth
        $bw.Write($dimByte)            # bHeight
        $bw.Write([byte]0)             # bColorCount
        $bw.Write([byte]0)             # bReserved
        $bw.Write([uint16]1)           # wPlanes
        $bw.Write([uint16]32)          # wBitCount
        $bw.Write([uint32]$bytes.Length) # dwBytesInRes
        $bw.Write([uint32]$offset)     # dwImageOffset
        $offset += $bytes.Length
    }

    foreach ($f in $frames) {
        [byte[]]$bytes = $f[1]
        $bw.Write($bytes)
    }
} finally {
    $bw.Dispose()
    $fs.Dispose()
}

$info = Get-Item $OutPath
Write-Output ("Generated {0} ({1} bytes) with sizes: {2}" -f $info.FullName, $info.Length, ($sizes -join ", "))
