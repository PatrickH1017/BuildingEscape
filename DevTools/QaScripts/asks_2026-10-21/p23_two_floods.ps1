# Two floods: a PERSON (0.3 m clear) must be able to walk to both carrier spots, and the ground within 2 m of the treasure must also be ground an AGENT (0.55 m clear: the floor's NavMesh, the guards) can reach - the LootQa rule "the loot on show can be walked to"
# (NavMesh path from a patrolling guard to NavMesh within 2 m of the item). A treasure only a person squeezes to, in a 1 m aisle with no NavMesh in it, fails that.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
$path = "$Tree\Assets\Scripts\Interior\LootPlan.cs"
$t = [IO.File]::ReadAllText($path)
$eol = if ($t.Contains("`r`n")) { "`r`n" } else { "`n" }
if ($t.Contains('AgentRadius')) { "already"; return }

# --- the reachability block of RoomForCarryCore
$oldStart = $t.IndexOf("            var seen = grid.Flood(box);")
$oldEnd = $t.IndexOf("        /// <summary>Is this box free of solid furniture")
if ($oldStart -lt 0 -or $oldEnd -lt 0) { throw "reachability block not found" }
$newBlock = @'
            var seen = grid.Flood(box, WalkGrid.Person);
            bool reachE = east && grid.Reached(seen, slot.x + ax, slot.z), reachW = west && grid.Reached(seen, slot.x - ax, slot.z);
            bool reachN = north && grid.Reached(seen, slot.x, slot.z + az), reachS = south && grid.Reached(seen, slot.x, slot.z - az);
            bool carriers = lifters >= 3 ? (reachE ? 1 : 0) + (reachN ? 1 : 0) + (reachW ? 1 : 0) + (reachS ? 1 : 0) >= 2 : (reachE && reachW) || (reachN && reachS);
            if (!carriers) return false;
            // and the treasure must lie where the floor's NavMesh is (what the guards and the QA call walkable: an agent keeps 0.55 m clear): open ground that an agent can reach within 2 m of it, as the loot test asks
            return grid.Near(grid.Flood(box, WalkGrid.AgentRadius), slot.x, slot.z, 2.0f);
        }

'@.Replace("`r`n", "`n").Replace("`n", $eol)
$t = $t.Substring(0, $oldStart) + $newBlock + $t.Substring($oldEnd)

# --- the grid: clearance per cell, one flood per radius
$gs = $t.IndexOf("        sealed class WalkGrid")
$ge = $t.IndexOf("        /// <summary>The smallest room whose bounds hold this point")
if ($gs -lt 0 -or $ge -lt 0) { throw "grid not found" }
$docStart = $t.LastIndexOf("        /// <summary>", $gs)
$gridNew = @'
        /// <summary>
        /// The floor of one room as a grid of cells with the clearance of each (how far it is from furniture, walls and the room's edge, up to 1 m), made once for every spot a treasure is tried at. <see cref="Flood"/> says which cells can be walked to from the way in
        /// (a spot just inside each doorway, and the guards' standing spot of the room, which the planner made sure can be walked to) by something of a given radius - a person (<see cref="Person"/>) or an agent of the floor's NavMesh (<see cref="AgentRadius"/>) - when a box stands somewhere.
        /// </summary>
        sealed class WalkGrid
        {
            public const float Person = 0.3f, AgentRadius = 0.55f;
            const float Step = 0.25f;
            public readonly RoomInfo Room;
            readonly float x0, z0;
            readonly int nx, nz;
            readonly float[] clear;
            readonly List<int> seeds = new List<int>();
            readonly Queue<int> queue = new Queue<int>();
            public bool HasSeeds => seeds.Count > 0;

            public WalkGrid(Layout layout, RoomInfo room)
            {
                Room = room;
                x0 = room.bounds.x0; z0 = room.bounds.z0;
                nx = Math.Max(1, (int)Math.Ceiling(room.bounds.W / Step));
                nz = Math.Max(1, (int)Math.Ceiling(room.bounds.D / Step));
                clear = new float[nx * nz];
                var near = new List<Rc>();
                var area = room.bounds.Grow(1.5f);
                foreach (var p in layout.pieces) if (p.solid && !p.onTop && !p.mount && p.h >= 0.35f && p.rc.Overlaps(area)) near.Add(p.rc);
                foreach (var w in layout.walls) if (w.y0 <= 1.0f && w.y1 >= 0.5f && w.rc.Overlaps(area)) near.Add(w.rc);
                foreach (var b in room.blocks) near.Add(b);
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        float cx = x0 + (ix + 0.5f) * Step, cz = z0 + (iz + 0.5f) * Step;
                        float c = Math.Min(1f, EdgeClearance(room, cx, cz));
                        for (int k = 0; k < near.Count && c > 0f; k++) c = Math.Min(c, DistanceTo(near[k], cx, cz));
                        clear[iz * nx + ix] = c;
                    }
                foreach (var d in room.doors)
                {
                    float dx = d.Cx, dz = d.Cz;
                    float ex = Math.Min(dx - room.bounds.x0, room.bounds.x1 - dx), ez = Math.Min(dz - room.bounds.z0, room.bounds.z1 - dz);
                    if (ez <= ex) dz += dz - room.bounds.z0 < room.bounds.z1 - dz ? 0.6f : -0.6f; else dx += dx - room.bounds.x0 < room.bounds.x1 - dx ? 0.6f : -0.6f;
                    AddSeed(dx, dz);
                }
                if (room.bounds.Contains(room.wx, room.wz)) AddSeed(room.wx, room.wz);
            }

            int CellOf(float x, float z)
            {
                int ix = (int)Math.Floor((x - x0) / Step), iz = (int)Math.Floor((z - z0) / Step);
                return ix < 0 || iz < 0 || ix >= nx || iz >= nz ? -1 : iz * nx + ix;
            }

            void AddSeed(float x, float z) { int c = CellOf(x, z); if (c >= 0 && clear[c] >= Person && !seeds.Contains(c)) seeds.Add(c); }

            bool Open(int i, Rc box, float radius) =>
                clear[i] >= radius && DistanceTo(box, x0 + (i % nx + 0.5f) * Step, z0 + (i / nx + 0.5f) * Step) >= radius;

            /// <summary>The cells that something of <paramref name="radius"/> can walk to from the way in with a box standing at <paramref name="box"/> (a cell is open when it is that clear of everything, the box too).</summary>
            public bool[] Flood(Rc box, float radius)
            {
                var seen = new bool[nx * nz];
                queue.Clear();
                foreach (int s0 in seeds) if (Open(s0, box, radius) && !seen[s0]) { seen[s0] = true; queue.Enqueue(s0); }
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue(), cx = c % nx, cz = c / nx;
                    if (cx > 0) Take(c - 1, box, radius, seen);
                    if (cx < nx - 1) Take(c + 1, box, radius, seen);
                    if (cz > 0) Take(c - nx, box, radius, seen);
                    if (cz < nz - 1) Take(c + nx, box, radius, seen);
                }
                return seen;
            }

            void Take(int i, Rc box, float radius, bool[] seen)
            {
                if (seen[i] || !Open(i, box, radius)) return;
                seen[i] = true; queue.Enqueue(i);
            }

            /// <summary>Can a person get to this spot (it is an open cell that the flood reached)?</summary>
            public bool Reached(bool[] seen, float x, float z) { int c = CellOf(x, z); return c >= 0 && seen[c]; }

            /// <summary>Has the flood reached ground within <paramref name="within"/> metres of this point?</summary>
            public bool Near(bool[] seen, float x, float z, float within)
            {
                int r = (int)Math.Ceiling(within / Step);
                int cx = (int)Math.Floor((x - x0) / Step), cz = (int)Math.Floor((z - z0) / Step);
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int ix = cx + dx, iz = cz + dz;
                        if (ix < 0 || iz < 0 || ix >= nx || iz >= nz || !seen[iz * nx + ix]) continue;
                        float ex = x0 + (ix + 0.5f) * Step - x, ez = z0 + (iz + 0.5f) * Step - z;
                        if (ex * ex + ez * ez <= within * within) return true;
                    }
                return false;
            }
        }

'@.Replace("`r`n", "`n").Replace("`n", $eol)
$t = $t.Substring(0, $docStart) + $gridNew + $t.Substring($ge)
[IO.File]::WriteAllText($path, $t, (New-Object Text.UTF8Encoding($false)))
"p23 done"
