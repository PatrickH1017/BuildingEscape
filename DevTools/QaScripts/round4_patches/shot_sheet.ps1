# Stitches eye-level pictures (NetDevHarness.CoreLooks etc.: %TEMP%\be_shot_<name>.png, 1280 x 720) into one labelled sheet.
# Usage: .\shot_sheet.ps1 -Pattern 'core_f*_axe*' -Out sheet_axes.png [-Cols 3] [-W 640]
param([Parameter(Mandatory)][string]$Pattern, [Parameter(Mandatory)][string]$Out, [int]$Cols = 3, [int]$W = 640)
Add-Type -AssemblyName System.Drawing
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @(Get-ChildItem $env:TEMP -Filter "be_shot_$Pattern.png" | Sort-Object Name)
if ($files.Count -eq 0) { "no pictures for $Pattern"; return }
$H = [int]($W * 9 / 16)
$rows = [math]::Ceiling($files.Count / $Cols)
$bmp = New-Object System.Drawing.Bitmap ($Cols * $W), ($rows * $H)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(30, 30, 34)); $g.InterpolationMode = 'HighQualityBicubic'
$font = New-Object System.Drawing.Font 'Arial', 10, ([System.Drawing.FontStyle]::Bold)
for ($i = 0; $i -lt $files.Count; $i++) {
  $img = [System.Drawing.Image]::FromFile($files[$i].FullName)
  $x = ($i % $Cols) * $W; $y = [int][math]::Floor($i / $Cols) * $H
  $g.DrawImage($img, $x, $y, $W, $H); $img.Dispose()
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(170, 0, 0, 0))), $x, $y, $W, 17)
  $g.DrawString(($files[$i].BaseName -replace '^be_shot_', ''), $font, [System.Drawing.Brushes]::White, $x + 3, $y)
}
$g.Dispose()
$outPath = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $here $Out }
$bmp.Save($outPath); $bmp.Dispose()
"sheet: $outPath ($($files.Count) pictures)"
