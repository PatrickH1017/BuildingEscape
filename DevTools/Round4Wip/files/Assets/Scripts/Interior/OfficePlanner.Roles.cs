using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The two kinds of room the security computer can lock (the owner, 2026-10-17 evening: "each floor should have at least one or multiple Executive Suites (the ones with glass walls and
    /// doors) and they should have the safes, with a locked door that can be unlocked at the computer" and "each floor should have a Server Room with a locked door that can be unlocked at
    /// the computer"): <see cref="RoomInfo.role"/> "suite" and "server". Decided here, in the pure planner, from the finished floor, so every machine and the tests agree.
    /// </summary>
    public sealed partial class OfficePlanner
    {
        public const string RoleSuite = "suite", RoleServer = "server";

        /// <summary>How many executive suites a floor of this many cells gets.</summary>
        public static int SuitesFor(int cells) => cells >= 24 ? 2 : 1;

        int OutsideSides(Rc b)
        {
            float width = inp.cols * cs, depth = inp.rows * cs;
            int n = 0;
            if (b.x0 < 0.7f) n++;
            if (b.x1 > width - 0.7f) n++;
            if (b.z0 < 0.7f) n++;
            if (b.z1 > depth - 0.7f) n++;
            return n;
        }

        /// <summary>The rooms, the ones at the building's edge first, then in an order that follows from the seed and the room's place (never from a string's hash).</summary>
        List<RoomCtx> Ranked(List<RoomCtx> list)
        {
            var keyed = new List<KeyValuePair<long, RoomCtx>>();
            foreach (var rc in list)
            {
                var b = rc.info.bounds;
                long key = -(long)OutsideSides(b) * 1000000L + Mix(inp.seed, rc.info.cellX * 31 + rc.info.cellY, (int)(b.x0 * 10f) + (int)(b.z0 * 10f) * 7, 91) % 100000;
                keyed.Add(new KeyValuePair<long, RoomCtx>(key, rc));
            }
            keyed.Sort((a, c) => a.Key.CompareTo(c.Key));
            var result = new List<RoomCtx>();
            foreach (var kv in keyed) result.Add(kv.Value);
            return result;
        }

        static float Narrow(RoomCtx rc) => Math.Min(rc.w, rc.d);

        /// <summary>Picks the executive suites and the server room of the floor, and takes the patrol stops out of them (a guard must never patrol into a room that can be locked).</summary>
        void PickRoles()
        {
            var numbered = new List<RoomCtx>();
            foreach (var rc in allRooms) if (RoomNumbers.IsNumbered(rc.info) && rc.theme != ThemeBreak) numbered.Add(rc);        // (the break room is for everybody: never a suite, never the server room)
            if (numbered.Count == 0) return;

            // ---- the suites: the big offices (the ExecOffice kind: at least 5 x 5 m), then smaller offices, then meeting rooms, then whatever has room for a desk and a safe
            int want = SuitesFor(inp.cols * inp.rows);
            var suites = new List<RoomCtx>();
            var tiers = new Func<RoomCtx, bool>[]
            {
                rc => rc.theme == "office" && Narrow(rc) >= 5f,
                rc => rc.theme == "office" && Narrow(rc) >= 3.9f,
                rc => (rc.theme == "conference" || rc.theme == "meeting" || rc.theme == "training") && Narrow(rc) >= 4.5f,
                rc => rc.theme != "server" && rc.theme != "storage" && rc.theme != "copy" && Narrow(rc) >= 3.5f,
            };
            foreach (var tier in tiers)
            {
                if (suites.Count >= want) break;
                var pool = numbered.FindAll(rc => !suites.Contains(rc) && tier(rc));
                foreach (var rc in Ranked(pool)) { if (suites.Count >= want) break; suites.Add(rc); }
            }

            // ---- the server room: the biggest server room, else the biggest storage or copy room, else the smallest room left
            RoomCtx server = null;
            foreach (var theme in new[] { "server", "storage", "copy" })
            {
                float best = -1f;
                foreach (var rc in numbered)
                    if (!suites.Contains(rc) && rc.theme == theme && rc.w * rc.d > best) { best = rc.w * rc.d; server = rc; }
                if (server != null) break;
            }
            if (server == null)
            {
                float smallest = float.MaxValue;
                foreach (var rc in numbered)
                    if (!suites.Contains(rc) && rc.w * rc.d < smallest) { smallest = rc.w * rc.d; server = rc; }
            }

            foreach (var rc in suites) rc.info.role = RoleSuite;
            if (server != null) server.info.role = RoleServer;

            // no patrol stop inside either kind of room
            layout.waypoints.RemoveAll(wp =>
            {
                foreach (var rc in allRooms) if (rc.info.role != null && rc.info.bounds.Contains(wp[0], wp[1])) return true;
                return false;
            });
        }
    }
}
