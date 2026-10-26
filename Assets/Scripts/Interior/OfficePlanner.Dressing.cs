using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The Floor 7 environmental art pass (the owner, 2026-10-23: "Environmental Art Pass - Floor 7 Only"): a test of whether more deliberate dressing makes the floor feel lived in. It runs AFTER the floor is planned and furnished
    /// (<see cref="OfficePlanner.Run"/>, once PlaceSafe and MergeHideZones are done), only on floor <see cref="DressedFloor"/>, and it only ADDS: books on the shelves, paper, mugs and lamps on the desks, a place setting at every
    /// seat of a meeting room, kitchen things in the break rooms, cleaning gear in the stores, rugs under the sofas, posters, boards and shelves on the walls. Nothing that was planned moves or goes: the walls, the rooms, the doorways,
    /// the hide zones, the patrol stops, the treasure spots and the first N pieces of the layout are exactly what they were (NetDevDressTest checks all of it). Every piece it adds has <see cref="Piece.dressing"/> set, so a picture or
    /// a test can tell it from the original furnishing, and <see cref="DressingOn"/> = false gives the floor as it was (the before pictures).
    ///
    /// The rules that keep it comfortable to walk through (the owner: "Maintaining clear, comfortable walkways is a hard requirement"):
    ///  - a solid thing is only put where it is flush with what it stands beside (6 cm or less) or at least 1.2 m (the planner's own walking width) from every other solid thing and wall - never a gap a person has to squeeze through;
    ///  - it never touches a doorway approach (the planner's keep-outs and the walkable rectangles <see cref="Layout.clear"/> get 0.9 m more), a patrol stop (0.8 m), a treasure spot (1.7 m) or the strip in front of safety gear;
    ///  - after every solid thing the room's reach grid is recomputed: anything a person (0.6 m wide, the guards' size with a margin) could reach before, and that is not within 0.6 m of the new thing, is still reachable;
    ///  - things on a desk, table or counter never take away a place for a small valuable (the surfaces of <see cref="ValuablePlan.SurfaceKeys"/> keep their capacity);
    ///  - nothing hangs on the walls that could become a glass front (FloorGenerator.Glass: an office wall onto a corridor), and nothing in a corridor reaches into the band of the direction signs (1.95 m and up);
    ///  - the small things have no collider (the same as the chairs and bins already are), the few solid ones (a vacuum cleaner, an arcade machine) go through the rules above, and none of it can hide a person (hide = false).
    /// Rooms where nobody goes (the restrooms), the guard outpost and the two big rings (the lobby and the service cells) are left as they are, apart from the lobby's rugs and table tops.
    /// </summary>
    public sealed partial class OfficePlanner
    {
        /// <summary>The last floor that is dressed: floor 7 was the test floor and floors 1 to 6 follow since the owner's go (2026-10-23); <see cref="FloorArtRules"/> is the one rule.</summary>
        public const int DressedFloor = FloorArtRules.LastFloor;
        /// <summary>For tests and the before pictures: false builds floor 7 as it was before the pass.</summary>
        public static bool DressingOn = true;
        /// <summary>How full the rooms get: 1 is the standard, 0.5 half as many pieces, 1.5 half as many again (the one knob for the amount of dressing; every room has a budget scaled by it).</summary>
        public static float DressBudget = 1f;
        /// <summary>For the tests: when set, the reasons a thing was not placed are collected here.</summary>
        public static List<string> DressDebug;
        /// <summary>What the last dressing did (piece counts per theme and per kind), for tests and the summary.</summary>
        public static readonly Dictionary<string, int> DressStats = new Dictionary<string, int>();

        void DressFloor()
        {
            if (!DressingOn || !FloorArtRules.AppliesTo(inp.floor)) return;
            new Dresser(this).Run();
        }

        /// <summary>The usable surfaces of a shelf: its usable width, the offset and depth of the shelf boards (host-local), and the height of each board with the free height above it. Measured from the pack's models.</summary>
        sealed class ShelfSpec
        {
            public float w, v, d;
            public float[] y, clear;
        }

        static ShelfSpec Spec(float w, float v, float d, params float[] yc)
        {
            var s = new ShelfSpec { w = w, v = v, d = d, y = new float[yc.Length / 2], clear = new float[yc.Length / 2] };
            for (int i = 0; i < s.y.Length; i++) { s.y[i] = yc[2 * i]; s.clear[i] = yc[2 * i + 1]; }
            return s;
        }

        static readonly Dictionary<string, ShelfSpec> Shelves = new Dictionary<string, ShelfSpec>
        {
            { "bookshelf", Spec(1.04f, 0.04f, 0.38f, 0.043f, 0.36f, 0.442f, 0.42f, 0.896f, 0.44f, 1.364f, 0.42f) },
            { "shelf", Spec(1.15f, 0f, 0.46f, 0.429f, 0.48f, 0.929f, 0.48f, 1.429f, 0.48f) },
            { "shelf_wood", Spec(2.10f, 0f, 0.46f, 0.360f, 0.39f, 0.771f, 0.39f, 1.182f, 0.39f) },
            { "kshelve", Spec(2.10f, 0f, 0.46f, 0.360f, 0.39f, 0.771f, 0.39f, 1.182f, 0.39f) },
            { "shelf_wide", Spec(2.90f, 0.04f, 0.38f, 0.040f, 0.38f, 0.437f, 0.39f, 0.846f, 0.39f, 1.255f, 0.40f) },
            { "kitchen_unit", Spec(2.90f, 0.04f, 0.38f, 0.040f, 0.38f, 0.437f, 0.39f, 0.846f, 0.39f, 1.255f, 0.40f) },
            { "shelf_low", Spec(1.28f, 0.02f, 0.32f, 0.062f, 0.26f, 0.341f, 0.26f, 0.617f, 0.26f) },
            { "credenza", Spec(1.28f, 0.02f, 0.32f, 0.062f, 0.26f, 0.341f, 0.26f, 0.617f, 0.26f) },
            { "shelf_4", Spec(1.05f, 0f, 0.62f, 0.265f, 0.40f, 0.676f, 0.40f) },
        };

        /// <summary>The boards of the wall shelves (mounted props): board heights above the shelf's underside, the free height above each, usable width, offset and depth.</summary>
        static readonly Dictionary<string, ShelfSpec> WallShelves = new Dictionary<string, ShelfSpec>
        {
            { "wall_shelf", Spec(1.36f, 0f, 0.30f, 0.279f, 0.22f, 0.528f, 0.40f) },
            { "shelf_wall_2", Spec(1.40f, 0f, 0.40f, 0.378f, 0.28f, 0.705f, 0.40f) },
            { "shelf_wall_3", Spec(1.50f, 0f, 0.40f, 0.351f, 0.40f) },
        };

        static readonly string[] PoolBooks = { "book_row_1", "book_row_2", "book_row_3", "book_row_1", "book_row_2", "book_pile_1", "book_pile_2", "book_pile_3", "book_pile_4", "book_pile_5", "magazine_1", "magazine_2" };
        static readonly string[] PoolBookDeco = { "book_row_1", "book_row_2", "book_row_3", "book_pile_1", "book_pile_2", "book_pile_3", "book_pile_4", "photo_2", "photo_3", "plant_desk", "trophy_cup", "cup_pens", "figure_knight", "figure_viking", "book_pile_5" };
        static readonly string[] PoolArchive = { "file_holder", "file_holder", "file_holder", "paper_pile_3", "paper_pile_2", "paper_tray_full", "paper_tray_empty", "folder_tray_2", "book_pile_2", "book_pile_3", "book_pile_4", "box_small", "food_container", "lunchbox_2" };
        static readonly string[] PoolStore = { "box_small", "box_small", "paper_tray_full", "file_holder", "file_holder", "paper_pile_3", "book_pile_2", "book_pile_3", "book_pile_4", "paper_tray_empty", "net_cable", "bucket_1" };
        static readonly string[] PoolDeskBack = { "lamp_a", "lamp_b", "lamp_c", "lamp_d", "photo_2", "photo_3", "photo_4", "photo_5", "plant_desk", "plant_tiny", "cup_pens", "radio_1", "book_pile_2", "file_holder", "photo_2", "plant_desk" };
        static readonly string[] PoolDeskFront = { "paper_1", "paper_2", "paper_3", "folder_1", "folder_2", "folder_3", "folder_4", "notepad_2", "clipboard", "magazine_1", "magazine_2", "book_pile_1", "book_pile_5", "calculator", "sticky_1", "sticky_2", "sticky_3", "laptop_2", "paper_pile_2", "folder_tray_2", "holepunch", "paper_tray_empty", "folder_1", "paper_1" };
        static readonly string[] PoolCups = { "cup_coffee_open", "cup_red", "cup_foam", "cup_coffee_open", "can_soda", "water_bottle", "cup_red", "cup_foam" };
        static readonly string[] PoolPaper = { "paper_1", "paper_2", "paper_3", "folder_1", "folder_2", "folder_3", "notepad_2", "clipboard", "paper_1", "paper_2" };

        static readonly string[] PoolKitchen = { "fruit_bowl", "cake", "donut_choc", "donut_pink", "dishes_1", "dishes_3", "lunchbox", "lunchbox_2", "milk", "sandwich_1", "sandwich_2", "toaster_2", "blender", "cup_kit_1", "cup_kit_2", "food_container", "chopping_board", "knife_holder", "pot", "napkins", "cup_saucer", "snack_1", "snack_2", "snack_3" };
        static readonly string[] PoolTableFood = { "fruit_bowl", "donut_choc", "donut_pink", "napkins", "snack_1", "snack_2", "snack_3", "sandwich_1", "sandwich_2", "cup_kit_1", "cup_kit_2", "cup_saucer", "lunchbox", "water_bottle", "can_soda", "magazine_1", "cake" };
        static readonly string[] PoolKitchenShelf = { "cup_kit_1", "cup_kit_2", "dishes_3", "pot", "food_container", "lunchbox", "plant_desk", "cup_saucer", "mints" };
        static readonly string[] PoolLounge = { "magazine_1", "magazine_2", "cup_red", "cup_coffee_open", "book_pile_1", "book_pile_5", "can_soda", "coffee_tray", "snack_1" };
        static readonly string[] PoolReception = { "plant_desk", "phone_rotary", "coffee_tray", "fruit_bowl", "photo_3", "cup_boss", "notepad_2", "cup_coffee_open", "mints", "jellybeans", "name_plate" };
        static readonly string[] PoolPins = { "certificate", "calendar", "child_art", "painting_small" };          // (no clock: the owner, 2026-10-24, "a clock is placed on the side of a desk, this should not be the case as it is not really the norm")
        static readonly string[] PoolWallBig = { "painting", "painting", "poster_1", "poster_3", "poster_stocks", "wall_dressing", "wall_dressing_3", "wall_dressing_4", "wall_dressing2", "corkboard_2" };
        static readonly string[] PoolWallSmall = { "certificate", "calendar", "child_art", "painting_small", "clock" };
        static readonly string[] PoolWallTrain = { "poster_1", "poster_3", "poster_stocks", "wall_dressing_3", "wall_dressing_4", "corkboard_2", "employee_board", "painting_small" };
        static readonly string[] PoolWallCanteen = { "poster_stocks", "poster_3", "painting", "wall_dressing", "wall_dressing_3", "employee_board", "corkboard_blank", "painting_small" };
        static readonly string[] PoolWallStore = { "employee_board", "corkboard_2", "wall_dressing2", "poster_1", "calendar" };
        static readonly string[] PoolWallServer = { "clock", "calendar", "wall_dressing2", "certificate", "poster_3" };
        static readonly string[] PoolWallOpen = { "employee_board", "corkboard_2", "wall_dressing_3", "poster_stocks", "poster_1", "wall_dressing2", "poster_3" };
        static readonly string[] PoolWallHall = { "poster_1", "poster_3", "poster_stocks", "painting", "wall_dressing2", "painting_small", "employee_board" };
        static readonly string[] PoolWallHallSmall = { "certificate", "calendar", "clock", "child_art", "painting_small" };

        static int StableHash(string s)
        {
            unchecked { int h = 17; for (int i = 0; i < s.Length; i++) h = h * 31 + s[i]; return h & 0x7fffffff; }
        }

        /// <summary>Does the quarter turn of a yaw (degrees) come to 0..3?</summary>
        static int Quarter(float yaw) => (((int)Math.Round(yaw / 90f)) % 4 + 4) % 4;

        static float RectGap(Rc a, Rc b)
        {
            float dx = Math.Max(0f, Math.Max(a.x0 - b.x1, b.x0 - a.x1)), dz = Math.Max(0f, Math.Max(a.z0 - b.z1, b.z0 - a.z1));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        sealed class Dresser
        {
            readonly OfficePlanner pl;
            readonly Layout L;
            Random rnd;

            // ---- the world, for the clearance rules
            readonly List<Rc> solids = new List<Rc>();
            readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            int[] stamp = new int[0];
            int stampNo;
            const float CellSz = 4f;
            readonly List<Rc> noMount = new List<Rc>();
            readonly List<Rc> kitZones = new List<Rc>();
            readonly List<float[]> stops = new List<float[]>();
            readonly List<float[]> slots = new List<float[]>();
            readonly List<int> near = new List<int>();
            readonly Dictionary<Piece, int[]> capBefore = new Dictionary<Piece, int[]>();

            // ---- the room being dressed
            RoomCtx r;
            ReachGrid reach;
            /// <summary>How many pieces this room may get in all, and a lower cap for the moment (a shelf fill takes only its share of the budget).</summary>
            int budget, cap = int.MaxValue;
            /// <summary>The last fifth of a room's budget (rooms with ten pieces or more) is kept for what stands on the floor: the small things (on furniture, on shelves, on the walls) are placed first by most recipes and would use it all.</summary>
            int reserve;
            /// <summary>Clocks hung by <see cref="DecorWall"/> in this room.</summary>
            int clocks;
            /// <summary>The things hung on the walls in and round this room (the old ones and the new): nothing tall is stood in front of one.</summary>
            readonly List<Piece> mounts = new List<Piece>();
            bool Full => addedRoom >= budget - reserve || addedRoom >= cap;
            bool FullFloor => addedRoom >= budget;
            List<Piece> tops = new List<Piece>();
            readonly List<Rc> rugs = new List<Rc>();
            int addedRoom;

            public Dresser(OfficePlanner pl)
            {
                this.pl = pl; L = pl.layout;
                foreach (var p in L.pieces) if (p.solid && !p.mount && !p.onTop) AddSolid(p.rc);
                foreach (var w in L.walls) if ((w.kind == "wall" || w.kind == "panel") && w.y0 < 0.5f) AddSolid(w.rc);
                foreach (var ri in L.rooms) foreach (var b in ri.blocks) AddSolid(b);
                Perimeter();
                GlassZones();
                KitZones();
                foreach (var wp in L.waypoints) stops.Add(new[] { wp[0], wp[1] });
                foreach (var ri in L.rooms) if (ri.wx != 0f || ri.wz != 0f) stops.Add(new[] { ri.wx, ri.wz });
                foreach (var s in L.bigSlots) slots.Add(new[] { s.x, s.z });
                foreach (var sf in L.safes) if (sf.piece != null) slots.Add(new[] { sf.piece.x, sf.piece.z });
            }

            // ------------------------------------------------------------------ the world

            void AddSolid(Rc rc)
            {
                int i = solids.Count;
                solids.Add(rc);
                int x0 = (int)Math.Floor(rc.x0 / CellSz), x1 = (int)Math.Floor(rc.x1 / CellSz), z0 = (int)Math.Floor(rc.z0 / CellSz), z1 = (int)Math.Floor(rc.z1 / CellSz);
                for (int a = x0; a <= x1; a++)
                    for (int b = z0; b <= z1; b++)
                    {
                        long key = (a + 1000L) * 100000L + (b + 1000L);
                        if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                        list.Add(i);
                    }
            }

            void Near(Rc area, List<int> res)
            {
                res.Clear();
                if (stamp.Length < solids.Count) Array.Resize(ref stamp, solids.Count + 256);
                stampNo++;
                int x0 = (int)Math.Floor(area.x0 / CellSz), x1 = (int)Math.Floor(area.x1 / CellSz), z0 = (int)Math.Floor(area.z0 / CellSz), z1 = (int)Math.Floor(area.z1 / CellSz);
                for (int a = x0; a <= x1; a++)
                    for (int b = z0; b <= z1; b++)
                    {
                        if (!grid.TryGetValue((a + 1000L) * 100000L + (b + 1000L), out var list)) continue;
                        foreach (int i in list) if (stamp[i] != stampNo) { stamp[i] = stampNo; res.Add(i); }
                    }
            }

            /// <summary>The outer walls of the cells (FloorGenerator.BuildShell): 0.4 m thick on every cell edge, a 4 m doorway where the maze is open.</summary>
            void Perimeter()
            {
                var inp = pl.inp; float cs = inp.cell; const float t = 0.2f, hg = 2f;
                for (int c = 0; c <= inp.cols; c++)
                    for (int rr = 0; rr < inp.rows; rr++)
                    {
                        float x = c * cs, z0 = rr * cs, z1 = z0 + cs, zm = (z0 + z1) * 0.5f;
                        bool open = c > 0 && c < inp.cols && inp.openE[c - 1, rr];
                        if (!open) AddSolid(new Rc(x - t, z0, x + t, z1));
                        else { AddSolid(new Rc(x - t, z0, x + t, zm - hg)); AddSolid(new Rc(x - t, zm + hg, x + t, z1)); }
                    }
                for (int rr = 0; rr <= inp.rows; rr++)
                    for (int c = 0; c < inp.cols; c++)
                    {
                        float z = rr * cs, x0 = c * cs, x1 = x0 + cs, xm = (x0 + x1) * 0.5f;
                        bool open = rr > 0 && rr < inp.rows && inp.openN[c, rr - 1];
                        if (!open) AddSolid(new Rc(x0, z - t, x1, z + t));
                        else { AddSolid(new Rc(x0, z - t, xm - hg, z + t)); AddSolid(new Rc(xm + hg, z - t, x1, z + t)); }
                    }
            }

            static bool GlassFriendly(string theme) => theme == "office" || theme == "conference" || theme == "meeting" || theme == "training";

            bool InCorridor(float x, float z)
            {
                foreach (var ri in L.rooms)
                {
                    if (ri.kind != 2 || !ri.bounds.Contains(x, z)) continue;
                    bool blocked = false;
                    foreach (var b in ri.blocks) if (b.Contains(x, z)) { blocked = true; break; }
                    if (!blocked) return true;
                }
                return false;
            }

            /// <summary>FloorGenerator.Glass.FrontOf: does this wall run along the edge of the room with a corridor on the other side?</summary>
            bool FrontOf(WallBox w, Rc b)
            {
                var wr = w.rc;
                bool alongX = wr.W > wr.D;
                float overlap = alongX ? Math.Min(wr.x1, b.x1) - Math.Max(wr.x0, b.x0) : Math.Min(wr.z1, b.z1) - Math.Max(wr.z0, b.z0);
                if (overlap < 1.2f) return false;
                float ox, oz;
                if (alongX)
                {
                    if (Math.Abs(wr.Cz - b.z0) < 0.4f) { ox = 0f; oz = -1f; }
                    else if (Math.Abs(wr.Cz - b.z1) < 0.4f) { ox = 0f; oz = 1f; }
                    else return false;
                }
                else
                {
                    if (Math.Abs(wr.Cx - b.x0) < 0.4f) { ox = -1f; oz = 0f; }
                    else if (Math.Abs(wr.Cx - b.x1) < 0.4f) { ox = 1f; oz = 0f; }
                    else return false;
                }
                return InCorridor(wr.Cx + ox * 0.9f, wr.Cz + oz * 0.9f);
            }

            /// <summary>The bits of wall that could become a glass front (see FloorGenerator.BuildOfficeWindows): nothing is hung within 0.5 m of one, so no office loses its window to the dressing.</summary>
            void GlassZones()
            {
                var op = L.outpost;
                foreach (var room in L.rooms)
                {
                    if (room.kind != 0 || !GlassFriendly(room.theme)) continue;
                    if (op != null && ReferenceEquals(op.room, room)) continue;
                    foreach (var w in L.walls)
                    {
                        if (w.kind != "wall" || w.y0 > 0.01f || w.y1 < pl.inp.wallH - 0.01f) continue;
                        var wr = w.rc;
                        float th = Math.Min(wr.W, wr.D), len = Math.Max(wr.W, wr.D);
                        if (th > 0.45f || len < 1.6f) continue;
                        if (!FrontOf(w, room.bounds)) continue;
                        noMount.Add(wr.Grow(0.5f));
                    }
                }
            }

            /// <summary>The floor in front of the safety gear (0.9 m out, 0.35 m to either side): the same strips NetDevR4Test.CoreRoomsTest checks.</summary>
            void KitZones()
            {
                foreach (var g in L.pieces)
                {
                    if (!g.mount || (g.key != "extinguisher" && g.key != "emergency_axe" && g.key != "fire_alarm")) continue;
                    float fx = (float)Math.Sin(g.yaw * Math.PI / 180.0), fz = (float)Math.Cos(g.yaw * Math.PI / 180.0);
                    Rc zone = g.rc;
                    if (Math.Abs(fx) > Math.Abs(fz)) { if (fx > 0f) zone.x1 = g.rc.x0 + 0.9f; else zone.x0 = g.rc.x1 - 0.9f; zone.z0 -= 0.35f; zone.z1 += 0.35f; }
                    else { if (fz > 0f) zone.z1 = g.rc.z0 + 0.9f; else zone.z0 = g.rc.z1 - 0.9f; zone.x0 -= 0.35f; zone.x1 += 0.35f; }
                    kitZones.Add(zone);
                }
            }

            // ------------------------------------------------------------------ random helpers

            float Rnd(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            bool Chance(double p) => rnd.NextDouble() < p;
            T One<T>(T[] xs) => xs[rnd.Next(xs.Length)];
            void Shuffle<T>(List<T> l) { for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = l[i]; l[i] = l[j]; l[j] = t; } }

            // ------------------------------------------------------------------ the rooms

            public void Run()
            {
                int before = L.pieces.Count;
                DressStats.Clear();
                var rooms = new List<RoomCtx>(pl.allRooms);
                foreach (var room in rooms)
                {
                    string th = room.theme;
                    if (th == "restroom" || th == "outpost" || th == "service" || th == "cafe_counter" || th == "cafe_seating") continue;
                    if (room.info == null) continue;
                    Begin(room);
                    try { Dress(); }
                    catch (Exception e) { Stat("errors", 1); System.Diagnostics.Debug.WriteLine("[Dress] " + room.name + ": " + e.Message); }
                    End();
                }
                Stat("pieces", L.pieces.Count - before);
                // the pieces count of each room follows
                var counts = new Dictionary<string, int>();
                foreach (var p in L.pieces) if (p.room != null) { counts.TryGetValue(p.room, out int n); counts[p.room] = n + 1; }
                foreach (var room in pl.allRooms) if (room.info != null) { counts.TryGetValue(room.name, out int n); room.info.pieceCount = n; }
            }

            void Stat(string key, int n)
            {
                if (n == 0) return;
                DressStats.TryGetValue(key, out int o); DressStats[key] = o + n;
            }

            void Begin(RoomCtx room)
            {
                r = room;
                rnd = new Random(Mix(pl.inp.seed, StableHash(room.name), pl.inp.floor, 70231));
                reach = r.ComputeReach();
                tops = new List<Piece>();
                foreach (var p in L.pieces) if (p.room == r.name && p.onTop && !p.mount) tops.Add(p);
                rugs.Clear();
                addedRoom = 0;
                cap = int.MaxValue;
                budget = (int)Math.Round(BaseBudget() * DressBudget);
                reserve = budget >= 10 ? budget / 5 : 0;
                clocks = 0;
                mounts.Clear();
                var rb = r.info.bounds.Grow(0.6f);
                foreach (var p in L.pieces) if (p.mount && rb.Overlaps(p.rc, 0.001f)) mounts.Add(p);
            }

            /// <summary>Would a thing of this footprint and height stand in front of (or against) something hung on the wall? (A 7 cm band round it counts: flush against the wall means in front of what hangs there.)</summary>
            bool MountBehind(Rc w, float h)
            {
                var g = w.Grow(0.07f);
                foreach (var p in mounts) if (p.y < h && p.rc.Overlaps(g, 0.004f)) return true;
                return false;
            }

            /// <summary>The most pieces a room of this kind gets (times <see cref="DressBudget"/>): enough to look lived in, not enough to make the floor heavy.</summary>
            int BaseBudget()
            {
                switch (r.theme)
                {
                    case "office": return r.w >= 5f && r.d >= 5f ? 24 : 16;
                    case "conference": case "meeting": return 18;
                    case "training": return 16;
                    case "break": return 20;
                    case "canteen": return 32;
                    case "storage": return 20;
                    case "copy": return 10;
                    case "server": return 8;
                    case "lounge": return 20;
                    case "nook": return 10;
                    case "open": return 20;
                    case "longopen": return 40;
                    case "hall": return r.longHall ? 8 : 4;
                    case "hub": return 1;
                    case "lobby": return 36;
                }
                return 8;
            }

            void End()
            {
                Stat(r.theme + ".rooms", 1);
                Stat(r.theme + ".added", addedRoom);
            }

            void Dress()
            {
                switch (r.theme)
                {
                    case "office": DOffice(); break;
                    case "conference": case "meeting": DConference(); break;
                    case "training": DTraining(); break;
                    case "break": DBreak(); break;
                    case "storage": DStorage(); break;
                    case "copy": DCopy(); break;
                    case "server": DServer(); break;
                    case "lounge": DLounge(); break;
                    case "nook": DNook(); break;
                    case "open": case "longopen": DOpen(); break;
                    case "canteen": DCanteen(); break;
                    case "hall": DHall(); break;
                    case "hub": DHub(); break;
                    case "lobby": DLobby(); break;
                }
            }

            // ------------------------------------------------------------------ placing: the floor

            Item Mark(Item it)
            {
                if (it == null || it.piece == null) return it;
                it.piece.dressing = true;
                if (it.piece.hide) { it.piece.hide = false; r.hides.Remove(it); }
                addedRoom++;
                if (it.piece.mount) mounts.Add(it.piece);
                return it;
            }

            bool InRoom(Rc rc, float m = 0f)
            {
                const float e = 0.005f;
                return rc.x0 >= -r.w / 2 + m - e && rc.x1 <= r.w / 2 - m + e && rc.z0 >= -r.d / 2 + m - e && rc.z1 <= r.d / 2 - m + e;
            }

            bool InKeep(Rc rc, bool solid, float h)
            {
                for (int i = 0; i < r.keep.Count; i++) if (r.keep[i].Overlaps(rc)) return true;
                for (int i = 0; i < r.blocks.Count; i++) if (r.blocks[i].Overlaps(rc)) return true;
                for (int i = 0; i < r.lanes.Count; i++) if (r.lanes[i].Overlaps(rc)) return true;
                if (solid && h > 0.3f) for (int i = 0; i < r.reachZones.Count; i++) if (r.reachZones[i].Overlaps(rc, 0.02f)) return true;
                return false;
            }

            /// <summary>The clearance rules for a solid thing (world rectangle): away from the patrol stops, the treasure spots, the doorways and the safety gear, and never a squeeze between it and anything else solid.</summary>
            string solidWhy;
            bool SolidOk(Rc w, float h)
            {
                foreach (var s in stops) if (w.Grow(0.8f).Contains(s[0], s[1])) { solidWhy = "stop"; return false; }
                foreach (var s in slots) if (w.Grow(1.7f).Contains(s[0], s[1])) { solidWhy = "slot"; return false; }
                foreach (var c in L.clear) if (w.Overlaps(c.Grow(0.9f))) { solidWhy = "clear"; return false; }
                if (h > 0.3f) foreach (var z in kitZones) if (w.Overlaps(z, 0.02f)) { solidWhy = "kit"; return false; }
                Near(w.Grow(1.2f), near);
                foreach (int i in near)
                {
                    float g = RectGap(w, solids[i]);
                    if (g > 0.06f && g < 1.2f) { solidWhy = "gap"; return false; }
                }
                return true;
            }

            /// <summary>Does the room still have every walkable bit it had, apart from the 0.6 m round the new thing? (Then the new baseline is the room as it is now.)</summary>
            bool ReachKept(Item it)
            {
                var g1 = r.ComputeReach();
                var sh = it.rc.Grow(reach.agent + 0.01f);
                for (int z = 0; z < reach.nz; z++)
                    for (int x = 0; x < reach.nx; x++)
                    {
                        int k = z * reach.nx + x;
                        if (!reach.seen[k] || g1.seen[k]) continue;
                        float cx = reach.ox + (x + 0.5f) * ReachGrid.Res, cz = reach.oz + (z + 0.5f) * ReachGrid.Res;
                        if (!sh.Contains(cx, cz)) return false;
                    }
                reach = g1;
                return true;
            }

            /// <summary>
            /// Is a thing standing here flush with something (6 cm or less from a wall or a solid thing), with nothing else at a squeezing distance? A thing that stands about the room by itself is not placed:
            /// everything with height stands against a wall or a piece of furniture, so it is never in the middle of a walkway. A thing without a collider also keeps 45 cm from everything else.
            /// </summary>
            bool Flush(Rc w, bool solid)
            {
                Near(w.Grow(1.2f), near);
                bool found = false;
                foreach (int i in near)
                {
                    float g = RectGap(w, solids[i]);
                    if (g <= 0.06f) found = true;
                    else if (g < (solid ? 1.2f : 0.45f)) return false;
                }
                return found;
            }

            /// <summary>
            /// A thing on the floor with its middle at (lx, lz) (room frame). Everything has to be inside the room, off the keep-outs and the aisles and clear of what is on the floor; anything taller than 30 cm also stands flush
            /// against something (<see cref="Flush"/>); a solid thing has to pass every clearance rule (<see cref="SolidOk"/>) and not cut off any walkable floor (<see cref="ReachKept"/>).
            /// </summary>
            /// <summary>
            /// The walk test for one new solid thing, over the room and 2 m round it (the rooms next door and the corridor too): a person (0.45 m) and a wide walker (0.6 m), taken as discs, start from anywhere on the edge of that area;
            /// whatever they could walk to before and is more than their own radius away from the new thing must still be walkable. (The per-room reach grid does not see a pocket that is also reached from next door.)
            /// </summary>
            bool WorldReachKept(Rc newRc)
            {
                var box = r.WorldBounds.Grow(2.0f);
                const float res = 0.1f;
                int nx = (int)Math.Ceiling(box.W / res), nz = (int)Math.Ceiling(box.D / res);
                var list = new List<int>();
                foreach (float agent in new[] { 0.45f, 0.6f })
                {
                    var blk = new bool[nx * nz];
                    Near(box.Grow(agent + 0.1f), list);
                    foreach (int i in list) Stamp(blk, box, nx, nz, res, solids[i], agent);
                    var before = FloodEdge(blk, nx, nz);
                    Stamp(blk, box, nx, nz, res, newRc, agent);
                    var after = FloodEdge(blk, nx, nz);
                    var shadow = newRc.Grow(agent + 0.11f);
                    for (int z = 0; z < nz; z++)
                        for (int x = 0; x < nx; x++)
                        {
                            int k = z * nx + x;
                            if (!before[k] || after[k]) continue;
                            if (!shadow.Contains(box.x0 + (x + 0.5f) * res, box.z0 + (z + 0.5f) * res)) return false;
                        }
                }
                return true;
            }

            static void Stamp(bool[] blk, Rc box, int nx, int nz, float res, Rc s, float agent)
            {
                int x0 = Math.Max(0, (int)Math.Floor((s.x0 - agent - box.x0) / res)), x1 = Math.Min(nx - 1, (int)Math.Ceiling((s.x1 + agent - box.x0) / res));
                int z0 = Math.Max(0, (int)Math.Floor((s.z0 - agent - box.z0) / res)), z1 = Math.Min(nz - 1, (int)Math.Ceiling((s.z1 + agent - box.z0) / res));
                float a2 = agent * agent;
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float cx = box.x0 + (x + 0.5f) * res, cz = box.z0 + (z + 0.5f) * res;
                        float dx = Math.Max(Math.Max(s.x0 - cx, 0f), cx - s.x1), dz = Math.Max(Math.Max(s.z0 - cz, 0f), cz - s.z1);
                        if (dx * dx + dz * dz < a2) blk[z * nx + x] = true;
                    }
            }

            static bool[] FloodEdge(bool[] blk, int nx, int nz)
            {
                var seen = new bool[nx * nz];
                var q = new Queue<int>();
                void Seed(int x, int z) { int k = z * nx + x; if (!blk[k] && !seen[k]) { seen[k] = true; q.Enqueue(k); } }
                for (int x = 0; x < nx; x++) { Seed(x, 0); Seed(x, nz - 1); }
                for (int z = 0; z < nz; z++) { Seed(0, z); Seed(nx - 1, z); }
                while (q.Count > 0)
                {
                    int k = q.Dequeue(); int x = k % nx, z = k / nx;
                    if (x + 1 < nx) Seed(x + 1, z);
                    if (x > 0) Seed(x - 1, z);
                    if (z + 1 < nz) Seed(x, z + 1);
                    if (z > 0) Seed(x, z - 1);
                }
                return seen;
            }

            string lastKey;
            Item Floor(string key, float lx, float lz, float yaw, bool loose = false)
            {
                if (FullFloor) return null;
                lastKey = key;
                var def = PropTable.Get(key);
                bool solid = def.solid, tall = def.H > 0.3f;
                int q = Quarter(yaw);
                float fx = (q & 1) == 0 ? def.W : def.D, fz = (q & 1) == 0 ? def.D : def.W;
                var rc = new Rc(lx - fx / 2, lz - fz / 2, lx + fx / 2, lz + fz / 2);
                if (!InRoom(rc)) return Rej("room", solid);
                if (InKeep(rc, solid, def.H)) return Rej("keep", solid);
                for (int i = 0; i < r.floor.Count; i++) if (r.floor[i].rc.Overlaps(rc, 0.012f)) return Rej("overlap", solid);      // (a hair under the 2 cm the checks allow, so a float rounding in the world frame cannot tip it over)
                var wrc = r.ToWorldRect(rc);
                foreach (var c in L.clear) if (wrc.Overlaps(c.Grow(solid ? 0.9f : 0.3f))) return Rej("clear", solid);
                foreach (var s in stops) if (wrc.Grow(solid ? 0.8f : 0f).Contains(s[0], s[1])) return Rej("stop", solid);
                if (solid && !SolidOk(wrc, def.H)) return Rej("solidok." + solidWhy, solid);
                if (tall && !loose && !Flush(wrc, solid)) return Rej("flush", solid);
                if (tall && MountBehind(wrc, def.H)) return Rej("mount", solid);
                var it = r.Put(key, lx, lz, yaw, 0f, false, false, -1);
                if (it == null) return null;
                Mark(it);
                if (solid)
                {
                    if (!ReachKept(it)) { RemoveDressed(it); return Rej("reach", solid); }
                    if (!WorldReachKept(it.piece.rc)) { RemoveDressed(it); reach = r.ComputeReach(); return Rej("worldreach", solid); }
                    AddSolid(it.piece.rc);
                }
                return it;
            }

            Item Rej(string why, bool solid)
            {
                if (solid) Stat("rej." + why, 1);
                if (DressDebug != null) DressDebug.Add(r.theme + " " + r.name + " " + lastKey + " " + why);
                return null;
            }

            void RemoveDressed(Item it)
            {
                r.RemoveItem(it);
                tops.RemoveAll(p => p == it.piece || p.host == it.piece);
                addedRoom--;
            }

            static int FaceQ(char side) => side == 'S' ? 0 : side == 'N' ? 2 : side == 'W' ? 1 : 3;

            /// <summary>Stands a thing against a wall of the room, facing in (the same geometry as RoomCtx.Wall), through the rules of <see cref="Floor"/>.</summary>
            Item WallFloor(char side, string key, float along, float gap = 0.02f)
            {
                if (!r.HasWall(side)) return null;
                var def = PropTable.Get(key);
                float pd = def.D, ly = 90f * FaceQ(side), lx, lz;
                switch (side)
                {
                    case 'S': lx = along; lz = -r.d / 2 + gap + pd / 2; break;
                    case 'N': lx = along; lz = r.d / 2 - gap - pd / 2; break;
                    case 'W': lx = -r.w / 2 + gap + pd / 2; lz = along; break;
                    default: lx = r.w / 2 - gap - pd / 2; lz = along; break;
                }
                return Floor(key, lx, lz, ly);
            }

            float WallLen(char side) => (side == 'N' || side == 'S') ? r.w : r.d;

            /// <summary>Tries a thing at places along one wall (in random order, every 15 cm) until one passes the rules.</summary>
            Item WallFloorAny(char side, string key, int tries = 40, float margin = 0.3f)
            {
                if (!r.HasWall(side)) return null;
                var def = PropTable.Get(key);
                float len = WallLen(side), half = def.W / 2f;
                var cand = new List<float>();
                for (float a = -len / 2 + half + margin; a <= len / 2 - half - margin + 1e-4f; a += 0.15f) cand.Add(a);
                Shuffle(cand);
                int n = 0;
                foreach (float a in cand)
                {
                    var it = WallFloor(side, key, a);
                    if (it != null) return it;
                    if (++n >= Math.Max(tries, 40)) break;
                }
                return null;
            }

            /// <summary>Tries a thing against each wall of the room in turn (random order).</summary>
            Item AnyWall(string key, float margin = 0.3f)
            {
                var sides = new List<char>(Sides4);
                Shuffle(sides);
                foreach (char sd in sides) { var it = WallFloorAny(sd, key, 40, margin); if (it != null) return it; }
                return null;
            }

            /// <summary>Tries the four corners of the room (a thing that stands in a corner, flush with both walls).</summary>
            Item CornerFloor(string key, bool needS = false)
            {
                var def = PropTable.Get(key);
                var order = new List<int> { 0, 1, 2, 3 };
                Shuffle(order);
                foreach (int c in order)
                {
                    float sx = (c & 1) == 0 ? -1f : 1f, sz = (c & 2) == 0 ? -1f : 1f;
                    char wx = sx < 0 ? 'W' : 'E', wz = sz < 0 ? 'S' : 'N';
                    if (!r.HasWall(wx) || !r.HasWall(wz)) continue;
                    if (needS && wz != 'S') continue;
                    float m = Math.Max(def.W, def.D) / 2f + 0.03f;
                    var it = Floor(key, sx * (r.w / 2 - m), sz * (r.d / 2 - m), 0f);
                    if (it != null) return it;
                }
                return null;
            }

            // ------------------------------------------------------------------ placing: on top of things

            /// <summary>Capacity of a surface for small valuables, worked out like ValuablePlan.FindSpots: how many spots it would take (at most 2) and whether a roomy one is left. <paramref name="extra"/> is a rectangle that would be added. It stops as soon as the answer is "2 and roomy".</summary>
            void Cap(Piece host, Rc? extra, out int n, out bool roomy)
            {
                n = 0; roomy = false;
                float top = host.y + host.h;
                const float Edge = 0.10f, Step = 0.05f, S = Valuables.SmallSpot, RW = 0.36f, RD = 0.30f, Apart = 0.50f;
                var inner = new Rc(host.rc.x0 + Edge, host.rc.z0 + Edge, host.rc.x1 - Edge, host.rc.z1 - Edge);
                if (inner.W < S - 0.01f || inner.D < S - 0.01f) return;
                var blocked = new List<Rc>();
                foreach (var o in tops) if (Math.Abs(o.y - top) < 0.08f && o.rc.Overlaps(host.rc, 0.001f)) blocked.Add(o.rc.Grow(0.04f));
                foreach (var s in L.mugSlots) if (s.host == host) blocked.Add(Rc.Centered(s.x, s.z, 0.34f, 0.34f));
                foreach (var s in L.bigSlots) if (s.host == host) blocked.Add(Rc.Centered(s.x, s.z, 0.90f, 0.90f));
                if (extra.HasValue) blocked.Add(extra.Value.Grow(0.04f));
                var chosen = new List<float[]>();
                for (float cz = inner.z0 + S * 0.5f; cz <= inner.z1 - S * 0.5f + 1e-4f; cz += Step)
                    for (float cx = inner.x0 + S * 0.5f; cx <= inner.x1 - S * 0.5f + 1e-4f; cx += Step)
                    {
                        if (!Fits(inner, blocked, cx, cz, S, S)) continue;
                        if (!roomy && (Fits(inner, blocked, cx, cz, RW, RD) || Fits(inner, blocked, cx, cz, RD, RW))) roomy = true;
                        if (n < 2)
                        {
                            bool nearOne = false;
                            foreach (var c in chosen) if (Math.Abs(c[0] - cx) < Apart && Math.Abs(c[1] - cz) < Apart) { nearOne = true; break; }
                            if (!nearOne) { chosen.Add(new[] { cx, cz }); n++; }
                        }
                        if (n >= 2 && roomy) return;
                    }
            }

            static bool Fits(Rc inner, List<Rc> blocked, float cx, float cz, float w, float d)
            {
                var rr = Rc.Centered(cx, cz, w, d);
                if (rr.x0 < inner.x0 - 1e-4f || rr.x1 > inner.x1 + 1e-4f || rr.z0 < inner.z0 - 1e-4f || rr.z1 > inner.z1 + 1e-4f) return false;
                for (int i = 0; i < blocked.Count; i++) if (blocked[i].Overlaps(rr, 0.002f)) return false;
                return true;
            }

            /// <summary>Puts a small thing on a desk, table, counter or cabinet (host-local u across its front, v along it, + towards the front). Nothing overlaps what is already there, no mug or treasure spot is covered, and a surface a valuable can lie on keeps room for as many as it had.</summary>
            Item OnTop(Item host, string key, float u, float v, float yawOff = 0f)
            {
                if (Full || host == null || host.piece == null || host.def.Lootable) return null;      // (a thing to steal is carried off with whatever stands on it)
                var def = PropTable.Get(key);
                float ang = yawOff * (float)Math.PI / 180f, c = Math.Abs((float)Math.Cos(ang)), s = Math.Abs((float)Math.Sin(ang));
                float bw = def.W * c + def.D * s, bd = def.W * s + def.D * c;
                float hw = host.def.W, hd = host.def.D;
                if (Math.Abs(u) + bw / 2 > hw / 2 - 0.02f || Math.Abs(v) + bd / 2 > hd / 2 - 0.02f) return null;
                int q = Quarter(host.localYaw);
                RoomCtx.RotQ(q, u, v, out var du, out var dv);
                float lx = host.lx + du, lz = host.lz + dv;
                float rw = (q & 1) == 0 ? bw : bd, rd = (q & 1) == 0 ? bd : bw;
                var wrc = r.ToWorldRect(new Rc(lx - rw / 2, lz - rd / 2, lx + rw / 2, lz + rd / 2));
                float top = host.Top;
                foreach (var p in tops) if (Math.Abs(p.y - top) < 0.08f && p.rc.Overlaps(wrc, 0.004f)) return null;
                foreach (var sl in L.mugSlots) if (ReferenceEquals(sl.host, host.piece) && Rc.Centered(sl.x, sl.z, 0.34f, 0.34f).Overlaps(wrc, 0.002f)) return null;
                foreach (var sl in L.bigSlots) if (ReferenceEquals(sl.host, host.piece) && Rc.Centered(sl.x, sl.z, 0.90f, 0.90f).Overlaps(wrc, 0.002f)) return null;
                if (ValuablePlan.SurfaceKeys.Contains(host.def.key) && !host.mount)
                {
                    if (!capBefore.TryGetValue(host.piece, out var cb))
                    {
                        Cap(host.piece, null, out int n0, out bool r0);
                        capBefore[host.piece] = cb = new[] { n0, r0 ? 1 : 0 };
                    }
                    Cap(host.piece, wrc, out int n1, out bool r1);
                    if (n1 < Math.Min(2, cb[0]) || (cb[1] == 1 && !r1)) return null;
                }
                var it = r.Put(key, lx, lz, host.localYaw + yawOff, top, false, false, 0);
                if (it == null) return null;
                it.piece.host = host.piece;
                tops.Add(it.piece);
                return Mark(it);
            }

            /// <summary>0 = the back half of the surface, 1 = the front half, 2 = anywhere.</summary>
            Item OnTopAny(Item host, string[] pool, int zone, int tries = 14, string forceKey = null)
            {
                if (host == null) return null;
                string key = forceKey ?? One(pool);
                var def = PropTable.Get(key);
                float hw = host.def.W, hd = host.def.D;
                for (int t = 0; t < tries; t++)
                {
                    float yo = Chance(0.5) ? Rnd(-25f, 25f) : 0f;
                    if (def.H < 0.1f && def.W > 0.15f && Chance(0.4)) yo = One(new[] { 0f, 90f, 180f, 20f, -20f, 70f });
                    float u = Rnd(-hw / 2 + def.W / 2 + 0.04f, hw / 2 - def.W / 2 - 0.04f);
                    float v;
                    if (zone == 0) v = -hd / 2 + def.D / 2 + 0.03f;
                    else if (zone == 1) v = Rnd(-hd * 0.1f, hd / 2 - def.D / 2 - 0.05f);
                    else v = Rnd(-hd / 2 + def.D / 2 + 0.03f, hd / 2 - def.D / 2 - 0.05f);
                    var it = OnTop(host, key, u, v, zone == 0 ? 0f : yo);
                    if (it != null) return it;
                }
                return null;
            }

            // ------------------------------------------------------------------ placing: on shelves

            void FillShelves(Item shelf, string[] pool, double density)
            {
                if (shelf == null || !Shelves.TryGetValue(shelf.def.key, out var sp)) return;
                for (int li = 0; li < sp.y.Length; li++) FillLevel(shelf, sp, shelf.elev, li, pool, density, false);
            }

            void FillLevel(Item shelf, ShelfSpec sp, float baseY, int li, string[] pool, double density, bool wallMounted)
            {
                float x = Rnd(0f, 0.08f), end = sp.w;
                int guard = 0;
                while (x < end - 0.1f && guard++ < 10)
                {
                    if (Full) return;
                    if (!Chance(density)) { x += Rnd(0.12f, 0.35f); continue; }
                    string key = null; float w = 0f, yaw = 0f;
                    for (int t = 0; t < 6; t++)
                    {
                        string k = One(pool);
                        var def = PropTable.Get(k);
                        if (def.H > sp.clear[li] + 0.005f) continue;
                        float yw = Chance(0.25) ? 90f : 0f;
                        float fw = yw == 0f ? def.W : def.D, fd = yw == 0f ? def.D : def.W;
                        if (x + fw > end + 0.005f || fd > sp.d) continue;
                        key = k; w = fw; yaw = yw; break;
                    }
                    if (key == null) { x += 0.2f; continue; }
                    OnShelf(shelf, sp, baseY, li, key, x + w / 2, yaw);
                    x += w + Rnd(0.01f, 0.1f);
                }
            }

            Item OnShelf(Item shelf, ShelfSpec sp, float baseY, int li, string key, float uFromLeft, float yawOff)
            {
                float u = uFromLeft - sp.w / 2;
                int q = Quarter(shelf.localYaw);
                RoomCtx.RotQ(q, u, sp.v, out var du, out var dv);
                var it = r.Put(key, shelf.lx + du, shelf.lz + dv, shelf.localYaw + yawOff, baseY + sp.y[li], false, false, 0);
                if (it == null) return null;
                it.piece.host = shelf.piece;
                tops.Add(it.piece);
                return Mark(it);
            }

            // ------------------------------------------------------------------ placing: on walls

            Rc MountRect(char side, PropDef def, float along)
            {
                float pw = def.W, pd = def.D, lx, lz;
                switch (side)
                {
                    case 'S': lx = along; lz = -r.d / 2 + pd / 2; break;
                    case 'N': lx = along; lz = r.d / 2 - pd / 2; break;
                    case 'W': lx = -r.w / 2 + pd / 2; lz = along; break;
                    default: lx = r.w / 2 - pd / 2; lz = along; break;
                }
                int q = Quarter(90f * FaceQ(side));
                float fx = (q & 1) == 0 ? pw : pd, fz = (q & 1) == 0 ? pd : pw;
                return new Rc(lx - fx / 2, lz - fz / 2, lx + fx / 2, lz + fz / 2);
            }

            /// <summary>Hangs something on a wall with its middle at the given height. Nothing goes where a glass front could be, nothing hangs over a thing standing on the furniture in front of it, and a corridor wall keeps clear of the direction signs.</summary>
            Item MountOn(char side, string key, float along, float mid)
            {
                if (Full || !r.HasWall(side)) return null;
                var def = PropTable.Get(key);
                if (r.kind == 2) mid = Math.Min(mid, 1.93f - def.H / 2f);
                float under = mid - def.H / 2f, top = under + def.H;
                if (under < 0.05f || top > 3.2f) return null;
                if (r.kind == 2 && top > 1.95f) return null;
                var lrc = MountRect(side, def, along);
                var wrc = r.ToWorldRect(lrc);
                foreach (var z in noMount) if (z.Overlaps(wrc)) return null;
                var deep = r.ToWorldRect(lrc.Grow(0.4f));
                foreach (var p in tops) if (p.y + p.h > under - 0.02f && p.rc.Overlaps(deep, 0.01f)) return null;
                var it = r.Mount(side, key, along, under, 0f, 0f);
                return Mark(it);
            }

            /// <summary>Hangs things along a wall one after another with gaps between (not evenly: a believable wall).</summary>
            int DecorWall(char side, string[] pool, double fill, float midLo, float midHi, float gapLo = 0.3f, float gapHi = 1.2f, int max = 4)
            {
                if (!r.HasWall(side)) return 0;
                float len = WallLen(side), cur = -len / 2 + Rnd(0.35f, 0.9f);
                int n = 0, guard = 0;
                string prev = null;
                while (cur < len / 2 - 0.4f && n < max && guard++ < 24)
                {
                    if (Full) break;
                    if (!Chance(fill)) { cur += Rnd(0.5f, 1.2f); continue; }
                    // variety: not the same picture twice in a row, and one clock a room is plenty
                    string key = One(pool);
                    for (int t = 0; t < 4 && pool.Length > 1 && (key == prev || (key == "clock" && clocks > 0)); t++) key = One(pool);
                    var def = PropTable.Get(key);
                    float mid = Rnd(midLo, midHi);
                    if (def.H > 1.2f) mid = Math.Min(mid, 1.3f);
                    float along = cur + def.W / 2f;
                    if (along + def.W / 2f > len / 2 - 0.3f) break;
                    var it = MountOn(side, key, along, mid);
                    if (it != null) { n++; prev = key; if (key == "clock") clocks++; cur += def.W + Rnd(gapLo, gapHi); DressWallShelf(it); }
                    else cur += 0.3f;
                }
                return n;
            }

            /// <summary>A wall shelf gets books, a plant, a trophy or a lamp on its boards.</summary>
            string[] wallShelfPool = PoolBookDeco;

            void DressWallShelf(Item shelf)
            {
                if (shelf == null || !WallShelves.TryGetValue(shelf.def.key, out var sp)) return;
                for (int li = sp.y.Length - 1; li >= 0; li--)
                {
                    if (li < sp.y.Length - 1 && !Chance(0.7)) continue;
                    FillLevel(shelf, sp, shelf.elev, li, wallShelfPool, 0.85, true);
                }
            }

            // ------------------------------------------------------------------ placing: rugs

            static bool RugFriendly(string key) => key.StartsWith("desk_") || key.StartsWith("chair_") || key.StartsWith("sofa") || key == "armchair" || key == "lounge_chair" || key.StartsWith("coffee_table") || key.StartsWith("table_")
                                                  || key.StartsWith("conf_table") || key == "fatboy" || key.StartsWith("beanbag") || key == "stool" || key == "cs_table" || key == "cs_chair" || key == "drafting_table";

            bool Rug(string key, float lx, float lz, float yaw)
            {
                var def = PropTable.Get(key);
                int q = Quarter(yaw);
                float fx = (q & 1) == 0 ? def.W : def.D, fz = (q & 1) == 0 ? def.D : def.W;
                var rc = new Rc(lx - fx / 2, lz - fz / 2, lx + fx / 2, lz + fz / 2);
                if (!InRoom(rc, 0.12f)) return false;
                for (int i = 0; i < r.keep.Count; i++) if (r.keep[i].Overlaps(rc, 0.02f)) return false;
                for (int i = 0; i < r.blocks.Count; i++) if (r.blocks[i].Overlaps(rc)) return false;
                for (int i = 0; i < r.lanes.Count; i++) if (r.lanes[i].Overlaps(rc)) return false;
                foreach (var o in rugs) if (o.Overlaps(rc)) return false;
                var wr = r.ToWorldRect(rc);
                foreach (var c in L.clear) if (wr.Overlaps(c)) return false;
                for (int i = 0; i < r.floor.Count; i++)
                    if (r.floor[i].rc.Overlaps(rc, 0.02f) && !RugFriendly(r.floor[i].def.key)) return false;
                var it = r.Put(key, lx, lz, yaw, 0f, false, false, 0);
                if (it == null) return false;
                Mark(it);
                rugs.Add(rc);
                return true;
            }

            /// <summary>The first of the rugs that fits with its middle near (cx, cz).</summary>
            bool RugNear(float cx, float cz, params string[] keys) => RugNearYaw(cx, cz, new[] { 0f, 90f }, keys);

            bool RugNearYaw(float cx, float cz, float[] yaws, params string[] keys)
            {
                var offs = new[] { new[] { 0f, 0f }, new[] { 0.25f, 0f }, new[] { -0.25f, 0f }, new[] { 0f, 0.25f }, new[] { 0f, -0.25f }, new[] { 0.5f, 0.5f }, new[] { -0.5f, 0.5f }, new[] { 0.5f, -0.5f }, new[] { -0.5f, -0.5f } };
                foreach (var key in keys)
                    foreach (float yaw in yaws)
                        foreach (var o in offs)
                            if (Rug(key, cx + o[0], cz + o[1], yaw)) return true;
                return false;
            }

            // ------------------------------------------------------------------ finding what is there

            List<Item> Find(params string[] keys)
            {
                var res = new List<Item>();
                foreach (var it in r.floor)
                    foreach (var k in keys) if (it.def.key == k) { res.Add(it); break; }
                return res;
            }

            List<Item> FindStarting(string prefix)
            {
                var res = new List<Item>();
                foreach (var it in r.floor) if (it.def.key.StartsWith(prefix)) res.Add(it);
                return res;
            }

            static bool IsDesk(Item it) => it.def.key.StartsWith("desk_") && it.def.key != "desk_reception";

            // ------------------------------------------------------------------ what every room gets

            /// <summary>Clutter for a desk: tall things along the back edge, paper in front, a cup or two.</summary>
            void DeskDress(Item desk, int front, int back, double cup, string[] cups = null)
            {
                for (int i = 0; i < back; i++) OnTopAny(desk, PoolDeskBack, 0);
                for (int i = 0; i < front; i++) OnTopAny(desk, PoolDeskFront, 1);
                if (Chance(cup)) OnTopAny(desk, cups ?? PoolCups, 2);
                if (Chance(cup * 0.3)) OnTopAny(desk, cups ?? PoolCups, 2);
            }

            /// <summary>Fills the shelves of the room with books, folders and knick-knacks: at most <paramref name="share"/> of the room's budget, spread over all the shelves.</summary>
            void Shelving(double bookDensity = 0.8, string[] pool = null, double share = 0.3)
            {
                var shelves = Find("bookshelf", "shelf", "shelf_wood", "kshelve", "shelf_wide", "kitchen_unit", "shelf_4");
                int levels = 0;
                foreach (var sh in shelves) if (Shelves.TryGetValue(sh.def.key, out var sp)) levels += sp.y.Length;
                if (levels == 0) return;
                int want = Math.Max(2, (int)Math.Round(budget * share));
                double d = Math.Min(bookDensity, Math.Max(0.2, want / (levels * 2.4)));
                int saved = cap;
                cap = Math.Min(budget, addedRoom + want);
                Shuffle(shelves);
                foreach (var sh in shelves) FillShelves(sh, pool ?? PoolBookDeco, d);
                cap = saved;
            }

            /// <summary>A plant or a book pile on top of the cabinets (at eye height: the little things).</summary>
            void CabinetTops(double chance)
            {
                foreach (var cab in Find("cabinet_med", "cabinet_small", "shelf_box", "kstorage"))
                {
                    if (!Chance(chance)) continue;
                    OnTopAny(cab, new[] { "plant_desk", "plant_tiny", "book_pile_1", "book_pile_5", "photo_2", "photo_3", "cup_pens", "paper_pile_2", "plant_desk", "figure_knight" }, 2, 6);
                }
            }

            void Switches(int n)
            {
                // a light switch and a socket by the door and by the desks: the little things that make a wall look used
                char[] sides = { 'S', 'N', 'W', 'E' };
                for (int i = 0; i < n; i++)
                {
                    char side = One(sides);
                    if (!r.HasWall(side)) continue;
                    float len = WallLen(side);
                    MountOn(side, i == 0 ? "switch_1" : "socket_1", Rnd(-len / 2 + 0.6f, len / 2 - 0.6f), i == 0 ? 1.2f : 0.35f);
                }
            }

            // ------------------------------------------------------------------ offices

            void DOffice()
            {
                bool exec = r.w >= 5f && r.d >= 5f;
                int persona = rnd.Next(4);               // 0 tidy, 1 busy, 2 green, 3 personal
                Shelving(persona == 1 ? 0.9 : 0.75, null, 0.3);

                // the floor first (the few big things): a coat rack, a plant, a bin by the desk
                if (Chance(0.5)) CornerFloor("coat_rack");
                if (persona == 2) { AnyWall("plant_flowers_2"); CornerFloor("plant_big"); }
                else if (Chance(0.4)) CornerFloor(One(new[] { "plant_tall", "plant_floor", "plant_big" }));
                if (exec && Chance(0.25)) CornerFloor("swiss_ball");
                if (Chance(0.65)) { foreach (var desk in Find("desk_1", "desk_2", "desk_3")) { if (BinBeside(desk)) break; } }

                // the desks: tall things at the back, paper in front, a cup
                foreach (var desk in Find("desk_1", "desk_2", "desk_3"))
                {
                    if (exec) { DeskDress(desk, 2 + (persona == 1 ? 1 : 0), 1 + (persona == 3 ? 1 : 0), 0.9, new[] { "cup_boss", "cup_red", "cup_coffee_open", "cup_boss" }); OnTopAny(desk, new[] { "globe", "cradle", "orbiter", "figure_knight", "neon_cactus" }, 0, 8); }
                    else DeskDress(desk, 1 + (persona == 1 ? 2 : 1), persona == 2 ? 2 : 1, 0.75);
                }
                foreach (var cred in Find("credenza"))
                {
                    if (exec) { OnTopAny(cred, new[] { "typewriter", "globe", "cradle", "orbiter", "trophy_cup", "figure_viking", "radio_1" }, 0, 8); OnTopAny(cred, new[] { "photo_3", "photo_4", "photo_5", "plant_desk", "book_pile_1" }, 2, 8); }
                    else OnTopAny(cred, new[] { "plant_desk", "photo_2", "book_pile_5", "cup_pens" }, 2, 8);
                    FillShelves(cred, PoolArchive, 0.5);
                }
                foreach (var t in Find("table_box", "coffee_table", "coffee_table2")) { OnTopAny(t, new[] { "magazine_1", "magazine_2", "book_pile_1", "cup_red", "cup_coffee_open", "paper_2" }, 2, 6); if (Chance(0.5)) OnTopAny(t, new[] { "cup_coffee_open", "cup_red", "magazine_2" }, 2, 6); }
                CabinetTops(persona == 2 ? 0.8 : 0.45);

                // walls: pictures, a certificate, now and then a wall shelf or a board; the glass-front walls are left alone (MountOn)
                Walls(exec ? 0.5 : 0.4, 0.5, persona == 3 ? 0.7 : 0.35);
                Vent(0.4);
                Switches(2);

                // a rug under the desk chair (a big one for an executive), or by the sofa
                if (exec)
                {
                    var desk = Find("desk_3");
                    if (desk.Count > 0) RugNear(desk[0].lx, desk[0].lz - 0.5f, "rug_1", "rug_2", "rug_3");
                    var sofa = Find("sofa1", "sofa2");
                    if (sofa.Count > 0 && rugs.Count == 0) RugNear(sofa[0].lx + (sofa[0].lx > 0 ? -1f : 1f), sofa[0].lz, "rug_2", "rug_3");
                }
                else if (Chance(0.5))
                {
                    var desk = Find("desk_1", "desk_2", "desk_3");
                    if (desk.Count > 0) RugNear(desk[0].lx, desk[0].lz + (desk[0].lz > 0 ? -0.8f : 0.8f), "rug_3", "rug_2");
                }
            }

            /// <summary>A bin beside a desk (it has no collider; it stands where the legs end).</summary>
            bool BinBeside(Item desk)
            {
                string key = One(new[] { "bin_rubbish", "bin_tall", "trash_small" });
                var def = PropTable.Get(key);
                int q = Quarter(desk.localYaw);
                foreach (float side in new[] { 1f, -1f })
                {
                    RoomCtx.RotQ(q, side * (desk.def.W / 2 + def.W / 2 + 0.03f), -desk.def.D / 2 + def.D / 2 + 0.05f, out var du, out var dv);
                    if (Floor(key, desk.lx + du, desk.lz + dv, 0f) != null) return true;
                }
                return false;
            }

            // ------------------------------------------------------------------ shared pieces of the recipes

            static readonly char[] Sides4 = { 'S', 'N', 'W', 'E' };
            static readonly string[] Shelfy = { "shelf_wall_2", "shelf_wall_3", "wall_shelf" };

            /// <summary>Pictures, boards and clocks on the free bits of the walls (a believable wall: some bare, some busy). <paramref name="big"/> and <paramref name="small"/> are the pools; the three chances say how full a wall gets.</summary>
            void Walls(double bigFill, double smallFill, double shelfChance, string[] big = null, string[] small = null)
            {
                big = big ?? PoolWallBig; small = small ?? PoolWallSmall;
                var sides = new List<char>(Sides4);
                Shuffle(sides);
                foreach (char side in sides)
                {
                    if (!r.HasWall(side)) continue;
                    int max = WallLen(side) > 5f ? 3 : 2;
                    DecorWall(side, big, bigFill, 1.4f, 1.7f, 0.4f, 1.4f, max);
                    DecorWall(side, small, smallFill, 1.5f, 1.8f, 0.3f, 1.0f, max);
                    if (shelfChance > 0 && Chance(shelfChance)) DecorWall(side, Shelfy, 0.8, 1.45f, 1.65f, 0.3f, 1.0f, 1);
                }
            }

            /// <summary>A bin against a wall (it has no collider, like the planner's own).</summary>
            bool BinAgainstWall(string[] keys = null)
            {
                string key = One(keys ?? new[] { "bin_rubbish", "bin_tall", "trash_small" });
                var sides = new List<char>(Sides4);
                Shuffle(sides);
                foreach (char side in sides) if (WallFloorAny(side, key, 8, 0.4f) != null) return true;
                return false;
            }

            /// <summary>Now and then an air vent high on a wall.</summary>
            void Vent(double chance)
            {
                if (r.kind != 0 || !Chance(chance)) return;
                MountOn(One(new[] { 'N', 'E', 'W', 'S' }), "air_vent", Rnd(-1.2f, 1.2f), 2.75f);
            }

            /// <summary>Things on the counters and tables of a kitchen: a fruit bowl, a cake, the lunch somebody left.</summary>
            void KitchenTops(Item host, int n, string[] pool)
            {
                for (int i = 0; i < n; i++) OnTopAny(host, pool, i == 0 ? 2 : 1, 10);
            }

            /// <summary>A place setting at every chair round a table: a paper or a folder in front of it, a cup beside it, turned to face the chair.</summary>
            void SeatSettings(Item t)
            {
                int q = Quarter(t.localYaw);
                float hw = t.def.W, hd = t.def.D;
                var seats = new List<float[]>();
                foreach (var c in r.floor)
                {
                    if (c.def.cat != PropCat.Chair) continue;
                    RoomCtx.RotQ(-q, c.lx - t.lx, c.lz - t.lz, out var u, out var v);
                    if (Math.Abs(u) > hw / 2 + 0.8f || Math.Abs(v) > hd / 2 + 0.8f) continue;
                    seats.Add(new[] { u, v });
                }
                foreach (var s in seats)
                {
                    float u = s[0], v = s[1];
                    float pu = Math.Max(-(hw / 2 - 0.30f), Math.Min(hw / 2 - 0.30f, u)), pv = Math.Max(-(hd / 2 - 0.30f), Math.Min(hd / 2 - 0.30f, v));
                    float yaw;
                    if (Math.Abs(v) - hd / 2 > Math.Abs(u) - hw / 2) yaw = v > 0 ? 0f : 180f; else yaw = u > 0 ? 90f : 270f;
                    if (Chance(0.8)) OnTop(t, One(PoolPaper), pu + Rnd(-0.06f, 0.06f), pv + Rnd(-0.05f, 0.05f), yaw + Rnd(-12f, 12f));
                    if (Chance(0.5))
                    {
                        // the cup goes at the right hand of the seat
                        float side = (Math.Abs(v) - hd / 2 > Math.Abs(u) - hw / 2) ? (v > 0 ? -1f : 1f) : (u > 0 ? 1f : -1f);
                        float cu = (Math.Abs(v) - hd / 2 > Math.Abs(u) - hw / 2) ? pu + side * 0.38f : pu;
                        float cv = (Math.Abs(v) - hd / 2 > Math.Abs(u) - hw / 2) ? pv : pv + side * 0.38f;
                        OnTop(t, One(PoolCups), cu, cv, Rnd(0f, 360f));
                    }
                }
            }

            /// <summary>Notes, a certificate or a calendar pinned on a cubicle wall (the faces nobody has put anything against), above the desk and beside the screen.</summary>
            void PinNotes(double chance)
            {
                var panels = new List<Item>();
                foreach (var it in r.floor) if (it.def.key == "cube_wall" || it.def.key == "cube_wall_tall") panels.Add(it);
                var pinned = new List<Rc>();
                foreach (var w in panels)
                {
                    if (Full) return;
                    bool alongX = w.rc.W > w.rc.D;
                    float len = alongX ? w.rc.W : w.rc.D, th = alongX ? w.rc.D : w.rc.W;
                    float wallTop = w.h;
                    foreach (int face in new[] { -1, 1 })
                    {
                        if (!Chance(chance)) continue;
                        string key = One(PoolPins);
                        var def = PropTable.Get(key);
                        float under = wallTop - 0.04f - def.H;
                        if (under < 0.85f) continue;
                        var cand = new List<float>();
                        for (float a = -len / 2 + def.W / 2 + 0.2f; a <= len / 2 - def.W / 2 - 0.2f; a += 0.25f) cand.Add(a);
                        Shuffle(cand);
                        foreach (float a in cand)
                        {
                            float lx, lz, ly;
                            if (alongX) { lx = w.lx + a; lz = w.lz + face * (th / 2 + def.D / 2 + 0.004f); ly = face > 0 ? 0f : 180f; }
                            else { lz = w.lz + a; lx = w.lx + face * (th / 2 + def.D / 2 + 0.004f); ly = face > 0 ? 90f : 270f; }
                            var rect = new Rc(lx - (alongX ? def.W : def.D) / 2, lz - (alongX ? def.D : def.W) / 2, lx + (alongX ? def.W : def.D) / 2, lz + (alongX ? def.D : def.W) / 2);
                            // anything tall right in front (a half wall, a cabinet) or on the desk in front (a screen) is in the way
                            var zone = alongX ? new Rc(rect.x0 - 0.05f, face > 0 ? w.rc.z1 : w.rc.z0 - 0.4f, rect.x1 + 0.05f, face > 0 ? w.rc.z1 + 0.4f : w.rc.z0)
                                              : new Rc(face > 0 ? w.rc.x1 : w.rc.x0 - 0.4f, rect.z0 - 0.05f, face > 0 ? w.rc.x1 + 0.4f : w.rc.x0, rect.z1 + 0.05f);
                            bool blocked = false;
                            foreach (var f in r.floor) if (!ReferenceEquals(f, w) && f.solid && f.h > 0.88f && f.rc.Overlaps(zone, 0.02f)) { blocked = true; break; }
                            if (!blocked) foreach (var p in tops) if (p.y + p.h > under - 0.02f && p.rc.Overlaps(r.ToWorldRect(zone), 0.01f)) { blocked = true; break; }
                            if (!blocked) foreach (var o in pinned) if (o.Overlaps(rect, 0.01f)) { blocked = true; break; }
                            if (blocked) continue;
                            if (rect.x0 < -r.w / 2 || rect.x1 > r.w / 2 || rect.z0 < -r.d / 2 || rect.z1 > r.d / 2) continue;
                            var it = r.Put(key, lx, lz, ly, under, false, true, 0);
                            if (it == null) continue;
                            Mark(it);
                            pinned.Add(rect);
                            break;
                        }
                    }
                }
            }

            // ------------------------------------------------------------------ meeting rooms and training rooms

            void DConference()
            {
                foreach (var t in Find("conf_table", "conf_table2"))
                {
                    SeatSettings(t);
                    OnTopAny(t, null, 2, 12, "phone_conf");
                    if (Chance(0.4)) OnTopAny(t, null, 2, 12, "coffee_tray");
                }
                foreach (var cred in Find("credenza"))
                {
                    FillShelves(cred, PoolArchive, 0.4);
                    OnTopAny(cred, new[] { "photo_3", "plant_desk", "book_pile_1", "paper_tray_full", "photo_4" }, 2, 8);
                }
                CabinetTops(0.5);
                Walls(0.35, 0.4, 0.2);
                Vent(0.7);
                Switches(2);
                if (Chance(0.55)) CornerFloor("whiteboard_easel");
                if (Chance(0.5)) BinAgainstWall();
            }

            void DTraining()
            {
                foreach (var d in Find("desk_1"))
                {
                    if (Chance(0.75)) OnTopAny(d, PoolPaper, 1, 8);
                    if (Chance(0.3)) OnTopAny(d, PoolCups, 2, 8);
                    if (Chance(0.2)) OnTopAny(d, PoolDeskBack, 0, 6);
                }
                foreach (var d in Find("desk_2")) DeskDress(d, 3, 2, 1.0);
                Shelving(0.85);
                CabinetTops(0.6);
                Walls(0.5, 0.5, 0.15, PoolWallTrain, PoolWallSmall);
                Vent(0.6);
                Switches(2);
                if (Chance(0.6)) CornerFloor("whiteboard_easel");
                if (Chance(0.45)) CornerFloor("ohp");
                BinAgainstWall();
                if (Chance(0.4)) BinAgainstWall();
            }

            // ------------------------------------------------------------------ the break room and the canteen

            void DBreak()
            {
                wallShelfPool = PoolKitchenShelf;
                foreach (var c in Find("counter_a1", "counter_a2", "counter_b1", "counter_b2")) KitchenTops(c, 2, PoolKitchen);
                foreach (var t in Find("cs_table", "table_round", "table_round2", "table_box")) KitchenTops(t, Chance(0.7) ? 2 : 1, PoolTableFood);
                foreach (var t in Find("coffee_table", "coffee_table2")) { OnTopAny(t, PoolLounge, 2, 8); if (Chance(0.5)) OnTopAny(t, PoolLounge, 2, 8); }
                Walls(0.3, 0.4, 0.6, PoolWallCanteen, PoolWallSmall);
                if (r.kind == 0 && Chance(0.25)) MountOn(One(new[] { 'N', 'E', 'W' }), One(new[] { "bunting_1", "bunting_2" }), Rnd(-0.8f, 0.8f), 2.25f);
                Vent(0.5);
                Switches(2);
                BinAgainstWall(new[] { "bin_tall", "bin_rubbish" });
                if (Chance(0.6)) BinAgainstWall(new[] { "bin_rubbish", "bin_tall" });
                if (Chance(0.5)) AnyWall("wet_floor", 0.4f);
                foreach (var sofa in Find("sofa_wood", "sofa2", "sofa3"))
                {
                    var table = Find("coffee_table", "coffee_table2");
                    if (table.Count > 0) { RugNear(table[0].lx, table[0].lz, "rug_2", "rug_3"); break; }
                }
            }

            void DCanteen()
            {
                wallShelfPool = PoolKitchenShelf;
                foreach (var c in Find("counter_a1", "counter_a2", "counter_b1", "counter_b2")) KitchenTops(c, 2, PoolKitchen);
                foreach (var t in Find("cs_table")) KitchenTops(t, Chance(0.6) ? 2 : 1, PoolTableFood);
                Walls(0.4, 0.5, 0.0, PoolWallCanteen, PoolWallSmall);
                if (r.kind == 0 && Chance(0.5)) MountOn('N', One(new[] { "bunting_1", "bunting_2" }), Rnd(-3f, 3f), 2.3f);
                Vent(0.6);
                Switches(2);
                BinAgainstWall(new[] { "bin_tall", "bin_rubbish" });
                BinAgainstWall(new[] { "bin_rubbish", "bin_tall" });
                if (Chance(0.6)) BinAgainstWall(new[] { "bin_tall" });
                if (Chance(0.6)) AnyWall("wet_floor", 0.4f);
                if (Chance(0.4)) AnyWall("cart_2", 0.5f);
            }

            // ------------------------------------------------------------------ storage, copy room, server room

            void DStorage()
            {
                foreach (var s in Find("shelf", "kshelve", "shelf_wide", "kitchen_unit", "shelf_wood", "bookshelf", "shelf_4")) FillShelves(s, PoolStore, 0.8);
                foreach (var cab in Find("cabinet_med", "cabinet_small", "shelf_box", "kstorage"))
                    if (Chance(0.6)) OnTopAny(cab, new[] { "box_small", "paper_tray_full", "book_pile_2", "file_holder", "paper_pile_3", "folder_tray_2" }, 2, 6);
                // the cleaner's corner and whatever is stacked against the walls
                var janitor = new List<string> { "mop_1", "mop_2", "bucket_1", "broom", "wet_floor" };
                Shuffle(janitor);
                int jn = 2 + rnd.Next(2);
                for (int i = 0; i < jn && i < janitor.Count; i++)
                {
                    if (janitor[i] == "mop_1" || janitor[i] == "bucket_1") CornerFloor(janitor[i]);
                    else AnyWall(janitor[i], 0.3f);
                }
                if (Chance(0.55)) AnyWall("vacuum", 0.4f);
                if (Chance(0.5)) AnyWall("pallet_loaded", 0.4f);
                if (Chance(0.6)) AnyWall("box_stack", 0.4f);
                if (Chance(0.4)) AnyWall("cart_2", 0.4f);
                if (Chance(0.4)) AnyWall("janitor_cart", 0.4f);
                Walls(0.2, 0.4, 0.0, PoolWallStore, PoolWallSmall);
                Vent(0.7);
                Switches(1);
            }

            void DCopy()
            {
                foreach (var t in Find("table_box", "table_long", "table_big"))
                {
                    OnTopAny(t, null, 2, 8, One(new[] { "fax_machine", "paper_tray_full", "guillotine", "folder_tray_2" }));
                    OnTopAny(t, new[] { "holepunch", "scissors", "paper_tray_empty", "paper_pile_2", "folder_1", "tape" }, 2, 8);
                }
                foreach (var cab in Find("cabinet_med", "cabinet_small", "shelf_box", "kstorage"))
                    if (Chance(0.6)) OnTopAny(cab, new[] { "paper_tray_full", "paper_tray_empty", "folder_tray_2", "paper_pile_3", "box_small", "plant_desk" }, 2, 6);
                foreach (var s in Find("shelf", "bookshelf", "kshelve")) FillShelves(s, PoolStore, 0.8);
                // sheets on the floor by the printers and a bin for them
                foreach (var p in Find("printer", "copier"))
                {
                    if (!Chance(0.4)) continue;
                    float fx = (float)Math.Sin(p.localYaw * Math.PI / 180.0), fz = (float)Math.Cos(p.localYaw * Math.PI / 180.0);
                    float off = p.def.D / 2 + 0.45f;
                    Floor(One(new[] { "papers_floor_1", "papers_floor_2" }), p.lx + fx * off, p.lz + fz * off, Rnd(0f, 360f) > 180f ? 0f : 90f);
                }
                BinAgainstWall(new[] { "bin_tall", "bin_rubbish" });
                if (Chance(0.6)) BinAgainstWall(new[] { "bin_rubbish", "bin_tall" });
                Walls(0.3, 0.4, 0.0, PoolWallStore, PoolWallSmall);
                Vent(0.6);
                Switches(1);
            }

            void DServer()
            {
                // an IT bench against the door wall with a laptop and spare gear, a fan in a corner, cables on the floor in front of a rack, a card reader by the door
                var bench = WallFloorAny('S', "table_long", 14, 0.5f);
                if (bench != null)
                {
                    OnTopAny(bench, null, 2, 8, One(new[] { "laptop_2", "laptop_folded" }));
                    OnTopAny(bench, null, 2, 8, "monitor_2");
                    OnTopAny(bench, null, 2, 8, One(new[] { "server_node_2", "server_node_3", "server_node_1" }));
                    OnTopAny(bench, new[] { "net_cable", "modem", "keyboard_2", "cup_red", "mousepad", "notepad_2", "headset" }, 2, 8);
                    OnTopAny(bench, new[] { "net_cable", "modem", "cup_coffee_open", "can_soda", "paper_2" }, 2, 8);
                }
                foreach (var rack in Find("server_rack", "server_rack2"))
                {
                    if (!Chance(0.25)) continue;
                    float fx = (float)Math.Sin(rack.localYaw * Math.PI / 180.0), fz = (float)Math.Cos(rack.localYaw * Math.PI / 180.0);
                    float off = rack.def.D / 2 + 0.38f;
                    Floor("cord_pile", rack.lx + fx * off, rack.lz + fz * off, (Quarter(rack.localYaw) & 1) == 0 ? 0f : 90f);
                }
                if (Chance(0.4)) CornerFloor("fan_stand");
                foreach (var d in r.doors)
                {
                    foreach (float sgn in new[] { 1f, -1f })
                        if (MountOn('S', "card_scanner", d[0] + sgn * (d[1] / 2 + 0.3f), 1.25f) != null) break;
                }
                Vent(0.8);
                MountOn(One(new[] { 'N', 'E', 'W' }), "air_vent", Rnd(-1.5f, 1.5f), 2.75f);
                Walls(0.15, 0.3, 0.0, PoolWallServer, PoolWallServer);
                Switches(1);
            }

            // ------------------------------------------------------------------ lounges and the reading nook

            void DLounge()
            {
                wallShelfPool = PoolBookDeco;
                foreach (var t in Find("coffee_table", "coffee_table2", "coffee_table3"))
                {
                    OnTopAny(t, PoolLounge, 2, 8);
                    if (Chance(0.6)) OnTopAny(t, PoolLounge, 2, 8);
                }
                foreach (var cred in Find("credenza"))
                {
                    FillShelves(cred, new[] { "book_row_1", "book_row_2", "magazine_1", "photo_2", "plant_desk", "book_pile_1" }, 0.5);
                    OnTopAny(cred, new[] { "radio_1", "headphones", "plant_desk", "photo_3", "book_pile_1", "neon_cactus" }, 2, 8);
                }
                Shelving(0.8);
                CabinetTops(0.5);
                Walls(0.5, 0.5, 0.5);
                Vent(0.4);
                Switches(2);
                // one thing to play with, against a wall, and a spare bean bag
                if (Chance(0.75))
                {
                    var games = new List<string> { "arcade", "jukebox", "fishtank", "table_football" };
                    Shuffle(games);
                    foreach (var g in games) if (AnyWall(g, 0.4f) != null) break;
                }
                if (Chance(0.5)) CornerFloor(One(new[] { "beanbag_2", "beanbag_3" }));
                if (Chance(0.5)) CornerFloor("lamp");
                if (Chance(0.4)) AnyWall(One(new[] { "plant_flowers_2", "plant_floor" }), 0.4f);
                var tbl = Find("coffee_table", "coffee_table2", "coffee_table3");
                if (tbl.Count > 0) RugNear(tbl[0].lx, tbl[0].lz, "rug_1", "rug_2", "rug_3", "rug_4");
            }

            void DNook()
            {
                Shelving(0.9);
                CabinetTops(0.7);
                foreach (var t in Find("coffee_table", "coffee_table2")) OnTopAny(t, PoolLounge, 2, 8);
                Walls(0.4, 0.5, 0.6);
                Switches(1);
                if (Chance(0.5)) CornerFloor("lamp");
                var tbl = Find("coffee_table", "coffee_table2", "coffee_table3");
                if (tbl.Count > 0) RugNear(tbl[0].lx, tbl[0].lz, "rug_3", "rug_4", "rug_2");
            }

            // ------------------------------------------------------------------ open plan, halls, junctions, the lobby

            void DOpen()
            {
                foreach (var desk in Find("desk_1", "desk_2", "desk_3")) DeskDress(desk, Chance(0.7) ? 1 : 2, Chance(0.6) ? 1 : 0, 0.55);
                Shelving(0.8, PoolArchive);
                CabinetTops(0.6);
                PinNotes(0.5);
                Walls(0.4, 0.5, 0.15, PoolWallOpen, PoolWallSmall);
                Vent(0.5);
                Switches(2);
                if (Chance(0.6)) CornerFloor("coat_rack");
                foreach (var desk in Find("desk_1", "desk_2", "desk_3")) if (Chance(0.2)) BinBeside(desk);
                if (Chance(0.5)) AnyWall("plant_flowers_2", 0.4f);
            }

            void DHall()
            {
                foreach (var s in Find("bookshelf", "shelf")) FillShelves(s, PoolBookDeco, 0.75);
                CabinetTops(0.7);
                if (!r.openLane) Walls(r.longHall ? 0.3 : 0.35, 0.4, 0.0, PoolWallHall, PoolWallHallSmall);
                if (!r.openLane && Chance(0.12)) RugNearYaw(0f, 0f, r.longHall ? new[] { 90f } : new[] { 0f }, "rug_4", "rug_3");
            }

            void DHub()
            {
                if (Chance(0.35)) RugNear(0f, 0f, "rug_1", "rug_2");
            }

            void DLobby()
            {
                foreach (var t in Find("coffee_table"))
                {
                    OnTopAny(t, PoolLounge, 2, 8);
                    if (Chance(0.5)) OnTopAny(t, PoolLounge, 2, 8);
                }
                foreach (var d in Find("desk_reception"))
                {
                    for (int i = 0; i < 4; i++) OnTopAny(d, i == 0 ? PoolDeskBack : PoolReception, i == 0 ? 0 : 2, 10);
                }
                CabinetTops(0.8);
                Walls(0.35, 0.4, 0.0, PoolWallHall, PoolWallHallSmall);
                foreach (var t in Find("coffee_table")) RugNear(t.lx, t.lz, "rug_1", "rug_2", "rug_3");
            }
        }
    }
}
