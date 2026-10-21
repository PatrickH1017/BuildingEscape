# Round 12, batch G33 (after DockSeeds `t16`: 23 of 24 seeds fine, 2 forklifts everywhere; seed 119785 had no room left for the group of drums): the first group of drums goes down first (it is the smallest of the big things), then the forklifts and the pallet block.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'

Open-Src "$A\FloorGenerator.Dock.cs"
Rep 'PlaceForklifts(2); PlacePallets(1); PlaceDrums(1);' 'PlaceDrums(1); PlaceForklifts(2); PlacePallets(1);'
Save-Src
Write-Output 'round 12 batch G33 patched'
