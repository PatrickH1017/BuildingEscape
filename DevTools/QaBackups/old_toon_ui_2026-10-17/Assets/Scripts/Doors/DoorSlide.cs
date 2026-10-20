using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// Where the two leaves of a sliding door go when it opens. The owner, 2026-10-17: "The doors also currently slide into nothing so the glass looks like it is bending when a door is shut,
    /// they should technically slide into a wall, granted the wall next to the door is a size capable of holding a sliding door of that size within it" and "Doors next to each other should
    /// not overlap when both sliding into the wall". A leaf used to narrow into the jamb (its glass squashed with it); now it keeps its size and slides sideways into the wall beside the
    /// doorway (the wall piece is a solid box, so the leaf is out of sight inside it). Where two doors share one piece of wall, the piece is divided between them, so their leaves never meet:
    /// when it is too short for both, each slides in as far as its share goes (the rest of its leaf stays in the doorway, like a door frame). A piece that is a glass front (a window wall) has
    /// no room inside it: a leaf there slides in front of the glass, on the corridor's side, like the glass sliding doors of a real office. A door whose walls have no room for it at all
    /// narrows into the jambs as before. Plain data and arithmetic, no Unity types, so it is tested outside Unity.
    /// </summary>
    public static class DoorSlide
    {
        /// <summary>Kept clear at the end of a piece of wall, and between the leaves of two doors that share one.</summary>
        public const float EndMargin = 0.04f, LeafGap = 0.06f;
        /// <summary>A door that can slide out of the doorway less than this share of the way narrows into its jambs instead.</summary>
        public const float MinUseful = 0.7f;
        /// <summary>Two rectangles whose edges are closer than this touch.</summary>
        const float Touch = 0.06f;

        /// <summary>How one door opens. Sides are in the door's own frame (its local x: the low leaf is at -x, the high one at +x; its local +z is the lane side).</summary>
        public struct Plan
        {
            /// <summary>False: the leaves narrow into the jambs (the old way).</summary>
            public bool slide;
            /// <summary>How far each leaf slides out of the doorway, in metres (at most the leaf's own width, half the doorway).</summary>
            public float lowTravel, highTravel;
            /// <summary>0: inside the wall (hidden); +1 / -1: in front of the wall on its +z / -z side (a glass front).</summary>
            public int lowLane, highLane;
            /// <summary>For a door that cannot slide (<see cref="slide"/> false): the side its leaves swing open to, +1 / -1 across the wall (into the room the door belongs to; the guard outpost's door opens out into the corridor).</summary>
            public int swing;
        }

        sealed class Piece
        {
            public float lo, hi, perp, thick;          // along the wall, its middle line across it, its thickness
            public bool alongX;
            public int overlay;                        // 0 solid wall; +-1 a glass front, the side of it that is in front (the corridor): in the world's +/- direction across it
            public int a = -1, b = -1;                 // the door whose high side touches this piece's low end (a) and the door whose low side touches its high end (b)
            public float travelA, travelB;
        }

        sealed class Beam
        {
            public float lo, hi, perp, thick;
            public bool alongX;
            public int loPiece = -1, hiPiece = -1;
        }

        /// <param name="beams">The beam (header box) over each doorway.</param>
        /// <param name="walls">Every wall box that stands from the floor to the ceiling (the solid pieces beside the doorways).</param>
        /// <param name="overlay">For each of those walls: 0 for a solid wall, +1 / -1 for a glass front (the side of it, across the wall, where a leaf may slide in front of it: the corridor's).</param>
        public static Plan[] PlanAll(IList<Rc> beams, IList<Rc> walls, IList<int> overlay)
        {
            var plans = new Plan[beams.Count];
            var pieces = new List<Piece>(walls.Count);
            for (int j = 0; j < walls.Count; j++)
            {
                var r = walls[j];
                bool alongX = r.W >= r.D;
                float len = alongX ? r.W : r.D;
                if (len < 0.3f) { pieces.Add(null); continue; }
                pieces.Add(new Piece
                {
                    alongX = alongX, lo = alongX ? r.x0 : r.z0, hi = alongX ? r.x1 : r.z1,
                    perp = alongX ? r.Cz : r.Cx, thick = alongX ? r.D : r.W, overlay = overlay != null && j < overlay.Count ? overlay[j] : 0
                });
            }
            var bs = new List<Beam>(beams.Count);
            for (int i = 0; i < beams.Count; i++)
            {
                var r = beams[i];
                bool alongX = r.W >= r.D;
                var b = new Beam { alongX = alongX, lo = alongX ? r.x0 : r.z0, hi = alongX ? r.x1 : r.z1, perp = alongX ? r.Cz : r.Cx, thick = alongX ? r.D : r.W };
                bs.Add(b);
                for (int j = 0; j < pieces.Count; j++)
                {
                    var p = pieces[j];
                    if (p == null || p.alongX != b.alongX || Math.Abs(p.perp - b.perp) > Touch || Math.Abs(p.thick - b.thick) > Touch) continue;
                    if (Math.Abs(p.hi - b.lo) <= Touch) { b.loPiece = j; p.b = i; }          // the piece ends where the doorway starts: the doorway's low side leaf slides into it
                    else if (Math.Abs(p.lo - b.hi) <= Touch) { b.hiPiece = j; p.a = i; }
                }
            }

            // share every piece between the (at most two) doors that touch it
            foreach (var p in pieces)
            {
                if (p == null || (p.a < 0 && p.b < 0)) continue;
                float len = p.hi - p.lo, free = len - 2f * EndMargin;
                float a = p.a >= 0 ? (bs[p.a].hi - bs[p.a].lo) * 0.5f : 0f, bb = p.b >= 0 ? (bs[p.b].hi - bs[p.b].lo) * 0.5f : 0f;
                if (p.a >= 0 && p.b >= 0)
                {
                    free -= LeafGap;
                    if (free >= a + bb) { p.travelA = a; p.travelB = bb; }
                    else { float k = Math.Max(0f, free) / Math.Max(1e-4f, a + bb); p.travelA = a * k; p.travelB = bb * k; }
                }
                else if (p.a >= 0) p.travelA = Math.Max(0f, Math.Min(a, free));
                else p.travelB = Math.Max(0f, Math.Min(bb, free));
            }

            // each door: its two travels, in its own frame
            for (int i = 0; i < beams.Count; i++)
            {
                var b = bs[i];
                float leaf = (b.hi - b.lo) * 0.5f;
                float loT = 0f, hiT = 0f; int loL = 0, hiL = 0;
                if (b.loPiece >= 0) { var p = pieces[b.loPiece]; loT = Math.Min(leaf, p.travelB); loL = p.overlay; }
                if (b.hiPiece >= 0) { var p = pieces[b.hiPiece]; hiT = Math.Min(leaf, p.travelA); hiL = p.overlay; }
                var plan = new Plan();
                // the door's own frame: for a door in a wall along x its low leaf is on the world's low side; for a wall along z the door is turned a quarter, so its low leaf is on the high side
                if (b.alongX) { plan.lowTravel = loT; plan.highTravel = hiT; plan.lowLane = loL; plan.highLane = hiL; }
                else { plan.lowTravel = hiT; plan.highTravel = loT; plan.lowLane = hiL; plan.highLane = loL; }
                plan.slide = leaf > 0f && plan.lowTravel + plan.highTravel >= MinUseful * 2f * leaf;
                plans[i] = plan;
            }
            return plans;
        }

        /// <summary>
        /// The plans for the doors of a planned floor: <paramref name="beams"/> are the "header" boxes in the order of the layout (the order the doors are built in); the pieces are the layout's
        /// full-height "wall" boxes. <paramref name="overlaySign"/> says, for a wall box that is a glass front, on which side of it (+1 / -1 across it) a leaf may slide in front of it (0: a solid wall).
        /// </summary>
        public static Plan[] ForLayout(Layout layout, float wallHeight, Func<WallBox, int> overlaySign, List<WallBox> beams)
        {
            var rcs = new List<Rc>();
            beams.Clear();
            foreach (var w in layout.walls) if (w.kind == "header" || w.kind == "lockheader") { beams.Add(w); rcs.Add(w.rc); }
            var walls = new List<Rc>(); var overlay = new List<int>();
            foreach (var w in layout.walls)
            {
                if (w.kind != "wall" || w.y0 > 0.01f || w.y1 < wallHeight - 0.01f) continue;
                walls.Add(w.rc);
                overlay.Add(overlaySign != null ? overlaySign(w) : 0);
            }
            var plans = PlanAll(rcs, walls, overlay);
            for (int i = 0; i < plans.Length; i++) plans[i].swing = SwingSign(layout, beams[i]);
            return plans;
        }

        /// <summary>Which way (+1 / -1 across the wall) a door swings open: into the room whose doorway it is (the guard outpost's door: out of the room, into the corridor).</summary>
        public static int SwingSign(Layout layout, WallBox beam)
        {
            var r = beam.rc;
            bool alongX = r.W >= r.D;
            var area = r.Grow(0.05f);
            foreach (var room in layout.rooms)
                foreach (var d in room.doors)
                {
                    if (!d.Overlaps(area)) continue;
                    float across = alongX ? room.bounds.Cz - r.Cz : room.bounds.Cx - r.Cx;
                    int sign = across >= 0f ? 1 : -1;
                    return room.theme == "outpost" ? -sign : sign;
                }
            return 1;
        }
    }
}
