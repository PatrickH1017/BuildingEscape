# Tests for the beacons on the walls (the owner, 2026-10-24: "placed high up on the walls sporadically (touching the wall)"): the pure rules (NetDevTest.AlarmRulesTest), the Play scenario AlarmLooks and the built-player self-play (QaAutopilot).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Editor\NetDev\NetDevAlarmLooks.cs')
Rep '        public static void AlarmRulesTest()' @'
        /// <summary>The four wall boxes (0.2 m thick) round a room of a test floor; <paramref name="doorX0"/> to <paramref name="doorX1"/> leaves a doorway in its south wall.</summary>
        static void BeaconTestShell(Rc rr, List<Rc> shell, float doorX0 = 0f, float doorX1 = 0f)
        {
            const float t = 0.2f;
            if (doorX1 > doorX0) { shell.Add(new Rc(rr.x0 - t, rr.z0 - t, doorX0, rr.z0)); shell.Add(new Rc(doorX1, rr.z0 - t, rr.x1 + t, rr.z0)); }
            else shell.Add(new Rc(rr.x0 - t, rr.z0 - t, rr.x1 + t, rr.z0));
            shell.Add(new Rc(rr.x0 - t, rr.z1, rr.x1 + t, rr.z1 + t));
            shell.Add(new Rc(rr.x0 - t, rr.z0, rr.x0, rr.z1));
            shell.Add(new Rc(rr.x1, rr.z0, rr.x1 + t, rr.z1));
        }

        static List<BeaconSpot> BeaconTestPlan(List<Rc> shell, List<Rc> halls, List<Rc> bigs, int seed)
        {
            var faces = new List<WallFace>();
            foreach (var w in shell) BeaconRules.FacesOf(w.x0, w.z0, w.x1, w.z1, 0.6f, faces);
            return BeaconRules.Plan(faces, seed,
                (x, z) => { foreach (var h in halls) if (h.Contains(x, z)) return 2; foreach (var b in bigs) if (b.Contains(x, z)) return 1; return 0; },
                (x, z) => { foreach (var w in shell) if (w.Contains(x, z)) return true; return false; });
        }

        public static void AlarmRulesTest()
'@
RepRegion '            // ---- the beacons: the corners of the hallways (a long one also along its walls) and two opposite corners of every big room' 'BeaconRules.Plan(null, null).Count == 0, $"({thin.Count})");' @'
            // ---- the beacons: scattered over the wall faces of the hallways and the big rooms, high up, their backs against the wall, facing out into the room (the owner, 2026-10-24: "placed high up on the walls sporadically (touching the wall)")
            var hall = new Rc(0f, 0f, 30f, 3f);
            var big = new Rc(40f, 0f, 52f, 10f);
            var small = new Rc(60f, 0f, 63f, 4f);
            var shell = new List<Rc>();
            BeaconTestShell(hall, shell); BeaconTestShell(big, shell); BeaconTestShell(small, shell);
            var halls = new List<Rc> { hall };
            var bigs = new List<Rc> { big };                                                              // (the 12 m2 room is no big room: nothing is wanted in it)
            var plan = BeaconTestPlan(shell, halls, bigs, 7);
            int inHall = plan.Count(b => b.hall), inBig = plan.Count(b => !b.hall);
            bool inSmall = plan.Any(b => small.Grow(0.5f).Contains(b.x, b.z));
            Check("beacons: the walls of a 30 m hallway and of a 120 m2 room carry two or more each, the walls of a 12 m2 room none", inHall >= 2 && inBig >= 2 && !inSmall, $"({plan.Count}: {inHall} in the hallway, {inBig} in the room, small room {inSmall})");
            bool onWall = true, facing = true, awayFromCorners = true;
            foreach (var b in plan)
            {
                var r = b.hall ? hall : big;
                float dEdge = Mathf.Min(Mathf.Min(Mathf.Abs(b.x - r.x0), Mathf.Abs(b.x - r.x1)), Mathf.Min(Mathf.Abs(b.z - r.z0), Mathf.Abs(b.z - r.z1)));
                onWall &= dEdge < 1e-3f && r.Grow(0.001f).Contains(b.x, b.z);
                float fx = Mathf.Sin(b.yaw * Mathf.Deg2Rad), fz = Mathf.Cos(b.yaw * Mathf.Deg2Rad);
                facing &= r.Contains(b.x + fx * 0.5f, b.z + fz * 0.5f) && !r.Contains(b.x - fx * 0.5f, b.z - fz * 0.5f);
                float toCorner = Mathf.Min(Mathf.Min(Vector2.Distance(new Vector2(b.x, b.z), new Vector2(r.x0, r.z0)), Vector2.Distance(new Vector2(b.x, b.z), new Vector2(r.x1, r.z0))),
                                           Mathf.Min(Vector2.Distance(new Vector2(b.x, b.z), new Vector2(r.x0, r.z1)), Vector2.Distance(new Vector2(b.x, b.z), new Vector2(r.x1, r.z1))));
                awayFromCorners &= toCorner >= BeaconRules.EndMargin - 0.21f;
            }
            Check("beacons: every beacon stands on the face of a wall of its room (never in the middle of it), facing straight out into the room, and clear of the corners", onWall && facing && awayFromCorners, $"(on a wall {onWall}, facing {facing}, off the corners {awayFromCorners})");
            float closest = float.MaxValue;
            for (int i = 0; i < plan.Count; i++) for (int j = i + 1; j < plan.Count; j++) closest = Mathf.Min(closest, Vector2.Distance(new Vector2(plan[i].x, plan[i].z), new Vector2(plan[j].x, plan[j].z)));
            Check("beacons: they are scattered, not in a row: no two are closer than 9 m", closest >= BeaconRules.MinGap - 1e-3f && Mathf.Abs(BeaconRules.MinGap - 9f) < 1e-5f, $"(closest {closest:0.00} m)");
            var again = BeaconTestPlan(shell, halls, bigs, 7);
            var other = BeaconTestPlan(shell, halls, bigs, 8);
            bool same = again.Count == plan.Count, differs = other.Count != plan.Count;
            for (int i = 0; i < Mathf.Min(plan.Count, again.Count); i++) same &= again[i].x == plan[i].x && again[i].z == plan[i].z && again[i].yaw == plan[i].yaw;
            for (int i = 0; i < Mathf.Min(plan.Count, other.Count); i++) differs |= other[i].x != plan[i].x || other[i].z != plan[i].z;
            Check("beacons: the same seed gives the same plan on every machine, another seed another one", same && differs, $"(same {same}, other seed differs {differs})");
            bool clearOfDoor = true;
            var shellDoor = new List<Rc>(); BeaconTestShell(hall, shellDoor, 14f, 16f);
            for (int seed = 1; seed <= 25; seed++)
                foreach (var b in BeaconTestPlan(shellDoor, halls, bigs, seed))
                    if (Mathf.Abs(b.z) < 1e-3f && b.x > 13.05f && b.x < 16.95f) clearOfDoor = false;
            Check("beacons: none is hung at a doorway (the wall's face ends there, and a beacon keeps 0.9 m from its ends)", clearOfDoor);
            var shell3 = new List<Rc>(); var halls3 = new List<Rc>();
            for (int i = 0; i < 40; i++) { var h = new Rc(0f, i * 20f, 30f, i * 20f + 3f); halls3.Add(h); BeaconTestShell(h, shell3); }
            var thin = BeaconTestPlan(shell3, halls3, new List<Rc>(), 3);
            Check("beacons: a floor with a lot of hallways has at most 60 beacons and an empty floor has none", thin.Count == BeaconRules.MaxBeacons && BeaconRules.Plan(null, 1, null, null).Count == 0, $"({thin.Count})");
            float hmin = 1f, hmax = 0f; bool hashSteady = true;
            for (int i = 0; i < 2000; i++) { float v = BeaconRules.Hash01(i, i * 3, 5, 9); hmin = Mathf.Min(hmin, v); hmax = Mathf.Max(hmax, v); hashSteady &= v == BeaconRules.Hash01(i, i * 3, 5, 9); }
            Check("beacons: the random numbers of the plan are in [0, 1), spread over it, and the same every time", hmin >= 0f && hmax < 1f && hmin < 0.05f && hmax > 0.95f && hashSteady, $"({hmin:0.000} to {hmax:0.000})");
            Check("beacons: the lamp hangs 0.30 m under the ceiling, its ring's back 2 cm off the wall", Mathf.Abs(BeaconRules.Drop - 0.30f) < 1e-5f && Mathf.Abs(BeaconRules.Standoff - 0.02f) < 1e-5f);
'@
Rep '            AlarmCheck("floor 1 has alarm beacons in its corners", bc != null && bc.Count >= 6 && bc.Count <= BeaconRules.MaxBeacons, $"({(bc != null ? bc.Count : -1)})");' '            AlarmCheck("floor 1 has alarm beacons on its walls", bc != null && bc.Count >= 6 && bc.Count <= BeaconRules.MaxBeacons, $"({(bc != null ? bc.Count : -1)})");'
Rep '            AlarmCheck("all of them hang just under the ceiling", high);' @'
            AlarmCheck("all of them hang just under the ceiling", high);
            AlarmCheck("every beacon is mounted on a wall: a ray straight back from the lamp hits the wall's face 2.7 cm behind its middle", bc.TouchingWall() == bc.Count, $"({bc.TouchingWall()} of {bc.Count})");
            float apart = float.MaxValue;
            for (int i = 0; i < bc.Count; i++) for (int j = i + 1; j < bc.Count; j++) { var d = bc.WallPointOf(i) - bc.WallPointOf(j); d.y = 0f; apart = Mathf.Min(apart, d.magnitude); }
            AlarmCheck("they are scattered: no two are closer than 9 m", apart >= BeaconRules.MinGap - 0.05f, $"(closest {apart:0.0} m)");
'@
Rep '            var at = bc.PositionOf(0);' @'
            // the beacon with the most room in front of it, so that the picture shows the lamp on its wall and the room it looks into
            int pick = 0; float room = 0f;
            for (int i = 0; i < bc.Count; i++)
            {
                var fwdI = Quaternion.Euler(0f, bc.BeamYaw(i), 0f) * Vector3.forward;
                float free = Physics.Raycast(new Vector3(bc.PositionOf(i).x, 1.4f, bc.PositionOf(i).z), fwdI, out var hitI, 7f, ~0, QueryTriggerInteraction.Ignore) ? hitI.distance : 7f;
                if (free > room) { room = free; pick = i; }
            }
            var at = bc.PositionOf(pick);
'@
Rep '            float yaw0 = bc.BeamYaw(0);' '            float yaw0 = bc.BeamYaw(pick);'
Rep '            player.TeleportTo(new Vector3(at.x + facing.x * 6f, 1.1f, at.z + facing.z * 6f), Quaternion.LookRotation(-facing));' '            float stand = Mathf.Max(1.5f, Mathf.Min(6f, room - 0.8f));
            player.TeleportTo(new Vector3(at.x + facing.x * stand, 1.1f, at.z + facing.z * stand), Quaternion.LookRotation(-facing));'
Rep '                    cam.transform.position = new Vector3(at.x + facing.x * 4f, 1.9f, at.z + facing.z * 4f);' '                    float camBack = Mathf.Max(1.5f, Mathf.Min(4f, room - 0.8f));
                    cam.transform.position = new Vector3(at.x + facing.x * camBack, 1.9f, at.z + facing.z * camBack);'
Save-Src

Open-Src ($A + 'Scripts\QaAutopilot.cs')
Rep 'hang dark in the corners (the owner, 2026-10-24)"' 'hang dark high on the walls with their backs against them (the owner, 2026-10-24)"'
Rep 'cornerBeacons.Count >= 4 && cornerBeacons.Level == 0f' 'cornerBeacons.Count >= 4 && cornerBeacons.TouchingWall() == cornerBeacons.Count && cornerBeacons.Level == 0f'
Rep '{(cornerBeacons != null ? cornerBeacons.Count : -1)} beacons)");' '{(cornerBeacons != null ? cornerBeacons.Count : -1)} beacons, {(cornerBeacons != null ? cornerBeacons.TouchingWall() : -1)} on a wall)");'
Save-Src
"beacon wall tests patched"
