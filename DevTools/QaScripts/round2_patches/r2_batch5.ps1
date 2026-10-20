# Round 2, batch 5: one carpet colour per floor (owner: "too colorful, each floor a set color, floors can share colors"). Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'BE_qa\Assets\Scripts\FloorGenerator.Surfaces.cs'
$lines = [IO.File]::ReadAllLines($path)
function Find-Line([string]$startsWith, [int]$from = 0) { for ($i = $from; $i -lt $lines.Length; $i++) { if ($lines[$i].TrimStart().StartsWith($startsWith)) { return $i } } throw "not found: $startsWith" }

# ---- A: the palette replaces the per-room colour table
$a0 = Find-Line '// the carpet colours of each kind of room'
$a1 = Find-Line '};' $a0
$paletteText = @'
        /// <summary>
        /// The carpet of each floor (the owner, 2026-10-17: "the carpets are a bit too colorful. Each floor should be a set color and floors can have the same colors as others"): ONE quiet
        /// office colour for all the carpeted rooms of a floor, the same in every run, no two floors in a row alike (a palette of six, so floor 1 and floor 7 match).
        /// </summary>
        static readonly Color[] FloorCarpets =
        {
            Rgb(0.30f, 0.38f, 0.50f),   // slate blue
            Rgb(0.30f, 0.43f, 0.37f),   // sage green
            Rgb(0.47f, 0.30f, 0.33f),   // wine
            Rgb(0.40f, 0.41f, 0.44f),   // office grey
            Rgb(0.45f, 0.40f, 0.32f),   // taupe
            Rgb(0.27f, 0.41f, 0.45f),   // teal grey
        };
        static readonly Color OutpostCarpet = Rgb(0.50f, 0.24f, 0.24f);   // the guard outpost keeps its dark red

        /// <summary>The carpet colour of a floor (before the night-light boost).</summary>
        public static Color CarpetColourOf(int floor) => FloorCarpets[((floor - 1) % FloorCarpets.Length + FloorCarpets.Length) % FloorCarpets.Length];
        public static int CarpetPaletteSize => FloorCarpets.Length;
        public int CarpetTintCount { get; private set; }
'@
$new = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $a0; $i++) { $new.Add($lines[$i]) }
foreach ($l in ($paletteText -split "`r?`n")) { $new.Add($l) }
for ($i = $a1 + 1; $i -lt $lines.Length; $i++) { $new.Add($lines[$i]) }
$lines = $new.ToArray()

# ---- B + C: the floors loop
$b0 = Find-Line 'TileArea = 0f; CarpetArea = 0f; UsingOwnerCarpets = owners;'
$c1 = Find-Line 'CarpetKinds = carpets;' $b0
$loopText = @'
            TileArea = 0f; CarpetArea = 0f; UsingOwnerCarpets = owners;
            int carpets = 0;
            var floorColours = new HashSet<Color>();
            var floorCarpet = Boost(CarpetColourOf(layoutFloor));
            foreach (var p in L.patches)
            {
                var r = p.rc;
                int tint = p.tint;
                string key; Material mat; float repeat; bool isCarpet = true;
                if (tint == 0 || tint == 8)                                                       // the path: corridors and the lobby
                {
                    key = "tile" + tint;
                    mat = SurfaceMat(key, FloorTile(), tint == 8 ? Rgb(0.98f, 0.93f, 0.84f) : Rgb(0.90f, 0.92f, 0.95f), 0.45f);
                    repeat = TileRepeat;
                    TileArea += r.W * r.D;
                    isCarpet = false;
                }
                else if (tint == 12)                                                              // the guard outpost keeps its dark red
                {
                    key = "weave_outpost";
                    mat = SurfaceMat(key, CarpetWeave(), Boost(OutpostCarpet), 0.04f);
                    repeat = WeaveRepeat;
                    CarpetArea += r.W * r.D;
                }
                else if (owners)                                                                  // the owner's carpets: one of them for the whole floor
                {
                    var c = catalog.Pick(layoutFloor);
                    key = "owner_" + c.label;
                    mat = SurfaceMat(key, c.albedo, c.tint, 0.05f);
                    repeat = Mathf.Max(0.2f, c.tileMeters);
                    CarpetArea += r.W * r.D;
                    floorColours.Add(Color.clear);
                }
                else                                                                              // the game's own carpet: the floor's colour everywhere
                {
                    bool squares = tint == 1 || tint == 7 || tint == 10;
                    key = (squares ? "squares_" : "weave_") + ColorUtility.ToHtmlStringRGB(floorCarpet);
                    mat = SurfaceMat(key, squares ? CarpetSquares() : CarpetWeave(), floorCarpet, 0.04f);
                    repeat = WeaveRepeat;
                    CarpetArea += r.W * r.D;
                    floorColours.Add(floorCarpet);
                }
                if (!meshes.TryGetValue(key, out var mb)) { meshes[key] = mb = new MeshBuilder(); mats[key] = mat; if (isCarpet) carpets++; }
                mb.QuadUV(new Vector3(r.x0, FloorTop, r.z0), new Vector3(r.x0, FloorTop, r.z1), new Vector3(r.x1, FloorTop, r.z1), new Vector3(r.x1, FloorTop, r.z0), Vector3.up,
                          new Vector2(r.x0 / repeat, r.z0 / repeat), new Vector2(r.x0 / repeat, r.z1 / repeat), new Vector2(r.x1 / repeat, r.z1 / repeat), new Vector2(r.x1 / repeat, r.z0 / repeat));
            }
            CarpetTintCount = floorColours.Count;
            CarpetKinds = carpets;
'@
$new = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $b0; $i++) { $new.Add($lines[$i]) }
foreach ($l in ($loopText -split "`r?`n")) { $new.Add($l) }
for ($i = $c1 + 1; $i -lt $lines.Length; $i++) { $new.Add($lines[$i]) }
$lines = $new.ToArray()

# the doc comment of BuildRoomFloors
$text = ($lines -join "`n") + "`n"
$old = 'Carpet in the rooms (the owner''s carpets when there are some, painted ones otherwise: a different one from room to room, picked by where the room is), tile in the'
if (-not $text.Contains($old)) { throw 'doc line not found' }
$text = $text.Replace($old, 'Carpet in the rooms (the owner''s carpets when there are some, painted ones otherwise: ONE colour for the whole floor, see CarpetColourOf), tile in the')
[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
Write-Output "batch 5 patched ($($lines.Length) lines)"
