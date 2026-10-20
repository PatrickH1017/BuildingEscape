using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// What makes an office floor look like a place, not a grey box (the owner, 2026-10-17: "add a floor path, rooms should be carpet but the path should be a tile like texture";
    /// "the office feels like it is missing some character/life like it doesn't have a lot of color or uniqueness ... Please add windows on the outside walls"; "use a city similar to the one
    /// behind the boss in the cinematic outside the windows on the floors"; "I added carpet materials to use"):
    ///  - the rooms are CARPET, in many colours and two weaves (the owner's carpets from <see cref="FloorSurfaceCatalog"/> when the project has some, the game's own painted ones otherwise);
    ///  - the corridors ("the path") and the lobby are TILE;
    ///  - every room has one painted accent wall;
    ///  - the outside walls have WINDOWS (real openings: piers, sills, heads and steel frames, one merged mesh), and the cinematic's city lies outside them (<see cref="ExteriorCity"/>).
    /// All of it is cosmetic: no network ids, no random numbers of the floor's own generator (it hashes the place), no colliders (the outside walls keep theirs, hidden), so it can never
    /// change where anybody can walk or what is generated; a failure is a warning and the plain walls come back.
    /// </summary>
    public partial class FloorGenerator
    {
        // ------------------------------------------------------------------ floors: carpet in the rooms, tile on the path

        const float FloorTop = 0.010f, WeaveRepeat = 2.4f, TileRepeat = 1.2f, CarpetBoost = 1.15f;
        /// <summary>Carpet tiles (0.6 m squares with seams) show a grid like the corridors' tile, so by default every carpeted room gets plain broadloom: grid = path, no grid = rooms.</summary>
        const bool CarpetTilesOn = false;
        static Texture2D carpetWeaveTex, carpetSquaresTex, floorTileTex;
        readonly Dictionary<string, Material> surfaceMats = new Dictionary<string, Material>();

        /// <summary>For tests and pictures: how many square metres of the floor on show are tile and how many carpet, how many different carpets are laid, whether they are the owner's.</summary>
        public float TileArea { get; private set; }
        public float CarpetArea { get; private set; }
        public int CarpetKinds { get; private set; }
        public bool UsingOwnerCarpets { get; private set; }
        public int AccentWalls { get; private set; }
        public int WindowCount { get; private set; }
        public int CityTowers { get; private set; }
        /// <summary>How many cars drive in the streets of the city outside, and how many roads there are (see <see cref="CityStreets"/>).</summary>
        public int CityCars { get; private set; }
        public int CityRoads { get; private set; }
        public int CityMeshes { get; private set; }
        public float CityReach { get; private set; }
        public Transform Scenery { get; private set; }
        public long SceneryMs { get; private set; }
        /// <summary>The plan of the floor on show (null on the docks, the boss floor, or when the floor could not be furnished), for the test tools.</summary>
        public Layout CurrentLayout => layout;

        static Color Rgb(float r, float g, float b) => new Color(r, g, b, 1f);

        /// <summary>
        /// The carpet of each floor (the owner, 2026-10-17: "the carpets are a bit too colorful. Each floor should be a set color and floors can have the same colors as others"): ONE quiet
        /// office colour for all the carpeted rooms of a floor, the same in every run, no two floors in a row alike (a palette of six, so floor 1 and floor 7 match).
        /// </summary>
        static readonly Color[] FloorCarpets =
        {
            Rgb(0.33f, 0.39f, 0.48f),   // slate blue
            Rgb(0.32f, 0.42f, 0.36f),   // sage green
            Rgb(0.45f, 0.32f, 0.34f),   // wine
            Rgb(0.40f, 0.41f, 0.43f),   // office grey
            Rgb(0.45f, 0.41f, 0.35f),   // taupe
            Rgb(0.29f, 0.40f, 0.43f),   // teal grey
        };
        static readonly Color OutpostCarpet = Rgb(0.50f, 0.24f, 0.24f);   // the guard outpost keeps its dark red

        /// <summary>The carpet colour of a floor (before the night-light boost).</summary>
        public static Color CarpetColourOf(int floor) => FloorCarpets[((floor - 1) % FloorCarpets.Length + FloorCarpets.Length) % FloorCarpets.Length];
        public static int CarpetPaletteSize => FloorCarpets.Length;
        public int CarpetTintCount { get; private set; }

        /// <summary>The floors are lit dimly at night, so the carpet colours are painted a little brighter than they should read.</summary>
        static Color Boost(Color c) => new Color(Mathf.Min(1f, c.r * CarpetBoost), Mathf.Min(1f, c.g * CarpetBoost), Mathf.Min(1f, c.b * CarpetBoost), 1f);

        static int Mix2(int a, int b) { unchecked { int h = a * 73856093 ^ b * 19349663; return h ^ (h >> 15); } }

        static float SurfHash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        static Texture2D Paint(string name, int w, int h, System.Func<int, int, float> shade)
        {
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    byte g = (byte)(Mathf.Clamp01(shade(x, y)) * 255f);
                    px[y * w + x] = new Color32(g, g, g, 255);
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>Broadloom carpet: a fine grain with the faint ridges of the weave (tiles every 2.4 m, the pixels are 4 cm).</summary>
        static Texture2D CarpetWeave() => carpetWeaveTex ? carpetWeaveTex : (carpetWeaveTex =
            Paint("CarpetWeave", 64, 64, (x, y) => (0.86f + 0.14f * SurfHash(x, y)) * ((y & 1) == 0 ? 1f : 0.965f) * ((x & 1) == 0 ? 1f : 0.985f)));

        /// <summary>Carpet tiles, 0.6 m square (four by four to the picture), each a hair different in shade and in the grain's direction, with the thin seams between them.</summary>
        static Texture2D CarpetSquares() => carpetSquaresTex ? carpetSquaresTex : (carpetSquaresTex =
            Paint("CarpetSquares", 128, 128, (x, y) =>
            {
                int sx = x / 32, sy = y / 32, lx = x % 32, ly = y % 32;
                float tile = 0.93f + 0.07f * SurfHash(sx + 3, sy + 5);
                float grain = (((sx + sy) & 1) == 0 ? (lx / 4) & 1 : (ly / 4) & 1) == 0 ? 1f : 0.965f;
                float seam = lx == 0 || ly == 0 ? 0.80f : 1f;
                return tile * grain * seam * (0.95f + 0.05f * SurfHash(x * 3 + 1, y * 5 + 2));
            }));

        /// <summary>Floor tile, 0.6 m square (two by two to the picture), a little different from one to the next, in grout, with a fine speckle.</summary>
        static Texture2D FloorTile() => floorTileTex ? floorTileTex : (floorTileTex =
            Paint("FloorTile", 128, 128, (x, y) =>
            {
                int lx = x % 64, ly = y % 64;
                if (lx < 2 || ly < 2) return 0.50f;                                                     // the grout
                float tile = 0.88f + 0.12f * SurfHash(x / 64 + 7, y / 64 + 3);
                float bevel = lx < 5 || ly < 5 || lx > 60 || ly > 60 ? 0.93f : 1f;
                return tile * bevel * (0.96f + 0.04f * SurfHash(x * 3 + 1, y * 5 + 2));
            }));

        Material SurfaceMat(string key, Texture tex, Color tint, float gloss, float metallic = 0f)
        {
            if (surfaceMats.TryGetValue(key, out var m) && m) return m;
            m = Surface("Floor_" + key, tint, gloss, false, metallic);
            m.mainTexture = tex;
            surfaceMats[key] = m;
            return m;
        }

        void DestroySurfaceMaterials()
        {
            foreach (var m in surfaceMats.Values) if (m) Kill(m);
            surfaceMats.Clear();
        }

        static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            if (!shadows) mr.shadowCastingMode = ShadowCastingMode.Off;
            go.AddComponent<OwnedMesh>().mesh = mesh;                                             // freed with the floor
            return go;
        }

        /// <summary>
        /// The floors of the rooms: ONE mesh per surface (a carpet colour, or tile), each patch of the plan a flat quad whose UVs are in metres, so every picture repeats at the same size across the
        /// whole floor. Carpet in the rooms (the owner's carpets when there are some, painted ones otherwise: ONE colour for the whole floor, see CarpetColourOf), tile in the
        /// corridors and the lobby. Replaces the one flat-coloured slab per patch.
        /// </summary>
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
                    bool squares = CarpetTilesOn && (tint == 1 || tint == 7 || tint == 10);
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

        static readonly Color[] AccentTeal = { Rgb(0.10f, 0.58f, 0.64f), Rgb(0.14f, 0.42f, 0.58f), Rgb(0.36f, 0.62f, 0.38f), Rgb(0.36f, 0.38f, 0.72f), Rgb(0.86f, 0.42f, 0.24f), Rgb(0.90f, 0.70f, 0.20f) };
        static readonly Color[] AccentWarm = { Rgb(0.86f, 0.42f, 0.24f), Rgb(0.90f, 0.70f, 0.20f), Rgb(0.36f, 0.62f, 0.38f), Rgb(0.80f, 0.28f, 0.42f) };
        static readonly Color[] AccentSoft = { Rgb(0.58f, 0.34f, 0.62f), Rgb(0.80f, 0.28f, 0.42f), Rgb(0.10f, 0.58f, 0.64f), Rgb(0.36f, 0.38f, 0.72f) };
        static readonly Color[] AccentDark = { Rgb(0.14f, 0.42f, 0.58f), Rgb(0.34f, 0.42f, 0.52f) };

        static Color AccentFor(string theme, int h)
        {
            Color[] set;
            switch (theme)
            {
                case "break": case "canteen": case "cafe_counter": case "cafe_seating": set = AccentWarm; break;
                case "lounge": case "nook": case "lobby": set = AccentSoft; break;
                case "server": case "storage": case "copy": case "service": set = AccentDark; break;
                default: set = AccentTeal; break;
            }
            return set[h % set.Length];
        }

        /// <summary>
        /// One wall of every room (not the corridors, not the guard outpost) is painted in an accent colour of the room's own: the longest stretch of partition on the room's edge that is at least
        /// three metres long, a thin sheet of colour a centimetre and a bit off the wall, on the room's side only. Merged into one mesh per colour. Where the room's furniture stands in front
        /// of it the paint shows round and above it, which is the point.
        /// </summary>
        void BuildAccentWalls(Transform interior, Layout L)
        {
            AccentWalls = 0;
            var byColour = new Dictionary<Color, MeshBuilder>();
            foreach (var room in L.rooms)
            {
                if (room.kind == 2 || (L.outpost != null && L.outpost.room == room)) continue;
                var rb = room.bounds;
                WallBox best = null; float bestLen = 3f, bestA = 0f, bestB = 0f; int bestSide = 0;
                foreach (var w in L.walls)
                {
                    if (w.kind != "wall" || w.y1 - w.y0 < wallHeight * 0.8f || windowedWalls.Contains(w)) continue;           // (full-height partitions only: no cubicle dividers)
                    var r = w.rc;
                    bool horizontal = r.W > r.D;
                    if ((horizontal ? r.D : r.W) > 0.6f) continue;
                    float a, b; int side;
                    if (horizontal)
                    {
                        a = Mathf.Max(r.x0, rb.x0 + 0.15f); b = Mathf.Min(r.x1, rb.x1 - 0.15f);
                        if (Mathf.Abs(r.z1 - rb.z0) < 0.5f && r.Cz < rb.Cz) side = 1;                 // the wall below the room: its face looks +z
                        else if (Mathf.Abs(r.z0 - rb.z1) < 0.5f && r.Cz > rb.Cz) side = 2;            // above: -z
                        else continue;
                    }
                    else
                    {
                        a = Mathf.Max(r.z0, rb.z0 + 0.15f); b = Mathf.Min(r.z1, rb.z1 - 0.15f);
                        if (Mathf.Abs(r.x1 - rb.x0) < 0.5f && r.Cx < rb.Cx) side = 3;                 // left of the room: +x
                        else if (Mathf.Abs(r.x0 - rb.x1) < 0.5f && r.Cx > rb.Cx) side = 4;            // right: -x
                        else continue;
                    }
                    if (b - a > bestLen) { best = w; bestLen = b - a; bestA = a; bestB = b; bestSide = side; }
                }
                if (best == null) continue;
                var colour = AccentFor(room.theme, Mathf.Abs(Mix2(Mathf.RoundToInt(rb.Cx * 3f), Mathf.RoundToInt(rb.Cz * 3f))));
                if (!byColour.TryGetValue(colour, out var mb)) byColour[colour] = mb = new MeshBuilder();
                float y0 = 0.02f, y1 = best.y1, off = 0.012f; var wr = best.rc;
                switch (bestSide)
                {
                    case 1: { float z = wr.z1 + off; mb.Quad(new Vector3(bestB, y0, z), new Vector3(bestB, y1, z), new Vector3(bestA, y1, z), new Vector3(bestA, y0, z), Vector3.forward); break; }
                    case 2: { float z = wr.z0 - off; mb.Quad(new Vector3(bestA, y0, z), new Vector3(bestA, y1, z), new Vector3(bestB, y1, z), new Vector3(bestB, y0, z), Vector3.back); break; }
                    case 3: { float x = wr.x1 + off; mb.Quad(new Vector3(x, y0, bestA), new Vector3(x, y1, bestA), new Vector3(x, y1, bestB), new Vector3(x, y0, bestB), Vector3.right); break; }
                    default: { float x = wr.x0 - off; mb.Quad(new Vector3(x, y0, bestB), new Vector3(x, y1, bestB), new Vector3(x, y1, bestA), new Vector3(x, y0, bestA), Vector3.left); break; }
                }
                AccentWalls++;
            }
            if (byColour.Count == 0) return;
            var paint = new GameObject("AccentWalls").transform;
            paint.SetParent(interior, false);
            foreach (var kv in byColour)
            {
                var mesh = kv.Value.ToMesh("Accent_" + ColorUtility.ToHtmlStringRGB(kv.Key));
                if (mesh != null) MeshObject(paint, "Accent_" + ColorUtility.ToHtmlStringRGB(kv.Key), mesh, SurfaceMat("accent_" + ColorUtility.ToHtmlStringRGB(kv.Key), Texture2D.whiteTexture, kv.Key, 0.08f), false);
            }
        }

        // ------------------------------------------------------------------ windows along the outside walls, and the city beyond them

        sealed class OuterSeg { public bool alongX; public float line, lo, hi; public GameObject go; }
        readonly List<OuterSeg> outerSegs = new List<OuterSeg>();
        bool officeFloor;

        const float WindowBay = 4.5f, SillHeight = 0.85f, WindowTopHeight = 2.65f, PierWidth = 1.0f;

        /// <summary>BuildShell made an outside wall of an office floor: it keeps its collider (nobody can leave the building) but not its look, which <see cref="BuildScenery"/> makes with windows in it.</summary>
        void OuterWall(GameObject wall, bool alongX, float line, float lo, float hi)
        {
            if (!officeFloor || !wall) return;
            var r = wall.GetComponent<Renderer>();
            if (r) r.enabled = false;
            outerSegs.Add(new OuterSeg { alongX = alongX, line = line, lo = lo, hi = hi, go = wall });
        }

        /// <summary>Where partition walls meet an outside wall (a pier stands there, so a wall never ends in the middle of a window).</summary>
        List<float> PartitionEnds(OuterSeg s)
        {
            var ends = new List<float>();
            if (layout == null) return ends;
            foreach (var w in layout.walls)
            {
                if (w.kind != "wall") continue;
                var r = w.rc;
                if (s.alongX)
                {
                    if (r.D > r.W && (Mathf.Abs(r.z0 - s.line) < 0.7f || Mathf.Abs(r.z1 - s.line) < 0.7f) && r.Cx > s.lo && r.Cx < s.hi) ends.Add(r.Cx);
                }
                else if (r.W > r.D && (Mathf.Abs(r.x0 - s.line) < 0.7f || Mathf.Abs(r.x1 - s.line) < 0.7f) && r.Cz > s.lo && r.Cz < s.hi) ends.Add(r.Cz);
            }
            return ends;
        }

        static void Block(MeshBuilder b, OuterSeg s, float t0, float t1, float y0, float y1, float inner = -0.2f, float outer = 0.2f)
        {
            if (t1 - t0 < 0.005f) return;
            if (s.alongX) b.Box(new Vector3(t0, y0, s.line + inner), new Vector3(t1, y1, s.line + outer));
            else b.Box(new Vector3(s.line + inner, y0, t0), new Vector3(s.line + outer, y1, t1));
        }

        void AddWindow(MeshBuilder wall, MeshBuilder frame, OuterSeg s, float a, float b, float inward)
        {
            Block(wall, s, a, b, 0f, SillHeight);                                       // the sill: wall under the window
            Block(wall, s, a, b, WindowTopHeight, wallHeight);                          // the head: wall over it
            const float jamb = 0.07f, depth = 0.07f;
            Block(frame, s, a, a + jamb, SillHeight, WindowTopHeight, -depth, depth);   // steel frame: two jambs, a mullion, a transom, a lip top and bottom
            Block(frame, s, b - jamb, b, SillHeight, WindowTopHeight, -depth, depth);
            if (b - a > 2.2f) Block(frame, s, (a + b) * 0.5f - 0.03f, (a + b) * 0.5f + 0.03f, SillHeight, WindowTopHeight, -depth, depth);
            Block(frame, s, a, b, 1.72f, 1.77f, -depth, depth);
            Block(frame, s, a, b, SillHeight, SillHeight + 0.04f, -depth, depth);
            Block(frame, s, a, b, WindowTopHeight - 0.04f, WindowTopHeight, -depth, depth);
            // the inside sill, a ledge on the room's side
            float lo = inward > 0f ? 0.2f : -0.34f, hi = inward > 0f ? 0.34f : -0.2f;
            Block(wall, s, a - 0.04f, b + 0.04f, SillHeight, SillHeight + 0.04f, lo, hi);
            WindowCount++;
        }

        void AddWindowWall(MeshBuilder wall, MeshBuilder frame, OuterSeg s, float inward)
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
                if (gapEnd - cursor >= 1.2f) AddWindow(wall, frame, s, cursor, gapEnd, inward);
                else Block(wall, s, cursor, gapEnd, 0f, wallHeight);                       // too narrow for a window: wall
                if (i < merged.Count) { Block(wall, s, merged[i].x, merged[i].y, 0f, wallHeight); cursor = merged[i].y; }
            }
        }

        /// <summary>Windows in the outside walls and the city outside them. Never stops a floor: if anything goes wrong the plain outside walls are shown again.</summary>
        void BuildScenery(int floor, int seed)
        {
            WindowCount = 0; CityTowers = 0; CityMeshes = 0; CityReach = 0f; SceneryMs = 0; CityCars = 0; CityRoads = 0;
            if (outerSegs.Count == 0) return;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var holder = new GameObject("Scenery").transform;
                holder.SetParent(current, false);
                Scenery = holder;
                var wall = new MeshBuilder(); var frame = new MeshBuilder();
                float cx = cols * cellSize * 0.5f, cz = rows * cellSize * 0.5f;
                foreach (var s in outerSegs) AddWindowWall(wall, frame, s, s.alongX ? (cz > s.line ? 1f : -1f) : (cx > s.line ? 1f : -1f));
                var walls = wall.ToMesh("OuterWalls");
                var frames = frame.ToMesh("WindowFrames");
                if (walls != null) MeshObject(holder, "OuterWalls", walls, wallMat, true);
                if (frames != null) MeshObject(holder, "WindowFrames", frames, SurfaceMat("window_frame", Texture2D.whiteTexture, Rgb(0.11f, 0.12f, 0.14f), 0.45f, 0.55f), true);
                var city = ExteriorCity.Build(holder, new Vector3(cx, 0f, cz), cx, cz, -(4f + 3.2f * Mathf.Max(0, floor)), seed, out var stats);
                CityTowers = stats.towers; CityMeshes = stats.meshes; CityReach = stats.maxReach; CityCars = stats.cars; CityRoads = stats.roads;
                SceneryMs = clock.ElapsedMilliseconds;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Scenery] The windows and the city could not be built, so the outside walls stay plain: " + e);
                foreach (var s in outerSegs) if (s.go) { var r = s.go.GetComponent<Renderer>(); if (r) r.enabled = true; }
                if (Scenery) { Scenery.gameObject.SetActive(false); Kill(Scenery.gameObject); Scenery = null; }
            }
        }
    }
}
