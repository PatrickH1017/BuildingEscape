using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Windows are kept clear (the owner, 2026-10-24: "make sure there aren't clocks and other items on the windows to the outside, that doesn't make sense", and, with pictures of real glass offices, "make sure there aren't too many objects outside offices with glass walls ... filing
    /// cabinets outside glass windows"): once the floor knows where its outside windows are and which walls of its offices are glass, <see cref="ClearViews"/> takes out of the layout (before anything is built from it) every wall prop that hangs on a window opening - a clock, a poster, a
    /// picture, an extinguisher on the outside windows or on a glass front, from either side - and every piece of furniture that blocks the view in the strip of corridor outside a glass front (<see cref="ViewClearRules"/>); what stood on a removed piece goes with it and a
    /// hiding place nobody can hide behind any more goes too. The rules are <see cref="ViewClearRules"/>; the scenario PropScan checks the finished floors.
    /// </summary>
    public partial class FloorGenerator
    {
        /// <summary>An opening in a wall that props must keep clear: a window of the outside wall or a glass front of an office.</summary>
        public struct WindowSpan
        {
            public bool alongX;
            /// <summary>The wall's centre line (z for a wall that runs along x, x for one along z), its half thickness, and its two ends along the wall.</summary>
            public float line, half, lo, hi;
            /// <summary>The opening along the wall, and from where to where it reaches up.</summary>
            public float a, b, sill, head;
            /// <summary>+1 / -1: which way is outside (the corridor, for a glass front).</summary>
            public float outwardSign;
        }

        readonly List<WindowSpan> glassSpans = new List<WindowSpan>();
        readonly List<WindowSpan> exteriorWindows = new List<WindowSpan>();

        /// <summary>For tests: the glass fronts of this floor's offices, and the windows of its outside walls.</summary>
        public IReadOnlyList<WindowSpan> GlassSpans => glassSpans;
        public IReadOnlyList<WindowSpan> ExteriorWindows => exteriorWindows;
        /// <summary>For tests: how many pieces of the layout <see cref="ClearViews"/> took out.</summary>
        public int ViewCleared { get; private set; }

        /// <summary>The windows of the outside walls, worked out the way <c>AddWindowWall</c> builds them (a window in every gap between piers that is 1.2 m wide or more): the piers stand at every bay and where a partition meets the wall.</summary>
        List<WindowSpan> OuterWindowSpans()
        {
            var res = new List<WindowSpan>();
            foreach (var s in outerSegs)
            {
                float length = s.hi - s.lo;
                int bays = Mathf.Max(1, Mathf.RoundToInt(length / WindowBay));
                float bay = length / bays;
                var piers = new List<Vector2>();
                for (int i = 0; i <= bays; i++) { float c = s.lo + i * bay; piers.Add(new Vector2(Mathf.Max(s.lo, c - PierWidth * 0.5f), Mathf.Min(s.hi, c + PierWidth * 0.5f))); }
                foreach (var e in PartitionEnds(s)) piers.Add(new Vector2(e - 0.45f, e + 0.45f));
                piers.Sort((p, q) => p.x.CompareTo(q.x));
                var merged = new List<Vector2>();
                foreach (var p in piers)
                {
                    if (merged.Count > 0 && p.x <= merged[merged.Count - 1].y + 0.01f) merged[merged.Count - 1] = new Vector2(merged[merged.Count - 1].x, Mathf.Max(merged[merged.Count - 1].y, p.y));
                    else merged.Add(p);
                }
                float cursor = s.lo;
                for (int i = 0; i <= merged.Count; i++)
                {
                    float gapEnd = i < merged.Count ? merged[i].x : s.hi;
                    if (gapEnd - cursor >= 1.2f)
                        res.Add(new WindowSpan { alongX = s.alongX, line = s.line, half = 0.2f, lo = s.lo, hi = s.hi, a = cursor, b = gapEnd, sill = SillHeight, head = WindowTopHeight, outwardSign = 0f });
                    if (i < merged.Count) cursor = merged[i].y;
                }
            }
            return res;
        }

        /// <summary>Takes out of the layout what hangs on a window and what blocks the view outside a glass front (see the class summary). Never touches loot, safes, lockers or the guards' outpost.</summary>
        void ClearViews(Layout L)
        {
            ViewCleared = 0;
            exteriorWindows.Clear();
            if (officeFloor) exteriorWindows.AddRange(OuterWindowSpans());
            if (L == null || (exteriorWindows.Count == 0 && glassSpans.Count == 0)) return;
            var op = L.outpost;
            var opArea = op != null ? op.outer.Grow(0.02f) : default(Rc);
            var gone = new HashSet<Piece>();
            foreach (var p in L.pieces)
            {
                if (p.loot || p.picked || p.key == "vault_safe" || p.key == "locker2") continue;
                if (op != null && p.rc.x0 >= opArea.x0 && p.rc.x1 <= opArea.x1 && p.rc.z0 >= opArea.z0 && p.rc.z1 <= opArea.z1) continue;          // (the outpost keeps what it has)
                if (p.mount)
                {
                    foreach (var w in exteriorWindows) if (ViewClearRules.MountOnWall(w.alongX, w.line, w.a, w.b, w.sill, w.head, p.rc, p.y, p.h)) { gone.Add(p); break; }
                    if (!gone.Contains(p)) foreach (var g in glassSpans) if (ViewClearRules.MountOnWall(g.alongX, g.line, g.a, g.b, g.sill, g.head, p.rc, p.y, p.h)) { gone.Add(p); break; }
                }
                else if (ViewClearRules.BlocksView(false, p.y, p.h))
                {
                    foreach (var g in glassSpans)
                        if (p.rc.Overlaps(ViewClearRules.Strip(g.alongX, g.line, g.half, g.lo, g.hi, g.outwardSign, ViewClearRules.ClearDepth))) { gone.Add(p); break; }
                }
            }
            if (gone.Count == 0) return;
            for (bool grew = true; grew;)                                                    // (what stands on a removed piece goes with it: a monitor on a desk, a plant on a cabinet)
            {
                grew = false;
                foreach (var p in L.pieces) if (!gone.Contains(p) && p.host != null && gone.Contains(p.host)) { gone.Add(p); grew = true; }
            }
            ViewCleared = gone.Count;
            L.pieces.RemoveAll(gone.Contains);
            L.hideZones.RemoveAll(z => !L.pieces.Exists(q => q.hide && q.rc.Grow(ViewClearRules.HideReach).Overlaps(z)));          // (a hiding place with nothing left to hide behind)
        }
    }
}
