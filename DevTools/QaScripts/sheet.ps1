# A contact sheet: the given pictures in a grid with a caption under each (the file name without "be_shot_" and ".png").
# Usage: .\sheet.ps1 -Files a.png,b.png,... -Out C:\bqs\sheet.png [-Cols 3] [-CellW 640] [-CellH 360]
param([Parameter(Mandatory)][string[]]$Files, [Parameter(Mandatory)][string]$Out, [int]$Cols = 3, [int]$CellW = 640, [int]$CellH = 360)
Add-Type -AssemblyName System.Drawing
$rows = [int][Math]::Ceiling($Files.Count / [double]$Cols)
$cap = 22
$bmp = New-Object System.Drawing.Bitmap ($Cols * $CellW), ($rows * ($CellH + $cap))
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(30, 30, 34))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font = New-Object System.Drawing.Font('Consolas', 11)
$brush = [System.Drawing.Brushes]::White
for ($i = 0; $i -lt $Files.Count; $i++) {
  $x = ($i % $Cols) * $CellW; $y = [Math]::Floor($i / $Cols) * ($CellH + $cap)
  if (Test-Path $Files[$i]) {
    $img = [System.Drawing.Image]::FromFile($Files[$i])
    $g.DrawImage($img, $x, $y + $cap, $CellW, $CellH)
    $img.Dispose()
  }
  $name = [IO.Path]::GetFileNameWithoutExtension($Files[$i]) -replace '^be_shot_', ''
  $g.DrawString($name, $font, $brush, $x + 4, $y + 2)
}
$g.Dispose()
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"saved $Out ($($Files.Count) pictures)"
