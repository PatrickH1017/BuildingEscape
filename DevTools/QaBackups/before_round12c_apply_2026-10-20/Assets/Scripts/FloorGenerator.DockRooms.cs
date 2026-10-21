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

        /// <summary>The inside of the break room (x, z, width, depth): the south-west corner of the basement.</summary>
        public Rect BreakRoom { get; private set; }
        /// <summary>The inside of the PVP room: the north-west corner.</summary>
        public Rect PvpRoomArea { get; private set; }
        /// <summary>The PVP room is an L: the hall of <see cref="PvpRoomArea"/> along the north wall and this part that goes down the west side of the elevator room (the pocket that used to be closed; the owner, 2026-10-19 night: "a bit larger ... like a warehouse").</summary>
        public Rect PvpRoomAnnex { get; private set; }
        /// <summary>The deeper part of the PVP room east of the elevator room (the owner, 2026-10-19 night: "much larger ... it doesn't need to be symmetrical with the breakroom"): it joins the hall along z 13.2.</summary>
        public Rect PvpRoomBlock { get; private set; }
        /// <summary>The doorway of each room: the middle of the gap in its east wall.</summary>
        public Vector3 BreakRoomDoor { get; private set; }
        public Vector3 PvpRoomDoor { get; private set; }

        /// <summary>The middle of the TV on the break room's south wall is this far from the room's west wall (the workers' row is centred on it; six of them are five metres wide).</summary>
        const float BreakTvX = 5.8f;

        /// <summary>A wall along z at <paramref name="x"/> from <paramref name="z0"/> to <paramref name="z1"/> with a doorway whose middle is at <paramref name="gapCentre"/>.</summary>
        void DoorwayWall(float x, float z0, float z1, float gapCentre)
        {
            float g0 = gapCentre - RoomDoorWidth * 0.5f, g1 = gapCentre + RoomDoorWidth * 0.5f;
            if (g0 - z0 > 0.05f) WallZ(x, (z0 + g0) * 0.5f, g0 - z0);
            if (z1 - g1 > 0.05f) WallZ(x, (g1 + z1) * 0.5f, z1 - g1);
            float lintel = wallHeight - RoomDoorHeight;
            Box("DoorHeader", new Vector3(x, RoomDoorHeight + lintel * 0.5f, gapCentre), new Vector3(0.4f, lintel, RoomDoorWidth), wallMat);
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

        void BuildDockRooms(Transform parent)
        {
            var elev = CellCenter(Vector2Int.zero);
            float depth = rows * cellSize;
            float southWallZ = elev.z - 4f, northWallZ = elev.z + 4f;          // the elevator room's own walls (8 x 8 m)
            float westFace = elev.x - 4f;                                       // its west wall: the corner walls run out to it
            float eastX = elev.x + 0.5f;                                        // the rooms' east walls
            BreakRoom = new Rect(0.2f, 0.2f, eastX - 0.4f, southWallZ - 0.4f);                              // x 0.2..9.3, z 0.2..4.8
            float pvpEast = 23.8f, blockWallZ = 10.9f;                           // the PVP room's east wall (just past the first roller door of the bay) and the south wall of its deeper east part
            float elevEast = elev.x + 4.2f;                                       // the outer face of the elevator room's east wall
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, pvpEast - 0.4f, depth - 0.2f - (northWallZ + 0.2f)); // x 0.2..23.6, z 13.2..17.8: the hall, the whole way along the north wall
            PvpRoomBlock = new Rect(elevEast, blockWallZ + 0.2f, (pvpEast - 0.2f) - elevEast, (northWallZ + 0.2f) - (blockWallZ + 0.2f));          // x 13.2..23.6, z 11.1..13.2
            PvpRoomAnnex = new Rect(0.2f, southWallZ + 0.2f, westFace - 0.4f, (northWallZ + 0.2f) - (southWallZ + 0.2f));           // x 0.2..4.6, z 5.2..13.2: it joins the hall along z 13.2
            float breakDoorZ = 2.4f, pvpDoorZ = depth - 2.4f;
            BreakRoomDoor = new Vector3(eastX, 0f, breakDoorZ);
            PvpRoomDoor = new Vector3(pvpEast, 0f, pvpDoorZ);

            // ---- the walls: the corners' own walls on the elevator room's side, and an east wall with a doorway for each
            WallX(westFace * 0.5f, southWallZ, westFace);
            // (no wall at the elevator room's north-west corner any more: the pocket west of the elevator room is part of the PVP room now)
            DoorwayWall(eastX, 0f, southWallZ, breakDoorZ);
            WallX((elevEast + pvpEast) * 0.5f, blockWallZ, pvpEast - elevEast);        // the south wall of the east part: from the elevator room's corner to the east wall
            DoorwayWall(pvpEast, blockWallZ, depth, pvpDoorZ);                         // the east wall, with the doorway

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

            // ================= the PVP room: a warehouse (the owner, 2026-10-19 night: "a bit larger with some storage containers for players to use as coverage, maybe it can somewhat be like a warehouse (but really there for PvP
            // just looks like a warehouse to make sense for the Loading Dock vibe)"): an L (the hall along the north wall and the old pocket west of the elevator room), shipping containers, loaded pallets, crates and barrels to hide
            // behind, and the lane in front of the doorway left open
            RoomLight(parent, "PvpRoomLight", new Vector3(BreakTvX - 1.0f, 3.1f, pvpDoorZ - 0.5f), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightWest", new Vector3(1.9f, 3.1f, PvpRoomArea.yMin + 1.2f), new Color(1f, 0.85f, 0.8f), 1.0f);
            RoomLight(parent, "PvpRoomLightAnnex", new Vector3(3.6f, 3.1f, PvpRoomAnnex.center.y), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightMiddle", new Vector3(11.9f, 3.1f, 14.8f), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightEast", new Vector3(18.0f, 3.1f, 14.0f), new Color(1f, 0.85f, 0.8f), 1.2f);
            RoomLight(parent, "PvpRoomLightBlock", new Vector3(21.0f, 3.1f, 12.5f), new Color(1f, 0.85f, 0.8f), 1.0f);
            // the hall: a small container along the north wall at its west end, loaded pallets and barrels on the south side, two blocks of boxes (the middle of the hall and the way in from the doorway stay open)
            PlaceKey("container_small", 1.87f, PvpRoomArea.yMax - 1.23f, 90f, parent, 0f, true);
            PlaceKey("pallet_loaded", 2.4f, PvpRoomArea.yMin + 0.75f, 0f, parent, 0f, true);
            PlaceKey("pallet_loaded", 6.6f, PvpRoomArea.yMin + 0.75f, 90f, parent, 0f, true);
            PlaceKey("barrel", 0.55f, PvpRoomArea.yMin + 0.45f, 0f, parent, 0f, true);
            PlaceKey("barrel", 0.55f, PvpRoomArea.yMin + 1.15f, 0f, parent, 0f, true);
            BoxBlock(parent, 6.0f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);
            // boxes on pallets, as a warehouse has them
            BoxBlock(parent, 10.0f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 20.9f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 1, true);
            BoxBlock(parent, 4.5f, PvpRoomArea.yMin + 0.75f, 0f, 1, 1, 3, false);
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
            // the annex: a full-length container along the west wall, pairs of stacked crates on the east side, an aisle between them
            PlaceKey("container", 1.43f, PvpRoomAnnex.center.y, 0f, parent, 0f, true);
            foreach (float cz in new[] { PvpRoomAnnex.yMin + 1.7f, PvpRoomAnnex.yMax - 1.7f })
            {
                PlaceKey("crate_large", 4.3f, cz, 0f, parent, 0f, true);
                PlaceKey("crate_large", 4.3f, cz, 20f, parent, 0.79f, true);          // (a second one on top)
            }
        }
    }
}
