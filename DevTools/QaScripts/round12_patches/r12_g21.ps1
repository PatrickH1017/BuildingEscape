# Round 12, batch G21: the first run of G17..G20 showed (a) the path 2.8 m wide across the whole docks that G17 kept clear (my reading of "as if the workers cleared the path for darts") took so much of the walls and the floor that the docks
# had one shelf and no drums left: the owner's words are about the floor between the player (who stands at the tape line, 2.37 m from the board) and the board, which the 5.2 m x 4.8 m zone in front of the board covers, so the long path is
# taken out again; (b) the drums were placed last and found no room: forklifts, pallet blocks, drums, then the box stacks; (c) the test of the clean mesh counted the tape's and the scoreboard's meshes under the board.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
Rep @'
                new Vector4(BreakRoomDoor.x, BreakRoomDoor.z - 1.4f, width, BreakRoomDoor.z + 1.4f),                       // and the path from the break room's doorway to the board, as if the workers had cleared it for darts: no boxes, not even on pallets
'@ @'
'@
Rep @'
            // drums in twos and threes
            for (int tries = 0, groups = 3; tries < 600 && groups > 0; tries++)
            {
                float x = F(1f, width - 1f), z = F(1f, depth - 1f);
                if (!Free(x, z, 0.95f)) continue;
                int n = 2 + rng.Next(2);
                for (int k = 0; k < n; k++) DockDressing.Drum(dockHolder, x + (k % 2) * 0.62f, z + (k / 2) * 0.62f, rng.Next(2));
                placed.Add(new Vector3(x + 0.3f, z + 0.3f, 0.9f));
                groups--; count += n;
            }
            return count;
'@ @'
            return count;
'@
Rep @'
            // cardboard stacks of one to three boxes, square to the room and right on top of each other (the owner, 2026-10-18: "The boxes in the basement should not be floating randomly they should be placed nicely")
'@ @'
            // drums in twos and threes
            for (int tries = 0, groups = 3; tries < 600 && groups > 0; tries++)
            {
                float x = F(1f, width - 1f), z = F(1f, depth - 1f);
                if (!Free(x, z, 0.95f)) continue;
                int n = 2 + rng.Next(2);
                for (int k = 0; k < n; k++) DockDressing.Drum(dockHolder, x + (k % 2) * 0.62f, z + (k / 2) * 0.62f, rng.Next(2));
                placed.Add(new Vector3(x + 0.3f, z + 0.3f, 0.9f));
                groups--; count += n;
            }

            // cardboard stacks of one to three boxes, square to the room and right on top of each other (the owner, 2026-10-18: "The boxes in the basement should not be floating randomly they should be placed nicely")
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep @'
                    bool onPath = pb.max.x > gen.BreakRoomDoor.x && pb.min.x < board.FaceCentre.x + 0.4f && pb.max.z > gen.BreakRoomDoor.z - 1.4f && pb.min.z < gen.BreakRoomDoor.z + 1.4f;
                    if (pb.min.y < 2.2f && (inFront || onPath)) laneProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
'@ @'
                    if (pb.min.y < 2.2f && inFront) laneProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
'@
Rep 'R12Check("nothing stands on the floor between the break room''s doorway and the dart board: the path across the docks and the 4.6 m (15 ft) in front of the board are clear of boxes, pallets, shelves, forklifts and drums"' 'R12Check("nothing stands on the floor in front of the dart board: the 4.6 m (15 ft) between the player and the board and 2.4 m to each side are clear of boxes, pallets, shelves, forklifts and drums"'
Rep 'boardMeshes.Count == 1 && boardMeshes[0].name == FloorGenerator.DartBoardCleanMesh && boardMeshes[0].GetIndexCount(0) / 3 == 160 && boardMeshes[0].bounds.size.z < 0.05f,' 'boardMeshes.Count(mm => mm.name == FloorGenerator.DartBoardCleanMesh) == 1 && !boardMeshes.Any(mm => mm.name == "SM_Prop_Darkboard_01") && boardMeshes.First(mm => mm.name == FloorGenerator.DartBoardCleanMesh).GetIndexCount(0) / 3 == 160 && boardMeshes.First(mm => mm.name == FloorGenerator.DartBoardCleanMesh).bounds.size.z < 0.05f,'
Save-Src
Write-Output 'round 12 batch G21 patched'
