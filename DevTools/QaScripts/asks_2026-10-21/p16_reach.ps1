# "accessible from both sides" means a second person can WALK to the far side: a server in a dead-end aisle has room behind it that nobody can reach past the box. The carry rule now also floods the room's floor from its doorways (and the guards' standing spot)
# with the box in place, and both standing spots must be reached.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

Open-Src "$Tree\Assets\Scripts\Interior\LootPlan.cs"
if ($s.Contains('WalkGrid')) { "LootPlan already patched"; return }

Rep @'
        public static bool RoomForCarry(Layout layout, Slot slot, int lifters)
        {
            if (lifters <= 1) return true;
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = slot.host != null ? 1.15f : 1.0f;
            if (slot.host == null && FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            var room = RoomOf(layout, slot.x, slot.z);
            if (slot.host != null) return SideFree(layout, slot, 1f, 0f, ahead, need, room) || SideFree(layout, slot, 0f, 1f, ahead, need, room) || SideFree(layout, slot, -1f, 0f, ahead, need, room) || SideFree(layout, slot, 0f, -1f, ahead, need, room);
            // on the floor each carrier needs a spot of their own, on opposite sides: room for one person to stand and lift (<see cref="StandRadius"/>), not the room two people need side by side
            float stand = StandRadius(lifters);
            bool east = SideFree(layout, slot, 1f, 0f, ahead, stand, room), north = SideFree(layout, slot, 0f, 1f, ahead, stand, room);
            bool west = SideFree(layout, slot, -1f, 0f, ahead, stand, room), south = SideFree(layout, slot, 0f, -1f, ahead, stand, room);
            return (east && west) || (north && south);
        }
'@ @'
        public static bool RoomForCarry(Layout layout, Slot slot, int lifters, float sizeX = 1f, float sizeZ = 1f) => RoomForCarryCore(layout, slot, lifters, sizeX, sizeZ, null);

        static bool RoomForCarryCore(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, WalkGrid grid)
        {
            if (lifters <= 1) return true;
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = slot.host != null ? 1.15f : 1.0f;
            if (slot.host == null && FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            var room = grid != null ? grid.Room : RoomOf(layout, slot.x, slot.z);
            if (slot.host != null) return SideFree(layout, slot, 1f, 0f, ahead, need, room) || SideFree(layout, slot, 0f, 1f, ahead, need, room) || SideFree(layout, slot, -1f, 0f, ahead, need, room) || SideFree(layout, slot, 0f, -1f, ahead, need, room);
            // on the floor each carrier needs a spot of their own, on opposite sides: room for one person to stand and lift (<see cref="StandRadius"/>), not the room two people need side by side
            float stand = StandRadius(lifters);
            bool east = SideFree(layout, slot, 1f, 0f, ahead, stand, room), north = SideFree(layout, slot, 0f, 1f, ahead, stand, room);
            bool west = SideFree(layout, slot, -1f, 0f, ahead, stand, room), south = SideFree(layout, slot, 0f, -1f, ahead, stand, room);
            bool alongX = east && west, alongZ = north && south;
            if (!alongX && !alongZ) return false;
            // and a second person must be able to WALK to the far side: with the box in place, both spots must be reachable from where people come into the room
            if (room == null) return true;
            if (grid == null) grid = new WalkGrid(layout, room);
            if (!grid.HasSeeds) return true;                                                                  // (no way in is known: nothing to say about it)
            bool yawed = Math.Abs(Math.Sin(slot.yaw * Math.PI / 180.0)) > 0.5;
            float hw = (yawed ? sizeZ : sizeX) * 0.5f, hd = (yawed ? sizeX : sizeZ) * 0.5f;
            var seen = grid.Flood(new Rc(slot.x - hw, slot.z - hd, slot.x + hw, slot.z + hd));
            if (alongX && grid.Reached(seen, slot.x + ahead, slot.z) && grid.Reached(seen, slot.x - ahead, slot.z)) return true;
            if (alongZ && grid.Reached(seen, slot.x, slot.z + ahead) && grid.Reached(seen, slot.x, slot.z - ahead)) return true;
            return false;
        }

        /// <summary>
        /// The floor of one room as a grid of cells a person can stand in (clear of furniture, walls and the room's edge by a person's half width), made once for every spot a treasure is tried at. <see cref="Flood"/> says which cells can be walked to from the
        /// way in (a spot just inside each doorway, and the guards' standing spot of the room, which the planner made sure can be walked to) when a box stands somewhere.
        /// </summary>
        sealed class WalkGrid
        {
            const float Step = 0.25f, Person = 0.3f;
            public readonly RoomInfo Room;
            readonly float x0, z0;
            readonly int nx, nz;
            readonly bool[] blocked;
            readonly List<int> seeds = new List<int>();
            readonly Queue<int> queue = new Queue<int>();
            public bool HasSeeds => seeds.Count > 0;

            public WalkGrid(Layout layout, RoomInfo room)
            {
                Room = room;
                x0 = room.bounds.x0; z0 = room.bounds.z0;
                nx = Math.Max(1, (int)Math.Ceiling(room.bounds.W / Step));
                nz = Math.Max(1, (int)Math.Ceiling(room.bounds.D / Step));
                blocked = new bool[nx * nz];
                var near = new List<Rc>();
                var area = room.bounds.Grow(1.5f);
                foreach (var p in layout.pieces) if (p.solid && !p.onTop && !p.mount && p.h >= 0.35f && p.rc.Overlaps(area)) near.Add(p.rc);
                foreach (var w in layout.walls) if (w.y0 <= 1.0f && w.y1 >= 0.5f && w.rc.Overlaps(area)) near.Add(w.rc);
                foreach (var b in room.blocks) near.Add(b);
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        float cx = x0 + (ix + 0.5f) * Step, cz = z0 + (iz + 0.5f) * Step;
                        bool bl = EdgeClearance(room, cx, cz) < Person;
                        for (int k = 0; !bl && k < near.Count; k++) if (DistanceTo(near[k], cx, cz) < Person) bl = true;
                        blocked[iz * nx + ix] = bl;
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

            void AddSeed(float x, float z) { int c = CellOf(x, z); if (c >= 0 && !blocked[c] && !seeds.Contains(c)) seeds.Add(c); }

            bool BlockedWith(int i, Rc box) =>
                blocked[i] || DistanceTo(box, x0 + (i % nx + 0.5f) * Step, z0 + (i / nx + 0.5f) * Step) < Person;

            /// <summary>The cells that can be walked to from the way in with a box standing at <paramref name="box"/> (a cell is open when a person fits there: clear of everything, the box too, by a person's half width).</summary>
            public bool[] Flood(Rc box)
            {
                var seen = new bool[nx * nz];
                queue.Clear();
                foreach (int s0 in seeds) if (!BlockedWith(s0, box) && !seen[s0]) { seen[s0] = true; queue.Enqueue(s0); }
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue(), cx = c % nx, cz = c / nx;
                    if (cx > 0) Take(c - 1, box, seen);
                    if (cx < nx - 1) Take(c + 1, box, seen);
                    if (cz > 0) Take(c - nx, box, seen);
                    if (cz < nz - 1) Take(c + nx, box, seen);
                }
                return seen;
            }

            void Take(int i, Rc box, bool[] seen)
            {
                if (seen[i] || BlockedWith(i, box)) return;
                seen[i] = true; queue.Enqueue(i);
            }

            /// <summary>Can a person get to this spot (it is an open cell that the flood reached)?</summary>
            public bool Reached(bool[] seen, float x, float z) { int c = CellOf(x, z); return c >= 0 && seen[c]; }
        }
'@

Rep @'
        public static bool TryFindCarrySpot(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, out float x, out float z)
        {
            x = slot.x; z = slot.z;
            if (lifters <= 1 || slot.host != null) return false;
            var room = RoomOf(layout, slot.x, slot.z);
            if (room == null) return false;
'@ @'
        public static bool TryFindCarrySpot(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, out float x, out float z)
        {
            x = slot.x; z = slot.z;
            if (lifters <= 1 || slot.host != null) return false;
            var room = RoomOf(layout, slot.x, slot.z);
            if (room == null) return false;
            var grid = new WalkGrid(layout, room);
'@
Rep '                    if (!RoomForCarry(layout, spot, lifters)) continue;' '                    if (!RoomForCarryCore(layout, spot, lifters, sizeX, sizeZ, grid)) continue;'
Save-Src

Open-Src "$Tree\Assets\Scripts\FloorGenerator.Interior.cs"
if (-not $s.Contains('t.size.x, t.size.z)) {')) {
Rep '                    if (!LootPlan.RoomForCarry(layout, s, t.lifters))
                    {' '                    if (!LootPlan.RoomForCarry(layout, s, t.lifters, t.size.x, t.size.z))
                    {'
Save-Src
}
"p16 done"
