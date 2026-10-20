# R12: the doors slide INTO the wall beside them (DoorSlide) instead of narrowing into the jambs, and two doors that share a piece of wall never overlap.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

# ---- the planner (pure)
$dst = "$Tree\Assets\Scripts\Doors\DoorSlide.cs"
$src = [IO.File]::ReadAllText("$PSScriptRoot\DoorSlide.cs.txt").Replace("`r`n", "`n").Replace("`n", "`r`n")
[IO.File]::WriteAllText($dst, $src, (New-Object Text.UTF8Encoding($false)))
"wrote DoorSlide.cs"

# ---- the door
Open-Src "$Tree\Assets\Scripts\Doors\SlidingDoor.cs"
if (-not $s.Contains('SetSlide')) {
Rep @'
        public bool IsShut => Logic.IsShut;
'@ @'
        // ---- how the leaves leave the doorway: sliding into the wall beside it (see DoorSlide) instead of narrowing into the jambs
        bool slide;
        float lowTravel, highTravel, lowDepth, highDepth;

        /// <summary>
        /// The leaves keep their size and slide sideways out of the doorway, <paramref name="plan"/> says how far and where (inside the wall, or in front of a glass front). A plan that says
        /// "no" leaves the door as it is: its leaves narrow into the jambs.
        /// </summary>
        public void SetSlide(DoorSlide.Plan plan)
        {
            if (!plan.slide) return;
            slide = true;
            lowTravel = plan.lowTravel; highTravel = plan.highTravel;
            float lane = Depth * 0.5f + LeafThickness * 0.5f + 0.012f;           // (in front of the wall's face, not touching it)
            lowDepth = plan.lowLane * lane; highDepth = plan.highLane * lane;
            Apply();
        }

        /// <summary>Does this door slide into the wall (true) or narrow into its jambs (false)?</summary>
        public bool Slides => slide;

        public bool IsShut => Logic.IsShut;
'@
Rep @'
        void Apply()
        {
            float w = DoorGeometry.LeafWidth(HalfGap, Logic.Open);
            SetLeaf(leafLow, rendLow, decorLow, -1, w);
            SetLeaf(leafHigh, rendHigh, decorHigh, +1, w);
            bool shut = Logic.IsShut;
            if (blocker.enabled != shut) blocker.enabled = shut;
        }
'@ @'
        void Apply()
        {
            if (slide) ApplySlide();
            else
            {
                float w = DoorGeometry.LeafWidth(HalfGap, Logic.Open);
                SetLeaf(leafLow, rendLow, decorLow, -1, w);
                SetLeaf(leafHigh, rendHigh, decorHigh, +1, w);
            }
            bool shut = Logic.IsShut;
            if (blocker.enabled != shut) blocker.enabled = shut;
        }

        /// <summary>Each leaf slides out of the doorway as far as its plan says (eased); one that goes in front of a glass front steps out of the doorway's line first. A leaf that is all the way inside the wall is not drawn.</summary>
        void ApplySlide()
        {
            float o = Mathf.Clamp01(Logic.Open), e = o * o * (3f - 2f * o), stepOut = Mathf.Clamp01(o / 0.25f);
            float w = HalfGap - DoorGeometry.Seam;
            SlideLeaf(leafLow, rendLow, decorLow, -1, w, lowTravel * e, lowDepth * stepOut, o >= 1f && lowDepth == 0f && lowTravel >= w - 0.01f);
            SlideLeaf(leafHigh, rendHigh, decorHigh, +1, w, highTravel * e, highDepth * stepOut, o >= 1f && highDepth == 0f && highTravel >= w - 0.01f);
        }

        void SlideLeaf(Transform t, Renderer r, List<Renderer> decor, int side, float width, float moved, float depth, bool insideWall)
        {
            bool visible = !insideWall;
            if (r.enabled != visible)
            {
                r.enabled = visible;
                for (int i = 0; i < decor.Count; i++) if (decor[i]) decor[i].enabled = visible;
            }
            if (!visible) return;
            t.localPosition = new Vector3(side * ((HalfGap + DoorGeometry.Seam) * 0.5f + moved), Height * 0.5f, depth);
            t.localScale = new Vector3(width, Height, LeafThickness);
        }
'@
Save-Src
}

# ---- the generator: plan the slides once the doors exist
Open-Src "$Tree\Assets\Scripts\FloorGenerator.Doors.cs"
if (-not $s.Contains('ApplyDoorSlides')) {
Rep 'using UnityEngine;
using UnityEngine.Rendering;' 'using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;'
Rep @'
            var holder = new GameObject("Doors");
            holder.transform.SetParent(current, false);
            var manager = holder.AddComponent<DoorManager>();
            var mat = DoorMat();
            foreach (var w in layout.walls)
            {
                if (w.kind == "header")
                {
                    var door = MakeDoor(holder.transform, w, mat);
'@ @'
            var holder = new GameObject("Doors");
            holder.transform.SetParent(current, false);
            var manager = holder.AddComponent<DoorManager>();
            var mat = DoorMat();
            var byBeam = new Dictionary<WallBox, SlidingDoor>();
            foreach (var w in layout.walls)
            {
                if (w.kind == "header")
                {
                    var door = MakeDoor(holder.transform, w, mat);
                    byBeam[w] = door;
'@
Rep @'
            if (manager.Count == 0) { Kill(holder); return; }
            manager.OpenDoorsWithPeople();
        }
'@ @'
            if (manager.Count == 0) { Kill(holder); return; }
            try { ApplyDoorSlides(byBeam); }
            catch (System.Exception e) { Debug.LogWarning("[Doors] The doors narrow into their jambs as before (their slide plan failed): " + e.Message); }      // (decoration: never stops a floor)
            manager.OpenDoorsWithPeople();
        }

        /// <summary>How many doors of the floor slide into the wall beside them, and how many narrow into their jambs because their walls have no room (see <see cref="DoorSlide"/>).</summary>
        public int DoorSlides { get; private set; }
        public int DoorShrinks { get; private set; }

        /// <summary>The doors slide into the wall (the owner, 2026-10-17): each one gets the plan <see cref="DoorSlide"/> works out from the walls beside it.</summary>
        void ApplyDoorSlides(Dictionary<WallBox, SlidingDoor> doorsByBeam)
        {
            var beams = new List<WallBox>();
            var plans = DoorSlide.ForLayout(layout, wallHeight, OverlaySignOf, beams);
            DoorSlides = 0; DoorShrinks = 0;
            for (int i = 0; i < beams.Count; i++)
            {
                if (!doorsByBeam.TryGetValue(beams[i], out var door)) continue;
                if (plans[i].slide) { door.SetSlide(plans[i]); DoorSlides++; }
                else DoorShrinks++;
            }
        }

        /// <summary>For a glass front: which side of the wall (+1 / -1 across it) a door leaf may slide in front of it on (the corridor's); 0 for a solid wall.</summary>
        int OverlaySignOf(WallBox w)
        {
            if (!windowedWalls.Contains(w) || !windowedOutward.TryGetValue(w, out var outward)) return 0;
            float across = w.rc.W >= w.rc.D ? outward.z : outward.x;
            return across >= 0f ? 1 : -1;
        }
'@
Save-Src
}

# ---- the glass fronts remember which way the corridor is
Open-Src "$Tree\Assets\Scripts\FloorGenerator.Glass.cs"
if (-not $s.Contains('windowedOutward')) {
Rep '        readonly HashSet<WallBox> windowedWalls = new HashSet<WallBox>();' '        readonly HashSet<WallBox> windowedWalls = new HashSet<WallBox>();
        /// <summary>For each glass front: the step from the wall towards the corridor (a door leaf slides in front of the glass on that side, see DoorSlide).</summary>
        readonly Dictionary<WallBox, Vector3> windowedOutward = new Dictionary<WallBox, Vector3>();'
Rep '            windowedWalls.Clear();
            var solid = new MeshBuilder(); var frame = new MeshBuilder(); var glass = new MeshBuilder();' '            windowedWalls.Clear();
            windowedOutward.Clear();
            var solid = new MeshBuilder(); var frame = new MeshBuilder(); var glass = new MeshBuilder();'
Rep '                    if (AddGlassFront(solid, frame, glass, w, outward, outside >= 1)) { windowedWalls.Add(w); fronts++; }' '                    if (AddGlassFront(solid, frame, glass, w, outward, outside >= 1)) { windowedWalls.Add(w); windowedOutward[w] = outward; fronts++; }'
Save-Src
}

# ---- the pure tests
Open-Src "$Tree\Assets\Editor\NetDev\NetDevR3Test.cs"
if (-not $s.Contains('DoorSlideTest')) {
Rep '            SightTest();
        }' '            SightTest();
            DoorSlideTest();
        }'
Rep @'
        /// <summary>R14: officers see much further down a straight line of sight.</summary>
'@ @'
        /// <summary>R12: the doors slide into the wall beside them, and two doors never overlap.</summary>
        static void DoorSlideTest()
        {
            const float H = 3.5f;
            Rc Wall(float a0, float a1) => new Rc(a0, 0f, a1, 0.2f);                   // a wall box along x (thickness 0.2)
            Rc Beam(float a0, float a1) => new Rc(a0, 0f, a1, 0.2f);

            // one 2 m doorway in a long wall: both leaves slide in all the way, hidden in the wall
            var one = DoorSlide.PlanAll(new[] { Beam(4f, 6f) }, new[] { Wall(0f, 4f), Wall(6f, 12f) }, null);
            Check("a door with wall on both sides slides into it", one[0].slide && one[0].lowLane == 0 && one[0].highLane == 0);
            Check("...with both leaves clear of the doorway", Mathf.Approximately(one[0].lowTravel, 1f) && Mathf.Approximately(one[0].highTravel, 1f));

            // a short piece of wall at one end: that leaf slides as far as it can (the rest stays in the doorway)
            var shortEnd = DoorSlide.PlanAll(new[] { Beam(4.9f, 6.9f) }, new[] { Wall(4f, 4.9f), Wall(6.9f, 12f) }, null);
            Check("a 0.9 m end piece takes 0.82 m of the leaf (never more than the wall holds)", shortEnd[0].slide && shortEnd[0].lowTravel > 0.8f && shortEnd[0].lowTravel < 0.9f && Mathf.Approximately(shortEnd[0].highTravel, 1f));

            // a wall with no room at all: the door narrows into its jambs as before
            var none = DoorSlide.PlanAll(new[] { Beam(0.2f, 2.2f) }, new[] { Wall(2.2f, 8f) }, null);
            Check("a door with wall on one side only (half the travel) narrows into its jambs", !none[0].slide);

            // two doors with one piece of wall between them that holds both leaves: both slide in, side by side
            var two = DoorSlide.PlanAll(new[] { Beam(2f, 4f), Beam(8f, 10f) }, new[] { Wall(0f, 2f), Wall(4f, 8f), Wall(10f, 14f) }, null);
            Check("two doors with room for both leaves between them: both slide in all the way", two[0].slide && two[1].slide && Mathf.Approximately(two[0].highTravel, 1f) && Mathf.Approximately(two[1].lowTravel, 1f));

            // a wide hall door (4.8 m, 2.4 m leaves) 3 m from a narrow one: the piece holds 3.0 m for 3.4 m of leaves - the two share it, they do not overlap
            var wide = DoorSlide.PlanAll(new[] { Beam(2.4f, 7.2f), Beam(10.2f, 12.2f) }, new[] { Wall(0f, 2.4f), Wall(7.2f, 10.2f), Wall(12.2f, 18f) }, null);
            float usedByWide = wide[0].highTravel, usedByNarrow = wide[1].lowTravel;
            Check("the piece between a wide and a narrow door is shared: their leaves together never go further than the piece", usedByWide + usedByNarrow <= 3.0f - 2f * DoorSlide.EndMargin - DoorSlide.LeafGap + 1e-3f);
            Check("...and both doors still slide (each gets its share)", wide[0].slide && wide[1].slide && usedByWide > 1.9f && usedByNarrow > 0.7f);

            // a glass front beside a door: the leaf cannot go inside it and slides in front of the glass, on the corridor's side
            var glass = DoorSlide.PlanAll(new[] { Beam(4f, 6f) }, new[] { Wall(0f, 4f), Wall(6f, 12f) }, new[] { 1, 0 });
            Check("a door beside a glass front slides in front of the glass on the corridor's side", glass[0].slide && glass[0].lowLane == 1 && glass[0].highLane == 0);

            // a door in a wall along z (turned a quarter): its low leaf is on the high side
            var tall = DoorSlide.PlanAll(new[] { new Rc(0f, 4f, 0.2f, 6f) }, new[] { new Rc(0f, 0f, 0.2f, 4f), new Rc(0f, 6f, 0.2f, 12f) }, new[] { 0, -1 });
            Check("in a wall along z the low leaf is on the far side (the door is turned a quarter): the glass lane follows", tall[0].slide && tall[0].highLane == 0 && tall[0].lowLane == -1);

            // every door of 60 real floors: nothing slides further than its leaf or its wall, and two doors never overlap
            int doors = 0, slides = 0, shrinks = 0, overlaps = 0, tooFar = 0, floors = 0;
            for (int seed = 1; seed <= 20; seed++)
                foreach (int fl in new[] { 1, 4, 9 })
                {
                    var shape = FloorShape.Maze(new System.Random(seed * 31 + fl), 8, 5, 0.12);
                    var layout = OfficePlanner.Build(shape.PlanFor(fl, seed * 31 + fl, 18f, 3.5f, 777));
                    var beams = new System.Collections.Generic.List<WallBox>();
                    var plans = DoorSlide.ForLayout(layout, H, null, beams);
                    floors++;
                    // the walls (pieces) the doors touch: the leaves that go into the same piece must be disjoint
                    var pieceUse = new System.Collections.Generic.Dictionary<WallBox, float>();
                    for (int i = 0; i < beams.Count; i++)
                    {
                        doors++;
                        float leaf = (beams[i].rc.W >= beams[i].rc.D ? beams[i].rc.W : beams[i].rc.D) * 0.5f;
                        if (plans[i].slide) slides++; else shrinks++;
                        if (plans[i].lowTravel > leaf + 1e-3f || plans[i].highTravel > leaf + 1e-3f || plans[i].lowTravel < 0f || plans[i].highTravel < 0f) tooFar++;
                    }
                    // no two doors' leaves overlap: for every piece, what its two neighbours take fits in it
                    foreach (var w in layout.walls)
                    {
                        if (w.kind != "wall" || w.y0 > 0.01f || w.y1 < H - 0.01f) continue;
                        bool ax = w.rc.W >= w.rc.D;
                        float lo = ax ? w.rc.x0 : w.rc.z0, hi = ax ? w.rc.x1 : w.rc.z1, perp = ax ? w.rc.Cz : w.rc.Cx;
                        float taken = 0f; int touching = 0;
                        for (int i = 0; i < beams.Count; i++)
                        {
                            var r = beams[i].rc; bool bx = r.W >= r.D;
                            if (bx != ax || Mathf.Abs((bx ? r.Cz : r.Cx) - perp) > 0.06f) continue;
                            float blo = bx ? r.x0 : r.z0, bhi = bx ? r.x1 : r.z1;
                            bool touchesLo = Mathf.Abs(bhi - lo) <= 0.06f, touchesHi = Mathf.Abs(blo - hi) <= 0.06f;     // the doorway ends where the piece starts / starts where the piece ends
                            if (!touchesLo && !touchesHi) continue;
                            touching++;
                            // the leaf on the side of the doorway that faces this piece (in the door's frame: low leaf = low side for a wall along x, high side for a wall along z)
                            // touchesHi: the piece lies on the doorway's low side, so the leaf on that side slides into it (in the door's own frame: the low leaf for a wall along x, the high leaf for a wall along z)
                            float t = touchesHi ? (bx ? plans[i].lowTravel : plans[i].highTravel) : (bx ? plans[i].highTravel : plans[i].lowTravel);
                            if (plans[i].slide) taken += t;
                        }
                        if (touching >= 1 && taken > (hi - lo) - (touching == 2 ? 2f * DoorSlide.EndMargin + DoorSlide.LeafGap : DoorSlide.EndMargin) + 1e-3f) overlaps++;
                    }
                }
            Check($"the plans of {floors} real floors ({doors} doors): no leaf slides further than its own width", tooFar == 0);
            Check($"...and no two doors take more of a piece of wall than it has ({overlaps} overlaps)", overlaps == 0);
            Check($"...and most doors slide into the wall ({slides} of {doors}, {shrinks} narrow into their jambs)", doors > 500 && slides >= doors * 70 / 100);
        }

        /// <summary>R14: officers see much further down a straight line of sight.</summary>
'@
Save-Src
}
