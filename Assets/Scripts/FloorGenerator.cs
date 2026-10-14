using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    public struct FloorInfo
    {
        public int floor, cols, rows, rooms, lootCount, lootValue, quota, guards;
        public Vector3 spawn, elevator;
        public float estimatedMinutes;
        /// <summary>True for the basement: a small safe zone with the vendor (no guards, loot or quota).</summary>
        public bool isHub;
    }

    /// <summary>
    /// Builds one office floor at runtime: a maze of rooms (DFS spanning tree + a few loops),
    /// sized from targetMinutes so a full sweep of the floor takes about that long.
    /// Rooms hold loot, desks (hide spots) and mugs; guards patrol slowly between rooms.
    /// </summary>
    public class FloorGenerator : MonoBehaviour
    {
        [Header("Size (derived from target time)")]
        [Tooltip("How long a full sweep of one floor should take.")]
        public float targetMinutes = 7f;
        [Tooltip("Effective team speed in m/s once carrying, waiting and hiding are counted.")]
        public float assumedMoveSpeed = 3.5f;
        public float cellSize = 18f;
        public float wallHeight = 3.5f;
        public float doorWidth = 4f;
        [Range(0f, 0.5f)] public float loopChance = 0.12f;

        [Header("Basement (safe zone with the vendor)")]
        [Tooltip("Basement size in rooms. Far smaller than a normal floor. The elevator is in the first room, the vendor in the last.")]
        public int basementCols = 2;
        public int basementRows = 1;

        [Header("Guards (slow on purpose: easier to coordinate with friends)")]
        public float guardWalkSpeed = 1.5f;
        public float guardInvestigateSpeed = 2.2f;
        public float guardChaseSpeed = 3.6f;
        public int baseGuards = 3;
        public int maxGuards = 12;

        [Header("Materials")]
        public Material floorMat, wallMat, deskMat, elevatorMat, lootMat, mugMat, guardMat;
        [Tooltip("Optional. The vendor falls back to the loot material when empty.")]
        public Material vendorMat;

        static readonly (string name, Vector3 size, int lifters, int value, float weight)[] LootTable =
        {
            ("Golden Stapler",   new Vector3(0.7f, 0.4f, 0.4f), 1, 800,  4f),
            ("Espresso Machine", new Vector3(0.9f, 0.9f, 0.8f), 2, 1500, 12f),
            ("Prototype Server", new Vector3(1.0f, 1.4f, 0.9f), 2, 2500, 18f),
            ("Crystal Trophy",   new Vector3(0.5f, 0.9f, 0.5f), 1, 1200, 6f),
            ("Executive Safe",   new Vector3(1.1f, 1.1f, 1.1f), 3, 3000, 30f), // too heavy for a pack: carry with friends (Q)
        };

        [Header("Office pack (Overtime Escape > Setup Office Pack)")]
        [Tooltip("Leave empty to load Resources/OfficeCatalog automatically.")]
        public OfficeCatalog office;
        public float deskScale = 1.2f;

        OfficeCatalog Office => office ? office : (office = Resources.Load<OfficeCatalog>("OfficeCatalog"));
        readonly List<Vector3> deskTops = new();
        readonly List<float> deskYaws = new();

        Transform current;
        bool[,] openE, openN;
        int cols, rows;

        public Transform Current => current;

        /// <summary>Destroys the previous floor (call a frame before Generate so its NavMesh is gone).</summary>
        public void Clear()
        {
            if (current) Kill(current.gameObject);
            current = null;
        }

        /// <param name="floor">Level index: 0 = Basement (safe zone), 1..10 = office floors.</param>
        public FloorInfo Generate(int floor, int seed)
        {
            bool hub = floor <= 0;
            var rng = new System.Random(seed);
            current = new GameObject($"Floor_{floor}").transform;
            current.SetParent(transform, false);

            int rooms;
            if (hub)
            {
                // ---- basement: a tiny open space, no maze
                cols = Mathf.Max(2, basementCols);
                rows = Mathf.Max(1, basementRows);
                rooms = cols * rows;
                OpenAllDoors();
            }
            else
            {
                // ---- size from the time target
                float roomsF = targetMinutes * 60f * assumedMoveSpeed / (2f * cellSize) + 1f;
                int roomCount = Mathf.Max(6, Mathf.RoundToInt(roomsF));
                rows = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(roomCount / 1.5f)));
                cols = Mathf.Max(2, Mathf.RoundToInt(roomCount / (float)rows));
                rooms = cols * rows;
                CarveMaze(rng);
                // the elevator room always opens to both of its neighbours, so you can leave by whichever
                // side is closer to where you're heading instead of walking around
                if (cols > 1) openE[0, 0] = true;
                if (rows > 1) openN[0, 0] = true;
            }

            var dist = Distances();
            // One real shaft: the elevator is in the same corner room on every level, so it lines up
            // from the basement to the top. The team arrives inside it.
            var elevCell = Vector2Int.zero;
            var info = new FloorInfo
            {
                floor = floor, cols = cols, rows = rows, rooms = rooms, isHub = hub,
                spawn = CellCenter(elevCell) + new Vector3(0f, 0f, 0.5f), // middle of the elevator car
                elevator = CellCenter(elevCell),
                estimatedMinutes = hub ? 0f : 2f * (rooms - 1) * cellSize / assumedMoveSpeed / 60f
            };

            BuildShell();
            var deskSpots = hub ? new List<Vector3>() : BuildFurniture(rng, elevCell);
            BuildElevatorRoom(elevCell);

            if (hub)
            {
                BuildVendor(new Vector2Int(cols - 1, rows - 1));
                return info; // safe zone: no loot, mugs, hide spots, guards (so no NavMesh needed either)
            }

            // ---- bake before adding dynamic things
            var surface = current.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            foreach (var (pos, i) in WithIndex(deskSpots)) MakeHideSpot(pos, i, deskYaws[i]);
            PlaceLoot(rng, floor, elevCell, ref info);
            PlaceMugs(rng);
            info.guards = PlaceGuards(rng, floor, dist, elevCell);
            return info;
        }

        // ------------------------------------------------------------------ maze

        void CarveMaze(System.Random rng)
        {
            openE = new bool[cols, rows];
            openN = new bool[cols, rows];
            var visited = new bool[cols, rows];
            var stack = new Stack<Vector2Int>();
            visited[0, 0] = true; stack.Push(Vector2Int.zero);
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            var opts = new List<Vector2Int>();
            while (stack.Count > 0)
            {
                var cur = stack.Peek();
                opts.Clear();
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (n.x >= 0 && n.y >= 0 && n.x < cols && n.y < rows && !visited[n.x, n.y]) opts.Add(n);
                }
                if (opts.Count == 0) { stack.Pop(); continue; }
                var next = opts[rng.Next(opts.Count)];
                SetOpen(cur, next);
                visited[next.x, next.y] = true;
                stack.Push(next);
            }
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    if (c < cols - 1 && !openE[c, r] && rng.NextDouble() < loopChance) openE[c, r] = true;
                    if (r < rows - 1 && !openN[c, r] && rng.NextDouble() < loopChance) openN[c, r] = true;
                }
        }

        /// <summary>Basement layout: every interior wall has a door.</summary>
        void OpenAllDoors()
        {
            openE = new bool[cols, rows];
            openN = new bool[cols, rows];
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    openE[c, r] = c < cols - 1;
                    openN[c, r] = r < rows - 1;
                }
        }

        void SetOpen(Vector2Int a, Vector2Int b)
        {
            if (b.x > a.x) openE[a.x, a.y] = true;
            else if (b.x < a.x) openE[b.x, b.y] = true;
            else if (b.y > a.y) openN[a.x, a.y] = true;
            else openN[b.x, b.y] = true;
        }

        bool IsOpen(Vector2Int a, Vector2Int b)
        {
            if (b.x > a.x) return openE[a.x, a.y];
            if (b.x < a.x) return openE[b.x, b.y];
            if (b.y > a.y) return openN[a.x, a.y];
            return openN[b.x, b.y];
        }

        int[,] Distances()
        {
            var dist = new int[cols, rows];
            for (int c = 0; c < cols; c++) for (int r = 0; r < rows; r++) dist[c, r] = -1;
            var q = new Queue<Vector2Int>();
            dist[0, 0] = 0; q.Enqueue(Vector2Int.zero);
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                foreach (var d in dirs)
                {
                    var n = cur + d;
                    if (n.x < 0 || n.y < 0 || n.x >= cols || n.y >= rows || dist[n.x, n.y] >= 0) continue;
                    if (!IsOpen(cur, n)) continue;
                    dist[n.x, n.y] = dist[cur.x, cur.y] + 1;
                    q.Enqueue(n);
                }
            }
            return dist;
        }

        Vector2Int FarthestCell(int[,] dist)
        {
            var best = Vector2Int.zero; int bd = -1;
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    if (dist[c, r] > bd) { bd = dist[c, r]; best = new Vector2Int(c, r); }
            return best;
        }

        Vector3 CellCenter(Vector2Int c) => new Vector3((c.x + 0.5f) * cellSize, 0f, (c.y + 0.5f) * cellSize);

        // ------------------------------------------------------------------ geometry

        void BuildShell()
        {
            Box("Floor", new Vector3(cols * cellSize / 2f, -0.25f, rows * cellSize / 2f),
                new Vector3(cols * cellSize, 0.5f, rows * cellSize), floorMat);

            float gap = doorWidth, half = (cellSize - gap) / 2f;
            // walls running along Z at x = c * cellSize
            for (int c = 0; c <= cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    bool solid = c == 0 || c == cols || !openE[c - 1, r];
                    float x = c * cellSize, z0 = r * cellSize;
                    if (solid) WallZ(x, z0 + cellSize / 2f, cellSize);
                    else
                    {
                        WallZ(x, z0 + half / 2f, half);
                        WallZ(x, z0 + cellSize - half / 2f, half);
                    }
                }
            // walls running along X at z = r * cellSize
            for (int r = 0; r <= rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    bool solid = r == 0 || r == rows || !openN[c, r - 1];
                    float z = r * cellSize, x0 = c * cellSize;
                    if (solid) WallX(x0 + cellSize / 2f, z, cellSize);
                    else
                    {
                        WallX(x0 + half / 2f, z, half);
                        WallX(x0 + cellSize - half / 2f, z, half);
                    }
                }
        }

        void WallZ(float x, float z, float len) =>
            Box("Wall", new Vector3(x, wallHeight / 2f, z), new Vector3(0.4f, wallHeight, len), wallMat);
        void WallX(float x, float z, float len) =>
            Box("Wall", new Vector3(x, wallHeight / 2f, z), new Vector3(len, wallHeight, 0.4f), wallMat);

        List<Vector3> BuildFurniture(System.Random rng, Vector2Int elevCell)
        {
            var spots = new List<Vector3>();
            deskTops.Clear(); deskYaws.Clear();
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    var cell = new Vector2Int(c, r);
                    if (cell == Vector2Int.zero || cell == elevCell) continue;
                    if (rng.NextDouble() > 0.55) continue;
                    var pos = CellCenter(cell) + new Vector3(Rand(rng, -5f, 5f), 0, Rand(rng, -5f, 5f));
                    var cat = Office;
                    float yaw = rng.Next(2) * 90f;
                    if (cat && cat.desk1)
                    {
                        var prefab = (rng.Next(2) == 0 || !cat.desk2) ? cat.desk1 : cat.desk2;
                        var d = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), current);
                        d.name = "Desk"; d.transform.localScale = Vector3.one * deskScale;
                        deskTops.Add(pos + Vector3.up * 0.95f * deskScale);
                    }
                    else
                    {
                        yaw = 0f;
                        Box("Desk", pos + new Vector3(0, 0.75f, 0), new Vector3(2.4f, 0.15f, 1.2f), deskMat);
                        foreach (var sx in new[] { -1f, 1f })
                            Box("DeskLeg", pos + new Vector3(sx * 1.1f, 0.35f, 0), new Vector3(0.15f, 0.7f, 1.1f), deskMat);
                    }
                    deskYaws.Add(yaw);
                    spots.Add(pos);
                }
            return spots;
        }

        void MakeHideSpot(Vector3 pos, int i, float yaw)
        {
            var hs = new GameObject($"HideSpot_{i}");
            hs.transform.SetParent(current); hs.transform.position = pos + new Vector3(0, 0.4f, 0);
            bool model = deskTops.Count > 0;
            hs.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var c = hs.AddComponent<BoxCollider>(); c.isTrigger = true;
            c.size = model ? new Vector3(1.6f, 0.9f, 2.8f) : new Vector3(2.2f, 0.9f, 1.4f);
            hs.AddComponent<HideSpot>();
        }

        /// <summary>
        /// The elevator shaft is a private walled room inside the far cell with one door.
        /// Its interior is marked Not Walkable on the NavMesh so guards never wander in.
        /// </summary>
        void BuildElevatorRoom(Vector2Int cell)
        {
            var center = CellCenter(cell);
            const float half = 4f, doorHalf = 1.5f, t = 0.4f;
            var room = new GameObject("ElevatorRoom");
            room.transform.SetParent(current); room.transform.position = center;

            // One door, facing the east neighbour (north if east is closed), so the exit is a few steps from the car.
            bool doorEast = cols > 1 && IsOpen(cell, cell + Vector2Int.right);
            bool doorNorth = !doorEast && rows > 1 && IsOpen(cell, cell + Vector2Int.up); // one shaft door only
            if (!doorEast && !doorNorth) doorEast = true;
            float segLen = half - doorHalf;
            // north (+z) and south (-z) walls
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? half : -half;
                bool hasDoor = doorNorth && side == 0;
                if (!hasDoor) Box(side == 0 ? "ShaftWall_N" : "ShaftWall_S", center + new Vector3(0, wallHeight / 2f, z), new Vector3(half * 2 + t, wallHeight, t), wallMat, room.transform);
                else
                {
                    Box("ShaftWall_N1", center + new Vector3(-(doorHalf + segLen / 2f) - t / 2f, wallHeight / 2f, z), new Vector3(segLen + t, wallHeight, t), wallMat, room.transform);
                    Box("ShaftWall_N2", center + new Vector3(doorHalf + segLen / 2f + t / 2f, wallHeight / 2f, z), new Vector3(segLen + t, wallHeight, t), wallMat, room.transform);
                }
            }
            // east (+x) and west (-x) walls
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? half : -half;
                bool hasDoor = doorEast && side == 0;
                if (!hasDoor) Box(side == 0 ? "ShaftWall_E" : "ShaftWall_W", center + new Vector3(x, wallHeight / 2f, 0), new Vector3(t, wallHeight, half * 2), wallMat, room.transform);
                else
                {
                    Box("ShaftWall_E1", center + new Vector3(x, wallHeight / 2f, -(doorHalf + segLen / 2f)), new Vector3(t, wallHeight, segLen), wallMat, room.transform);
                    Box("ShaftWall_E2", center + new Vector3(x, wallHeight / 2f, doorHalf + segLen / 2f), new Vector3(t, wallHeight, segLen), wallMat, room.transform);
                }
            }

            // sliding door panels that fill each gap (open at first; they close when the quota is met)
            var door = room.AddComponent<ElevatorDoor>();
            var dMat = elevatorMat ? elevatorMat : wallMat;
            float dt = t * 0.8f, pw = doorHalf; // each panel covers half the gap
            if (doorEast)
            {
                var pa = Box("DoorPanel_E_A", center + new Vector3(half, wallHeight / 2f, -pw / 2f), new Vector3(dt, wallHeight, pw), dMat, room.transform);
                var pb = Box("DoorPanel_E_B", center + new Vector3(half, wallHeight / 2f, pw / 2f), new Vector3(dt, wallHeight, pw), dMat, room.transform);
                door.Setup(pa.transform, pb.transform, new Vector3(0, 0, -pw), new Vector3(0, 0, pw));
            }
            if (doorNorth)
            {
                var pa = Box("DoorPanel_N_A", center + new Vector3(-pw / 2f, wallHeight / 2f, half), new Vector3(pw, wallHeight, dt), dMat, room.transform);
                var pb = Box("DoorPanel_N_B", center + new Vector3(pw / 2f, wallHeight / 2f, half), new Vector3(pw, wallHeight, dt), dMat, room.transform);
                door.Setup(pa.transform, pb.transform, new Vector3(-pw, 0, 0), new Vector3(pw, 0, 0));
            }

            Box("ElevatorFloor", center + new Vector3(0, 0.02f, 0.5f), new Vector3(5, 0.1f, 4.5f), elevatorMat, room.transform, collider: false);

            var zone = new GameObject("DropZone");
            zone.transform.SetParent(room.transform, false);
            zone.transform.localPosition = new Vector3(0, 1.2f, 0.5f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(5, 2.4f, 4.5f);
            zone.AddComponent<ElevatorExtraction>().door = door;

            // keep guards out
            var keepOut = new GameObject("GuardKeepOut");
            keepOut.transform.SetParent(room.transform, false);
            var mv = keepOut.AddComponent<NavMeshModifierVolume>();
            mv.center = new Vector3(0, 1f, 0);
            mv.size = new Vector3(half * 2 - 0.2f, 3f, half * 2 - 0.2f);
            mv.area = 1; // built-in "Not Walkable"
        }

        /// <summary>
        /// The basement shop: a counter with the vendor behind it, a sale zone in front of it
        /// (F sells loot inside, number keys buy) and a shelf point where bought items appear.
        /// </summary>
        void BuildVendor(Vector2Int cell)
        {
            var c = CellCenter(cell);
            var mat = vendorMat ? vendorMat : lootMat;

            var root = new GameObject("Vendor");
            root.transform.SetParent(current);
            root.transform.position = c;

            Box("VendorCounter", c + new Vector3(0f, 0.55f, 4f), new Vector3(6f, 1.1f, 1.2f), deskMat, root.transform);
            Box("VendorSign", c + new Vector3(0f, 2.8f, 8.6f), new Vector3(5f, 0.8f, 0.2f), mat, root.transform, collider: false);

            // the vendor: a capsule behind the counter, facing the shop floor (-Z)
            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "VendorNPC";
            npc.transform.SetParent(root.transform);
            npc.transform.position = c + new Vector3(0f, 1f, 5.6f);
            npc.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            npc.GetComponent<Renderer>().sharedMaterial = mat;
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.transform.SetParent(npc.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.6f, 0.45f);
            nose.transform.localScale = new Vector3(0.4f, 0.15f, 0.2f);
            nose.GetComponent<Renderer>().sharedMaterial = deskMat;
            Kill(nose.GetComponent<Collider>());
            if (Application.isPlaying) PartyCharacterFactory.Attach(npc, PartyCharacterFactory.VendorLook);

            // shady back-alley setup: stacked crates and duffel bags behind him, a dim bare bulb
            var back = c + new Vector3(0f, 0f, 7.6f);
            Box("Crate1", back + new Vector3(-2.6f, 0.5f, 0f), new Vector3(1.4f, 1f, 1.2f), deskMat, root.transform);
            Box("Crate2", back + new Vector3(-2.4f, 1.4f, 0.1f), new Vector3(1.0f, 0.8f, 1.0f), deskMat, root.transform);
            Box("Crate3", back + new Vector3(2.8f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1.3f), deskMat, root.transform);
            Box("Duffel1", back + new Vector3(1.4f, 0.25f, -0.2f), new Vector3(1.2f, 0.5f, 0.55f), mat, root.transform);
            Box("Duffel2", back + new Vector3(-0.8f, 0.2f, -0.4f), new Vector3(0.9f, 0.4f, 0.5f), mat, root.transform);
            var lamp = new GameObject("DealerBulb");
            lamp.transform.SetParent(root.transform);
            lamp.transform.position = c + new Vector3(0f, 3.0f, 5.2f);
            var lt = lamp.AddComponent<Light>();
            lt.type = LightType.Point; lt.color = new Color(1f, 0.45f, 0.2f); lt.range = 9f; lt.intensity = 1.6f;

            // sale zone in front of the counter (also overlaps the counter top)
            var zone = new GameObject("VendorZone");
            zone.transform.SetParent(root.transform);
            zone.transform.position = c + new Vector3(0f, 1.2f, 1f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(8f, 2.4f, 6f);

            var shelf = new GameObject("Shelf").transform;
            shelf.SetParent(root.transform);
            shelf.position = c + new Vector3(0f, 1.2f, 4f);

            var v = zone.AddComponent<Vendor>();
            v.shelf = shelf;
            v.itemMat = mugMat;
            v.npc = npc.transform;
        }

        // ------------------------------------------------------------------ things

        int ModelLoot(GameObject prefab, string label, Vector3 pos, float yaw, int lifters, float weight, int value, float scale)
            => ModelLootObj(prefab, label, pos, yaw, lifters, weight, value, scale).GetComponent<HeavyLoot>().baseValue;

        /// <summary>Names for the test-mode spawn buttons (the loot table, then the Office pack items).</summary>
        public string[] SpawnableLootNames
        {
            get
            {
                var l = new List<string>();
                foreach (var t in LootTable) l.Add(t.name);
                l.Add("Office PC"); l.Add("Office Printer");
                return l.ToArray();
            }
        }

        /// <summary>Test mode: drops one loot item (index into SpawnableLootNames) at a world position.</summary>
        public GameObject SpawnLoot(int index, Vector3 pos, int floor)
        {
            float scale = 1f + 0.12f * Mathf.Max(1, floor);
            var parent = current ? current : transform;
            if (index < LootTable.Length)
            {
                var t = LootTable[index];
                var go = Box(t.name, pos, t.size, lootMat, null, dynamic: true);
                go.transform.SetParent(parent);
                var l = go.AddComponent<HeavyLoot>();
                l.displayName = t.name; l.lifters = t.lifters;
                l.baseValue = Mathf.RoundToInt(t.value * scale / 50f) * 50;
                go.GetComponent<Rigidbody>().mass = t.lifters * l.massPerLifter;
                go.GetComponent<Pickup>().weight = t.weight;
                return go;
            }
            var cat = Office;
            if (!cat) return null;
            if (index == LootTable.Length && cat.pc) return ModelLootObj(cat.pc, "Office PC", pos, 0f, 1, 7f, 900, scale);
            if (index == LootTable.Length + 1 && cat.printer) return ModelLootObj(cat.printer, "Office Printer", pos, 0f, 2, 16f, 800, scale);
            return null;
        }

        GameObject ModelLootObj(GameObject prefab, string label, Vector3 pos, float yaw, int lifters, float weight, int value, float scale)
        {
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), current ? current : transform);
            go.name = label;
            var rb = go.AddComponent<Rigidbody>();
            var l = go.AddComponent<HeavyLoot>();
            l.displayName = label; l.lifters = lifters;
            l.baseValue = Mathf.RoundToInt(value * scale / 50f) * 50;
            rb.mass = lifters * l.massPerLifter;
            go.GetComponent<Pickup>().weight = weight;
            return go;
        }

        void PlaceLoot(System.Random rng, int floor, Vector2Int elevCell, ref FloorInfo info)
        {
            int baseCount = 8 + floor; // floor 1 -> 9 items, top floor -> 18
            float scale = 1f + 0.12f * floor;
            int total = 0, count = 0;
            for (int i = 0; i < baseCount; i++)
            {
                Vector2Int cell;
                do { cell = new Vector2Int(rng.Next(cols), rng.Next(rows)); }
                while (cell == Vector2Int.zero || cell == elevCell);
                var t = LootTable[rng.Next(LootTable.Length)];
                var pos = CellCenter(cell) + new Vector3(Rand(rng, -6f, 6f), t.size.y / 2f + 0.05f, Rand(rng, -6f, 6f));
                var go = Box(t.name, pos, t.size, lootMat, null, dynamic: true);
                go.transform.SetParent(current);
                var l = go.AddComponent<HeavyLoot>();
                l.displayName = t.name; l.lifters = t.lifters;
                l.baseValue = Mathf.RoundToInt(t.value * scale / 50f) * 50;
                go.GetComponent<Rigidbody>().mass = t.lifters * l.massPerLifter;
                go.GetComponent<Pickup>().weight = t.weight;
                total += l.baseValue; count++;
            }

            // Office pack: computers sitting on desks, and printers on the floor
            var cat = Office;
            if (cat && cat.pc)
                foreach (var top in deskTops)
                {
                    if (rng.NextDouble() > 0.5) continue;
                    total += ModelLoot(cat.pc, "Office PC", top + Vector3.up * 0.03f, rng.Next(4) * 90f, 1, 7f, 900, scale);
                    count++;
                }
            if (cat && cat.printer)
            {
                int printers = 1 + floor / 3;
                for (int i = 0; i < printers; i++)
                {
                    Vector2Int cell;
                    do { cell = new Vector2Int(rng.Next(cols), rng.Next(rows)); }
                    while (cell == Vector2Int.zero || cell == elevCell);
                    var pos = CellCenter(cell) + new Vector3(Rand(rng, -6f, 6f), 0.05f, Rand(rng, -6f, 6f));
                    total += ModelLoot(cat.printer, "Office Printer", pos, rng.Next(4) * 90f, 2, 16f, 800, scale);
                    count++;
                }
            }

            info.lootCount = count;
            info.lootValue = total;
            // gentle start: 35% of the loot value on floor 1, rising to ~60% by floor 10
            float quotaShare = Mathf.Lerp(0.35f, 0.6f, Mathf.Clamp01((floor - 1) / 9f));
            info.quota = Mathf.RoundToInt(total * quotaShare / 100f) * 100;
        }

        void PlaceMugs(System.Random rng)
        {
            int n = cols * rows * 2 / 3;
            for (int i = 0; i < n; i++)
            {
                var cell = new Vector2Int(rng.Next(cols), rng.Next(rows));
                var pos = CellCenter(cell) + new Vector3(Rand(rng, -7f, 7f), 0.15f, Rand(rng, -7f, 7f));
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"Mug_{i}"; go.transform.SetParent(current);
                go.transform.position = pos; go.transform.localScale = new Vector3(0.18f, 0.12f, 0.18f);
                go.GetComponent<Renderer>().sharedMaterial = mugMat;
                go.AddComponent<Rigidbody>().mass = 0.4f;
                go.AddComponent<Throwable>();
            }
        }

        int PlaceGuards(System.Random rng, int floor, int[,] dist, Vector2Int elevCell)
        {
            int count = Mathf.Min(maxGuards, baseGuards + floor);
            int placed = 0;
            for (int g = 0; g < count; g++)
            {
                var route = new List<Vector3>();
                int stops = 3 + rng.Next(2);
                for (int s = 0; s < stops; s++)
                {
                    Vector2Int cell;
                    int tries = 0;
                    do { cell = new Vector2Int(rng.Next(cols), rng.Next(rows)); tries++; }
                    while ((dist[cell.x, cell.y] < 2 || cell == elevCell) && tries < 30); // calm spawn, no guards in the elevator room
                    var p = CellCenter(cell) + new Vector3(Rand(rng, -3f, 3f), 0, Rand(rng, -3f, 3f));
                    if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) route.Add(hit.position);
                }
                if (route.Count < 2) continue;
                SpawnGuard($"Guard_{g}", route, g + floor); // a different guard look for each guard (and mix per floor)
                placed++;
            }
            return placed;
        }

        void SpawnGuard(string name, List<Vector3> route, int look)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name; go.transform.SetParent(current);
            go.transform.position = route[0] + Vector3.up * 1f;
            go.GetComponent<Renderer>().sharedMaterial = guardMat;
            var agent = go.AddComponent<NavMeshAgent>();
            agent.height = 2f; agent.radius = 0.5f;
            var ai = go.AddComponent<GuardAI>();
            ai.walkSpeed = guardWalkSpeed;
            ai.investigateSpeed = guardInvestigateSpeed;
            ai.chaseSpeed = guardChaseSpeed;
            go.AddComponent<GuardVisual>().lookIndex = look;
            var pts = new Transform[route.Count];
            for (int i = 0; i < route.Count; i++)
            {
                var t = new GameObject($"{name}_wp{i}").transform;
                t.SetParent(current); t.position = route[i]; pts[i] = t;
            }
            ai.patrolPoints = pts;
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0, 0.6f, 0.45f);
            nose.transform.localScale = new Vector3(0.4f, 0.15f, 0.2f);
            Kill(nose.GetComponent<Collider>());
        }

        // ------------------------------------------------------------------ helpers

        GameObject Box(string name, Vector3 pos, Vector3 size, Material mat, Transform parent = null,
                       bool dynamic = false, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.position = pos; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.SetParent(parent ? parent : current);
            if (!collider) Kill(go.GetComponent<Collider>());
            if (dynamic) go.AddComponent<Rigidbody>();
            return go;
        }

        static void Kill(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);

        static IEnumerable<(T, int)> WithIndex<T>(List<T> l)
        {
            for (int i = 0; i < l.Count; i++) yield return (l[i], i);
        }
    }
}