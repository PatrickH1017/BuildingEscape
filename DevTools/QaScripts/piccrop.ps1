# Crops the same region out of two pictures and puts them one above the other, enlarged: C:\bqs\uipics\crop_<name>.png
# Usage: .\piccrop.ps1 -A <fileA> -B <fileB> -X 760 -Y 440 -W 400 -H 180 -Zoom 3 -Out C:\bqs\uipics\crop.png
param([Parameter(Mandatory)][string]$A, [Parameter(Mandatory)][string]$B, [int]$X = 0, [int]$Y = 0, [int]$W = 400, [int]$H = 200, [int]$Zoom = 3, [string]$Out = 'C:\bqs\uipics\crop.png')
Add-Type -AssemblyName System.Drawing
$ia = [Drawing.Image]::FromFile($A); $ib = [Drawing.Image]::FromFile($B)
$bmp = New-Object Drawing.Bitmap ($W * $Zoom), ($H * $Zoom * 2 + 6)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
$src = New-Object Drawing.Rectangle $X, $Y, $W, $H
$g.DrawImage($ia, (New-Object Drawing.Rectangle 0, 0, ($W * $Zoom), ($H * $Zoom)), $src, [Drawing.GraphicsUnit]::Pixel)
$g.DrawImage($ib, (New-Object Drawing.Rectangle 0, ($H * $Zoom + 6), ($W * $Zoom), ($H * $Zoom)), $src, [Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$bmp.Save($Out, [Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose(); $ia.Dispose(); $ib.Dispose()
"wrote $Out"
