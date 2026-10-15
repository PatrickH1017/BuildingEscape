using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The basement as a loading dock: the vendor's room has three garage doors on its far wall, two shut and the
    /// third open with the back of an articulated lorry's trailer pulled into it. The trailer is a hallway of four
    /// slabs (floor, two sides, roof) and a front wall, and the vendor stands inside behind a counter with his stock
    /// on shelves. The sale zone, the shelf where bought things appear and the NPC keep the roles the old shop
    /// room gave them.
    /// </summary>
    public partial class FloorGenerator
    {
        // set while the basement is being built: BuildShell leaves the vendor room's far wall to BuildLoadingDock
        bool dockWalls;

        const float DockDoorWidth = 5.0f, DockDoorHeight = 3.1f;
        static readonly float[] DockDoorOffsets = { -5.8f, 0f, 5.8f };
        const int DockOpenDoor = 2;                     // the third door (to the right as you come in) is the open one
        const float TrailerInner = 3.4f, TrailerLength = 8.5f, TrailerHeight = 3.1f, TrailerWall = 0.2f;

        /// <summary>The ground point just inside the open bay (kept clear of boxes), and the vendor's counter. For tests and tools.</summary>
        public Vector3 DockTrailerEntrance { get; private set; }
        public Vector3 DockCounter { get; private set; }

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

        void BuildLoadingDock(Vector2Int cell, int seed)
        {
            var c = CellCenter(cell);
            var root = new GameObject("Vendor");
            root.transform.SetParent(current);
            root.transform.position = c;

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

                if (i == DockOpenDoor) continue; // the bay with the trailer in it
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
            Box("BayBollardL", new Vector3(tx - DockDoorWidth / 2f - 0.15f, 0.6f, zWall - 1.0f), new Vector3(0.25f, 1.2f, 0.25f), hazard, root.transform);
            Box("BayBollardR", new Vector3(tx + DockDoorWidth / 2f + 0.15f, 0.6f, zWall - 1.0f), new Vector3(0.25f, 1.2f, 0.25f), hazard, root.transform);

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

            // ---- the vendor, behind a counter at the front of the trailer
            var mat = vendorMat ? vendorMat : lootMat;
            float counterZ = zf - 2.9f;
            Box("VendorCounter", new Vector3(tx, 0.55f, counterZ), new Vector3(TrailerInner - 0.2f, 1.1f, 0.9f), deskMat, root.transform);
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

            // and a basement's worth of junk everywhere else
            DockClutter = ScatterStorage(root.transform, seed, c, tx, zWall);
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
            };
            var placed = new List<Vector3>(); // x, z, radius

            bool Free(float x, float z, float r, bool againstWall = false)
            {
                // things standing against a wall are allowed to touch it; everything else keeps a step away
                if (!againstWall && (x < r + 0.4f || z < r + 0.4f || x > width - r - 0.4f || z > depth - r - 0.4f)) return false;
                if (x < 0.1f || z < 0.1f || x > width - 0.1f || z > depth - 0.1f) return false;
                foreach (var c in clear)
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

            // filing cabinets first (always a handful), then shelving, against the walls with the fronts facing in
            var cabinetKeys = new[] { "cabinet_med", "cabinet_med", "cabinet_small" };
            var shelfKeys = new[] { "shelf", "kshelve", "bookshelf" };
            int wallTarget = 5 + 4;
            for (int tries = 0; tries < 300 && wallTarget > 0; tries++)
            {
                var keys = wallTarget > 4 ? cabinetKeys : shelfKeys;
                if (!PropTable.TryGet(keys[rng.Next(keys.Length)], out var def)) continue;
                int side = rng.Next(4); // 0 west, 1 east, 2 south, 3 north
                float along = side < 2 ? F(1f, depth - 1f) : F(1f, width - 1f);
                float half = def.D / 2f + 0.12f;
                float x, z, yaw;
                switch (side)
                {
                    case 0: x = half; z = along; yaw = 90f; break;
                    case 1: x = width - half; z = along; yaw = 270f; break;
                    case 2: x = along; z = half; yaw = 0f; break;
                    default: x = along; z = depth - half; yaw = 180f; break;
                }
                float r = Mathf.Max(def.W, def.D) / 2f;
                if (!Free(x, z, r, true)) continue;
                if (PlaceKey(def.key, x, z, yaw, parent, 0f, true) == null) continue;
                placed.Add(new Vector3(x, z, r));
                wallTarget--; count++;
            }

            // boxes: stacks of one to three, singly and in groups
            int stacks = 30;
            for (int tries = 0; tries < 400 && stacks > 0; tries++)
            {
                float x = F(0.8f, width - 0.8f), z = F(0.8f, depth - 0.8f);
                if (!Free(x, z, 0.5f)) continue;
                int height = 1 + rng.Next(3);
                float yaw = F(0f, 360f);
                bool ok = false;
                for (int h = 0; h < height; h++)
                    ok |= PlaceKey("box", x + F(-0.05f, 0.05f), z + F(-0.05f, 0.05f), yaw + F(-10f, 10f), parent, h * 0.66f, h == 0) != null;
                if (!ok) break;
                placed.Add(new Vector3(x, z, 0.55f));
                stacks--; count += height;
            }

            // a chair or two, turned any which way
            int chairs = 2;
            string[] chairKeys = { "chair_office", "chair_std", "chair_simple" };
            for (int tries = 0; tries < 120 && chairs > 0; tries++)
            {
                float x = F(1f, width - 1f), z = F(1f, depth - 1f);
                if (!Free(x, z, 0.5f)) continue;
                if (PlaceKey(chairKeys[rng.Next(chairKeys.Length)], x, z, F(0f, 360f), parent, 0f, false) == null) continue;
                placed.Add(new Vector3(x, z, 0.5f));
                chairs--; count++;
            }
            return count;
        }

        /// <summary>Shelving down both walls of the trailer, boxes on top, bags on the floor; the aisle is left clear.</summary>
        void StockTrailer(Transform parent, float tx, float zr, float length, float innerWidth, Material fallback)
        {
            var rng = new System.Random(0x7A11);
            string[] racks = { "kshelve", "shelf", "bookshelf", "kshelve", "cabinet_med" };
            bool any = false;
            foreach (var side in new[] { -1f, 1f })
            {
                float z = 0.6f;
                while (z < length)
                {
                    if (!PropTable.TryGet(racks[rng.Next(racks.Length)], out var def)) break;
                    float w = def.W;
                    if (z + w > length)
                    {
                        if (!PropTable.TryGet("shelf", out def)) break;
                        w = def.W;
                        if (z + w > length) break;
                    }
                    float x = tx + side * (innerWidth / 2f - def.D / 2f - 0.02f);
                    float zc = zr + z + w / 2f;
                    // the rack's front faces the aisle: yaw 90 on the left wall faces +X, 270 on the right faces -X
                    if (PlaceKey(def.key, x, zc, side < 0 ? 90f : 270f, parent, 0f, true) != null) any = true;
                    if (rng.NextDouble() < 0.7 && PropTable.TryGet("box", out var bx))
                        PlaceKey("box", x, zc + (float)(rng.NextDouble() - 0.5) * w * 0.4f, rng.Next(360), parent, def.H, false);
                    z += w + 0.1f;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                // flat things only: nothing that would narrow the aisle
                float x = tx + (i % 2 == 0 ? -1f : 1f) * (innerWidth / 2f - 0.85f);
                float z = zr + 1f + (float)rng.NextDouble() * (length - 1.5f);
                if (PlaceKey("briefcase", x, z, rng.Next(360), parent, 0f, false) != null) any = true;
            }
            if (!any)
            {
                // the packs are not in the project: plain crates
                Box("Crate1", new Vector3(tx - 0.9f, 0.5f, zr + 3.0f), new Vector3(0.9f, 1f, 0.9f), deskMat, parent);
                Box("Crate2", new Vector3(tx + 0.9f, 0.6f, zr + 4.5f), new Vector3(0.9f, 1.2f, 0.9f), deskMat, parent);
                Box("Duffel1", new Vector3(tx + 0.8f, 0.25f, zr + 2.2f), new Vector3(0.9f, 0.5f, 0.5f), fallback, parent);
            }
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
    }
}
