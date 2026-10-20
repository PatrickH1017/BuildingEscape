# Round 2, batch 14: the rooms get broadloom only, no carpet-square grid (a grid on a carpet reads as tile; the owner: "make sure the tile grids don't overlap with any carpets"). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'
Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Surfaces.cs')
Rep 'const float FloorTop = 0.010f, WeaveRepeat = 2.4f, TileRepeat = 1.2f, CarpetBoost = 1.15f;' @'
const float FloorTop = 0.010f, WeaveRepeat = 2.4f, TileRepeat = 1.2f, CarpetBoost = 1.15f;
        /// <summary>Carpet tiles (0.6 m squares with seams) show a grid like the corridors' tile, so by default every carpeted room gets plain broadloom: grid = path, no grid = rooms.</summary>
        const bool CarpetTilesOn = false;
'@
Rep 'bool squares = tint == 1 || tint == 7 || tint == 10;' 'bool squares = CarpetTilesOn && (tint == 1 || tint == 7 || tint == 10);'
Save-Src
Write-Output 'batch 14 patched'
