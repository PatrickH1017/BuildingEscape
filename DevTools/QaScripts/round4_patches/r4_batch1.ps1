# Round 4 (a new session of agent 2 picks up agent 4's hand-off, DEVELOPER_NOTES 2.23), batch 1: executive suites and the server room are decided by the planner (R1, R2),
# suites always have a glass front and glass doors, their numbers and the server room's are painted in gold on the wall. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeLayout.cs')
Rep '        public string theme;
        public int kind;                 // 0 room, 1 open zone, 2 corridor / lobby' @'
        public string theme;
        /// <summary>null, "suite" (an executive suite) or "server" (the server room): the rooms the security computer locks (OfficePlanner.Roles.cs).</summary>
        public string role;
        public int kind;                 // 0 room, 1 open zone, 2 corridor / lobby
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficePlanner.cs')
Rep '            ValidateOutpostSlots();
            PlaceSafe();' '            ValidateOutpostSlots();
            PickRoles();                      // the executive suites and the server room (the safe goes into a suite first)
            PlaceSafe();'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficePlanner.Outpost.cs')
Rep '            var friendly = new List<RoomCtx>();
            var others = new List<RoomCtx>();
            foreach (var rc in allRooms)
            {
                if (!RoomNumbers.IsNumbered(rc.info)) continue;
                (SafeFriendly(rc.theme) ? friendly : others).Add(rc);
            }
            foreach (var tier in new[] { friendly, others })' '            var suites = new List<RoomCtx>();
            var friendly = new List<RoomCtx>();
            var others = new List<RoomCtx>();
            foreach (var rc in allRooms)
            {
                if (!RoomNumbers.IsNumbered(rc.info)) continue;
                if (rc.info.role == RoleSuite) suites.Add(rc);
                else if (rc.info.role == RoleServer) { }                         // (the server room keeps its racks)
                else (SafeFriendly(rc.theme) ? friendly : others).Add(rc);
            }
            foreach (var tier in new[] { suites, friendly, others })'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\RoomNumbers.cs')
Rep '            public float x, z, nx, nz, y0, y1;
        }' '            public float x, z, nx, nz, y0, y1;
            /// <summary>The number of an executive suite or of the server room: no plate, gold lettering straight on the wall over the door, "almost as if painted" (the owner).</summary>
            public bool painted;
        }'
Rep 'plates.Add(new Plate { label = label, x = x, z = z, nx = nx, nz = nz, y0 = beam.y0, y1 = beam.y1 });' 'plates.Add(new Plate { label = label, x = x, z = z, nx = nx, nz = nz, y0 = beam.y0, y1 = beam.y1, painted = room.role != null });'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.RoomNumbers.cs')
Rep 'static readonly Color PlateColour = new Color(0.10f, 0.13f, 0.18f), LetterColour = new Color(0.95f, 0.93f, 0.84f);' 'static readonly Color PlateColour = new Color(0.10f, 0.13f, 0.18f), LetterColour = new Color(0.95f, 0.93f, 0.84f), GoldColour = new Color(0.86f, 0.66f, 0.22f);'
Rep '            var letterTris = new List<int>(plates.Count * 120);
            foreach (var p in plates) AddPlate(p, verts, normals, plateTris, letterTris);' '            var letterTris = new List<int>(plates.Count * 120);
            var goldTris = new List<int>();
            foreach (var p in plates) AddPlate(p, verts, normals, plateTris, letterTris, goldTris);'
Rep '            mesh.subMeshCount = 2;
            mesh.SetTriangles(plateTris, 0);
            mesh.SetTriangles(letterTris, 1);' '            mesh.subMeshCount = 3;
            mesh.SetTriangles(plateTris, 0);
            mesh.SetTriangles(letterTris, 1);
            mesh.SetTriangles(goldTris, 2);'
Rep 'r.sharedMaterials = new[] { Flat(PlateColour, 0.35f), Flat(LetterColour, 0.2f) };' 'r.sharedMaterials = new[] { Flat(PlateColour, 0.35f), Flat(LetterColour, 0.2f), Flat(GoldColour, 0.3f) };'
Rep 'void AddPlate(RoomNumbers.Plate p, List<Vector3> verts, List<Vector3> normals, List<int> plateTris, List<int> letterTris)' 'void AddPlate(RoomNumbers.Plate p, List<Vector3> verts, List<Vector3> normals, List<int> plateTris, List<int> letterTris, List<int> goldTris)'
Rep '            AddQuad(centre, right, up, width * 0.5f, height * 0.5f, normal, verts, normals, plateTris);' '            if (!p.painted) AddQuad(centre, right, up, width * 0.5f, height * 0.5f, normal, verts, normals, plateTris);      // (a suite''s or the server room''s number is painted on the wall: gold letters, no plate)
            var letterList = p.painted ? goldTris : letterTris;'
Rep 'AddQuad(c, right, up, (b - a) * 0.5f, DotSize * 0.5f, normal, verts, normals, letterTris);' 'AddQuad(c, right, up, (b - a) * 0.5f, DotSize * 0.5f, normal, verts, normals, letterList);'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Glass.cs')
Rep '                float chance = outside >= 2 ? 1f : outside == 1 ? 0.6f : 0.25f;' '                float chance = room.role == OfficePlanner.RoleSuite ? 1f : outside >= 2 ? 1f : outside == 1 ? 0.6f : 0.25f;      // (an executive suite always has its glass front)'
Rep 'if (AddGlassFront(solid, frame, glass, w, outward, outside >= 1)) { windowedWalls.Add(w); windowedOutward[w] = outward; fronts++; }' 'if (AddGlassFront(solid, frame, glass, w, outward, outside >= 1 || room.role == OfficePlanner.RoleSuite)) { windowedWalls.Add(w); windowedOutward[w] = outward; fronts++; }'
Rep '        public int GlassDoors { get; private set; }' @'
        public int GlassDoors { get; private set; }
        /// <summary>The rooms that have a glass front (for the tests).</summary>
        public IReadOnlyList<Rc> GlassRoomBounds => glassRooms;
'@
Save-Src
Write-Output 'round 4 batch 1 patched'
