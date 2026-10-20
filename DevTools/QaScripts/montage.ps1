# Puts two pictures of the same screen side by side (left: Classic, right: Blue) with a caption bar: C:\bqs\uipics\compare\<name>.png
# Usage: .\montage.ps1 -Left C:\bqs\uipics\classic -Right C:\bqs\uipics\blue -Names be_menu_lobby_main,be_menu_esc
param([Parameter(Mandatory)][string]$Left, [Parameter(Mandatory)][string]$Right, [Parameter(Mandatory)][string[]]$Names, [string]$Out = 'C:\bqs\uipics\compare', [int]$Width = 960)
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Out | Out-Null
foreach ($n in $Names) {
  $a = Join-Path $Left "$n.png"; $b = Join-Path $Right "$n.png"
  if (-not (Test-Path $a) -or -not (Test-Path $b)) { "missing: $n"; continue }
  $ia = [Drawing.Image]::FromFile($a); $ib = [Drawing.Image]::FromFile($b)
  $h = [int]($Width * $ia.Height / $ia.Width)
  $bar = 34
  $bmp = New-Object Drawing.Bitmap ($Width * 2 + 6), ($h + $bar)
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.Clear([Drawing.Color]::FromArgb(24, 24, 28))
  $g.DrawImage($ia, 0, $bar, $Width, $h)
  $g.DrawImage($ib, $Width + 6, $bar, $Width, [int]($Width * $ib.Height / $ib.Width))
  $font = New-Object Drawing.Font 'Segoe UI', 14, ([Drawing.FontStyle]::Bold)
  $g.DrawString("CLASSIC  -  $n", $font, [Drawing.Brushes]::White, 8, 5)
  $g.DrawString("BLUE (TEST)", $font, [Drawing.Brushes]::White, $Width + 14, 5)
  $g.Dispose()
  $bmp.Save((Join-Path $Out "$n.png"), [Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose(); $ia.Dispose(); $ib.Dispose()
  "wrote $n"
}
