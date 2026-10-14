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

        [Header("Guards (slow on purpose: easier to coordinate with friends)")]
        public float guardWalkSpeed = 1.5f;
        public float guardInvestigateSpeed = 2.2f;
        public float guardChaseSpeed = 3.6f;
        public int baseGuards = 3;
        public int maxGuards = 12;

        [Header("Materials")]
        public Material floorMat, wallMat, deskMat, elevatorMat, lootMat, mugMat, guardMat;

        static readonly (string name, Vector3 size, int lifters, int value)[] LootTable =
        {
            ("Golden Stapler",   new Vector3(0.7f, 0.4f, 0.4f), 1, 800),
            ("Espresso Machine", new Vector3(0.9f, 0.9f, 0.8f), 2, 1500),
            ("Prototype Server", new Vector3(1.0f, 1.4f, 0.9f), 2, 2500),
            ("Crystal Trophy",   new Vector3(0.5f, 0.9f, 0.5f), 1, 1200),
            ("Executive Safe",   new Vector3(1.1f, 1.1f, 1.1f), 3, 3000),
        };

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

        /// <param name="floor">Level index: 0 = Basement, 1..10 = office floors.</param>
        public FloorInfo Generate(int floor, int seed)
        {
            var rng = new System.Random(seed);
            current = new GameObject($"Floor_{floor}").transform;
            current.SetParent(transform, false);

            // ---- size from the time target
            float roomsF = targetMinutes * 60f * assumedMoveSpeed / (2f * cellSize) + 1f;
            int roomCount = Mathf.Max(6, Mathf.RoundToInt(roomsF));
            rows = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(roomCount / 1.5f)));
            cols = Mathf.Max(2, Mathf.RoundToInt(roomCount / (float)rows));
            int rooms = cols * rows;

            CarveMaze(rng);
            var dist = Distances();
            var elevCell = FarthestCell(dist);
            var info = new FloorInfo
            {
                floor = floor, cols = cols, rows = rows, rooms = rooms,
                spawn = CellCenter(Vector2Int.zero),
                elevator = CellCenter(elevCell),
                estimatedMinutes = 2f * (rooms - 1) * cellSize / assumedMoveSpeed / 60f
            };

            BuildShell();
            var deskSpots = BuildFurniture(rng, elevCell);
            BuildElevatorRoom(elevCell);

            // ---- bake before adding dynamic things
            var surface = current.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            foreach (var (pos, i) in WithIndex(deskSpots)) MakeHideSpot(pos, i);
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
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    var cell = new Vector2Int(c, r);
                    if (cell == Vector2Int.zero || cell == elevCell) continue;
                    if (rng.NextDouble() > 0.55) continue;
                    var pos = CellCenter(cell) + new Vector3(Rand(rng, -5f, 5f), 0, Rand(rng, -5f, 5f));
                    Box("Desk", pos + new Vector3(0, 0.75f, 0), new Vector3(2.4f, 0.15f, 1.2f), deskMat);
                    foreach (var sx in new[] { -1f, 1f })
                        Box("DeskLeg", pos + new Vector3(sx * 1.1f, 0.35f, 0), new Vector3(0.15f, 0.7f, 1.1f), deskMat);
                    spots.Add(pos);
                }
            return spots;
        }

        void MakeHideSpot(Vector3 pos, int i)
        {
            var hs = new GameObject($"HideSpot_{i}");
            hs.transform.SetParent(current); hs.transform.position = pos + new Vector3(0, 0.4f, 0);
            var c = hs.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = new Vector3(2.2f, 0.9f, 1.4f);
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

            // back + side walls, front wall (south) with a door gap
            Box("ShaftWall_N", center + new Vector3(0, wallHeight / 2f, half), new Vector3(half * 2 + t, wallHeight, t), wallMat, room.transform);
            Box("ShaftWall_E", center + new Vector3(half, wallHeight / 2f, 0), new Vector3(t, wallHeight, half * 2), wallMat, room.transform);
            Box("ShaftWall_W", center + new Vector3(-half, wallHeight / 2f, 0), new Vector3(t, wallHeight, half * 2), wallMat, room.transform);
            float segLen = half - doorHalf;
            Box("ShaftWall_S1", center + new Vector3(-(doorHalf + segLen / 2f), wallHeight / 2f, -half), new Vector3(segLen, wallHeight, t), wallMat, room.transform);
            Box("ShaftWall_S2", center + new Vector3(doorHalf + segLen / 2f, wallHeight / 2f, -half), new Vector3(segLen, wallHeight, t), wallMat, room.transform);

            Box("ElevatorFloor", center + new Vector3(0, 0.02f, 0.5f), new Vector3(5, 0.1f, 4.5f), elevatorMat, room.transform, collider: false);

            var zone = new GameObject("DropZone");
            zone.transform.SetParent(room.transform, false);
            zone.transform.localPosition = new Vector3(0, 1.2f, 0.5f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(5, 2.4f, 4.5f);
            zone.AddComponent<ElevatorExtraction>();

            // keep guards out
            var keepOut = new GameObject("GuardKeepOut");
            keepOut.transform.SetParent(room.transform, false);
            var mv = keepOut.AddComponent<NavMeshModifierVolume>();
            mv.center = new Vector3(0, 1f, 0);
            mv.size = new Vector3(half * 2 - 0.2f, 3f, half * 2 - 0.2f);
            mv.area = 1; // built-in "Not Walkable"
        }
        // ------------------------------------------------------------------ things

        void PlaceLoot(System.Random rng, int floor, Vector2Int elevCell, ref FloorInfo info)
        {
            int count = 8 + floor; // basement (0) -> 8 items, top floor -> 18
            float scale = 1f + 0.12f * floor;
            int total = 0;
            for (int i = 0; i < count; i++)
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
                total += l.baseValue;
            }
            info.lootCount = count;
            info.lootValue = total;
            info.quota = Mathf.RoundToInt(total * 0.6f / 100f) * 100;
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
                SpawnGuard($"Guard_{g}", route);
                placed++;
            }
            return placed;
        }

        void SpawnGuard(string name, List<Vector3> route)
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