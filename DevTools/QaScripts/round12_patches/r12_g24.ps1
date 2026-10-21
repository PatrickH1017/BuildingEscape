# Round 12, batch G24: after G23 (the big PVP warehouse) the docks had too few pallet blocks for the dock tests (DockLook 3 of 6, Sneak 13 of 14 cardboard pieces): a warehouse has boxes on pallets, so three more blocks stand in the PVP room
# (north wall of the hall, the east part's north wall, the hall's south side); and the test of the open middle used a capsule of 1 m radius that reached into the floor (it starts at 1.1 m now).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.DockRooms.cs'
Rep @'
            // the east part of the warehouse: a full container along the north wall, a small one standing across the joint with the deeper part, loaded pallets and stacked crates in the south, the lane to the doorway open
'@ @'
            // boxes on pallets, as a warehouse has them
            BoxBlock(parent, 10.0f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 20.9f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 1, true);
            BoxBlock(parent, 4.5f, PvpRoomArea.yMin + 0.75f, 0f, 1, 1, 3, false);
            // the east part of the warehouse: a full container along the north wall, a small one standing across the joint with the deeper part, loaded pallets and stacked crates in the south, the lane to the doorway open
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep 'bool middleOpen = !Physics.CheckCapsule(middle + Vector3.up * 0.5f, middle + Vector3.up * 1.6f, 1.0f, ~0, QueryTriggerInteraction.Ignore);' 'bool middleOpen = !Physics.CheckCapsule(middle + Vector3.up * 1.1f, middle + Vector3.up * 1.7f, 1.0f, ~0, QueryTriggerInteraction.Ignore);          // (a capsule of 1 m radius: it starts above 1 m or it would reach into the floor)'
Save-Src
Write-Output 'round 12 batch G24 patched'
