using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// How much there is to steal on a floor and where it lies. Plain data and arithmetic (no Unity types), so it can be tested outside Unity.
    /// The small valuables (<see cref="Valuables"/>, <see cref="ValuablePlan"/>) lie on desks and tables, one in as many numbered rooms as possible first, so that
    /// opening a door has a fair chance of showing something, and only then more, at most <see cref="CellCap"/> in a cell (<see cref="Pick"/>). The few big
    /// treasures (a stapler, a trophy, an espresso machine, a server, a safe) stand on the planner's treasure spots - the ones that need two or three people to
    /// carry only where there is room to carry them (<see cref="RoomForCarry"/>). Furniture is never loot (the owner, 2026-10-17).
    /// </summary>
    public static class LootPlan
    {
        /// <summary>The most stealable things picked in one 18 m cell.</summary>
        public const int CellCap = 4;

        /// <summary>How many big treasures (a stapler, a trophy, a server...) a floor puts on the planner's treasure spots: 11 on floor 1, 22 on floor 12 (fewer if the heavy ones have no room).</summary>
        public static int TreasureCount(int floor) => 10 + Math.Max(1, floor);

        /// <summary>
        /// Chooses which of the candidates (in the order given, already shuffled) become loot: first one per group (a room), then the rest,
        /// never more than <paramref name="cellCap"/> from one cell and never more than <paramref name="target"/> in all. Returns the chosen flags.
        /// </summary>
        public static bool[] Pick(int count, int[] group, int[] cell, int target, int cellCap)
        {
            var picked = new bool[count];
            var inCell = new Dictionary<int, int>();
            var seenGroup = new HashSet<int>();
            int got = 0;
            for (int pass = 0; pass < 2 && got < target; pass++)
            {
                for (int i = 0; i < count && got < target; i++)
                {
                    if (picked[i]) continue;
                    if (pass == 0 && seenGroup.Contains(group[i])) continue;
                    inCell.TryGetValue(cell[i], out int n);
                    if (n >= cellCap) continue;
                    inCell[cell[i]] = n + 1;
                    seenGroup.Add(group[i]);
                    picked[i] = true;
                    got++;
                }
            }
            return picked;
        }

        /// <summary>The number of the room (in <see cref="Layout.rooms"/>) that a point is in, counting only rooms with a door; -1 when it is in none.</summary>
        public static int RoomIndexAt(Layout layout, float x, float z)
        {
            for (int i = 0; i < layout.rooms.Count; i++)
            {
                var r = layout.rooms[i];
                if (r.kind == 0 && r.doors.Count > 0 && r.bounds.Contains(x, z)) return i;
            }
            return -1;
        }

        /// <summary>
        /// The room (of any kind: an office, an open-plan area, a hallway, a lobby) that a point is in: the smallest one that holds it (the guard outpost lies inside its
        /// cell's lobby). A point in a doorway - just outside every room - belongs to the smallest room within <paramref name="slack"/> metres. -1 when there is none.
        /// The loot scanner uses it to tell which loot is in the room the player stands in.
        /// </summary>
        public static int RoomAround(Layout layout, float x, float z, float slack = 0.6f)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                float grow = pass == 0 ? 0f : slack, area = float.MaxValue;
                int best = -1;
                for (int i = 0; i < layout.rooms.Count; i++)
                {
                    var b = layout.rooms[i].bounds.Grow(grow);
                    if (!b.Contains(x, z)) continue;
                    float a = b.W * b.D;
                    if (a < area) { area = a; best = i; }
                }
                if (best >= 0) return best;
            }
            return -1;
        }

        // ------------------------------------------------------------------ room to carry a heavy treasure

        /// <summary>How far a point is from a rectangle (0 inside it).</summary>
        static float DistanceTo(Rc r, float x, float z)
        {
            float dx = Math.Max(Math.Max(r.x0 - x, 0f), x - r.x1), dz = Math.Max(Math.Max(r.z0 - z, 0f), z - r.z1);
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// The free room round a point: how far, up to <paramref name="max"/> metres, a disc round (x, z) can grow before it touches solid furniture (taller than a
        /// knee; <paramref name="ignore"/> is left out - the counter a thing stands on) or a partition wall (the beams over doorways are no wall to a person).
        /// </summary>
        public static float FreeRadius(Layout layout, float x, float z, float max, Piece ignore = null)
        {
            float best = max;
            foreach (var p in layout.pieces)
            {
                if (p == ignore || !p.solid || p.onTop || p.mount || p.h < 0.35f) continue;
                float d = DistanceTo(p.rc, x, z);
                if (d < best) best = d;
            }
            foreach (var w in layout.walls)
            {
                if (w.y0 > 1.0f || w.y1 < 0.5f) continue;
                float d = DistanceTo(w.rc, x, z);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Can the people who must carry a treasure of <paramref name="lifters"/> hands get at it and walk it out? A thing for one needs nothing. A thing on a counter (<c>slot.host</c>) needs free room to stand and lift on one side of it - a disc 0.8 m
        /// (0.95 m for three) wide round a point 1.15 m from it, where its carriers stand side by side in front of it (the owner, 2026-10-17: "Carrying loot together was glitchy because a lot of items where in places where two players couldn't fit together
        /// while carrying"). A thing standing on the FLOOR (the prototype server, the executive safe) must be reachable from its sides (the owner, 2026-10-21: "a object that two people can pick up together but it is not accessible from both sides": the server
        /// stood in the aisle between two racks, open at one end only): its box clear of furniture and walls; a spot for a person (a disc of <see cref="StandRadius"/>, inside the room) just beyond each face, <see cref="StandOffset"/> from it; two carriers stand on
        /// OPPOSITE sides (a thing for three needs two sides, any two: three people cannot all stand opposite each other); and a second person must be able to WALK to the far side - with the box in place, both spots must be reachable from where people come into the
        /// room (<see cref="WalkGrid"/>). <paramref name="sizeX"/> / <paramref name="sizeZ"/> are the box before the slot's turn.
        /// </summary>
        public static bool RoomForCarry(Layout layout, Slot slot, int lifters, float sizeX = 1f, float sizeZ = 1f) => RoomForCarryCore(layout, slot, lifters, sizeX, sizeZ, null, false);

        /// <summary>The gap between a treasure's face and the middle of the spot a carrier stands at, and the free room (a disc, metres) one carrier needs there: a person is 0.6 m wide; the third carrier of a heavy thing needs a little more.</summary>
        public const float StandOffset = 0.45f;
        public static float StandRadius(int lifters) => lifters >= 3 ? 0.40f : 0.35f;

        /// <param name="onNavMesh">For a spot a treasure is moved to: it must also lie where the floor's NavMesh is (see the end of this method).</param>
        static bool RoomForCarryCore(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, WalkGrid grid, bool onNavMesh)
        {
            if (lifters <= 1) return true;
            var room = grid != null ? grid.Room : RoomOf(layout, slot.x, slot.z);
            if (slot.host != null)
            {
                float need = lifters >= 3 ? 0.95f : 0.80f;
                return SideFree(layout, slot, 1f, 0f, 1.15f, need, room) || SideFree(layout, slot, 0f, 1f, 1.15f, need, room) || SideFree(layout, slot, -1f, 0f, 1.15f, need, room) || SideFree(layout, slot, 0f, -1f, 1.15f, need, room);
            }
            bool yawed = Math.Abs(Math.Sin(slot.yaw * Math.PI / 180.0)) > 0.5;
            float hw = (yawed ? sizeZ : sizeX) * 0.5f, hd = (yawed ? sizeX : sizeZ) * 0.5f;
            var box = new Rc(slot.x - hw, slot.z - hd, slot.x + hw, slot.z + hd);
            if (!BoxClear(layout, box)) return false;
            float stand = StandRadius(lifters), ax = hw + StandOffset, az = hd + StandOffset;
            bool east = SpotFree(layout, slot.x + ax, slot.z, stand, room), west = SpotFree(layout, slot.x - ax, slot.z, stand, room);
            bool north = SpotFree(layout, slot.x, slot.z + az, stand, room), south = SpotFree(layout, slot.x, slot.z - az, stand, room);
            int openSides = (east ? 1 : 0) + (north ? 1 : 0) + (west ? 1 : 0) + (south ? 1 : 0);
            if (lifters >= 3 ? openSides < 2 : !((east && west) || (north && south))) return false;
            // a second person must be able to WALK to the far side: with the box in place, the spots must be reachable from where people come into the room
            if (room == null) return true;
            if (grid == null) grid = new WalkGrid(layout, room);
            if (!grid.HasSeeds) return true;                                                                  // (no way in is known: nothing to say about it)
            var seen = grid.Flood(box, WalkGrid.Person);
            bool reachE = east && grid.Reached(seen, slot.x + ax, slot.z), reachW = west && grid.Reached(seen, slot.x - ax, slot.z);
            bool reachN = north && grid.Reached(seen, slot.x, slot.z + az), reachS = south && grid.Reached(seen, slot.x, slot.z - az);
            bool carriers = lifters >= 3 ? (reachE ? 1 : 0) + (reachN ? 1 : 0) + (reachW ? 1 : 0) + (reachS ? 1 : 0) >= 2 : (reachE && reachW) || (reachN && reachS);
            if (!carriers) return false;
            // a treasure that is MOVED must also lie where the floor's NavMesh is (what the guards and the QA call walkable: an agent keeps 0.5 m clear): open ground that an agent can reach within 2 m of it, as the loot test asks;
            // a spot the planner chose is left as it was
            return !onNavMesh || grid.Near(grid.Flood(box, WalkGrid.AgentRadius), slot.x, slot.z, 2.0f);
        }
        /// <summary>Is this box free of solid furniture (taller than a knee) and walls? (Touching within 3 cm is fine.)</summary>
        static bool BoxClear(Layout layout, Rc box)
        {
            foreach (var p in layout.pieces)
                if (p.solid && !p.onTop && !p.mount && p.h >= 0.35f && p.rc.Overlaps(box, 0.03f)) return false;
            foreach (var w in layout.walls)
                if (w.y0 <= 1.0f && w.y1 >= 0.5f && w.rc.Overlaps(box, 0.03f)) return false;
            return true;
        }

        /// <summary>Is there room for a person to stand at this spot (a free disc of radius <paramref name="stand"/>, inside the room: its edge is a wall)?</summary>
        static bool SpotFree(Layout layout, float x, float z, float stand, RoomInfo room)
        {
            if (room != null && EdgeClearance(room, x, z) < stand) return false;
            return FreeRadius(layout, x, z, stand) >= stand;
        }

        /// <summary>
        /// The floor of one room as a grid of cells with the clearance of each (how far it is from furniture, walls and the room's edge, up to 1 m), made once for every spot a treasure is tried at. <see cref="Flood"/> says which cells can be walked to from the way in
        /// (a spot just inside each doorway, and the guards' standing spot of the room, which the planner made sure can be walked to) by something of a given radius - a person (<see cref="Person"/>) or an agent of the floor's NavMesh (<see cref="AgentRadius"/>) - when a box stands somewhere.
        /// </summary>
        sealed class WalkGrid
        {
            public const float Person = 0.3f, AgentRadius = 0.5f;
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
        /// <summary>The smallest room whose bounds hold this point, or null (a plan made of nothing but furniture).</summary>
        static RoomInfo RoomOf(Layout layout, float x, float z)
        {
            RoomInfo best = null; float area = float.MaxValue;
            foreach (var r in layout.rooms)
                if (r.bounds.Contains(x, z) && r.bounds.W * r.bounds.D < area) { best = r; area = r.bounds.W * r.bounds.D; }
            return best;
        }

        /// <summary>How far a point is from the edge of a room (negative outside it): the maze's cell walls stand at the edge and are not in <see cref="Layout.walls"/>, so the edge itself has to count as a wall.</summary>
        static float EdgeClearance(RoomInfo room, float x, float z) =>
            Math.Min(Math.Min(x - room.bounds.x0, room.bounds.x1 - x), Math.Min(z - room.bounds.z0, room.bounds.z1 - z));

        /// <summary>Is there room to stand and lift on one side of a treasure on a counter (a free disc of radius <paramref name="need"/> a distance <paramref name="ahead"/> from its middle, in the direction (fx, fz))?</summary>
        static bool SideFree(Layout layout, Slot slot, float fx, float fz, float ahead, float need, RoomInfo room = null)
        {
            float px = slot.x + fx * ahead, pz = slot.z + fz * ahead;
            if (room != null && EdgeClearance(room, px, pz) < need) return false;                      // (the spot is at, or beyond, the wall round the room)
            return FreeRadius(layout, px, pz, need, slot.host) >= need;
        }

        /// <summary>
        /// A treasure on the floor that cannot be reached from the sides where the planner put it moves to the nearest spot of its own room where it can: its box (grown a little) clear of furniture and walls, a metre clear of every doorway of the room and of the
        /// guards' standing places and of the rectangles that must stay walkable, and <see cref="RoomForCarry"/> true there. A grid of 0.25 m steps up to 3.5 m from the planned spot, the nearest first (the first found wins a tie); nothing random, so every
        /// machine finds the same spot. False when there is none (the treasure is then left out). Only for a thing for two or three on the floor.
        /// </summary>
        public static bool TryFindCarrySpot(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, out float x, out float z)
        {
            x = slot.x; z = slot.z;
            if (lifters <= 1 || slot.host != null) return false;
            var room = RoomOf(layout, slot.x, slot.z);
            if (room == null) return false;
            var grid = new WalkGrid(layout, room);
            float hx = sizeX * 0.5f + 0.06f, hz = sizeZ * 0.5f + 0.06f;
            const float step = 0.25f, reach = 3.5f;
            int n = (int)(reach / step);
            int best = int.MaxValue; bool found = false;
            for (int iz = -n; iz <= n; iz++)
                for (int ix = -n; ix <= n; ix++)
                {
                    int dd = ix * ix + iz * iz;
                    if (dd >= best || dd > n * n) continue;
                    float cx = slot.x + ix * step, cz = slot.z + iz * step;
                    var foot = new Rc(cx - hx, cz - hz, cx + hx, cz + hz);
                    if (foot.x0 < room.bounds.x0 + 0.05f || foot.x1 > room.bounds.x1 - 0.05f || foot.z0 < room.bounds.z0 + 0.05f || foot.z1 > room.bounds.z1 - 0.05f) continue;
                    if (!FootprintClear(layout, room, foot)) continue;
                    var spot = new Slot { x = cx, y = slot.y, z = cz, yaw = slot.yaw, kind = slot.kind, room = slot.room, host = null };
                    if (!RoomForCarryCore(layout, spot, lifters, sizeX, sizeZ, grid, true)) continue;
                    best = dd; x = cx; z = cz; found = true;
                }
            return found;
        }

        /// <summary>Is the box <paramref name="foot"/> free of solid furniture and walls, a metre from the room's doorways and the guards' standing places, and out of the rectangles that must stay walkable?</summary>
        static bool FootprintClear(Layout layout, RoomInfo room, Rc foot)
        {
            foreach (var p in layout.pieces)
                if (p.solid && !p.onTop && !p.mount && p.h >= 0.35f && p.rc.Overlaps(foot)) return false;
            foreach (var w in layout.walls)
                if (w.y0 <= 1.0f && w.y1 >= 0.5f && w.rc.Overlaps(foot)) return false;
            foreach (var d in room.doors)
                if (d.Grow(1.0f).Overlaps(foot)) return false;
            foreach (var c in layout.clear)
                if (c.Overlaps(foot)) return false;
            foreach (var wp in layout.waypoints)
                if (wp != null && wp.Length >= 2 && DistanceTo(foot, wp[0], wp[1]) < 1.0f) return false;
            return true;
        }
    }
}
