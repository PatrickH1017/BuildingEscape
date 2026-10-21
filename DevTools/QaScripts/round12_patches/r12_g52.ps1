# Round 12, batch G52: the PVP room (the owner, 2026-10-20: "The doorway to the PvP room should also be a bit larger since the forklift should technically fit through it and the PvP room should not have any freight
# containers inside of it, more shelves with boxes"): the doorway is a forklift door (2.4 m wide, 2.8 m high; the lobby's forklift is 1.2 x 2.2 m), the three shipping containers are gone, and the lobby's steel shelving with its
# cardboard (DockDressing.Shelf) stands along the walls and in rows as the cover. Tests: Round12 (no container, 20+ shelves, nothing inside anything else, a forklift-sized box passes the doorway; pictures incl. a plan view).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

# ---------------------------------------------------------------- the generator
Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.DockRooms.cs'
Rep 'const float RoomDoorWidth = 1.6f, RoomDoorHeight = 2.6f;' @'
const float RoomDoorWidth = 1.6f, RoomDoorHeight = 2.6f;
        /// <summary>
        /// The PVP room's doorway is a FORKLIFT door (the owner, 2026-10-20: "the forklift should technically fit through it"): the lobby's forklift (<see cref="DockDressing.Forklift"/>) is 1.2 m wide and 2.2 m high, so the gap is 2.4 m wide and 2.8 m high
        /// (the break room's stays an ordinary 1.6 x 2.6 m door).
        /// </summary>
        public const float PvpDoorWidth = 2.4f, PvpDoorHeight = 2.8f;
'@
Rep 'with a doorway whose middle is at <paramref name="gapCentre"/>.</summary>' 'with a doorway whose middle is at <paramref name="gapCentre"/>, <paramref name="width"/> wide and <paramref name="height"/> high (an ordinary door unless asked otherwise).</summary>'
RepRegion '        void DoorwayWall(float x, float z0, float z1, float gapCentre)' 'new Vector3(0.4f, lintel, RoomDoorWidth), wallMat);' @'
        void DoorwayWall(float x, float z0, float z1, float gapCentre, float width = RoomDoorWidth, float height = RoomDoorHeight)
        {
            float g0 = gapCentre - width * 0.5f, g1 = gapCentre + width * 0.5f;
            if (g0 - z0 > 0.05f) WallZ(x, (z0 + g0) * 0.5f, g0 - z0);
            if (z1 - g1 > 0.05f) WallZ(x, (g1 + z1) * 0.5f, z1 - g1);
            float lintel = wallHeight - height;
            Box("DoorHeader", new Vector3(x, height + lintel * 0.5f, gapCentre), new Vector3(0.4f, lintel, width), wallMat);
'@
Rep 'DoorwayWall(eastX, northWallZ, depth, pvpDoorZ);' 'DoorwayWall(eastX, northWallZ, depth, pvpDoorZ, PvpDoorWidth, PvpDoorHeight);          // (a forklift door: PvpDoorWidth x PvpDoorHeight)'
RepRegion '            // ================= the PVP room: a warehouse of the Loading Dock' '            BoxBlock(parent, 8.4f, 23.55f, 0f, 2, 2, 1, true);' @'
            // ================= the PVP room: a warehouse of the Loading Dock (the owner, 2026-10-19 night: "storage containers for players to use as coverage, somewhat like a warehouse"; 2026-10-20: "extend it further back behind the elevator and extend out
            // the walls wider to the right when you walk into the PvP room"; and later that day: "the doorway to the PvP room should also be a bit larger since the forklift should technically fit through it and the PvP room should not have any
            // freight containers inside of it, more shelves with boxes"): the old room, 6.4 m more to the north and the pocket behind the elevator room, with the lobby's steel shelving and its cardboard (DockDressing.Shelf) as the cover - along the
            // walls and in rows - a few pallets and crates, the red mat in the middle (where the fighters start), and the lane in front of the forklift-sized doorway open
            var pvpLight = new Color(1f, 0.85f, 0.8f);
            RoomLight(parent, "PvpRoomLight", new Vector3(BreakTvX - 1.0f, 3.1f, pvpDoorZ - 0.5f), pvpLight, 1.2f);
            RoomLight(parent, "PvpRoomLightNorth", new Vector3(4.75f, 3.1f, 21.5f), pvpLight, 1.4f);
            RoomLight(parent, "PvpRoomLightNorthEast", new Vector3(8.0f, 3.1f, 19.5f), pvpLight, 0.9f);
            RoomLight(parent, "PvpRoomLightAnnex", new Vector3(3.6f, 3.1f, PvpRoomAnnex.center.y), pvpLight, 1.2f);
            var mat = Flat(new Color(0.55f, 0.08f, 0.08f), 0.1f);
            Box("PvpMat", new Vector3(4.75f, 0.012f, (PvpRoomArea.yMin + PvpRoomArea.yMax) * 0.5f), new Vector3(4.2f, 0.024f, 3.4f), mat, parent, collider: false);

            // steel shelving, the lobby's units with their cardboard on the boards (1.8 x 0.66 x 2.4 m, each one solid): a line down the west wall, the north wall full, two units on the elevator room's wall, and three rows of cover - north of the
            // mat, south of it (west of the doorway's lane) and down the middle of the pocket
            float rackHalf = DockDressing.ShelfDepth * 0.5f + 0.02f;                       // (a unit against a wall: its back two centimetres off the wall's face)
            float halfLen = DockDressing.ShelfWidth * 0.5f;
            Transform rackHolder = dockHolder != null ? dockHolder : parent;
            void Rack(float x, float z, float yaw) => DockDressing.Shelf(rackHolder, x, z, yaw, (int)(Mathf.Abs(x) * 7f + Mathf.Abs(z) * 13f));
            void RackRow(float x0, float x1, float z, int n) { for (int i = 0; i < n; i++) Rack(n == 1 ? x0 : Mathf.Lerp(x0, x1, i / (n - 1f)), z, 0f); }                 // n units side by side along x, their middles from x0 to x1
            void RackColumn(float x, float z0, float z1, int n) { for (int i = 0; i < n; i++) Rack(x, n == 1 ? z0 : Mathf.Lerp(z0, z1, i / (n - 1f)), 90f); }               // the same along z
            float westX = PvpRoomArea.xMin + rackHalf, northRackZ = pvpNorth - rackHalf, southRackZ = PvpRoomArea.yMin + rackHalf;
            RackRow(PvpRoomArea.xMin + 0.02f + halfLen, PvpRoomArea.xMax - 0.02f - halfLen, northRackZ, 5);                                  // the north wall: five units across its whole width
            RackColumn(westX, PvpRoomAnnex.yMin + 0.02f + halfLen, northRackZ - rackHalf - 0.02f - halfLen, 10);                           // the west wall: one line of ten, from the pocket's south wall to the north row
            RackRow(5.65f + halfLen, PvpRoomArea.xMax - 0.02f - halfLen, southRackZ, 2);                                                     // the elevator room's north wall: two units, flush with the east wall
            RackRow(4.75f - 1.83f, 4.75f + 1.83f, 21.9f, 3);                                                                                  // a row north of the mat
            RackRow(2.05f + halfLen, 5.65f - halfLen, 15.5f, 2);                                                                              // a row south of it, west of the doorway's lane
            RackColumn(2.4f, PvpRoomAnnex.center.y - 1.83f, PvpRoomAnnex.center.y + 1.83f, 3);                                              // a row down the middle of the pocket

            // lower cover: stacked crates on the pocket's east side, loaded pallets and crates on the hall's east side, two blocks of boxes on pallets by the west line (the way in from the doorway and the middle stay open)
            foreach (float cz in new[] { PvpRoomAnnex.yMin + 1.7f, PvpRoomAnnex.yMax - 1.7f })
            {
                PlaceKey("crate_large", 4.3f, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", 4.3f, cz, 20f, parent, 0.79f, true);          // (a second one on top)
            }
            PlaceKey("pallet_loaded", 7.9f, 18.2f, 90f, parent, 0f, true);
            PlaceKey("pallet_loaded", 7.9f, 20.2f, 0f, parent, 0f, true);
            PlaceKey("crate_large", 8.7f, 21.6f, 0f, parent, 0f, true);
            PlaceKey("crate_large", 8.7f, 22.4f, 0f, parent, 0f, true);
            BoxBlock(parent, 1.9f, 16.85f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 1.9f, 19.9f, 0f, 2, 2, 1, true);
'@
Save-Src

# ---------------------------------------------------------------- the test
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
RepRegion '                var warehouse = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t && t.parent != null).ToList();' '$"({full.Count} full, {small.Count} small, {cover} pieces)");' @'
                var warehouse = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(w => w && w.parent != null).ToList();
                bool InPvp(Vector3 p) => room.Contains(new Vector2(p.x, p.z)) || annex.Contains(new Vector2(p.x, p.z));
                // no freight containers (the owner, 2026-10-20: "the PvP room should not have any freight containers inside of it, more shelves with boxes"): steel shelving with its cardboard instead, pallets and crates for lower cover
                var pvpContainers = warehouse.Where(w => (w.name == "container" || w.name == "container_small") && InPvp(w.position)).ToList();
                var pvpRacks = warehouse.Where(w => w.name == "Shelf" && InPvp(w.position)).ToList();
                int cover = warehouse.Count(w => (w.name == "pallet_loaded" || w.name == "crate_large" || w.name == "barrel") && InPvp(w.position));
                bool racksSolid = pvpRacks.All(w => w.GetComponentsInChildren<Collider>().Length == 1 && w.GetComponent<BoxCollider>() != null);
                int rackBoxes = pvpRacks.Sum(w => w.GetComponentsInChildren<Transform>().Count(c => c.name == "ShelfBox"));
                R12Check("no shipping container stands in the PVP room; steel shelving with its boxes does (20 units or more, each solid with one box collider), and pallets and crates give lower cover (at least 6 pieces)",
                    pvpContainers.Count == 0 && pvpRacks.Count >= 20 && racksSolid && rackBoxes >= pvpRacks.Count * 4 && cover >= 6, $"({pvpContainers.Count} containers, {pvpRacks.Count} shelves with {rackBoxes} boxes, {cover} pallets / crates / barrels)");
                // nothing stands inside anything else or through a wall: the floor plans of the shelves, pallets, crates and box blocks do not overlap (a crate on a crate is above it, not in it) and lie within the room
                var pvpPieces = new List<(string name, Bounds box)>();
                foreach (var piece in warehouse.Where(w => (w.name == "Shelf" || w.name == "pallet_loaded" || w.name == "crate_large" || w.name == "barrel" || w.name == "BoxBlock") && InPvp(w.position)))
                {
                    var pieceRend = piece.GetComponentsInChildren<Renderer>();
                    if (pieceRend.Length == 0) continue;
                    var pieceBox = pieceRend[0].bounds; foreach (var pr0 in pieceRend) pieceBox.Encapsulate(pr0.bounds);
                    pvpPieces.Add((piece.name, pieceBox));
                }
                var pvpClash = new List<string>();
                for (int ia = 0; ia < pvpPieces.Count; ia++)
                    for (int ib = ia + 1; ib < pvpPieces.Count; ib++)
                    {
                        var pa = pvpPieces[ia].box; var pb = pvpPieces[ib].box;
                        bool inPlan = pa.min.x < pb.max.x - 0.03f && pb.min.x < pa.max.x - 0.03f && pa.min.z < pb.max.z - 0.03f && pb.min.z < pa.max.z - 0.03f;
                        bool inHeight = pa.min.y < pb.max.y - 0.05f && pb.min.y < pa.max.y - 0.05f;
                        if (inPlan && inHeight) pvpClash.Add($"{pvpPieces[ia].name} {pa.center:0.0} / {pvpPieces[ib].name} {pb.center:0.0}");
                    }
                var pvpOut = pvpPieces.Where(pp => !(InPvp(new Vector3(pp.box.min.x + 0.01f, 0f, pp.box.min.z + 0.01f)) && InPvp(new Vector3(pp.box.max.x - 0.01f, 0f, pp.box.max.z - 0.01f))
                    && InPvp(new Vector3(pp.box.min.x + 0.01f, 0f, pp.box.max.z - 0.01f)) && InPvp(new Vector3(pp.box.max.x - 0.01f, 0f, pp.box.min.z + 0.01f)))).Select(pp => pp.name + " " + pp.box.center.ToString("0.0")).ToList();
                R12Check("nothing in the PVP room stands inside anything else or through a wall: the shelves, pallets, crates and box blocks have floor plans that do not overlap and lie within the room",
                    pvpClash.Count == 0 && pvpOut.Count == 0, $"({pvpPieces.Count} pieces; overlapping: {string.Join("; ", pvpClash.Take(6))}; outside: {string.Join("; ", pvpOut.Take(6))})");
                // the doorway is a forklift door (the owner, 2026-10-20: "the forklift should technically fit through it"): a box the size of the lobby's forklift passes it side-on with room to spare, and the break room's doorway is still the ordinary one
                var pvpForkTmp = new GameObject("R12ForkSize").transform;
                var pvpFork = DockDressing.Forklift(pvpForkTmp, 0f, 0f, 0f);
                var pvpForkRend = pvpFork.GetComponentsInChildren<Renderer>();
                var pvpForkBox = pvpForkRend[0].bounds; foreach (var fr0 in pvpForkRend) pvpForkBox.Encapsulate(fr0.bounds);
                Destroy(pvpForkTmp.gameObject);
                bool DoorPasses(Vector3 doorAt, float width, float height) => !Physics.CheckBox(new Vector3(doorAt.x, 0.06f + height * 0.5f, doorAt.z), new Vector3(0.3f, height * 0.5f, width * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                R12Check("the PVP room's doorway is a forklift door (2.4 m wide, 2.8 m high): the lobby's forklift goes through it with room to spare, and the break room's door is still the ordinary 1.6 m one",
                    DoorPasses(gen.PvpRoomDoor, pvpForkBox.size.x + 0.6f, pvpForkBox.size.y + 0.3f) && DoorPasses(gen.PvpRoomDoor, 2.3f, 2.7f) && !DoorPasses(gen.PvpRoomDoor, 2.5f, 2.7f)
                    && DoorPasses(gen.BreakRoomDoor, 1.5f, 2.5f) && !DoorPasses(gen.BreakRoomDoor, 1.7f, 2.5f), $"(the forklift is {pvpForkBox.size.x:0.00} x {pvpForkBox.size.y:0.00} m)");
'@
Rep @'
                    ShotCam(cam, "round12_pvp_annex");
                    Destroy(cam.gameObject);
'@ @'
                    ShotCam(cam, "round12_pvp_annex");
                    // the doorway with the lobby's forklift standing in it (it goes through: the owner, 2026-10-20), from the docks and from inside the room
                    var forkPic = new GameObject("R12ForkPicture").transform;
                    DockDressing.Forklift(forkPic, pd.x + 0.5f, pd.z, 270f);
                    cam.fieldOfView = 66f;
                    cam.transform.position = new Vector3(pd.x + 4.0f, 1.9f, pd.z - 1.4f);
                    cam.transform.LookAt(new Vector3(pd.x, 1.0f, pd.z));
                    ShotCam(cam, "round12_pvp_forklift_outside");
                    cam.transform.position = new Vector3(pd.x - 5.0f, 1.9f, pd.z + 2.4f);
                    cam.transform.LookAt(new Vector3(pd.x, 1.0f, pd.z));
                    ShotCam(cam, "round12_pvp_forklift_inside");
                    Destroy(forkPic.gameObject);
                    // the plan of the room: a camera straight down from just under the ceiling (north to the right, west at the top)
                    var planCam = new GameObject("R12PlanCam").AddComponent<Camera>();
                    planCam.enabled = false; planCam.orthographic = true; planCam.orthographicSize = 5.6f; planCam.nearClipPlane = 0.05f; planCam.farClipPlane = 8f;
                    planCam.clearFlags = CameraClearFlags.SolidColor; planCam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
                    planCam.transform.SetPositionAndRotation(new Vector3(4.75f, 3.4f, 14.7f), Quaternion.Euler(90f, 270f, 0f));
                    ShotCam(planCam, "round12_pvp_plan");
                    Destroy(planCam.gameObject);
                    Destroy(cam.gameObject);
'@
Save-Src
Write-Output 'round 12 batch G52 patched'
