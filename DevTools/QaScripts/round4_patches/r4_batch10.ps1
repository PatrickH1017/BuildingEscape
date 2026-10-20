# Round 4, batch 10: the tests of the core rooms (A9 break room exists, A10 restrooms: a pair, locked doors, signs, nothing inside). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR4Test.cs')
Rep '        /// <summary>A1 / A2 of round 4: the elevator doors' '        /// <summary>
        /// A9 / A10 of round 4 (the owner, 2026-10-17 night): every floor has a break room (with its own door, a decent size) and the two restrooms: a Men''s and a Women''s room by the elevator lobby, each behind a
        /// door that never opens (Layout.deadDoors), with a pictogram sign beside the door and an OUT OF ORDER sign on it, nothing furnished inside, no patrol stop inside, no room number.
        /// </summary>
        public static void CoreRoomsTest()
        {
            int floors = 0;
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
                            var signs = lay.pieces.Where(p => (p.key == "sign_wc_men" || p.key == "sign_wc_women") && (p.x - dx) * (p.x - dx) + (p.z - dz) * (p.z - dz) < 2.4f * 2.4f).ToList();
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
            Check($"core: all {floors} planned floors have exactly two restrooms, a Men''s and a Women''s", pairs && menWomen);
            Check("core: each restroom door is a doorway that never opens (deadDoors), and the rooms are not numbered", doorsOk && unnumbered);
            Check("core: nothing is furnished inside a restroom and no patrol stop lies inside", empty && stops);
            Check("core: each restroom door has one pictogram sign beside it (1.0-1.8 m up) and an OUT OF ORDER sign on it", signsOk);
            Check("core: the restrooms are by the elevator lobby (within three quarters of the floor''s length of it)", nearLobby);
            Check("core: every floor has a break room with a door of its own, at least 4 m across", hasBreak);
        }

        /// <summary>A1 / A2 of round 4: the elevator doors'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevTest.cs')
Rep '            DockWorkersTest();' '            DockWorkersTest();
            CoreRoomsTest();'
Save-Src
Write-Output 'round 4 batch 10 patched'
