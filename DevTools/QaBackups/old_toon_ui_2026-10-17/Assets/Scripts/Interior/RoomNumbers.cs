using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// Room numbers like a real building's: floor 1 has 101, 102, ..., floor 2 has 201, 202, ... (floor 12: 1201, ...). Numbers run
    /// 01 to 99 on a floor; a floor with more rooms than that goes round again with a letter (101A, 102A, ...), the way an annex is
    /// numbered, so the number always starts with the floor and is always the same length. Plain data and arithmetic (no Unity
    /// types) so it can be tested outside Unity. <see cref="Plates"/> says which number goes where; FloorGenerator draws them.
    /// Everything follows from the layout, which every machine builds identically, so every player sees the same numbers.
    ///
    /// The ORDER of the numbers follows the corridors, so a player can follow them (see <see cref="Of"/>): 101 is the first room you meet
    /// leaving the elevator and the numbers keep rising as you walk away from it; at a fork the branch on the left is numbered first, then
    /// the one straight on, then the one on the right; inside a room-lined cell the numbers go along the corridor and pair up across it
    /// (the left side first). The rooms of a cell are never split between two rounds of numbers, and a new round (the letter) only starts where a
    /// branch starts, so a corridor is never cut in two by it.
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
            /// <summary>The number of an executive suite or of the server room: no plate, gold lettering straight on the wall over the door, "almost as if painted" (the owner).</summary>
            public bool painted;
        }

        /// <summary>Does this room get a number? Offices, meeting rooms, break rooms, storage, server rooms and so on do (they have a door);
        /// corridors, lobbies and open-plan areas are not rooms with a door.</summary>
        public static bool IsNumbered(RoomInfo r) => r != null && r.kind == 0 && r.doors.Count > 0 && r.theme != "outpost" && r.theme != "restroom";

        /// <summary>A floor's numbered rooms in the order of their numbers, with each room's label and round (0 = no letter, 1 = A, ...).</summary>
        public sealed class Numbering
        {
            public readonly List<RoomInfo> rooms = new List<RoomInfo>();
            public readonly List<string> labels = new List<string>();
            public readonly List<int> rounds = new List<int>();

            /// <summary>The place of a room in the order of the numbers (0-based), or -1.</summary>
            public int IndexOf(RoomInfo room)
            {
                for (int i = 0; i < rooms.Count; i++) if (ReferenceEquals(rooms[i], room)) return i;
                return -1;
            }
        }

        /// <summary>
        /// The numbering of a floor. The cells are walked like the corridors are: from the elevator's cell, along the shortest ways through the
        /// doorways of the maze (so every cell hangs off the cell nearer the elevator that it is reached through); the rooms of a cell are
        /// numbered when the walk reaches it, then its branches one after the other. At a fork the branch that turns left (as you walk away from
        /// the elevator) comes first, then straight on, then right; at the elevator itself the way its door faces comes first (east, else north),
        /// then the other. Inside a cell the rooms go along the way you came in, and the two sides of the corridor are interleaved (the left side
        /// first). So along any walk away from the elevator the numbers only ever go up, and neighbouring numbers are neighbouring rooms, except
        /// where a branch ends and the next one starts.
        /// </summary>
        public static Numbering Of(Layout layout, int floor)
        {
            var n = new Numbering();
            if (layout == null) return n;
            var all = new List<RoomInfo>();
            foreach (var r in layout.rooms) if (IsNumbered(r)) all.Add(r);
            if (all.Count == 0) return n;
            Walk(layout, all, new Emitter(n, floor));
            return n;
        }

        /// <summary>The label of a room of a floor, or null if the room has no number.</summary>
        public static string LabelOf(Layout layout, RoomInfo room, int floor)
        {
            if (layout == null || room == null || floor < 1 || !IsNumbered(room)) return null;
            var n = Of(layout, floor);
            int i = n.IndexOf(room);
            return i < 0 ? null : n.labels[i];
        }

        /// <summary>The label of the <paramref name="index"/>th number of a floor (1, 2, ...) when they run without a gap: 101 ... 199, then 101A ... 199A, 101B ...</summary>
        public static string Label(int floor, int index)
        {
            int round = (index - 1) / RoomsPerRound, inRound = (index - 1) % RoomsPerRound + 1;
            return LabelAt(floor, inRound, round);
        }

        /// <summary>The label for number <paramref name="number"/> (1-99) of a round (0 = no letter, 1 = A, ...): floor 3, number 7 = "307", round 1 = "307A".</summary>
        public static string LabelAt(int floor, int number, int round)
        {
            string label = (floor * 100 + number).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return round <= 0 ? label : label + (char)('A' + (round - 1) % 26);
        }

        /// <summary>Every plate of a floor: one over each doorway of each numbered room, in the order of the numbers.</summary>
        public static List<Plate> Plates(Layout layout, int floor)
        {
            var plates = new List<Plate>();
            if (layout == null || floor < 1) return plates;
            var numbering = Of(layout, floor);
            for (int i = 0; i < numbering.rooms.Count; i++)
            {
                var room = numbering.rooms[i];
                string label = numbering.labels[i];
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
                    plates.Add(new Plate { label = label, x = x, z = z, nx = nx, nz = nz, y0 = beam.y0, y1 = beam.y1, painted = room.role != null });
                }
            }
            return plates;
        }

        // ------------------------------------------------------------------ the walk

        /// <summary>Hands out the numbers as the walk meets rooms: which round it is in and how many numbers of it are used.</summary>
        sealed class Emitter
        {
            readonly Numbering n;
            readonly int floor;
            int round, used;

            public Emitter(Numbering n, int floor) { this.n = n; this.floor = floor; }

            /// <summary>Starts the next round (the letter) now if <paramref name="count"/> more rooms would not fit in this one.</summary>
            public void RoundBreakIfNeeded(int count)
            {
                if (used > 0 && used + count > RoomsPerRound) { round++; used = 0; }
            }

            /// <summary>Numbers the rooms of one cell (they are never split between two rounds).</summary>
            public void Add(List<RoomInfo> group)
            {
                RoundBreakIfNeeded(group.Count);
                foreach (var room in group)
                {
                    if (used >= RoomsPerRound) { round++; used = 0; }
                    used++;
                    n.rooms.Add(room); n.rounds.Add(round); n.labels.Add(LabelAt(floor, used, round));
                }
            }
        }

        // the four steps from a cell to its neighbours (x east, y north), in the order a cell looks for the neighbour it hangs off
        static readonly int[] StepX = { -1, 0, 1, 0 }, StepY = { 0, -1, 0, 1 };

        static void Walk(Layout layout, List<RoomInfo> all, Emitter em)
        {
            var input = layout.input;
            int cols = input != null ? input.cols : 0, rows = input != null ? input.rows : 0;
            bool maze = input != null && cols > 0 && rows > 0 && input.openE != null && input.openN != null;

            // the rooms of each cell (rooms outside the grid, or all of them when the layout does not know its maze, keep the planner's order)
            var byCell = maze ? new List<RoomInfo>[cols * rows] : null;
            var loose = new List<RoomInfo>();
            foreach (var r in all)
            {
                if (!maze || r.cellX < 0 || r.cellY < 0 || r.cellX >= cols || r.cellY >= rows) { loose.Add(r); continue; }
                int c = r.cellX + r.cellY * cols;
                (byCell[c] ?? (byCell[c] = new List<RoomInfo>())).Add(r);
            }

            if (maze)
            {
                float size = input.cell > 0f ? input.cell : 18f;
                int cellCount = cols * rows;
                int root = System.Math.Max(0, System.Math.Min(cols - 1, input.elevC)) + System.Math.Max(0, System.Math.Min(rows - 1, input.elevR)) * cols;
                var dist = new int[cellCount];
                var parent = new int[cellCount];
                for (int i = 0; i < cellCount; i++) { dist[i] = -1; parent[i] = -1; }
                var order = new List<int>(cellCount);          // the cells in the order the walk through the doorways reaches them (nearest first)
                var queue = new Queue<int>();
                dist[root] = 0; queue.Enqueue(root);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue(), cx = cur % cols, cy = cur / cols;
                    order.Add(cur);
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + StepX[k], ny = cy + StepY[k];
                        if (!DirectionSigns.IsOpen(cols, rows, input.openE, input.openN, cx, cy, nx, ny)) continue;
                        int next = nx + ny * cols;
                        if (dist[next] >= 0) continue;
                        dist[next] = dist[cur] + 1;
                        queue.Enqueue(next);
                    }
                }
                // a cell hangs off the first neighbour (west, south, east, north) that is open and one step nearer the elevator
                foreach (int c in order)
                {
                    if (c == root) continue;
                    int cx = c % cols, cy = c / cols;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + StepX[k], ny = cy + StepY[k];
                        if (!DirectionSigns.IsOpen(cols, rows, input.openE, input.openN, cx, cy, nx, ny)) continue;
                        int p = nx + ny * cols;
                        if (dist[p] == dist[c] - 1) { parent[c] = p; break; }
                    }
                }
                // how many numbered rooms hang off each cell, itself included (a branch that fits in what is left of a round stays whole)
                var below = new int[cellCount];
                for (int c = 0; c < cellCount; c++) below[c] = byCell[c] != null ? byCell[c].Count : 0;
                for (int i = order.Count - 1; i > 0; i--) { int c = order[i]; if (parent[c] >= 0) below[parent[c]] += below[c]; }

                var visited = new bool[cellCount];
                var walk = new WalkState { cols = cols, rows = rows, size = size, parent = parent, below = below, byCell = byCell, visited = visited, em = em, doorEast = input.elevDoorEast };
                Visit(walk, root, 1, 0, true);
                // cells the walk could not reach (not in a real maze): after everything else, in cell order
                for (int c = 0; c < cellCount; c++)
                    if (!visited[c] && byCell[c] != null) em.Add(OrderInCell(byCell[c], 1, 0, size));
            }
            // rooms with no cell, or a layout with no maze: the order the planner made them in, one group per cell
            if (loose.Count > 0)
            {
                int lastX = int.MinValue, lastY = int.MinValue;
                List<RoomInfo> current = null;
                foreach (var r in loose)
                {
                    if (current == null || r.cellX != lastX || r.cellY != lastY)
                    {
                        if (current != null) em.Add(current);
                        current = new List<RoomInfo>(); lastX = r.cellX; lastY = r.cellY;
                    }
                    current.Add(r);
                }
                if (current != null) em.Add(current);
            }
        }

        sealed class WalkState
        {
            public int cols, rows;
            public float size;
            public int[] parent, below;
            public List<RoomInfo>[] byCell;
            public bool[] visited;
            public Emitter em;
            public bool doorEast;
        }

        /// <summary>Walks into a cell travelling (tx, ty): numbers its rooms, then each branch that hangs off it, left first.</summary>
        static void Visit(WalkState w, int cell, int tx, int ty, bool isRoot)
        {
            w.visited[cell] = true;
            if (w.byCell[cell] != null) w.em.Add(OrderInCell(w.byCell[cell], tx, ty, w.size));
            int cx = cell % w.cols, cy = cell / w.cols;
            var kids = new List<int>(4);
            for (int k = 0; k < 4; k++)
            {
                int nx = cx + StepX[k], ny = cy + StepY[k];
                if (nx < 0 || ny < 0 || nx >= w.cols || ny >= w.rows) continue;
                int next = nx + ny * w.cols;
                if (w.parent[next] == cell) kids.Add(next);
            }
            // left, then straight on, then right (at the elevator: the way its door faces, then the other)
            kids.Sort((a, b) =>
            {
                int ka = TurnKey(a, cx, cy, w.cols, tx, ty, isRoot, w.doorEast), kb = TurnKey(b, cx, cy, w.cols, tx, ty, isRoot, w.doorEast);
                return ka != kb ? ka.CompareTo(kb) : a.CompareTo(b);
            });
            foreach (int kid in kids)
            {
                // a branch that fits in a round of its own but not in what is left of this one starts the next round (so a corridor is never cut in two)
                if (w.below[kid] > 0 && w.below[kid] <= RoomsPerRound) w.em.RoundBreakIfNeeded(w.below[kid]);
                Visit(w, kid, kid % w.cols - cx, kid / w.cols - cy, false);
            }
        }

        static int TurnKey(int kid, int cx, int cy, int cols, int tx, int ty, bool isRoot, bool doorEast)
        {
            int dx = kid % cols - cx, dy = kid / cols - cy;
            if (isRoot)
            {
                return doorEast ? (dx == 1 ? 0 : dy == 1 ? 1 : dx == -1 ? 2 : 3) : (dy == 1 ? 0 : dx == 1 ? 1 : dy == -1 ? 2 : 3);
            }
            int cross = tx * dy - ty * dx, dot = tx * dx + ty * dy;     // x east, y north: a positive cross is a turn to the left
            return cross > 0 ? 0 : dot > 0 ? 1 : cross < 0 ? 2 : 3;
        }

        /// <summary>The rooms of one cell in numbering order: along the way the walk came in (1.5 m steps), and across the corridor the left side first.</summary>
        static List<RoomInfo> OrderInCell(List<RoomInfo> rooms, int tx, int ty, float size)
        {
            var keyed = new List<(float bin, float left, float along, int index, RoomInfo room)>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                float ox = (r.cellX + 0.5f) * size, oz = (r.cellY + 0.5f) * size;
                float dx = r.bounds.Cx - ox, dz = r.bounds.Cz - oz;
                float along = dx * tx + dz * ty;               // progress in the direction of travel
                float left = dx * -ty + dz * tx;               // how far to the left of the way in (left of somebody walking in)
                keyed.Add(((float)System.Math.Floor((along + size) / 1.5f), left, along, i, r));
            }
            keyed.Sort((a, b) =>
            {
                int c = a.bin.CompareTo(b.bin); if (c != 0) return c;
                c = b.left.CompareTo(a.left); if (c != 0) return c;          // the left side first
                c = a.along.CompareTo(b.along); if (c != 0) return c;
                return a.index.CompareTo(b.index);
            });
            var ordered = new List<RoomInfo>(keyed.Count);
            foreach (var k in keyed) ordered.Add(k.room);
            return ordered;
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
