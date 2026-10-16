using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// Room numbers like a real building's: floor 1 has 101, 102, ..., floor 2 has 201, 202, ... (floor 12: 1201, ...). Numbers run
    /// 01 to 99 on a floor; a floor with more rooms than that goes round again with a letter (101A, 102A, ...), the way an annex is
    /// numbered, so the number always starts with the floor and is always the same length. Plain data and arithmetic (no Unity
    /// types) so it can be tested outside Unity. <see cref="Plates"/> says which number goes where; FloorGenerator draws them.
    /// Everything follows from the layout, which every machine builds identically, so every player sees the same numbers.
    /// </summary>
    public static class RoomNumbers
    {
        /// <summary>Rooms numbered before the numbers go round again with a letter.</summary>
        public const int RoomsPerRound = 99;

        /// <summary>A number plate: where it hangs and which way it faces.</summary>
        public sealed class Plate
        {
            /// <summary>What the plate says: "101", or "101A" in the second round of a big floor.</summary>
            public string label;
            /// <summary>The middle of the plate on the outer face of the beam over the doorway (x, z), and the direction it faces: away from
            /// the room, into the corridor. y0 / y1: the bottom and top of that beam.</summary>
            public float x, z, nx, nz, y0, y1;
        }

        /// <summary>Does this room get a number? Offices, meeting rooms, break rooms, storage, server rooms and so on do (they have a door);
        /// corridors, lobbies and open-plan areas are not rooms with a door.</summary>
        public static bool IsNumbered(RoomInfo r) => r != null && r.kind == 0 && r.doors.Count > 0 && r.theme != "outpost";

        /// <summary>The label of a room of a floor (101, 102, ... in the order the planner made the rooms), or null if the room has no number.</summary>
        public static string LabelOf(Layout layout, RoomInfo room, int floor)
        {
            if (layout == null || room == null || floor < 1 || !IsNumbered(room)) return null;
            int index = 0;
            foreach (var r in layout.rooms)
            {
                if (!IsNumbered(r)) continue;
                index++;
                if (ReferenceEquals(r, room)) return Label(floor, index);
            }
            return null;
        }

        /// <summary>The label of the <paramref name="index"/>th numbered room of a floor (1, 2, ...): 101 ... 199, then 101A ... 199A, 101B ...</summary>
        public static string Label(int floor, int index)
        {
            int round = (index - 1) / RoomsPerRound, inRound = (index - 1) % RoomsPerRound + 1;
            string label = (floor * 100 + inRound).ToString();
            return round == 0 ? label : label + (char)('A' + (round - 1) % 26);
        }

        /// <summary>Every plate of a floor: one over each doorway of each numbered room, in the order the planner made the rooms
        /// (west to east, south to north) so the numbers grow the way you walk away from the elevator.</summary>
        public static List<Plate> Plates(Layout layout, int floor)
        {
            var plates = new List<Plate>();
            if (layout == null || floor < 1) return plates;
            int index = 0;
            foreach (var room in layout.rooms)
            {
                if (!IsNumbered(room)) continue;
                string label = Label(floor, ++index);
                foreach (var door in room.doors)
                {
                    // the beam that was built over this doorway (the plate hangs on its face, wherever the wall really stands)
                    var beam = BeamOver(layout, door);
                    if (beam == null) continue;

                    // the doorway is cut into one edge of the room: the plate faces away from the room's middle
                    float dx = (door.Cx - room.bounds.Cx) / (room.bounds.W * 0.5f);
                    float dz = (door.Cz - room.bounds.Cz) / (room.bounds.D * 0.5f);
                    float nx = 0f, nz = 0f;
                    bool alongX = System.Math.Abs(dx) > System.Math.Abs(dz);
                    if (alongX) nx = dx > 0f ? 1f : -1f; else nz = dz > 0f ? 1f : -1f;
                    var rc = beam.rc;
                    float x = alongX ? (nx > 0f ? rc.x1 : rc.x0) : rc.Cx;
                    float z = alongX ? rc.Cz : (nz > 0f ? rc.z1 : rc.z0);
                    plates.Add(new Plate { label = label, x = x, z = z, nx = nx, nz = nz, y0 = beam.y0, y1 = beam.y1 });
                }
            }
            return plates;
        }

        /// <summary>The header beam over a doorway: the "header" box that overlaps it most closely.</summary>
        static WallBox BeamOver(Layout layout, Rc door)
        {
            WallBox best = null;
            float bestDistance = float.MaxValue;
            var area = door.Grow(0.05f);
            foreach (var w in layout.walls)
            {
                if (w.kind != "header" || !w.rc.Overlaps(area)) continue;
                float d = System.Math.Abs(w.rc.Cx - door.Cx) + System.Math.Abs(w.rc.Cz - door.Cz);
                if (d < bestDistance) { bestDistance = d; best = w; }
            }
            return best;
        }

        // ------------------------------------------------------------------ lettering (a 3 x 5 dot font: no fonts needed)

        public const int GlyphColumns = 3, GlyphRows = 5;

        const string FontChars = "0123456789ABCDEFGHIJ";

        static readonly string[] Font =
        {
            "111101101101111",   // 0
            "010110010010111",   // 1
            "111001111100111",   // 2
            "111001111001111",   // 3
            "101101111001001",   // 4
            "111100111001111",   // 5
            "111100111101111",   // 6
            "111001010010010",   // 7
            "111101111101111",   // 8
            "111101111001111",   // 9
            "010101111101101",   // A
            "110101110101110",   // B
            "011100100100011",   // C
            "110101101101110",   // D
            "111100110100111",   // E
            "111100110100100",   // F
            "011100101101011",   // G
            "101101111101101",   // H
            "111010010010111",   // I
            "001001001101010",   // J
        };

        /// <summary>Is this dot of the character (a digit or a letter A-J) lit? Row 0 is the top row, column 0 the left one.</summary>
        public static bool Lit(char ch, int column, int row)
        {
            int at = FontChars.IndexOf(ch);
            if (at < 0 || column < 0 || column >= GlyphColumns || row < 0 || row >= GlyphRows) return false;
            return Font[at][row * GlyphColumns + column] == '1';
        }
    }
}
