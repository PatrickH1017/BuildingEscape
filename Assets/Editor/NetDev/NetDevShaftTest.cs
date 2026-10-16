using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>Is the cell (c, r) one of the two stairwell cells of the plan?</summary>
        static bool IsStairCell(PlanInput input, int c, int r)
        {
            if (input.stairC == null) return false;
            for (int i = 0; i < input.stairC.Length; i++) if (input.stairC[i] == c && input.stairR[i] == r) return true;
            return false;
        }

        /// <summary>Is the cell (c, r) open towards its neighbour (c + dx, r + dy)? False for a neighbour off the floor.</summary>
        static bool OpenToward(FloorShape shape, int cols, int rows, int c, int r, int dx, int dy)
        {
            int nx = c + dx, ny = r + dy;
            return nx >= 0 && ny >= 0 && nx < cols && ny < rows && shape.IsOpen(c, r, nx, ny);
        }

        /// <summary>
        /// The guards' own elevator and the guard outpost, on real floors of several sizes (the planner is pure, so this runs outside Play
        /// Mode): the shaft is in the corner opposite the players' elevator, in the same cell on every floor of a size (so the shafts line up);
        /// its car stands clear of the furniture and so does the run from its door to the doorway; the outpost is in another cell (the farthest
        /// from the elevator that is not the shaft's), has no elevator alcove any more, a locker along its whole back wall and a clear strip in
        /// front of its window and door; both doors face an open neighbour; and the same input plans the same floor.
        /// </summary>
        public static void GuardShaftTest()
        {
            bool shaftCorner = true, shaftSameCell = true, outpostElsewhere = true, outpostFarthest = true, doorsOpen = true, inCell = true,
                 noAlcove = true, oneOutpost = true, carClear = true, laneClear = true, frontClear = true, lockersOk = true, deterministic = true;
            bool stairsCount = true, stairsCorner = true, stairsApart = true, stairDoorsOpen = true, stairBlockClear = true, stairLaneClear = true, stairsNotOutpost = true;
            int floors = 0, minLockers = int.MaxValue;
            const float cs = 18f;
            foreach (var (cols, rows) in new[] { (8, 5), (7, 5), (6, 4), (9, 6), (3, 2) })
            {
                var shaftCells = new HashSet<string>();
                foreach (var (fl, sd) in new[] { (1, 4101), (2, 777), (3, 31), (5, 9172), (8, 2288), (12, 515) })
                {
                    var shape = FloorShape.Maze(new System.Random(sd), cols, rows, 0.12);
                    var input = shape.PlanFor(fl, sd, cs, 3.5f, 777);
                    var dist = shape.Distances();
                    floors++;

                    // ---- where they are
                    shaftCorner &= input.elevC == 0 && input.elevR == 0 && input.guestC == cols - 1 && input.guestR == rows - 1;
                    shaftCells.Add(input.guestC + "," + input.guestR);
                    bool sameAsShaft = input.outpostC == input.guestC && input.outpostR == input.guestR;
                    outpostElsewhere &= input.outpost && !sameAsShaft && !(input.outpostC == 0 && input.outpostR == 0);
                    for (int c = 0; c < cols; c++)
                        for (int r = 0; r < rows; r++)
                            if (!(c == input.guestC && r == input.guestR) && !IsStairCell(input, c, r)) outpostFarthest &= dist[c, r] <= dist[input.outpostC, input.outpostR];
                    doorsOpen &= OpenToward(shape, cols, rows, input.guestC, input.guestR, input.guestDirX, input.guestDirY)
                              && OpenToward(shape, cols, rows, input.outpostC, input.outpostR, input.outpostDirX, input.outpostDirY);

                    // ---- the stairwells: the two corners that are neither elevator's
                    stairsCount &= input.stairC != null && input.stairC.Length == 2 && input.stairR.Length == 2 && input.stairDirX.Length == 2 && input.stairDirY.Length == 2;
                    if (input.stairC != null && input.stairC.Length == 2)
                    {
                        stairsCorner &= input.stairC[0] == 0 && input.stairR[0] == rows - 1 && input.stairC[1] == cols - 1 && input.stairR[1] == 0;
                        stairsApart &= !(input.stairC[0] == input.stairC[1] && input.stairR[0] == input.stairR[1])
                                    && !IsStairCell(input, 0, 0) && !IsStairCell(input, input.guestC, input.guestR);
                        stairsNotOutpost &= !IsStairCell(input, input.outpostC, input.outpostR);
                        for (int i = 0; i < 2; i++)
                            stairDoorsOpen &= OpenToward(shape, cols, rows, input.stairC[i], input.stairR[i], input.stairDirX[i], input.stairDirY[i]);
                    }

                    // ---- the plan
                    var lay = OfficePlanner.Build(input);
                    var again = OfficePlanner.Build(shape.PlanFor(fl, sd, cs, 3.5f, 777));
                    var op = lay.outpost;
                    if (op == null) { oneOutpost = false; continue; }
                    deterministic &= again.outpost != null && again.outpost.outer.ToString() == op.outer.ToString()
                                  && again.pieces.Count == lay.pieces.Count && again.rooms.Count == lay.rooms.Count;
                    int outposts = 0;
                    foreach (var room in lay.rooms) if (room.theme == "outpost") outposts++;
                    oneOutpost &= outposts == 1;

                    var o = op.outer;
                    inCell &= o.x0 >= input.outpostC * cs && o.x1 <= (input.outpostC + 1) * cs && o.z0 >= input.outpostR * cs && o.z1 <= (input.outpostR + 1) * cs
                           && op.bounds.Contains(op.room.wx, op.room.wz);
                    noAlcove &= op.room.blocks.Count == 0 && typeof(OutpostInfo).GetField("car") == null && typeof(OutpostInfo).GetField("carInner") == null;

                    // ---- the guards' elevator: nothing in its car, nothing in the run from its door to the doorway of its cell
                    float cx = (input.guestC + 0.5f) * cs, cz = (input.guestR + 0.5f) * cs;
                    var car = new Rc(cx - 2.8f, cz - 2.8f, cx + 2.8f, cz + 2.8f);
                    int dx = input.guestDirX, dz = input.guestDirY;
                    var lane = dx != 0
                        ? new Rc(dx > 0 ? cx + 2.7f : cx - cs / 2f, cz - 1.9f, dx > 0 ? cx + cs / 2f : cx - 2.7f, cz + 1.9f)
                        : new Rc(cx - 1.9f, dz > 0 ? cz + 2.7f : cz - cs / 2f, cx + 1.9f, dz > 0 ? cz + cs / 2f : cz - 2.7f);

                    // ---- the strip in front of the outpost's front wall (the window needs a view, the lock room to stand)
                    Rc strip = op.frontX < 0 ? new Rc(o.x0 - 3.2f, o.z0 - 0.4f, o.x0, o.z1 + 0.4f)
                             : op.frontX > 0 ? new Rc(o.x1, o.z0 - 0.4f, o.x1 + 3.2f, o.z1 + 0.4f)
                             : op.frontZ < 0 ? new Rc(o.x0 - 0.4f, o.z0 - 3.2f, o.x1 + 0.4f, o.z0)
                             : new Rc(o.x0 - 0.4f, o.z1, o.x1 + 0.4f, o.z1 + 3.2f);

                    var stairBlocks = new List<Rc>(); var stairLanes = new List<Rc>();
                    for (int i = 0; input.stairC != null && i < input.stairC.Length; i++)
                    {
                        float sx = (input.stairC[i] + 0.5f) * cs, sz = (input.stairR[i] + 0.5f) * cs;
                        stairBlocks.Add(new Rc(sx - OfficePlanner.StairHalf, sz - OfficePlanner.StairHalf, sx + OfficePlanner.StairHalf, sz + OfficePlanner.StairHalf));
                        int ddx = input.stairDirX[i], ddz = input.stairDirY[i];
                        stairLanes.Add(ddx != 0
                            ? new Rc(ddx > 0 ? sx + 2.3f : sx - cs / 2f, sz - 1.7f, ddx > 0 ? sx + cs / 2f : sx - 2.3f, sz + 1.7f)
                            : new Rc(sx - 1.7f, ddz > 0 ? sz + 2.3f : sz - cs / 2f, sx + 1.7f, ddz > 0 ? sz + cs / 2f : sz - 2.3f));
                    }
                    int lockers = 0;
                    foreach (var p in lay.pieces)
                    {
                        if (p.key == "locker2" && op.bounds.Contains(p.x, p.z)) lockers++;
                        if (p.mount) continue;                          // (things hung on a wall are in nobody's way)
                        carClear &= !p.rc.Overlaps(car);
                        laneClear &= !p.rc.Overlaps(lane);
                        frontClear &= !p.rc.Overlaps(strip);
                        foreach (var b in stairBlocks) stairBlockClear &= !p.rc.Overlaps(b);
                        foreach (var l in stairLanes) stairLaneClear &= !p.rc.Overlaps(l);
                    }
                    minLockers = Mathf.Min(minLockers, lockers);
                    lockersOk &= lockers >= 5;
                }
                shaftSameCell &= shaftCells.Count == 1;
            }
            Check($"guard shaft: planned {floors} floors of five sizes", floors == 30);
            Check("guard shaft: the guards' elevator is in the corner opposite the players' on every floor", shaftCorner);
            Check("guard shaft: it is the same cell on every floor of a size (the shafts line up)", shaftSameCell);
            Check("guard shaft: the outpost is in another cell, never the elevator's", outpostElsewhere);
            Check("guard shaft: ... the farthest from the elevator that is not the shaft's", outpostFarthest);
            Check("guard shaft: both doors face an open neighbour", doorsOpen);
            Check("guard shaft: exactly one outpost room per floor", oneOutpost);
            Check("guard shaft: the outpost lies inside its own cell and its standing spot inside the room", inCell);
            Check("guard shaft: the outpost has no elevator alcove (no block in the room, no car fields)", noAlcove);
            Check("guard shaft: no prop stands in the guards' elevator car", carClear);
            Check("guard shaft: no prop stands in the run from the car's door to the doorway of its cell", laneClear);
            Check("guard shaft: nothing stands in the strip in front of the outpost's window and door", frontClear);
            Check($"guard shaft: at least five lockers in the outpost (fewest on any floor: {minLockers})", lockersOk);
            Check("guard shaft: the same input plans the same floor", deterministic);
            Check("stairwells: two on every floor", stairsCount);
            Check("stairwells: in the two corners that are neither elevator's (north-west and south-east)", stairsCorner);
            Check("stairwells: not the players' elevator's cell, not the guards' elevator's, not each other's", stairsApart);
            Check("stairwells: the outpost is in none of them", stairsNotOutpost);
            Check("stairwells: every door faces an open neighbour", stairDoorsOpen);
            Check("stairwells: no prop stands in a landing", stairBlockClear);
            Check("stairwells: no prop stands in the run from a stairwell's door to the doorway of its cell", stairLaneClear);
        }
    }
}
