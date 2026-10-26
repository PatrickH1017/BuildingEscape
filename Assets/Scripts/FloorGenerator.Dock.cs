using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The basement as a loading dock: the vendor's room has three garage doors on its far wall, two shut and the
    /// third open with the back of an articulated lorry's trailer pulled into it. The trailer is a hallway of four
    /// slabs (floor, two sides, roof) and a front wall, and the vendor stands inside behind a counter with his stock
    /// on shelves. The sale zone, the shelf where bought things appear and the NPC keep the roles the old shop
    /// room gave them. A TV hangs on the south wall near the south-east corner (<see cref="DockTv"/>) with a clear space in front of it, where the workers stand
    /// in the run-start cinematic.
    /// </summary>
    public partial class FloorGenerator
    {
        // set while the basement is being built: BuildShell leaves the vendor room's far wall to BuildLoadingDock
        bool dockWalls;
        // the dock's dressing in the lobby's clean look (DockDressing): a holder at the world origin under the dock's root, and the component that moves the clock, the lamps and the beacon
        Transform dockHolder;
        Net.LobbyDockLife dockLife;

        const float DockDoorWidth = 5.0f, DockDoorHeight = 3.1f;
        static readonly float[] DockDoorOffsets = { -5.8f, 0f, 5.8f };
        const int DockOpenDoor = 2;                     // the third door (to the right as you come in) is the open one
        const float TrailerInner = 3.4f, TrailerLength = 8.5f, TrailerHeight = 3.1f, TrailerWall = 0.2f;

        /// <summary>The ground point just inside the open bay (kept clear of boxes), and the vendor's counter. For tests and tools.</summary>
        public Vector3 DockTrailerEntrance { get; private set; }
        public Vector3 DockCounter { get; private set; }
        /// <summary>The floor point under the middle of the TV on the break room's south wall (on the wall's inside face).</summary>
        public Vector3 DockTvPoint { get; private set; }


        /// <summary>World centre of a cell (for tests and tools).</summary>
        public Vector3 CellCentre(Vector2Int c) => CellCenter(c);

        static readonly Dictionary<Color, Material> flatMats = new Dictionary<Color, Material>();

        /// <summary>A plain matt colour for the dock's set dressing (garage doors, trailer, hazard paint).</summary>
        static Material Flat(Color c, float gloss = 0.15f)
        {
            if (flatMats.TryGetValue(c, out var m) && m) return m;
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            m = new Material(sh) { name = "DockFlat", color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            flatMats[c] = m;
            return m;
        }

        /// <summary>The vendor bay's roller door: the same panel, ribs and handle as the two shut ones beside it, hung from the top edge of the opening and rolled up until the cutscene shuts it.</summary>
        void BuildBayDoor(Transform parent, float dc, float zWall, Material doorMat, Material rib, Material dark)
        {
            var bay = new GameObject("BayDoor");
            bay.transform.SetParent(parent, false);
            bay.transform.position = new Vector3(dc, DockDoorHeight, zWall - 0.08f);          // the pivot is the top edge of the opening
            Box("BayDoorPanel", new Vector3(dc, DockDoorHeight / 2f, zWall - 0.08f), new Vector3(DockDoorWidth - 0.1f, DockDoorHeight, 0.12f), doorMat, bay.transform);
            for (int k = 0; k < 6; k++)
                Box("DoorRib", new Vector3(dc, 0.3f + k * 0.5f, zWall - 0.18f), new Vector3(DockDoorWidth - 0.1f, 0.07f, 0.1f), rib, bay.transform, collider: false);
            Box("DoorHandle", new Vector3(dc, 1.0f, zWall - 0.28f), new Vector3(0.7f, 0.12f, 0.1f), dark, bay.transform, collider: false);
            bay.AddComponent<DockBayDoor>();
        }

        void BuildLoadingDock(Vector2Int cell, int seed)
        {
            var c = CellCenter(cell);
            var root = new GameObject("Vendor");
            root.transform.SetParent(current);
            root.transform.position = c;
            dockHolder = DockDressing.Holder(root.transform);
            dockLife = root.AddComponent<Net.LobbyDockLife>();

            float zWall = (cell.y + 1) * cellSize;           // the far (north) wall of the dock bay
            float x0 = cell.x * cellSize;
            var doorMat = Flat(new Color(0.62f, 0.65f, 0.70f), 0.3f); // roller-door steel
            var rib = Flat(new Color(0.40f, 0.43f, 0.47f), 0.3f);
            var hazard = Flat(new Color(0.95f, 0.78f, 0.1f));
            var dark = Flat(new Color(0.12f, 0.12f, 0.13f));
            var steel = Flat(new Color(0.78f, 0.80f, 0.83f), 0.35f);
            var truckWhite = Flat(new Color(0.88f, 0.89f, 0.91f), 0.3f);

            // ---- the wall: solid between and beside the doors, a lintel over each opening
            float cx = c.x;
            float prevEnd = x0;
            for (int i = 0; i < DockDoorOffsets.Length; i++)
            {
                float dc = cx + DockDoorOffsets[i];
                float start = dc - DockDoorWidth / 2f, end = dc + DockDoorWidth / 2f;
                if (start > prevEnd + 0.01f) WallX((prevEnd + start) / 2f, zWall, start - prevEnd);
                prevEnd = end;
                float lintelH = wallHeight - DockDoorHeight;
                Box("DoorLintel", new Vector3(dc, DockDoorHeight + lintelH / 2f, zWall), new Vector3(DockDoorWidth, lintelH, 0.4f), wallMat, root.transform);

                if (i == DockOpenDoor)
                {
                    // the bay with the trailer in it: its roller door is rolled up (the way it always was) except during the run-start cutscene (DockBayDoor)
                    BuildBayDoor(root.transform, dc, zWall, doorMat, rib, dark);
                    continue;
                }
                // a shut roller door: slatted panel just inside the opening, with a handle
                Box($"GarageDoor_{i}", new Vector3(dc, DockDoorHeight / 2f, zWall - 0.08f), new Vector3(DockDoorWidth - 0.1f, DockDoorHeight, 0.12f), doorMat, root.transform);
                for (int k = 0; k < 6; k++)
                    Box("DoorRib", new Vector3(dc, 0.3f + k * 0.5f, zWall - 0.18f), new Vector3(DockDoorWidth - 0.1f, 0.07f, 0.1f), rib, root.transform, collider: false);
                Box("DoorHandle", new Vector3(dc, 1.0f, zWall - 0.28f), new Vector3(0.7f, 0.12f, 0.1f), dark, root.transform, collider: false);
            }
            if (prevEnd < x0 + cellSize - 0.01f) WallX((prevEnd + x0 + cellSize) / 2f, zWall, x0 + cellSize - prevEnd);

            // yellow safety paint in front of every door, bollards at the open one
            foreach (var off in DockDoorOffsets)
                Box("HazardLine", new Vector3(cx + off, 0.015f, zWall - 1.6f), new Vector3(DockDoorWidth, 0.03f, 0.3f), hazard, root.transform, collider: false);

            // ---- the trailer backed into the open bay
            float tx = cx + DockDoorOffsets[DockOpenDoor];
            float zr = zWall + 0.35f;                         // rear edge, just outside the wall
            float zf = zr + TrailerLength;                    // front wall
            float zm = (zr + zf) / 2f;
            float outer = TrailerInner + TrailerWall * 2f;
            DockDressing.Bollard(dockHolder, tx - DockDoorWidth / 2f - 0.15f, zWall - 1.0f);          // (the lobby's yellow bollards with their dark caps)
            DockDressing.Bollard(dockHolder, tx + DockDoorWidth / 2f + 0.15f, zWall - 1.0f);

            Box("TrailerFloor", new Vector3(tx, -0.1f, zm), new Vector3(outer, 0.2f, TrailerLength), deskMat, root.transform);
            Box("TrailerWallL", new Vector3(tx - TrailerInner / 2f - TrailerWall / 2f, TrailerHeight / 2f, zm), new Vector3(TrailerWall, TrailerHeight, TrailerLength), truckWhite, root.transform);
            Box("TrailerWallR", new Vector3(tx + TrailerInner / 2f + TrailerWall / 2f, TrailerHeight / 2f, zm), new Vector3(TrailerWall, TrailerHeight, TrailerLength), truckWhite, root.transform);
            Box("TrailerRoof", new Vector3(tx, TrailerHeight + 0.1f, zm), new Vector3(outer, 0.2f, TrailerLength), truckWhite, root.transform);
            Box("TrailerFront", new Vector3(tx, TrailerHeight / 2f, zf - 0.1f), new Vector3(outer, TrailerHeight, 0.2f), truckWhite, root.transform);

            // the rubber dock seal that closes the gap between the trailer and the door frame, and the rear fittings
            float gap = (DockDoorWidth - outer) / 2f;
            Box("DockSealL", new Vector3(tx - outer / 2f - gap / 2f, DockDoorHeight / 2f, zWall + 0.28f), new Vector3(gap + 0.1f, DockDoorHeight, 0.3f), dark, root.transform);
            Box("DockSealR", new Vector3(tx + outer / 2f + gap / 2f, DockDoorHeight / 2f, zWall + 0.28f), new Vector3(gap + 0.1f, DockDoorHeight, 0.3f), dark, root.transform);
            Box("RearBar", new Vector3(tx, 0.25f, zr + 0.1f), new Vector3(outer, 0.18f, 0.2f), steel, root.transform, collider: false);
            foreach (var sx in new[] { -1f, 1f })
                Box("RearLight", new Vector3(tx + sx * (outer / 2f + 0.02f), 0.9f, zr + 0.05f), new Vector3(0.12f, 0.35f, 0.1f), Flat(new Color(0.9f, 0.1f, 0.1f)), root.transform, collider: false);

            // inside: a work light
            var work = new GameObject("TrailerLight");
            work.transform.SetParent(root.transform);
            work.transform.position = new Vector3(tx, TrailerHeight - 0.3f, zm - 0.8f);
            var wl = work.AddComponent<Light>();
            wl.type = LightType.Point; wl.color = new Color(1f, 0.93f, 0.8f); wl.range = 10f; wl.intensity = 1.5f;
            dockLife.trailerLamp = wl;          // (breathes a little, like the lobby's trailer light)

            // ---- the vendor, behind a counter at the front of the trailer
            var mat = vendorMat ? vendorMat : lootMat;
            float counterZ = zf - 2.9f;
            Box("VendorCounter", new Vector3(tx, 0.55f, counterZ), new Vector3(TrailerInner - 0.2f, 1.1f, 0.9f), deskMat, root.transform).GetComponent<Renderer>().enabled = false;          // (its collider stays; DockDressing.CounterLook gives it its look)
            Box("VendorSign", new Vector3(tx, 2.45f, zf - 0.28f), new Vector3(2.0f, 0.55f, 0.1f), mat, root.transform, collider: false);

            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = "VendorNPC";
            npc.transform.SetParent(root.transform);
            npc.transform.position = new Vector3(tx, 1f, zf - 1.5f);
            npc.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // facing the open back of the trailer
            npc.GetComponent<Renderer>().sharedMaterial = mat;
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.transform.SetParent(npc.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.6f, 0.45f);
            nose.transform.localScale = new Vector3(0.4f, 0.15f, 0.2f);
            nose.GetComponent<Renderer>().sharedMaterial = deskMat;
            Kill(nose.GetComponent<Collider>());
            if (Application.isPlaying) PartyCharacterFactory.Attach(npc, PartyCharacterFactory.VendorLook);

            var lamp = new GameObject("DealerBulb");
            lamp.transform.SetParent(root.transform);
            lamp.transform.position = new Vector3(tx, 2.7f, zf - 1.8f);
            var lt = lamp.AddComponent<Light>();
            lt.type = LightType.Point; lt.color = new Color(1f, 0.55f, 0.25f); lt.range = 6f; lt.intensity = 1.4f;

            // his stock: shelves down both walls with crates on top, bags on the floor
            StockTrailer(root.transform, tx, zr, TrailerLength - 3.4f, TrailerInner, mat);
            BuildDockDecor(cx, zWall, x0, tx, zm);

            // sale zone: the bay and the trailer, so loot can be sold from the mouth of the truck
            var zone = new GameObject("VendorZone");
            zone.transform.SetParent(root.transform);
            float zoneNear = zWall - 3.5f, zoneFar = zf - 1.0f;
            zone.transform.position = new Vector3(tx, 1.2f, (zoneNear + zoneFar) / 2f);
            var bc = zone.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(DockDoorWidth + 1.2f, 2.4f, zoneFar - zoneNear);

            // where bought items appear: on the counter, spread across it (the trailer is narrow)
            var shelf = new GameObject("Shelf").transform;
            shelf.SetParent(root.transform);
            shelf.position = new Vector3(tx, 1.2f, counterZ);

            var v = zone.AddComponent<Vendor>();
            v.shelf = shelf;
            v.itemMat = mugMat;
            v.npc = npc.transform;
            v.shelfSpacing = 0.55f;
            v.talkRange = 6.5f; // the trailer is long: a customer at its mouth can call out
            DockTrailerEntrance = new Vector3(tx, 0f, zWall - 2.5f);
            DockCounter = new Vector3(tx, 0f, counterZ);

            // he greets whoever walks up to him
            var chat = zone.AddComponent<VendorChat>();
            chat.head = npc.transform;
            chat.lines = new[] { CustodyRules.GreetingLine };   // "Glad you made it. Make it fast and make it a good deal."

            // a punching bag to whack, off to the side of the way through
            var cloth = Flat(new Color(0.78f, 0.66f, 0.45f), 0.1f);
            var wood = Flat(new Color(0.42f, 0.30f, 0.20f), 0.1f);
            var target = Flat(new Color(0.8f, 0.12f, 0.1f), 0.1f);
            DummyAt = new Vector3(c.x - 5.2f, 0f, c.z - 4.6f);
            TrainingDummy.Create(root.transform, DummyAt, 30f, cloth, wood, target);
            foreach (var (dx, dz, sx, sz) in new[] { (0f, 1.2f, 2.6f, 0.08f), (0f, -1.2f, 2.6f, 0.08f), (1.2f, 0f, 0.08f, 2.4f), (-1.2f, 0f, 0.08f, 2.4f) })
                Box("DummyMark", DummyAt + new Vector3(dx, 0.012f, dz), new Vector3(sx, 0.025f, sz), hazard, root.transform, collider: false);

            // the two side rooms: the break room, with the TV that the workers watch the boss's monologue on when a run starts (IntroCinematic), and the PVP room (FloorGenerator.DockRooms.cs)
            BuildDockRooms(root.transform);
            BuildDartBoard(root.transform);
            BuildDockVending(root.transform, tx);          // (the vending machines: before the scatter below, which keeps clear of them)

            // and a basement's worth of junk everywhere else
            DockClutter = ScatterStorage(root.transform, seed, c, tx, zWall);
        }

        /// <summary>
        /// The look of the lobby's loading dock on the basement's own (the owner, 2026-10-19: "the layout is great ... use the lobby as a reference, for example the clock is great"): steel door frames and roller boxes, a dark band along the foot of the bay wall
        /// between the doors, a wall clock on the east wall that tells the real time, pipes under the ceiling, a notice board and an extinguisher, two hanging lamps, a turning beacon by the open bay and the glowing strip in the trailer (see <see cref="DockDressing"/>).
        /// </summary>
        void BuildDockDecor(float cx, float zWall, float x0, float tx, float zm)
        {
            var h = dockHolder;
            float zi = zWall - 0.2f;                                   // the inner face of the bay wall
            float width = cols * cellSize, depth = rows * cellSize, eastInner = width - 0.2f;
            float prevEnd = x0;
            for (int i = 0; i < DockDoorOffsets.Length; i++)
            {
                float dc = cx + DockDoorOffsets[i];
                float start = dc - DockDoorWidth / 2f, end = dc + DockDoorWidth / 2f;
                DockDressing.DoorFrame(h, dc, zi, DockDoorWidth, DockDoorHeight, true);
                DockDressing.WallBase(h, prevEnd, start, zi);
                prevEnd = end;
            }
            DockDressing.WallBase(h, prevEnd, x0 + cellSize, zi);
            DockDressing.Clock(h, dockLife, new Vector3(eastInner - 0.07f, 2.45f, depth / 2f), 90f);
            DockDressing.Pipes(h, eastInner, 0.4f, depth - 0.4f, 3.1f);
            DockDressing.WallThings(h, eastInner, depth / 2f - 2.8f, depth / 2f + 2.8f);
            DockDressing.Lamps(h, dockLife, new[] { new Vector3(cx - 3.2f, 0f, zWall - 7.5f), new Vector3(tx - 1.0f, 0f, zWall - 11.0f) }, wallHeight - 0.05f, 0.3f);
            DockDressing.Beacon(h, dockLife, new Vector3(tx - DockDoorWidth / 2f - 0.9f, 2.7f, zi - 0.15f));
            DockDressing.TrailerStrip(h, new Vector3(tx, TrailerHeight - 0.06f, zm - 1.5f), 3.4f);
            // the vendor's shop (the owner, 2026-10-19: "the Vendor's truck looking nicer with the new assets"): his counter, his sign and a lamp over the counter
            float zf = zWall + 0.35f + TrailerLength, counterZ = zf - 2.9f;
            DockDressing.CounterLook(h, tx, counterZ, TrailerInner - 0.2f, 0.9f);
            DockDressing.SignFrame(h, tx, 2.45f, zf - 0.28f, 2.0f, 0.55f);
            DockDressing.LampFixture(h, new Vector3(tx, 0f, counterZ - 0.3f), TrailerHeight - 0.02f, 0.35f);
            DockDressing.GunWall(h, tx, zf - 0.2f, 0.8f);          // (the guns on the wall behind him hang there once they are unlocked: the owner, 2026-10-19)
        }

        /// <summary>
        /// Where the dock's vending machines stand (<see cref="DockVendingRules"/>): [0] and [1] the two black ones on the main room's south wall, [2] the black one in the break room. Empty when the machines could not be built (their packs are
        /// not in the project). For tests and tools.
        /// </summary>
        public DockVendingRules.Spot[] DockVendingSpots { get; private set; } = new DockVendingRules.Spot[0];

        /// <summary>The inner face of the basement's outer walls (they are 0.4 m thick and centred on the edge of the floor).</summary>
        const float DockWallInner = 0.2f;

        /// <summary>
        /// The vending machines (the owner, 2026-10-23: both machines of the main room are the black one - "Use the black vending machine instead of the red" - and every machine sells the consumables, see <see cref="VendingMachine"/>; 2026-10-22: "add them both to the main room of the loading dock and add the smaller black one to the break room ... In the main room I want them on the wall across from the vendor"): the black Synty one
        /// ("vending") and the big red nappin one ("vending_red") side by side on the main room's south wall, which is across from the vendor's trailer in the north wall, the pair centred on the vendor; and a second black one in the break room, against
        /// its east wall in the corner by the south wall (the cast of the run-start cutscene stands further north and the doorway is beside it). Plain solid props for now: nothing is sold from them yet.
        /// </summary>
        void BuildDockVending(Transform parent, float vendorX)
        {
            DockVendingSpots = new DockVendingRules.Spot[0];
            if (!PropTable.TryGet("vending", out var black)) return;
            // both machines of the main room are the black one (the owner, 2026-10-23: "Use the black vending machine instead of the red"); each of the three is a vending machine that sells the consumables (VendingMachine)
            DockVendingRules.MainRoom(vendorX, DockWallInner, black.W, black.D, black.W, black.D, out var firstSpot, out var secondSpot);
            var breakSpot = DockVendingRules.BreakRoom(BreakRoom.xMax, BreakRoom.yMin, black.W, black.D);
            DockVendingSpots = new[] { firstSpot, secondSpot, breakSpot };
            var walls = new[] { DockWallInner, DockWallInner, BreakRoom.xMax };
            for (int i = 0; i < DockVendingSpots.Length; i++)
            {
                var go = PlaceKey("vending", DockVendingSpots[i].x, DockVendingSpots[i].z, DockVendingSpots[i].yaw, parent, 0f, true);
                SnapBackToWall(go, DockVendingSpots[i], walls[i]);
                HideVendingGlass(go);
                if (go != null) go.AddComponent<VendingMachine>().Setup(DockVendingSpots[i], go.transform.position.y);
            }
        }

        /// <summary>
        /// The black Synty machine's glass front is a flat quad (SM_Prop_VendingMachine_01_Glass) that uses the SAME opaque colour-atlas material as the body, so it is a solid blue-grey panel that hides the stock inside (the owner, 2026-10-22, from Unity:
        /// "the inside didnt load"). The quad is switched off here, so the goods show; only the dock's two machines are touched (the tower's break rooms build theirs from the same prop and are not changed).
        /// </summary>
        static void HideVendingGlass(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0) r.enabled = false;
        }

        /// <summary>
        /// Slides a placed machine along the way it faces until the back of the REAL model (the measured bounds of its renderers, not the numbers of the prop table) stands <see cref="DockVendingRules.WallGap"/> off the wall, whose inner face is at
        /// <paramref name="wall"/> (z for a machine facing north or south, x for one facing east or west).
        /// </summary>
        static void SnapBackToWall(GameObject go, DockVendingRules.Spot spot, float wall)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            spot.Front(out int fx, out int fz);
            float back = fz > 0 ? b.min.z : fz < 0 ? b.max.z : fx > 0 ? b.min.x : b.max.x;
            float want = (fz > 0 || fx > 0) ? wall + DockVendingRules.WallGap : wall - DockVendingRules.WallGap;
            float d = want - back;
            go.transform.position += new Vector3(fx != 0 ? d : 0f, 0f, fz != 0 ? d : 0f);
        }

        /// <summary>Where the dummy stands, and how many pieces of storage were scattered (for tests).</summary>
        public Vector3 DummyAt { get; private set; }
        public int DockClutter { get; private set; }

        /// <summary>
        /// Boxes, filing cabinets, shelving and a chair or two, as a basement store room would have: stacks of boxes
        /// all over, cabinets and shelves along the walls. The way from the elevator to the open bay, the front of
        /// the truck and the dummy are kept clear. Different every run (from the floor seed).
        /// </summary>
        int ScatterStorage(Transform parent, int seed, Vector3 bayCentre, float tx, float zWall)
        {
            var rng = new System.Random(seed ^ 0x5702A6E);
            float width = cols * cellSize, depth = rows * cellSize;
            var elev = CellCenter(Vector2Int.zero);

            // rectangles (minX, minZ, maxX, maxZ) that stay empty
            var clear = new List<Vector4>
            {
                new Vector4(elev.x - 5.4f, elev.z - 5.4f, elev.x + 5.4f, elev.z + 5.4f),                 // the elevator room and its door
                new Vector4(elev.x + 3f, bayCentre.z - 3.0f, tx + 3.4f, bayCentre.z + 3.0f),              // the way through to the bay
                new Vector4(tx - 3.6f, zWall - 8.5f, tx + 3.6f, zWall),                                   // the front of the truck
                new Vector4(bayCentre.x - cellSize / 2f, zWall - 2.3f, bayCentre.x + cellSize / 2f, zWall), // in front of the doors
                new Vector4(DummyAt.x - 2.2f, DummyAt.z - 2.2f, DummyAt.x + 2.2f, DummyAt.z + 2.2f),      // room to swing at the dummy
                new Vector4(0f, 0f, 9.9f, 5.3f),  // the break room: the TV, the workers who watch it and the cinematic's cameras
                new Vector4(0f, 5.1f, 5.1f, 12.9f),  // the closed pocket between the two rooms
                new Vector4(0f, 12.7f, 9.9f, 18f),  // the PVP room
                new Vector4(BreakRoomDoor.x, BreakRoomDoor.z - 2.4f, BreakRoomDoor.x + 4.6f, BreakRoomDoor.z + 2.4f),   // in front of the break room's doorway
                new Vector4(PvpRoomDoor.x, PvpRoomDoor.z - 2.4f, PvpRoomDoor.x + 6.4f, PvpRoomDoor.z + 2.4f),            // in front of the PVP room's doorway, a plaza of 6.4 x 4.8 m (the owner, 2026-10-19 night: a forklift stood across the doorway; 2026-10-20: "the Forklift should not be blocking the PvP room doorway ... players need to be able to get into the PvP room")
                new Vector4(elev.x + 4.2f, elev.z + 3.0f, elev.x + 6.9f, depth),                                          // and a lane 2.7 m wide from it down the elevator room's east wall to the way through to the bay: a forklift fits all the way from the elevator's door to the PVP room's doorway
                new Vector4(cellSize - 0.2f, 0f, cellSize + 0.2f, (cellSize - doorWidth) / 2f),                       // the wall between the two halves of the docks (its south stub, the dart board's wall): nothing stands half inside it
                new Vector4(cellSize - 0.2f, depth - (cellSize - doorWidth) / 2f, cellSize + 0.2f, depth),            // (the north stub of the same wall)
                new Vector4(width - 1.6f, depth / 2f - 4f, width, depth / 2f + 4f),  // the clock, the notice board and the extinguisher on the east wall
            };
            // the hallway outside the break room, from its doorway to the wall between the two halves of the docks, where the dart board hangs (the owner, 2026-10-20: "the hallway should just be cleaned up a bit (leave the shelves
            // against the wall)"): no boxes, pallets, drums or forklift stand on its floor; the steel shelves against the walls may (those are placed against a wall, which is not checked against this list)
            var floorClear = new List<Vector4> { new Vector4(BreakRoomDoor.x, 0f, cellSize - 0.2f, (cellSize - doorWidth) / 2f) };
            foreach (var vm in DockVendingSpots)                                                                             // the vending machines and the room in front of them (DockVendingRules)
            {
                DockVendingRules.KeepClear(vm, out float vx0, out float vz0, out float vx1, out float vz1);
                clear.Add(new Vector4(vx0, vz0, vx1, vz1));
            }
            var placed = new List<Vector3>(); // x, z, radius

            bool Free(float x, float z, float r, bool againstWall = false)
            {
                // things standing against a wall are allowed to touch it; everything else keeps a step away
                if (!againstWall && (x < r + 0.4f || z < r + 0.4f || x > width - r - 0.4f || z > depth - r - 0.4f)) return false;
                if (x < 0.1f || z < 0.1f || x > width - 0.1f || z > depth - 0.1f) return false;
                foreach (var c in clear)
                    if (x > c.x - r && x < c.z + r && z > c.y - r && z < c.w + r) return false;
                if (!againstWall)
                    foreach (var c in floorClear)
                        if (x > c.x - r && x < c.z + r && z > c.y - r && z < c.w + r) return false;
                foreach (var p in placed)
                {
                    float dx = x - p.x, dz = z - p.y, rr = r + p.z;
                    if (dx * dx + dz * dz < rr * rr) return false;
                }
                return true;
            }

            int count = 0;
            float F(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // steel shelving against the walls, fronts facing in (the clean units of the lobby's loading dock, DockDressing: the owner, 2026-10-19, "the assets from the lobby scene look way cleaner")
            int wallTarget = 9;
            for (int tries = 0; tries < 300 && wallTarget > 0; tries++)
            {
                int side = rng.Next(4); // 0 west, 1 east, 2 south, 3 north
                float along = side < 2 ? F(1f, depth - 1f) : F(1f, width - 1f);
                float half = DockDressing.ShelfDepth / 2f + 0.12f;
                float x, z, yaw;
                switch (side)
                {
                    case 0: x = half; z = along; yaw = 90f; break;
                    case 1: x = width - half; z = along; yaw = 270f; break;
                    case 2: x = along; z = half; yaw = 0f; break;
                    default: x = along; z = depth - half; yaw = 180f; break;
                }
                float r = DockDressing.ShelfWidth / 2f;
                if (!Free(x, z, r, true)) continue;
                DockDressing.Shelf(dockHolder, x, z, yaw, rng.Next(1000));
                placed.Add(new Vector3(x, z, r));
                wallTarget--; count++;
            }

            // the big things in rounds (the owner, 2026-10-20: the hallway is cleared, the rest of the floor is mostly rooms, so what is left holds only a few of them, and every kind must be there): one forklift, one pallet block and one group of
            // drums first, then ten box stacks, then the rest of the big things if there is room, then the stacks up to 12 - 16. Each is checked where it really stands.
            int PlaceForklifts(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 800 && done < n; tries++)
                {
                    float x = F(1.6f, width - 1.6f), z = F(1.6f, depth - 1.6f), yaw = F(0f, 360f);
                    var rot = Quaternion.Euler(0f, yaw, 0f);
                    if (!Free(x, z, 1.35f)) continue;
                    bool fits = true;                                                                                       // (the forks point along +z and reach 1.9 m from its middle: the four corners of its box are checked too)
                    foreach (var (lx, lz) in new[] { (0.7f, 1.9f), (-0.7f, 1.9f), (0.7f, -1.4f), (-0.7f, -1.4f) })
                    {
                        var corner = rot * new Vector3(lx, 0f, lz);
                        if (!Free(x + corner.x, z + corner.z, 0.1f)) { fits = false; break; }
                    }
                    if (!fits) continue;
                    DockDressing.Forklift(dockHolder, x, z, yaw);
                    var tip = rot * new Vector3(0f, 0f, 1.5f);
                    placed.Add(new Vector3(x, z, 1.35f));
                    placed.Add(new Vector3(x + tip.x, z + tip.z, 0.45f));
                    done++; count++;
                }
                return done;
            }

            // pallets with their boxes squared on them
            int PlacePallets(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 800 && done < n; tries++)
                {
                    float x = F(1.2f, width - 1.2f), z = F(1.2f, depth - 1.2f);
                    if (!Free(x, z, 0.9f)) continue;
                    DockDressing.BoxBlock(dockHolder, x, z, 90f * rng.Next(4), 2, 2, 1 + rng.Next(2), true, rng.Next(1000));
                    placed.Add(new Vector3(x, z, 0.9f));
                    done++; count += 4;
                }
                return done;
            }

            // drums in twos and threes
            int PlaceDrums(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 800 && done < n; tries++)
                {
                    float x = F(1f, width - 1f), z = F(1f, depth - 1f);
                    if (!Free(x, z, 0.85f) || !Free(x + 0.62f, z + 0.62f, 0.4f)) continue;          // (the group spreads 0.62 m to the east and to the north: its far drum must not stand in a keep-clear rectangle either)
                    int drums = 2 + rng.Next(2);
                    for (int k = 0; k < drums; k++) DockDressing.Drum(dockHolder, x + (k % 2) * 0.62f, z + (k / 2) * 0.62f, rng.Next(2));
                    placed.Add(new Vector3(x + 0.3f, z + 0.3f, 0.85f));
                    done++; count += drums;
                }
                return done;
            }

            // cardboard stacks of one to three boxes, square to the room and right on top of each other (the owner, 2026-10-18: "The boxes in the basement should not be floating randomly they should be placed nicely")
            int PlaceStacks(int n)
            {
                int done = 0;
                for (int tries = 0; tries < 2000 && done < n; tries++)
                {
                    float x = F(0.8f, width - 0.8f), z = F(0.8f, depth - 0.8f);
                    if (!Free(x, z, 0.5f)) continue;
                    int height = 1 + rng.Next(3);
                    DockDressing.BoxStack(dockHolder, x, z, 90f * rng.Next(4), height, rng.Next(1000));
                    placed.Add(new Vector3(x, z, 0.55f));
                    done++; count += height;
                }
                return done;
            }

            int wantStacks = 12 + rng.Next(5);                                      // (12 to 16 whatever the seed)
            PlaceDrums(1); PlaceForklifts(2); PlacePallets(3);                      // (all three pallet blocks before the stacks, which crowd the floor: the docks must have their pallet blocks, the PVP room's four are not the only ones)
            int haveStacks = PlaceStacks(10);
            PlaceDrums(2);
            PlaceStacks(wantStacks - haveStacks);

            return count;
        }

        /// <summary>Shelving down both walls of the trailer, boxes on top, bags on the floor; the aisle is left clear.</summary>
        void StockTrailer(Transform parent, float tx, float zr, float length, float innerWidth, Material fallback)
        {
            // the clean steel shelving of the lobby's trailer down both walls, boxes on its boards, the aisle left clear (DockDressing)
            var rng = new System.Random(0x7A11);
            // his shelves down both walls: the lobby's steel frames with no cardboard, black-market stock on the boards - weapons on top, gadgets below (DockDressing; the owner, 2026-10-19: "all cool gadgets and weapons and stuff in his truck")
            int shelfNo = 0;
            foreach (var side in new[] { -1f, 1f })
            {
                float z = 0.4f;
                while (z + DockDressing.ShelfWidth <= length)
                {
                    float x = tx + side * (innerWidth / 2f - DockDressing.ShelfDepth / 2f - 0.03f);
                    float zc = zr + z + DockDressing.ShelfWidth / 2f;
                    var shelfRoot = DockDressing.StockShelf(dockHolder, x, zc, side < 0 ? 90f : 270f, shelfNo % 2, rng.Next(1000), 1 + shelfNo % 2);          // (the front faces the aisle: yaw 90 on the left wall faces +X, 270 on the right faces -X)
                    // the briefcases lie FLAT on the boards (the owner, 2026-10-19: "The briefcases should be on the shelves and flat not standing")
                    PlaceBriefcaseFlat(shelfRoot.TransformPoint(new Vector3(0f, 0.92f, 0f)), shelfRoot.eulerAngles.y + rng.Next(2) * 180f, parent);
                    if (shelfNo % 2 == 1) PlaceBriefcaseFlat(shelfRoot.TransformPoint(new Vector3(0f, 0.17f, 0f)), shelfRoot.eulerAngles.y, parent);
                    shelfNo++;
                    z += DockDressing.ShelfWidth + 0.12f;
                }
            }
            
        }

        /// <summary>
        /// A briefcase from the prop pack lying FLAT with its middle over <paramref name="surface"/> (a point on a shelf's board): the pack's own model stands on its edge, so it is turned onto its broad side by its measured box (the owner, 2026-10-19: "on the
        /// shelves and flat not standing"), then set down on the surface.
        /// </summary>
        GameObject PlaceBriefcaseFlat(Vector3 surface, float yawDeg, Transform parent)
        {
            var go = PlaceKey("briefcase", surface.x, surface.z, yawDeg, parent, surface.y, false);
            if (go == null) return null;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return go;
            Bounds Box() { var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); return b; }
            var size = Box().size;
            if (size.y > Mathf.Min(size.x, size.z) * 1.05f) go.transform.RotateAround(Box().center, size.x < size.z ? Vector3.forward : Vector3.right, 90f);          // (standing on its edge: its thinnest side is not the vertical one)
            var flat = Box();
            go.transform.position += new Vector3(surface.x - flat.center.x, surface.y - flat.min.y, surface.z - flat.center.z);
            return go;
        }

        /// <summary>Puts one prop from the table at a ground spot (using the same measured templates the rooms use).</summary>
        GameObject PlaceKey(string key, float x, float z, float yawDeg, Transform parent, float y, bool solid)
        {
            if (!PropTable.TryGet(key, out var def)) return null;
            var pr = GetProto(def);
            if (pr == null) return null;
            var rot = Quaternion.Euler(0f, yawDeg + def.FrontYaw, 0f);
            var off = rot * new Vector3(pr.centre.x, 0f, pr.centre.z);
            var go = Instantiate(pr.go, new Vector3(x - off.x, y - pr.minY, z - off.z), rot, parent);
            go.name = key;
            if (solid && def.solid) AddBox(go, pr, Mathf.Max(def.H, 0.05f));
            return go;
        }

        /// <summary>
        /// The dart board (the owner, 2026-10-19 night): the Synty board on the end wall of the hallway outside the break room (the wall between the two halves of the docks), straight across from the break room's doorway, its middle 1.7 m up and its face to the west; the hallway is kept clear of boxes, pallets, drums and forklifts (the steel shelves along its walls stay)
        /// (ScatterStorage). The game: <see cref="DartBoard"/>.
        /// </summary>
        /// <summary>The pack's dart board with its portrait and its dart taken off (Resources/DartBoardClean, made by the Editor tool DartBoardMeshBuilder).</summary>
        public const string DartBoardCleanMesh = "DartBoardClean";

        void BuildDartBoard(Transform parent)
        {
            if (!Application.isPlaying || !PropTable.TryGet("dartboard", out var def)) return;
            // the end wall of the hallway outside the break room: the west face of the wall between the two halves of the docks, straight across from the break room's doorway (the owner, 2026-10-20: "The dart board and tape should be in
            // this hallway ..., where it was originally")
            float faceX = cellSize - 0.2f;
            var go = PlaceKey("dartboard", faceX - def.D * 0.5f, BreakRoomDoor.z, 270f, parent, 1.7f - def.H * 0.5f, false);
            if (go == null) return;
            // the owner, 2026-10-19 night: "take the picture off the center of the dart board ... and the dart that is already on it": the pack's board has a portrait pinned over the bull and a dart stuck in it, all in one mesh
            var clean = Resources.Load<Mesh>(DartBoardCleanMesh);
            if (clean != null) foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.sharedMesh = clean;
            go.AddComponent<DartBoard>();
        }
    }
}
