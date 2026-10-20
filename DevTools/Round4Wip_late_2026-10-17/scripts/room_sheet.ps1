# Stitches the overhead room pictures (NetDevHarness.RoomShots, %TEMP%\be_<tag>_<theme>_f<floor>_<n>.png) into one labelled sheet.
# Usage: .\room_sheet.ps1 -Tag base -Themes break,office -Out sheet_rooms.png [-Cols 3] [-Tile 420]
param([Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string[]]$Themes, [Parameter(Mandatory)][string]$Out, [int]$Cols = 3, [int]$Tile = 420)
Add-Type -AssemblyName System.Drawing
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @()
foreach ($t in $Themes) { $files += Get-ChildItem $env:TEMP -Filter "be_${Tag}_${t}_f*.png" | Sort-Object Name }
if ($files.Count -eq 0) { "no pictures for tag $Tag themes $($Themes -join ',')"; return }
$rows = [math]::Ceiling($files.Count / $Cols)
$bmp = New-Object System.Drawing.Bitmap ($Cols * $Tile), ($rows * $Tile)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(30, 30, 34)); $g.InterpolationMode = 'HighQualityBicubic'
$font = New-Object System.Drawing.Font 'Arial', 10, ([System.Drawing.FontStyle]::Bold)
for ($i = 0; $i -lt $files.Count; $i++) {
  $img = [System.Drawing.Image]::FromFile($files[$i].FullName)
  $x = ($i % $Cols) * $Tile; $y = [int][math]::Floor($i / $Cols) * $Tile
  $g.DrawImage($img, $x, $y, $Tile, $Tile); $img.Dispose()
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(170, 0, 0, 0))), $x, $y, $Tile, 17)
  $g.DrawString(($files[$i].BaseName -replace "^be_${Tag}_", ''), $font, [System.Drawing.Brushes]::White, $x + 3, $y)
}
$g.Dispose()
$outPath = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $here $Out }
$bmp.Save($outPath); $bmp.Dispose()
"sheet: $outPath ($($files.Count) rooms)"
