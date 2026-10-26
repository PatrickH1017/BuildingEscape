using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the Floor 7 doors (<c>DoorSkinRules.cs</c>, 2026-10-23): only floor 7 gets them, the pack's door pieces are in the prop table with the sizes the rules expect, the wide hall doors and the
    /// doors of the glass offices are glass and the others solid (wood for the office-like rooms), a leaf holds one panel in a 2 m doorway and two in the 4.8 m hall doorway, the panels of a leaf tile it exactly without a gap or an
    /// overlap, a panel fills the leaf's unit cube in height and thickness (so the door moves, slides and swings as the old slab did), the handles of a pair meet in the middle and the hinges are at the jambs, and the glass
    /// double door alternates the pack's left and right leaves. Whole floors are planned (several seeds) to prove that every doorway the planner makes is one the rules can dress. Called from <c>NetDevTest.PolicyTest</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void DoorSkinTest()
        {
            Check("door skin: it works on floor 7 and no other floor", DoorSkinRules.AppliesTo(7) && Enumerable.Range(1, 12).Where(f => f != 7).All(f => !DoorSkinRules.AppliesTo(f)));

            // the prop table: the four pieces, the sizes the rules use
            var want = new Dictionary<string, float[]>
            {
                { DoorSkinRules.KeyWhite, new[] { DoorSkinRules.SingleW, DoorSkinRules.SingleH, DoorSkinRules.SingleD } },
                { DoorSkinRules.KeyWood, new[] { DoorSkinRules.SingleW, DoorSkinRules.SingleH, DoorSkinRules.SingleD } },
                { DoorSkinRules.KeyGlassL, new[] { DoorSkinRules.LargeW, DoorSkinRules.LargeH, DoorSkinRules.LargeD } },
                { DoorSkinRules.KeyGlassR, new[] { DoorSkinRules.LargeW, DoorSkinRules.LargeH, DoorSkinRules.LargeD } },
            };
            foreach (var kv in want)
            {
                PropDef d;
                bool has = PropTable.TryGet(kv.Key, out d) && d != null;
                Check("door skin: the prop table has " + kv.Key + " as a prop that is not solid", has && !d.solid, kv.Key);
                Check("door skin: " + kv.Key + " has the size the rules use", has && Math.Abs(d.W - kv.Value[0]) < 1e-4f && Math.Abs(d.H - kv.Value[1]) < 1e-4f && Math.Abs(d.D - kv.Value[2]) < 1e-4f, kv.Key);
            }
            PropDef w1, w2, g1, g2;
            PropTable.TryGet(DoorSkinRules.KeyWhite, out w1); PropTable.TryGet(DoorSkinRules.KeyWood, out w2);
            PropTable.TryGet(DoorSkinRules.KeyGlassL, out g1); PropTable.TryGet(DoorSkinRules.KeyGlassR, out g2);
            Check("door skin: the white and the wooden door are two prefabs, the glass pair two more", w1 != null && w2 != null && g1 != null && g2 != null && new[] { w1.path, w2.path, g1.path, g2.path }.Distinct().Count() == 4);
            Check("door skin: the pieces are the pack's doors (SM_Bld_Door_02, _05 and Door_Large_01 _L / _R)",
                  w1 != null && w1.path.EndsWith("SM_Bld_Door_02") && w2.path.EndsWith("SM_Bld_Door_05") && g1.path.EndsWith("SM_Bld_Door_Large_01_L") && g2.path.EndsWith("SM_Bld_Door_Large_01_R"));

            // the looks
            var wood = new[] { "office", "conference", "meeting", "training" };
            var white = new[] { "storage", "copy", "server", "break", "canteen", "lounge", "nook", "open", "hall", "hub", "lobby", "restroom", "cafe", "service", "outpost", null };
            Check("door skin: the wide hall doors are glass whatever the room", new[] { "office", "storage", null }.All(t => DoorSkinRules.LookFor(t, false, true) == DoorLook.Glass));
            Check("door skin: the doors of the offices with a glass front are glass", new[] { "office", "conference", "storage", null }.All(t => DoorSkinRules.LookFor(t, true, false) == DoorLook.Glass));
            Check("door skin: the office-like rooms have the wooden door", wood.All(t => DoorSkinRules.LookFor(t, false, false) == DoorLook.Wood));
            Check("door skin: every other room has the white door (and a doorway of no room too)", white.All(t => DoorSkinRules.LookFor(t, false, false) == DoorLook.White));

            // panels in a leaf
            const float seam = 0.01f;
            Check("door skin: a 2 m doorway has one panel in a leaf, whatever the look", DoorSkinRules.Panels(1f - seam, DoorSkinRules.PanelWidth(DoorLook.White)) == 1 && DoorSkinRules.Panels(1f - seam, DoorSkinRules.PanelWidth(DoorLook.Wood)) == 1 && DoorSkinRules.Panels(1f - seam, DoorSkinRules.PanelWidth(DoorLook.Glass)) == 1);
            Check("door skin: the 4.8 m hall doorway has two panels in a leaf", DoorSkinRules.Panels(2.4f - seam, DoorSkinRules.PanelWidth(DoorLook.Glass)) == 2);
            Check("door skin: a leaf always has at least one panel", DoorSkinRules.Panels(0.2f, 1f) == 1 && DoorSkinRules.Panels(0f, 1f) == 1);
            Check("door skin: the glass double door alternates the pack's left and right leaves (one panel: left, right; two: left, right, left, right)",
                  !DoorSkinRules.UseRight(-1, 0, 1) && DoorSkinRules.UseRight(1, 0, 1) &&
                  !DoorSkinRules.UseRight(-1, 0, 2) && DoorSkinRules.UseRight(-1, 1, 2) && !DoorSkinRules.UseRight(1, 0, 2) && DoorSkinRules.UseRight(1, 1, 2));
            Check("door skin: only the solid doors are turned round, and only in the low leaf", DoorSkinRules.Mirrored(DoorLook.White, -1) && DoorSkinRules.Mirrored(DoorLook.Wood, -1) && !DoorSkinRules.Mirrored(DoorLook.White, 1) && !DoorSkinRules.Mirrored(DoorLook.Glass, -1) && !DoorSkinRules.Mirrored(DoorLook.Glass, 1));

            // the fit: every model fills its slot of the unit cube exactly
            // (the pivots are measured: the solid door's hinge edge is its pivot and it stands to -x of it, the glass leaves' pivot is the middle of the pair)
            var models = new[]
            {
                (name: "white", w: DoorSkinRules.SingleW, h: DoorSkinRules.SingleH, d: DoorSkinRules.SingleD, cx: -0.488f, cy: 0.983f, cz: -0.039f, look: DoorLook.White),
                (name: "glass L", w: DoorSkinRules.LargeW, h: DoorSkinRules.LargeH, d: DoorSkinRules.LargeD, cx: -0.463f, cy: 1.226f, cz: -0.033f, look: DoorLook.Glass),
                (name: "glass R", w: DoorSkinRules.LargeW, h: DoorSkinRules.LargeH, d: DoorSkinRules.LargeD, cx: 0.463f, cy: 1.226f, cz: -0.033f, look: DoorLook.Glass),
            };
            bool fills = true, tiles = true, thick = true;
            string firstBad = "";
            foreach (var m in models)
                foreach (int panels in new[] { 1, 2, 3 })
                    foreach (bool mirror in new[] { false, true })
                    {
                        float prevHi = -0.5f;
                        for (int i = 0; i < panels; i++)
                        {
                            var f = DoorSkinRules.PanelFit(i, panels, mirror, m.w, m.h, m.d, m.cx, m.cy, m.cz);
                            // where the model's bounds end up (x, y, z of both edges) in the leaf's unit cube
                            float x0 = f.px + f.sx * (m.cx - m.w * 0.5f), x1 = f.px + f.sx * (m.cx + m.w * 0.5f);
                            if (x0 > x1) { float t = x0; x0 = x1; x1 = t; }
                            float y0 = f.py + f.sy * (m.cy - m.h * 0.5f), y1 = f.py + f.sy * (m.cy + m.h * 0.5f);
                            float z0 = f.pz + f.sz * (m.cz - m.d * 0.5f), z1 = f.pz + f.sz * (m.cz + m.d * 0.5f);
                            float slot0 = -0.5f + (float)i / panels, slot1 = -0.5f + (float)(i + 1) / panels;
                            if (Math.Abs(x0 - slot0) > 1e-4f || Math.Abs(x1 - slot1) > 1e-4f || Math.Abs(y0 + 0.5f) > 1e-4f || Math.Abs(y1 - 0.5f) > 1e-4f) { fills = false; if (firstBad == "") firstBad = m.name + " " + panels + " " + i; }
                            if (Math.Abs(z0 + 0.5f) > 1e-4f || Math.Abs(z1 - 0.5f) > 1e-4f) { thick = false; if (firstBad == "") firstBad = m.name + " thickness"; }
                            if (Math.Abs(x0 - prevHi) > 1e-4f) { tiles = false; if (firstBad == "") firstBad = m.name + " tiling " + panels; }
                            prevHi = x1;
                            if (mirror != (f.sx < 0f) || f.sy <= 0f || f.sz <= 0f) { fills = false; if (firstBad == "") firstBad = m.name + " signs"; }
                        }
                        if (Math.Abs(prevHi - 0.5f) > 1e-4f) { tiles = false; if (firstBad == "") firstBad = m.name + " tiling end " + panels; }
                    }
            Check("door skin: a panel of every model fills its slot of the leaf's unit cube exactly (width, height)", fills, firstBad);
            Check("door skin: its thickness is the leaf's (so the handles fold into the box the old leaf filled)", thick, firstBad);
            Check("door skin: the panels of a leaf tile it without a gap or an overlap", tiles, firstBad);

            // handles in the middle, hinges at the jambs: the solid door's pivot is its hinge edge
            var wm = models[0];
            var high = DoorSkinRules.PanelFit(0, 1, DoorSkinRules.Mirrored(DoorLook.White, 1), wm.w, wm.h, wm.d, wm.cx, wm.cy, wm.cz);
            var low = DoorSkinRules.PanelFit(0, 1, DoorSkinRules.Mirrored(DoorLook.White, -1), wm.w, wm.h, wm.d, wm.cx, wm.cy, wm.cz);
            Check("door skin: the hinge edge of a solid door is at the jamb: the high leaf's at its +x end and the low leaf's at its -x end", high.px > 0.45f && high.px < 0.55f && low.px < -0.45f && low.px > -0.55f, "high " + high.px.ToString("0.000", CultureInfo.InvariantCulture) + ", low " + low.px.ToString("0.000", CultureInfo.InvariantCulture));
            Check("door skin: the two leaves of a solid door are mirror images of each other", Math.Abs(high.px + low.px) < 1e-4f && Math.Abs(high.sx + low.sx) < 1e-6f && Math.Abs(high.sy - low.sy) < 1e-6f && Math.Abs(high.sz - low.sz) < 1e-6f);
            Check("door skin: the stretch of a solid door to the 2.45 m doorway is under 1.3 times (the door is 1.967 m; the handle ends up about 1.2 m up)", 2.45f / DoorSkinRules.SingleH < 1.3f && 2.45f / DoorSkinRules.SingleH > 1.2f);
            Check("door skin: the glass door is the doorway's height (2.45 m) within 5 mm", Math.Abs(DoorSkinRules.LargeH - 2.45f) < 0.005f);

            // whole floors: every doorway the planner makes is a doorway the rules can dress (a 2 m gap or the 4.8 m one, 2.45 m high), and the looks come out as expected
            int floors = 0, doorways = 0, narrow = 0, wide = 0, other = 0;
            foreach (var seed in new[] { 4101, 5, 31, 777 })
            {
                var L = WsMake(seed, seed == 777 ? 6 : 8, seed == 777 ? 4 : 5);
                floors++;
                foreach (var b in L.walls.Where(x => x.kind == "header"))
                {
                    doorways++;
                    float gap = Math.Max(b.rc.W, b.rc.D);
                    float half = gap * 0.5f;
                    int n = DoorSkinRules.Panels(half - seam, DoorSkinRules.PanelWidth(gap > 3f ? DoorLook.Glass : DoorLook.White));
                    if (Math.Abs(gap - 2f) < 0.01f && n == 1) narrow++;
                    else if (Math.Abs(gap - 4.8f) < 0.01f && n == 2) wide++;
                    else other++;
                    if (Math.Abs(b.y0 - 2.45f) > 0.005f) other++;
                }
            }
            Check("door skin: " + floors + " planned floors have " + doorways + " doorways, every one a 2 m gap with one panel per leaf or the 4.8 m gap with two, 2.45 m high", other == 0 && narrow > 0 && wide > 0 && narrow + wide == doorways, narrow + " narrow, " + wide + " wide, " + other + " other");
        }
    }
}
