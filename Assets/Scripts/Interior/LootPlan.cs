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
        /// Can the people who must carry a treasure of <paramref name="lifters"/> hands get at it and walk it out? A thing for one needs nothing. For two or
        /// three there must be free room to stand and lift on at least one side of it - a disc 0.8 m (0.95 m for three) wide round a point 1 m (1.15 m in front
        /// of a counter) from it - and a thing standing on the floor must not be wedged in. The owner, 2026-10-17: "Carrying loot together was glitchy because a lot of items
        /// where in places where two players couldn't fit together while carrying".
        /// </summary>
        public static bool RoomForCarry(Layout layout, Slot slot, int lifters)
        {
            if (lifters <= 1) return true;
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = slot.host != null ? 1.15f : 1.0f;
            if (slot.host == null && FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            for (int k = 0; k < 4; k++)
            {
                float fx = k == 0 ? 1f : k == 2 ? -1f : 0f, fz = k == 1 ? 1f : k == 3 ? -1f : 0f;
                if (FreeRadius(layout, slot.x + fx * ahead, slot.z + fz * ahead, need, slot.host) >= need) return true;
            }
            return false;
        }
    }
}