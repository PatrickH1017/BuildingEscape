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
    public partial class FloorGenerator : MonoBehaviour
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
        [Tooltip("Not used since 2026-10-23: the most patrolling guards is GuardRules.PatrolCap and it rises with the floor.")]
        public int maxGuards = 12;
        [Tooltip("Not used since 2026-10-23: the most guards alive at once, backup included, is GuardRules.MaxGuardsAlive (24).")]
        public int maxGuardsAlive = 24;

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

        [Header("Office pack (Employees Take Revenge > Setup Office Pack)")]
        [Tooltip("Leave empty to load Resources/OfficeCatalog automatically.")]
        public OfficeCatalog office;

        OfficeCatalog Office => office ? office : (office = Resources.Load<OfficeCatalog>("OfficeCatalog"));

        Transform current;
        bool[,] openE, openN;
        int cols, rows;

        public Transform Current => current;

        /// <summary>Destroys the previous floor (call a frame before Generate so its NavMesh is gone).</summary>
        public void Clear()
        {
            if (current) Kill(current.gameObject);
            current = null;
            stairs.Clear();
        }

        /// <param name="floor">Level index: 0 = Basement (safe zone), 1..10 = office floors.</param>
        public FloorInfo Generate(int floor, int seed)
        {
            bool hub = floor <= 0;
            bool boss = TowerRules.IsBoss(floor);
            Net.NetIds.Reset(); // a new floor: ids start again from 0, in creation order (same on every machine)
            if (hub) PvpBoard.Reset();            // (a new basement: a new leaderboard of the PVP room)
            NoteBaseSeed(floor, seed);
            planSeed = seed;
            ResetOutpost();
            stairs.Clear();
            lastEntry = -1;
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
            else if (boss)
            {
                // ---- the boss floor: a small open arena, no maze
                cols = 3; rows = 2;
                rooms = cols * rows;
                OpenAllDoors();
            }
            else
            {
                // ---- size from the time target
                SizeOfFloor(out cols, out rows);
                rooms = cols * rows;
                CarveMaze(rng);     // (the elevator room always opens to both of its neighbours: FloorShape.Maze)
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

            layout = null;
            ResetWallSkin();                      // (the Floor 7 pack walls and ceiling: FloorGenerator.WallSkin.cs)
            dockWalls = hub;
            officeFloor = !hub && !boss;          // the outside walls of an office floor get windows (FloorGenerator.Surfaces.cs)
            outerSegs.Clear();
            BuildShell();
            BuildElevatorRoom(elevCell, floor);

            if (hub)
            {
                BuildLoadingDock(new Vector2Int(cols - 1, rows - 1), seed);
                BuildAfterHours(floor, seed, elevCell, elevCell, true); // ceiling, lights, night look (no NavMesh on the docks, so before is fine)
                return info; // safe zone: no loot, mugs, hide spots, guards (so no NavMesh needed either)
            }

            var guestCell = ShaftCell();      // the guards' own elevator: the corner opposite the players', the same cell on every floor
            if (!boss)
            {
                // corridors, offices, break rooms...: partitions, floors and furniture, all in place before the bake
                try
                {
                    PlanInterior(floor, seed);
                    BuildInterior();
                }
                catch (System.Exception e)
                {
                    // a problem in the furnishing must never stop the floor from loading: fall back to the empty rooms
                    Debug.LogError("[Rooms] Could not furnish floor " + floor + ", so it is left empty. Please report this:\n" + e);
                    layout = null;
                    var unfinished = current.Find("Interior");
                    if (unfinished) { unfinished.gameObject.SetActive(false); Kill(unfinished.gameObject); }
                }
            }
            BuildScenery(floor, seed);        // windows in the outside walls and the city beyond them (cosmetic; it can never stop a floor)
            // the guard outpost keeps random spots and patrol stops out of itself
            if (layout != null && layout.outpost != null) { outpostArea = layout.outpost.outer; hasOutpostArea = true; }
            // where backup arrives (not in the docks: there are no guards there): the guards' own elevator, a car in the middle of the corner cell
            // opposite the players' elevator - in the same place on every floor, like a real shaft. Its doors open onto the cell's corridor.
            BuildGuestElevator(guestCell);
            BuildStairwells();                // the two stairwells in the other corners (office floors only)

            // ---- bake before adding dynamic things
            var surface = current.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            MakeHideZones();
            BuildDoors(); // sliding doors in the room doorways (after the bake: the NavMesh is made with every doorway open)
            OutpostStep("the window, keypad and lock", floor, () => BuildOutpostExtras(floor)); // the one-way window, the keypad, the alarm lamp, the lock (after the bake, once the doors exist)
            BuildAfterHours(floor, seed, elevCell, guestCell, false); // ceiling, light panels, night look (after the bake: a ceiling is not ground)
            if (boss)
            {
                info.lootCount = 0; info.lootValue = 0; info.quota = 0; // the boss floor is won by knocking the boss out
                info.guards = PlaceBoss(dist);
            }
            else
            {
                PlaceLoot(rng, floor, elevCell, ref info);
                PlaceMugs(rng);
                info.guards = PlaceGuards(rng, floor, dist, elevCell);
                if (floor == TowerRules.KeyFloor && firstGuard) MarkKeyHolder(firstGuard);
                bool onDuty = false;
                OutpostStep("the guard on duty in the outpost", floor, () => onDuty = PlaceOutpostGuard(floor));      // (after the others, never uses `rng`)
                if (onDuty) info.guards++;
                OutpostStep("the outpost's things and the safe's cash", floor, () => FillOutpost(floor));   // (what the guards keep in their outpost, and the cash in the floor's safe: never uses `rng`)
                OutpostStep("floor 1's crowbar", floor, () => FillFloorOneCrowbar(floor));              // (the owner, 2026-10-23: one random crowbar on floor 1, outside the outpost; the run seed's numbers only)
                OutpostStep("the hallway loudspeakers", floor, () => BuildSpeakers(floor));
                OutpostStep("the alarm beacons", floor, () => BuildBeacons(floor));          // (the owner, 2026-10-24: red lights that spin in the corners near the ceiling when the Big Alarm goes on; FloorGenerator.Beacons.cs)        // (the owner, 2026-10-18: speakers in the hallways, never uses `rng` either)

            }
            guestDoor?.Snap(false); // the NavMesh was baked with the doors open; now the car is shut
            foreach (var st in stairs) if (st.door != null) st.door.Snap(false);     // (and the stairwells' doors)
            return info;
        }

        // ------------------------------------------------------------------ maze

        /// <summary>The size of a normal floor in cells, from the time target.</summary>
        void SizeOfFloor(out int c, out int r)
        {
            float roomsF = targetMinutes * 60f * assumedMoveSpeed / (2f * cellSize) + 1f;
            int roomCount = Mathf.Max(6, Mathf.RoundToInt(roomsF));
            r = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(roomCount / 1.5f)));
            c = Mathf.Max(2, Mathf.RoundToInt(roomCount / (float)r));
        }

        /// <summary>The floor's maze (see <see cref="FloorShape.Maze"/>; it takes its random numbers from <paramref name="rng"/> in place).</summary>
        void CarveMaze(System.Random rng)
        {
            var shape = FloorShape.Maze(rng, cols, rows, loopChance);
            openE = shape.openE;
            openN = shape.openN;
        }

        FloorShape Shape() => new FloorShape(cols, rows, openE, openN);

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

        bool IsOpen(Vector2Int a, Vector2Int b)
        {
            if (b.x > a.x) return openE[a.x, a.y];
            if (b.x < a.x) return openE[b.x, b.y];
            if (b.y > a.y) return openN[a.x, a.y];
            return openN[b.x, b.y];
        }

        int[,] Distances() => Shape().Distances();

        Vector2Int FarthestCell(int[,] dist)
        {
            Shape().FarthestCell(dist, out int c, out int r);
            return new Vector2Int(c, r);
        }

        /// <summary>The cell of the guards' own elevator: the corner opposite the players' (see <see cref="FloorShape.ShaftCell"/>).</summary>
        Vector2Int ShaftCell()
        {
            Shape().ShaftCell(out int c, out int r);
            return new Vector2Int(c, r);
        }

        /// <summary>Is there a guards' elevator on the floor on show, and where is the middle of its car (the same corner cell on every floor)?</summary>
        public bool HasGuardShaft => guestDoor != null;
        public Vector3 GuardShaftCentre => guestSpawn;
        /// <summary>The way the guards' elevator door faces (a flat unit step along x or z).</summary>
        public Vector3 GuardShaftOut => guestOut;

        /// <summary>The middle of the players' elevator car: the same corner cell (0, 0) on every floor, like a real shaft (the security computer's Nav map marks it).</summary>
        public Vector3 PlayerElevatorCentre => CellCenter(Vector2Int.zero);

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
                    if (solid)
                    {
                        var wall = WallZ(x, z0 + cellSize / 2f, cellSize);
                        if (c == 0 || c == cols) OuterWall(wall, false, x, z0, z0 + cellSize);
                    }
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
                    if (dockWalls && r == rows && c == cols - 1) continue; // the garage-door wall: see FloorGenerator.Dock.cs
                    bool solid = r == 0 || r == rows || !openN[c, r - 1];
                    float z = r * cellSize, x0 = c * cellSize;
                    if (dockWalls && r == rows && c == 0)
                    {
                        // the PVP room goes out through this wall (FloorGenerator.DockRooms.cs): the wall starts east of it
                        WallX((PvpOpeningTo + x0 + cellSize) * 0.5f, z, x0 + cellSize - PvpOpeningTo);
                        continue;
                    }
                    if (solid)
                    {
                        var wall = WallX(x0 + cellSize / 2f, z, cellSize);
                        if (r == 0 || r == rows) OuterWall(wall, true, z, x0, x0 + cellSize);
                    }
                    else
                    {
                        WallX(x0 + half / 2f, z, half);
                        WallX(x0 + cellSize - half / 2f, z, half);
                    }
                }
        }

        GameObject WallZ(float x, float z, float len)
        {
            var go = Box("Wall", new Vector3(x, wallHeight / 2f, z), new Vector3(0.4f, wallHeight, len), wallMat);
            shellBoxes.Add(go);
            return go;
        }
        GameObject WallX(float x, float z, float len)
        {
            var go = Box("Wall", new Vector3(x, wallHeight / 2f, z), new Vector3(len, wallHeight, 0.4f), wallMat);
            shellBoxes.Add(go);
            return go;
        }

        /// <summary>
        /// The service elevator: a private walled room in the corner cell with one front door (east, or north if
        /// east is closed). Its interior is Not Walkable on the NavMesh so guards never wander in. A floor panel and a
        /// GO button sit on the wall by the door (F to use it, click a floor, click GO). On floor 12 the opposite wall is a locked back
        /// door to a hallway with the hidden elevator to the boss floor.
        /// </summary>
        void BuildElevatorRoom(Vector2Int cell, int floor)
        {
            var center = CellCenter(cell);
            const float half = 4f, doorHalf = 1.5f, t = 0.4f;
            var room = new GameObject("ElevatorRoom");
            room.transform.SetParent(current); room.transform.position = center;
            bool hasBack = floor == TowerRules.KeyFloor;

            bool doorEast = cols > 1 && IsOpen(cell, cell + Vector2Int.right);
            bool doorNorth = !doorEast && rows > 1 && IsOpen(cell, cell + Vector2Int.up); // one front door only
            if (!doorEast && !doorNorth) doorEast = true;
            bool backWest = hasBack && doorEast;
            bool backSouth = hasBack && doorNorth;
            float segLen = half - doorHalf;

            // north (+z) and south (-z) walls
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? half : -half;
                bool hasDoor = (doorNorth && side == 0) || (backSouth && side == 1);
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
                bool hasDoor = (doorEast && side == 0) || (backWest && side == 1);
                if (!hasDoor) Box(side == 0 ? "ShaftWall_E" : "ShaftWall_W", center + new Vector3(x, wallHeight / 2f, 0), new Vector3(t, wallHeight, half * 2), wallMat, room.transform);
                else
                {
                    Box("ShaftWall_E1", center + new Vector3(x, wallHeight / 2f, -(doorHalf + segLen / 2f)), new Vector3(t, wallHeight, segLen), wallMat, room.transform);
                    Box("ShaftWall_E2", center + new Vector3(x, wallHeight / 2f, doorHalf + segLen / 2f), new Vector3(t, wallHeight, segLen), wallMat, room.transform);
                }
            }

            // sliding door panels that fill each gap
            var door = room.AddComponent<ElevatorDoor>();
            bool look = UseCarLook;       // wood, steel, carpet and a mirror inside (FloorGenerator.ElevatorInterior.cs); the doors are stainless steel
            var dMat = look ? CarMaterial(ElevatorLook.Steel) : elevatorMat ? elevatorMat : wallMat;
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

            if (!look) Box("ElevatorFloor", center + new Vector3(0, 0.02f, 0.5f), new Vector3(5, 0.1f, 4.5f), elevatorMat, room.transform, collider: false);

            // the floor panel (a lit button for every floor) and its GO button on the wall by the front door,
            // in the corner where the player has room to stand in front of it
            Vector3 pPos, pView; int goSide;
            if (doorEast) { pPos = center + new Vector3(ElevatorLook.PanelAlong, 1.7f, half - t / 2f); pView = Vector3.forward; goSide = -1; }
            else { pPos = center + new Vector3(half - t / 2f, 1.7f, ElevatorLook.PanelAlong); pView = Vector3.right; goSide = 1; }
            var panel = ElevatorPanel.Build(room.transform, pPos, pView, goSide);
            if (look) BuildElevatorInterior(room.transform, center, doorEast, hasBack ? (int)(backWest ? ElevatorLook.Side.West : ElevatorLook.Side.South) : -1, floor);

            // the whole inside of the car counts as "in the elevator" (loot dropped anywhere in it banks; everyone must be in it to go)
            var zone = new GameObject("DropZone");
            zone.transform.SetParent(room.transform, false);
            zone.transform.localPosition = new Vector3(0, 1.2f, 0);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(7.2f, 2.4f, 7.2f);
            var car = zone.AddComponent<ElevatorExtraction>();
            car.door = door;
            car.panelPosition = panel.transform.position;
            car.doorDir = doorEast ? Vector2.right : Vector2.up;                              // (what is let go of in the car stays in it: ElevatorExtraction.FixedUpdate, 2026-10-22)
            car.backDoorDir = backWest ? Vector2.left : backSouth ? Vector2.down : Vector2.zero;
            car.innerHalf = half - t / 2f;

            // keep guards out of the car
            AddKeepOut(room.transform, Vector3.zero, new Vector3(half * 2 - 0.2f, 3f, half * 2 - 0.2f));

            if (hasBack) BuildHiddenHallway(cell, center, room.transform, half, doorHalf, t, backWest, car);
        }

        /// <summary>Floor 12: the locked back door, the private hallway behind it and the hidden elevator to the boss.</summary>
        void BuildHiddenHallway(Vector2Int cell, Vector3 center, Transform room, float half, float doorHalf, float t, bool west, ElevatorExtraction car)
        {
            var origin = new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);
            var dMat = UseCarLook ? CarMaterial(ElevatorLook.Steel) : elevatorMat ? elevatorMat : wallMat;
            float dt = t * 0.8f, pw = doorHalf;

            var backGo = new GameObject("BackDoor");
            backGo.transform.SetParent(room, false);
            var back = backGo.AddComponent<ElevatorDoor>();
            Vector3 doorPos, hallCenter, hallSize;
            if (west)
            {
                doorPos = center + new Vector3(-half, 0f, 0f);
                var pa = Box("BackPanel_A", doorPos + new Vector3(0, wallHeight / 2f, -pw / 2f), new Vector3(dt, wallHeight, pw), dMat, backGo.transform);
                var pb = Box("BackPanel_B", doorPos + new Vector3(0, wallHeight / 2f, pw / 2f), new Vector3(dt, wallHeight, pw), dMat, backGo.transform);
                back.Setup(pa.transform, pb.transform, new Vector3(0, 0, -pw), new Vector3(0, 0, pw));
                // hallway between the perimeter wall and the car: partition it off from the rest of the cell
                float x0 = origin.x, x1 = center.x - half;
                WallX((x0 + x1) / 2f, center.z + half, x1 - x0);
                WallX((x0 + x1) / 2f, center.z - half, x1 - x0);
                hallCenter = new Vector3((x0 + x1) / 2f, 1.2f, center.z);
                hallSize = new Vector3(x1 - x0 - 0.6f, 2.4f, half * 2 - 0.6f);
            }
            else
            {
                doorPos = center + new Vector3(0f, 0f, -half);
                var pa = Box("BackPanel_A", doorPos + new Vector3(-pw / 2f, wallHeight / 2f, 0), new Vector3(pw, wallHeight, dt), dMat, backGo.transform);
                var pb = Box("BackPanel_B", doorPos + new Vector3(pw / 2f, wallHeight / 2f, 0), new Vector3(pw, wallHeight, dt), dMat, backGo.transform);
                back.Setup(pa.transform, pb.transform, new Vector3(-pw, 0, 0), new Vector3(pw, 0, 0));
                float z0 = origin.z, z1 = center.z - half;
                WallZ(center.x + half, (z0 + z1) / 2f, z1 - z0);
                WallZ(center.x - half, (z0 + z1) / 2f, z1 - z0);
                hallCenter = new Vector3(center.x, 1.2f, (z0 + z1) / 2f);
                hallSize = new Vector3(half * 2 - 0.6f, 2.4f, z1 - z0 - 0.6f);
            }
            back.Snap(false); // locked: shut until the key is used
            car.backDoor = back;
            car.backDoorPosition = doorPos + Vector3.up;

            // the hidden elevator: the whole hallway is the car (everyone inside, then G)
            var hall = new GameObject("HiddenElevator");
            hall.transform.SetParent(room.parent, false);
            hall.transform.position = hallCenter;
            var hbc = hall.AddComponent<BoxCollider>(); hbc.isTrigger = true; hbc.size = hallSize;
            hall.AddComponent<BossElevator>();
            Box("HiddenElevatorFloor", new Vector3(hallCenter.x, 0.03f, hallCenter.z), new Vector3(hallSize.x, 0.1f, hallSize.z), vendorMat ? vendorMat : lootMat, hall.transform, collider: false);
            AddKeepOut(room.parent, hallCenter - room.parent.position + new Vector3(0, -0.2f, 0), hallSize + new Vector3(0.6f, 1f, 0.6f));
        }

        void AddKeepOut(Transform parent, Vector3 localCenter, Vector3 size)
        {
            var keepOut = new GameObject("GuardKeepOut");
            keepOut.transform.SetParent(parent, false);
            var mv = keepOut.AddComponent<NavMeshModifierVolume>();
            mv.center = localCenter + new Vector3(0, 1f, 0);
            mv.size = size;
            mv.area = 1; // built-in "Not Walkable"
        }

        // ------------------------------------------------------------------ guest elevator (backup)

        /// <summary>
        /// The guards' own elevator: backup guards arrive in it, its doors open and they walk out. A small walled car in the
        /// corner cell opposite the service elevator - the same cell on every floor, like a real shaft - with one door facing an open neighbour.
        /// </summary>
        void BuildGuestElevator(Vector2Int cell)
        {
            var center = CellCenter(cell);
            const float h = 2.6f, t = 0.4f, doorHalf = 1.4f;
            var dirs = new[] { Vector2Int.left, Vector2Int.down, Vector2Int.right, Vector2Int.up };
            var dir = GuestDoorDir(cell);

            var room = new GameObject("GuestElevator");
            room.transform.SetParent(current); room.transform.position = center;
            var mat = guardMat ? guardMat : wallMat;
            float seg = h - doorHalf;

            // four walls; the one facing `dir` has a gap with two sliding panels
            var door = room.AddComponent<ElevatorDoor>();
            door.chimeOnOpen = true;   // the guest elevator rings when it opens for the backup guards
            for (int i = 0; i < 4; i++)
            {
                var d = dirs[i];
                bool along = d.x != 0; // wall runs along z when the wall is east/west
                Vector3 wallPos = new Vector3(d.x * h, wallHeight / 2f, d.y * h);
                if (d != dir)
                {
                    Box("GuestWall", center + wallPos, along ? new Vector3(t, wallHeight, h * 2 + t) : new Vector3(h * 2 + t, wallHeight, t), wallMat, room.transform);
                    continue;
                }
                Vector3 axis = along ? Vector3.forward : Vector3.right; // direction the wall runs
                Vector3 sizeSeg = along ? new Vector3(t, wallHeight, seg) : new Vector3(seg, wallHeight, t);
                Box("GuestWall_A", center + wallPos - axis * (doorHalf + seg / 2f), sizeSeg, wallMat, room.transform);
                Box("GuestWall_B", center + wallPos + axis * (doorHalf + seg / 2f), sizeSeg, wallMat, room.transform);
                Vector3 pSize = along ? new Vector3(t * 0.8f, wallHeight, doorHalf) : new Vector3(doorHalf, wallHeight, t * 0.8f);
                var pa = Box("GuestPanel_A", center + wallPos - axis * (doorHalf / 2f), pSize, mat, room.transform);
                var pb = Box("GuestPanel_B", center + wallPos + axis * (doorHalf / 2f), pSize, mat, room.transform);
                door.Setup(pa.transform, pb.transform, -axis * doorHalf, axis * doorHalf);
                guestSide = axis;
                guestOut = new Vector3(dir.x, 0f, dir.y);
            }
            Box("GuestFloor", center + new Vector3(0, 0.03f, 0), new Vector3(h * 2 - 0.4f, 0.1f, h * 2 - 0.4f), mat, room.transform, collider: false);
            guestDoor = door;
            guestSpawn = center;
            GuestDoorOpen = false;
        }

        /// <summary>Opens or closes the guest elevator''s doors (host). The state reaches clients through the run state.</summary>
        void SetGuestDoor(bool open)
        {
            GuestDoorOpen = open;
            if (guestDoor) guestDoor.SetOpen(open);
        }

        /// <summary>Client: mirror the host''s guest elevator door.</summary>
        public void NetGuestDoor(bool open)
        {
            if (open == GuestDoorOpen) return;
            GuestDoorOpen = open;
            if (guestDoor) guestDoor.SetOpen(open);
        }

        System.Collections.IEnumerator CloseGuestDoorLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            SetGuestDoor(false);
            guestCloseRoutine = null;
        }

        // ------------------------------------------------------------------ the stairwells (backup comes out of these too)

        /// <summary>One stairwell: a small walled landing in a corner cell, with a steel double door onto the cell's corridor.</summary>
        sealed class Stairwell
        {
            public ElevatorDoor door;
            public Vector3 centre, side, outDir;
            public bool open;
            public Coroutine closing;
        }

        readonly List<Stairwell> stairs = new List<Stairwell>();
        /// <summary>How many stairwells the floor on show has (0 on the docks and the boss floor, 2 on an office floor), where each one's landing is, which way its door faces and whether it is open.</summary>
        public int StairCount => stairs.Count;
        public Vector3 StairCentre(int i) => stairs[i].centre;
        public Vector3 StairOut(int i) => stairs[i].outDir;
        public bool StairDoorOpen(int i) => stairs[i].open;
        /// <summary>The next-to-last door a backup call used (0 = the guards' elevator, 1 and 2 = the stairwells); the next call tries another one first.</summary>
        int lastEntry = -1;

        /// <summary>
        /// Which doors are open, as one byte for the network: bit 0 the guards' elevator, bit 1 the first stairwell, bit 2 the second. The host
        /// publishes it, clients apply it (<see cref="NetEntryDoors"/>).
        /// </summary>
        public byte EntryDoorMask
        {
            get
            {
                int m = GuestDoorOpen ? 1 : 0;
                for (int i = 0; i < stairs.Count && i < 2; i++) if (stairs[i].open) m |= 2 << i;
                return (byte)m;
            }
        }

        /// <summary>Client: mirror the host's doors (the guards' elevator and the two stairwells).</summary>
        public void NetEntryDoors(byte mask)
        {
            NetGuestDoor((mask & 1) != 0);
            for (int i = 0; i < stairs.Count && i < 2; i++) SetStairDoor(i, (mask & (2 << i)) != 0);
        }

        void SetStairDoor(int i, bool open)
        {
            var st = stairs[i];
            if (st.open == open) return;
            st.open = open;
            if (st.door) st.door.SetOpen(open);
        }

        System.Collections.IEnumerator CloseStairLater(int i, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (i < stairs.Count) { SetStairDoor(i, false); stairs[i].closing = null; }
        }

        /// <summary>The stairwells of the floor that was just planned (none on the boss floor or without a plan).</summary>
        void BuildStairwells()
        {
            stairs.Clear();
            if (layout == null || layout.input == null || layout.input.stairC == null) return;
            var inp = layout.input;
            for (int i = 0; i < inp.stairC.Length; i++)
                BuildStairwell(new Vector2Int(inp.stairC[i], inp.stairR[i]), new Vector2Int(inp.stairDirX[i], inp.stairDirY[i]), i);
        }

        /// <summary>
        /// A stairwell: four walls round a 4.2 m landing (the one facing <paramref name="dir"/> has a 2 m gap with two sliding steel panels),
        /// a dark floor, three steps up along the back wall, a red exit lamp and, beside the door, the word STAIRS. Like the guards' car it is
        /// built before the NavMesh bake (the doors start open, so the bake sees the doorway) and shut afterwards; its steps and signs have no colliders.
        /// </summary>
        void BuildStairwell(Vector2Int cell, Vector2Int dir, int index)
        {
            var center = CellCenter(cell);
            const float h = OfficePlanner.StairHalf - 0.15f, t = 0.3f, doorHalf = 1.0f;     // wall centre line, wall thickness, half the gap
            var dirs = new[] { Vector2Int.left, Vector2Int.down, Vector2Int.right, Vector2Int.up };
            var room = new GameObject("Stairwell_" + index);
            room.transform.SetParent(current); room.transform.position = center;
            var steel = OMat("door");
            var door = room.AddComponent<ElevatorDoor>();
            door.chimeOnOpen = false;                       // (the elevator rings; a stairwell door just thuds)
            var st = new Stairwell { door = door, centre = center, outDir = new Vector3(dir.x, 0f, dir.y) };
            bool doorAlongX = dir.x != 0;                   // the door's wall is an east / west wall (it runs along z)
            Vector3 axis = doorAlongX ? Vector3.forward : Vector3.right;
            st.side = axis;
            float seg = h - doorHalf;
            for (int i = 0; i < 4; i++)
            {
                var d = dirs[i];
                bool along = d.x != 0;
                Vector3 wallPos = new Vector3(d.x * h, wallHeight / 2f, d.y * h);
                if (d != dir)
                {
                    Box("StairWall", center + wallPos, along ? new Vector3(t, wallHeight, h * 2 + t) : new Vector3(h * 2 + t, wallHeight, t), wallMat, room.transform);
                    continue;
                }
                Vector3 sizeSeg = along ? new Vector3(t, wallHeight, seg) : new Vector3(seg, wallHeight, t);
                Box("StairWall_A", center + wallPos - axis * (doorHalf + seg / 2f), sizeSeg, wallMat, room.transform);
                Box("StairWall_B", center + wallPos + axis * (doorHalf + seg / 2f), sizeSeg, wallMat, room.transform);
                Vector3 pSize = along ? new Vector3(t * 0.8f, wallHeight, doorHalf) : new Vector3(doorHalf, wallHeight, t * 0.8f);
                var pa = Box("StairDoor_A", center + wallPos - axis * (doorHalf / 2f), pSize, steel, room.transform);
                var pb = Box("StairDoor_B", center + wallPos + axis * (doorHalf / 2f), pSize, steel, room.transform);
                door.Setup(pa.transform, pb.transform, -axis * doorHalf, axis * doorHalf);
                // (the STAIRS sign on the outside of the wall segment on the left of the door is the pack's own now: OfficePlanner.StairKit; the lamp inside on the right stays)
                Box("StairExitLamp", center + wallPos - st.outDir * (t * 0.5f + 0.03f) + axis * (doorHalf + 0.35f) + Vector3.up * (2.6f - wallHeight / 2f),
                               along ? new Vector3(0.05f, 0.16f, 0.5f) : new Vector3(0.5f, 0.16f, 0.05f), OMat("led"), room.transform, collider: false);
            }
            // the inside: a floor and three steps going up along the back wall (no colliders: nobody is in their way)
            Box("StairFloor", center + new Vector3(0, 0.03f, 0), new Vector3(h * 2 - 0.3f, 0.1f, h * 2 - 0.3f), OMat("dark"), room.transform, collider: false);
            var back = -st.outDir;
            for (int k = 0; k < 3; k++)
            {
                float height = 0.17f * (k + 1), depth = 0.5f, width = h * 2 - 0.5f;
                var pos = center + back * (h - t * 0.5f - 0.25f - depth * (2 - k) - 0.05f) + Vector3.up * (height * 0.5f);
                Box("StairStep", pos, doorAlongX ? new Vector3(depth, height, width) : new Vector3(width, height, depth), steel, room.transform, collider: false);
            }
            stairs.Add(st);
        }

        // ------------------------------------------------------------------ key, boss

        /// <summary>Marks a guard as the key holder (every machine does this: the floor is generated identically).</summary>
        void MarkKeyHolder(GuardAI ai)
        {
            ai.carriesKey = true;
            var icon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            icon.name = "KeyIcon";
            icon.transform.SetParent(ai.transform, false);
            icon.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            icon.transform.localScale = new Vector3(0.25f, 0.12f, 0.35f);
            icon.GetComponent<Renderer>().sharedMaterial = lootMat;
            Kill(icon.GetComponent<Collider>());
        }

        /// <summary>Host / offline: the key holder is down; the key drops where he fell.</summary>
        public void DropKey(Vector3 pos)
        {
            var go = SpawnKeyAt(pos);
            if (go && go.TryGetComponent(out Pickup pk)) Net.RunSync.Instance?.BroadcastSpawnKey(pk.NetId, pos);
        }

        /// <summary>Client: the host dropped the key.</summary>
        public void SpawnKeyFromNet(int id, Vector3 pos)
        {
            Net.NetIds.PendingId = id;
            SpawnKeyAt(pos);
            Net.NetIds.PendingId = Net.NetIds.None;
        }

        GameObject SpawnKeyAt(Vector3 pos)
        {
            var go = Box("Elevator Key", pos, new Vector3(0.25f, 0.1f, 0.5f), lootMat, current ? current : transform, dynamic: true);
            go.GetComponent<Rigidbody>().mass = 0.3f;
            var pk = go.AddComponent<Pickup>();
            pk.weight = 0.3f; pk.label = "Elevator Key"; pk.bankValue = 0;
            go.AddComponent<ElevatorKey>();
            return go;
        }

        /// <summary>The placeholder boss: one big, tough guard in the far room. Knocking it out wins the run.</summary>
        int PlaceBoss(int[,] dist)
        {
            var cell = FarthestCell(dist);
            var p = CellCenter(cell);
            if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas)) p = hit.position;
            var ai = SpawnGuard("Boss", new List<Vector3> { p, p + new Vector3(3f, 0f, 3f) }, 1);
            ai.IsBoss = true;
            ai.hitsToKill = 30;
            ai.transform.localScale = Vector3.one * 1.35f;
            return 1;
        }


        // ------------------------------------------------------------------ things

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

        void PlaceLoot(System.Random rng, int floor, Vector2Int elevCell, ref FloorInfo info)
        {
            float scale = 1f + 0.12f * floor;
            int total = 0, count = 0;
            if (layout != null)
            {
                // treasure spots and stealable furniture from the room plan
                PlaceLootFromLayout(rng, floor, scale, ref total, ref count);
                PlaceValuables(floor, ref total, ref count);          // watches, phones, wallets...: on the desks (never uses `rng`)
            }
            else
            {
                // no room plan (rooms switched off): the big treasures lie about in random rooms
                int baseCount = 8 + floor; // floor 1 -> 9 items, top floor -> 18
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
                    EnsurePickup(go).weight = t.weight;
                    total += l.baseValue; count++;
                }
            }

            info.lootCount = count;
            info.lootValue = total;
            // Floor 1 asks for $4,300 and the quota climbs from there (TowerRules.QuotaFor), but never more than 60% of
            // what is actually lying on the floor, so it can always be met.
            int quotaCap = Mathf.RoundToInt(total * 0.6f / 100f) * 100;
            info.quota = Mathf.Max(100, Mathf.Min(TowerRules.QuotaFor(floor), quotaCap));
        }

        void PlaceMugs(System.Random rng)
        {
            if (layout != null) { PlaceMugsOnSurfaces(rng); return; }
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
            int count = GuardRules.PatrolCount(floor, baseGuards);          // (rises with the floor to GuardRules.PatrolCap: the owner, 2026-10-23)
            int placed = 0;
            var plan = ArmedGuards.PlanFor(floor, count, new System.Random(unchecked(planSeed * 7919 + floor * 31 + 5)));      // (who carries what: its own random numbers, so the maze and the loot stay what they were)
            var spots = GuardSpots();
            // a patrol stop must be walkable from the elevator lobby: a spot behind something that blocks the NavMesh (the back of a small server room with a rack across its aisle) would
            // leave a guard standing in a pocket of floor for ever (found by the loot test: one guard reached 1 of 147 items)
            bool haveRef = false; Vector3 reference = Vector3.zero;
            if (layout != null)
            {
                RoomInfo lobby = null;
                foreach (var rm in layout.rooms) if (rm.theme == "lobby") { lobby = rm; break; }
                if (lobby != null && NavMesh.SamplePosition(new Vector3(lobby.wx, 0f, lobby.wz), out var lh, 4f, NavMesh.AllAreas)) { haveRef = true; reference = lh.position; }
            }
            var walkCheck = new NavMeshPath();
            // where the guards start and patrol: from the run's guard seed when there is one (a restarted run puts them elsewhere - the owner, 2026-10-22: "I dont want players to be able to memorize guard spawns"), else from the floor's own numbers
            // (nothing after the guards uses `rng`, so the maze and the loot, which came first, stay what they were)
            var routeRng = guardSeed != 0 ? new System.Random(GuardRules.RouteSeed(guardSeed, floor)) : rng;
            for (int g = 0; g < count; g++)
            {
                var route = new List<Vector3>();
                int stops = 3 + routeRng.Next(2);
                for (int s = 0; s < stops; s++)
                {
                    Vector2Int cell;
                    int tries = 0;
                    do { cell = new Vector2Int(routeRng.Next(cols), routeRng.Next(rows)); tries++; }
                    while ((dist[cell.x, cell.y] < 2 || cell == elevCell) && tries < 30); // calm spawn, no guards in the elevator room
                    // a walkable spot in one of that cell's rooms or corridors (or near its middle if there is no plan)
                    var list = spots[cell.x, cell.y];
                    var p = list != null && list.Count > 0
                        ? list[routeRng.Next(list.Count)]
                        : CellCenter(cell) + new Vector3(Rand(routeRng, -3f, 3f), 0, Rand(routeRng, -3f, 3f));
                    if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas))
                    {
                        bool connected = !haveRef || (NavMesh.CalculatePath(reference, hit.position, NavMesh.AllAreas, walkCheck) && walkCheck.status == NavMeshPathStatus.PathComplete);
                        if (connected) route.Add(hit.position);
                    }
                }
                if (route.Count < 2) continue;
                var made = SpawnGuard($"Guard_{g}", route, g + floor, plan[g]); // a different guard look for each guard (and mix per floor)
                if (placed == 0) firstGuard = made;
                placed++;
            }
            return placed;
        }

        int planSeed;
        /// <summary>The run's seed for where the guards start and patrol (<see cref="TowerManager.GuardSeed"/>), set before each floor is built. 0 (the editor tools): from the floor's own random numbers, as before.</summary>
        [System.NonSerialized] public int guardSeed;
        int FloorNow => HeistManager.Instance ? HeistManager.Instance.Floor : 0;

        /// <param name="loadout">What he carries and wears (<see cref="GuardLoadouts"/>); the default is nothing (a remote client's guard is told by the host).</param>
        GuardAI SpawnGuard(string name, List<Vector3> route, int look, GuardLoadout loadout = default)
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
            ai.SetLoadout(loadout);
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
            return ai;
        }

        int reinforcementCount;
        GuardAI firstGuard;

        // ---- guest elevator: backup arrives in it
        ElevatorDoor guestDoor;
        Vector3 guestSpawn, guestSide, guestOut;
        Coroutine guestCloseRoutine;
        /// <summary>True while the guest elevator's doors are open (replicated to clients).</summary>
        public bool GuestDoorOpen { get; private set; }

        /// <summary>A random point on the NavMesh somewhere on the current floor (not in the elevator room).</summary>
        Vector3 RandomWalkable()
        {
            for (int i = 0; i < 20; i++)
            {
                var cell = new Vector2Int(Random.Range(0, cols), Random.Range(0, rows));
                if (cell == Vector2Int.zero) continue;
                var p = CellCenter(cell) + new Vector3(Random.Range(-7f, 7f), 0f, Random.Range(-7f, 7f));
                if (NavMesh.SamplePosition(p, out var hit, 6f, NavMesh.AllAreas) && !InOutpost(hit.position) && !InGuardShaft(hit.position)) return hit.position;     // (not inside the guard outpost or the guards' elevator)
            }
            // (nothing found in 20 tries: the clear run in front of the guards' elevator door)
            return guestDoor != null ? guestSpawn + guestOut * 4.2f : CellCenter(new Vector2Int(cols - 1, rows - 1));
        }

        /// <summary>Is p inside the guards' elevator (the car and its walls)? Nobody wanders into a shut car.</summary>
        bool InGuardShaft(Vector3 p)
        {
            if (guestDoor != null && Mathf.Abs(p.x - guestSpawn.x) < 2.8f && Mathf.Abs(p.z - guestSpawn.z) < 2.8f) return true;
            foreach (var st in stairs)
                if (st.door != null && Mathf.Abs(p.x - st.centre.x) < OfficePlanner.StairHalf && Mathf.Abs(p.z - st.centre.z) < OfficePlanner.StairHalf) return true;
            return false;
        }

        /// <summary>A door backup can come out of: 0 = the guards' elevator, 1 and 2 = the stairwells.</summary>
        sealed class GuardEntry { public int index; public Vector3 centre, side, outDir; }

        /// <summary>Doors at least this far (m) from every free player come first: standing at one door must not stop the backup.</summary>
        const float EntrySafeDistance = 12f;

        List<GuardEntry> GuardEntries()
        {
            var l = new List<GuardEntry>();
            if (guestDoor != null) l.Add(new GuardEntry { index = 0, centre = guestSpawn, side = guestSide, outDir = guestOut });
            for (int i = 0; i < stairs.Count; i++)
                if (stairs[i].door != null) l.Add(new GuardEntry { index = i + 1, centre = stairs[i].centre, side = stairs[i].side, outDir = stairs[i].outDir });
            return l;
        }

        /// <summary>The flat distance from a point to the nearest player who is free (a pinned or busted one does not count); a huge number if there is none.</summary>
        static float NearestFreePlayer(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var pl in WobblyPlayerRegistry.All)
            {
                if (!pl || pl.Custody != CustodyState.Free) continue;
                var d = pl.transform.position - p; d.y = 0f;
                best = Mathf.Min(best, d.magnitude);
            }
            return best;
        }

        /// <summary>
        /// The doors in the order a backup call uses them: the doors more than <see cref="EntrySafeDistance"/> from every free player first, in a
        /// random order, the door the last call began with at the back of them; then the doors that are closer, the farthest first.
        /// </summary>
        List<GuardEntry> OrderEntries(List<GuardEntry> all)
        {
            var far = new List<GuardEntry>(); var near = new List<GuardEntry>();
            foreach (var e in all) (NearestFreePlayer(e.centre) >= EntrySafeDistance ? far : near).Add(e);
            for (int i = far.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var tmp = far[i]; far[i] = far[j]; far[j] = tmp; }
            if (far.Count > 1 && far[0].index == lastEntry) { var f = far[0]; far.RemoveAt(0); far.Add(f); }
            near.Sort((a, b) => NearestFreePlayer(b.centre).CompareTo(NearestFreePlayer(a.centre)));
            far.AddRange(near);
            return far;
        }

        void SetEntryDoor(int index, bool open)
        {
            if (index == 0) SetGuestDoor(open);
            else if (index - 1 < stairs.Count) SetStairDoor(index - 1, open);
        }

        void CloseEntryLater(int index, float seconds)
        {
            if (index == 0)
            {
                if (guestCloseRoutine != null) StopCoroutine(guestCloseRoutine);
                guestCloseRoutine = StartCoroutine(CloseGuestDoorLater(seconds));
            }
            else if (index - 1 < stairs.Count)
            {
                var st = stairs[index - 1];
                if (st.closing != null) StopCoroutine(st.closing);
                st.closing = StartCoroutine(CloseStairLater(index - 1, seconds));
            }
        }

        /// <summary>
        /// Backup: the guards come out of the guards' elevator and the two stairwells, never the outpost. Each call orders the doors
        /// (<see cref="OrderEntries"/>: away from the players first) and gives the guards the doors one after the other, so two guards come out of
        /// two different doors when there are two to choose from; the door opens with its sound, they walk out and wander. Returns how many were spawned.
        /// </summary>
        public int SpawnReinforcements(Vector3 target, int n)
        {
            if (!current) return 0;
            var entries = GuardEntries();
            if (entries.Count == 0) return SpawnReinforcementsAnywhere(target, n);

            var order = OrderEntries(entries);
            lastEntry = order[0].index;
            var perDoor = new Dictionary<int, int>();
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                var e = order[i % order.Count];
                perDoor.TryGetValue(e.index, out int k);
                perDoor[e.index] = k + 1;
                SetEntryDoor(e.index, true);
                var p = e.centre + e.side * ((k % 2 == 0 ? 0.8f : -0.8f)) - e.outDir * (0.6f * (k / 2));
                if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
                int look = Random.Range(0, 100);
                var ai = SpawnGuard($"Backup_{reinforcementCount++}", new List<Vector3> { p }, look, ArmedGuards.RollFor(FloorNow));
                ai.roam = RandomWalkable;   // after reaching the spot, wander freely again
                ai.IsBackup = true;         // (a backup officer notices a player in his cone much faster: GuardRules.BackupSpotFactor)
                ai.Dispatch(target);
                Net.RunSync.Instance?.BroadcastSpawnGuard(ai.NetId, p, look, target);
                made++;
            }
            foreach (var kv in perDoor) CloseEntryLater(kv.Key, 7f);
            return made;
        }

        int replacementCount;

        /// <summary>
        /// A guard was eliminated 15 seconds ago (<see cref="GuardRules.ReplacementSeconds"/>; the owner, 2026-10-23: "If a guard is eliminated after 15 seconds a new guard should come from a stairwell"): one new guard comes out of one of the stairwell doors -
        /// the one farthest from the players (<see cref="OrderEntries"/>), never the guards' elevator and never the outpost - and goes about like the other guards: a normal guard, not a backup officer (he does not spot faster). Host / offline; the clients
        /// show him as they show backup. True when he was made.
        /// </summary>
        public bool SpawnReplacement()
        {
            if (!current) return false;
            var doors = GuardEntries().FindAll(e => e.index >= 1);          // (index 0 is the guards' elevator)
            if (doors.Count == 0) return false;
            var door = OrderEntries(doors)[0];
            SetEntryDoor(door.index, true);
            var p = door.centre + door.side * 0.8f;
            if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas)) p = hit.position;
            int look = Random.Range(0, 100);
            var ai = SpawnGuard($"Replacement_{replacementCount++}", new List<Vector3> { p }, look, ArmedGuards.RollFor(FloorNow));
            ai.roam = RandomWalkable;                                         // (he wanders the floor)
            Net.RunSync.Instance?.BroadcastSpawnGuard(ai.NetId, p, look, p);
            CloseEntryLater(door.index, 7f);
            return true;
        }

        /// <summary>Tests: is this point at one of the stairwell doors (within <paramref name="within"/> m, on the floor plan)?</summary>
        public bool NearStairDoor(Vector3 p, float within)
        {
            foreach (var e in GuardEntries())
            {
                if (e.index < 1) continue;
                var d = p - e.centre; d.y = 0f;
                if (d.magnitude <= within) return true;
            }
            return false;
        }

        /// <summary>The old behaviour (no guest elevator on this floor): spawn out of sight somewhere on the floor.</summary>
        int SpawnReinforcementsAnywhere(Vector3 target, int n)
        {
            if (!current) return 0;
            int spawned = 0;
            for (int i = 0; i < n; i++)
            {
                // a random walkable spot anywhere on the floor (not right on top of the trouble)
                Vector3 best = Vector3.zero; bool found = false;
                for (int tries = 0; tries < 30 && !found; tries++)
                {
                    var p = RandomWalkable();
                    if (Vector3.Distance(p, target) < 12f && tries < 25) continue;
                    best = p; found = true;
                }
                if (!found) continue;
                int look = Random.Range(0, 100);
                var ai = SpawnGuard($"Backup_{reinforcementCount++}", new List<Vector3> { best }, look, ArmedGuards.RollFor(FloorNow));
                ai.roam = RandomWalkable;   // after reaching the spot, wander freely again
                ai.IsBackup = true;         // (a backup officer notices a player in his cone much faster: GuardRules.BackupSpotFactor)
                ai.Dispatch(target);
                Net.RunSync.Instance?.BroadcastSpawnGuard(ai.NetId, best, look, target); // clients show it too
                spawned++;
            }
            return spawned;
        }

        /// <summary>Client: the host called backup; show the guard it spawned (it moves from snapshots).</summary>
        public void SpawnReinforcementFromNet(int id, Vector3 pos, int look)
        {
            if (!current) return;
            Net.NetIds.PendingId = id;
            SpawnGuard($"Backup_{reinforcementCount++}", new List<Vector3> { pos }, look);
            Net.NetIds.PendingId = Net.NetIds.None;
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
