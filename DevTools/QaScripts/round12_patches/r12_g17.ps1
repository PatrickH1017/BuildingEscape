# Round 12, batch G17 (the owner, 2026-10-19 night, twice: "Boxes shouldn't be on the floor in-front of the dart board. It should be clear between the player and the board" and "There shouldn't be any boxes in between the board and
# the player not even on palettes (it should be as if the workers cleared the path for darts)"): the clear floor in front of the board is 5.2 m deep and 4.8 m wide (G16 made it 4.6 m: a stack just outside it still reached into
# the test's lane), and a path 2.8 m wide runs clear from the break room's doorway across the docks to the board.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
Rep @'
                new Vector4(width - 4.6f, BreakRoomDoor.z - 2.4f, width, BreakRoomDoor.z + 2.4f),                          // the dart board on the east wall and the room to throw from: 4.6 m (15 ft) clear in front of it (the owner, 2026-10-19 night)
'@ @'
                new Vector4(width - 5.2f, BreakRoomDoor.z - 2.4f, width, BreakRoomDoor.z + 2.4f),                          // the dart board on the east wall and the room to throw from: 5 m (16 ft) clear in front of it (the owner, 2026-10-19 night)
                new Vector4(BreakRoomDoor.x, BreakRoomDoor.z - 1.4f, width, BreakRoomDoor.z + 1.4f),                       // and the path from the break room's doorway to the board, as if the workers had cleared it for darts: no boxes, not even on pallets
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep @'
                    if (pb.min.y < 2.2f && pb.max.x > board.FaceCentre.x - 4.6f && pb.min.x < board.FaceCentre.x + 0.4f && pb.max.z > board.FaceCentre.z - 2.4f && pb.min.z < board.FaceCentre.z + 2.4f) laneProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
'@ @'
                    bool inFront = pb.max.x > board.FaceCentre.x - 4.6f && pb.min.x < board.FaceCentre.x + 0.4f && pb.max.z > board.FaceCentre.z - 2.4f && pb.min.z < board.FaceCentre.z + 2.4f;
                    bool onPath = pb.max.x > gen.BreakRoomDoor.x && pb.min.x < board.FaceCentre.x + 0.4f && pb.max.z > gen.BreakRoomDoor.z - 1.4f && pb.min.z < gen.BreakRoomDoor.z + 1.4f;
                    if (pb.min.y < 2.2f && (inFront || onPath)) laneProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
'@
Rep 'R12Check("nothing stands on the floor in front of the dart board: the 4.6 m (15 ft) in front of it and 2.4 m to each side are clear of boxes, pallets, shelves, forklifts and drums"' 'R12Check("nothing stands on the floor between the break room''s doorway and the dart board: the path across the docks and the 4.6 m (15 ft) in front of the board are clear of boxes, pallets, shelves, forklifts and drums"'
Save-Src
Write-Output 'round 12 batch G17 patched'
