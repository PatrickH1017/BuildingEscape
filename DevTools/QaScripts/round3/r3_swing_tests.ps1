# R12b tests: every door that cannot slide swings into its own room (pure test), plus the scenario's swing pictures and geometry checks (the master text NetDevR3.cs.txt).
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

# ---- the pure test (in the tree)
Open-Src "$Tree\Assets\Editor\NetDev\NetDevR3Test.cs"
if (-not $s.Contains('badSwing')) {
Rep 'int doors = 0, slides = 0, shrinks = 0, overlaps = 0, tooFar = 0, floors = 0;' 'int doors = 0, slides = 0, swings = 0, badSwing = 0, overlaps = 0, tooFar = 0, floors = 0;'
Rep '                        if (plans[i].slide) slides++; else shrinks++;' @'
                        if (plans[i].slide) slides++;
                        else
                        {
                            swings++;
                            // a door that cannot slide swings open into the room it belongs to (the outpost's door: out of the room)
                            var rr = beams[i].rc; bool along = rr.W >= rr.D;
                            float px = along ? rr.Cx : rr.Cx + plans[i].swing * 1.3f, pz = along ? rr.Cz + plans[i].swing * 1.3f : rr.Cz;
                            RoomInfo owner = null;
                            foreach (var room in layout.rooms) { foreach (var dd in room.doors) if (dd.Overlaps(rr.Grow(0.05f))) { owner = room; break; } if (owner != null) break; }
                            bool inside = owner != null && owner.bounds.Contains(px, pz);
                            if (plans[i].swing == 0 || owner == null || inside != (owner.theme != "outpost")) badSwing++;
                        }
'@
Rep '            Check($"...and most doors slide into the wall ({slides} of {doors}, {shrinks} narrow into their jambs)", doors > 500 && slides >= doors * 70 / 100);' @'
            Check($"...and most doors slide into the wall ({slides} of {doors}); the {swings} that cannot swing open", doors > 500 && slides >= doors * 70 / 100 && slides + swings == doors);
            Check($"...every door that swings opens into its own room, the outpost's out of it ({badSwing} wrong)", swings > 100 && badSwing == 0);
'@
Save-Src
}

# ---- the scenario (the master text in the scratchpad; r3_harness.ps1 copies it into the tree)
$p = "$PSScriptRoot\NetDevR3.cs.txt"
$t = [IO.File]::ReadAllText($p)
if (-not $t.Contains('swingChecked')) {
function Swap([string]$old, [string]$new) { if (-not $script:t.Contains($old)) { throw "anchor missing: $old" }; $script:t = $script:t.Replace($old, $new) }
Swap '        static Bounds LeafBox(SlidingDoor d, Transform leaf)
        {
            var s = leaf.localScale;' '        static Bounds LeafBox(SlidingDoor d, Transform leaf)
        {
            if (d.Swings && leaf.TryGetComponent(out Renderer swingRenderer)) return swingRenderer.bounds;       // (a leaf that swings is turned: its renderer knows its box)
            var s = leaf.localScale;'
Swap '$"({gen.DoorSlides} slide, {gen.DoorShrinks} narrow into their jambs, {dm.Count} doors)"' '$"({gen.DoorSlides} slide, {gen.DoorSwings} swing open, {dm.Count} doors)"'
Swap '            var narrowing = doors.FirstOrDefault(d => !d.Slides && !d.Wide && Panes(d) == 0);
            Log($"[R3] doors: plain={(plain != null)} wide={(wide != null)} glass={(glass != null)} narrowing={(narrowing != null)}");' '            var swinging = doors.FirstOrDefault(d => d.Swings && !d.Wide && Panes(d) == 0);
            Log($"[R3] doors: plain={(plain != null)} wide={(wide != null)} glass={(glass != null)} swinging={(swinging != null)}");'
Swap '            IEnumerator Show(SlidingDoor d, string name, float back = 3.4f, float side = 0f)' '            IEnumerator Show(SlidingDoor d, string name, float back = 3.4f, float side = 0f, bool flip = false)'
Swap '                if (ClearAhead(-fwd) > ClearAhead(fwd)) fwd = -fwd;' '                if (ClearAhead(-fwd) > ClearAhead(fwd)) fwd = -fwd;
                if (flip) fwd = -fwd;'
Swap '            yield return Show(narrowing, "door_narrowing", 3.4f, 1.2f);' '            yield return Show(swinging, "door_swing", 3.4f, 1.2f);
            yield return Show(swinging, "door_swing_other", 3.4f, -1.2f, true);
            R3Check("the floor has doors that swing open (the ones with too little wall beside them to slide into)", gen.DoorSwings > 5 && swinging != null, $"({gen.DoorSwings})");'
Swap '                if (!a.Slides) continue;' '                if (!a.Slides && !a.Swings) continue;'
Swap '                    if (!b.Slides || (a.Centre - b.Centre).sqrMagnitude > 64f) continue;' '                    if ((!b.Slides && !b.Swings) || (a.Centre - b.Centre).sqrMagnitude > 64f) continue;'
Swap '            // a picture of two neighbouring doors open' '            // an open swing door: both leaves stand square to the wall (their box is thin along the wall and about as deep as the leaf is wide)
            int swingChecked = 0, swingBad = 0;
            foreach (var d in doors.Where(x => x.Swings).Take(20))
                foreach (var leaf in new[] { d.LowLeaf, d.HighLeaf })
                {
                    var b = leaf.GetComponent<Renderer>().bounds;
                    float thin = d.AlongX ? b.size.x : b.size.z, deep = d.AlongX ? b.size.z : b.size.x;
                    swingChecked++;
                    if (Mathf.Abs(thin - SlidingDoor.LeafThickness) > 0.05f || deep < d.HalfGap * 0.8f) swingBad++;
                }
            R3Check($"an open swing door has its leaves square to the wall (about 90 degrees: {swingChecked} leaves looked at)", swingChecked > 10 && swingBad == 0, $"(bad {swingBad})");

            // a picture of two neighbouring doors open'
[IO.File]::WriteAllText($p, $t)
"scenario text patched"
}
