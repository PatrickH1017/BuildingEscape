# Round 2, batch 15: glass doors for the offices with glass fronts (the owner: "the offices with glass walls should also have glass doors"). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Glass.cs')
Rep '        public int GlassWallCount => windowedWalls.Count;' @'
        public int GlassWallCount => windowedWalls.Count;
        /// <summary>How many of the doors of those offices were made of glass too, and the rooms that have a glass front.</summary>
        public int GlassDoors { get; private set; }
        readonly List<Rc> glassRooms = new List<Rc>();
'@
Rep '            GlassPanes = 0; GlassOffices = 0; CornerOffices = 0;
            GlassFronts.Clear();' '            GlassPanes = 0; GlassOffices = 0; CornerOffices = 0; GlassDoors = 0;
            GlassFronts.Clear();
            glassRooms.Clear();'
Rep '                if (fronts > 0) { GlassOffices++; if (outside >= 2) CornerOffices++; }' '                if (fronts > 0) { GlassOffices++; glassRooms.Add(rb); if (outside >= 2) CornerOffices++; }'
Rep '        void RestoreWindowedWalls()' @'
        // ------------------------------------------------------------------ glass doors for the offices with glass fronts

        Mesh glassLeafMesh;

        /// <summary>Is this doorway (the beam over it) the door of an office that has a glass front?</summary>
        bool InGlassOffice(WallBox beam)
        {
            foreach (var b in glassRooms) if (b.Grow(0.3f).Contains(beam.rc.Cx, beam.rc.Cz)) return true;
            return false;
        }

        /// <summary>
        /// The frame of a glass door's leaf, in the leaf's own unit cube (x across, y -0.5 = the floor to +0.5 = the top, z the thickness): a kick plate, a top rail and a narrow stile on each side,
        /// with ONE big opening between them. (Public and static so that a test can look through it.)
        /// </summary>
        public static Mesh MakeGlassLeafMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.42f, 0.5f));          // the kick plate
            b.Box(new Vector3(-0.5f, 0.44f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));            // the top rail
            b.Box(new Vector3(-0.5f, -0.42f, -0.5f), new Vector3(-0.4f, 0.44f, 0.5f));         // the stiles
            b.Box(new Vector3(0.4f, -0.42f, -0.5f), new Vector3(0.5f, 0.44f, 0.5f));
            var m = b.ToMesh("GlassLeaf");
            m.hideFlags = HideFlags.HideAndDontSave;
            return m;
        }

        /// <summary>A door of an office with a glass front: both leaves are frames with one big opening and a faint pane in it, on both faces (decoration: a failure leaves the plain door).</summary>
        void DressGlassDoor(SlidingDoor door)
        {
            try
            {
                var frame = glassLeafMesh ? glassLeafMesh : (glassLeafMesh = MakeGlassLeafMesh());
                door.LowLeaf.GetComponent<MeshFilter>().sharedMesh = frame;
                door.HighLeaf.GetComponent<MeshFilter>().sharedMesh = frame;
                var glass = HallGlassMat();
                for (int side = -1; side <= 1; side += 2)
                {
                    var leaf = side < 0 ? door.LowLeaf : door.HighLeaf;
                    for (int face = -1; face <= 1; face += 2) door.AddDecor(side, DoorPicture(leaf, face, 0f, 0.01f, 0.80f, 0.86f, 0.03f, glass));
                }
                GlassDoors++;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Glass] Could not make an office's door of glass (it stays a plain one): " + e.Message);
            }
        }

        void RestoreWindowedWalls()
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Doors.cs')
Rep '                    if (door.Wide) DressHallDoor(door);                    // the main hallway doors get windows' @'
                    if (door.Wide) DressHallDoor(door);                    // the main hallway doors get windows
                    else if (InGlassOffice(w)) DressGlassDoor(door);       // the doors of the offices with a glass front are glass
'@
Rep '            if (hallLeafMesh) Kill(hallLeafMesh);' @'
            if (hallLeafMesh) Kill(hallLeafMesh);
            if (glassLeafMesh) Kill(glassLeafMesh);
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Look.cs')
Rep '                LookCheck($"{f} a wall with a glass front keeps its collider' @'
                LookCheck($"{f} the doors of offices with a glass front are glass", gen.GlassDoors >= 2, $"({gen.GlassDoors} glass doors for {gen.GlassOffices} offices)");
                LookCheck($"{f} a wall with a glass front keeps its collider
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Test.cs')
Rep @'
            // ---- tile and carpet never share a spot of floor
'@ @'
            // ---- "the offices with glass walls should also have glass doors": a ray through the glass leaf's opening passes, one through a stile or the kick plate hits
            var glassLeaf = FloorGenerator.MakeGlassLeafMesh();
            var straight = Vector3.forward;
            Check("doors: a glass door's leaf has one big opening (a ray through its middle passes; through a stile, the kick plate or the top rail it hits), inside the unit leaf",
                !RayHitsMesh(glassLeaf, new Vector3(0f, 0.1f, -2f), straight) && !RayHitsMesh(glassLeaf, new Vector3(-0.3f, -0.3f, -2f), straight) && !RayHitsMesh(glassLeaf, new Vector3(0.3f, 0.35f, -2f), straight)
                && RayHitsMesh(glassLeaf, new Vector3(-0.45f, 0f, -2f), straight) && RayHitsMesh(glassLeaf, new Vector3(0.45f, 0f, -2f), straight) && RayHitsMesh(glassLeaf, new Vector3(0f, -0.46f, -2f), straight)
                && RayHitsMesh(glassLeaf, new Vector3(0f, 0.47f, -2f), straight) && glassLeaf.bounds.min.x >= -0.5001f && glassLeaf.bounds.max.x <= 0.5001f && glassLeaf.bounds.min.y >= -0.5001f && glassLeaf.bounds.max.y <= 0.5001f);
            Object.DestroyImmediate(glassLeaf);

            // ---- tile and carpet never share a spot of floor
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevEtr.cs')
Rep 'else { narrow++; if (panes != 0 && panes != 16) narrowPanes++; }' 'else { narrow++; if (panes != 0 && panes != 16 && panes != 4) narrowPanes++; }'
Rep 'the room doors are plain (the outpost''s own door keeps its wired glass)' 'the room doors are plain (the outpost''s own door keeps its wired glass, the doors of offices with a glass front are glass: 4 big panes)'
Save-Src
Write-Output 'batch 15 patched'
