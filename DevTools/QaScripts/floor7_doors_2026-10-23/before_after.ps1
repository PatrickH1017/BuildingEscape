# Before / after pictures of the floor 7 doors: each name N is %TEMP%\be_shot_before_N.png next to %TEMP%\be_shot_after_N.png; one picture per name, the two side by side with a caption bar.
# Usage: .\before_after.ps1 -Names d6_shut_a,d2_shut_a -Out <folder> [-Captions 'White door','Wooden door'] [-Width 880] ; with -Sheet <file> the first picture of each name goes into one overview sheet (rows = names).
param([Parameter(Mandatory)][string[]]$Names, [Parameter(Mandatory)][string]$Out, [string[]]$Captions = @(), [int]$Width = 880, [string]$Sheet = '', [int]$SheetWidth = 640)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null
$tmp = $env:TEMP
$font = New-Object Drawing.Font 'Segoe UI', 13, ([Drawing.FontStyle]::Bold)
$pairs = @()
for ($i = 0; $i -lt $Names.Count; $i++) {
  $n = $Names[$i]
  $a = Join-Path $tmp "be_shot_before_$n.png"; $b = Join-Path $tmp "be_shot_after_$n.png"
  if (-not (Test-Path $a) -or -not (Test-Path $b)) { "missing: $n"; continue }
  $cap = if ($i -lt $Captions.Count) { $Captions[$i] } else { $n }
  $ia = [Drawing.Image]::FromFile($a); $ib = [Drawing.Image]::FromFile($b)
  $h = [int]($Width * $ia.Height / $ia.Width); $bar = 34
  $bmp = New-Object Drawing.Bitmap ($Width * 2 + 6), ($h + $bar)
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([Drawing.Color]::FromArgb(24, 24, 28))
  $g.DrawImage($ia, 0, $bar, $Width, $h); $g.DrawImage($ib, $Width + 6, $bar, $Width, $h)
  $g.DrawString("BEFORE  -  $cap", $font, [Drawing.Brushes]::White, 8, 5)
  $g.DrawString("AFTER", $font, [Drawing.Brushes]::White, $Width + 14, 5)
  $g.Dispose()
  $file = Join-Path $Out ("{0:00}_{1}.png" -f ($i + 1), $n)
  $bmp.Save($file, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose(); $ia.Dispose(); $ib.Dispose()
  $pairs += , @($a, $b, $cap)
  "wrote $file"
}
if ($Sheet -ne '' -and $pairs.Count -gt 0) {
  $bar = 30; $h = [int]($SheetWidth * 9 / 16)
  $bmp = New-Object Drawing.Bitmap ($SheetWidth * 2 + 6), ($pairs.Count * ($h + $bar))
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([Drawing.Color]::FromArgb(24, 24, 28))
  for ($r = 0; $r -lt $pairs.Count; $r++) {
    $ia = [Drawing.Image]::FromFile($pairs[$r][0]); $ib = [Drawing.Image]::FromFile($pairs[$r][1])
    $y = $r * ($h + $bar)
    $g.DrawImage($ia, 0, $y + $bar, $SheetWidth, $h); $g.DrawImage($ib, $SheetWidth + 6, $y + $bar, $SheetWidth, $h)
    $g.DrawString("BEFORE  -  " + $pairs[$r][2], $font, [Drawing.Brushes]::White, 8, $y + 4)
    $g.DrawString("AFTER", $font, [Drawing.Brushes]::White, $SheetWidth + 14, $y + 4)
    $ia.Dispose(); $ib.Dispose()
  }
  $g.Dispose(); $bmp.Save($Sheet, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
  "wrote the overview $Sheet"
}
