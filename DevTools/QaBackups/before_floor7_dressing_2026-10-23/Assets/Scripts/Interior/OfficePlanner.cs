using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>What the planner needs to know about the floor (all of it comes from the seed, so every machine agrees).</summary>
    public sealed class PlanInput
    {
        public int seed, floor, cols, rows;
        public float cell = 18f, wallH = 3.5f;
        /// <summary>openE[c, r]: door gap between (c, r) and (c+1, r). openN[c, r]: between (c, r) and (c, r+1).</summary>
        public bool[,] openE, openN;
        public int elevC, elevR;
        /// <summary>The service elevator's door faces east (otherwise north).</summary>
        public bool elevDoorEast = true;
        /// <summary>Floor 12: the hidden back-door hallway behind the shaft (west side if the door is east, else south).</summary>
        public bool hiddenHall;
        /// <summary>The cell of the guards' own elevator (the corner opposite the players' elevator, the same cell on every floor), and which neighbouring cell its door faces (-1,0 = west ...).</summary>
        public int guestC, guestR;
        public int guestDirX = -1, guestDirY;
        /// <summary>The cells of the stairwells (the corners that are neither elevator's; none on a floor too small for them) and which neighbouring cell each door faces.</summary>
        public int[] stairC, stairR, stairDirX, stairDirY;
        /// <summary>The cell of the guard outpost (the farthest cell that is not the elevators' or a stairwell's), and which neighbouring cell its front faces.</summary>
        public int outpostC, outpostR;
        public int outpostDirX = -1, outpostDirY;
        /// <summary>First floor: the lobby next to the elevator is a coffee shop.</summary>
        public bool cafe;
        /// <summary>This floor has a guard outpost (every office floor), in its own cell (<see cref="outpostC"/>, <see cref="outpostR"/>).</summary>
        public bool outpost;
        /// <summary>This floor has a floor safe (see <see cref="SafeRules"/>), and the number that picks the room it stands in.</summary>
        public bool hasSafe;
        public uint safeRoll;
        /// <summary>How many safes the floor has (see <see cref="SafeRules.SafeCount"/>); the first goes where the only safe always went, the others into other rooms.</summary>
        public int safeCount;
    }

    /// <summary>One 18 m x 18 m cell while it is being planned.</summary>
    public sealed class CellCtx
    {
        public int c, r;
        public float ox, oz;
        public bool oN, oE, oS, oW;
        /// <summary>Quarter turns of the plan frame (lets one corridor-along-x plan serve both directions).</summary>
        public int rot;
        public string plan;
        /// <summary>The random numbers of this cell: its planning and its furnishing share one sequence, the furnishing runs after every cell is planned (OfficePlanner.Core.cs).</summary>
        public System.Random rng;
        public readonly List<RoomCtx> rooms = new List<RoomCtx>();
        /// <summary>Two stacked rooms of one quadrant (6.4 x 3.1 m each), recorded while planning: where the restrooms can go.</summary>
        public readonly List<RoomCtx[]> pairs = new List<RoomCtx[]>();
        public readonly List<Rc> keeps = new List<Rc>();
    }

    /// <summary>
    /// Turns the maze of empty 18 m cells into a believable office floor: corridors, offices, conference rooms,
    /// break rooms, storage, copy and server rooms, open-plan areas, a lobby around each elevator, and a coffee
    /// shop on the first floor. It only produces plain data (<see cref="Layout"/>); FloorGenerator builds it.
    /// Everything is deterministic in (seed, cell), so all players get the identical floor.
    /// </summary>
    public sealed partial class OfficePlanner
    {
        public readonly Layout layout = new Layout();
        public System.Random rng;
        public readonly PlanInput inp;
        readonly float cs, half;        // cell size and half of it
        public const float Inner = 8.8f;       // inside face of the outer walls, from the cell centre
        const float Thick = 0.2f;       // interior partitions
        const float DoorW = 2.0f;
        const float CH = 2.2f;          // corridor half width (wings and splits)
        const float PM = 2.3f;          // partition centre line next to a corridor
        const float A0 = 2.4f;          // where the rooms start

        public static Layout Build(PlanInput p)
        {
            var pl = new OfficePlanner(p);
            pl.layout.input = p;
            pl.Run();
            return pl.layout;
        }

        OfficePlanner(PlanInput p) { inp = p; cs = p.cell; half = cs * 0.5f; }

        static int Mix(int seed, int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)(a + 1) * 40503u + 0x9E3779B9u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)(b + 1) * 69069u + 0x85EBCA6Bu;
                h *= 2246822519u;
                h ^= (uint)salt * 3266489917u;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        void Run()
        {
            // 1. plan every cell (rooms, walls, doorways, themes), 2. the break room and the restrooms every floor must have (OfficePlanner.Core.cs), 3. furnish every cell with the random
            // numbers its own planning left off at: a floor is the same as before wherever the core rooms do not touch it
            var cells = new List<CellCtx>();
            for (int c = 0; c < inp.cols; c++)
                for (int r = 0; r < inp.rows; r++)
                {
                    rng = new System.Random(Mix(inp.seed, c, r, 17));
                    var cell = BuildCell(c, r);
                    cell.rng = rng;
                    cells.Add(cell);
                }
            EnsureRestrooms(cells);
            EnsureBreakRoom(cells);
            foreach (var cell in cells) { rng = cell.rng; FurnishCell(cell); }
            ValidateOutpostSlots();
            PickRoles();                      // the executive suites and the server room (the safe goes into a suite first)
            PlaceSafe();
            MergeHideZones();
        }

        // ------------------------------------------------------------------ plan-frame helpers

        /// <summary>Plan-frame point to world.</summary>
        void P(CellCtx cell, float x, float z, out float wx, out float wz)
        {
            RoomCtx.RotQ(cell.rot, x, z, out var rx, out var rz);
            wx = cell.ox + rx; wz = cell.oz + rz;
        }

        Rc PR(CellCtx cell, float x0, float z0, float x1, float z1)
        {
            P(cell, x0, z0, out var ax, out var az);
            P(cell, x1, z1, out var bx, out var bz);
            return new Rc(Math.Min(ax, bx), Math.Min(az, bz), Math.Max(ax, bx), Math.Max(az, bz));
        }

        /// <summary>Cell-local (unrotated) rectangle to world.</summary>
        static Rc WR(CellCtx cell, float x0, float z0, float x1, float z1) => new Rc(cell.ox + x0, cell.oz + z0, cell.ox + x1, cell.oz + z1);

        RoomCtx NewRoom(CellCtx cell, string theme, int kind, float px, float pz, int turns, float w, float d)
        {
            P(cell, px, pz, out var wx, out var wz);
            var rc = new RoomCtx(this, theme, kind, wx, wz, turns + cell.rot, w, d);
            rc.name = theme + "@" + cell.c + "," + cell.r + "#" + cell.rooms.Count;
            rc.info = new RoomInfo { theme = theme, kind = kind, cellX = cell.c, cellY = cell.r, bounds = rc.WorldBounds };
            roomBounds[rc.name] = rc.info.bounds;
            layout.rooms.Add(rc.info);
            cell.rooms.Add(rc);
            return rc;
        }

        void AddBox(Rc rc, float y0, float y1, string kind)
        {
            layout.walls.Add(new WallBox { rc = rc, y0 = y0, y1 = y1, kind = kind });
        }

        /// <summary>
        /// A partition wall along x (<paramref name="alongX"/>) at z = coord (or along z at x = coord) from lo to hi,
        /// with doorway gaps {centre, width}. Doorways get a header above them. Plan-frame coordinates.
        /// </summary>
        void WallRun(CellCtx cell, bool alongX, float coord, float lo, float hi, List<float[]> gaps)
        {
            if (lo > hi) { var t = lo; lo = hi; hi = t; }
            gaps.Sort((a, b) => a[0].CompareTo(b[0]));
            float cur = lo;
            foreach (var g in gaps)
            {
                float g0 = g[0] - g[1] / 2, g1 = g[0] + g[1] / 2;
                Seg(cell, alongX, coord, cur, g0, 0f, inp.wallH, "wall");
                Seg(cell, alongX, coord, g0, g1, 2.45f, inp.wallH, "header");
                cur = g1;
            }
            Seg(cell, alongX, coord, cur, hi, 0f, inp.wallH, "wall");
        }

        void Seg(CellCtx cell, bool alongX, float coord, float a, float b, float y0, float y1, string kind)
        {
            if (b - a < 0.02f) return;
            Rc rc = alongX ? PR(cell, a, coord - Thick / 2, b, coord + Thick / 2) : PR(cell, coord - Thick / 2, a, coord + Thick / 2, b);
            AddBox(rc, y0, y1, kind);
        }

        /// <summary>Opening in a partition seen from a corridor: keep it clear on both sides, and note it for the tests.</summary>
        void DoorKeep(CellCtx cell, bool alongX, float coord, float centre, float width)
        {
            Rc keep = alongX
                ? PR(cell, centre - width / 2 - 0.45f, coord - 1.75f, centre + width / 2 + 0.45f, coord + 1.75f)
                : PR(cell, coord - 1.75f, centre - width / 2 - 0.45f, coord + 1.75f, centre + width / 2 + 0.45f);
            cell.keeps.Add(keep);
            Rc open = alongX
                ? PR(cell, centre - width / 2, coord - Thick / 2, centre + width / 2, coord + Thick / 2)
                : PR(cell, coord - Thick / 2, centre - width / 2, coord + Thick / 2, centre + width / 2);
            layout.clear.Add(open);
        }

        /// <summary>Records a doorway on the room's south wall (local x, width) and keeps its approach clear.</summary>
        void RoomDoor(CellCtx cell, RoomCtx room, float localX, float width)
        {
            room.doors.Add(new[] { localX, width });
            var keepLocal = new Rc(localX - width / 2 - 0.45f, -room.d / 2 - 1.75f, localX + width / 2 + 0.45f, -room.d / 2 + 1.75f);
            cell.keeps.Add(room.ToWorldRect(keepLocal));
            var open = room.ToWorldRect(new Rc(localX - width / 2, -room.d / 2 - Thick / 2, localX + width / 2, -room.d / 2 + Thick / 2));
            layout.clear.Add(open);
            room.info.doors.Add(open);
        }

        /// <summary>Gaps in the outer walls where the maze opens to a neighbour: keep them clear on this side.</summary>
        void PerimeterKeeps(CellCtx cell)
        {
            const float g = 2.45f, depth = 2.2f;
            if (cell.oN) cell.keeps.Add(WR(cell, -g, Inner - depth, g, half + 0.3f));
            if (cell.oS) cell.keeps.Add(WR(cell, -g, -half - 0.3f, g, -Inner + depth));
            if (cell.oE) cell.keeps.Add(WR(cell, Inner - depth, -g, half + 0.3f, g));
            if (cell.oW) cell.keeps.Add(WR(cell, -half - 0.3f, -g, -Inner + depth, g));
            if (cell.oN) layout.clear.Add(WR(cell, -2f, half - 0.2f, 2f, half + 0.2f));
            if (cell.oS) layout.clear.Add(WR(cell, -2f, -half - 0.2f, 2f, -half + 0.2f));
            if (cell.oE) layout.clear.Add(WR(cell, half - 0.2f, -2f, half + 0.2f, 2f));
            if (cell.oW) layout.clear.Add(WR(cell, -half - 0.2f, -2f, -half + 0.2f, 2f));
        }

        bool OpenToward(CellCtx cell, int dx, int dz)
        {
            if (dx > 0) return cell.oE;
            if (dx < 0) return cell.oW;
            if (dz > 0) return cell.oN;
            return cell.oS;
        }

        /// <summary>Is the plan-frame direction (1,0)/(0,1) (times sign) open to the neighbour in the world?</summary>
        bool PlanOpen(CellCtx cell, int px, int pz)
        {
            RoomCtx.RotQ(cell.rot, px, pz, out var wx, out var wz);
            return OpenToward(cell, (int)Math.Round(wx), (int)Math.Round(wz));
        }

        void Patch(Rc rc, int tint) { layout.patches.Add(new FloorPatch { rc = rc, tint = tint }); }

        // ------------------------------------------------------------------ cell dispatch

        CellCtx BuildCell(int c, int r)
        {
            var cell = new CellCtx { c = c, r = r, ox = (c + 0.5f) * cs, oz = (r + 0.5f) * cs };
            cell.oN = r < inp.rows - 1 && inp.openN[c, r];
            cell.oS = r > 0 && inp.openN[c, r - 1];
            cell.oE = c < inp.cols - 1 && inp.openE[c, r];
            cell.oW = c > 0 && inp.openE[c - 1, r];
            PerimeterKeeps(cell);

            bool isElev = c == inp.elevC && r == inp.elevR;
            bool isGuest = c == inp.guestC && r == inp.guestR && !isElev;
            int stair = StairIndex(c, r);
            bool isStair = stair >= 0 && !isElev && !isGuest;
            bool isOutpost = inp.outpost && c == inp.outpostC && r == inp.outpostR && !isElev && !isGuest && !isStair;
            if (isElev) PlanRing(cell, true);
            else if (isGuest) PlanRing(cell, false);
            else if (isStair) PlanStairRing(cell, stair);
            else if (isOutpost) PlanOutpostCell(cell);
            else if (inp.cafe && c == 1 && r == 0) PlanHall(cell, "cafe");
            else if ((c == 0 && r == 1) || (c == 1 && r == 0)) PlanHall(cell, "lobby");
            else
            {
                double roll = rng.NextDouble();
                if (roll < 0.58) PlanWing(cell);
                else if (roll < 0.76) PlanHall(cell, null);
                else PlanSplit(cell);
            }
            return cell;
        }

        static int ZoneTurns(int sx, int sz) => sx > 0 ? (sz > 0 ? 0 : 1) : (sz < 0 ? 2 : 3);

        float PickDoor(float lo, float hi)
        {
            float a = lo + DoorW / 2 + 0.7f, b = hi - DoorW / 2 - 0.7f;
            return b <= a ? (lo + hi) / 2 : a + (float)rng.NextDouble() * (b - a);
        }

        string Pick(string[] names, int[] weights)
        {
            int tot = 0; foreach (var w in weights) tot += w;
            int x = rng.Next(tot);
            for (int i = 0; i < names.Length; i++) { x -= weights[i]; if (x < 0) return names[i]; }
            return names[names.Length - 1];
        }

        // ------------------------------------------------------------------ Wing: a cross of corridors with a room in each corner

        void PlanWing(CellCtx cell)
        {
            cell.plan = "wing";
            var centre = NewRoom(cell, "hub", 2, 0f, 0f, 0, 2 * CH, 2 * CH);
            var armN = NewRoom(cell, "hall", 2, 0f, (CH + Inner) / 2, 0, 2 * CH, Inner - CH);
            var armS = NewRoom(cell, "hall", 2, 0f, -(CH + Inner) / 2, 2, 2 * CH, Inner - CH);
            var armE = NewRoom(cell, "hall", 2, (CH + Inner) / 2, 0, 1, 2 * CH, Inner - CH);
            var armW = NewRoom(cell, "hall", 2, -(CH + Inner) / 2, 0, 3, 2 * CH, Inner - CH);
            armN.wallOn[0] = armS.wallOn[0] = armE.wallOn[0] = armW.wallOn[0] = false;     // open to the central square
            armN.endGap = cell.oN; armS.endGap = cell.oS; armE.endGap = cell.oE; armW.endGap = cell.oW;

            var open = new bool[2, 2];     // [sx>0, sz>0]
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    double roll = rng.NextDouble();
                    string kind = roll < 0.40 ? "single" : roll < 0.75 ? "split" : "open";
                    open[sx > 0 ? 1 : 0, sz > 0 ? 1 : 0] = Quadrant(cell, sx, sz, kind);
                }
            // which side walls do the arms have? (an open-plan corner has no partition)
            bool O(int sx, int sz) => open[sx > 0 ? 1 : 0, sz > 0 ? 1 : 0];
            armN.wallOn[2] = !O(-1, 1); armN.wallOn[3] = !O(1, 1);
            armS.wallOn[2] = !O(1, -1); armS.wallOn[3] = !O(-1, -1);
            armE.wallOn[2] = !O(1, 1); armE.wallOn[3] = !O(1, -1);
            armW.wallOn[2] = !O(-1, -1); armW.wallOn[3] = !O(-1, 1);
        }

        /// <summary>One corner of a wing: one room, two rooms, or an open-plan zone. Returns true for an open zone.</summary>
        bool Quadrant(CellCtx cell, int sx, int sz, string kind)
        {
            if (kind == "open")
            {
                var zone = NewRoom(cell, "zone", 1, sx * (A0 + Inner) / 2, sz * (A0 + Inner) / 2, ZoneTurns(sx, sz), Inner - A0, Inner - A0);
                zone.wallOn[0] = false; zone.wallOn[2] = false;     // its south and west sides face the corridors
                zone.SetTheme(Pick(new[] { "open", "lounge", "meeting", "nook" }, new[] { 10, 3, 3, 1 }));
                return true;
            }

            var gapsH = new List<float[]>();      // doorways in the partition next to the x corridor (z = sz*PM)
            var gapsV = new List<float[]>();      // doorways in the partition next to the z corridor (x = sx*PM)
            var specs = new List<float[]>();      // {a0, a1, b0, b1}: |x| range and |z| range of each room
            const float mid = 5.6f;
            bool zStack = false, xStack = false;
            if (kind == "single") specs.Add(new[] { A0, Inner, A0, Inner });
            else if (rng.NextDouble() < 0.5)
            {
                zStack = true;
                specs.Add(new[] { A0, Inner, A0, mid - Thick / 2 });
                specs.Add(new[] { A0, Inner, mid + Thick / 2, Inner });
            }
            else
            {
                xStack = true;
                specs.Add(new[] { A0, mid - Thick / 2, A0, Inner });
                specs.Add(new[] { mid + Thick / 2, Inner, A0, Inner });
            }

            var madeRooms = new List<RoomCtx>();
            foreach (var s in specs)
            {
                float a0 = s[0], a1 = s[1], b0 = s[2], b1 = s[3];
                bool touchH = Math.Abs(b0 - A0) < 1e-4f;
                bool touchV = Math.Abs(a0 - A0) < 1e-4f;
                bool useH = (touchH && touchV) ? rng.NextDouble() < 0.5 : touchH;
                float cxm = sx * (a0 + a1) / 2, czm = sz * (b0 + b1) / 2;
                float rw = a1 - a0, rd = b1 - b0;
                RoomCtx room;
                float px, pz;
                if (useH)
                {
                    float t = PickDoor(a0, a1);
                    room = NewRoom(cell, "room", 0, cxm, czm, sz > 0 ? 0 : 2, rw, rd);
                    gapsH.Add(new[] { sx * t, DoorW });
                    px = sx * t; pz = sz * PM;
                }
                else
                {
                    float t = PickDoor(b0, b1);
                    room = NewRoom(cell, "room", 0, cxm, czm, sx > 0 ? 1 : 3, rd, rw);   // w runs along the door wall
                    gapsV.Add(new[] { sz * t, DoorW });
                    px = sx * PM; pz = sz * t;
                }
                P(cell, px, pz, out var dwx, out var dwz);
                room.ToLocal(dwx, dwz, out var ldx, out var ldz);
                RoomDoor(cell, room, ldx, DoorW);
                bool small = Math.Min(room.w, room.d) < 4f;
                room.SetTheme(small
                    ? Pick(new[] { "office", "storage", "copy", "server" }, new[] { 7, 2, 3, 2 })
                    : Pick(new[] { "office", "conference", "break", "storage", "lounge", "server", "training" }, new[] { 5, 3, 3, 1, 2, 1, 2 }));
                madeRooms.Add(room);
            }
            if (madeRooms.Count == 2) cell.pairs.Add(madeRooms.ToArray());          // (two stacked rooms: the restrooms are picked from these)

            float hx1 = sx * (Inner + 0.2f), vz1 = sz * (Inner + 0.2f);      // out to the middle of the outer wall
            WallRun(cell, true, sz * PM, sx * CH, hx1, gapsH);
            WallRun(cell, false, sx * PM, sz * CH, vz1, gapsV);
            foreach (var g in gapsH) DoorKeep(cell, true, sz * PM, g[0], g[1]);
            foreach (var g in gapsV) DoorKeep(cell, false, sx * PM, g[0], g[1]);
            if (zStack) WallRun(cell, true, sz * mid, sx * A0, hx1, new List<float[]>());
            if (xStack) WallRun(cell, false, sx * mid, sz * A0, vz1, new List<float[]>());
            return false;
        }

        // ------------------------------------------------------------------ Hall: one big open floor

        void PlanHall(CellCtx cell, string variant)
        {
            cell.plan = "hall:" + (variant ?? "std");
            const float L = 2.0f;
            // lanes
            var lanes = new List<RoomCtx>();
            var centre = NewRoom(cell, "hub", 2, 0f, 0f, 0, 2 * L, 2 * L);
            var armN = NewRoom(cell, "hall", 2, 0f, (L + Inner) / 2, 0, 2 * L, Inner - L);
            var armS = NewRoom(cell, "hall", 2, 0f, -(L + Inner) / 2, 2, 2 * L, Inner - L);
            var armE = NewRoom(cell, "hall", 2, (L + Inner) / 2, 0, 1, 2 * L, Inner - L);
            var armW = NewRoom(cell, "hall", 2, -(L + Inner) / 2, 0, 3, 2 * L, Inner - L);
            foreach (var a in new[] { armN, armS, armE, armW }) { a.wallOn[0] = false; a.wallOn[2] = false; a.wallOn[3] = false; a.openLane = true; }
            armN.endGap = cell.oN; armS.endGap = cell.oS; armE.endGap = cell.oE; armW.endGap = cell.oW;

            string[] themes;
            if (variant == "cafe") themes = new[] { "cafe_counter", "cafe_seating", "cafe_seating", "lounge" };
            else if (variant == "lobby") themes = new[] { "open", "meeting", "lounge", "open" };
            else themes = new[] { Pick(new[] { "open", "lounge", "meeting", "nook", "break" }, new[] { 8, 2, 3, 1, 1 }), "open", "open", Pick(new[] { "open", "meeting", "lounge" }, new[] { 4, 2, 2 }) };
            // shuffle
            for (int i = themes.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); var t = themes[i]; themes[i] = themes[j]; themes[j] = t; }
            int k = 0;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var zone = NewRoom(cell, "zone", 1, sx * (L + Inner) / 2, sz * (L + Inner) / 2, ZoneTurns(sx, sz), Inner - L, Inner - L);
                    zone.wallOn[0] = false; zone.wallOn[2] = false;
                    zone.SetTheme(themes[k++]);
                }
        }

        // ------------------------------------------------------------------ Split: a corridor with a long room on each side

        void PlanSplit(CellCtx cell)
        {
            cell.plan = "split";
            cell.rot = rng.Next(2);
            var cor = NewRoom(cell, "hall", 2, 0f, 0f, 0, 2 * Inner, 2 * CH);
            cor.wallOn[0] = true;
            cor.endGap = false;
            cor.longHall = true;
            cor.endGapA = PlanOpen(cell, -1, 0);
            cor.endGapB = PlanOpen(cell, 1, 0);
            var gaps = new List<float[]> { new[] { 0f, 4.8f }, new[] { -6.4f, DoorW }, new[] { 6.4f, DoorW } };
            for (int s = -1; s <= 1; s += 2)
            {
                var room = NewRoom(cell, "long", 0, 0f, s * (A0 + Inner) / 2, s > 0 ? 0 : 2, 2 * Inner, Inner - A0);
                foreach (var g in gaps) RoomDoor(cell, room, s > 0 ? g[0] : -g[0], g[1]);
                // the cross aisle: from the wide doorway straight to the far wall (where the cell may be open to a neighbour)
                room.lanes.Add(new Rc(-1.5f, -room.d / 2, 1.5f, room.d / 2));
                room.SetTheme(Pick(new[] { "longopen", "canteen", "training" }, new[] { 6, 2, 2 }));
                WallRun(cell, true, s * PM, -(Inner + 0.2f), Inner + 0.2f, new List<float[]>(gaps));
                foreach (var g in gaps) DoorKeep(cell, true, s * PM, g[0], g[1]);
            }
        }

        // ------------------------------------------------------------------ Ring: the lobby around an elevator

        void PlanRing(CellCtx cell, bool lobby)
        {
            cell.plan = lobby ? "lobby" : "service";
            var ring = NewRoom(cell, lobby ? "lobby" : "service", 2, 0f, 0f, 0, 2 * Inner, 2 * Inner);
            ring.isRing = true;
            if (lobby)
            {
                const float s = 4.2f;                                           // outside face of the shaft walls
                ring.blocks.Add(new Rc(-s, -s, s, s));
                if (inp.elevDoorEast) cell.keeps.Add(WR(cell, s - 0.1f, -2.6f, s + 2.6f, 2.6f));
                else cell.keeps.Add(WR(cell, -2.6f, s - 0.1f, 2.6f, s + 2.6f));
                if (inp.hiddenHall)
                {
                    Rc hall = inp.elevDoorEast ? new Rc(-half, -4.2f, -3.8f, 4.2f) : new Rc(-4.2f, -half, 4.2f, -3.8f);
                    ring.blocks.Add(hall);
                    cell.keeps.Add(WR(cell, hall.x0, hall.z0, hall.x1, hall.z1));
                }
            }
            else
            {
                const float s = 2.8f;                                           // outside face of the guards' car
                ring.blocks.Add(new Rc(-s, -s, s, s));
                // the car's door and the straight run from it to the doorway of the cell
                int dx = inp.guestDirX, dz = inp.guestDirY;
                Rc lane = dx != 0
                    ? new Rc(dx > 0 ? s - 0.1f : -half, -1.9f, dx > 0 ? half : -s + 0.1f, 1.9f)
                    : new Rc(-1.9f, dz > 0 ? s - 0.1f : -half, 1.9f, dz > 0 ? half : -s + 0.1f);
                cell.keeps.Add(WR(cell, lane.x0, lane.z0, lane.x1, lane.z1));
            }
            ring.endGap = false;
        }

        /// <summary>Which stairwell is in cell (c, r): 0 or 1, or -1.</summary>
        int StairIndex(int c, int r)
        {
            if (inp.stairC == null) return -1;
            for (int i = 0; i < inp.stairC.Length; i++) if (inp.stairC[i] == c && inp.stairR[i] == r) return i;
            return -1;
        }

        /// <summary>
        /// A stairwell's cell: the same kind of ring as the guards' car round a smaller solid block (the landing with its walls, 4.8 m), and the
        /// straight run from its door to the doorway of the cell kept free of furniture.
        /// </summary>
        void PlanStairRing(CellCtx cell, int i)
        {
            cell.plan = "service";
            var ring = NewRoom(cell, "service", 2, 0f, 0f, 0, 2 * Inner, 2 * Inner);
            ring.isRing = true;
            const float s = StairHalf;                                      // outside face of the landing's walls
            ring.blocks.Add(new Rc(-s, -s, s, s));
            int dx = inp.stairDirX[i], dz = inp.stairDirY[i];
            Rc lane = dx != 0
                ? new Rc(dx > 0 ? s - 0.1f : -half, -1.7f, dx > 0 ? half : -s + 0.1f, 1.7f)
                : new Rc(-1.7f, dz > 0 ? s - 0.1f : -half, 1.7f, dz > 0 ? half : -s + 0.1f);
            cell.keeps.Add(WR(cell, lane.x0, lane.z0, lane.x1, lane.z1));
            ring.endGap = false;
        }

        /// <summary>Half the outside size of a stairwell's walls (m); FloorGenerator builds the landing to this size.</summary>
        public const float StairHalf = 2.4f;

        /// <summary>The cell of the guard outpost: the big room round a solid block (the outpost with its walls).</summary>
        void PlanOutpostCell(CellCtx cell)
        {
            cell.plan = "service";
            var ring = NewRoom(cell, "service", 2, 0f, 0f, 0, 2 * Inner, 2 * Inner);
            ring.isRing = true;
            PlanOutpost(cell, ring);
            ring.endGap = false;
        }

        // ------------------------------------------------------------------ furnishing a planned cell

        void FurnishCell(CellCtx cell)
        {
            foreach (var room in cell.rooms)
            {
                var wb = room.WorldBounds;
                foreach (var k in cell.keeps)
                    if (k.Overlaps(wb, 0.01f)) room.keep.Add(room.ToLocalRect(k));
            }
            foreach (var room in cell.rooms)
            {
                Furnish(room);
                Settle(room);
                FinishRoom(cell, room);
                allRooms.Add(room);
            }
        }

        /// <summary>Every room of the floor once it is furnished (the floor safe is added afterwards).</summary>
        readonly List<RoomCtx> allRooms = new List<RoomCtx>();

        static int TintOf(string theme)
        {
            switch (theme)
            {
                case "office": return 1;
                case "conference": case "meeting": case "training": return 2;
                case "break": case "canteen": return 3;
                case "storage": return 4;
                case "server": return 5;
                case "lounge": case "nook": return 6;
                case "open": case "longopen": return 7;
                case "lobby": return 8;
                case "cafe_counter": case "cafe_seating": return 9;
                case "copy": return 10;
                case "service": return 11;
                case "outpost": return 12;
                default: return 0;
            }
        }

        void FinishRoom(CellCtx cell, RoomCtx room)
        {
            var wb = room.WorldBounds;
            if (!room.isRing) Patch(wb, TintOf(room.theme));
            else
            {
                // the ring is one big room, but its floor patch must skip the shaft / car block
                foreach (var rc in RingPatches(room)) Patch(rc, TintOf(room.theme));
            }
            // a spot guards can stand on
            float tx = 0f, tz = 0f;
            var rg = room.reach;
            if (room.kind == 2 && !room.isRing && !room.longHall) tz = room.d / 2f;
            if (room.isRing)
            {
                var pts = new[] { new[] { 6.5f, 0f }, new[] { 0f, 6.5f }, new[] { -6.5f, 0f }, new[] { 0f, -6.5f } };
                bool first = true;
                foreach (var pt in pts)
                    if (room.FindFreeReach(rg, pt[0], pt[1], out var fx, out var fz, 0.8f))
                    {
                        room.ToWorld(fx, fz, out var wx, out var wz);
                        if (first) { room.info.wx = wx; room.info.wz = wz; first = false; }
                        layout.waypoints.Add(new[] { wx, wz, cell.c, cell.r });
                    }
            }
            else if (room.longHall)
            {
                foreach (var tx2 in new[] { -5f, 0f, 5f })
                    if (room.FindFreeReach(rg, tx2, 0f, out var fx, out var fz, 0.8f))
                    {
                        room.ToWorld(fx, fz, out var wx, out var wz);
                        layout.waypoints.Add(new[] { wx, wz, cell.c, cell.r });
                        if (tx2 == 0f) { room.info.wx = wx; room.info.wz = wz; }
                    }
            }
            else if (room.theme == "outpost" || room.theme == ThemeRestroom)
            {
                room.info.wx = wb.Cx; room.info.wz = wb.Cz;      // (no patrol stops inside the guard outpost: the officers come from its elevator and leave; none in a restroom: its door never opens)
            }
            else if (room.FindFreeReach(rg, tx, tz, out var gx, out var gz, 0.8f) || room.FindFreeReach(rg, tx, tz, out gx, out gz, 0.55f))
            {
                room.ToWorld(gx, gz, out var wx, out var wz);
                room.info.wx = wx; room.info.wz = wz;
                layout.waypoints.Add(new[] { wx, wz, cell.c, cell.r });
            }
            room.info.hideCount = room.hides.Count;
            int n = 0;
            foreach (var p in layout.pieces) if (p.room == room.name) n++;
            room.info.pieceCount = n;
        }

        List<Rc> RingPatches(RoomCtx room)
        {
            // four rectangles around the blocked centre
            var res = new List<Rc>();
            if (room.blocks.Count == 0) { res.Add(room.WorldBounds); return res; }
            var b = room.blocks[0];
            float hx = room.w / 2, hz = room.d / 2;
            res.Add(room.ToWorldRect(new Rc(-hx, -hz, hx, b.z0)));        // south band
            res.Add(room.ToWorldRect(new Rc(-hx, b.z1, hx, hz)));         // north band
            res.Add(room.ToWorldRect(new Rc(-hx, b.z0, b.x0, b.z1)));     // west
            res.Add(room.ToWorldRect(new Rc(b.x1, b.z0, hx, b.z1)));      // east
            return res;
        }

        // ------------------------------------------------------------------ hide triggers

        const float HideReach = 0.8f;       // how close you must be to the furniture to duck behind it
        const float ZoneMaxSide = 9f;       // a single hide trigger never spans more than this
        const float ZoneWaste = 1.3f;       // merged box may be at most this much bigger than the boxes it replaces

        static float Area(Rc r) => Math.Max(0f, r.W) * Math.Max(0f, r.D);

        static float OverlapArea(Rc a, Rc b)
        {
            float w = Math.Min(a.x1, b.x1) - Math.Max(a.x0, b.x0), d = Math.Min(a.z1, b.z1) - Math.Max(a.z0, b.z0);
            return w > 0f && d > 0f ? w * d : 0f;
        }

        static Rc Union(Rc a, Rc b) => new Rc(Math.Min(a.x0, b.x0), Math.Min(a.z0, b.z0), Math.Max(a.x1, b.x1), Math.Max(a.z1, b.z1));

        /// <summary>
        /// One trigger rectangle per cluster of furniture you can duck behind. Rectangles are only merged when the
        /// merged box stays snug (a row of desks becomes one zone; two cabinets in opposite corners stay two), and every
        /// zone is cut off at the walls of its own room so you cannot hide through a partition.
        /// </summary>
        void MergeHideZones()
        {
            var groups = new Dictionary<string, List<Rc>>();
            foreach (var p in layout.pieces)
            {
                if (!p.hide) continue;
                if (!groups.TryGetValue(p.room, out var list)) groups[p.room] = list = new List<Rc>();
                list.Add(p.rc.Grow(HideReach));
            }
            foreach (var kv in groups)
            {
                var rects = kv.Value;
                bool merged = true;
                while (merged)
                {
                    merged = false;
                    int bi = -1, bj = -1; float bestWaste = float.MaxValue; Rc bu = default;
                    for (int i = 0; i < rects.Count; i++)
                        for (int j = i + 1; j < rects.Count; j++)
                        {
                            float inter = OverlapArea(rects[i], rects[j]);
                            if (inter <= 0f) continue;
                            var u = Union(rects[i], rects[j]);
                            if (u.W > ZoneMaxSide || u.D > ZoneMaxSide) continue;
                            float sum = Area(rects[i]) + Area(rects[j]) - inter;
                            float waste = Area(u) - sum;
                            if (Area(u) > ZoneWaste * sum + 0.3f) continue;
                            if (waste < bestWaste) { bestWaste = waste; bi = i; bj = j; bu = u; }
                        }
                    if (bi >= 0) { rects[bi] = bu; rects.RemoveAt(bj); merged = true; }
                }
                Rc? room = null;
                if (roomBounds.TryGetValue(kv.Key, out var rb)) room = rb;
                foreach (var z in rects)
                {
                    var c = z;
                    if (room.HasValue)
                    {
                        var r = room.Value;
                        c = new Rc(Math.Max(z.x0, r.x0), Math.Max(z.z0, r.z0), Math.Min(z.x1, r.x1), Math.Min(z.z1, r.z1));
                    }
                    if (c.W > 0.5f && c.D > 0.5f) layout.hideZones.Add(c);
                }
            }
        }

        readonly Dictionary<string, Rc> roomBounds = new Dictionary<string, Rc>();
    }
}
