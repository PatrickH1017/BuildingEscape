# Round 12, batch G37 (the owner, 2026-10-20, after seeing the shipped build `Playtest_2026-10-19j`): "The PvP room should be reverted this current one is not good".
# The PVP room goes back to the room it was before round 12 (the break room's twin: 9 x 4.6 m in the north-west corner, a doorway in its east wall at x 9.5, a red mat and four blocks of boxes):
#  - `FloorGenerator.DockRooms.cs` and `Pvp.cs` are put back exactly as they were before the round (DevTools\QaBackups\before_round12_apply_2026-10-19; they differ from the current ones only by the warehouse: the annex, the east block, the
#    containers / crates / pallets / barrels, the lights, `PvpRoomAnnex` / `PvpRoomBlock` and the three-part `Inside`);
#  - `FloorGenerator.BuildShell`: the wall between the two halves of the docks has its north half again;
#  - `FloorGenerator.Dock.cs` (ScatterStorage): the keep-clear rectangles of the PVP room are the old one (0, 12.7)-(9.9, 18), the east block's is gone, and the north stub of the dividing wall is a keep-clear rectangle like the south one;
#  - the Round12 scenario checks the small room instead of the warehouse; README item 20 says so.
# The warehouse version stays recoverable: `DevTools\QaBackups\before_round12c_apply_2026-10-20` (made by the ship) holds the current DockRooms.cs and Pvp.cs, and the patches r12_g20 / g23 / g24 are archived.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'
$B = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaBackups\before_round12_apply_2026-10-19\Assets\Scripts'

foreach ($f in 'FloorGenerator.DockRooms.cs', 'Pvp.cs') {
    if (-not (Test-Path "$B\$f")) { throw "the backup of $f is missing: $B\$f" }
    if ($env:R2_DRY) { Write-Output "dry run: would restore $f from the backup" }
    else { Copy-Item "$B\$f" "$A\$f" -Force; Write-Output "restored $f" }
}

Open-Src "$A\FloorGenerator.cs"
RepRx 'if \(!\(dockWalls && c == 1\)\) ' ''
RepRx '[ \t]*// \(the PVP warehouse runs across the line between the two cells of the docks: no wall of the north half there\)' ''
Save-Src

Open-Src "$A\FloorGenerator.Dock.cs"
RepRx '(?m)^[ \t]*new Vector4\(0f, 12\.7f, 24\.2f, 18f\),[^\r\n]*' @'
                new Vector4(0f, 12.7f, 9.9f, 18f),  // the PVP room
'@
RepRx '(?m)^[ \t]*new Vector4\(12\.9f, 10\.5f, 24\.2f, 12\.9f\),[^\r\n]*\r?\n' ''
Rep 'new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),' @'
new Vector4(cellSize - 0.2f, depth - (cellSize - doorWidth) / 2f, cellSize + 0.2f, depth),            // (the north stub of the same wall)
                new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),
'@
Save-Src

Open-Src "$E\NetDevRound12.cs"
RepRegion '// ---- the PVP room is a warehouse now (the owner, 2026-10-19 night)' '!PvpRules.Inside(blockRect, 18f, 9f) && !PvpRules.Inside(hallRect, 18f, 9f));' @'
// ---- the PVP room is the small room it was again (the owner, 2026-10-20: "The PvP room should be reverted this current one is not good"): one room of about 9 x 4.6 m in the north-west corner, the break room's twin, and the wall between the two halves of the docks whole again
            {
                var room = gen.PvpRoomArea;
                R12Check("the PVP room is the small room it was: one room of about 9 x 4.6 m (42 m2) in the north-west corner, its doorway in the east wall at x 9.5",
                    room.xMin < 0.5f && room.yMin > 13f && room.yMax > 17f && room.width > 8.5f && room.width < 9.6f && room.height > 4.2f && room.height < 5f && Mathf.Abs(gen.PvpRoomDoor.x - 9.5f) < 0.1f && Mathf.Abs(gen.PvpRoomDoor.z - 15.6f) < 0.1f,
                    $"(room {room}, doorway {gen.PvpRoomDoor})");
                var cross = new Vector3(18.0f, 0f, 15.0f);
                R12Check("the wall between the two halves of the docks is whole again: its north half stands (the warehouse that cut through it is gone)", Physics.CheckCapsule(cross + Vector3.up * 0.5f, cross + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore));
                R12Check("no shipping container stands in the docks any more", !FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t && (t.name == "container" || t.name == "container_small")));
'@
RepRx '(?m)^[ \t]*cam\.transform\.position = new Vector3\(3\.5f, 1\.8f, gen\.PvpRoomAnnex\.yMin \+ 0\.8f\);\r?\n[ \t]*cam\.transform\.LookAt\(new Vector3\(3\.0f, 1\.2f, gen\.PvpRoomAnnex\.yMax - 0\.5f\)\);\r?\n[ \t]*ShotCam\(cam, "round12_pvp_annex"\);\r?\n' ''
Save-Src

Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep 'The PVP room is a big warehouse now, with shipping containers and crates to hide behind. A dart board hangs' 'The PVP room is the small room it was again (the warehouse version is taken out). A dart board hangs'
Save-Src
Write-Output 'round 12 batch G37 patched'
