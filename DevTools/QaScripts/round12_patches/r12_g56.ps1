# Round 12, batch G56: the docks' pallet blocks (DockLook / DockSeeds want six in all; the PVP room has four): the two later pallet blocks of the random storage failed to find room on one seed in 24 (and on the
# default seed) once the way into the PVP room was kept clear on a wider plan; all three are placed before the box stacks now, which crowd the floor.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
Rep @'
            PlaceDrums(1); PlaceForklifts(2); PlacePallets(1);
            int haveStacks = PlaceStacks(10);
            PlacePallets(2); PlaceDrums(2);
'@ @'
            PlaceDrums(1); PlaceForklifts(2); PlacePallets(3);                      // (all three pallet blocks before the stacks, which crowd the floor: the docks must have their pallet blocks, the PVP room's four are not the only ones)
            int haveStacks = PlaceStacks(10);
            PlaceDrums(2);
'@
Save-Src
Write-Output 'round 12 batch G56 patched'
