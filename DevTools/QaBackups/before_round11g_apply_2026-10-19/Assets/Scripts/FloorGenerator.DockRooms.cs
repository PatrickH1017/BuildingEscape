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
            float bw = 0.52f, bd = 0.50f, bh = 0.42f;
            if (PropTable.TryGet("box", out var bdef)) { bw = bdef.W; bd = bdef.D; bh = bdef.H; }
            float baseY = 0f;
            if (pallet && PropTable.TryGet("pallet", out var pdef)) { PlaceKey("pallet", x, z, yaw, parent, 0f, true); baseY = pdef.H; }
            var rot = Quaternion.Euler(0f, yaw, 0f);
            for (int layer = 0; layer < layers; layer++)
                for (int i = 0; i < cols; i++)
                    for (int j = 0; j < rowsN; j++)
                    {
                        var off = rot * new Vector3((i - (cols - 1) * 0.5f) * (bw + 0.01f), 0f, (j - (rowsN - 1) * 0.5f) * (bd + 0.01f));
                        PlaceKey("box", x + off.x, z + off.z, yaw, parent, baseY + layer * bh, layer == 0 && !pallet);
                    }
        }

        void BuildDockRooms(Transform parent)
        {
            var elev = CellCenter(Vector2Int.zero);
            float depth = rows * cellSize;
            float southWallZ = elev.z - 4f, northWallZ = elev.z + 4f;          // the elevator room's own walls (8 x 8 m)
            float westFace = elev.x - 4f;                                       // its west wall: the corner walls run out to it
            float eastX = elev.x + 0.5f;                                        // the rooms' east walls
            BreakRoom = new Rect(0.2f, 0.2f, eastX - 0.4f, southWallZ - 0.4f);                              // x 0.2..9.3, z 0.2..4.8
            PvpRoomArea = new Rect(0.2f, northWallZ + 0.2f, eastX - 0.4f, depth - 0.2f - (northWallZ + 0.2f)); // x 0.2..9.3, z 13.2..17.8
            float breakDoorZ = 2.4f, pvpDoorZ = depth - 2.4f;
            BreakRoomDoor = new Vector3(eastX, 0f, breakDoorZ);
            PvpRoomDoor = new Vector3(eastX, 0f, pvpDoorZ);

            // ---- the walls: the corners' own walls on the elevator room's side, and an east wall with a doorway for each
            WallX(westFace * 0.5f, southWallZ, westFace);
            WallX(westFace * 0.5f, northWallZ, westFace);
            DoorwayWall(eastX, 0f, southWallZ, breakDoorZ);
            DoorwayWall(eastX, northWallZ, depth, pvpDoorZ);

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

            // ================= the PVP room: a few boxes and a red mat to fight on
            RoomLight(parent, "PvpRoomLight", new Vector3(BreakTvX - 1.0f, 3.1f, pvpDoorZ - 0.5f), new Color(1f, 0.85f, 0.8f), 1.2f);
            var mat = Flat(new Color(0.55f, 0.08f, 0.08f), 0.1f);
            Box("PvpMat", new Vector3(4.75f, 0.012f, (PvpRoomArea.yMin + PvpRoomArea.yMax) * 0.5f), new Vector3(6.0f, 0.024f, 3.4f), mat, parent, collider: false);
            BoxBlock(parent, 1.3f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 2, true);
            BoxBlock(parent, 8.1f, PvpRoomArea.yMax - 0.95f, 0f, 2, 2, 1, true);
            BoxBlock(parent, 0.75f, PvpRoomArea.yMin + 0.85f, 0f, 1, 2, 2, false);
            BoxBlock(parent, 8.5f, PvpRoomArea.yMin + 0.8f, 0f, 1, 1, 3, false);
        }
    }
}
