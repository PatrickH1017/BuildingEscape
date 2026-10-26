using System;

namespace BuildingEscape
{
    /// <summary>
    /// The PVP room as a battlefield (the owner, 2026-10-23: "The PvP room should use the battle royale assets from SYNTY to make it seem more like a battlefield"). The room (<c>FloorGenerator.DockRooms.cs</c>: the hall x 0.2..9.3, z 13.2..24.2 and the pocket
    /// x 0.2..4.8, z 5.2..13.2 behind the elevator room) keeps its steel shelving along the walls (the owner, 2026-10-20: "more shelves with boxes") and its crates, but the cover in the middle is the Battle Royale pack's: gabion barriers (the cubes of Barrier_01..03 and the long
    /// stack of Barrier_04), concrete block walls, tank traps, a wire spool and drums, all square to the room (the owner's rule of orderly rooms). Pure data: the list, the footprints and the rules the list obeys; the generator places the pieces
    /// (<c>FloorGenerator.BuildPvpBattlefield</c>), <see cref="On"/> switches the whole thing off (the old cardboard and rack cover comes back).
    /// The rules: every piece inside the room, no two pieces overlapping, the red mat (where the fighters start) free, and the lane in front of the forklift-sized doorway free.
    /// </summary>
    public static class PvpBattlefieldRules
    {
        /// <summary>The switch: false builds the room as it was before 2026-10-23 (racks and cardboard in the middle).</summary>
        public const bool On = true;

        public readonly struct Piece
        {
            public readonly string key; public readonly float x, z, yaw;
            public Piece(string key, float x, float z, float yaw = 0f) { this.key = key; this.x = x; this.z = z; this.yaw = yaw; }
        }

        // the room, as FloorGenerator.BuildDockRooms makes it (a Play test compares these with the generator's own PvpRoomArea / PvpRoomAnnex)
        public const float HallX0 = 0.2f, HallX1 = 9.3f, HallZ0 = 13.2f, HallZ1 = 24.2f;
        public const float PocketX0 = 0.2f, PocketX1 = 4.8f, PocketZ0 = 5.2f, PocketZ1 = 13.2f;
        /// <summary>The red mat: x 2.65..6.85, z 17.0..20.4. The lane of the doorway: east of x 5.6, z 14.4..16.8.</summary>
        public const float MatX0 = 2.65f, MatX1 = 6.85f, MatZ0 = 17.0f, MatZ1 = 20.4f;
        public const float LaneX0 = 5.6f, LaneX1 = 9.3f, LaneZ0 = 14.4f, LaneZ1 = 16.8f;
        /// <summary>Where the block of cardboard boxes on a pallet stands (north-west corner; R7 wants a few boxes in the room).</summary>
        public const float BoxBlockX = 1.95f, BoxBlockZ = 22.7f;

        /// <summary>The pieces (the prop keys are in <see cref="PropTable"/>; the sizes there are the Battle Royale models' own, measured). Yaw 0 or 90 only.</summary>
        public static readonly Piece[] Pieces =
        {
            // east side: the long barrier wall (5.5 x 1.4 m, 2.1 m high), the one big piece of cover; two drums beyond its north end
            new Piece("hesco_wall", 8.40f, 20.00f, 90f),
            new Piece("barrel", 7.95f, 23.15f), new Piece("barrel", 8.70f, 23.15f),
            // north: two concrete block walls with a gap of about a metre in the middle (the north-west corner holds a block of cardboard boxes on a pallet, see BoxBlockX / BoxBlockZ)
            new Piece("cinder_wall", 2.70f, 21.50f), new Piece("cinder_wall", 6.40f, 21.50f),
            // (the block of boxes at BoxBlockX / BoxBlockZ: not a Piece, it is the lobby's own cardboard)
            // west of the mat: a line of three gabion cubes
            new Piece("hesco_a", 1.85f, 17.90f), new Piece("hesco_b", 1.85f, 19.30f), new Piece("hesco_c", 1.85f, 20.70f),
            // south of the mat: two tank traps
            new Piece("tank_trap", 2.40f, 15.50f), new Piece("tank_trap", 4.40f, 15.50f),
        };

        /// <summary>The ground a piece covers: its width x depth from <see cref="PropTable"/>, turned by its yaw. False when the key is not in the table.</summary>
        public static bool Footprint(in Piece p, out float x0, out float x1, out float z0, out float z1)
        {
            x0 = x1 = z0 = z1 = 0f;
            if (!PropTable.TryGet(p.key, out var d)) return false;
            bool turned = ((int)Math.Round(p.yaw / 90f) & 1) != 0;
            float hw = (turned ? d.D : d.W) * 0.5f, hd = (turned ? d.W : d.D) * 0.5f;
            x0 = p.x - hw; x1 = p.x + hw; z0 = p.z - hd; z1 = p.z + hd;
            return true;
        }

        /// <summary>Do two rectangles overlap by more than <paramref name="slack"/> metres in both directions?</summary>
        public static bool Overlap(float ax0, float ax1, float az0, float az1, float bx0, float bx1, float bz0, float bz1, float slack = 0.02f)
            => ax0 < bx1 - slack && bx0 < ax1 - slack && az0 < bz1 - slack && bz0 < az1 - slack;

        static bool Inside(float x0, float x1, float z0, float z1, float rx0, float rx1, float rz0, float rz1) => x0 >= rx0 && x1 <= rx1 && z0 >= rz0 && z1 <= rz1;

        /// <summary>What is wrong with the list: "" when all is well, else the first problem found (the pure check calls it).</summary>
        public static string Problems()
        {
            for (int i = 0; i < Pieces.Length; i++)
            {
                var p = Pieces[i];
                if (!Footprint(p, out float x0, out float x1, out float z0, out float z1)) return $"piece {i} ({p.key}) is not in the prop table";
                if (((int)Math.Round(p.yaw / 90f) * 90) != (int)Math.Round(p.yaw)) return $"piece {i} ({p.key}) is not square to the room (yaw {p.yaw})";
                if (!Inside(x0, x1, z0, z1, HallX0, HallX1, HallZ0, HallZ1) && !Inside(x0, x1, z0, z1, PocketX0, PocketX1, PocketZ0, PocketZ1)) return $"piece {i} ({p.key}) is not inside the room ({x0:0.00}..{x1:0.00}, {z0:0.00}..{z1:0.00})";
                if (Overlap(x0, x1, z0, z1, MatX0, MatX1, MatZ0, MatZ1)) return $"piece {i} ({p.key}) is on the mat";
                if (Overlap(x0, x1, z0, z1, LaneX0, LaneX1, LaneZ0, LaneZ1)) return $"piece {i} ({p.key}) is in the doorway's lane";
                for (int j = i + 1; j < Pieces.Length; j++)
                {
                    if (!Footprint(Pieces[j], out float a0, out float a1, out float b0, out float b1)) continue;
                    if (Overlap(x0, x1, z0, z1, a0, a1, b0, b1)) return $"pieces {i} ({p.key}) and {j} ({Pieces[j].key}) overlap";
                }
            }
            return "";
        }
    }
}
