# Round 12, batch G23 (the owner, 2026-10-19 night: "The PvP room can be much larger if possible, it doesn't need to be symmetrical with the breakroom"): the PVP room is a big warehouse now, 166 m2 (it was 42, G20 made it 77):
#  - the HALL runs the whole way along the north wall, from the west wall to x 23.6 (just past the first roller door), 4.6 m deep (x 0.2..23.6, z 13.2..17.8: PvpRoomArea);
#  - the BLOCK: east of the elevator room the room is deeper, down to z 11.1 (x 13.2..23.6: PvpRoomBlock), the south wall at z 10.9 leaves the way from the elevator's door to the vendor open (the band z 0.2..10.7);
#  - the ANNEX of G20 (the old closed pocket west of the elevator room) stays;
#  - the doorway moves to the east wall at x 23.8 (z 15.6); the old east wall at x 9.5 is gone, the room is no longer the break room's twin.
# Furnished as a warehouse: a full container and a small one in the west part, two containers more in the east, loaded pallets, stacked crates, barrels, two blocks of boxes; the middle of the hall (the fighters' start), the junction
# with the annex and the lane in front of the doorway stay open. BE_qa (= C:\bqm) only.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$A\FloorGenerator.DockRooms.cs"
Rep @'
        public Rect PvpRoomAnnex { get; private set; }
'@ @'
        public Rect PvpRoomAnnex { get; private set; }
        /// <summary>The deeper part of the PVP room east of the elevator room (the owner, 2026-10-19 night: "much larger ... it doesn't need to be symmetrical with the breakroom"): it joins the hall along z 13.2.</summary>
        public Rect PvpRoomBlock { get; private set; }
'@
Rep @'
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, eastX - 0.4f, depth - 0.2f - (northWallZ + 0.2f)); // x 0.2..9.3, z 13.2..17.8
'@ @'
            float pvpEast = 23.8f, blockWallZ = 10.9f;                           // the PVP room's east wall (just past the first roller door of the bay) and the south wall of its deeper east part
            float elevEast = elev.x + 4.2f;                                       // the outer face of the elevator room's east wall
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, pvpEast - 0.4f, depth - 0.2f - (northWallZ + 0.2f)); // x 0.2..23.6, z 13.2..17.8: the hall, the whole way along the north wall
            PvpRoomBlock = new Rect(elevEast, blockWallZ + 0.2f, (pvpEast - 0.2f) - elevEast, (northWallZ + 0.2f) - (blockWallZ + 0.2f));          // x 13.2..23.6, z 11.1..13.2
'@
Rep @'
            PvpRoomDoor = new Vector3(eastX, 0f, pvpDoorZ);
'@ @'
            PvpRoomDoor = new Vector3(pvpEast, 0f, pvpDoorZ);
'@
Rep @'
            DoorwayWall(eastX, northWallZ, depth, pvpDoorZ);
'@ @'
            WallX((elevEast + pvpEast) * 0.5f, blockWallZ, pvpEast - elevEast);        // the south wall of the east part: from the elevator room's corner to the east wall
            DoorwayWall(pvpEast, blockWallZ, depth, pvpDoorZ);                         // the east wall, with the doorway
'@
Rep @'
            RoomLight(parent, "PvpRoomLightAnnex", new Vector3(3.6f, 3.1f, PvpRoomAnnex.center.y), new Color(1f, 0.85f, 0.8f), 1.2f);
'@ @'
            RoomLight(parent, "PvpRoomLightAnnex", new Vector3(3.6f, 3.1f, PvpRoomAnnex.center.y), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightMiddle", new Vector3(11.9f, 3.1f, 14.8f), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightEast", new Vector3(18.0f, 3.1f, 14.0f), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightBlock", new Vector3(21.0f, 3.1f, 12.5f), new Color(1f, 0.85f, 0.8f), 1.0f);
'@
Rep @'
            BoxBlock(parent, 6.0f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);
'@ @'
            BoxBlock(parent, 6.0f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);
            // the east part of the warehouse: a full container along the north wall, a small one standing across the joint with the deeper part, loaded pallets and stacked crates in the south, the lane to the doorway open
            PlaceKey("container", 17.1f, PvpRoomArea.yMax - 1.225f, 90f, parent, 0f, true);
            PlaceKey("container_small", 17.2f, PvpRoomBlock.yMin + 1.62f, 0f, parent, 0f, true);
            PlaceKey("pallet_loaded", 20.6f, PvpRoomBlock.yMin + 1.1f, 0f, parent, 0f, true);
            PlaceKey("pallet_loaded", 22.6f, PvpRoomBlock.yMin + 1.1f, 0f, parent, 0f, true);
            foreach (var (cx, cz) in new[] { (14.4f, PvpRoomBlock.yMin + 1.2f), (21.0f, 14.6f) })
            {
                PlaceKey("crate_large", cx, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", cx, cz, 25f, parent, 0.79f, true);
            }
'@
Save-Src

Open-Src "$A\FloorGenerator.cs"
Rep @'
                        WallZ(x, z0 + cellSize - half / 2f, half);
'@ @'
                        if (!(dockWalls && c == 1)) WallZ(x, z0 + cellSize - half / 2f, half);       // (the PVP warehouse runs across the line between the two cells of the docks: no wall of the north half there)
'@
Save-Src

Open-Src "$A\FloorGenerator.Dock.cs"
Rep @'
                new Vector4(0f, 12.7f, 9.9f, 18f),  // the PVP room
'@ @'
                new Vector4(0f, 12.7f, 24.2f, 18f),  // the PVP room: the hall along the whole north wall
                new Vector4(12.9f, 10.5f, 24.2f, 12.9f),  // and its deeper east part
'@
Save-Src

Open-Src "$A\Pvp.cs"
Rep @'
        public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z) || PvpRules.Inside(Annex, transform.position.x, transform.position.z);
'@ @'
        /// <summary>The deeper east part of the PVP room (see <see cref="Annex"/>), or an empty rectangle.</summary>
        public static Rect Block
        {
            get
            {
                var hm = HeistManager.Instance;
                return hm && hm.Floor == 0 && gen ? gen.PvpRoomBlock : default;
            }
        }

        public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z) || PvpRules.Inside(Annex, transform.position.x, transform.position.z) || PvpRules.Inside(Block, transform.position.x, transform.position.z);
'@
Save-Src

Open-Src "$E\NetDevRound12.cs"
Rep @'
                var annexRect = gen.PvpRoomAnnex; var hallRect = gen.PvpRoomArea;
'@ @'
                var annexRect = gen.PvpRoomAnnex; var hallRect = gen.PvpRoomArea; var blockRect = gen.PvpRoomBlock;
'@
Rep @'
                bool InPvp(Vector3 p) => hallRect.Contains(new Vector2(p.x, p.z)) || annexRect.Contains(new Vector2(p.x, p.z));
'@ @'
                bool InPvp(Vector3 p) => hallRect.Contains(new Vector2(p.x, p.z)) || annexRect.Contains(new Vector2(p.x, p.z)) || blockRect.Contains(new Vector2(p.x, p.z));
'@
Rep @'
                float area = hallRect.width * hallRect.height + annexRect.width * annexRect.height;
                R12Check("the PVP room is an L: the hall along the north wall and the old pocket west of the elevator room, joined, 70 m2 and more (it was 42)",
                    annexRect.width > 4f && annexRect.height > 7.5f && Mathf.Abs(annexRect.yMax - hallRect.yMin) < 0.3f && area > 70f, $"(hall {hallRect}, annex {annexRect}, {area:0} m2)");
                R12Check("a full-length shipping container and a small one stand in it, solid, as cover", full.Count == 1 && small.Count == 1 && full[0].GetComponentInChildren<Collider>() != null && small[0].GetComponentInChildren<Collider>() != null,
                    $"({full.Count} full, {small.Count} small)");
'@ @'
                float area = hallRect.width * hallRect.height + annexRect.width * annexRect.height + blockRect.width * blockRect.height;
                R12Check("the PVP room is a big warehouse (the owner: \"much larger\", not the break room's twin): the hall the whole way along the north wall, the deeper east part, the old pocket west of the elevator room, joined, 150 m2 and more (it was 42)",
                    hallRect.width > 20f && annexRect.width > 4f && annexRect.height > 7.5f && Mathf.Abs(annexRect.yMax - hallRect.yMin) < 0.3f && blockRect.width > 9f && Mathf.Abs(blockRect.yMax - hallRect.yMin) < 0.3f && area > 150f,
                    $"(hall {hallRect}, block {blockRect}, annex {annexRect}, {area:0} m2)");
                R12Check("two full-length shipping containers and two small ones stand in it, solid, as cover", full.Count == 2 && small.Count == 2 && full.All(t => t.GetComponentInChildren<Collider>() != null) && small.All(t => t.GetComponentInChildren<Collider>() != null),
                    $"({full.Count} full, {small.Count} small)");
'@
Rep @'
                var lane = new Vector3(gen.PvpRoomDoor.x - 1.4f, 0f, gen.PvpRoomDoor.z);
                bool laneOpen = !Physics.CheckCapsule(lane + Vector3.up * 0.5f, lane + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                R12Check("the hall opens into the annex (no wall: a player fits through the joint) and the way in from the doorway is open", jointOpen && laneOpen, $"(joint {jointOpen}, lane {laneOpen})");
'@ @'
                var lane = new Vector3(gen.PvpRoomDoor.x - 1.4f, 0f, gen.PvpRoomDoor.z);
                bool laneOpen = !Physics.CheckCapsule(lane + Vector3.up * 0.5f, lane + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var middle = new Vector3((hallRect.xMin + hallRect.xMax) * 0.5f, 0f, (hallRect.yMin + hallRect.yMax) * 0.5f);
                bool middleOpen = !Physics.CheckCapsule(middle + Vector3.up * 0.5f, middle + Vector3.up * 1.6f, 1.0f, ~0, QueryTriggerInteraction.Ignore);
                var blockJoint = new Vector3(19.0f, 0f, hallRect.yMin);
                bool blockJointOpen = !Physics.CheckCapsule(blockJoint + Vector3.up * 0.5f, blockJoint + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var cross = new Vector3(18.0f, 0f, 15.0f);
                bool crossOpen = !Physics.CheckCapsule(cross + Vector3.up * 0.5f, cross + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore);       // (the dividing wall between the two cells of the docks has no north half: the hall runs through)
                R12Check("the hall opens into the annex and into the deeper east part (no wall: a player fits through the joints), the way in from the doorway is open and so is the middle of the hall (where the fighters start)",
                    jointOpen && laneOpen && middleOpen && blockJointOpen && crossOpen, $"(annex joint {jointOpen}, doorway lane {laneOpen}, middle {middleOpen}, block joint {blockJointOpen}, across the cell line {crossOpen})");
'@
Rep @'
                R12Check("a fighter in the annex counts as inside the PVP room, one in the elevator room next to it does not",
                    PvpRules.Inside(annexRect, 2.5f, 9f) && !PvpRules.Inside(annexRect, 7f, 9f) && !PvpRules.Inside(hallRect, 7f, 9f));
'@ @'
                R12Check("a fighter in the annex or in the deeper east part counts as inside the PVP room, one in the elevator room or in the way from its door to the vendor does not",
                    PvpRules.Inside(annexRect, 2.5f, 9f) && !PvpRules.Inside(annexRect, 7f, 9f) && !PvpRules.Inside(hallRect, 7f, 9f) && PvpRules.Inside(blockRect, 18f, 12f) && !PvpRules.Inside(blockRect, 18f, 9f) && !PvpRules.Inside(hallRect, 18f, 9f));
'@
Save-Src
Write-Output 'round 12 batch G23 patched'
