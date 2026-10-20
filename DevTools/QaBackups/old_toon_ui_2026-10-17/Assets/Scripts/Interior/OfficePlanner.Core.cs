using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// What every office floor must have besides the rooms the maze happens to roll (the owner, 2026-10-17 night): a BREAK ROOM ("There should also be a break room on each floor with some tables, chairs,
    /// vending machines and maybe a couch", "can also have a microwave, etc.") and RESTROOMS ("a door players can't open but with a bathroom sign outside of it", "Players should not be allowed to
    /// enter the restroom"). Both are decided here, after every cell is planned and BEFORE any of it is furnished, so no furniture has to be thrown away.
    ///
    /// The restrooms sit by the elevator lobby (an office keeps its elevators, stairs and restrooms together, in the core): two rooms side by side, one Men's and one Women's, each behind a door that
    /// never opens (<see cref="Layout.deadDoors"/>: FloorGenerator builds it as a locked door whose prompt says "Locked"), a pictogram sign beside the door and an OUT OF ORDER sign on it. Nothing is
    /// furnished inside, no patrol stop lies inside, nobody numbers them.
    /// </summary>
    public sealed partial class OfficePlanner
    {
        public const string ThemeRestroom = "restroom", ThemeBreak = "break";

        int CellDistance(int c, int r) => Math.Abs(c - inp.elevC) + Math.Abs(r - inp.elevR);

        /// <summary>A room that can be turned into one of the core rooms: a room with a door of its own, not the guard outpost, not already a break room or a restroom.</summary>
        static bool Convertible(RoomCtx rc) => rc.kind == 0 && rc.theme != "outpost" && rc.theme != ThemeBreak && rc.theme != ThemeRestroom && rc.doors.Count == 1 && rc.info != null && rc.info.doors.Count == 1;

        /// <summary>The Men's and the Women's rooms by the elevator lobby, behind locked doors with signs.</summary>
        void EnsureRestrooms(List<CellCtx> cells)
        {
            RoomCtx[] pair = null;
            // two stacked small rooms of one quadrant (6.4 x 3.1 m each), the nearest to the elevator first
            var cand = new List<KeyValuePair<long, RoomCtx[]>>();
            foreach (var cell in cells)
                foreach (var p in cell.pairs)
                {
                    if (!Convertible(p[0]) || !Convertible(p[1])) continue;
                    long key = (long)CellDistance(cell.c, cell.r) * 1000000L + Mix(inp.seed, cell.c * 17 + cell.r, (int)(p[0].info.bounds.x0 * 10f), 4711) % 100000;
                    cand.Add(new KeyValuePair<long, RoomCtx[]>(key, p));
                }
            cand.Sort((a, b) => a.Key.CompareTo(b.Key));
            if (cand.Count > 0) pair = cand[0].Value;
            else
            {
                // no stacked pair on this floor: the two enclosed rooms nearest to the elevator (the smallest ones first)
                var singles = new List<KeyValuePair<long, RoomCtx>>();
                foreach (var cell in cells)
                    foreach (var rc in cell.rooms)
                    {
                        if (!Convertible(rc)) continue;
                        long key = (long)CellDistance(cell.c, cell.r) * 1000000L + (long)(rc.w * rc.d * 100f) * 10L + Mix(inp.seed, cell.c * 17 + cell.r, (int)(rc.info.bounds.x0 * 10f), 4712) % 10;
                        singles.Add(new KeyValuePair<long, RoomCtx>(key, rc));
                    }
                singles.Sort((a, b) => a.Key.CompareTo(b.Key));
                if (singles.Count >= 2) pair = new[] { singles[0].Value, singles[1].Value };
            }
            if (pair == null) return;

            for (int i = 0; i < 2; i++)
            {
                var rm = pair[i];
                rm.SetTheme(ThemeRestroom);
                var door = rm.info.doors[0];
                layout.deadDoors.Add(door);
                HangDoorSign(rm, i == 0 ? "sign_wc_men" : "sign_wc_women", 1.30f);
                // the notice on the door itself, at the height of the handle (it says why the door never opens)
                var ooo = PropTable.Get("sign_out_of_order");
                float faceZ = -rm.d / 2 - Thick;
                rm.PutDef(ooo, rm.doors[0][0], faceZ - ooo.D / 2, 180f, 1.05f, false, true, 0);
            }
        }

        /// <summary>
        /// A sign on the corridor face of the wall beside a room's door (on the side with more wall), its underside <paramref name="bottom"/> metres up: everything is in the room frame, the door on the south
        /// wall, the corridor on the far side of the wall (local -z). Returns null when the room has no door.
        /// </summary>
        Item HangDoorSign(RoomCtx rm, string signKey, float bottom)
        {
            if (rm.doors.Count == 0) return null;
            float doorX = rm.doors[0][0], doorW = rm.doors[0][1];
            float faceZ = -rm.d / 2 - Thick;                       // the corridor face of the partition
            var sign = PropTable.Get(signKey);
            // the wall beside the doorway: from its edge to the room's own corner
            float leftRoom = -rm.w / 2 - (doorX - doorW / 2), rightRoom = rm.w / 2 - (doorX + doorW / 2);      // (the free wall to either side of the doorway)
            bool useRight = rightRoom >= leftRoom;
            float room = useRight ? rightRoom : leftRoom;
            float edge = useRight ? doorX + doorW / 2 : doorX - doorW / 2;
            float sx = edge + (useRight ? 1f : -1f) * Math.Min(room * 0.5f, 0.25f + sign.W * 0.5f);
            return rm.PutDef(sign, sx, faceZ - sign.D / 2, 180f, bottom, false, true, 0);
        }

        /// <summary>A break room of a decent size on every floor: if the maze rolled none that has its own door, the best-fitting room becomes one.</summary>
        void EnsureBreakRoom(List<CellCtx> cells)
        {
            foreach (var cell in cells)
                foreach (var rc in cell.rooms)
                    if (rc.theme == ThemeBreak && rc.kind == 0 && rc.info != null && Math.Min(rc.w, rc.d) >= 4f) return;

            float centreC = (inp.cols - 1) * 0.5f, centreR = (inp.rows - 1) * 0.5f;
            // the rooms that give up the least when they turn into a break room, in this order
            var order = new[] { "lounge", "conference", "meeting", "training", "storage", "copy", "office", "server" };
            foreach (float minSide in new[] { 6.0f, 5.0f, 4.0f })
                foreach (var theme in order)
                {
                    RoomCtx best = null; long bestKey = long.MaxValue;
                    foreach (var cell in cells)
                        foreach (var rc in cell.rooms)
                        {
                            if (!Convertible(rc) || rc.theme != theme || Math.Min(rc.w, rc.d) < minSide) continue;
                            float dc = cell.c - centreC, dr = cell.r - centreR;
                            long key = (long)((dc * dc + dr * dr) * 100f) * 100000L + Mix(inp.seed, cell.c * 17 + cell.r, (int)(rc.info.bounds.x0 * 10f), 4713) % 100000;
                            if (key < bestKey) { bestKey = key; best = rc; }
                        }
                    if (best != null) { best.SetTheme(ThemeBreak); return; }
                }
        }
    }
}
