# Round 2, batch 13: tile and carpet never share a spot of floor (the owner: "make sure the tile grids don't overlap with any carpets"); the night test ignores the scenery. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# ---- BuildRoomFloors: patches are laid in an order, and what is already laid with another surface is cut out of the next one
$path = Join-Path $Dir 'Scripts\FloorGenerator.Surfaces.cs'
$lines = [IO.File]::ReadAllLines($path)
$start = -1; $end = -1
for ($i = 0; $i -lt $lines.Length; $i++) {
  if ($start -lt 0 -and $lines[$i].Contains('void BuildRoomFloors(Transform interior, Layout L)')) { $start = $i }
  if ($start -ge 0 -and $i -gt $start -and $lines[$i].Contains('/// <summary>The fallback when the carpet and tile cannot be laid')) { $end = $i; break }
}
if ($start -lt 0 -or $end -lt 0) { throw 'BuildRoomFloors not found' }
$method = @'
        /// <summary>For tests: the rectangles of floor that were laid as tile and as carpet (no tile lies over a carpet or the other way round: two textures on one plane flicker).</summary>
        public readonly List<Rc> LaidTiles = new List<Rc>(), LaidCarpets = new List<Rc>();

        /// <summary>The order the patches of the plan are laid in: the guard outpost's carpet, the other carpets, the lobby's tile, the corridors' tile.</summary>
        static int Rank(int tint) => tint == 12 ? 0 : tint == 8 ? 2 : tint == 0 ? 3 : 1;

        /// <summary>The parts of each rectangle that <paramref name="hole"/> does not cover (up to four pieces per rectangle).</summary>
        public static List<Rc> CutOut(List<Rc> rects, Rc hole)
        {
            var result = new List<Rc>();
            foreach (var r in rects)
            {
                if (hole.x0 >= r.x1 || hole.x1 <= r.x0 || hole.z0 >= r.z1 || hole.z1 <= r.z0) { result.Add(r); continue; }
                float ix0 = Mathf.Max(r.x0, hole.x0), ix1 = Mathf.Min(r.x1, hole.x1);
                if (r.x0 < ix0) result.Add(new Rc(r.x0, r.z0, ix0, r.z1));                                          // the strip to the left of the hole (full depth)
                if (ix1 < r.x1) result.Add(new Rc(ix1, r.z0, r.x1, r.z1));                                          // the strip to the right
                if (r.z0 < hole.z0) result.Add(new Rc(ix0, r.z0, ix1, Mathf.Min(hole.z0, r.z1)));                   // below the hole
                if (hole.z1 < r.z1) result.Add(new Rc(ix0, Mathf.Max(hole.z1, r.z0), ix1, r.z1));                   // above it
            }
            return result;
        }

        void BuildRoomFloors(Transform interior, Layout L)
        {
            var floors = new GameObject("RoomFloors").transform;
            floors.SetParent(interior, false);
            var meshes = new Dictionary<string, MeshBuilder>();
            var mats = new Dictionary<string, Material>();
            var catalog = FloorSurfaceCatalog.Instance;
            bool owners = catalog != null && catalog.UsableCount > 0;
            TileArea = 0f; CarpetArea = 0f; UsingOwnerCarpets = owners;
            LaidTiles.Clear(); LaidCarpets.Clear();
            int carpets = 0;
            var floorColours = new HashSet<Color>();
            var floorCarpet = Boost(CarpetColourOf(layoutFloor));
            var order = new List<FloorPatch>(L.patches);
            order.Sort((a, b) => Rank(a.tint).CompareTo(Rank(b.tint)));
            var painted = new List<KeyValuePair<Rc, string>>();
            foreach (var p in order)
            {
                var r = p.rc;
                int tint = p.tint;
                string key; Material mat; float repeat; bool isCarpet = true;
                if (tint == 0 || tint == 8)                                                       // the path: corridors and the lobby
                {
                    key = "tile" + tint;
                    mat = SurfaceMat(key, FloorTile(), tint == 8 ? Rgb(0.98f, 0.93f, 0.84f) : Rgb(0.90f, 0.92f, 0.95f), 0.45f);
                    repeat = TileRepeat;
                    isCarpet = false;
                }
                else if (tint == 12)                                                              // the guard outpost keeps its dark red
                {
                    key = "weave_outpost";
                    mat = SurfaceMat(key, CarpetWeave(), Boost(OutpostCarpet), 0.04f);
                    repeat = WeaveRepeat;
                }
                else if (owners)                                                                  // the owner's carpets: one of them for the whole floor
                {
                    var c = catalog.Pick(layoutFloor);
                    key = "owner_" + c.label;
                    mat = SurfaceMat(key, c.albedo, c.tint, 0.05f);
                    repeat = Mathf.Max(0.2f, c.tileMeters);
                    floorColours.Add(Color.clear);
                }
                else                                                                              // the game's own carpet: the floor's colour everywhere
                {
                    bool squares = tint == 1 || tint == 7 || tint == 10;
                    key = (squares ? "squares_" : "weave_") + ColorUtility.ToHtmlStringRGB(floorCarpet);
                    mat = SurfaceMat(key, squares ? CarpetSquares() : CarpetWeave(), floorCarpet, 0.04f);
                    repeat = WeaveRepeat;
                    floorColours.Add(floorCarpet);
                }
                // what is already laid with another surface is not laid over again: patches of the plan overlap where a corridor runs into a room, and two textures on one plane flicker
                var pieces = new List<Rc> { r };
                foreach (var done in painted) if (done.Value != key) pieces = CutOut(pieces, done.Key);
                painted.Add(new KeyValuePair<Rc, string>(r, key));
                if (!meshes.TryGetValue(key, out var mb)) { meshes[key] = mb = new MeshBuilder(); mats[key] = mat; if (isCarpet) carpets++; }
                foreach (var q in pieces)
                {
                    float area = q.W * q.D;
                    if (area < 0.01f) continue;
                    if (isCarpet) { CarpetArea += area; LaidCarpets.Add(q); } else { TileArea += area; LaidTiles.Add(q); }
                    mb.QuadUV(new Vector3(q.x0, FloorTop, q.z0), new Vector3(q.x0, FloorTop, q.z1), new Vector3(q.x1, FloorTop, q.z1), new Vector3(q.x1, FloorTop, q.z0), Vector3.up,
                              new Vector2(q.x0 / repeat, q.z0 / repeat), new Vector2(q.x0 / repeat, q.z1 / repeat), new Vector2(q.x1 / repeat, q.z1 / repeat), new Vector2(q.x1 / repeat, q.z0 / repeat));
                }
            }
            CarpetTintCount = floorColours.Count;
            CarpetKinds = carpets;
            foreach (var kv in meshes)
            {
                var mesh = kv.Value.ToMesh("RoomFloor_" + kv.Key);
                if (mesh != null) MeshObject(floors, "RoomFloor_" + kv.Key, mesh, mats[kv.Key], false);
            }
        }
'@
$new = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $start; $i++) { $new.Add($lines[$i]) }
foreach ($l in ($method -split "`r?`n")) { $new.Add($l) }
$new.Add('')
for ($i = $end; $i -lt $lines.Length; $i++) { $new.Add($lines[$i]) }
if (-not $env:R2_DRY) { [IO.File]::WriteAllText($path, (($new -join "`n") + "`n"), (New-Object Text.UTF8Encoding($false))) }
Write-Output "BuildRoomFloors replaced (lines $start-$end of $($lines.Length))"

# ---- the night test must not count the city's sky dome as "above the walls"
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevDoors.cs')
Rep 'if (floorRoot) foreach (var r in floorRoot.GetComponentsInChildren<Renderer>()) if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;' 'if (floorRoot) foreach (var r in floorRoot.GetComponentsInChildren<Renderer>()) { if (gen.Scenery && r.transform.IsChildOf(gen.Scenery)) continue; if (r.bounds.max.y > maxY) maxY = r.bounds.max.y; }       // (the city outside the windows reaches far above)'
Save-Src

# ---- Looks: no tile over carpet
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Look.cs')
Rep '                LookCheck($"{f} the outside walls have windows"' @'
                float overlap = 0f;
                foreach (var tl in gen.LaidTiles)
                    foreach (var cp in gen.LaidCarpets)
                    {
                        float ow = Mathf.Min(tl.x1, cp.x1) - Mathf.Max(tl.x0, cp.x0), od = Mathf.Min(tl.z1, cp.z1) - Mathf.Max(tl.z0, cp.z0);
                        if (ow > 0.001f && od > 0.001f) overlap += ow * od;
                    }
                LookCheck($"{f} no tile lies over a carpet and no carpet over tile", overlap < 0.01f && gen.LaidTiles.Count > 5 && gen.LaidCarpets.Count > 5, $"(overlap {overlap:0.000} m2 of {gen.LaidTiles.Count} tile and {gen.LaidCarpets.Count} carpet rectangles)");
                LookCheck($"{f} the outside walls have windows"
'@
Save-Src

# ---- pure test of the cutting
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Test.cs')
Rep @'
            // ---- the outpost computer's log-in
'@ @'
            // ---- tile and carpet never share a spot of floor: a rectangle with a rectangle cut out of it
            var whole = new System.Collections.Generic.List<Rc> { new Rc(0f, 0f, 10f, 6f) };
            float AreaOf(System.Collections.Generic.List<Rc> rs) { float a = 0f; foreach (var rr in rs) a += rr.W * rr.D; return a; }
            var centreCut = FloorGenerator.CutOut(whole, new Rc(3f, 2f, 7f, 4f));
            var cornerCut = FloorGenerator.CutOut(whole, new Rc(8f, 4f, 14f, 9f));
            var allCut = FloorGenerator.CutOut(whole, new Rc(-1f, -1f, 11f, 7f));
            var apart = FloorGenerator.CutOut(whole, new Rc(20f, 0f, 30f, 6f));
            var twice = FloorGenerator.CutOut(centreCut, new Rc(0f, 0f, 5f, 3f));
            bool noOverlapLeft = true;
            foreach (var pc in centreCut) noOverlapLeft &= !(pc.x1 > 3f && pc.x0 < 7f && pc.z1 > 2f && pc.z0 < 4f);
            Check("floors: cutting a rectangle out of another leaves exactly the rest (a hole in the middle, a corner, all of it, none of it, a second cut)",
                Mathf.Abs(AreaOf(centreCut) - (60f - 8f)) < 1e-3f && centreCut.Count == 4 && noOverlapLeft && Mathf.Abs(AreaOf(cornerCut) - (60f - 4f)) < 1e-3f && allCut.Count == 0 && apart.Count == 1 && Mathf.Abs(AreaOf(twice) - 39f) < 1e-3f);

            // ---- the outpost computer's log-in
'@
Save-Src
Write-Output 'batch 13 patched'
