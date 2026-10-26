using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The two side rooms of the basement (the owner, 2026-10-17 / 18). The BREAK ROOM in the south-west corner: the TV that the run-start cutscene is played on hangs on its south wall (the workers watch it
    /// from the room; the old TV of the main room is gone), with a fridge, counters and a microwave, a desk, a few chairs and a door with a restroom sign. The PVP ROOM in the north-west corner: "a side room off
    /// to the back section in the loading docks that just has a few boxes in it" (the fights are <see cref="PvpRules"/>). Both are 9 x 4.6 m rooms against the west wall, south and north of the elevator
    /// room, whose own walls close them on that side; each has a doorway in its east wall.
    /// </summary>
    public partial class FloorGenerator
    {
        const float RoomDoorWidth = 1.6f, RoomDoorHeight = 2.6f;
        /// <summary>
        /// The PVP room's doorway is a FORKLIFT door (the owner, 2026-10-20: "the forklift should technically fit through it"): the lobby's forklift (<see cref="DockDressing.Forklift"/>) is 1.2 m wide and 2.2 m high, so the gap is 2.4 m wide and 2.8 m high
        /// (the break room's stays an ordinary 1.6 x 2.6 m door).
        /// </summary>
        public const float PvpDoorWidth = 2.4f, PvpDoorHeight = 2.8f;

        /// <summary>The inside of the break room (x, z, width, depth): the south-west corner of the basement.</summary>
        public Rect BreakRoom { get; private set; }
        /// <summary>The inside of the PVP room: the north-west corner.</summary>
        public Rect PvpRoomArea { get; private set; }
        /// <summary>
        /// The PVP room is an L (the owner, 2026-10-20, with a sketch: "extend it further back behind the elevator and extend out the walls wider to the right when you walk into the PvP room"): the hall of <see cref="PvpRoomArea"/> (the old room and
        /// <see cref="PvpExtension"/> metres more to the north, where it goes out through the building's north wall) and this part behind (west of) the elevator room, the pocket that used to be closed.
        /// </summary>
        public Rect PvpRoomAnnex { get; private set; }

        /// <summary>The PVP room goes out through the first cell's north wall: in the docks that wall starts at this x (east of it it stands as before) and the room reaches this many metres beyond it.</summary>
        const float PvpOpeningTo = 9.7f, PvpExtension = 6.4f;
        /// <summary>The doorway of each room: the middle of the gap in its east wall.</summary>
        public Vector3 BreakRoomDoor { get; private set; }
        public Vector3 PvpRoomDoor { get; private set; }

        /// <summary>The middle of the TV on the break room's south wall is this far from the room's west wall (the workers' row is centred on it; six of them are five metres wide).</summary>
        const float BreakTvX = 5.8f;

        /// <summary>A wall along z at <paramref name="x"/> from <paramref name="z0"/> to <paramref name="z1"/> with a doorway whose middle is at <paramref name="gapCentre"/>, <paramref name="width"/> wide and <paramref name="height"/> high (an ordinary door unless asked otherwise).</summary>
        void DoorwayWall(float x, float z0, float z1, float gapCentre, float width = RoomDoorWidth, float height = RoomDoorHeight)
        {
            float g0 = gapCentre - width * 0.5f, g1 = gapCentre + width * 0.5f;
            if (g0 - z0 > 0.05f) WallZ(x, (z0 + g0) * 0.5f, g0 - z0);
            if (z1 - g1 > 0.05f) WallZ(x, (g1 + z1) * 0.5f, z1 - g1);
            float lintel = wallHeight - height;
            Box("DoorHeader", new Vector3(x, height + lintel * 0.5f, gapCentre), new Vector3(0.4f, lintel, width), wallMat);
        }

        /// <summary>A prop against the north wall of the break room (its back to the wall, its front facing south into the room).</summary>
        float NorthZ(string key) => PropTable.TryGet(key, out var d) ? BreakRoom.yMax - d.D * 0.5f - 0.02f : BreakRoom.yMax - 0.4f;

        void RoomLight(Transform parent, string name, Vector3 pos, Color colour, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = colour; l.range = 10f; l.intensity = intensity; l.shadows = LightShadows.None;
        }

        /// <summary>One tidy block of boxes (a grid of <paramref name="cols"/> x <paramref name="rows"/>, <paramref name="layers"/> high) square to the room, on a pallet when <paramref name="pallet"/>.</summary>
        void BoxBlock(Transform parent, float x, float z, float yaw, int cols, int rowsN, int layers, bool pallet)
        {
            DockDressing.BoxBlock(dockHolder != null ? dockHolder : parent, x, z, yaw, cols, rowsN, layers, pallet, (int)(Mathf.Abs(x) * 31f + Mathf.Abs(z) * 17f));          // (the lobby's cardboard on its pallet, square to the room)
        }

        /// <summary>The PVP room's cover in the middle: the Battle Royale pack's pieces of <see cref="PvpBattlefieldRules"/> (the owner, 2026-10-23), solid, square to the room.</summary>
        void BuildPvpBattlefield(Transform parent)
        {
            foreach (var p in PvpBattlefieldRules.Pieces) PlaceKey(p.key, p.x, p.z, p.yaw, parent, 0f, true);
            BoxBlock(parent, PvpBattlefieldRules.BoxBlockX, PvpBattlefieldRules.BoxBlockZ, 0f, 2, 2, 2, true);          // (a few cardboard boxes stay: the owner, 2026-10-20, "more shelves with boxes")
        }

        void BuildDockRooms(Transform parent)
        {
            var elev = CellCenter(Vector2Int.zero);
            float depth = rows * cellSize;
            float southWallZ = elev.z - 4f, northWallZ = elev.z + 4f;          // the elevator room's own walls (8 x 8 m)
            float westFace = elev.x - 4f;                                       // its west wall: the corner walls run out to it
            float eastX = elev.x + 0.5f;                                        // the rooms' east walls
            BreakRoom = new Rect(0.2f, 0.2f, eastX - 0.4f, southWallZ - 0.4f);                              // x 0.2..9.3, z 0.2..4.8
            float pvpNorth = depth - 0.2f + PvpExtension;                                                       // the inner face of the new north wall (z 24.2)
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, eastX - 0.4f, pvpNorth - (northWallZ + 0.2f));        // x 0.2..9.3, z 13.2..24.2: the old room and the part that goes out to the north
            PvpRoomAnnex = new Rect(0.2f, southWallZ + 0.2f, westFace - 0.4f, (northWallZ + 0.2f) - (southWallZ + 0.2f));           // x 0.2..4.8, z 5.2..13.2: behind the elevator room, joined to the hall along z 13.2
            float breakDoorZ = 2.4f, pvpDoorZ = depth - 2.4f;
            BreakRoomDoor = new Vector3(eastX, 0f, breakDoorZ);
            PvpRoomDoor = new Vector3(eastX, 0f, pvpDoorZ);

            // ---- the walls: the corners' own walls on the elevator room's side, and an east wall with a doorway for each
            WallX(westFace * 0.5f, southWallZ, westFace);
            // (no wall at the elevator room's north-west corner: the pocket behind the elevator room is part of the PVP room)
            DoorwayWall(eastX, 0f, southWallZ, breakDoorZ);
            DoorwayWall(eastX, northWallZ, depth, pvpDoorZ, PvpDoorWidth, PvpDoorHeight);          // (a forklift door: PvpDoorWidth x PvpDoorHeight)
            // the part of the PVP room that goes out to the north: its floor, three walls (the building's north wall is open there, see BuildShell) and its ceiling (BuildCeiling)
            float pvpOuter = pvpNorth + 0.4f;                                                                  // the outer face of the new north wall
            Box("PvpFloorNorth", new Vector3((PvpOpeningTo - 0.2f) * 0.5f, -0.25f, (depth - 0.1f + pvpOuter) * 0.5f), new Vector3(PvpOpeningTo + 0.2f, 0.5f, pvpOuter - depth + 0.1f), floorMat);
            WallX((PvpOpeningTo - 0.2f) * 0.5f, pvpNorth + 0.2f, PvpOpeningTo + 0.2f);                         // the north wall
            WallZ(0f, (depth + pvpOuter) * 0.5f, pvpOuter - depth);                                            // the west wall, going on from the building's own
            WallZ(eastX, (depth + pvpOuter) * 0.5f, pvpOuter - depth);                                         // the east wall, going on from the doorway wall

            // ================= the break room
            DockTvPoint = new Vector3(BreakTvX, 0f, BreakRoom.yMin);
            if (Application.isPlaying) DockTv.Build(parent, DockTvPoint);                         // (the TV of the run-start cutscene hangs here now, on the south wall, facing north into the room)
            RoomLight(parent, "BreakRoomLight", new Vector3(BreakTvX - 1.2f, 3.1f, 2.5f), new Color(1f, 0.93f, 0.8f), 1.2f);
            PlaceKey("fridge", 1.0f, NorthZ("fridge"), 180f, parent, 0f, true);
            for (int i = 0; i < 3; i++) PlaceKey("counter_a2", 2.25f + i * 1.0f, NorthZ("counter_a2"), 180f, parent, 0f, true);
            PlaceKey("microwave", 2.25f, NorthZ("counter_a2"), 180f, parent, 0.95f, false);          // (on the first counter)
            // the desk against the west wall with its chair, and two more chairs beside it
            float deskX = 0.2f + (PropTable.TryGet("desk_1", out var dd) ? dd.D * 0.5f : 0.44f) + 0.02f;
            PlaceKey("desk_1", deskX, 1.5f, 90f, parent, 0f, true);
            PlaceKey("chair_cafe", deskX + 0.9f, 1.5f, 270f, parent, 0f, false);
            PlaceKey("chair_cafe", 0.75f, 3.0f, 90f, parent, 0f, false);
            PlaceKey("chair_cafe", 0.75f, 3.55f, 90f, parent, 0f, false);
            // the door with the restroom sign, in the north wall (the elevator room's south face) at its east end
            var doorMat = Flat(new Color(0.34f, 0.25f, 0.18f), 0.2f);
            float rdx = 8.3f, rdz = BreakRoom.yMax - 0.04f;
            Box("RestroomDoor", new Vector3(rdx, 1.05f, rdz), new Vector3(0.95f, 2.1f, 0.08f), doorMat, parent, collider: false);
            Box("RestroomDoorHandle", new Vector3(rdx + 0.34f, 1.0f, rdz - 0.07f), new Vector3(0.04f, 0.04f, 0.1f), Flat(new Color(0.8f, 0.8f, 0.82f), 0.5f), parent, collider: false);
            PlaceKey("sign_wc", rdx, BreakRoom.yMax - 0.02f, 180f, parent, 2.25f, false);
            // two posters (the owner, 2026-10-24: "please put a poster or two up in the Loading Docks break room, reference image attached for which spots"): the west wall over the desk and the south wall between the TV and the corner (DockPosterRules)
            foreach (var poster in DockPosterRules.BreakRoom(BreakRoom.xMin, BreakRoom.yMin, BreakRoom.xMax, BreakRoom.yMax))
            {
                if (!PropTable.TryGet(poster.key, out var pd)) continue;
                float outX = Mathf.Sin(poster.yaw * Mathf.Deg2Rad), outZ = Mathf.Cos(poster.yaw * Mathf.Deg2Rad);          // (the way it faces: into the room)
                PlaceKey(poster.key, poster.x + outX * (pd.D * 0.5f + 0.005f), poster.z + outZ * (pd.D * 0.5f + 0.005f), poster.yaw, parent, poster.centreY - pd.H * 0.5f, false);
            }

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
            bool battlefield = PvpBattlefieldRules.On;                                                                                        // (T7, the owner, 2026-10-23: the cover in the middle is the Battle Royale pack's: PvpBattlefieldRules; false = the racks and cardboard of before)
            if (!battlefield) RackRow(4.75f - 1.83f, 4.75f + 1.83f, 21.9f, 3);                                                                                  // a row north of the mat
            if (!battlefield) RackRow(2.05f + halfLen, 5.65f - halfLen, 15.5f, 2);                                                                              // a row south of it, west of the doorway's lane
            if (!battlefield) RackColumn(2.3f, PvpRoomAnnex.center.y - 1.83f, PvpRoomAnnex.center.y + 1.83f, 3);                                              // a row down the middle of the pocket

            // lower cover: stacked crates and two blocks of boxes on the pocket's east side, loaded pallets and crates on the hall's east side, two blocks of boxes on pallets by the west line (the way in from the doorway and the middle stay open)
            foreach (float cz in new[] { PvpRoomAnnex.yMin + 2.2f, PvpRoomAnnex.yMax - 2.2f })
            {
                PlaceKey("crate_large", 4.2f, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", 4.2f, cz, 20f, parent, 0.79f, true);          // (a second one on top, turned: its corners stay inside the wall)
            }
            BoxBlock(parent, 4.15f, PvpRoomAnnex.yMin + 0.52f, 0f, 2, 2, 1, true);                  // the pocket's east side: a block of boxes on a pallet in its south-east corner ...
            BoxBlock(parent, 4.15f, PvpRoomAnnex.center.y, 0f, 2, 2, 2, true);                       // ... and a taller one in the middle, between the crate pairs
            if (!battlefield) PlaceKey("pallet_loaded", 7.9f, 18.2f, 90f, parent, 0f, true);
            if (!battlefield) PlaceKey("pallet_loaded", 7.9f, 20.2f, 0f, parent, 0f, true);
            if (!battlefield) PlaceKey("crate_large", 8.7f, 21.6f, 0f, parent, 0f, true);
            if (!battlefield) PlaceKey("crate_large", 8.7f, 22.4f, 0f, parent, 0f, true);
            if (!battlefield) BoxBlock(parent, 1.9f, 16.85f, 0f, 2, 2, 2, true);
            if (!battlefield) BoxBlock(parent, 1.9f, 19.9f, 0f, 2, 2, 1, true);
            if (battlefield) BuildPvpBattlefield(parent);
        }
    }
}
