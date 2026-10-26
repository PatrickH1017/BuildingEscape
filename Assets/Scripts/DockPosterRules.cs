namespace BuildingEscape
{
    /// <summary>
    /// The posters of the Loading Docks' break room (the owner, 2026-10-24: "please put a poster or two up in the Loading Docks break room, reference image attached for which spots": a picture of the room with two spots circled in red). Without Unity types, so it is tested outside Unity.
    /// The two spots: the room's WEST wall (the one behind the desk and the two chairs), above the desk, and the room's SOUTH wall (the one the TV hangs on), between the TV and the south-west corner. A poster hangs with its back on the wall's face, its middle at <see cref="Spot.centreY"/> above the floor.
    /// </summary>
    public static class DockPosterRules
    {
        public struct Spot
        {
            /// <summary>The prop (<c>PropTable</c> key).</summary>
            public string key;
            /// <summary>The point of the wall's face the poster's middle is over (floor plan), and which way it faces (degrees: 0 = +z, 90 = +x, 180 = -z, 270 = -x: the way the poster looks into the room).</summary>
            public float x, z, yaw;
            /// <summary>The height of the poster's middle above the floor (m).</summary>
            public float centreY;
        }

        /// <summary>How far along the west wall (from its south end) and along the south wall (from its west end) the two posters hang: 0.40 of 4.6 m is z 2.0 (over the desk), 0.36 of 9.1 m is x 3.5 (two metres west of the TV).</summary>
        public const float WestAlong = 0.40f, SouthAlong = 0.36f;
        /// <summary>The posters' middles (m above the floor): eye height and a little above, over the desk and clear of the ceiling.</summary>
        public const float WestHeight = 1.85f, SouthHeight = 1.90f;

        /// <summary>The posters of a break room whose inside is x0..x1 by z0..z1 (the south-west corner of the basement).</summary>
        public static Spot[] BreakRoom(float x0, float z0, float x1, float z1)
        {
            return new[]
            {
                new Spot { key = "poster_1", x = x0, z = z0 + (z1 - z0) * WestAlong, yaw = 90f, centreY = WestHeight },
                new Spot { key = "poster_3", x = x0 + (x1 - x0) * SouthAlong, z = z0, yaw = 0f, centreY = SouthHeight },
            };
        }
    }
}
