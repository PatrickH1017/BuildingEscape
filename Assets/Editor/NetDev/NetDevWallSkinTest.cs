using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the Floor 7 wall and ceiling pass (<c>WallSkinRules.cs</c>, 2026-10-23). They plan whole floors and prove that the plan only DRESSES what is there: the layout is the same before and
    /// after (walls, rooms, doorways), every face of every wall that should wear the pack's wall pieces is covered exactly once and nothing else is (never across a doorway, never on a glass front, the outpost, a cubicle
    /// divider or the outside walls), the pieces keep the pack's length (stretched, never doubled), the beams over the doorways are plain, the accent walls come from the accent rule, every colour is in range, the ceiling
    /// tiles divide the floor evenly, the same plan comes out every time, and only floor 7 is touched. Called from <c>NetDevTest.PolicyTest</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void WallSkinTest()
        {
            Check("wall skin: it works on floor 7 and no other floor", WallSkinRules.AppliesTo(7) && WallSkinRules.SkinnedFloor == 7 && Enumerable.Range(1, 12).Where(f => f != 7).All(f => !WallSkinRules.AppliesTo(f)));
            foreach (var key in new[] { "skin_wall_baseboard", "skin_wall_trim", "skin_wall_blank", "skin_wall_brick", "skin_ceiling_panel" })
            {
                PropDef d;
                Check("wall skin: the prop table has " + key, PropTable.TryGet(key, out d) && d != null && !d.solid, key);
            }
            Check("wall skin: the pieces are the pack's size (2.5 m long, 2.997 m high) and the ceiling tile target is 3 m", Math.Abs(WallSkinRules.ModuleLength - 2.5f) < 1e-5f && Math.Abs(WallSkinRules.ModuleHeight - 2.997f) < 1e-5f && Math.Abs(WallSkinRules.TileTarget - 3f) < 1e-5f);
            Check("wall skin: the lift off the wall is 4 mm or less (the room number plates stand 6 mm off it)", WallSkinRules.Lift > 0f && WallSkinRules.Lift <= 0.004f + 1e-6f);
            WsPaints();
            WsFloor(4101, 8, 5, true);
            WsFloor(5, 8, 5, false);
            WsFloor(777, 6, 4, false);
            WsFloor(31, 8, 5, false);

            // the layout is not changed by planning, and the same plan comes out every time
            var L = WsMake(4101, 8, 5);
            string before = WsSig(L);
            var p1 = WallSkinRules.Plan(L, new HashSet<WallBox>(), 4f);
            var p2 = WallSkinRules.Plan(L, new HashSet<WallBox>(), 4f);
            Check("wall skin: planning does not change the layout", before == WsSig(L));
            Check("wall skin: the same floor gives the same plan every time", WsPlanSig(p1) == WsPlanSig(p2) && p1.modules.Count == p2.modules.Count);

            // with a glass front the plan leaves that wall alone, and with the headers off the beams stay as they were
            var glass = new HashSet<WallBox>(L.walls.Where(w => w.kind == "wall" && w.y1 > 3f).Take(10));
            var pg = WallSkinRules.Plan(L, glass, 4f);
            Check("wall skin: a wall that has a glass front gets no pieces", glass.All(w => !pg.boxes.Contains(w)) && pg.modules.Count < p1.modules.Count, pg.modules.Count + " vs " + p1.modules.Count);
            var ph = WallSkinRules.Plan(L, new HashSet<WallBox>(), 4f, new WallSkinRules.Options { headers = false });
            Check("wall skin: with the beams off there is no piece over a doorway", ph.modules.All(m => !m.header) && p1.modules.Any(m => m.header));
            var pa = WallSkinRules.Plan(L, new HashSet<WallBox>(), 4f, new WallSkinRules.Options { accents = false });
            Check("wall skin: with the accents off there is no accent or brick piece", pa.modules.All(m => !m.accent && m.style != SkinStyle.Brick) && pa.accentFaces == 0);
        }

        // ------------------------------------------------------------------ helpers

        static Layout WsMake(int seed, int cols, int rows)
        {
            bool saved = OfficePlanner.DressingOn;
            try
            {
                OfficePlanner.DressingOn = true;
                var shape = FloorShape.Maze(new System.Random(seed), cols, rows, 0.12);
                return OfficePlanner.Build(shape.PlanFor(7, seed, 18f, 3.5f, 5 * seed + 7));
            }
            finally { OfficePlanner.DressingOn = saved; }
        }

        static string WsF(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string WsR(Rc r) => "[" + WsF(r.x0) + "," + WsF(r.z0) + "," + WsF(r.x1) + "," + WsF(r.z1) + "]";

        static string WsSig(Layout L)
        {
            var sb = new StringBuilder();
            foreach (var w in L.walls) sb.Append("W" + WsR(w.rc) + WsF(w.y0) + WsF(w.y1) + w.kind);
            foreach (var r in L.rooms)
            {
                sb.Append("R" + r.theme + r.role + r.kind + WsR(r.bounds));
                foreach (var d in r.doors) sb.Append("d" + WsR(d));
            }
            foreach (var w in L.waypoints) sb.Append("V" + WsF(w[0]) + "," + WsF(w[1]));
            sb.Append("n" + L.pieces.Count);
            return sb.ToString();
        }

        static string WsPlanSig(SkinPlan p)
        {
            var sb = new StringBuilder();
            foreach (var m in p.modules) sb.Append((int)m.style + "|" + (m.alongX ? 1 : 0) + "|" + m.nrm + "|" + WsF(m.line) + "|" + WsF(m.a) + "|" + WsF(m.b) + "|" + WsF(m.y0) + "|" + WsF(m.y1) + "|" + WsF(m.tr) + "," + WsF(m.tg) + "," + WsF(m.tb) + "|" + (m.accent ? 1 : 0) + "\n");
            foreach (var t in p.tiles) sb.Append("T" + WsF(t.x0) + "," + WsF(t.z0) + "," + WsF(t.x1) + "," + WsF(t.z1) + "\n");
            return sb.ToString();
        }

        /// <summary>The walls between the cells, as FloorGenerator.BuildShell makes them (0.4 m thick on the cell lines, a doorway gap where the maze is open); the outside ones are marked.</summary>
        static List<KeyValuePair<Rc, bool>> WsShell(Layout L, float doorWidth)
        {
            var res = new List<KeyValuePair<Rc, bool>>();
            var inp = L.input; float cell = inp.cell, gap = doorWidth, half = (cell - gap) / 2f;
            for (int c = 0; c <= inp.cols; c++)
                for (int r = 0; r < inp.rows; r++)
                {
                    bool edge = c == 0 || c == inp.cols; bool solid = edge || !inp.openE[c - 1, r];
                    float x = c * cell, z0 = r * cell;
                    if (solid) res.Add(new KeyValuePair<Rc, bool>(new Rc(x - 0.2f, z0, x + 0.2f, z0 + cell), edge));
                    else { res.Add(new KeyValuePair<Rc, bool>(new Rc(x - 0.2f, z0, x + 0.2f, z0 + half), false)); res.Add(new KeyValuePair<Rc, bool>(new Rc(x - 0.2f, z0 + cell - half, x + 0.2f, z0 + cell), false)); }
                }
            for (int r = 0; r <= inp.rows; r++)
                for (int c = 0; c < inp.cols; c++)
                {
                    bool edge = r == 0 || r == inp.rows; bool solid = edge || !inp.openN[c, r - 1];
                    float z = r * cell, x0 = c * cell;
                    if (solid) res.Add(new KeyValuePair<Rc, bool>(new Rc(x0, z - 0.2f, x0 + cell, z + 0.2f), edge));
                    else { res.Add(new KeyValuePair<Rc, bool>(new Rc(x0, z - 0.2f, x0 + half, z + 0.2f), false)); res.Add(new KeyValuePair<Rc, bool>(new Rc(x0 + cell - half, z - 0.2f, x0 + cell, z + 0.2f), false)); }
                }
            return res;
        }

        static void WsPaints()
        {
            var themes = new[] { "office", "conference", "meeting", "training", "lounge", "nook", "open", "longopen", "break", "canteen", "cafe_seating", "copy", "server", "storage", "restroom", "outpost", "hall", "hub", "lobby", "service", "nothing", null };
            bool ok = true; string bad = "";
            foreach (var t in themes)
            {
                var p = WallSkinRules.PaintOf(t);
                var tr = WallSkinRules.TintOf(p.r, WallSkinRules.BaseBeige[0]); var tg = WallSkinRules.TintOf(p.g, WallSkinRules.BaseBeige[1]); var tb = WallSkinRules.TintOf(p.b, WallSkinRules.BaseBeige[2]);
                bool good = p.r > 0.4f && p.r <= 1f && p.g > 0.4f && p.g <= 1f && p.b > 0.4f && p.b <= 1f && (p.style == SkinStyle.Baseboard || p.style == SkinStyle.Trim)
                    && tr >= 0.2f && tr <= 1.6f && tg >= 0.2f && tg <= 1.6f && tb >= 0.2f && tb <= 1.6f;
                if (!good) { ok = false; bad += " " + t; }
            }
            Check("wall skin: every room theme has a paint in range and a skirting that is not brick", ok, bad);
            Check("wall skin: brick is only for the lounges, nooks, break rooms and canteens", WallSkinRules.BrickRoom("lounge") && WallSkinRules.BrickRoom("nook") && WallSkinRules.BrickRoom("break") && WallSkinRules.BrickRoom("canteen") && !WallSkinRules.BrickRoom("office") && !WallSkinRules.BrickRoom("server") && !WallSkinRules.BrickRoom("hall"));
            Check("wall skin: the tint is limited to 0.2..1.6", WallSkinRules.TintOf(0f, 0.7f) == 0.2f && WallSkinRules.TintOf(5f, 0.7f) == 1.6f && Math.Abs(WallSkinRules.TintOf(0.7f, 0.7f) - 1f) < 1e-5f);
        }

        // ------------------------------------------------------------------ one floor

        static void WsFloor(int seed, int cols, int rows, bool heavy)
        {
            var L = WsMake(seed, cols, rows);
            float wallH = L.input.wallH, cell = L.input.cell, width = cols * cell, depth = rows * cell;
            var plan = WallSkinRules.Plan(L, new HashSet<WallBox>(), 4f);
            string tag = "wall skin (seed " + seed + ", " + cols + "x" + rows + "): ";
            var shell = WsShell(L, 4f);

            // every wall rectangle that can stand in front of a face (the partitions of the layout and the walls between the cells)
            var solids = new List<Rc>(); var solidY = new List<float[]>();
            foreach (var w in L.walls) if (w.y1 >= wallH - 0.01f) { solids.Add(w.rc); solidY.Add(new[] { w.y0, w.y1 }); }
            foreach (var s in shell) { solids.Add(s.Key); solidY.Add(new[] { 0f, wallH }); }

            Check(tag + "the plan has pieces, in all four kinds, and brick", plan.modules.Count > 100 && plan.perStyle.Count == 4 && plan.brickFaces > 0 && plan.accentFaces > 0, plan.modules.Count + " pieces, " + plan.perStyle.Count + " styles, " + plan.accentFaces + " accents");

            // 1. every piece stands on a real wall: its back is inside a wall box (or a beam), along its whole length, at its height
            int offWall = 0, wrongLen = 0, wrongY = 0, outsideWall = 0, glassOrPanel = 0; string first = "";
            foreach (var m in plan.modules)
            {
                float len = m.b - m.a;
                if (len < WallSkinRules.MinFace - 1e-4f || len > WallSkinRules.ModuleLength + 1e-3f) wrongLen++;
                if (m.header) { if (m.style != SkinStyle.Blank || m.y1 < wallH - 0.01f || m.y0 < 0.5f) wrongY++; }
                else if (m.y0 > 0.01f || Math.Abs(m.y1 - wallH) > 0.02f) wrongY++;
                if (m.nrm != 1 && m.nrm != -1) wrongY++;
                // perimeter walls are never skinned
                bool onEdge = m.shell && (m.alongX ? (Math.Abs(m.line - 0f) < 0.3f || Math.Abs(m.line - depth) < 0.3f) : (Math.Abs(m.line - 0f) < 0.3f || Math.Abs(m.line - width) < 0.3f));
                if (onEdge) outsideWall++;
                // the back of the piece (0.01 m behind its face) at the ends, the middle and every 0.5 m
                int steps = Math.Max(2, (int)Math.Ceiling(len / 0.5f));
                for (int i = 0; i <= steps; i++)
                {
                    float t = m.a + (m.b - m.a) * i / steps;
                    t = Math.Max(m.a + 0.002f, Math.Min(m.b - 0.002f, t));
                    float x = m.alongX ? t : m.line - m.nrm * 0.01f, z = m.alongX ? m.line - m.nrm * 0.01f : t;
                    float ymid = m.header ? (m.y0 + m.y1) * 0.5f : 1.5f;
                    bool inside = false;
                    for (int k = 0; k < solids.Count; k++)
                        if (solids[k].Contains(x, z) && solidY[k][0] <= ymid + 1e-3f && solidY[k][1] >= ymid - 1e-3f) { inside = true; break; }
                    if (!inside) { offWall++; if (first == "") first = "(" + WsF(x) + "," + WsF(z) + ") " + m.style + " " + (m.alongX ? "x" : "z") + " " + m.nrm; break; }
                }
            }
            Check(tag + "every piece stands on a wall, never across a doorway or in the air", offWall == 0, offWall + " off the wall " + first);
            Check(tag + "every piece is 5 cm to 2.5 m long, full height (or the beam's height)", wrongLen == 0 && wrongY == 0, wrongLen + " wrong length, " + wrongY + " wrong height/side");
            Check(tag + "nothing goes on the outside walls", outsideWall == 0, outsideWall + "");

            // 2. nothing on the walls that are not skinned: cubicle dividers (kind panel), the beams over locked doors, anything lower than full height
            int low = 0;
            foreach (var m in plan.modules)
            {
                if (m.shell) continue;
                float xm = m.alongX ? (m.a + m.b) * 0.5f : m.line - m.nrm * 0.01f, zm = m.alongX ? m.line - m.nrm * 0.01f : (m.a + m.b) * 0.5f;
                bool ok = false;
                foreach (var w in L.walls)
                    if (w.rc.Contains(xm, zm) && (w.kind == "wall" || (w.kind == "header" && m.header)) && w.y1 >= wallH - 0.01f && Math.Abs(w.y0 - m.y0) < 0.01f) { ok = true; break; }
                if (!ok) low++;
            }
            Check(tag + "no piece on a cubicle divider, a locked door's beam or a low wall", low == 0, low + "");

            // 3. the guard outpost keeps its own walls
            if (L.outpost != null)
            {
                var op = L.outpost.outer.Grow(0.02f); int inOp = 0;
                foreach (var m in plan.modules)
                {
                    float xm = m.alongX ? (m.a + m.b) * 0.5f : m.line - m.nrm * 0.01f, zm = m.alongX ? m.line - m.nrm * 0.01f : (m.a + m.b) * 0.5f;
                    if (!m.shell && op.Contains(xm, zm)) inOp++;
                }
                Check(tag + "the guard outpost's own walls get no pieces", inOp == 0, inOp + "");
            }

            // 3b. no piece is wasted on a face that another wall hides (a partition standing against the wall)
            int hiddenMods = 0;
            foreach (var m in plan.modules)
            {
                float mid = (m.a + m.b) * 0.5f;
                float px = m.alongX ? mid : m.line + m.nrm * 0.01f, pz = m.alongX ? m.line + m.nrm * 0.01f : mid;
                for (int k = 0; k < solids.Count; k++)
                    if (solids[k].Contains(px, pz) && solidY[k][0] <= m.y0 + 0.01f && solidY[k][1] >= m.y1 - 0.01f) { hiddenMods++; break; }
            }
            Check(tag + "no piece on a face that another wall hides", hiddenMods == 0, hiddenMods + "");

            // 4. coverage: every sampled spot on a skinned face that no other wall hides has a piece (and only one), on the right side
            int missing = 0, doubled = 0, samples = 0; string miss1 = "", dbl1 = "";
            var faces = new List<object[]>();       // rc, y0, y1, isShell
            foreach (var w in L.walls) if (w.kind == "wall" && w.y0 < 0.01f && w.y1 >= wallH - 0.01f) faces.Add(new object[] { w.rc, w.y0, w.y1, false, w });
            foreach (var s in shell) if (!s.Value) faces.Add(new object[] { s.Key, 0f, wallH, true, null });
            foreach (var f in faces)
            {
                var r = (Rc)f[0]; bool alongX = r.W > r.D; float lo = alongX ? r.x0 : r.z0, hi = alongX ? r.x1 : r.z1;
                if (hi - lo < 0.2f) continue;
                if (L.outpost != null && r.x0 >= L.outpost.outer.x0 - 0.02f && r.x1 <= L.outpost.outer.x1 + 0.02f && r.z0 >= L.outpost.outer.z0 - 0.02f && r.z1 <= L.outpost.outer.z1 + 0.02f && !(bool)f[3]) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    float line = alongX ? (side > 0 ? r.z1 : r.z0) : (side > 0 ? r.x1 : r.x0);
                    for (float t = lo + 0.13f; t < hi - 0.1f; t += 0.37f)
                    {
                        float px = alongX ? t : line + side * 0.01f, pz = alongX ? line + side * 0.01f : t;
                        bool hidden = false;
                        for (int k = 0; k < solids.Count; k++)
                            if (!(solids[k].x0 == r.x0 && solids[k].z0 == r.z0 && solids[k].x1 == r.x1 && solids[k].z1 == r.z1) && solids[k].Contains(px, pz) && solidY[k][0] <= 0.01f && solidY[k][1] >= wallH - 0.01f) { hidden = true; break; }
                        if (hidden) continue;
                        samples++;
                        int n = 0;
                        foreach (var m in plan.modules)
                            if (!m.header && m.alongX == alongX && m.nrm == side && Math.Abs(m.line - line) < 1e-3f && t >= m.a - 1e-4f && t < m.b - 1e-4f) n++;
                        if (n == 0) { missing++; if (miss1 == "") miss1 = "(" + WsF(px) + "," + WsF(pz) + ")"; }
                        else if (n > 1) { doubled++; if (dbl1 == "") dbl1 = "(" + WsF(px) + "," + WsF(pz) + ") " + WsR(r); }
                    }
                }
            }
            Check(tag + "every open stretch of wall face has a piece (" + samples + " spots looked at)", missing == 0 && samples > 500, missing + " bare " + miss1);
            Check(tag + "no stretch of wall face has two pieces", doubled == 0, doubled + " " + dbl1);

            // 5. the pieces of one face follow each other: no overlap between two neighbours (a run is the pieces on one face of one wall at one height)
            var runs = plan.modules.GroupBy(m => (m.header ? "h" : "w") + (m.alongX ? "x" : "z") + m.nrm + "|" + WsF(m.line) + "|" + WsF(m.y0) + "|" + WsF(m.y1));
            int gaps = 0;
            foreach (var g in runs)
            {
                var l = g.OrderBy(m => m.a).ToList();
                for (int i = 0; i + 1 < l.Count; i++)
                    if (l[i].b > l[i + 1].a + 1e-3f) gaps++;
            }
            Check(tag + "pieces of one face never overlap", gaps == 0, gaps + "");

            // 6. the accents: each accent piece is on an accent face the rule picked; the accent faces are real full-height partitions of a room
            int stray = 0;
            foreach (var m in plan.modules)
            {
                if (!m.accent) continue;
                bool ok = false;
                foreach (var af in plan.accentList)
                {
                    var r = af.wall.rc; bool alongX = r.W > r.D;
                    int want = alongX ? (af.side == 1 ? 1 : -1) : (af.side == 3 ? 1 : -1);
                    float line = alongX ? (af.side == 1 ? r.z1 : r.z0) : (af.side == 3 ? r.x1 : r.x0);
                    if (m.alongX == alongX && m.nrm == want && Math.Abs(m.line - line) < 1e-3f && m.a >= af.a - WallSkinRules.MinFace - 1e-3f && m.b <= af.b + WallSkinRules.MinFace + 1e-3f) { ok = true; break; }
                }
                if (!ok) stray++;
            }
            Check(tag + "every accent piece is on a face the accent rule picked", stray == 0, stray + "");
            bool accentsReal = plan.accentList.All(af => af.wall.kind == "wall" && af.wall.y1 - af.wall.y0 >= wallH * 0.8f && af.b - af.a >= 3f && af.side >= 1 && af.side <= 4 && af.paint != null && af.paint.Length >= 3 && af.paint.All(v => v >= 0f && v <= 1f)
                && (af.brick == WallSkinRules.BrickRoom(af.theme)));
            Check(tag + "the accent faces are 3 m or more of full wall, painted in range, brick only in the brick rooms", accentsReal);
            Check(tag + "no accent in the corridors or the lobby rings", plan.accentList.All(af => af.theme != "hall" && af.theme != "service" && af.theme != "hub"));

            // 7. paint and moulding follow the room: one paint along each unbroken stretch of plain wall (the pieces that follow each other with a join), the paint of the room at the middle of the stretch
            //    (a stretch is one room nearly always; along an open zone it can look into two, and then it keeps one paint rather than changing colour in the middle of a wall); brick only on accent faces; a beam is plain
            int paintWrong = 0, brickWrong = 0, hdrWrong = 0, tintBad = 0, chains = 0; string pw1 = "";
            foreach (var g in runs)
            {
                var l = g.OrderBy(m => m.a).ToList();
                int i = 0;
                while (i < l.Count)
                {
                    int j = i;
                    while (j + 1 < l.Count && l[j].joinB && Math.Abs(l[j].b - l[j + 1].a) < 1e-3f && l[j].accent == l[j + 1].accent) j++;
                    var head = l[i]; var last = l[j]; chains++;
                    bool same = true;
                    for (int k = i; k <= j; k++) same &= l[k].style == head.style && Math.Abs(l[k].tr - head.tr) < 1e-5f && Math.Abs(l[k].tg - head.tg) < 1e-5f && Math.Abs(l[k].tb - head.tb) < 1e-5f && l[k].room == head.room;
                    if (!same) { paintWrong++; if (pw1 == "") pw1 = "mixed stretch at " + WsF(head.a) + ".." + WsF(last.b); }
                    if (!head.header && !head.accent)
                    {
                        float mid = (head.a + last.b) * 0.5f;
                        float px = head.alongX ? mid : head.line + head.nrm * 0.4f, pz = head.alongX ? head.line + head.nrm * 0.4f : mid;
                        string name; string theme = WallSkinRules.RoomThemeAt(L, px, pz, out name);
                        var pnt = WallSkinRules.PaintOf(theme);
                        if (pnt.style != head.style || Math.Abs(WallSkinRules.TintOf(pnt.r, WallSkinRules.BaseBeige[0]) - head.tr) > 1e-4f) { paintWrong++; if (pw1 == "") pw1 = "(" + WsF(px) + "," + WsF(pz) + ") " + theme + " vs " + head.style + " room " + head.room; }
                    }
                    i = j + 1;
                }
            }
            foreach (var m in plan.modules)
            {
                if (m.tr < 0.2f - 1e-4f || m.tr > 1.6f + 1e-4f || m.tg < 0.2f - 1e-4f || m.tg > 1.6f + 1e-4f || m.tb < 0.2f - 1e-4f || m.tb > 1.6f + 1e-4f) tintBad++;
                if (m.style == SkinStyle.Brick && !m.accent) brickWrong++;
                if (m.header && m.style != SkinStyle.Blank) hdrWrong++;
            }
            Check(tag + "each unbroken stretch of plain wall has one paint and skirting, the ones of the room at its middle (" + chains + " stretches)", paintWrong == 0 && chains > 50, paintWrong + " " + pw1);
            Check(tag + "brick is only on accent faces, the beams are plain, every tint is in range", brickWrong == 0 && hdrWrong == 0 && tintBad == 0, brickWrong + "/" + hdrWrong + "/" + tintBad);

            // 8. the ceiling: a grid of tiles that fills the floor exactly, none overlapping, none tiny or huge
            double area = 0; bool inside2 = true, sized = true;
            foreach (var t in plan.tiles)
            {
                area += (t.x1 - t.x0) * (t.z1 - t.z0);
                if (t.x0 < -1e-3f || t.z0 < -1e-3f || t.x1 > width + 1e-3f || t.z1 > depth + 1e-3f) inside2 = false;
                float w = t.x1 - t.x0, d = t.z1 - t.z0;
                if (w < 2.4f || w > 3.6f || d < 2.4f || d > 3.6f) sized = false;
                if (t.tr < 0.2f || t.tr > 1.6f || t.tg < 0.2f || t.tg > 1.6f || t.tb < 0.2f || t.tb > 1.6f) sized = false;
            }
            Check(tag + "the ceiling tiles fill the floor exactly (" + plan.tiles.Count + " tiles)", Math.Abs(area - width * depth) < 0.05 && inside2 && plan.tiles.Count > 10, area + " vs " + (width * depth));
            Check(tag + "the ceiling tiles are about 3 m square", sized);
            bool noOverlap = true;
            if (heavy)
            {
                var ts = plan.tiles;
                for (int i = 0; i < ts.Count && noOverlap; i++)
                    for (int j = i + 1; j < ts.Count; j++)
                        if (ts[i].x0 < ts[j].x1 - 1e-3f && ts[i].x1 > ts[j].x0 + 1e-3f && ts[i].z0 < ts[j].z1 - 1e-3f && ts[i].z1 > ts[j].z0 + 1e-3f) { noOverlap = false; break; }
            }
            else
            {
                var key = new HashSet<string>(plan.tiles.Select(t => WsF(t.x0) + "," + WsF(t.z0)));
                noOverlap = key.Count == plan.tiles.Count;
            }
            Check(tag + "no two ceiling tiles overlap", noOverlap);

            // 9. the cost: how many pieces
            Check(tag + "the number of pieces is sane (" + plan.modules.Count + " pieces + " + plan.tiles.Count + " tiles)", plan.modules.Count < 6000 && plan.tiles.Count < 3000);
        }
    }
}
