# Renders a labelled contact sheet of Synty prefabs (SyntySheet.Pick in the QA tree, with graphics) and writes <Out> with each tile's name drawn in it.
# Usage: .\pick_sheet.ps1 -List list_furniture.txt -Out sheet_furniture.png [-Cols 8] [-Tile 192] [-Yaw 160]
param([Parameter(Mandatory)][string]$List, [Parameter(Mandatory)][string]$Out, [int]$Cols = 8, [int]$Tile = 192, [double]$Yaw = 160)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$listPath = if ([IO.Path]::IsPathRooted($List)) { $List } else { Join-Path $here $List }
$outPath = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $here $Out }
$raw = Join-Path $here ('raw_' + [IO.Path]::GetFileName($outPath))
$env:SYNTY_LIST = $listPath; $env:SYNTY_OUT = $raw; $env:SYNTY_COLS = "$Cols"; $env:SYNTY_TILE = "$Tile"; $env:SYNTY_YAW = "$Yaw"
try { & "$here\run_method.ps1" -Gfx -Method BuildingEscape.EditorTools.SyntySheet.Pick -Log pick -Pattern 'error CS|\[Synty\] .*drawn' -Minutes 15 }
finally { Remove-Item Env:SYNTY_LIST, Env:SYNTY_OUT, Env:SYNTY_COLS, Env:SYNTY_TILE, Env:SYNTY_YAW -ErrorAction SilentlyContinue }
Add-Type -AssemblyName System.Drawing
$names = Get-Content $listPath | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { ($_.Trim() -replace '\.prefab$','' -replace '^.*/','' -replace '^SM_Prop_','') }
$img = [System.Drawing.Bitmap]::FromFile($raw)
$g = [System.Drawing.Graphics]::FromImage($img)
$font = New-Object System.Drawing.Font 'Arial', 9, ([System.Drawing.FontStyle]::Bold)
$rows = [math]::Ceiling($names.Count / $Cols)
for ($i = 0; $i -lt $names.Count; $i++) {
  $x = ($i % $Cols) * $Tile; $y = $i / $Cols; $y = [int][math]::Floor($y) * $Tile
  $label = "{0} {1}" -f ($i + 1), $names[$i]
  $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 0, 0, 0))), $x, $y, $Tile, 15)
  $g.DrawString($label, $font, [System.Drawing.Brushes]::White, $x + 2, $y)
}
$g.Dispose(); $copy = New-Object System.Drawing.Bitmap $img; $img.Dispose(); $copy.Save($outPath); $copy.Dispose()
"labelled sheet: $outPath"
