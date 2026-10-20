# Round 2, batch 2: the floors (carpet + tile), accent walls, windows and city wired into FloorGenerator. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets\Scripts'

# ---- FloorGenerator.cs
Open-Src (Join-Path $Dir 'FloorGenerator.cs')
Rep @'
            layout = null;
            dockWalls = hub;
            BuildShell();
'@ @'
            layout = null;
            dockWalls = hub;
            officeFloor = !hub && !boss;          // the outside walls of an office floor get windows (FloorGenerator.Surfaces.cs)
            outerSegs.Clear();
            BuildShell();
'@
Rep @'
            // the guard outpost keeps random spots and patrol stops out of itself
            if (layout != null && layout.outpost != null)
'@ @'
            BuildScenery(floor, seed);        // windows in the outside walls and the city beyond them (cosmetic; it can never stop a floor)
            // the guard outpost keeps random spots and patrol stops out of itself
            if (layout != null && layout.outpost != null)
'@
Rep 'if (solid) WallZ(x, z0 + cellSize / 2f, cellSize);' @'
if (solid)
                    {
                        var wall = WallZ(x, z0 + cellSize / 2f, cellSize);
                        if (c == 0 || c == cols) OuterWall(wall, false, x, z0, z0 + cellSize);
                    }
'@
Rep 'if (solid) WallX(x0 + cellSize / 2f, z, cellSize);' @'
if (solid)
                    {
                        var wall = WallX(x0 + cellSize / 2f, z, cellSize);
                        if (r == 0 || r == rows) OuterWall(wall, true, z, x0, x0 + cellSize);
                    }
'@
Rep @'
        void WallZ(float x, float z, float len) =>
'@ @'
        GameObject WallZ(float x, float z, float len) =>
'@
Rep @'
        void WallX(float x, float z, float len) =>
'@ @'
        GameObject WallX(float x, float z, float len) =>
'@
Save-Src

# ---- FloorGenerator.Interior.cs
Open-Src (Join-Path $Dir 'FloorGenerator.Interior.cs')
Rep @'
            // floors: a thin tinted slab per room (no collider, so the NavMesh ignores it)
            var floors = new GameObject("RoomFloors").transform;
            floors.SetParent(interior, false);
            foreach (var p in L.patches)
            {
                var r = p.rc;
                var go = Box("RoomFloor", new Vector3(r.Cx, 0.005f, r.Cz), new Vector3(r.W, 0.01f, r.D), TintMat(p.tint), floors, collider: false);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
'@ @'
            // floors: carpet in the rooms and tile on the path, one merged mesh per surface (no collider, so the NavMesh ignores it); one painted wall in every room
            // (FloorGenerator.Surfaces.cs). If either fails the floor still loads: plain coloured floors, bare walls.
            try { BuildRoomFloors(interior, L); }
            catch (System.Exception e) { Debug.LogWarning("[Floors] The carpet and the tile could not be laid, so the rooms get plain coloured floors: " + e); BuildFlatFloors(interior, L); }
            try { BuildAccentWalls(interior, L); }
            catch (System.Exception e) { Debug.LogWarning("[Floors] The accent walls could not be painted: " + e); }
'@
Rep @'
            DestroyValuableMaterials();
            if (protoHolder) Kill(protoHolder.gameObject);
'@ @'
            DestroyValuableMaterials();
            DestroySurfaceMaterials();
            if (protoHolder) Kill(protoHolder.gameObject);
'@
Save-Src

# ---- FloorGenerator.Surfaces.cs: the plain-floor fallback, the full-height test for accent walls, no `from`/`to` identifiers, clamped carpet boost
Open-Src (Join-Path $Dir 'FloorGenerator.Surfaces.cs')
Rep 'public bool alongX; public float line, from, to; public GameObject go;' 'public bool alongX; public float line, lo, hi; public GameObject go;'
Rep 'void OuterWall(GameObject wall, bool alongX, float line, float from, float to)' 'void OuterWall(GameObject wall, bool alongX, float line, float lo, float hi)'
Rep 'outerSegs.Add(new OuterSeg { alongX = alongX, line = line, from = from, to = to, go = wall });' 'outerSegs.Add(new OuterSeg { alongX = alongX, line = line, lo = lo, hi = hi, go = wall });'
Rep 'r.Cx > s.from && r.Cx < s.to' 'r.Cx > s.lo && r.Cx < s.hi'
Rep 'r.Cz > s.from && r.Cz < s.to' 'r.Cz > s.lo && r.Cz < s.hi'
Rep 'float length = s.to - s.from;' 'float length = s.hi - s.lo;'
Rep 'piers.Add(new Vector2(Mathf.Max(s.from, c - PierWidth * 0.5f), Mathf.Min(s.to, c + PierWidth * 0.5f)));' 'piers.Add(new Vector2(Mathf.Max(s.lo, c - PierWidth * 0.5f), Mathf.Min(s.hi, c + PierWidth * 0.5f)));'
Rep 'float c = s.from + i * bay;' 'float c = s.lo + i * bay;'
Rep 'float cursor = s.from;' 'float cursor = s.lo;'
Rep 'float gapEnd = i < merged.Count ? merged[i].x : s.to;' 'float gapEnd = i < merged.Count ? merged[i].x : s.hi;'
Rep 'if (w.kind != "wall") continue;
                    var r = w.rc;
                    bool horizontal' 'if (w.kind != "wall" || w.y1 - w.y0 < wallHeight * 0.8f) continue;           // (full-height partitions only: no cubicle dividers)
                    var r = w.rc;
                    bool horizontal'
Rep 'var col = set[variant % set.Length] * CarpetBoost; col.a = 1f;' 'var col = Boost(set[variant % set.Length]);'
Rep '        static int Mix2(int a, int b)' @'
        /// <summary>The floors are lit dimly at night, so the carpet colours are painted a little brighter than they should read.</summary>
        static Color Boost(Color c) => new Color(Mathf.Min(1f, c.r * CarpetBoost), Mathf.Min(1f, c.g * CarpetBoost), Mathf.Min(1f, c.b * CarpetBoost), 1f);

        static int Mix2(int a, int b)
'@
Rep '        // ------------------------------------------------------------------ one painted accent wall in every room' @'
        /// <summary>The fallback when the carpet and tile cannot be laid: a thin tinted slab per patch of the plan, as the floors always were.</summary>
        void BuildFlatFloors(Transform interior, Layout L)
        {
            var old = interior.Find("RoomFloors");
            if (old) { old.gameObject.SetActive(false); Kill(old.gameObject); }
            var floors = new GameObject("RoomFloors").transform;
            floors.SetParent(interior, false);
            foreach (var p in L.patches)
            {
                var r = p.rc;
                var go = Box("RoomFloor", new Vector3(r.Cx, 0.005f, r.Cz), new Vector3(r.W, 0.01f, r.D), TintMat(p.tint), floors, collider: false);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ one painted accent wall in every room
'@
Save-Src
Write-Output 'batch 2 patched'
