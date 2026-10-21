# Round 12, batch G32 (after DockSeeds `t15`: 23 of 24 seeds fine, but the second forklift never found room - the stacks were placed before it - and one drum of a group stood on the edge of the way to the bay): both forklifts are placed in
# the first round, before the stacks (the stacks are small and fill what is left; ten of them still fit), and a group of drums is checked at its far drum too.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'

Open-Src "$A\FloorGenerator.Dock.cs"
Rep 'PlaceForklifts(1); PlacePallets(1); PlaceDrums(1);' 'PlaceForklifts(2); PlacePallets(1); PlaceDrums(1);'
Rep 'PlaceForklifts(1); PlacePallets(2); PlaceDrums(2);' 'PlacePallets(2); PlaceDrums(2);'
Rep 'if (!Free(x, z, 0.85f)) continue;' 'if (!Free(x, z, 0.85f) || !Free(x + 0.62f, z + 0.62f, 0.4f)) continue;          // (the group spreads 0.62 m to the east and to the north: its far drum must not stand in a keep-clear rectangle either)'
Save-Src
Write-Output 'round 12 batch G32 patched'
