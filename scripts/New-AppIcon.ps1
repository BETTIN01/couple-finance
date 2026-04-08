param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\CoupleFinance.Desktop\Assets')
)

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
    param(
        [System.Drawing.RectangleF]$Rect,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath

    $path.AddArc($Rect.X, $Rect.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rect.Right - $diameter, $Rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rect.X, $Rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()

    return $path
}

function New-ColorBlend {
    param(
        [System.Drawing.Color[]]$Colors,
        [float[]]$Positions
    )

    $blend = New-Object System.Drawing.Drawing2D.ColorBlend
    $blend.Colors = $Colors
    $blend.Positions = $Positions
    return $blend
}

function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $canvas = New-Object System.Drawing.RectangleF 4, 4, ($Size - 8), ($Size - 8)
    $cornerRadius = [Math]::Max(8, $Size * 0.22)
    $backgroundPath = New-RoundedRectPath -Rect $canvas -Radius $cornerRadius
    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0),
        (New-Object System.Drawing.PointF $Size, $Size),
        [System.Drawing.Color]::FromArgb(255, 11, 22, 46),
        [System.Drawing.Color]::FromArgb(255, 42, 21, 63))

    $backgroundBrush.InterpolationColors = New-ColorBlend `
        -Colors @(
            [System.Drawing.Color]::FromArgb(255, 9, 20, 43),
            [System.Drawing.Color]::FromArgb(255, 21, 34, 70),
            [System.Drawing.Color]::FromArgb(255, 46, 25, 72)
        ) `
        -Positions @(0.0, 0.56, 1.0)

    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $glowRect = New-Object System.Drawing.RectangleF ($Size * 0.16), ($Size * 0.12), ($Size * 0.70), ($Size * 0.54)
    $glowPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glowPath.AddEllipse($glowRect)
    $glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush $glowPath
    $glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(72, 255, 191, 94)
    $glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 255, 191, 94))
    $graphics.FillEllipse($glowBrush, $glowRect)

    $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(62, 255, 255, 255), [Math]::Max(1.5, $Size * 0.018))
    $graphics.DrawPath($borderPen, $backgroundPath)

    $coinBack = New-Object System.Drawing.RectangleF ($Size * 0.47), ($Size * 0.20), ($Size * 0.24), ($Size * 0.24)
    $coinFront = New-Object System.Drawing.RectangleF ($Size * 0.22), ($Size * 0.28), ($Size * 0.36), ($Size * 0.36)
    $coinShadow = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(56, 4, 8, 20))
    $coinFill = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF $coinFront.Left, $coinFront.Top),
        (New-Object System.Drawing.PointF $coinFront.Right, $coinFront.Bottom),
        [System.Drawing.Color]::FromArgb(255, 255, 198, 102),
        [System.Drawing.Color]::FromArgb(255, 236, 143, 27))
    $coinFill.InterpolationColors = New-ColorBlend `
        -Colors @(
            [System.Drawing.Color]::FromArgb(255, 255, 210, 122),
            [System.Drawing.Color]::FromArgb(255, 245, 166, 48),
            [System.Drawing.Color]::FromArgb(255, 216, 120, 21)
        ) `
        -Positions @(0.0, 0.58, 1.0)

    $coinBackFill = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF $coinBack.Left, $coinBack.Top),
        (New-Object System.Drawing.PointF $coinBack.Right, $coinBack.Bottom),
        [System.Drawing.Color]::FromArgb(220, 255, 198, 102),
        [System.Drawing.Color]::FromArgb(220, 222, 130, 26))
    $coinOutline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 224, 167), [Math]::Max(2.0, $Size * 0.028))
    $coinOutline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $graphics.FillEllipse($coinShadow, ($coinBack.X + ($Size * 0.012)), ($coinBack.Y + ($Size * 0.018)), $coinBack.Width, $coinBack.Height)
    $graphics.FillEllipse($coinShadow, ($coinFront.X + ($Size * 0.012)), ($coinFront.Y + ($Size * 0.018)), $coinFront.Width, $coinFront.Height)
    $graphics.FillEllipse($coinBackFill, $coinBack)
    $graphics.DrawEllipse($coinOutline, $coinBack)
    $graphics.FillEllipse($coinFill, $coinFront)
    $graphics.DrawEllipse($coinOutline, $coinFront)

    $nodeFill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 248, 250, 255))
    $nodeOutline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 237, 151, 33), [Math]::Max(1.5, $Size * 0.018))
    $linePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 244, 248, 255), [Math]::Max(2.5, $Size * 0.05))
    $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $linePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $points = @(
        (New-Object System.Drawing.PointF ($Size * 0.28), ($Size * 0.67)),
        (New-Object System.Drawing.PointF ($Size * 0.41), ($Size * 0.55)),
        (New-Object System.Drawing.PointF ($Size * 0.54), ($Size * 0.60)),
        (New-Object System.Drawing.PointF ($Size * 0.69), ($Size * 0.39))
    )

    $graphics.DrawLines($linePen, $points)

    $nodeDiameter = [Math]::Max(4.0, $Size * 0.085)
    foreach ($point in $points) {
        $nodeRect = New-Object System.Drawing.RectangleF ($point.X - ($nodeDiameter / 2)), ($point.Y - ($nodeDiameter / 2)), $nodeDiameter, $nodeDiameter
        $graphics.FillEllipse($nodeFill, $nodeRect)
        $graphics.DrawEllipse($nodeOutline, $nodeRect)
    }

    $sparkRect = New-Object System.Drawing.RectangleF ($Size * 0.66), ($Size * 0.18), ($Size * 0.10), ($Size * 0.10)
    $sparkFill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(210, 255, 245, 210))
    $sparkPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(180, 255, 231, 171), [Math]::Max(1.0, $Size * 0.012))
    $graphics.FillEllipse($sparkFill, $sparkRect)
    $graphics.DrawEllipse($sparkPen, $sparkRect)

    $backgroundBrush.Dispose()
    $glowBrush.Dispose()
    $glowPath.Dispose()
    $borderPen.Dispose()
    $coinShadow.Dispose()
    $coinFill.Dispose()
    $coinBackFill.Dispose()
    $coinOutline.Dispose()
    $nodeFill.Dispose()
    $nodeOutline.Dispose()
    $linePen.Dispose()
    $sparkFill.Dispose()
    $sparkPen.Dispose()
    $backgroundPath.Dispose()
    $graphics.Dispose()

    return $bitmap
}

function Get-PngBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $memoryStream = New-Object System.IO.MemoryStream
    $Bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $memoryStream.ToArray()
    $memoryStream.Dispose()
    return $bytes
}

function Save-IcoFile {
    param(
        [string]$Path,
        [int[]]$Sizes,
        [byte[][]]$Images
    )

    $fileStream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($fileStream)

    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$Images.Length)

    $offset = 6 + (16 * $Images.Length)
    for ($i = 0; $i -lt $Images.Length; $i++) {
        $size = $Sizes[$i]
        $image = $Images[$i]

        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $writer.Write([byte]($(if ($size -ge 256) { 0 } else { $size })))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$image.Length)
        $writer.Write([UInt32]$offset)

        $offset += $image.Length
    }

    for ($i = 0; $i -lt $Images.Length; $i++) {
        $writer.Write($Images[$i])
    }

    $writer.Flush()
    $writer.Dispose()
    $fileStream.Dispose()
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

$iconSizes = @(16, 24, 32, 48, 64, 128, 256)
$iconImages = New-Object 'System.Collections.Generic.List[byte[]]'

foreach ($size in $iconSizes) {
    $bitmap = New-IconBitmap -Size $size
    $iconImages.Add((Get-PngBytes -Bitmap $bitmap))
    $bitmap.Dispose()
}

$previewBitmap = New-IconBitmap -Size 512
$previewPath = Join-Path $resolvedOutput 'AppIcon-preview.png'
$previewBitmap.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
$previewBitmap.Dispose()

$icoPath = Join-Path $resolvedOutput 'AppIcon.ico'
Save-IcoFile -Path $icoPath -Sizes $iconSizes -Images $iconImages.ToArray()

Write-Host "Ícone salvo em $icoPath"
Write-Host "Preview salvo em $previewPath"
