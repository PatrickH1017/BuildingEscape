using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// Round 4 (the owner's R1 and R2 of 2026-10-17 evening): every office floor has at least one executive suite (two from 24 cells) and exactly one server room, decided by the planner;
        /// both are numbered rooms with a door, no patrol stop lies inside them, and the floor's safe goes into a suite.
        /// </summary>
        public static void RolesTest()
        {
            int floors = 0, withSafe = 0, safeInSuite = 0, fullSuites = 0;
            bool suitesOk = true, serverOk = true, noStops = true, rolesApart = true, bigEnough = true;
            foreach (var (cols, rows) in new[] { (8, 5), (7, 5), (6, 4), (9, 6) })       // (a floor of 3 x 2 cells has no numbered room at all: no suite is possible there, and the game never makes one)
                foreach (int sd in new[] { 4101, 777, 31, 9172 })
                    foreach (int floor in new[] { 1, 2, 3, 4, 7, 12 })
                    {
                        var shape = FloorShape.Maze(new System.Random(sd), cols, rows, 0.12);
                        var lay = OfficePlanner.Build(shape.PlanFor(floor, sd, 18f, 3.5f, 5 * sd + floor));
                        floors++;
                        int suites = 0, servers = 0;
                        foreach (var r in lay.rooms)
                        {
                            if (r.role == OfficePlanner.RoleSuite)
                            {
                                suites++;
                                suitesOk &= RoomNumbers.IsNumbered(r);
                                bigEnough &= Mathf.Min(r.bounds.W, r.bounds.D) >= 3.4f;
                            }
                            else if (r.role == OfficePlanner.RoleServer) { servers++; serverOk &= RoomNumbers.IsNumbered(r); }
                            else if (r.role != null) rolesApart = false;
                            if (r.role != null) foreach (var wp in lay.waypoints) noStops &= !r.bounds.Contains(wp[0], wp[1]);
                        }
                        suitesOk &= suites >= 1 && suites <= OfficePlanner.SuitesFor(cols * rows);
                        if (suites == OfficePlanner.SuitesFor(cols * rows)) fullSuites++;
                        serverOk &= servers == 1;
                        if (lay.safe != null) { withSafe++; if (lay.safe.room.role == OfficePlanner.RoleSuite) safeInSuite++; }
                    }
            Check($"roles: every one of {floors} planned floors has at least one executive suite (two from 24 cells), all numbered rooms with a door", suitesOk);
            Check("roles: ... and exactly one server room, a numbered room too; no other role exists", serverOk && rolesApart);
            Check("roles: a suite is at least 3.4 m across, and no patrol stop lies inside a suite or the server room", bigEnough && noStops);
            Check($"roles: nearly always the full number of suites ({fullSuites} of {floors}) and the floor's safe stands in a suite ({safeInSuite} of {withSafe})", fullSuites * 10 >= floors * 9 && (withSafe == 0 || safeInSuite * 10 >= withSafe * 8));
            Check("roles: the numbers of suites and of the server room are painted (no plate), the others have plates", PaintedNumbersCheck());
        }

        /// <summary>
        /// A6 / A8 of round 4 (the owner, 2026-10-17 night): the playable characters are the Synty office people, called "Dock worker 1", "Dock worker 2" and so on; the defaults, the migration of an old saved look,
        /// the cutscene cast and the boss follow. Needs the character catalog (Employees Take Revenge > Setup Characters) built with the Synty pack imported.
        /// </summary>
        public static void DockWorkersTest()
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) { Check("dock workers: the character catalog exists", false); return; }
            var players = cat.Players();
            bool allOffice = players.Count >= 12, atlas = true, numbered = true, noGuardLooks = true, fits = true;
            for (int i = 0; i < players.Count; i++)
            {
                var c = players[i];
                allOffice &= c.id.StartsWith("syn_") && c.prefab != null;
                atlas &= c.atlas != null;
                numbered &= cat.DockLabel(c) == "Dock worker " + (i + 1);
                noGuardLooks &= !c.id.Contains("security") && c.id != IntroContent.BossModelId;
                var a = PlayerAppearance.ForSlot(0); a.model = c.id;
                fits &= a.Serialize().Length <= 61 && CharacterCatalog.ValidId(c.id);
            }
            Check($"dock workers: the {players.Count} playable characters are all Synty office people with their own atlas", allOffice && atlas);
            Check("dock workers: Customize calls them Dock worker 1, Dock worker 2 ... in order, and nobody looks like a guard (security) or the cutscene's boss", numbered && noGuardLooks);
            bool four = players.Count >= 4;
            for (int i = 0; four && i < 4; i++) four &= players[i].id == CharacterCatalogBuilder.DockWorkerIds[i] && PlayerAppearance.DefaultToons[i] == players[i].id;
            Check("dock workers: Dock worker 1-4 are the four characters the owner showed (cap, blonde woman, glasses + headphones, vest) and the first four defaults", four);
            Check("dock workers: the security guards are not offered to players, but the pack's other people are (\"most of them\")", !players.Any(p => p.id.Contains("security")) && cat.Find("syn_security_male_01") != null && cat.Find("syn_security_female_01") != null && players.Any(p => p.id == "syn_business_female_01") && players.Any(p => p.id == "syn_developer_male_01"));
            Check("dock workers: every id fits the look string that is sent over the network", fits);
            bool defaultsPlayable = PlayerAppearance.DefaultToons.Length >= 5 && PlayerAppearance.DefaultToons.Distinct().Count() == PlayerAppearance.DefaultToons.Length;
            foreach (var id in PlayerAppearance.DefaultToons) defaultsPlayable &= players.Any(p => p.id == id);
            for (int s = 0; s < 5; s++) defaultsPlayable &= PlayerAppearance.ForSlot(s).model == PlayerAppearance.DefaultToons[s];
            Check("dock workers: the five default slots are five different playable dock workers", defaultsPlayable);
            var oldToon = PlayerAppearance.ForSlot(2); oldToon.model = "male_a";
            var party = PlayerAppearance.ForSlot(2); party.model = null;
            var chosen = PlayerAppearance.ForSlot(2); chosen.model = players[players.Count - 1].id;
            Check("dock workers: an old saved look (a Tiny Toon person, the Party character) becomes the first dock worker, a chosen dock worker stays",
                PlayerAppearance.Migrated(oldToon).model == PlayerAppearance.DefaultToons[0] && PlayerAppearance.Migrated(party).model == PlayerAppearance.DefaultToons[0] && PlayerAppearance.Migrated(chosen).model == chosen.model
                && PlayerAppearance.Migrated(party).height == party.height && PlayerAppearance.Migrated(party).body.Equals(party.body));
            var boss = cat.Find(IntroContent.BossModelId);
            Check("dock workers: the cutscene's boss is the Synty boss and no player can be him", boss != null && boss.id == "syn_boss_male_01" && boss.role != ToonRole.Player && !players.Any(p => p.id == boss.id));
            bool cast = true; foreach (var id in PlayerAppearance.DefaultToons) cast &= cat.Find(id) != null;
            Check("dock workers: the old Tiny Toon people stay in the catalog (old saved looks, the guards, the zombie)", cat.Find("male_a") != null && cat.Find("police") != null && cat.Find("zombie") != null && cat.FirstWithRole(ToonRole.Guard) != null && cast);
        }

        /// <summary>
        /// A9 / A10 of round 4 (the owner, 2026-10-17 night): every floor has a break room (with its own door, a decent size) and the two restrooms: a Men's and a Women's room by the elevator lobby, each behind a
        /// door that never opens (Layout.deadDoors), with a pictogram sign beside the door and an OUT OF ORDER sign on it, nothing furnished inside, no patrol stop inside, no room number.
        /// </summary>
        public static void CoreRoomsTest()
        {
            int floors = 0, diag = 0;
            bool pairs = true, doorsOk = true, empty = true, signsOk = true, stops = true, unnumbered = true, menWomen = true, hasBreak = true, nearLobby = true;
            foreach (var (cols, rows) in new[] { (8, 5), (7, 5), (6, 4), (9, 6) })
                foreach (int sd in new[] { 4101, 777, 31, 9172 })
                    foreach (int floor in new[] { 1, 2, 3, 4, 7, 12 })
                    {
                        var shape = FloorShape.Maze(new System.Random(sd), cols, rows, 0.12);
                        var plan = shape.PlanFor(floor, sd, 18f, 3.5f, 5 * sd + floor);
                        var lay = OfficePlanner.Build(plan);
                        floors++;
                        var rest = lay.rooms.Where(r => r.theme == "restroom").ToList();
                        pairs &= rest.Count == 2;
                        if (rest.Count != 2 && diag++ < 8) Debug.Log($"[NetTest] core diag: {cols}x{rows} seed {sd} floor {floor}: {rest.Count} restrooms, pairs in cells {string.Join(",", lay.rooms.Where(r => r.kind == 0).Select(r => r.theme + (r.bounds.W < 4f || r.bounds.D < 4f ? "s" : "")).Take(40))}");
                        int men = 0, women = 0;
                        foreach (var rr in rest)
                        {
                            bool dead = rr.doors.Count == 1 && lay.deadDoors.Any(d => d.Overlaps(rr.doors[0].Grow(0.05f)));
                            doorsOk &= dead;
                            empty &= !lay.pieces.Any(p => !p.mount && rr.bounds.Contains(p.x, p.z));
                            stops &= !lay.waypoints.Any(wp => rr.bounds.Contains(wp[0], wp[1]));
                            unnumbered &= !RoomNumbers.IsNumbered(rr);
                            if (rr.doors.Count != 1) continue;
                            var dc = rr.doors[0];
                            float dx = dc.Cx, dz = dc.Cz;
                            var signs = lay.pieces.Where(p => (p.key == "sign_wc_men" || p.key == "sign_wc_women") && (p.x - dx) * (p.x - dx) + (p.z - dz) * (p.z - dz) < 1.6f * 1.6f).ToList();
                            var ooo = lay.pieces.Where(p => p.key == "sign_out_of_order" && (p.x - dx) * (p.x - dx) + (p.z - dz) * (p.z - dz) < 0.9f * 0.9f).ToList();
                            signsOk &= signs.Count == 1 && ooo.Count == 1 && signs[0].mount && signs[0].y > 1.0f && signs[0].y < 1.8f;
                            if (signs.Count == 1) { if (signs[0].key == "sign_wc_men") men++; else women++; }
                        }
                        menWomen &= men == 1 && women == 1;
                        hasBreak &= lay.rooms.Any(r => r.theme == "break" && r.kind == 0 && Mathf.Min(r.bounds.W, r.bounds.D) >= 4f);
                        // by the elevator lobby: the restrooms are among the nearest rooms to the elevator, never on the far side of the floor
                        if (rest.Count == 2)
                        {
                            float ex = (plan.elevC + 0.5f) * 18f, ez = (plan.elevR + 0.5f) * 18f;
                            float far = Mathf.Max(plan.cols, plan.rows) * 18f * 0.75f;
                            foreach (var rr in rest) nearLobby &= (rr.bounds.Cx - ex) * (rr.bounds.Cx - ex) + (rr.bounds.Cz - ez) * (rr.bounds.Cz - ez) < far * far;
                        }
                    }
            Check($"core: all {floors} planned floors have exactly two restrooms, a Men's and a Women's", pairs && menWomen);
            Check("core: each restroom door is a doorway that never opens (deadDoors), and the rooms are not numbered", doorsOk && unnumbered);
            Check("core: nothing is furnished inside a restroom and no patrol stop lies inside", empty && stops);
            Check("core: each restroom door has one pictogram sign beside it (1.0-1.8 m up) and an OUT OF ORDER sign on it", signsOk);
            Check("core: the restrooms are by the elevator lobby (within three quarters of the floor's length of it)", nearLobby);
            Check("core: every floor has a break room with a door of its own, at least 4 m across", hasBreak);
        }

        /// <summary>A1 / A2 of round 4: the elevator doors take 2 seconds to open (and close at their old speed), the pop-up before they close counts down over 2 seconds.</summary>
        public static void ElevatorTimingTest()
        {
            float k = 0f; int frames = 0;
            while (k < 1f && frames < 1000) { k = ElevatorDoor.Advance(k, 1f, 1f / 60f, 1.6f); frames++; }
            float kAt19 = 0f; for (int i = 0; i < 114; i++) kAt19 = ElevatorDoor.Advance(kAt19, 1f, 1f / 60f, 1.6f);       // 1.9 s
            float c = 1f; int closeFrames = 0;
            while (c > 0f && closeFrames < 1000) { c = ElevatorDoor.Advance(c, 0f, 1f / 60f, 1.6f); closeFrames++; }
            Check("elevator: the doors take 2 seconds to open (not open yet after 1.9 s, open at 2 s) and the same 0.63 s as before to close",
                ElevatorDoor.OpenSeconds == 2f && frames >= 119 && frames <= 121 && kAt19 > 0.9f && kAt19 < 0.96f && closeFrames >= 37 && closeFrames <= 39, $"(open in {frames} frames, 1.9 s: {kAt19:0.00}, close in {closeFrames} frames)");
            Check("elevator: the pop-up before the doors close counts down over the 2 second delay", ElevatorExtraction.CloseDelay == 2f);
        }

        static bool PaintedNumbersCheck()
        {
            var shape = FloorShape.Maze(new System.Random(777), 8, 5, 0.12);
            var lay = OfficePlanner.Build(shape.PlanFor(2, 777, 18f, 3.5f, 5 * 777 + 2));
            var plates = RoomNumbers.Plates(lay, 2);
            int painted = 0, plain = 0;
            foreach (var p in plates) { if (p.painted) painted++; else plain++; }
            int roleDoors = 0;
            foreach (var r in lay.rooms) if (r.role != null && RoomNumbers.IsNumbered(r)) roleDoors += r.doors.Count;
            return painted == roleDoors && painted >= 2 && plain > painted;
        }
    }
}
