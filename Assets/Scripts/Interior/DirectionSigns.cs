using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildingEscape
{
    /// <summary>One direction sign on a corridor wall: "ROOMS 111-120" and an arrow. Plain data (see <see cref="DirectionSigns"/>).</summary>
    public sealed class DirSign
    {
        /// <summary>The two lines of lettering: "ROOMS" and the numbers ("111-120").</summary>
        public string line1, line2;
        /// <summary>The arrow points to the right of somebody who stands in the corridor and looks at the sign (otherwise to the left).</summary>
        public bool arrowRight;
        /// <summary>The way the arrow points in the world: one step along x or z. It is the way along the corridor the sign hangs in.</summary>
        public int dirX, dirZ;
        /// <summary>The middle of the sign on the wall (x, z), the height of its middle, and the way it faces (into the corridor).</summary>
        public float x, y, z, nx, nz;
        /// <summary>The size of one dot of the lettering and of the plate, in metres.</summary>
        public float dot, width, height;
        /// <summary>The columns taken by the text (the wider of the two lines); the whole sign is <see cref="Columns"/> by <see cref="DirectionSigns.TextRows"/> dots.</summary>
        public int textCols;
        /// <summary>The cell the sign hangs in.</summary>
        public int cellX, cellY;
        /// <summary>The numbered rooms it names (1-based, in the order of their numbers) and the nearest cell that holds any of them.</summary>
        public int firstIndex, lastIndex, toCellX, toCellY;
        /// <summary>How many doorways it takes to get from the sign's cell to that cell.</summary>
        public int steps;

        public int Columns => textCols + DirectionSigns.ArrowGap + DotText.ArrowSize;
    }

    /// <summary>
    /// The direction signs of a floor: a few signs on corridor walls that say "ROOMS 111-120" with an arrow, for the players who look for a room
    /// number (a note about a safe names one). Plain data and arithmetic - no Unity types - so it can be tested outside Unity; everything follows
    /// from the layout and the floor's maze, so every player sees the same signs.
    ///
    /// What a sign says is always true. The numbered rooms are grouped into blocks of ten in the order of their numbers (101-110, 111-120, ...,
    /// and again for 101A-110A... on a floor with more than 99 rooms). A sign names a block that has no room in its own cell, and its arrow
    /// points the way a shortest walk through the doorways of the maze to the nearest room of that block begins: along the corridor the sign
    /// hangs in, towards a doorway that is open. Signs go in a few cells spread over the floor (the first in the cell closest to the elevator),
    /// two at most in each, on the wall faces that look into a corridor and have room for them.
    /// </summary>
    public static class DirectionSigns
    {
        /// <summary>The rooms named on one sign (the last block of a round may be a little longer, never shorter than <see cref="MinLastBlock"/>).</summary>
        public const int BlockSize = 10, MinLastBlock = 4;
        /// <summary>The lettering: "ROOMS" over the numbers.</summary>
        public const string Title = "ROOMS", TitleOne = "ROOM";
        public const int TextRows = DotText.Rows * 2 + 1;
        /// <summary>Empty columns between the text and the arrow, and the margin round the lettering on the plate, in dots.</summary>
        public const int ArrowGap = 3, MarginDots = 3;
        /// <summary>The height of the sign's middle above the floor (the walls are 3.5 m high and the doors 2.45 m).</summary>
        public const float SignY = 2.45f;
        /// <summary>The biggest and the smallest dot: 5 cm makes 25 cm tall letters.</summary>
        public const float MaxDot = 0.05f, MinDot = 0.032f;
        /// <summary>A sign keeps this far from the end of the wall it hangs on (a doorway, a corner).</summary>
        public const float EndMargin = 0.15f;
        /// <summary>A sign hangs this far off the wall (the builder), and nothing taller than the sign's underside may stand this deep in front of it.</summary>
        public const float Standoff = 0.012f, ClearDepth = 0.5f;
        /// <summary>How many cells get signs, how many signs each gets at most.</summary>
        public const int MaxPerCell = 2;

        public static int AreasFor(int cellCount) => Math.Max(2, Math.Min(5, cellCount / 9));

        // ------------------------------------------------------------------ blocks of rooms

        /// <summary>A run of ten numbered rooms (their indices count from 1, in the order of their numbers).</summary>
        public sealed class Block
        {
            public int first, last;
            /// <summary>"ROOMS", or "ROOM" for a block of one; and the numbers on the second line ("111-120", or just "101A").</summary>
            public string title, range;
            /// <summary>The cells (cell x + cell y * columns) that hold its rooms.</summary>
            public readonly List<int> cells = new List<int>();
        }

        /// <summary>Groups the numbered rooms of a floor into blocks of ten, round by round (a round is up to 99 rooms, see <see cref="RoomNumbers"/>); the rooms are in the order of their numbers, which follows the corridors, so a block is a stretch of corridor.</summary>
        public static List<Block> MakeBlocks(Layout layout, int floor, int columns)
        {
            var blocks = new List<Block>();
            var numbering = RoomNumbers.Of(layout, floor);
            var rooms = numbering.rooms;
            int roundStart = 0;
            while (roundStart < rooms.Count)
            {
                int round = numbering.rounds[roundStart], roundEnd = roundStart;
                while (roundEnd < rooms.Count && numbering.rounds[roundEnd] == round) roundEnd++;
                int inRound = roundEnd - roundStart;
                var spans = new List<int[]>();                                   // first and last index (1-based) of each block
                for (int at = 0; at < inRound; at += BlockSize)
                    spans.Add(new[] { roundStart + at + 1, roundStart + Math.Min(inRound, at + BlockSize) });
                int lastSpan = spans.Count - 1;
                if (lastSpan > 0 && spans[lastSpan][1] - spans[lastSpan][0] + 1 < MinLastBlock)   // (a round of its own that is short stays as it is)
                {
                    spans[lastSpan - 1][1] = spans[lastSpan][1];                  // a short tail joins the block before it
                    spans.RemoveAt(lastSpan);
                }
                foreach (var span in spans)
                {
                    bool one = span[0] == span[1];
                    var b = new Block
                    {
                        first = span[0], last = span[1], title = one ? TitleOne : Title,
                        range = one ? numbering.labels[span[0] - 1] : numbering.labels[span[0] - 1] + "-" + numbering.labels[span[1] - 1]
                    };
                    for (int i = b.first - 1; i < b.last; i++)
                    {
                        int cell = rooms[i].cellX + rooms[i].cellY * columns;
                        if (!b.cells.Contains(cell)) b.cells.Add(cell);
                    }
                    blocks.Add(b);
                }
                roundStart = roundEnd;
            }
            return blocks;
        }

        // ------------------------------------------------------------------ the maze

        /// <summary>Is there a doorway between two neighbouring cells? (False for cells outside the floor.)</summary>
        public static bool IsOpen(int cols, int rows, bool[,] openE, bool[,] openN, int ax, int ay, int bx, int by)
        {
            if (ax < 0 || ay < 0 || bx < 0 || by < 0 || ax >= cols || bx >= cols || ay >= rows || by >= rows) return false;
            if (bx == ax + 1 && by == ay) return openE[ax, ay];
            if (bx == ax - 1 && by == ay) return openE[bx, by];
            if (by == ay + 1 && bx == ax) return openN[ax, ay];
            if (by == ay - 1 && bx == ax) return openN[bx, by];
            return false;
        }

        /// <summary>Doorways to walk from every cell to every other: result[a * cells + b] (cell = x + y * columns; -1: cannot be reached).</summary>
        public static int[] Distances(int cols, int rows, bool[,] openE, bool[,] openN)
        {
            int n = cols * rows;
            var d = new int[n * n];
            for (int i = 0; i < d.Length; i++) d[i] = -1;
            var queue = new Queue<int>();
            int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
            for (int a = 0; a < n; a++)
            {
                d[a * n + a] = 0;
                queue.Clear();
                queue.Enqueue(a);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue(), cx = cur % cols, cy = cur / cols;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + dx[k], ny = cy + dy[k];
                        if (!IsOpen(cols, rows, openE, openN, cx, cy, nx, ny)) continue;
                        int next = nx + ny * cols;
                        if (d[a * n + next] >= 0) continue;
                        d[a * n + next] = d[a * n + cur] + 1;
                        queue.Enqueue(next);
                    }
                }
            }
            return d;
        }

        // ------------------------------------------------------------------ places on the walls

        /// <summary>A stretch of one wall face that looks into a corridor and is free of doorways and corners.</summary>
        sealed class Site
        {
            public int cx, cy;
            public bool alongX;
            public float nx, nz;
            public float face, lo, hi;
        }

        static int Hash(int seed, int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)(a + 1) * 40503u + 0x9E3779B9u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)(b + 1) * 69069u + 0x85EBCA6Bu;
                h *= 2246822519u;
                h ^= (uint)salt * 3266489917u;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }

        static List<Site> FindSites(Layout layout, int cols, int rows, float cell, float wallH)
        {
            var sites = new List<Site>();
            // the corridors (hubs, arms, long halls, lobbies), by cell
            var corridors = new List<RoomInfo>[cols * rows];
            foreach (var r in layout.rooms)
            {
                if (r.kind != 2 || r.cellX < 0 || r.cellY < 0 || r.cellX >= cols || r.cellY >= rows) continue;
                int id = r.cellX + r.cellY * cols;
                (corridors[id] ?? (corridors[id] = new List<RoomInfo>())).Add(r);
            }
            var outpost = layout.outpost;
            var outer = outpost != null ? outpost.outer.Grow(0.02f) : default(Rc);

            bool InCorridor(float x, float z)
            {
                int cx = Math.Max(0, Math.Min(cols - 1, (int)(x / cell))), cy = Math.Max(0, Math.Min(rows - 1, (int)(z / cell)));
                var list = corridors[cx + cy * cols];
                if (list == null) return false;
                foreach (var r in list)
                {
                    if (!r.bounds.Contains(x, z)) continue;
                    bool blocked = false;
                    foreach (var b in r.blocks) if (b.Contains(x, z)) { blocked = true; break; }
                    if (!blocked) return true;
                }
                return false;
            }

            const float step = 0.1f, off = 0.6f;
            foreach (var w in layout.walls)
            {
                if (w.kind != "wall" || w.y0 > 0.01f || w.y1 < wallH - 0.01f) continue;
                var rc = w.rc;
                if (outpost != null && rc.x0 >= outer.x0 && rc.x1 <= outer.x1 && rc.z0 >= outer.z0 && rc.z1 <= outer.z1) continue;   // the outpost's own walls
                bool alongX = rc.W >= rc.D;
                float thick = alongX ? rc.D : rc.W, a0 = alongX ? rc.x0 : rc.z0, a1 = alongX ? rc.x1 : rc.z1;
                if (thick > 0.45f || a1 - a0 < 1.0f) continue;
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float face = alongX ? (sgn > 0 ? rc.z1 : rc.z0) : (sgn > 0 ? rc.x1 : rc.x0);
                    float nx = alongX ? 0f : sgn, nz = alongX ? sgn : 0f;
                    int samples = (int)Math.Floor((a1 - a0) / step) + 1;
                    int runStart = -1;
                    for (int k = 0; k <= samples; k++)
                    {
                        bool ok = false;
                        if (k < samples)
                        {
                            float s = a0 + k * step;
                            float px = alongX ? s : face + nx * off, pz = alongX ? face + nz * off : s;
                            ok = InCorridor(px, pz);
                        }
                        if (ok) { if (runStart < 0) runStart = k; continue; }
                        if (runStart < 0) continue;
                        float lo = a0 + runStart * step + EndMargin, hi = a0 + (k - 1) * step - EndMargin;
                        runStart = -1;
                        if (hi - lo < 1.2f) continue;
                        float mid = (lo + hi) * 0.5f;
                        float mx = alongX ? mid : face, mz = alongX ? face : mid;
                        sites.Add(new Site
                        {
                            cx = Math.Max(0, Math.Min(cols - 1, (int)(mx / cell))), cy = Math.Max(0, Math.Min(rows - 1, (int)(mz / cell))),
                            alongX = alongX, nx = nx, nz = nz, face = face, lo = lo, hi = hi
                        });
                    }
                }
            }
            return sites;
        }

        // ------------------------------------------------------------------ the plan

        /// <summary>
        /// The signs of a floor. <paramref name="seed"/> is the floor's seed (it only decides which cells and walls are used).
        /// </summary>
        public static List<DirSign> Plan(Layout layout, int floor, int cols, int rows, bool[,] openE, bool[,] openN, float cell, float wallH, int seed)
        {
            var result = new List<DirSign>();
            if (layout == null || floor < 1 || cols < 1 || rows < 1 || openE == null || openN == null) return result;
            var blocks = MakeBlocks(layout, floor, cols);
            if (blocks.Count < 2) return result;
            int n = cols * rows;
            var dist = Distances(cols, rows, openE, openN);
            var sites = FindSites(layout, cols, rows, cell, wallH);
            if (sites.Count == 0) return result;

            // pieces by cell, for the clearance check
            var piecesByCell = new List<Piece>[n];
            foreach (var p in layout.pieces)
            {
                int cx = Math.Max(0, Math.Min(cols - 1, (int)(p.x / cell))), cy = Math.Max(0, Math.Min(rows - 1, (int)(p.z / cell)));
                (piecesByCell[cx + cy * cols] ?? (piecesByCell[cx + cy * cols] = new List<Piece>())).Add(p);
            }

            var sitesByCell = new List<Site>[n];
            foreach (var s in sites) (sitesByCell[s.cx + s.cy * cols] ?? (sitesByCell[s.cx + s.cy * cols] = new List<Site>())).Add(s);
            var withSites = new List<int>();
            for (int c = 0; c < n; c++) if (sitesByCell[c] != null) withSites.Add(c);

            // which cells get signs: the one closest to the elevator that can take any, then always the one farthest from those that do
            int want = AreasFor(n);
            var done = new List<int>();
            var tried = new HashSet<int>();
            while (done.Count < want && tried.Count < withSites.Count)
            {
                int pick = -1;
                if (done.Count == 0)
                {
                    int bestDist = int.MaxValue, bestHash = -1;
                    foreach (int c in withSites)
                    {
                        if (tried.Contains(c)) continue;
                        int d = dist[c] < 0 ? int.MaxValue - 1 : dist[c];         // (cell 0 is the elevator's)
                        int h = Hash(seed, c, 0, 91);
                        if (d < bestDist || (d == bestDist && h > bestHash)) { pick = c; bestDist = d; bestHash = h; }
                    }
                }
                else
                {
                    int bestGap = -1, bestHash = -1;
                    foreach (int c in withSites)
                    {
                        if (tried.Contains(c)) continue;
                        int gap = int.MaxValue;
                        foreach (int a in done) gap = Math.Min(gap, Math.Abs(c % cols - a % cols) + Math.Abs(c / cols - a / cols));
                        int h = Hash(seed, c, 1, 92);
                        if (gap > bestGap || (gap == bestGap && h > bestHash)) { pick = c; bestGap = gap; bestHash = h; }
                    }
                }
                if (pick < 0) break;
                tried.Add(pick);
                if (PlanCell(pick, sitesByCell[pick], blocks, dist, n, cols, rows, openE, openN, layout, piecesByCell, result, seed, cell, floor) > 0) done.Add(pick);
            }
            return result;
        }

        sealed class Candidate
        {
            public Site site;
            public int dir;                 // +1 or -1 along the wall
            public Block block;
            public int steps, toCell, order;
        }

        static int PlanCell(int area, List<Site> sites, List<Block> blocks, int[] dist, int n, int cols, int rows, bool[,] openE, bool[,] openN,
                            Layout layout, List<Piece>[] piecesByCell, List<DirSign> result, int seed, float cell, int floor)
        {
            int ax = area % cols, ay = area / cols;
            var cands = new List<Candidate>();
            int sidx = 0;
            foreach (var site in sites)
            {
                sidx++;
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    int dx = site.alongX ? dir : 0, dy = site.alongX ? 0 : dir;
                    int bx = ax + dx, by = ay + dy;
                    if (!IsOpen(cols, rows, openE, openN, ax, ay, bx, by)) continue;
                    int neighbour = bx + by * cols;
                    foreach (var b in blocks)
                    {
                        if (b.cells.Contains(area)) continue;                   // it has a room right here: nothing to point at
                        int dmin = int.MaxValue;
                        foreach (int t in b.cells) { int d = dist[area * n + t]; if (d >= 0 && d < dmin) dmin = d; }
                        if (dmin == int.MaxValue || dmin < 1) continue;
                        int via = -1;
                        foreach (int t in b.cells)
                            if (dist[area * n + t] == dmin && dist[neighbour * n + t] == dmin - 1) { via = t; break; }
                        if (via < 0) continue;                                   // the shortest walk to its nearest room does not begin this way
                        cands.Add(new Candidate { site = site, dir = dir, block = b, steps = dmin, toCell = via, order = Hash(seed, area, sidx * 64 + (dir > 0 ? 1 : 0) + b.first * 4096, 93) });
                    }
                }
            }
            if (cands.Count == 0) return 0;

            // a block a few doorways away is the most useful; then whatever the hash says
            var sorted = cands.OrderBy(c => c.steps >= 2 && c.steps <= 8 ? 0 : 1).ThenBy(c => c.order).ToList();
            int made = 0;
            for (int pass = 0; pass < 2 && made < MaxPerCell; pass++)
            {
                foreach (var c in sorted)
                {
                    if (made >= MaxPerCell) break;
                    bool clash = false;
                    foreach (var s in result)
                    {
                        if (s.cellX != ax || s.cellY != ay) continue;
                        bool sameBlock = s.firstIndex == c.block.first;
                        bool sameWay = s.dirX == (c.site.alongX ? c.dir : 0) && s.dirZ == (c.site.alongX ? 0 : c.dir);
                        if (sameBlock || (pass == 0 && sameWay)) { clash = true; break; }
                    }
                    if (clash) continue;
                    var sign = Place(c, ax, ay, cell, piecesByCell, layout, result, floor, cols);
                    if (sign == null) continue;
                    result.Add(sign);
                    made++;
                }
            }
            return made;
        }

        /// <summary>Works out the sign for a candidate: its size, and a spot on the site's stretch of wall that nothing is in the way of.</summary>
        static DirSign Place(Candidate c, int ax, int ay, float cell, List<Piece>[] piecesByCell, Layout layout, List<DirSign> placed, int floor, int cols)
        {
            var site = c.site;
            var sign = new DirSign
            {
                line1 = c.block.title, line2 = c.block.range,
                nx = site.nx, nz = site.nz,
                dirX = site.alongX ? c.dir : 0, dirZ = site.alongX ? 0 : c.dir,
                y = SignY, cellX = ax, cellY = ay,
                firstIndex = c.block.first, lastIndex = c.block.last,
                toCellX = c.toCell % cols, toCellY = c.toCell / cols, steps = c.steps
            };
            // looking at the wall, right is (-nz, nx)
            float rightX = -site.nz, rightZ = site.nx;
            sign.arrowRight = sign.dirX * rightX + sign.dirZ * rightZ > 0f;
            sign.textCols = Math.Max(DotText.Columns(sign.line1), DotText.Columns(sign.line2));

            float room = site.hi - site.lo;
            int dots = sign.Columns + 2 * MarginDots;
            float dot = Math.Min(MaxDot, room / dots);
            if (dot < MinDot) return null;
            sign.dot = dot;
            sign.width = dots * dot;
            sign.height = (TextRows + 2 * MarginDots) * dot;

            // slide along the stretch (middle first) until nothing is in the way
            float half = sign.width * 0.5f;
            float mid = (site.lo + site.hi) * 0.5f, slack = Math.Max(0f, room * 0.5f - half);
            for (int k = 0; k <= 12; k++)
            {
                float shift = k == 0 ? 0f : ((k & 1) == 1 ? 1 : -1) * ((k + 1) / 2) * 0.25f;
                if (Math.Abs(shift) > slack + 1e-4f) continue;
                float along = mid + shift;
                sign.x = site.alongX ? along : site.face;
                sign.z = site.alongX ? site.face : along;
                if (!Blocked(sign, site, piecesByCell, layout, placed, cell, cols)) return sign;
            }
            return null;
        }

        /// <summary>The space the sign takes in front of its wall (a thin box), as a rectangle on the floor plan.</summary>
        public static Rc Footprint(DirSign s, float depth)
        {
            float half = s.width * 0.5f;
            bool alongX = s.nz != 0f;
            float a0 = (alongX ? s.x : s.z) - half, a1 = (alongX ? s.x : s.z) + half;
            float face = alongX ? s.z : s.x;
            float n = alongX ? s.nz : s.nx;
            float f0 = n > 0f ? face : face - depth, f1 = n > 0f ? face + depth : face;
            return alongX ? new Rc(a0, f0, a1, f1) : new Rc(f0, a0, f1, a1);
        }

        static bool Blocked(DirSign sign, Site site, List<Piece>[] piecesByCell, Layout layout, List<DirSign> placed, float cell, int cols)
        {
            var box = Footprint(sign, ClearDepth);
            float bottom = sign.y - sign.height * 0.5f - 0.05f, top = sign.y + sign.height * 0.5f + 0.05f;

            // other signs
            foreach (var o in placed)
                if (Footprint(o, ClearDepth).Overlaps(box) && Math.Abs(o.y - sign.y) < (o.height + sign.height) * 0.5f + 0.05f) return true;

            // doorways (a sliding door's leaves stay inside the doorway, but keep a hand's width clear)
            var thin = Footprint(sign, 0.05f);
            foreach (var r in layout.rooms)
                foreach (var d in r.doors)
                    if (d.Grow(0.1f).Overlaps(thin)) return true;

            // furniture and things hanging on the wall
            int cx0 = Math.Max(0, (int)((box.x0 - 0.5f) / cell)), cx1 = Math.Min(cols - 1, (int)((box.x1 + 0.5f) / cell));
            int rowsCount = piecesByCell.Length / cols;
            int cy0 = Math.Max(0, (int)((box.z0 - 0.5f) / cell)), cy1 = Math.Min(rowsCount - 1, (int)((box.z1 + 0.5f) / cell));
            for (int cy = cy0; cy <= cy1; cy++)
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    var list = piecesByCell[cx + cy * cols];
                    if (list == null) continue;
                    foreach (var p in list)
                    {
                        if (p.y + p.h < bottom || p.y > top) continue;
                        if (p.rc.Grow(0.05f).Overlaps(box)) return true;
                    }
                }
            return false;
        }

        // ------------------------------------------------------------------ the lettering

        /// <summary>The lit dots of a sign: the text (two centred lines) and the arrow, in dots from the top left of the lettering.</summary>
        public static void Compose(DirSign s, List<DotText.Run> text, List<DotText.Run> arrow)
        {
            int textCol0 = s.arrowRight ? 0 : DotText.ArrowSize + ArrowGap;
            int c1 = DotText.Columns(s.line1), c2 = DotText.Columns(s.line2);
            DotText.AddRuns(s.line1, textCol0 + (s.textCols - c1) / 2, 0, text);
            DotText.AddRuns(s.line2, textCol0 + (s.textCols - c2) / 2, DotText.Rows + 1, text);
            int arrowCol0 = s.arrowRight ? s.textCols + ArrowGap : 0;
            DotText.AddArrowRuns(s.arrowRight, arrowCol0, (TextRows - DotText.ArrowSize) / 2, arrow);
        }
    }
}
