using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The inside of a floor. OfficePlanner (pure code, see Scripts/Interior) lays the floor out as corridors, offices,
    /// conference rooms, break rooms, storage, copy and server rooms, open-plan areas, lobbies and (on floor 1) a coffee
    /// shop, and says where every piece of furniture goes. This part builds that plan: partition walls, tinted floors,
    /// furniture from the asset packs, the hide triggers, and the loot, mugs and guard routes that come out of it.
    /// Everything is a pure function of the floor seed, so every player gets the identical floor.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Rooms (furniture, partitions, hiding places)")]
        [Tooltip("Furnish the floor with corridors, offices, break rooms and so on. Off = the old empty rooms.")]
        public bool furnishRooms = true;
        [Tooltip("Also place small desk clutter (cups, mice, paper). Turn this off if floors take too long to build.")]
        public bool smallProps = true;
        [Tooltip("Furniture in rooms farther than this from the camera is not drawn (0 = always draw everything).")]
        public float propDrawDistance = 60f;

        // the plan for the floor being built (null on the docks, the boss floor, or with furnishRooms off)
        Layout layout;
        readonly Dictionary<int, Transform> cellRoots = new Dictionary<int, Transform>();

        // Furniture prefabs are measured once and kept as inactive templates (no colliders), then cloned for every use.
        sealed class Proto
        {
            public GameObject go;
            public Vector3 centre, size;   // bounds relative to the template's pivot (identity rotation, scale applied)
            public float minY;
        }
        readonly Dictionary<string, Proto> protos = new Dictionary<string, Proto>();
        readonly HashSet<string> warned = new HashSet<string>();
        Transform protoHolder;
        Material partitionMat, panelMat;
        readonly Dictionary<int, Material> tintMats = new Dictionary<int, Material>();

        // floor colours per room kind (see OfficePlanner.TintOf)
        static readonly Color[] Tints =
        {
            new Color(0.74f, 0.72f, 0.68f), // 0 corridor
            new Color(0.78f, 0.82f, 0.70f), // 1 office
            new Color(0.62f, 0.70f, 0.82f), // 2 conference / training
            new Color(0.86f, 0.78f, 0.62f), // 3 break room / canteen
            new Color(0.66f, 0.62f, 0.56f), // 4 storage
            new Color(0.56f, 0.62f, 0.72f), // 5 server room
            new Color(0.80f, 0.62f, 0.70f), // 6 lounge
            new Color(0.70f, 0.78f, 0.68f), // 7 open plan
            new Color(0.90f, 0.86f, 0.76f), // 8 lobby
            new Color(0.90f, 0.76f, 0.58f), // 9 coffee shop
            new Color(0.72f, 0.76f, 0.82f), // 10 copy room
            new Color(0.64f, 0.66f, 0.60f), // 11 service area
            new Color(0.46f, 0.22f, 0.22f), // 12 guard outpost (the old red room: dark red carpet)
        };

        void OnDestroy()
        {
            foreach (var m in tintMats.Values) if (m) Kill(m);
            tintMats.Clear();
            if (partitionMat) Kill(partitionMat);
            if (panelMat) Kill(panelMat);
            DestroyDoorMaterial();
            DestroyNightMaterials();
            DestroyCarMaterials();
            DestroyOutpostMaterials();
            DestroyValuableMaterials();
            DestroySurfaceMaterials();
            if (protoHolder) Kill(protoHolder.gameObject);
        }

        // ------------------------------------------------------------------ planning

        /// <summary>Which neighbour the guest elevator's door faces (the first open side: west, south, east, north).</summary>
        Vector2Int GuestDoorDir(Vector2Int cell)
        {
            var dirs = new[] { Vector2Int.left, Vector2Int.down, Vector2Int.right, Vector2Int.up };
            foreach (var d in dirs)
            {
                var n = cell + d;
                if (n.x >= 0 && n.y >= 0 && n.x < cols && n.y < rows && IsOpen(cell, n)) return d;
            }
            return Vector2Int.left;
        }

        /// <summary>Lays the floor out (pure data) and decides which flagged props become loot.</summary>
        void PlanInterior(int floor, int seed)
        {
            layout = null;
            if (!furnishRooms) return;
            if (Mathf.Abs(cellSize - 18f) > 0.01f)
            {
                Debug.LogWarning("[Rooms] The room layouts are made for 18 m cells; cellSize is " + cellSize + ", so the floor is left empty.");
                return;
            }

            // (the same input FloorShape makes for a floor that is only being looked at: SafeRoomLabel)
            layout = OfficePlanner.Build(Shape().PlanFor(floor, seed, cellSize, wallHeight, IntelSeedFor(floor, seed)));
            layoutFloor = floor;
            layoutSeed = seed;
            SelectLoot(floor, seed);
        }

        int CellIndex(float x, float z)
        {
            int cx = Mathf.Clamp((int)(x / cellSize), 0, cols - 1), cz = Mathf.Clamp((int)(z / cellSize), 0, rows - 1);
            return cz * cols + cx;
        }

        /// <summary>
        /// Furniture is never loot (the owner, 2026-10-17: "rather than some monitors be interactable and some not, make the loot more stuff like watches and phones and
        /// such"): the themes still flag monitors, printers and so on, but nothing is picked, so every piece stays plain furniture. What there is to steal lies on the
        /// desks (<see cref="PlaceValuables"/>) and on the treasure spots (<see cref="PlaceLootFromLayout"/>).
        /// </summary>
        void SelectLoot(int floor, int seed)
        {
            foreach (var p in layout.pieces) p.picked = false;
        }

        // ------------------------------------------------------------------ building

        Transform CellRoot(Transform interior, float x, float z)
        {
            int key = CellIndex(x, z);
            if (!cellRoots.TryGetValue(key, out var t))
            {
                t = new GameObject($"Cell_{key % cols}_{key / cols}").transform;
                t.SetParent(interior, false);
                cellRoots[key] = t;
            }
            return t;
        }

        Material TintMat(int tint)
        {
            tint = Mathf.Clamp(tint, 0, Tints.Length - 1);
            if (tintMats.TryGetValue(tint, out var m) && m) return m;
            var src = floorMat ? floorMat : wallMat;
            if (!src) return null;
            m = new Material(src) { hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_Color")) m.color = Tints[tint];
            tintMats[tint] = m;
            return m;
        }

        Material PartitionMat()
        {
            if (partitionMat) return partitionMat;
            if (!wallMat) return null;
            partitionMat = new Material(wallMat) { hideFlags = HideFlags.HideAndDontSave };
            if (partitionMat.HasProperty("_Color")) partitionMat.color = Color.Lerp(wallMat.color, Color.white, 0.45f);
            return partitionMat;
        }

        Material PanelMat()
        {
            if (panelMat) return panelMat;
            var src = deskMat ? deskMat : wallMat;
            if (!src) return null;
            panelMat = new Material(src) { hideFlags = HideFlags.HideAndDontSave };
            if (panelMat.HasProperty("_Color")) panelMat.color = new Color(0.45f, 0.52f, 0.60f);
            return panelMat;
        }

        /// <summary>Partition walls, tinted floors and furniture (before the NavMesh bake, so all of it is in the bake).</summary>
        void BuildInterior()
        {
            cellRoots.Clear();
            if (layout == null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var L = layout;
            var interior = new GameObject("Interior").transform;
            interior.SetParent(current, false);

            // partitions: wall segments, and the beams over the doorways
            wallObjects.Clear();
            windowedWalls.Clear();
            var walls = new GameObject("Partitions").transform;
            walls.SetParent(interior, false);
            var pm = PartitionMat();
            var op = L.outpost;
            var opWalls = op != null ? OutpostWallMat() : null;                 // (the guard outpost's own walls are dark slate)
            var opArea = op != null ? op.outer.Grow(0.02f) : default(Rc);
            foreach (var w in L.walls)
            {
                var r = w.rc;
                bool mine = opWalls != null && r.x0 >= opArea.x0 && r.x1 <= opArea.x1 && r.z0 >= opArea.z0 && r.z1 <= opArea.z1;
                var go = Box(w.kind == "header" ? "DoorHeader" : w.kind == "lockheader" ? "LockHeader" : "Partition",
                             new Vector3(r.Cx, (w.y0 + w.y1) * 0.5f, r.Cz), new Vector3(r.W, w.y1 - w.y0, r.D),
                             mine ? opWalls : pm ? pm : wallMat, walls);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                wallObjects[w] = go;
            }

            // floors: carpet in the rooms and tile on the path, one merged mesh per surface (no collider, so the NavMesh ignores it); one painted wall in every room
            // (FloorGenerator.Surfaces.cs). If either fails the floor still loads: plain coloured floors, bare walls.
            try { BuildRoomFloors(interior, L); }
            catch (System.Exception e) { Debug.LogWarning("[Floors] The carpet and the tile could not be laid, so the rooms get plain coloured floors: " + e); BuildFlatFloors(interior, L); }
            try { BuildOfficeWindows(interior, L); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Glass] The glass fronts could not be built, so the offices keep their plain walls: " + e);
                RestoreWindowedWalls();
                var half = interior.Find("OfficeWindows");
                if (half) { half.gameObject.SetActive(false); Kill(half.gameObject); }
            }
            try { BuildAccentWalls(interior, L); }
            catch (System.Exception e) { Debug.LogWarning("[Floors] The accent walls could not be painted: " + e); }

            BuildRoomNumbers(interior);
            BuildDirectionSigns(interior);

            // furniture
            int made = 0, boxes = 0;
            foreach (var p in L.pieces)
            {
                if (p.picked) continue; // spawned as loot after the bake
                if (!smallProps && p.def.cat == PropCat.DeskItem) continue;
                try
                {
                    var parent = CellRoot(interior, p.x, p.z);
                    var go = SpawnPiece(p, parent, ref boxes);
                    if (go) made++;
                }
                catch (System.Exception e) { LogOnce("furnishing " + p.def.key, e); }
            }

            if (Application.isPlaying && propDrawDistance > 0f)
            {
                var culler = interior.gameObject.AddComponent<PropCuller>();
                culler.Setup(new List<Transform>(cellRoots.Values), propDrawDistance);
            }
            Debug.Log($"[Rooms] Floor built: {made} props ({boxes} plain boxes), {L.walls.Count} partitions, {L.hideZones.Count} hiding zones, " +
                      $"{L.rooms.Count} rooms in {sw.ElapsedMilliseconds} ms");
        }

        GameObject SpawnPiece(Piece p, Transform parent, ref int boxes)
        {
            var def = p.def;
            bool wantCollider = p.solid && !p.mount && !p.onTop;

            if (def.path == null && def.key == "locker2") return BuildLocker(p, parent, wantCollider);
            if (def.path == null && def.key == "vault_safe" && Application.isPlaying) return BuildFloorSafePiece(p, parent);

            if (def.path == null)
            {
                // a free-standing screen / cubicle divider
                var go = Box("Screen", new Vector3(p.rc.Cx, p.h * 0.5f, p.rc.Cz), new Vector3(p.rc.W, p.h, p.rc.D), PanelMat(), parent);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                return go;
            }

            var pr = GetProto(def);
            if (pr == null)
            {
                // the pack is not in the project: stand in a plain box for furniture, skip the small stuff
                if (!wantCollider) return null;
                var go = Box(def.key, new Vector3(p.rc.Cx, p.y + p.h * 0.5f, p.rc.Cz), new Vector3(p.rc.W, p.h, p.rc.D), deskMat, parent);
                boxes++;
                return go;
            }

            var inst = PlaceProto(pr, def, p, parent);
            if (wantCollider) AddBox(inst, pr, Mathf.Max(def.H, 0.05f));
            return inst;
        }

        /// <summary>A clone of the template, turned and moved so its footprint sits at the piece's spot and its underside at the piece's height.</summary>
        GameObject PlaceProto(Proto pr, PropDef def, Piece p, Transform parent)
        {
            var rot = Quaternion.Euler(0f, p.yaw + def.FrontYaw, 0f);
            var off = rot * new Vector3(pr.centre.x, 0f, pr.centre.z);
            var pos = new Vector3(p.x - off.x, p.y - pr.minY, p.z - off.z);
            var go = Instantiate(pr.go, pos, rot, parent);
            go.name = def.key;
            return go;
        }

        /// <summary>One box collider around the whole prop (cheap, and a good fit for furniture).</summary>
        static void AddBox(GameObject go, Proto pr, float maxHeight)
        {
            var s = go.transform.lossyScale;
            if (Mathf.Abs(s.x) < 1e-5f || Mathf.Abs(s.y) < 1e-5f || Mathf.Abs(s.z) < 1e-5f) return;
            float h = Mathf.Min(pr.size.y, maxHeight);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(pr.centre.x / s.x, (pr.minY + h * 0.5f) / s.y, pr.centre.z / s.z);
            bc.size = new Vector3(pr.size.x / s.x, h / s.y, pr.size.z / s.z);
        }

        // ------------------------------------------------------------------ prop templates

        static bool CastsShadow(PropDef def)
        {
            if (def.cat == PropCat.DeskItem || def.cat == PropCat.Mount) return false;
            return Mathf.Max(def.W, def.D) >= 0.6f || def.H >= 0.9f;
        }

        Proto GetProto(PropDef def)
        {
            if (protos.TryGetValue(def.key, out var p) && (p == null || p.go)) return p;
            var cat = Office;
            var prefab = cat ? cat.Get(def.key) : null;
            if (!prefab)
            {
                if (warned.Add("missing:" + def.key))
                    Debug.LogWarning($"[Rooms] No prefab for '{def.key}' ({def.path}); using a plain box. Run Employees Take Revenge > Setup Office Pack.");
                protos[def.key] = null;
                return null;
            }
            p = MakeProto(def.key, prefab, def.scale, CastsShadow(def));
            if (p != null)
            {
                float ex = def.nx * def.scale, ey = def.ny * def.scale, ez = def.nz * def.scale;
                if (Mathf.Abs(p.size.x - ex) > Mathf.Max(0.05f, ex * 0.15f) || Mathf.Abs(p.size.y - ey) > Mathf.Max(0.05f, ey * 0.15f) ||
                    Mathf.Abs(p.size.z - ez) > Mathf.Max(0.05f, ez * 0.15f))
                    if (warned.Add("size:" + def.key))
                        Debug.LogWarning($"[Rooms] '{def.key}' measures {p.size.x:0.00} x {p.size.y:0.00} x {p.size.z:0.00} m but PropTable expects {ex:0.00} x {ey:0.00} x {ez:0.00}.");
            }
            protos[def.key] = p;
            return p;
        }

        /// <summary>Instantiates a prefab once, measures it, strips colliders and lights, and parks it as an inactive template.</summary>
        Proto MakeProto(string id, GameObject prefab, float scale, bool castShadows)
        {
            if (!protoHolder)
            {
                var stale = transform.Find("PropTemplates"); // left behind by a script reload in the editor
                if (stale) Kill(stale.gameObject);
                var h = new GameObject("PropTemplates");
                h.hideFlags = HideFlags.HideAndDontSave;
                h.transform.SetParent(transform, false);
                h.SetActive(false);
                protoHolder = h.transform;
            }
            var go = Instantiate(prefab);
            go.name = id;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = go.transform.localScale * scale;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) DestroyImmediate(l);
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) { DestroyImmediate(go); return null; }
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            if (!castShadows) foreach (var r in rs) r.shadowCastingMode = ShadowCastingMode.Off;
            go.transform.SetParent(protoHolder, false);
            return new Proto { go = go, centre = b.center, size = b.size, minY = b.min.y };
        }

        // ------------------------------------------------------------------ hiding places

        /// <summary>One trigger per cluster of furniture (after the bake). Stand in one and press H.</summary>
        void MakeHideZones()
        {
            if (layout == null) return;
            var root = new GameObject("HideZones").transform;
            root.SetParent(current, false);
            int i = 0;
            foreach (var z in layout.hideZones)
            {
                var go = new GameObject("Hide_" + i++);
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(z.Cx, 0.9f, z.Cz);
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(z.W, 1.8f, z.D);
                go.AddComponent<HideSpot>();
            }
        }

        // ------------------------------------------------------------------ loot, mugs

        /// <summary>
        /// The big treasures go to the planner's treasure spots: a stapler on a desk, a trophy on a credenza, an espresso machine in the break room, a server, a safe.
        /// The ones that take two or three people to carry only stand where those people have room to lift and walk them out (<see cref="LootPlan.RoomForCarry"/>);
        /// a spot that is too cramped is skipped and the next one used.
        /// </summary>
        void PlaceLootFromLayout(System.Random rng, int floor, float scale, ref int total, ref int count)
        {
            var slots = new List<Slot>(layout.bigSlots);
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = slots[i]; slots[i] = slots[j]; slots[j] = t;
            }
            int want = LootPlan.TreasureCount(floor), placed = 0, cramped = 0;
            for (int i = 0; i < slots.Count && placed < want; i++)
            {
                try
                {
                    var s = slots[i];
                    int idx = LootIndex(s.kind);
                    var t = LootTable[idx];
                    if (!LootPlan.RoomForCarry(layout, s, t.lifters)) { cramped++; continue; }
                    var pos = new Vector3(s.x, s.y + t.size.y * 0.5f + 0.03f, s.z);
                    var go = Box(t.name, pos, t.size, lootMat, null, dynamic: true);
                    go.transform.SetParent(current);
                    go.transform.rotation = Quaternion.Euler(0f, s.yaw, 0f);
                    var l = go.AddComponent<HeavyLoot>();
                    l.displayName = t.name; l.lifters = t.lifters;
                    l.baseValue = Mathf.RoundToInt(t.value * scale / 50f) * 50;
                    go.GetComponent<Rigidbody>().mass = t.lifters * l.massPerLifter;
                    EnsurePickup(go).weight = t.weight;
                    total += l.baseValue; count++; placed++;
                }
                catch (System.Exception e) { LogOnce("treasure " + slots[i].kind, e); }
            }
            if (cramped > 0) Debug.Log($"[Loot] floor {floor}: {placed} treasures placed, {cramped} spots skipped because two or three people could not carry what stands there");
        }

        /// <summary>HeavyLoot adds its Pickup in Awake, which Unity skips in edit mode (the floor preview), so make sure it is there.</summary>
        static Pickup EnsurePickup(GameObject go)
        {
            var pk = go.GetComponent<Pickup>();
            return pk ? pk : go.AddComponent<Pickup>();
        }

        /// <summary>Logs a problem with one prop once, so a bad prefab does not flood the Console or stop the floor.</summary>
        void LogOnce(string what, System.Exception e)
        {
            if (warned.Add("ex:" + what)) Debug.LogError("[Rooms] Problem while " + what + " (the floor still loads): " + e);
        }

        static int LootIndex(string kind)
        {
            switch (kind)
            {
                case "stapler": return 0;
                case "espresso": return 1;
                case "server": return 2;
                case "trophy": return 3;
                default: return 4; // safe
            }
        }

        /// <summary>A prefab turned into loot at a world position (Test Mode's spawn buttons).</summary>
        GameObject ModelLootObj(GameObject prefab, string label, Vector3 pos, float yaw, int lifters, float weight, int value, float scale)
        {
            string id = "prefab:" + prefab.name;
            if (!protos.TryGetValue(id, out var pr) || pr == null || !pr.go)
            {
                pr = MakeProto(id, prefab, 1f, true);
                protos[id] = pr;
            }
            GameObject go;
            if (pr != null)
            {
                var rot = Quaternion.Euler(0f, yaw, 0f);
                var off = rot * new Vector3(pr.centre.x, 0f, pr.centre.z);
                go = Instantiate(pr.go, new Vector3(pos.x - off.x, pos.y - pr.minY, pos.z - off.z), rot, current ? current : transform);
                AddBox(go, pr, 100f);
                go.AddComponent<Rigidbody>();
            }
            else
            {
                go = Box(label, pos + Vector3.up * 0.3f, new Vector3(0.6f, 0.6f, 0.6f), lootMat, current ? current : transform, dynamic: true);
            }
            go.name = label;
            var l = go.AddComponent<HeavyLoot>();
            l.displayName = label; l.lifters = lifters;
            l.baseValue = Mathf.RoundToInt(value * scale / 50f) * 50;
            go.GetComponent<Rigidbody>().mass = lifters * l.massPerLifter;
            EnsurePickup(go).weight = weight;
            return go;
        }

        readonly List<Slot> spareMugSlots = new List<Slot>();            // desks and counters that got no mug

        /// <summary>Mugs (something to throw to lure guards) sit on desks and counters.</summary>
        void PlaceMugsOnSurfaces(System.Random rng)
        {
            var slots = new List<Slot>(layout.mugSlots);
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var t = slots[i]; slots[i] = slots[j]; slots[j] = t;
            }
            int n = Mathf.Min(slots.Count, cols * rows * 3 / 2);
            spareMugSlots.Clear();
            for (int i = n; i < slots.Count; i++) spareMugSlots.Add(slots[i]);
            for (int i = 0; i < n; i++)
            {
                var s = slots[i];
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"Mug_{i}"; go.transform.SetParent(current);
                go.transform.position = new Vector3(s.x, s.y + 0.13f, s.z);
                go.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
                go.GetComponent<Renderer>().sharedMaterial = mugMat;
                go.AddComponent<Rigidbody>().mass = 0.4f;
                go.AddComponent<Throwable>();
            }
        }

        // ------------------------------------------------------------------ guard routes

        /// <summary>Patrol stops: the planner's walkable spots in a cell (inside its rooms and corridors).</summary>
        List<Vector3>[,] GuardSpots()
        {
            var spots = new List<Vector3>[cols, rows];
            if (layout == null) return spots;
            foreach (var w in layout.waypoints)
            {
                int c = (int)w[2], r = (int)w[3];
                if (c < 0 || r < 0 || c >= cols || r >= rows) continue;
                if (InOutpost(new Vector3(w[0], 0f, w[1]))) continue;          // the outpost has its own guard: the patrols do not go in
                if (spots[c, r] == null) spots[c, r] = new List<Vector3>();
                spots[c, r].Add(new Vector3(w[0], 0f, w[1]));
            }
            return spots;
        }
    }
}
