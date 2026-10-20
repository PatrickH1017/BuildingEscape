# R12b: the owner: "For any door that can't slide properly into the wall please make it a swing door and see how it works".
# A door that DoorSlide says cannot slide (its walls have too little room for its leaves) gets two hinged leaves that swing open at the jambs, into the room it belongs to
# (the outpost's door out into the corridor), instead of narrowing into its jambs.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

# ---- the plan knows which way a swinging door opens
Open-Src "$Tree\Assets\Scripts\Doors\DoorSlide.cs"
if (-not $s.Contains('public int swing;')) {
Rep @'
            /// <summary>0: inside the wall (hidden); +1 / -1: in front of the wall on its +z / -z side (a glass front).</summary>
            public int lowLane, highLane;
'@ @'
            /// <summary>0: inside the wall (hidden); +1 / -1: in front of the wall on its +z / -z side (a glass front).</summary>
            public int lowLane, highLane;
            /// <summary>For a door that cannot slide (<see cref="slide"/> false): the side its leaves swing open to, +1 / -1 across the wall (into the room the door belongs to; the guard outpost's door opens out into the corridor).</summary>
            public int swing;
'@
Rep @'
            foreach (var w in layout.walls) if (w.kind == "header") { beams.Add(w); rcs.Add(w.rc); }
'@ @'
            foreach (var w in layout.walls) if (w.kind == "header" || w.kind == "lockheader") { beams.Add(w); rcs.Add(w.rc); }
'@
Rep @'
            return PlanAll(rcs, walls, overlay);
        }
'@ @'
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
'@
Save-Src
}

# ---- the door
Open-Src "$Tree\Assets\Scripts\Doors\SlidingDoor.cs"
if (-not $s.Contains('SetSwing')) {
Rep @'
        /// <summary>Does this door slide into the wall (true) or narrow into its jambs (false)?</summary>
        public bool Slides => slide;
'@ @'
        /// <summary>Does this door slide into the wall?</summary>
        public bool Slides => slide;

        // ---- a door whose walls have no room for its leaves swings open instead (hinged at the jambs)
        bool swing;
        int swingSign = 1;
        const float SwingDegrees = 92f;

        /// <summary>
        /// The leaves are hinged at the jambs and swing open towards the side <paramref name="sign"/> (+1 / -1 across the wall, in the door's own frame): for a door that cannot slide into the
        /// wall beside it (<see cref="DoorSlide"/>). The doorway's approach is kept clear of furniture on both sides, so the leaves have room.
        /// </summary>
        public void SetSwing(int sign)
        {
            swing = true;
            swingSign = sign >= 0 ? 1 : -1;
            Apply();
        }

        /// <summary>Does this door swing open (hinged leaves)?</summary>
        public bool Swings => swing;
'@
Rep @'
            if (slide) ApplySlide();
            else
            {
'@ @'
            if (slide) ApplySlide();
            else if (swing) ApplySwing();
            else
            {
'@
Rep @'
        void SlideLeaf(Transform t, Renderer r, List<Renderer> decor, int side, float width, float moved, float depth, bool insideWall)
'@ @'
        /// <summary>Both leaves turn about their hinges at the jambs (eased, up to 92 degrees). The hinge moves half a leaf's thickness inwards as the door opens, so an open leaf lies flush against the side of the doorway.</summary>
        void ApplySwing()
        {
            float o = Mathf.Clamp01(Logic.Open), e = o * o * (3f - 2f * o);
            float w = HalfGap - DoorGeometry.Seam;
            float hingeX = HalfGap - LeafThickness * 0.5f * e;
            float angle = SwingDegrees * e * swingSign;
            SwingLeaf(leafLow, rendLow, decorLow, -1, w, hingeX, angle);
            SwingLeaf(leafHigh, rendHigh, decorHigh, +1, w, hingeX, angle);
        }

        void SwingLeaf(Transform t, Renderer r, List<Renderer> decor, int side, float width, float hingeX, float angle)
        {
            if (!r.enabled)
            {
                r.enabled = true;
                for (int i = 0; i < decor.Count; i++) if (decor[i]) decor[i].enabled = true;
            }
            // the low leaf is hinged at -x and reaches towards +x: turned by -angle about y it swings towards +z; the high leaf is hinged at +x and reaches towards -x: +angle swings it towards +z
            var q = Quaternion.Euler(0f, side < 0 ? -angle : angle, 0f);
            var hinge = new Vector3(side * hingeX, Height * 0.5f, 0f);
            t.localRotation = q;
            t.localPosition = hinge + q * new Vector3(-side * width * 0.5f, 0f, 0f);
            t.localScale = new Vector3(width, Height, LeafThickness);
        }

        void SlideLeaf(Transform t, Renderer r, List<Renderer> decor, int side, float width, float moved, float depth, bool insideWall)
'@
Save-Src
}

# ---- the generator: the doors that cannot slide swing; the outpost's door is planned too
Open-Src "$Tree\Assets\Scripts\FloorGenerator.Doors.cs"
if (-not $s.Contains('DoorSwings')) {
Rep @'
                    outpostDoor = MakeOutpostDoor(holder.transform, w);
'@ @'
                    outpostDoor = MakeOutpostDoor(holder.transform, w);
                    byBeam[w] = outpostDoor;
'@
Rep @'
        /// <summary>How many doors of the floor slide into the wall beside them, and how many narrow into their jambs because their walls have no room (see <see cref="DoorSlide"/>).</summary>
        public int DoorSlides { get; private set; }
        public int DoorShrinks { get; private set; }
'@ @'
        /// <summary>How many doors of the floor slide into the wall beside them, and how many swing open instead because their walls have no room for their leaves (see <see cref="DoorSlide"/>).</summary>
        public int DoorSlides { get; private set; }
        public int DoorSwings { get; private set; }
'@
Rep '            DoorSlides = 0; DoorShrinks = 0;' '            DoorSlides = 0; DoorSwings = 0;'
Rep @'
                if (plans[i].slide) { door.SetSlide(plans[i]); DoorSlides++; }
                else DoorShrinks++;
'@ @'
                if (plans[i].slide) { door.SetSlide(plans[i]); DoorSlides++; }
                else { door.SetSwing(plans[i].swing); DoorSwings++; }          // (a door that has no room to slide swings open: the owner, 2026-10-17)
'@
Rep 'catch (System.Exception e) { Debug.LogWarning("[Doors] The doors narrow into their jambs as before (their slide plan failed): " + e.Message); }' 'catch (System.Exception e) { Debug.LogWarning("[Doors] The doors narrow into their jambs as before (their slide plan failed): " + e.Message); }'
Save-Src
}
