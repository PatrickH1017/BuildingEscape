using System;

namespace BuildingEscape
{
    /// <summary>
    /// Where the vending machines of the Loading Dock stand (the owner, 2026-10-22: "add them both to the main room of the loading dock and add the smaller black one to the break room ... In the main room I want them on the wall
    /// across from the vendor"). Plain numbers and no Unity types, so the layout can be tested outside the editor; <c>FloorGenerator.BuildDockVending</c> puts the props where these rules say.
    /// A machine's yaw is the one <c>FloorGenerator.PlaceKey</c> takes: 0 faces north (+z), 90 east (+x), 180 south, 270 west.
    /// </summary>
    public static class DockVendingRules
    {
        /// <summary>The back of a machine stands this far off its wall.</summary>
        public const float WallGap = 0.02f;
        /// <summary>The two machines of the main room stand this far apart.</summary>
        public const float SideGap = 0.15f;
        /// <summary>The break room's machine stands this far from the corner it is nearest to.</summary>
        public const float CornerGap = 0.1f;
        /// <summary>Kept clear of storage in front of a machine, measured from its front face (the owner, 2026-10-22: "remove the boxes and red and blue barrel in front of the vending machines": the first 1.5 m left drums and box stacks standing 2 to 4 m in front of them).</summary>
        public const float FrontRoom = 5.0f;
        /// <summary>Kept clear of storage at each side of a machine (a shelf or a box stack right beside it crowds it). Not more: the free floor of the docks that can hold a forklift or a pallet block is small, and a wider strip here costs the scatter its minimums.</summary>
        public const float SideRoom = 0.6f;
        /// <summary>Kept clear behind a machine (the wall is there).</summary>
        public const float BackRoom = 0.2f;

        /// <summary>One machine: the middle of its footprint on the floor, the way it faces, and how wide (across its front) and deep (front to back) it is.</summary>
        public struct Spot
        {
            public float x, z, yaw, width, depth;

            /// <summary>Turned a quarter: its width runs along z.</summary>
            public bool Turned => (((int)Math.Round(yaw / 90f)) & 1) != 0;
            public float XMin => x - (Turned ? depth : width) * 0.5f;
            public float XMax => x + (Turned ? depth : width) * 0.5f;
            public float ZMin => z - (Turned ? width : depth) * 0.5f;
            public float ZMax => z + (Turned ? width : depth) * 0.5f;
            /// <summary>The unit step the front looks along (x, z).</summary>
            public void Front(out int fx, out int fz)
            {
                int q = ((int)Math.Round(yaw / 90f)) & 3;
                fx = q == 1 ? 1 : q == 3 ? -1 : 0;
                fz = q == 0 ? 1 : q == 2 ? -1 : 0;
            }
        }

        /// <summary>
        /// The main room: both machines side by side on the wall across from the vendor (the south wall, the vendor's trailer being backed into the north one), facing the room, the pair centred on the vendor's x. The black one is on the
        /// west, the red one on the east. <paramref name="wallZ"/> is the inner face of the wall.
        /// </summary>
        public static void MainRoom(float vendorX, float wallZ, float blackWidth, float blackDepth, float redWidth, float redDepth, out Spot black, out Spot red)
        {
            float total = blackWidth + SideGap + redWidth;
            float left = vendorX - total * 0.5f;
            black = new Spot { x = left + blackWidth * 0.5f, z = wallZ + WallGap + blackDepth * 0.5f, yaw = 0f, width = blackWidth, depth = blackDepth };
            red = new Spot { x = left + blackWidth + SideGap + redWidth * 0.5f, z = wallZ + WallGap + redDepth * 0.5f, yaw = 0f, width = redWidth, depth = redDepth };
        }

        /// <summary>
        /// The break room: the black machine against the east wall (<paramref name="eastWallX"/> is that wall's inner face), as far south as it goes, facing west into the room. <paramref name="southWallZ"/> is the inner face of the south wall.
        /// </summary>
        public static Spot BreakRoom(float eastWallX, float southWallZ, float width, float depth) =>
            new Spot { x = eastWallX - WallGap - depth * 0.5f, z = southWallZ + CornerGap + width * 0.5f, yaw = 270f, width = width, depth = depth };

        /// <summary>Do two footprints overlap (by more than <paramref name="margin"/>)?</summary>
        public static bool Overlap(Spot a, Spot b, float margin = 0f) =>
            a.XMin < b.XMax - margin && b.XMin < a.XMax - margin && a.ZMin < b.ZMax - margin && b.ZMin < a.ZMax - margin;

        /// <summary>The floor a machine keeps free of storage: its footprint, <see cref="SideRoom"/> at each side, <see cref="BackRoom"/> behind it and <see cref="FrontRoom"/> in front of its front face.</summary>
        public static void KeepClear(Spot s, out float xMin, out float zMin, out float xMax, out float zMax)
        {
            xMin = s.XMin; xMax = s.XMax; zMin = s.ZMin; zMax = s.ZMax;
            s.Front(out int fx, out int fz);
            if (fz != 0)                                                                    // facing north or south: the sides are east and west
            {
                xMin -= SideRoom; xMax += SideRoom;
                if (fz > 0) { zMin -= BackRoom; zMax += FrontRoom; } else { zMax += BackRoom; zMin -= FrontRoom; }
            }
            else                                                                            // facing east or west: the sides are north and south
            {
                zMin -= SideRoom; zMax += SideRoom;
                if (fx > 0) { xMin -= BackRoom; xMax += FrontRoom; } else { xMax += BackRoom; xMin -= FrontRoom; }
            }
        }
    }
}
