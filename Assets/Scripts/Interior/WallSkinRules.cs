using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>Which wall piece of the POLYGON Office pack a stretch of wall is made of.</summary>
    public enum SkinStyle { Baseboard = 0, Trim = 1, Blank = 2, Brick = 3 }

    /// <summary>
    /// One piece of pack wall (SM_Bld_Wall_*_01), stretched along one face of one wall box. The face is described the way the planner describes walls: <see cref="alongX"/> says the wall runs along x
    /// (its faces look along z), <see cref="nrm"/> which way the face looks (+1 / -1 along the other axis), <see cref="line"/> where the face is (z when alongX, else x), <see cref="a"/> .. <see cref="b"/>
    /// how far it reaches along the wall and <see cref="y0"/> .. <see cref="y1"/> how high.
    /// </summary>
    public sealed class SkinModule
    {
        public SkinStyle style;
        public bool alongX;
        public int nrm;
        public float line, a, b, y0, y1;
        /// <summary>A free end of the wall (a doorway jamb, the end of a stub): the piece reaches a hair past it so that its end face hides the end of the wall box.</summary>
        public bool capA, capB;
        /// <summary>This piece is followed by another of the same paint (the next stretch of the same face): it reaches a few millimetres into it, so no crack of the old wall shows between them.</summary>
        public bool joinB;
        /// <summary>The colour the piece is multiplied by (the pack's wall is beige: 1,1,1 leaves it beige).</summary>
        public float tr = 1f, tg = 1f, tb = 1f;
        public bool header, accent, shell;
        public string room;
        public float Length => b - a;
        /// <summary>The turn (degrees about the vertical axis) that makes the pack piece's decorated face look the way the face looks: the pack's pieces face +z at yaw 0, like every prop.</summary>
        public float Yaw => alongX ? (nrm > 0 ? 0f : 180f) : (nrm > 0 ? 90f : 270f);
    }

    /// <summary>One ceiling tile (SM_Bld_Ceiling_Panel_02, stretched to size): the rectangle it covers.</summary>
    public sealed class SkinTile
    {
        public float x0, z0, x1, z1;
        public float tr = 1f, tg = 1f, tb = 1f;
    }

    public sealed class SkinPlan
    {
        public readonly List<SkinModule> modules = new List<SkinModule>();
        public readonly List<SkinTile> tiles = new List<SkinTile>();
        /// <summary>How many accent stretches the plan made (one per room that has one) and how many of those are brick.</summary>
        public int accentFaces, brickFaces;
        /// <summary>Wall boxes that were left as they were (glass fronts, the guard outpost's walls, odd little boxes).</summary>
        public int leftAlone;
        public readonly Dictionary<SkinStyle, int> perStyle = new Dictionary<SkinStyle, int>();
        /// <summary>The wall boxes of the layout that got at least one piece (the builder gives their old boxes the plain paint of the halls, so a sliver of the old wall matches).</summary>
        public readonly HashSet<WallBox> boxes = new HashSet<WallBox>();
        /// <summary>The accent stretches the plan made (for tests and pictures).</summary>
        public readonly List<WallSkinRules.AccentFace> accentList = new List<WallSkinRules.AccentFace>();
    }

    /// <summary>
    /// The Floor 7 wall and ceiling pass (the owner, 2026-10-23: "replacing the walls with the walls from our asset as well as the ceiling. right now the walls seem a little blank"): where the pack's wall pieces and
    /// ceiling tiles go. Pure data, no Unity types, so the whole plan can be made and checked outside the editor. The plan only DRESSES what is there: the wall boxes (the colliders, the NavMesh, the doorways), the light panels and
    /// the layout are not touched; a piece of pack wall is laid over each face of a wall (a hair, 4 mm, proud of it: the room number plates and signs stand 6 to 18 mm off the wall, so they stay in front) and the tiles hang a few centimetres under the old flat ceiling.
    ///  - every face of every full-height partition and of every wall between two cells is covered, except where another wall stands against it, the glass fronts of the offices, and the guard outpost (its walls are its own dark slate);
    ///    the outside walls are not touched either (they have the windows: FloorGenerator.Surfaces.cs);
    ///  - the beams over the doorways get a plain piece;
    ///  - the pieces are 2.5 m long (the pack's length) and stretched a little to fit, so there is never a short odd one; they stretch only along the wall, so the mouldings keep their size;
    ///  - each face takes the paint and the moulding of the ROOM it looks into (offices and meeting rooms have a dado, corridors and stores a plain skirting), and the one accent wall of each room (the same wall and colour the
    ///    flat accent paint had, see FloorGenerator.Surfaces) is made of the same pieces: painted, or brick in the lounges, break rooms and canteens;
    ///  - the ceiling is a grid of 3 m tiles over the whole floor.
    /// </summary>
    public static class WallSkinRules
    {
        /// <summary>The one floor this is tried on.</summary>
        public const int SkinnedFloor = 7;
        public static bool AppliesTo(int floor) => floor == SkinnedFloor;

        /// <summary>The pack's wall piece: 2.5 m long, 2.997 m high (the module the planner stretches), and the base colour of its beige (so a paint colour can be turned into a tint).</summary>
        public const float ModuleLength = 2.5f, ModuleHeight = 2.997f;
        public static readonly float[] BaseBeige = { 0.757f, 0.686f, 0.592f };
        /// <summary>The pack's ceiling tile (grey, 2.5 m square) and the size of the tiles of the plan.</summary>
        public static readonly float[] BaseGrey = { 0.702f, 0.686f, 0.675f };
        public const float TileTarget = 3.0f;
        /// <summary>How far a piece stands off the face of the box it covers (the flat part of the piece; its mouldings stand another 1.4 cm out), and how far it reaches past a free end.</summary>
        public const float Lift = 0.004f, EndOver = 0.012f, JoinOver = 0.005f;
        /// <summary>The mouldings of the Baseboard and Trim pieces stand this far proud of their flat face.</summary>
        public const float Bump = 0.014f;
        /// <summary>Wall faces shorter than this get no piece (a sliver that nobody sees).</summary>
        public const float MinFace = 0.05f;

        // ------------------------------------------------------------------ paint

        public struct Paint
        {
            public SkinStyle style;
            public float r, g, b;
            public Paint(SkinStyle s, float r, float g, float b) { style = s; this.r = r; this.g = g; this.b = b; }
        }

        /// <summary>The paint of a room's walls, as the colour you see (a tint of the pack's beige is worked out from it).</summary>
        public static Paint PaintOf(string theme)
        {
            switch (theme)
            {
                case "office": return new Paint(SkinStyle.Baseboard, 0.88f, 0.84f, 0.76f);
                case "conference": case "meeting": case "training": return new Paint(SkinStyle.Baseboard, 0.76f, 0.81f, 0.80f);
                case "lounge": case "nook": return new Paint(SkinStyle.Baseboard, 0.88f, 0.78f, 0.72f);
                case "break": case "canteen": case "cafe_counter": case "cafe_seating": return new Paint(SkinStyle.Baseboard, 0.92f, 0.86f, 0.66f);
                case "server": return new Paint(SkinStyle.Trim, 0.68f, 0.73f, 0.78f);
                case "storage": case "copy": case "restroom": return new Paint(SkinStyle.Trim, 0.80f, 0.79f, 0.76f);
                case "open": case "longopen": return new Paint(SkinStyle.Trim, 0.86f, 0.84f, 0.80f);
                case "lobby": return new Paint(SkinStyle.Baseboard, 0.90f, 0.85f, 0.74f);
                default: return new Paint(SkinStyle.Trim, 0.88f, 0.85f, 0.78f);        // hall, hub, service and anything new
            }
        }

        public static float TintOf(float colour, float baseColour) => Math.Max(0.2f, Math.Min(1.6f, colour / baseColour));

        // ------------------------------------------------------------------ the accent wall of each room (the same pick FloorGenerator.BuildAccentWalls makes)

        static readonly float[][] AccentTeal = { C(0.10f, 0.58f, 0.64f), C(0.14f, 0.42f, 0.58f), C(0.36f, 0.62f, 0.38f), C(0.36f, 0.38f, 0.72f), C(0.86f, 0.42f, 0.24f), C(0.90f, 0.70f, 0.20f) };
        static readonly float[][] AccentWarm = { C(0.86f, 0.42f, 0.24f), C(0.90f, 0.70f, 0.20f), C(0.36f, 0.62f, 0.38f), C(0.80f, 0.28f, 0.42f) };
        static readonly float[][] AccentSoft = { C(0.58f, 0.34f, 0.62f), C(0.80f, 0.28f, 0.42f), C(0.10f, 0.58f, 0.64f), C(0.36f, 0.38f, 0.72f) };
        static readonly float[][] AccentDark = { C(0.14f, 0.42f, 0.58f), C(0.34f, 0.42f, 0.52f) };
        static float[] C(float r, float g, float b) => new[] { r, g, b };

        static float[] AccentFor(string theme, int h)
        {
            float[][] set;
            switch (theme)
            {
                case "break": case "canteen": case "cafe_counter": case "cafe_seating": set = AccentWarm; break;
                case "lounge": case "nook": case "lobby": set = AccentSoft; break;
                case "server": case "storage": case "copy": case "service": set = AccentDark; break;
                default: set = AccentTeal; break;
            }
            return set[h % set.Length];
        }

        static int Mix2(int a, int b) { unchecked { int h = a * 73856093 ^ b * 19349663; return h ^ (h >> 15); } }
        static int RoundI(float v) => (int)Math.Round(v, MidpointRounding.ToEven);

        /// <summary>Rooms whose accent wall is brick instead of paint (the cosy ones).</summary>
        public static bool BrickRoom(string theme) => theme == "lounge" || theme == "nook" || theme == "break" || theme == "canteen" || theme == "cafe_seating";

        /// <summary>One room's accent stretch: which wall box, which side of it (1: the face looks +z, 2: -z, 3: +x, 4: -x), how far along, and the paint.</summary>
        public sealed class AccentFace
        {
            public WallBox wall;
            public int side;
            public float a, b;
            public float[] paint;
            public bool brick;
            public string theme;
        }

        /// <summary>
        /// The longest stretch of full-height partition (3 m or more) along the edge of each room (not the corridors, not the guard outpost, not a glass front), on the room's side: the same rule and the same colours as
        /// FloorGenerator.BuildAccentWalls, so a floor keeps the accent walls it had.
        /// </summary>
        public static List<AccentFace> AccentFaces(Layout L, float wallHeight, ISet<WallBox> glass)
        {
            var res = new List<AccentFace>();
            foreach (var room in L.rooms)
            {
                if (room.kind == 2 || (L.outpost != null && L.outpost.room == room)) continue;
                var rb = room.bounds;
                WallBox best = null; float bestLen = 3f, bestA = 0f, bestB = 0f; int bestSide = 0;
                foreach (var w in L.walls)
                {
                    if (w.kind != "wall" || w.y1 - w.y0 < wallHeight * 0.8f || (glass != null && glass.Contains(w))) continue;
                    var r = w.rc;
                    bool horizontal = r.W > r.D;
                    if ((horizontal ? r.D : r.W) > 0.6f) continue;
                    float a, b; int side;
                    if (horizontal)
                    {
                        a = Math.Max(r.x0, rb.x0 + 0.15f); b = Math.Min(r.x1, rb.x1 - 0.15f);
                        if (Math.Abs(r.z1 - rb.z0) < 0.5f && r.Cz < rb.Cz) side = 1;
                        else if (Math.Abs(r.z0 - rb.z1) < 0.5f && r.Cz > rb.Cz) side = 2;
                        else continue;
                    }
                    else
                    {
                        a = Math.Max(r.z0, rb.z0 + 0.15f); b = Math.Min(r.z1, rb.z1 - 0.15f);
                        if (Math.Abs(r.x1 - rb.x0) < 0.5f && r.Cx < rb.Cx) side = 3;
                        else if (Math.Abs(r.x0 - rb.x1) < 0.5f && r.Cx > rb.Cx) side = 4;
                        else continue;
                    }
                    if (b - a > bestLen) { best = w; bestLen = b - a; bestA = a; bestB = b; bestSide = side; }
                }
                if (best == null) continue;
                int h = Mix2(RoundI(rb.Cx * 3f), RoundI(rb.Cz * 3f));
                h = h < 0 ? unchecked(-h) : h;
                if (h < 0) h = 0;
                bool brick = BrickRoom(room.theme);
                res.Add(new AccentFace { wall = best, side = bestSide, a = bestA, b = bestB, paint = AccentFor(room.theme, h), brick = brick, theme = room.theme });
            }
            return res;
        }

        // ------------------------------------------------------------------ the plan

        sealed class W
        {
            public Rc rc;
            public float y0, y1;
            public bool header, skin, shell;
            public WallBox box;
            public bool AlongX => rc.W > rc.D;
        }

        public sealed class Options
        {
            /// <summary>Keep the guard outpost's walls dark slate (they are not covered).</summary>
            public bool skipOutpost = true;
            /// <summary>Make the accent walls (otherwise every face takes its room's paint).</summary>
            public bool accents = true;
            /// <summary>The beams over the doorways (a plain piece); off leaves them as the old boxes.</summary>
            public bool headers = true;
        }

        /// <summary>
        /// The plan for a floor. <paramref name="glass"/> are the wall boxes the glass fronts replaced (left alone). The walls between the cells and the outside walls come from the grid in <c>L.input</c>
        /// (FloorGenerator.BuildShell makes them: 0.4 m thick on the cell lines, a gap of <paramref name="doorWidth"/> where the maze is open).
        /// </summary>
        public static SkinPlan Plan(Layout L, ISet<WallBox> glass, float doorWidth, Options opt = null)
        {
            opt = opt ?? new Options();
            var plan = new SkinPlan();
            var inp = L.input;
            float cell = inp.cell, wallH = inp.wallH;
            var ws = new List<W>();
            bool hasOp = L.outpost != null && opt.skipOutpost;
            var opArea = hasOp ? L.outpost.outer.Grow(0.02f) : default(Rc);

            foreach (var w in L.walls)
            {
                bool full = w.y0 < 0.01f && w.y1 >= wallH - 0.01f;
                bool hdr = (w.kind == "header" || w.kind == "lockheader") && w.y1 >= wallH - 0.01f;
                if (!full && !hdr) { plan.leftAlone++; continue; }
                var r = w.rc;
                bool mine = hasOp && r.x0 >= opArea.x0 && r.x1 <= opArea.x1 && r.z0 >= opArea.z0 && r.z1 <= opArea.z1;
                bool skin = !mine && !(glass != null && glass.Contains(w)) && (full ? w.kind == "wall" : (w.kind == "header" && opt.headers));
                if (!skin) plan.leftAlone++;
                ws.Add(new W { rc = r, y0 = w.y0, y1 = w.y1, header = hdr && !full, skin = skin, box = w });
            }

            // the walls between the cells (skinned) and round the building (only in the way: its faces have the windows)
            float gap = doorWidth, half = (cell - gap) / 2f;
            for (int c = 0; c <= inp.cols; c++)
                for (int r = 0; r < inp.rows; r++)
                {
                    bool edge = c == 0 || c == inp.cols;
                    bool solid = edge || !inp.openE[c - 1, r];
                    float x = c * cell, z0 = r * cell;
                    if (solid) ws.Add(Shell(new Rc(x - 0.2f, z0, x + 0.2f, z0 + cell), wallH, !edge));
                    else
                    {
                        ws.Add(Shell(new Rc(x - 0.2f, z0, x + 0.2f, z0 + half), wallH, true));
                        ws.Add(Shell(new Rc(x - 0.2f, z0 + cell - half, x + 0.2f, z0 + cell), wallH, true));
                    }
                }
            for (int r = 0; r <= inp.rows; r++)
                for (int c = 0; c < inp.cols; c++)
                {
                    bool edge = r == 0 || r == inp.rows;
                    bool solid = edge || !inp.openN[c, r - 1];
                    float z = r * cell, x0 = c * cell;
                    if (solid) ws.Add(Shell(new Rc(x0, z - 0.2f, x0 + cell, z + 0.2f), wallH, !edge));
                    else
                    {
                        ws.Add(Shell(new Rc(x0, z - 0.2f, x0 + half, z + 0.2f), wallH, true));
                        ws.Add(Shell(new Rc(x0 + cell - half, z - 0.2f, x0 + cell, z + 0.2f), wallH, true));
                    }
                }

            var accents = opt.accents ? AccentFaces(L, wallH, glass) : new List<AccentFace>();
            var accentOf = new Dictionary<WallBox, List<AccentFace>>();
            foreach (var af in accents)
            {
                if (!accentOf.TryGetValue(af.wall, out var l)) accentOf[af.wall] = l = new List<AccentFace>();
                l.Add(af);
            }

            foreach (var w in ws)
            {
                if (!w.skin) continue;
                var r = w.rc;
                bool alongX = w.AlongX;
                float thick = alongX ? r.D : r.W, len = alongX ? r.W : r.D;
                if (len < MinFace) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    float line = alongX ? (side > 0 ? r.z1 : r.z0) : (side > 0 ? r.x1 : r.x0);
                    float lo = alongX ? r.x0 : r.z0, hi = alongX ? r.x1 : r.z1;
                    var spans = new List<float[]> { new[] { lo, hi } };
                    // another wall that stands against this face (or this face runs into) hides that stretch of it
                    float probe = line + side * 0.01f;
                    foreach (var o in ws)
                    {
                        if (ReferenceEquals(o, w) || o.y0 > w.y0 + 0.01f || o.y1 < w.y1 - 0.01f) continue;
                        var q = o.rc;
                        float o0 = alongX ? q.z0 : q.x0, o1 = alongX ? q.z1 : q.x1;
                        if (!(o0 < probe && probe < o1)) continue;
                        float s0 = alongX ? q.x0 : q.z0, s1 = alongX ? q.x1 : q.z1;
                        if (s1 <= lo + 1e-4f || s0 >= hi - 1e-4f) continue;
                        Subtract(spans, s0, s1);
                        if (spans.Count == 0) break;
                    }
                    if (spans.Count == 0) continue;

                    // the accent stretch of this face, if it is one
                    AccentFace acc = null;
                    if (!w.shell && !w.header && w.box != null && accentOf.TryGetValue(w.box, out var al))
                    {
                        int want = alongX ? (side > 0 ? 1 : 2) : (side > 0 ? 3 : 4);
                        foreach (var af in al) if (af.side == want) { acc = af; break; }
                    }

                    foreach (var sp in spans)
                    {
                        if (sp[1] - sp[0] < MinFace) continue;
                        var parts = new List<float[]>();                       // {from, to, accent 0/1}
                        if (acc != null && acc.b > sp[0] + MinFace && acc.a < sp[1] - MinFace)
                        {
                            float a0 = Math.Max(sp[0], acc.a), a1 = Math.Min(sp[1], acc.b);
                            if (a0 - sp[0] < MinFace) a0 = sp[0];            // (a sliver of plain wall beside the accent joins the accent)
                            if (sp[1] - a1 < MinFace) a1 = sp[1];
                            if (a0 > sp[0]) parts.Add(new[] { sp[0], a0, 0f });
                            parts.Add(new[] { a0, a1, 1f });
                            if (a1 < sp[1]) parts.Add(new[] { a1, sp[1], 0f });
                        }
                        else parts.Add(new[] { sp[0], sp[1], 0f });
                        foreach (var pt in parts)
                        {
                            bool isAcc = pt[2] > 0.5f;
                            var theme = RoomThemeAt(L, alongX ? (pt[0] + pt[1]) * 0.5f : line + side * 0.4f, alongX ? line + side * 0.4f : (pt[0] + pt[1]) * 0.5f, out string roomName);
                            var pnt = PaintOf(theme);
                            SkinStyle style = w.header ? SkinStyle.Blank : pnt.style;
                            float tr = TintOf(pnt.r, BaseBeige[0]), tg = TintOf(pnt.g, BaseBeige[1]), tb = TintOf(pnt.b, BaseBeige[2]);
                            if (isAcc)
                            {
                                if (acc.brick) { style = SkinStyle.Brick; tr = tg = tb = 1f; }
                                else { tr = TintOf(acc.paint[0], BaseBeige[0]); tg = TintOf(acc.paint[1], BaseBeige[1]); tb = TintOf(acc.paint[2], BaseBeige[2]); style = pnt.style == SkinStyle.Trim ? SkinStyle.Trim : SkinStyle.Baseboard; }
                            }
                            int n = Math.Max(1, (int)Math.Ceiling((pt[1] - pt[0]) / ModuleLength - 1e-4f));
                            float step = (pt[1] - pt[0]) / n;
                            for (int i = 0; i < n; i++)
                            {
                                var m = new SkinModule
                                {
                                    style = style, alongX = alongX, nrm = side, line = line,
                                    a = pt[0] + i * step, b = pt[0] + (i + 1) * step, y0 = w.y0, y1 = w.y1,
                                    tr = tr, tg = tg, tb = tb, header = w.header, accent = isAcc, shell = w.shell, room = roomName
                                };
                                m.capA = i == 0 && Math.Abs(pt[0] - lo) < 1e-3f;
                                m.capB = i == n - 1 && Math.Abs(pt[1] - hi) < 1e-3f;
                                m.joinB = i < n - 1;
                                plan.modules.Add(m);
                                if (w.box != null) plan.boxes.Add(w.box);
                                plan.perStyle[style] = (plan.perStyle.TryGetValue(style, out int cnt) ? cnt : 0) + 1;
                            }
                        }
                    }
                }
            }
            foreach (var af in accents) { plan.accentFaces++; if (af.brick) plan.brickFaces++; plan.accentList.Add(af); }

            // the ceiling: a grid of tiles that divides the floor evenly
            float width = inp.cols * cell, depth = inp.rows * cell;
            int nx = Math.Max(1, RoundI(width / TileTarget)), nz = Math.Max(1, RoundI(depth / TileTarget));
            float tw = width / nx, td = depth / nz;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    plan.tiles.Add(new SkinTile
                    {
                        x0 = i * tw, x1 = (i + 1) * tw, z0 = j * td, z1 = (j + 1) * td,
                        tr = TintOf(0.80f, BaseGrey[0]), tg = TintOf(0.80f, BaseGrey[1]), tb = TintOf(0.79f, BaseGrey[2])
                    });
            return plan;
        }

        static W Shell(Rc rc, float wallH, bool skin) => new W { rc = rc, y0 = 0f, y1 = wallH, skin = skin, shell = true };

        /// <summary>Takes the stretch [s0, s1] out of a list of stretches.</summary>
        static void Subtract(List<float[]> spans, float s0, float s1)
        {
            for (int i = spans.Count - 1; i >= 0; i--)
            {
                var sp = spans[i];
                if (s1 <= sp[0] || s0 >= sp[1]) continue;
                spans.RemoveAt(i);
                if (s0 > sp[0] + 1e-4f) spans.Add(new[] { sp[0], s0 });
                if (s1 < sp[1] - 1e-4f) spans.Add(new[] { s1, sp[1] });
            }
            spans.Sort((p, q) => p[0].CompareTo(q[0]));
        }

        /// <summary>The theme of the smallest room that contains the spot (the room a wall face looks into), "hall" when there is none.</summary>
        public static string RoomThemeAt(Layout L, float x, float z, out string name)
        {
            RoomInfo best = null; float bestArea = float.MaxValue;
            foreach (var r in L.rooms)
            {
                if (!r.bounds.Contains(x, z)) continue;
                float area = r.bounds.W * r.bounds.D;
                if (area < bestArea) { best = r; bestArea = area; }
            }
            name = best == null ? "none" : best.theme;
            return best == null || best.theme == null ? "hall" : best.theme;
        }
    }
}
