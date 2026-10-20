using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Shows how room numbers lie on real floors (8 x 5 cells, the size the generator makes) so a numbering rule can be judged by looking at
    /// it and by numbers: for several floors a map of the cells with the numbers of their rooms (north at the top, the elevator at the bottom
    /// left), how far apart rooms with neighbouring numbers are, and the direction signs (how many, what they say). The report goes to
    /// %TEMP%\be_room_audit.txt (and a summary to the log). Run: <c>-executeMethod BuildingEscape.EditorTools.NetDevRoomAudit.RoomAudit</c>.
    /// </summary>
    public static class NetDevRoomAudit
    {
        const int Cols = 8, Rows = 5;

        public static void RoomAudit()
        {
            var report = new StringBuilder();
            int totalSigns = 0, totalRooms = 0, floors = 0, totalPairs = 0, totalViolations = 0, startsNear = 0;
            double jumpSum = 0; int jumpN = 0, jumpFar = 0; float jumpMax = 0f;
            foreach (var (floor, seed) in new[] { (1, 4101), (2, 777), (4, 9172), (6, 31337), (9, 2288), (12, 515) })
            {
                var shape = FloorShape.Maze(new System.Random(seed), Cols, Rows, 0.12);
                var input = shape.PlanFor(floor, seed, 18f, 3.5f, 777);
                var layout = OfficePlanner.Build(input);

                // the numbered rooms with their labels, ordered by label
                var rooms = new List<(string label, RoomInfo room)>();
                foreach (var r in layout.rooms)
                    if (RoomNumbers.IsNumbered(r)) rooms.Add((RoomNumbers.LabelOf(layout, r, floor), r));
                rooms.Sort((a, b) => CompareLabels(a.label, b.label));

                report.AppendLine($"==== floor {floor}  seed {seed}  {Cols} x {Rows} cells, {rooms.Count} numbered rooms ====");
                // the map: north at the top
                for (int row = Rows - 1; row >= 0; row--)
                {
                    var line = new StringBuilder();
                    for (int col = 0; col < Cols; col++)
                    {
                        var here = new List<string>();
                        foreach (var (label, r) in rooms) if (r.cellX == col && r.cellY == row) here.Add(label);
                        string cell = col == 0 && row == 0 ? "[ELEVATOR] " : "";
                        cell += string.Join(" ", here);
                        line.Append(cell.PadRight(30));
                    }
                    report.AppendLine($"row {row}: {line}".TrimEnd());
                }

                // how far apart are neighbouring numbers?
                var jumps = new List<float>();
                for (int i = 1; i < rooms.Count; i++)
                {
                    var a = rooms[i - 1].room.bounds; var b = rooms[i].room.bounds;
                    float d = Mathf.Sqrt((a.Cx - b.Cx) * (a.Cx - b.Cx) + (a.Cz - b.Cz) * (a.Cz - b.Cz));
                    jumps.Add(d); jumpSum += d; jumpN++; if (d > 30f) jumpFar++; if (d > jumpMax) jumpMax = d;
                }
                jumps.Sort();
                float median = jumps.Count > 0 ? jumps[jumps.Count / 2] : 0f, p90 = jumps.Count > 0 ? jumps[Mathf.Min(jumps.Count - 1, jumps.Count * 9 / 10)] : 0f;
                report.AppendLine($"neighbouring numbers are {median:0.#} m apart (median), {p90:0.#} m (90th percentile), {(jumps.Count > 0 ? jumps[jumps.Count - 1] : 0f):0.#} m (largest)");

                // walking away from the elevator: does a number ever go DOWN? (every cell is compared with the cells on its way from the elevator)
                var dist = shape.Distances();
                int cellsN = Cols * Rows;
                var minAt = new int[cellsN]; var maxAt = new int[cellsN];
                for (int i = 0; i < cellsN; i++) { minAt[i] = int.MaxValue; maxAt[i] = -1; }
                for (int i = 0; i < rooms.Count; i++)
                {
                    int cell = rooms[i].room.cellX + rooms[i].room.cellY * Cols;
                    if (i < minAt[cell]) minAt[cell] = i;
                    if (i > maxAt[cell]) maxAt[cell] = i;
                }
                int pairs = 0, violations = 0;
                int[] sx = { -1, 0, 1, 0 }, sy = { 0, -1, 0, 1 };
                for (int cell = 0; cell < cellsN; cell++)
                {
                    if (maxAt[cell] < 0) continue;
                    int at = cell;
                    while (dist[at % Cols, at / Cols] > 0)
                    {
                        int cx = at % Cols, cy = at / Cols, up = -1;
                        for (int k = 0; k < 4 && up < 0; k++)
                        {
                            int nx = cx + sx[k], ny = cy + sy[k];
                            if (nx < 0 || ny < 0 || nx >= Cols || ny >= Rows || !shape.IsOpen(cx, cy, nx, ny)) continue;
                            if (dist[nx, ny] == dist[cx, cy] - 1) up = nx + ny * Cols;
                        }
                        if (up < 0) break;
                        at = up;
                        if (maxAt[at] >= 0) { pairs++; if (maxAt[at] > minAt[cell]) violations++; }
                    }
                }
                int first = rooms.Count > 0 ? rooms[0].room.cellX + rooms[0].room.cellY * Cols : -1, nearest = int.MaxValue;
                for (int cell = 0; cell < cellsN; cell++) if (maxAt[cell] >= 0) nearest = Mathf.Min(nearest, dist[cell % Cols, cell / Cols]);
                report.AppendLine($"walking away from the elevator a number goes DOWN in {violations} of {pairs} cell pairs ({(pairs > 0 ? 100f * violations / pairs : 0f):0}%); " +
                                  $"the first number ({(rooms.Count > 0 ? rooms[0].label : "-")}) is {(first >= 0 ? dist[first % Cols, first / Cols] : -1)} doorways from the elevator, the nearest cell with rooms is {nearest}");
                totalPairs += pairs; totalViolations += violations; if (first >= 0 && dist[first % Cols, first / Cols] <= nearest + 1) startsNear++;

                // the signs
                var signs = DirectionSigns.Plan(layout, floor, Cols, Rows, shape.openE, shape.openN, 18f, 3.5f, seed);
                report.AppendLine($"direction signs: {signs.Count}");
                foreach (var s in signs)
                    report.AppendLine($"  cell ({s.cellX},{s.cellY}): \"{s.line1} {s.line2}\" arrow {(s.dirX != 0 ? (s.dirX > 0 ? "east" : "west") : (s.dirZ > 0 ? "north" : "south"))}, " +
                                      $"{s.steps} doorways to cell ({s.toCellX},{s.toCellY}), dot {s.dot * 100f:0.0} cm, width {s.width:0.00} m");
                report.AppendLine();
                totalSigns += signs.Count; totalRooms += rooms.Count; floors++;
            }
            string summary = $"{floors} floors, {totalRooms} numbered rooms, {totalSigns} signs; neighbouring numbers {jumpSum / System.Math.Max(1, jumpN):0.#} m apart on average, " +
                             $"{jumpFar} of {jumpN} pairs more than 30 m apart, largest {jumpMax:0.#} m; walking away from the elevator a number goes DOWN in {totalViolations} of {totalPairs} cell pairs " +
                             $"({(totalPairs > 0 ? 100f * totalViolations / totalPairs : 0f):0}%); the first number is in the nearest cell with rooms (or the next) on {startsNear} of {floors} floors";
            report.AppendLine("TOTAL: " + summary);
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_room_audit.txt");
            System.IO.File.WriteAllText(path, report.ToString());
            Debug.Log($"[Audit] {summary} -> {path}");
            EditorApplication.Exit(0);
        }

        /// <summary>Labels in the order of their numbers: by the digits, a label with a letter after those without.</summary>
        static int CompareLabels(string a, string b)
        {
            bool la = char.IsLetter(a[a.Length - 1]), lb = char.IsLetter(b[b.Length - 1]);
            if (la != lb) return la ? 1 : -1;
            string da = la ? a.Substring(0, a.Length - 1) : a, db = lb ? b.Substring(0, b.Length - 1) : b;
            int c = int.Parse(da, CultureInfo.InvariantCulture).CompareTo(int.Parse(db, CultureInfo.InvariantCulture));
            return c != 0 ? c : string.CompareOrdinal(a, b);
        }
    }
}
