using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>A free spot on top of a desk, table or counter where a small valuable can lie (see <see cref="ValuablePlan"/>).</summary>
    public sealed class ValuableSpot
    {
        /// <summary>The middle of the spot and the height of the surface it is on.</summary>
        public float x, y, z;
        /// <summary>Degrees about the vertical.</summary>
        public float yaw;
        /// <summary>Is there room for a laptop (0.34 x 0.26 m) here, with the yaw above?</summary>
        public bool roomy;
        /// <summary>The desk, table or counter it lies on.</summary>
        public Piece host;
        /// <summary>The numbered room it is in (an index in <see cref="Layout.rooms"/>), or -1 in a corridor or an open area.</summary>
        public int roomIndex;
        /// <summary>The theme of the room it is in ("office", "conference", "break", ...), which decides what is likely to lie there.</summary>
        public string theme;
    }

    /// <summary>One valuable the floor will hold: what it is, where it lies and what it is worth.</summary>
    public sealed class PlannedValuable
    {
        public ValuableDef def;
        public ValuableSpot spot;
        public int value;
    }

    /// <summary>
    /// Where the small valuables of a floor lie. Plain data and arithmetic (no Unity types), a function of the floor's layout and seed only. The furniture
    /// of the rooms is never loot; what there is to steal lies on top of the desks, tables and counters (<see cref="SurfaceKeys"/>) in the free room between
    /// the monitors, the paper and the mugs - at most two on a surface, spread over as many rooms as possible first (<see cref="LootPlan.Pick"/>).
    /// </summary>
    public static class ValuablePlan
    {
        /// <summary>The kinds of furniture something can lie on.</summary>
        public static readonly HashSet<string> SurfaceKeys = new HashSet<string>
        {
            "desk_1", "desk_2", "desk_3", "table_box", "table_round", "coffee_table", "counter_a1", "counter_a2", "counter_b1", "counter_b2",
            "conf_table", "credenza", "cs_counter", "cs_display", "cs_table", "kstorage"
        };

        /// <summary>The margin kept clear at the edge of a surface, the search step, and the footprints tried: a small spot and a roomy one (either way round).</summary>
        const float Edge = 0.10f, Step = 0.05f, SmallW = Valuables.SmallSpot, SmallD = Valuables.SmallSpot, RoomyW = 0.36f, RoomyD = 0.30f;
        /// <summary>The most valuables on one surface, and how far apart two on the same surface are (centre to centre).</summary>
        public const int MaxPerSurface = 2;
        const float Apart = 0.50f;

        static bool Fits(Rc inner, List<Rc> blocked, float cx, float cz, float w, float d)
        {
            var r = Rc.Centered(cx, cz, w, d);
            if (r.x0 < inner.x0 - 1e-4f || r.x1 > inner.x1 + 1e-4f || r.z0 < inner.z0 - 1e-4f || r.z1 > inner.z1 + 1e-4f) return false;
            for (int i = 0; i < blocked.Count; i++) if (blocked[i].Overlaps(r, 0.002f)) return false;
            return true;
        }

        /// <summary>The smallest room (any kind) that holds a point, or -1.</summary>
        static int SmallestRoomAt(Layout layout, float x, float z)
        {
            int best = -1; float area = float.MaxValue;
            for (int i = 0; i < layout.rooms.Count; i++)
            {
                var b = layout.rooms[i].bounds;
                if (!b.Contains(x, z)) continue;
                float a = b.W * b.D;
                if (a < area) { area = a; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Every place on top of a surface where a valuable fits: up to <see cref="MaxPerSurface"/> on each, picked from the free room there is. Skips the guard
        /// outpost (it has its own things lying about). The same layout and seed give the same spots.
        /// </summary>
        public static List<ValuableSpot> FindSpots(Layout layout, int seed)
        {
            var spots = new List<ValuableSpot>();
            if (layout == null) return spots;
            var onTop = new List<Piece>();
            foreach (var p in layout.pieces) if (p.onTop && !p.mount) onTop.Add(p);
            var outpostItems = new List<Rc>();
            if (layout.outpost != null)
                foreach (var s in layout.outpost.itemSlots) outpostItems.Add(Rc.Centered(s.x, s.z, 0.5f, 0.5f));

            int hostNo = 0;
            foreach (var host in layout.pieces)
            {
                if (host.mount || host.onTop || !host.solid || !SurfaceKeys.Contains(host.key)) continue;
                if (host.room != null && host.room.StartsWith("outpost")) continue;
                hostNo++;
                float top = host.y + host.h;
                var inner = new Rc(host.rc.x0 + Edge, host.rc.z0 + Edge, host.rc.x1 - Edge, host.rc.z1 - Edge);
                if (inner.W < SmallW - 0.01f || inner.D < SmallD - 0.01f) continue;

                // what is in the way on top: things already lying there, mug spots, the treasure spots, the outpost's table
                var blocked = new List<Rc>();
                foreach (var o in onTop)
                    if (Math.Abs(o.y - top) < 0.08f && o.rc.Overlaps(host.rc, 0.001f)) blocked.Add(o.rc.Grow(0.04f));
                foreach (var s in layout.mugSlots) if (s.host == host) blocked.Add(Rc.Centered(s.x, s.z, 0.34f, 0.34f));
                foreach (var s in layout.bigSlots) if (s.host == host) blocked.Add(Rc.Centered(s.x, s.z, 0.90f, 0.90f));
                foreach (var r in outpostItems) if (r.Overlaps(host.rc, 0.001f)) blocked.Add(r);

                // every free spot on a grid, with whether a laptop fits
                var cand = new List<float[]>();                 // x, z, roomy (0/1), yaw quarter
                for (float cz = inner.z0 + SmallD * 0.5f; cz <= inner.z1 - SmallD * 0.5f + 1e-4f; cz += Step)
                    for (float cx = inner.x0 + SmallW * 0.5f; cx <= inner.x1 - SmallW * 0.5f + 1e-4f; cx += Step)
                    {
                        if (!Fits(inner, blocked, cx, cz, SmallW, SmallD)) continue;
                        int wide = Fits(inner, blocked, cx, cz, RoomyW, RoomyD) ? 1 : Fits(inner, blocked, cx, cz, RoomyD, RoomyW) ? 2 : 0;
                        cand.Add(new[] { cx, cz, wide });
                    }
                if (cand.Count == 0) continue;

                int taken = 0;
                var chosen = new List<float[]>();
                for (int attempt = 0; attempt < 12 && taken < MaxPerSurface; attempt++)
                {
                    int at = (int)(Valuables.Unit(seed, hostNo, attempt, 31) * cand.Count);
                    var c = cand[Math.Min(cand.Count - 1, at)];
                    bool near = false;
                    foreach (var o in chosen) if (Math.Abs(o[0] - c[0]) < Apart && Math.Abs(o[1] - c[1]) < Apart) { near = true; break; }
                    if (near) continue;
                    chosen.Add(c);
                    taken++;
                    // a second one must also be clear of this one
                    bool wide = c[2] > 0.5f;
                    float yaw = c[2] == 2f ? 90f : 0f;
                    if (c[2] == 0f) yaw = (float)(Math.Floor(Valuables.Unit(seed, hostNo, attempt, 32) * 24.0) * 15.0);
                    else if (c[2] == 1f) yaw = Valuables.Unit(seed, hostNo, attempt, 33) < 0.5 ? 0f : 180f;
                    else yaw = Valuables.Unit(seed, hostNo, attempt, 33) < 0.5 ? 90f : 270f;
                    var spot = new ValuableSpot { x = c[0], y = top, z = c[1], yaw = yaw, roomy = wide, host = host, roomIndex = LootPlan.RoomIndexAt(layout, c[0], c[1]) };
                    int room = SmallestRoomAt(layout, c[0], c[1]);
                    spot.theme = room >= 0 ? layout.rooms[room].theme : null;
                    spots.Add(spot);
                    blocked.Add(Rc.Centered(c[0], c[1], 0.5f, 0.42f));
                    // the candidates that are now blocked must go
                    cand.RemoveAll(o => Math.Abs(o[0] - c[0]) < Apart && Math.Abs(o[1] - c[1]) < Apart);
                    if (cand.Count == 0) break;
                }
            }
            return spots;
        }

        static int CellOf(float x, float z, float cellSize, int cols, int rows) =>
            Math.Max(0, Math.Min(cols - 1, (int)(x / cellSize))) + cols * Math.Max(0, Math.Min(rows - 1, (int)(z / cellSize)));

        /// <summary>
        /// The valuables of a floor: <see cref="Valuables.Count"/> of the spots, one in as many rooms as possible first, never more than
        /// <see cref="LootPlan.CellCap"/> in a cell, each with a kind that suits the room and a value that follows the floor's quota.
        /// </summary>
        public static List<PlannedValuable> Plan(Layout layout, int seed, int floor, float cellSize, int cols, int rows)
        {
            var result = new List<PlannedValuable>();
            var spots = FindSpots(layout, seed);
            if (spots.Count == 0) return result;
            var rng = new Random(seed ^ 0x3C6E1F);
            for (int i = spots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = spots[i]; spots[i] = spots[j]; spots[j] = t;
            }
            var group = new int[spots.Count];
            var cell = new int[spots.Count];
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                cell[i] = CellOf(s.x, s.z, cellSize, cols, rows);
                group[i] = s.roomIndex >= 0 ? s.roomIndex : -1 - cell[i];
            }
            var picked = LootPlan.Pick(spots.Count, group, cell, Valuables.Count(floor), LootPlan.CellCap);
            int k = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                if (!picked[i]) continue;
                var s = spots[i];
                var def = Valuables.Pick(s.theme, s.roomy, Valuables.Unit(seed, i, k, 41));
                result.Add(new PlannedValuable { def = def, spot = s, value = Valuables.ValueOf(def, floor, Valuables.Unit(seed, i, k, 42)) });
                k++;
            }
            return result;
        }
    }
}
