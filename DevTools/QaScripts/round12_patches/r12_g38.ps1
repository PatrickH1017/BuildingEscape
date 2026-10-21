# Round 12, batch G38 (the owner, 2026-10-20, with a sketch): "When I said to make it bigger I meant to extend it further back behind the elevator and extend out the walls wider to the right when you walk into the PvP room".
# (G37 put the PVP room back to its first form; the warehouse of 2026-10-19 spread east across the dock hall, which was not what he meant.) The new PVP room, the doorway where it always was (east wall x 9.5, z 15.6):
#  - the old room (x 0.2..9.3, z 13.2..17.8) goes 6.4 m further NORTH, out through the building's north wall (the owner's "wider to the right when you walk into the room"): the north wall of the first cell of the docks starts at x 9.7
#    now, the room has its own floor, ceiling, three walls and lights out there;
#  - the pocket behind (west of) the elevator room, which used to be closed (x 0.2..4.8, z 5.2..13.2), is part of it ("further back behind the elevator"), joined along z 13.2 (the old corner wall is gone);
#  - furnished like a warehouse (the owner, 2026-10-19: "storage containers for players to use as coverage, somewhat like a warehouse"): containers, loaded pallets, crates, barrels, four blocks of boxes, the red mat in the middle
#    (where the fighters start) and the lane in front of the doorway open. About 130 m2 (it was 42).
# BE_qa (= C:\bqm) only.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

# ---------------------------------------------------------------- the rooms
Open-Src "$A\FloorGenerator.DockRooms.cs"
Rep @'
        public Rect PvpRoomArea { get; private set; }
'@ @'
        public Rect PvpRoomArea { get; private set; }
        /// <summary>
        /// The PVP room is an L (the owner, 2026-10-20, with a sketch: "extend it further back behind the elevator and extend out the walls wider to the right when you walk into the PvP room"): the hall of <see cref="PvpRoomArea"/> (the old room and
        /// <see cref="PvpExtension"/> metres more to the north, where it goes out through the building's north wall) and this part behind (west of) the elevator room, the pocket that used to be closed.
        /// </summary>
        public Rect PvpRoomAnnex { get; private set; }

        /// <summary>The PVP room goes out through the first cell's north wall: in the docks that wall starts at this x (east of it it stands as before) and the room reaches this many metres beyond it.</summary>
        const float PvpOpeningTo = 9.7f, PvpExtension = 6.4f;
'@
Rep @'
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, eastX - 0.4f, depth - 0.2f - (northWallZ + 0.2f)); // x 0.2..9.3, z 13.2..17.8
'@ @'
            float pvpNorth = depth - 0.2f + PvpExtension;                                                       // the inner face of the new north wall (z 24.2)
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, eastX - 0.4f, pvpNorth - (northWallZ + 0.2f));        // x 0.2..9.3, z 13.2..24.2: the old room and the part that goes out to the north
            PvpRoomAnnex = new Rect(0.2f, southWallZ + 0.2f, westFace - 0.4f, (northWallZ + 0.2f) - (southWallZ + 0.2f));           // x 0.2..4.8, z 5.2..13.2: behind the elevator room, joined to the hall along z 13.2
'@
Rep @'
            WallX(westFace * 0.5f, southWallZ, westFace);
            WallX(westFace * 0.5f, northWallZ, westFace);
            DoorwayWall(eastX, 0f, southWallZ, breakDoorZ);
            DoorwayWall(eastX, northWallZ, depth, pvpDoorZ);
'@ @'
            WallX(westFace * 0.5f, southWallZ, westFace);
            // (no wall at the elevator room's north-west corner: the pocket behind the elevator room is part of the PVP room)
            DoorwayWall(eastX, 0f, southWallZ, breakDoorZ);
            DoorwayWall(eastX, northWallZ, depth, pvpDoorZ);
            // the part of the PVP room that goes out to the north: its floor, three walls (the building's north wall is open there, see BuildShell) and its ceiling (BuildCeiling)
            float pvpOuter = pvpNorth + 0.4f;                                                                  // the outer face of the new north wall
            Box("PvpFloorNorth", new Vector3((PvpOpeningTo - 0.2f) * 0.5f, -0.25f, (depth - 0.1f + pvpOuter) * 0.5f), new Vector3(PvpOpeningTo + 0.2f, 0.5f, pvpOuter - depth + 0.1f), floorMat);
            WallX((PvpOpeningTo - 0.2f) * 0.5f, pvpNorth + 0.2f, PvpOpeningTo + 0.2f);                         // the north wall
            WallZ(0f, (depth + pvpOuter) * 0.5f, pvpOuter - depth);                                            // the west wall, going on from the building's own
            WallZ(eastX, (depth + pvpOuter) * 0.5f, pvpOuter - depth);                                         // the east wall, going on from the doorway wall
'@
RepRegion '// ================= the PVP room: a few boxes and a red mat to fight on' 'BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);' @'
// ================= the PVP room: a warehouse of the Loading Dock (the owner, 2026-10-19 night: "storage containers for players to use as coverage, somewhat like a warehouse"; 2026-10-20: "extend it further back behind the elevator and extend out
            // the walls wider to the right when you walk into the PvP room"): the old room, 6.4 m more to the north and the pocket behind the elevator room, shipping containers, loaded pallets, crates and barrels to hide behind, the red mat in the middle
            // (where the fighters start) and the lane in front of the doorway open
            var pvpLight = new Color(1f, 0.85f, 0.8f);
            RoomLight(parent, "PvpRoomLight", new Vector3(BreakTvX - 1.0f, 3.1f, pvpDoorZ - 0.5f), pvpLight, 1.2f);
            RoomLight(parent, "PvpRoomLightNorth", new Vector3(4.75f, 3.1f, 21.5f), pvpLight, 1.4f);
            RoomLight(parent, "PvpRoomLightNorthEast", new Vector3(8.0f, 3.1f, 19.5f), pvpLight, 0.9f);
            RoomLight(parent, "PvpRoomLightAnnex", new Vector3(3.6f, 3.1f, PvpRoomAnnex.center.y), pvpLight, 1.2f);
            var mat = Flat(new Color(0.55f, 0.08f, 0.08f), 0.1f);
            Box("PvpMat", new Vector3(4.75f, 0.012f, (PvpRoomArea.yMin + PvpRoomArea.yMax) * 0.5f), new Vector3(4.2f, 0.024f, 3.4f), mat, parent, collider: false);
            // the pocket behind the elevator room: a full-length container along the west wall, pairs of stacked crates on the east side, an aisle between them
            PlaceKey("container", 1.43f, PvpRoomAnnex.center.y, 0f, parent, 0f, true);
            foreach (float cz in new[] { PvpRoomAnnex.yMin + 1.7f, PvpRoomAnnex.yMax - 1.7f })
            {
                PlaceKey("crate_large", 4.3f, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", 4.3f, cz, 20f, parent, 0.79f, true);          // (a second one on top)
            }
            // the old room's south side: barrels and pallets along the wall, blocks of boxes (the way in from the doorway stays open)
            PlaceKey("barrel", 0.55f, PvpRoomArea.yMin + 0.45f, 0f, parent, 0f, true);
            PlaceKey("barrel", 0.55f, PvpRoomArea.yMin + 1.15f, 0f, parent, 0f, true);
            PlaceKey("pallet_loaded", 2.4f, PvpRoomArea.yMin + 0.75f, 0f, parent, 0f, true);
            PlaceKey("pallet_loaded", 6.6f, PvpRoomArea.yMin + 0.75f, 90f, parent, 0f, true);
            BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);
            BoxBlock(parent, 1.3f, 16.85f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 3.6f, 15.2f, 0f, 1, 2, 2, false);
            // the part that goes out to the north: a full container along the west wall, a small one on the north wall, pallets, crates and a block of boxes on the east side (the middle stays open)
            PlaceKey("container", 1.43f, 21.0f, 0f, parent, 0f, true);
            PlaceKey("container_small", 5.4f, pvpNorth - 1.225f, 90f, parent, 0f, true);
            PlaceKey("pallet_loaded", 7.9f, 18.2f, 90f, parent, 0f, true);
            PlaceKey("pallet_loaded", 7.9f, 20.2f, 0f, parent, 0f, true);
            PlaceKey("crate_large", 8.7f, 21.6f, 0f, parent, 0f, true);
            PlaceKey("crate_large", 8.7f, 22.4f, 0f, parent, 0f, true);
            BoxBlock(parent, 8.4f, 23.55f, 0f, 2, 2, 1, true);
'@
Save-Src

# ---------------------------------------------------------------- the building's north wall is open where the room goes out; the room's ceiling
Open-Src "$A\FloorGenerator.cs"
Rep @'
                    bool solid = r == 0 || r == rows || !openN[c, r - 1];
                    float z = r * cellSize, x0 = c * cellSize;
                    if (solid)
'@ @'
                    bool solid = r == 0 || r == rows || !openN[c, r - 1];
                    float z = r * cellSize, x0 = c * cellSize;
                    if (dockWalls && r == rows && c == 0)
                    {
                        // the PVP room goes out through this wall (FloorGenerator.DockRooms.cs): the wall starts east of it
                        WallX((PvpOpeningTo + x0 + cellSize) * 0.5f, z, x0 + cellSize - PvpOpeningTo);
                        continue;
                    }
                    if (solid)
'@
Save-Src

Open-Src "$A\FloorGenerator.AfterHours.cs"
Rep @'
                    rend.receiveShadows = false;
                }
        }
'@ @'
                    rend.receiveShadows = false;
                }
            if (dockWalls)
            {
                // the part of the PVP room that goes out through the north wall of the first cell (FloorGenerator.DockRooms.cs): its ceiling
                float ex0 = -0.2f, ex1 = PvpOpeningTo, ez0 = rows * cellSize + 0.2f, ez1 = rows * cellSize - 0.2f + PvpExtension + 0.4f;
                var ext = Box("Ceiling_PvpNorth", new Vector3((ex0 + ex1) * 0.5f, wallHeight - CeilingSink + CeilingThickness * 0.5f, (ez0 + ez1) * 0.5f), new Vector3(ex1 - ex0, CeilingThickness, ez1 - ez0), mat, holder);
                var er = ext.GetComponent<Renderer>();
                er.shadowCastingMode = ShadowCastingMode.Off;
                er.receiveShadows = false;
            }
        }
'@
Save-Src

# ---------------------------------------------------------------- the fighters' room is two rectangles now
Open-Src "$A\Pvp.cs"
Rep @'
        public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z);
'@ @'
        /// <summary>The part of the PVP room behind the elevator room (the room is an L), or an empty rectangle.</summary>
        public static Rect Annex
        {
            get
            {
                var hm = HeistManager.Instance;
                return hm && hm.Floor == 0 && gen ? gen.PvpRoomAnnex : default;       // (Arena looks the generator up)
            }
        }

        public bool Inside => PvpRules.Inside(Arena, transform.position.x, transform.position.z) || PvpRules.Inside(Annex, transform.position.x, transform.position.z);
'@
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src "$E\NetDevRound12.cs"
RepRegion '// ---- the PVP room is the small room it was again (the owner, 2026-10-20: "The PvP room should be reverted this current one is not good")' 'R12Check("no shipping container stands in the docks any more", !FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t && (t.name == "container" || t.name == "container_small")));' @'
// ---- the PVP room (the owner, 2026-10-20, with a sketch: "extend it further back behind the elevator and extend out the walls wider to the right when you walk into the PvP room"): the old room with its doorway where it was, 6.4 m more to the
            // north (out through the building's north wall) and the pocket behind the elevator room joined to it
            {
                var room = gen.PvpRoomArea; var annex = gen.PvpRoomAnnex;
                float area = room.width * room.height + annex.width * annex.height;
                R12Check("the PVP room is the old room with the doorway where it was, 6.4 m further north, and the pocket behind the elevator room joined to it: 120 to 150 m2 (it was 42)",
                    room.xMin < 0.5f && room.yMin > 13f && room.yMax > 24f && room.width > 8.5f && room.width < 9.6f && annex.xMin < 0.5f && annex.yMin < 6f && annex.yMax > 12.5f && Mathf.Abs(annex.yMax - room.yMin) < 0.3f && annex.width > 4.2f && area > 120f && area < 150f
                    && Mathf.Abs(gen.PvpRoomDoor.x - 9.5f) < 0.1f && Mathf.Abs(gen.PvpRoomDoor.z - 15.6f) < 0.1f,
                    $"(room {room}, annex {annex}, {area:0} m2, doorway {gen.PvpRoomDoor})");
                // the part that goes out to the north: a floor to stand on, a ceiling, walls on three sides (above the containers), and the old north wall open in front of it but standing east of it
                var inNorth = new Vector3(4.75f, 1.0f, 21.5f);
                bool floorThere = Physics.Raycast(inNorth, Vector3.down, out var floorHit, 3f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(floorHit.point.y) < 0.05f;
                bool ceilingThere = Physics.Raycast(inNorth, Vector3.up, out var ceilHit, 6f, ~0, QueryTriggerInteraction.Ignore) && ceilHit.collider.name == "Ceiling_PvpNorth";
                var high = new Vector3(4.75f, 2.9f, 21.5f);
                bool wallN = Physics.Raycast(high, Vector3.forward, out var hn, 6f, ~0, QueryTriggerInteraction.Ignore) && hn.point.z > 24f && hn.point.z < 24.5f;
                bool wallE = Physics.Raycast(high, Vector3.right, out var he, 8f, ~0, QueryTriggerInteraction.Ignore) && he.point.x > 9.2f && he.point.x < 9.7f;
                bool wallW = Physics.Raycast(high, Vector3.left, out var hw, 8f, ~0, QueryTriggerInteraction.Ignore) && hw.point.x > -0.3f && hw.point.x < 0.3f;
                var gapSpot = new Vector3(5.0f, 0f, 18.0f); var wallSpot = new Vector3(12.0f, 0f, 18.0f);
                bool oldWallOpen = !Physics.CheckCapsule(gapSpot + Vector3.up * 0.5f, gapSpot + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
                bool oldWallStands = Physics.CheckCapsule(wallSpot + Vector3.up * 0.5f, wallSpot + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
                R12Check("the north part has a floor under it, a ceiling over it and walls on the north, east and west; the building's old north wall is open in front of it and still stands east of it",
                    floorThere && ceilingThere && wallN && wallE && wallW && oldWallOpen && oldWallStands, $"(floor {floorThere}, ceiling {ceilingThere}, walls N {wallN} E {wallE} W {wallW}, old wall open {oldWallOpen}, stands {oldWallStands})");
                var joint = new Vector3(4.0f, 0f, room.yMin);
                bool jointOpen = !Physics.CheckCapsule(joint + Vector3.up * 0.5f, joint + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var lane = new Vector3(gen.PvpRoomDoor.x - 1.4f, 0f, gen.PvpRoomDoor.z);
                bool laneOpen = !Physics.CheckCapsule(lane + Vector3.up * 0.5f, lane + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var middle = new Vector3((room.xMin + room.xMax) * 0.5f, 0f, (room.yMin + room.yMax) * 0.5f);
                bool middleOpen = !Physics.CheckCapsule(middle + Vector3.up * 1.1f, middle + Vector3.up * 1.7f, 1.0f, ~0, QueryTriggerInteraction.Ignore);          // (a capsule of 1 m radius: it starts above 1 m or it would reach into the floor)
                R12Check("the pocket opens into the room (no wall: a player fits through the joint), the way in from the doorway is open and so is the middle of the room (where the fighters start)",
                    jointOpen && laneOpen && middleOpen, $"(joint {jointOpen}, doorway lane {laneOpen}, middle {middleOpen})");
                var warehouse = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t && t.parent != null).ToList();
                bool InPvp(Vector3 p) => room.Contains(new Vector2(p.x, p.z)) || annex.Contains(new Vector2(p.x, p.z));
                var full = warehouse.Where(t => t.name == "container" && InPvp(t.position)).ToList();
                var small = warehouse.Where(t => t.name == "container_small" && InPvp(t.position)).ToList();
                int cover = warehouse.Count(t => (t.name == "pallet_loaded" || t.name == "crate_large" || t.name == "barrel") && InPvp(t.position));
                R12Check("two full-length shipping containers and a small one stand in it, solid, with pallets, crates and barrels (at least 10 pieces) as more cover", full.Count == 2 && small.Count == 1 && full.All(t => t.GetComponentInChildren<Collider>() != null) && small.All(t => t.GetComponentInChildren<Collider>() != null) && cover >= 10,
                    $"({full.Count} full, {small.Count} small, {cover} pieces)");
                R12Check("a fighter in the pocket or in the north part counts as inside the PVP room, one in the elevator room or in the hallway outside does not",
                    PvpRules.Inside(annex, 2.5f, 9f) && PvpRules.Inside(room, 4.75f, 22f) && !PvpRules.Inside(annex, 7f, 9f) && !PvpRules.Inside(room, 12f, 15.6f) && !PvpRules.Inside(room, 12f, 20f));
'@
RepRx '(?m)^[ \t]*cam\.transform\.position = new Vector3\(8\.6f, 1\.7f, gen\.PvpRoomArea\.yMin \+ 2\.3f\);\r?\n[ \t]*cam\.transform\.LookAt\(new Vector3\(2\.0f, 1\.0f, gen\.PvpRoomArea\.yMin \+ 1\.2f\)\);\r?\n[ \t]*ShotCam\(cam, "round12_pvp_hall"\);\r?\n' @'
                    cam.transform.position = new Vector3(8.8f, 1.8f, 15.6f);
                    cam.transform.LookAt(new Vector3(3.5f, 1.2f, 21.0f));
                    ShotCam(cam, "round12_pvp_north");
                    cam.transform.position = new Vector3(8.7f, 2.2f, 14.2f);
                    cam.transform.LookAt(new Vector3(2.0f, 1.0f, 16.0f));
                    ShotCam(cam, "round12_pvp_hall");
                    cam.transform.position = new Vector3(3.5f, 1.8f, gen.PvpRoomAnnex.yMin + 0.8f);
                    cam.transform.LookAt(new Vector3(3.0f, 1.2f, gen.PvpRoomAnnex.yMax - 0.5f));
                    ShotCam(cam, "round12_pvp_annex");
                    cam.transform.position = new Vector3(4.75f, 11.0f, 19.0f);
                    cam.transform.LookAt(new Vector3(4.75f, 0f, 18.5f));
                    ShotCam(cam, "round12_pvp_from_above");

'@
Save-Src

Open-Src 'C:\bqs\README_PLAYTEST.txt'
Rep 'The PVP room is the small room it was again (the warehouse version is taken out). A dart board hangs' 'The PVP room is bigger now: it goes further back behind the elevator room and out to the right (north) of its doorway, with shipping containers, pallets and crates to hide behind. A dart board hangs'
Save-Src
Write-Output 'round 12 batch G38 patched'
