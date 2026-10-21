# Round 12, batch G16: (1) the forklifts are placed FIRST in the docks (largest first: forklifts, pallet blocks, box stacks, drums), the G14 order left no room for them (DockLook: 0 forklifts);
# (2) the owner, 2026-10-19 night: "Boxes shouldn't be on the floor in-front of the dart board. It should be clear between the player and the board which should be a few feet": the keep-clear rectangle in front of the board is 4.6 m
# (15 ft) deep and 4.8 m wide (it was 3.6 x 4.0), and the Round12 scenario measures the lane.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
Rep @'
            // two forklifts, and drums in twos and threes
            for (int tries = 0, lifts = 2; tries < 600 && lifts > 0; tries++)
            {
                float x = F(1.6f, width - 1.6f), z = F(1.6f, depth - 1.6f);
                if (!Free(x, z, 1.35f)) continue;
                DockDressing.Forklift(dockHolder, x, z, F(0f, 360f));
                placed.Add(new Vector3(x, z, 1.35f));
                lifts--; count++;
            }
            for (int tries = 0, groups = 3; tries < 600 && groups > 0; tries++)
'@ @'
            // drums in twos and threes
            for (int tries = 0, groups = 3; tries < 600 && groups > 0; tries++)
'@
Rep @'
            // a few pallets with their boxes squared on them (first: they are the big ones, the stacks fill what is left)
'@ @'
            // two forklifts (first of all: they are the biggest, the pallets, the stacks and the drums fill what is left)
            for (int tries = 0, lifts = 2; tries < 600 && lifts > 0; tries++)
            {
                float x = F(1.6f, width - 1.6f), z = F(1.6f, depth - 1.6f);
                if (!Free(x, z, 1.35f)) continue;
                DockDressing.Forklift(dockHolder, x, z, F(0f, 360f));
                placed.Add(new Vector3(x, z, 1.35f));
                lifts--; count++;
            }

            // a few pallets with their boxes squared on them (then these: they are big, the stacks fill what is left)
'@
Rep @'
                new Vector4(width - 3.6f, BreakRoomDoor.z - 2.0f, width, BreakRoomDoor.z + 2.0f),                          // the dart board on the east wall and the room to throw from
'@ @'
                new Vector4(width - 4.6f, BreakRoomDoor.z - 2.4f, width, BreakRoomDoor.z + 2.4f),                          // the dart board on the east wall and the room to throw from: 4.6 m (15 ft) clear in front of it (the owner, 2026-10-19 night)
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep @'
                float width = gen.CellColumns * 18f;
'@ @'
                // the lane in front of the board is clear (the owner, 2026-10-19 night: "Boxes shouldn't be on the floor in-front of the dart board. It should be clear between the player and the board which should be a few feet")
                var laneProps = new List<string>();
                foreach (var pt in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Shelf" && pt.name != "StockShelf" && pt.name != "Forklift" && pt.name != "Drum" && pt.name != "Bollard") continue;
                    var prs = pt.GetComponentsInChildren<Renderer>();
                    if (prs.Length == 0) continue;
                    var pb = prs[0].bounds; foreach (var pr in prs) pb.Encapsulate(pr.bounds);
                    if (pb.min.y < 2.2f && pb.max.x > board.FaceCentre.x - 4.6f && pb.min.x < board.FaceCentre.x + 0.4f && pb.max.z > board.FaceCentre.z - 2.4f && pb.min.z < board.FaceCentre.z + 2.4f) laneProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
                }
                R12Check("nothing stands on the floor in front of the dart board: the 4.6 m (15 ft) in front of it and 2.4 m to each side are clear of boxes, pallets, shelves, forklifts and drums", laneProps.Count == 0, $"(in the way: {string.Join("; ", laneProps)})");
                float width = gen.CellColumns * 18f;
'@
Save-Src
Write-Output 'round 12 batch G16 patched'
