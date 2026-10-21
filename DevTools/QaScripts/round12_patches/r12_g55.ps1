# Round 12, batch G55: nothing may block the way into the PVP room (the owner, 2026-10-20: "The Forklift should not be blocking the PvP room doorway" / "players need to be able to get into the PvP room" / "Implement that before the build").
# Generator: the keep-clear plaza in front of the PVP doorway is 6.4 x 4.8 m (it was 4.6 x 4.8) and a lane 2.7 m wide runs from it down the elevator room's east wall to the way through to the bay, so a forklift fits all the way
# from the elevator's door to the doorway; no random forklift, drum, stack, block or shelf can stand in it (Free() checks every placement against these rectangles). Tests: a walk-through (flood fill on the floor plan with
# a capsule at every 0.2 m cell) in DockSeeds over 24 seeds and in Round12: from just outside the elevator's door a player (0.8 m across) reaches EVERY free spot of the PVP room and a forklift-sized body (1.3 m across, 2.3 m high)
# reaches the doorway and gets through it. The pictures that stood the lobby's forklift IN the doorway (a test prop, never part of the game) are gone: a picture of the clear way in from the elevator's door replaces them.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

# ---------------------------------------------------------------- the generator
Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
RepRx 'new Vector4\(PvpRoomDoor\.x, PvpRoomDoor\.z - 2\.4f, PvpRoomDoor\.x \+ 4\.6f, PvpRoomDoor\.z \+ 2\.4f\),[^\r\n]*' @'
new Vector4(PvpRoomDoor.x, PvpRoomDoor.z - 2.4f, PvpRoomDoor.x + 6.4f, PvpRoomDoor.z + 2.4f),            // in front of the PVP room's doorway, a plaza of 6.4 x 4.8 m (the owner, 2026-10-19 night: a forklift stood across the doorway; 2026-10-20: "the Forklift should not be blocking the PvP room doorway ... players need to be able to get into the PvP room")
                new Vector4(elev.x + 4.2f, elev.z + 3.0f, elev.x + 6.9f, depth),                                          // and a lane 2.7 m wide from it down the elevator room's east wall to the way through to the bay: a forklift fits all the way from the elevator's door to the PVP room's doorway
'@
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevDockSeeds.cs'
Rep 'bool ok = lifts >= 1 && drums >= 2 && blocks >= 6 && stacks >= 10 && shelves >= 4 && way.Length == 0 && truck.Length == 0 && dummy.Length == 0 && hall.Count == 0;' @'
string wayIn = PvpWayIn(gen);                                                                    // (the owner, 2026-10-20: players must be able to get into the PVP room, whatever the seed)
                bool ok = lifts >= 1 && drums >= 2 && blocks >= 6 && stacks >= 10 && shelves >= 4 && way.Length == 0 && truck.Length == 0 && dummy.Length == 0 && hall.Count == 0 && wayIn.Length == 0;
'@
Rep '(ok ? "" : $"; way [{way}] truck [{truck}] dummy [{dummy}] hallway [{string.Join(",", hall)}]")' '(ok ? "" : $"; way [{way}] truck [{truck}] dummy [{dummy}] hallway [{string.Join(",", hall)}] PVP room [{wayIn}]")'
Rep @'
            Quit(fails == 0 ? 0 : 1);
        }
'@ @'
            Quit(fails == 0 ? 0 : 1);
        }

        const float GridStep = 0.2f;

        /// <summary>The docks' floor plan (x 0..36, z 0..24.4: the hall and the PVP room's north part) as a grid of 0.2 m cells: a cell is free when a capsule of this radius, from the floor up to <paramref name="height"/>, stands in it clear of everything.</summary>
        static bool[,] FreeGrid(float radius, float height)
        {
            int nx = (int)(36f / GridStep), nz = (int)(24.4f / GridStep);
            var free = new bool[nx, nz];
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Vector3((i + 0.5f) * GridStep, 0f, (j + 0.5f) * GridStep);
                    free[i, j] = !Physics.CheckCapsule(p + Vector3.up * (0.05f + radius), p + Vector3.up * Mathf.Max(0.05f + radius, height - radius), radius, ~0, QueryTriggerInteraction.Ignore);
                }
            return free;
        }

        /// <summary>The cells that can be walked to from <paramref name="start"/> (steps between free cells that share an edge).</summary>
        static bool[,] Flood(bool[,] free, Vector3 start)
        {
            int nx = free.GetLength(0), nz = free.GetLength(1);
            var seen = new bool[nx, nz];
            int si = Mathf.Clamp((int)(start.x / GridStep), 0, nx - 1), sj = Mathf.Clamp((int)(start.z / GridStep), 0, nz - 1);
            if (!free[si, sj]) return seen;
            var queue = new Queue<(int i, int j)>();
            queue.Enqueue((si, sj)); seen[si, sj] = true;
            int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
            while (queue.Count > 0)
            {
                var (ci, cj) = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int ni = ci + di[k], nj = cj + dj[k];
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || seen[ni, nj] || !free[ni, nj]) continue;
                    seen[ni, nj] = true; queue.Enqueue((ni, nj));
                }
            }
            return seen;
        }

        /// <summary>
        /// Can players get into the PVP room (the owner, 2026-10-20: "the Forklift should not be blocking the PvP room doorway ... players need to be able to get into the PvP room")? From just outside the elevator's door a player (a capsule 0.8 m across, 1.8 m high)
        /// has to reach EVERY free spot of the PVP room (the hall, its north part and the pocket), and a forklift-sized body (1.3 m across, 2.3 m high) the doorway and the first metres inside it. Returns "" when all is well, else what is wrong.
        /// </summary>
        static string PvpWayIn(FloorGenerator gen)
        {
            var problems = new List<string>();
            var start = new Vector3(14.2f, 0f, 9.0f);                                      // just outside the elevator's east door, in the way through to the bay
            var room = gen.PvpRoomArea; var annex = gen.PvpRoomAnnex;
            Physics.SyncTransforms();
            var free = FreeGrid(0.4f, 1.8f);
            if (!free[(int)(start.x / GridStep), (int)(start.z / GridStep)]) problems.Add("the spot outside the elevator's door is blocked");
            var reach = Flood(free, start);
            int total = 0, cut = 0; string firstCut = "";
            for (int i = 0; i < free.GetLength(0); i++)
                for (int j = 0; j < free.GetLength(1); j++)
                {
                    var p = new Vector2((i + 0.5f) * GridStep, (j + 0.5f) * GridStep);
                    if (!free[i, j] || !(room.Contains(p) || annex.Contains(p))) continue;
                    total++;
                    if (!reach[i, j]) { cut++; if (firstCut.Length == 0) firstCut = $"({p.x:0.0}, {p.y:0.0})"; }
                }
            if (total < 300) problems.Add($"only {total} free cells in the PVP room");
            if (cut > 0) problems.Add($"{cut} of {total} free spots of the PVP room cannot be walked to from the elevator's door (the first at {firstCut})");
            var wide = FreeGrid(0.65f, 2.3f);
            var wideReach = Flood(wide, start);
            foreach (var (label, x) in new[] { ("the plaza outside the doorway", gen.PvpRoomDoor.x + 2.5f), ("the doorway", gen.PvpRoomDoor.x), ("just inside the doorway", gen.PvpRoomDoor.x - 1.8f) })
                if (!wideReach[(int)(x / GridStep), (int)(gen.PvpRoomDoor.z / GridStep)]) problems.Add($"a forklift cannot get to {label}");
            return string.Join("; ", problems);
        }
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep 'if (b.min.x < door.x + 4.6f && b.max.x > door.x && b.min.z < door.z + 2.4f && b.max.z > door.z - 2.4f) liftOk = false;' 'if (b.min.x < door.x + (door == gen.PvpRoomDoor ? 6.4f : 4.6f) && b.max.x > door.x && b.min.z < door.z + 2.4f && b.max.z > door.z - 2.4f) liftOk = false;'
Rep 'no forklift parks within 4.6 m of the {label}''s doorway' 'no forklift parks in front of the {label}''s doorway (within 4.6 m of it, 6.4 m for the PVP room''s)'
Rep @'
                    && DoorPasses(gen.BreakRoomDoor, 1.5f, 2.5f) && !DoorPasses(gen.BreakRoomDoor, 1.7f, 2.5f), $"(the forklift is {pvpForkBox.size.x:0.00} x {pvpForkBox.size.y:0.00} m)");
'@ @'
                    && DoorPasses(gen.BreakRoomDoor, 1.5f, 2.5f) && !DoorPasses(gen.BreakRoomDoor, 1.7f, 2.5f), $"(the forklift is {pvpForkBox.size.x:0.00} x {pvpForkBox.size.y:0.00} m)");
                // nothing blocks the way in (the owner, 2026-10-20: "the Forklift should not be blocking the PvP room doorway ... players need to be able to get into the PvP room"): a walk-through on the floor plan
                string pvpWayIn = PvpWayIn(gen);
                R12Check("players can walk from just outside the elevator's door to EVERY free spot of the PVP room, and a forklift-sized body (1.3 x 2.3 m) to its doorway and through it: nothing blocks the way in", pvpWayIn.Length == 0, $"({pvpWayIn})");
'@
RepRegion '                    // the doorway with the lobby''s forklift standing in it (it goes through: the owner, 2026-10-20), from the docks and from inside the room' '                    Destroy(forkPic.gameObject);' @'
                    // the way in (the owner, 2026-10-20: "players need to be able to get into the PvP room"): from the elevator's door up the clear lane to the plaza in front of the doorway
                    var elevOut = gen.CellCentre(Vector2Int.zero) + new Vector3(5.2f, 0f, 0f);
                    cam.fieldOfView = 70f;
                    cam.transform.position = new Vector3(elevOut.x, 1.7f, elevOut.z);
                    cam.transform.LookAt(new Vector3(elevOut.x, 1.3f, pd.z));
                    ShotCam(cam, "round12_pvp_way_in");
'@
Save-Src
Write-Output 'round 12 batch G55 patched'
