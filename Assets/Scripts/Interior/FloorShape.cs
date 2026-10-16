using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The shape of an office floor, free of Unity types: how many 18 m cells it has, which walls between cells have a doorway, how far
    /// every cell is from the service elevator, which cell is the farthest (the guard outpost goes there) and which way that cell opens.
    /// FloorGenerator builds its floors from this, and so can anything that needs to know what a floor that has not been built yet will
    /// look like - the notes in the guard outposts name the room of a safe on a later floor (<see cref="FloorGenerator.SafeRoomLabel"/>),
    /// which is why the planner's input is made here, in one place, and not twice.
    /// </summary>
    public sealed class FloorShape
    {
        public readonly int cols, rows;
        /// <summary>openE[c, r]: a doorway between (c, r) and (c+1, r). openN[c, r]: between (c, r) and (c, r+1).</summary>
        public readonly bool[,] openE, openN;

        // the four directions the maze is carved in, in the order the random choice is made (do not reorder: it fixes every floor's maze)
        static readonly int[] DirX = { 1, -1, 0, 0 }, DirY = { 0, 0, 1, -1 };

        public FloorShape(int cols, int rows)
        {
            this.cols = cols; this.rows = rows;
            openE = new bool[cols, rows];
            openN = new bool[cols, rows];
        }

        /// <summary>Wraps doorways that already exist (the generator's own arrays are used as they are, not copied).</summary>
        public FloorShape(int cols, int rows, bool[,] openE, bool[,] openN)
        {
            this.cols = cols; this.rows = rows;
            this.openE = openE; this.openN = openN;
        }

        /// <summary>
        /// A maze of the given size: a depth-first spanning tree from the elevator's cell (0, 0) plus a few extra doorways that make loops,
        /// and the elevator's cell always open to both neighbours. The random numbers are taken from <paramref name="rng"/> in place,
        /// so the generator can go on using the same generator afterwards (the loot and the guards follow the maze).
        /// </summary>
        public static FloorShape Maze(Random rng, int cols, int rows, double loopChance)
        {
            var s = new FloorShape(cols, rows);
            s.Carve(rng, loopChance);
            // the elevator room always opens to both of its neighbours, so you can leave by whichever
            // side is closer to where you're heading instead of walking around
            if (cols > 1) s.openE[0, 0] = true;
            if (rows > 1) s.openN[0, 0] = true;
            return s;
        }

        void Carve(Random rng, double loopChance)
        {
            var visited = new bool[cols, rows];
            var stack = new Stack<int>();                 // cell = c + r * cols
            visited[0, 0] = true; stack.Push(0);
            var opts = new List<int>(4);
            while (stack.Count > 0)
            {
                int cur = stack.Peek(), cx = cur % cols, cy = cur / cols;
                opts.Clear();
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DirX[d], ny = cy + DirY[d];
                    if (nx >= 0 && ny >= 0 && nx < cols && ny < rows && !visited[nx, ny]) opts.Add(nx + ny * cols);
                }
                if (opts.Count == 0) { stack.Pop(); continue; }
                int next = opts[rng.Next(opts.Count)];
                int px = next % cols, py = next / cols;
                SetOpen(cx, cy, px, py);
                visited[px, py] = true;
                stack.Push(next);
            }
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    if (c < cols - 1 && !openE[c, r] && rng.NextDouble() < loopChance) openE[c, r] = true;
                    if (r < rows - 1 && !openN[c, r] && rng.NextDouble() < loopChance) openN[c, r] = true;
                }
        }

        void SetOpen(int ax, int ay, int bx, int by)
        {
            if (bx > ax) openE[ax, ay] = true;
            else if (bx < ax) openE[bx, by] = true;
            else if (by > ay) openN[ax, ay] = true;
            else openN[bx, by] = true;
        }

        /// <summary>Is there a doorway between two neighbouring cells?</summary>
        public bool IsOpen(int ax, int ay, int bx, int by)
        {
            if (bx > ax) return openE[ax, ay];
            if (bx < ax) return openE[bx, by];
            if (by > ay) return openN[ax, ay];
            return openN[bx, by];
        }

        /// <summary>How many doorways it takes to walk from the elevator's cell to each cell (-1: cannot be reached).</summary>
        public int[,] Distances()
        {
            var dist = new int[cols, rows];
            for (int c = 0; c < cols; c++) for (int r = 0; r < rows; r++) dist[c, r] = -1;
            var q = new Queue<int>();
            dist[0, 0] = 0; q.Enqueue(0);
            while (q.Count > 0)
            {
                int cur = q.Dequeue(), cx = cur % cols, cy = cur / cols;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + DirX[d], ny = cy + DirY[d];
                    if (nx < 0 || ny < 0 || nx >= cols || ny >= rows || dist[nx, ny] >= 0) continue;
                    if (!IsOpen(cx, cy, nx, ny)) continue;
                    dist[nx, ny] = dist[cx, cy] + 1;
                    q.Enqueue(nx + ny * cols);
                }
            }
            return dist;
        }

        /// <summary>The cell farthest from the elevator (the first one found, going column by column).</summary>
        public void FarthestCell(int[,] dist, out int fc, out int fr)
        {
            fc = 0; fr = 0;
            int best = -1;
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    if (dist[c, r] > best) { best = dist[c, r]; fc = c; fr = r; }
        }

        /// <summary>Which neighbour a cell's guard car faces: the first open side going west, south, east, north (west if none).</summary>
        public void GuestDoorDir(int c, int r, out int dx, out int dy)
        {
            int[] tx = { -1, 0, 1, 0 }, ty = { 0, -1, 0, 1 };
            for (int i = 0; i < 4; i++)
            {
                int nx = c + tx[i], ny = r + ty[i];
                if (nx >= 0 && ny >= 0 && nx < cols && ny < rows && IsOpen(c, r, nx, ny)) { dx = tx[i]; dy = ty[i]; return; }
            }
            dx = -1; dy = 0;
        }

        /// <summary>Does the service elevator's front door face east (otherwise north)? East when it opens east, else north when it opens north.</summary>
        public bool ElevatorDoorEast()
        {
            bool east = cols > 1 && IsOpen(0, 0, 1, 0);
            bool north = !east && rows > 1 && IsOpen(0, 0, 0, 1);
            return east || !north;
        }

        /// <summary>
        /// The input the room planner needs for this floor. <paramref name="intelSeed"/> is the run's seed for the safes (see <see cref="SafeRules"/>).
        /// Every floor gets a guard outpost in its farthest cell; whether it also has a safe, and the number that picks the safe's room, follow
        /// from the seed and the floor alone.
        /// </summary>
        public PlanInput PlanFor(int floor, int seed, float cell, float wallHeight, int intelSeed)
        {
            var dist = Distances();
            FarthestCell(dist, out int gc, out int gr);
            GuestDoorDir(gc, gr, out int gdx, out int gdy);
            return new PlanInput
            {
                seed = seed, floor = floor, cols = cols, rows = rows, cell = cell, wallH = wallHeight,
                openE = openE, openN = openN,
                elevC = 0, elevR = 0, elevDoorEast = ElevatorDoorEast(),
                hiddenHall = floor == TowerRules.KeyFloor,
                guestC = gc, guestR = gr, guestDirX = gdx, guestDirY = gdy,
                cafe = floor == 1,
                outpost = true,
                hasSafe = SafeRules.HasSafe(intelSeed, floor),
                safeRoll = SafeRules.RoomRoll(intelSeed, floor)
            };
        }
    }
}
