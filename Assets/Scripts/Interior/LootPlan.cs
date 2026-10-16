using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// How much there is to steal on a floor and where it lies. Plain data and arithmetic (no Unity types), so it can be tested outside Unity.
    /// The room themes flag far more things as stealable than a floor should hold, so a floor picks a share of them. It picks to cover the
    /// numbered rooms first (one thing in as many rooms as it can), so that opening a door has a fair chance of showing something, and only
    /// then fills up with more things, at most <see cref="CellCap"/> in a cell. Then a few small valuables (a cash envelope, a gold watch,
    /// casino chips) lie on the desks of rooms that have nothing else.
    /// </summary>
    public static class LootPlan
    {
        /// <summary>The most stealable things picked in one 18 m cell.</summary>
        public const int CellCap = 4;

        /// <summary>How many stealable pieces of furniture a floor holds: 56 on floor 1, 100 on floor 12 (as many as the rooms can offer).</summary>
        public static int SmallTarget(int floor) => 52 + 4 * Math.Max(1, floor);

        /// <summary>How many big treasures (a stapler, a trophy, a server...) a floor puts on the planner's treasure spots: 11 on floor 1, 22 on floor 12.</summary>
        public static int TreasureCount(int floor) => 10 + Math.Max(1, floor);

        /// <summary>The most small valuables left lying on desks: 12 on floor 1, 17 on floor 12 (fewer if there are not enough empty rooms with a desk).</summary>
        public static int LooseCount(int floor) => 12 + Math.Max(1, floor) / 2;

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

        static int CellOf(float x, float z, float cellSize, int cols, int rows) =>
            Math.Max(0, Math.Min(cols - 1, (int)(x / cellSize))) + cols * Math.Max(0, Math.Min(rows - 1, (int)(z / cellSize)));

        /// <summary>
        /// Picks the stealable pieces of a floor: sets <see cref="Piece.picked"/> on the chosen ones (and clears it on the others) and returns how
        /// many there are. <paramref name="rng"/> only shuffles the candidates.
        /// </summary>
        public static int SelectSmall(Layout layout, Random rng, int floor, float cellSize, int cols, int rows)
        {
            var cand = new List<Piece>();
            foreach (var p in layout.pieces)
            {
                p.picked = false;
                if (p.loot && p.def.Lootable && !p.mount) cand.Add(p);
            }
            for (int i = cand.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = cand[i]; cand[i] = cand[j]; cand[j] = t;
            }
            var group = new int[cand.Count];
            var cell = new int[cand.Count];
            for (int i = 0; i < cand.Count; i++)
            {
                var p = cand[i];
                cell[i] = CellOf(p.x, p.z, cellSize, cols, rows);
                int room = RoomIndexAt(layout, p.x, p.z);
                group[i] = room >= 0 ? room : -1 - cell[i];            // (a thing in a corridor or an open area stands for that cell)
            }
            var picked = Pick(cand.Count, group, cell, SmallTarget(floor), CellCap);
            int got = 0;
            for (int i = 0; i < cand.Count; i++) if (picked[i]) { cand[i].picked = true; got++; }
            return got;
        }

        /// <summary>
        /// Where the small valuables lie: spare spots on desks and counters (<paramref name="spare"/>) in numbered rooms that hold nothing
        /// else - no picked piece, none of <paramref name="taken"/> (the x, z of the treasures and anything else already put there) - at most
        /// one to a room, at most <see cref="LooseCount"/>. <paramref name="rng"/> decides which rooms.
        /// </summary>
        public static List<Slot> LooseSpots(Layout layout, List<Slot> spare, List<float[]> taken, Random rng, int floor)
        {
            var result = new List<Slot>();
            if (layout == null || spare == null || spare.Count == 0) return result;
            var busy = new HashSet<int>();
            foreach (var p in layout.pieces)
                if (p.picked) { int r = RoomIndexAt(layout, p.x, p.z); if (r >= 0) busy.Add(r); }
            if (taken != null)
                foreach (var t in taken) { int r = RoomIndexAt(layout, t[0], t[1]); if (r >= 0) busy.Add(r); }
            if (layout.safe != null) { int r = RoomIndexAt(layout, layout.safe.x, layout.safe.z); if (r >= 0) busy.Add(r); }

            var options = new List<Slot>();
            var roomOf = new Dictionary<Slot, int>();
            foreach (var s in spare)
            {
                if (s.host != null && s.host.picked) continue;          // (its desk piece went away as loot)
                int r = RoomIndexAt(layout, s.x, s.z);
                if (r < 0 || busy.Contains(r) || !RoomNumbers.IsNumbered(layout.rooms[r])) continue;
                roomOf[s] = r;
                options.Add(s);
            }
            for (int i = options.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = options[i]; options[i] = options[j]; options[j] = t;
            }
            int want = LooseCount(floor);
            var used = new HashSet<int>();
            foreach (var s in options)
            {
                if (result.Count >= want) break;
                if (!used.Add(roomOf[s])) continue;
                result.Add(s);
            }
            return result;
        }
    }
}
