# Round 12, batch G52b: the PVP room after the first test run (t49): the turned crate of each stacked pair poked 2 cm into the elevator room's wall (moved 10 cm west and the pairs 0.5 m apart), and the docks lost two
# of their pallet blocks (the old room had four, the new one two, and DockLook / DockSeeds want six in all): two blocks of boxes on pallets stand on the pocket's east side between the crate pairs (the pocket's middle row of
# shelves moved 10 cm west to keep the aisles even).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.DockRooms.cs'
Rep @'
            foreach (float cz in new[] { PvpRoomAnnex.yMin + 1.7f, PvpRoomAnnex.yMax - 1.7f })
            {
                PlaceKey("crate_large", 4.3f, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", 4.3f, cz, 20f, parent, 0.79f, true);          // (a second one on top)
            }
'@ @'
            foreach (float cz in new[] { PvpRoomAnnex.yMin + 2.2f, PvpRoomAnnex.yMax - 2.2f })
            {
                PlaceKey("crate_large", 4.2f, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", 4.2f, cz, 20f, parent, 0.79f, true);          // (a second one on top, turned: its corners stay inside the wall)
            }
            BoxBlock(parent, 4.15f, PvpRoomAnnex.yMin + 0.52f, 0f, 2, 2, 1, true);                  // the pocket's east side: a block of boxes on a pallet in its south-east corner ...
            BoxBlock(parent, 4.15f, PvpRoomAnnex.center.y, 0f, 2, 2, 2, true);                       // ... and a taller one in the middle, between the crate pairs
'@
Rep 'RackColumn(2.4f, PvpRoomAnnex.center.y - 1.83f, PvpRoomAnnex.center.y + 1.83f, 3);' 'RackColumn(2.3f, PvpRoomAnnex.center.y - 1.83f, PvpRoomAnnex.center.y + 1.83f, 3);'
Rep 'stacked crates on the pocket''s east side, loaded pallets and crates on the hall''s east side, two blocks of boxes on pallets by the west line' 'stacked crates and two blocks of boxes on the pocket''s east side, loaded pallets and crates on the hall''s east side, two blocks of boxes on pallets by the west line'
Save-Src
Write-Output 'round 12 batch G52b patched'
