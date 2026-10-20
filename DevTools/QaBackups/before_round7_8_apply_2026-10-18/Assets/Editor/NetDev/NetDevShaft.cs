using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BuildingEscape.Net;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check (offline, Greybox scene) of the guards' own elevator and of the guard on duty in the outpost: the shaft is in the corner
    /// opposite the service elevator on every floor, in the same place; the backup arrives in it and walks out of its door, never out of the
    /// outpost; the outpost holds exactly one guard, who neither sees nor hears anything outside his room (even through the open door), starts
    /// to call for backup when somebody steps in, and is stopped by a hit in time.
    /// Run: Unity -batchmode -nographics -executeMethod BuildingEscape.EditorTools.NetDevHarness.Shaft   (look for "[Shaft] DONE fails=0").
    /// The two-process version (the client must see the same floor and the same backup) is ShaftNet: runmp.ps1 -Method ShaftNet -Roles host,c1.
    /// </summary>
    public partial class NetDevRunner
    {
        int shaftFails;

        void ShaftCheck(string name, bool ok, string detail = "")
        {
            if (!ok) shaftFails++;
            Log($"[Shaft] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static float ShaftFlat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        static readonly MethodInfo GuardCanSee = typeof(GuardAI).GetMethod("CanSee", BindingFlags.NonPublic | BindingFlags.Instance);
        static bool GuardSees(GuardAI g, WobblyPlayer p) => (bool)GuardCanSee.Invoke(g, new object[] { p });
        static GuardAI DutyGuard() => GuardAI.All.FirstOrDefault(g => g && g.name == "Guard_Outpost");

        /// <summary>Which landing a point is in: 0 the guards' elevator, 1 and 2 the stairwells, -1 none of them.</summary>
        static int LandingOf(FloorGenerator gen, Vector3 p)
        {
            if (gen.HasGuardShaft && ShaftFlat(p, gen.GuardShaftCentre) < 2.7f) return 0;
            for (int i = 0; i < gen.StairCount; i++) if (ShaftFlat(p, gen.StairCentre(i)) < 2.3f) return i + 1;
            return -1;
        }

        /// <summary>The corners of a guard's cone as drawn on the floor (null while there is none: no shader in a batch run).</summary>
        static Vector3[] ConeOf(GuardAI g)
        {
            var v = g.GetComponent<GuardVision>();
            if (v == null) return null;
            var f = typeof(GuardVision).GetField("verts", BindingFlags.NonPublic | BindingFlags.Instance);
            var o = typeof(GuardVision).GetField("fanObject", BindingFlags.NonPublic | BindingFlags.Instance);
            var go = o != null ? o.GetValue(v) as GameObject : null;
            return go != null && go.activeInHierarchy && f != null ? f.GetValue(v) as Vector3[] : null;
        }

        IEnumerator ShaftScenario()
        {
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            void GoTo(Vector3 p) { player.TeleportTo(new Vector3(p.x, 1.1f, p.z), player.transform.rotation); rb.linearVelocity = Vector3.zero; }

            ShaftCheck("basement: no guards' elevator on the docks", !gen.HasGuardShaft);

            // ================= every office floor: the shaft in the corner, the outpost and its guard
            Vector3 firstShaft = default;
            foreach (int floor in new[] { 1, 3, 8, 12 })
            {
                yield return LoadLevel(hm, floor);
                string f = $"floor {floor}:";
                var info = tm.CurrentInfo;
                var root = gen.Current;
                var corner = gen.CellCentre(new Vector2Int(info.cols - 1, info.rows - 1));
                ShaftCheck($"{f} the guards' elevator is in the corner opposite the service elevator", gen.HasGuardShaft && ShaftFlat(gen.GuardShaftCentre, corner) < 0.05f, $"(at {gen.GuardShaftCentre}, corner {corner})");
                if (floor == 1) firstShaft = gen.GuardShaftCentre;
                else ShaftCheck($"{f} ... exactly where it was on floor 1 (the shafts line up)", ShaftFlat(gen.GuardShaftCentre, firstShaft) < 0.05f);
                var nw = gen.CellCentre(new Vector2Int(0, info.rows - 1)); var se = gen.CellCentre(new Vector2Int(info.cols - 1, 0));
                bool stairsThere = gen.StairCount == 2 && ShaftFlat(gen.StairCentre(0), nw) < 0.05f && ShaftFlat(gen.StairCentre(1), se) < 0.05f;
                ShaftCheck($"{f} two stairwells, in the north-west and south-east corners, where the doors the backup can come out of line up from floor to floor", stairsThere,
                    $"(count {gen.StairCount})");
                ShaftCheck($"{f} each stairwell has its walls, a door and the word STAIRS", gen.StairCount == 2 && root.Find("Stairwell_0") != null && root.Find("Stairwell_1") != null
                    && root.Find("Stairwell_0").GetComponent<ElevatorDoor>() != null && root.Find("Stairwell_1").GetComponent<ElevatorDoor>() != null
                    && root.GetComponentsInChildren<Transform>(true).Count(t => t.name == "sign_stairs_big") == 2);
                var car = root.Find("GuestElevator");
                ShaftCheck($"{f} its car stands in the middle of that cell, with its door", car != null && car.GetComponent<ElevatorDoor>() != null && ShaftFlat(car.position, gen.GuardShaftCentre) < 0.05f);

                bool hasOutpost = gen.TryGetOutpostRoom(out var room);
                ShaftCheck($"{f} the floor has an outpost", hasOutpost);
                if (!hasOutpost) continue;
                var g0 = gen.GuardShaftCentre;
                ShaftCheck($"{f} ... in another cell than the guards' elevator", !room.Overlaps(new Rc(g0.x - 2.8f, g0.z - 2.8f, g0.x + 2.8f, g0.z + 2.8f)));
                var wide = room.Grow(0.4f);
                ShaftCheck($"{f} no elevator door panel stands in the outpost (the backup does not arrive there)",
                    !root.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("GuestPanel") && wide.Contains(t.position.x, t.position.z)));

                var duty = DutyGuard();
                var inside = GuardAI.All.Where(g => g && room.Contains(g.transform.position.x, g.transform.position.z)).ToList();
                ShaftCheck($"{f} exactly one guard stands in the outpost, the one on duty", inside.Count == 1 && inside[0] == duty && duty.stationary, $"({inside.Count} inside: {string.Join(",", inside.Select(g => g.name))})");
                ShaftCheck($"{f} and only one guard on the floor is on duty", GuardAI.All.Count(g => g && g.name == "Guard_Outpost") == 1);
                bool routesOut = true;
                foreach (var g in GuardAI.All)
                {
                    if (!g || g == duty || g.patrolPoints == null) continue;
                    foreach (var pt in g.patrolPoints) if (pt && wide.Contains(pt.position.x, pt.position.z)) routesOut = false;
                }
                ShaftCheck($"{f} no other guard has a patrol stop in the outpost", routesOut);
                ShaftCheck($"{f} the guard on duty faces the way the front wall faces (out of the window)", duty != null && Vector3.Dot(duty.transform.forward, duty.postForward) > 0.95f);

                // the way out of the guards' door leads to the rest of the floor
                var from = gen.GuardShaftCentre + gen.GuardShaftOut * 4.2f;
                var to = gen.CellCentre(Vector2Int.zero) + new Vector3(0f, 0f, 7f);
                bool a = NavMesh.SamplePosition(from, out var h1, 2f, NavMesh.AllAreas), b = NavMesh.SamplePosition(to, out var h2, 3f, NavMesh.AllAreas);
                var path = new NavMeshPath();
                bool walk = a && b && NavMesh.CalculatePath(h1.position, h2.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                ShaftCheck($"{f} the run in front of the guards' door is on the NavMesh and leads on to the rest of the floor", walk, $"(from {a}, to {b}, {path.status})");

                // ---- backup arrives in the car
                if (floor == 1 || floor == 8)
                {
                    int made = gen.SpawnReinforcements(to, 2);
                    yield return null;
                    var fresh = GuardAI.All.Where(g => g && g.name.StartsWith("Backup_")).ToList();
                    ShaftCheck($"{f} backup: two officers arrive", made == 2 && fresh.Count == 2, $"(made {made}, {fresh.Count} in the list)");
                    ShaftCheck($"{f} backup: inside the landing of one of the guards' doors (the elevator or a stairwell)", fresh.Count > 0 && fresh.All(g => LandingOf(gen, g.transform.position) >= 0));
                    ShaftCheck($"{f} backup: none of them in the outpost", fresh.All(g => !wide.Contains(g.transform.position.x, g.transform.position.z)));
                    ShaftCheck($"{f} backup: the door of every landing they stand in is open for them", fresh.All(g => { int l = LandingOf(gen, g.transform.position); return l >= 0 && ((gen.EntryDoorMask >> l) & 1) == 1; }));
                    yield return new WaitForSeconds(2.5f);
                    ShaftCheck($"{f} backup: they have walked out and are on their way", fresh.Count > 0 && fresh.All(g => g && LandingOf(gen, g.transform.position) < 0));
                    foreach (var g in fresh) if (g) Destroy(g.gameObject);
                    yield return null;
                }

                if (floor == 1)
                {
                    // (the patrol guards would spot a player standing at a door and call real backup into the count: switch them off for these checks)
                    foreach (var other in GuardAI.All.ToList()) if (other) other.enabled = false;
                    yield return new WaitForSeconds(7.5f);               // (every door is shut again)
                    ShaftCheck($"{f} every door is shut again after a call", gen.EntryDoorMask == 0, $"(mask {gen.EntryDoorMask})");

                    // ---- camping at the guards' elevator: the backup comes out of the stairwells instead
                    GoTo(gen.GuardShaftCentre + gen.GuardShaftOut * 4.5f);
                    yield return new WaitForSeconds(0.8f);
                    int m1 = gen.SpawnReinforcements(to, 2);
                    yield return null;
                    var camp = GuardAI.All.Where(g => g && g.name.StartsWith("Backup_")).ToList();
                    var landings = camp.Select(g => LandingOf(gen, g.transform.position)).ToList();
                    ShaftCheck("camping at the guards' elevator: two officers arrive, none of them out of the elevator, whose door stays shut",
                        m1 == 2 && camp.Count == 2 && landings.All(l => l >= 1) && !gen.GuestDoorOpen, $"(landings {string.Join(",", landings)})");
                    ShaftCheck("camping at the guards' elevator: they come out of two different stairwells", landings.Distinct().Count() == 2 && gen.EntryDoorMask == 6, $"(mask {gen.EntryDoorMask})");
                    foreach (var g in camp) if (g) Destroy(g.gameObject);
                    yield return new WaitForSeconds(7.5f);

                    // ---- camping at a stairwell: the backup comes out of the elevator and the other stairwell
                    GoTo(gen.StairCentre(0) + gen.StairOut(0) * 4.5f);
                    yield return new WaitForSeconds(0.8f);
                    int m2 = gen.SpawnReinforcements(to, 2);
                    yield return null;
                    var camp2 = GuardAI.All.Where(g => g && g.name.StartsWith("Backup_")).ToList();
                    var landings2 = camp2.Select(g => LandingOf(gen, g.transform.position)).ToList();
                    ShaftCheck("camping at the first stairwell: none of the officers comes out of it, and they use two different doors",
                        m2 == 2 && landings2.All(l => l == 0 || l == 2) && landings2.Distinct().Count() == 2 && !gen.StairDoorOpen(0), $"(landings {string.Join(",", landings2)})");
                    foreach (var g in camp2) if (g) Destroy(g.gameObject);
                    yield return new WaitForSeconds(7.5f);

                    // ---- far from every door: the doors are used in turn, never the same one twice running, and all three get their turn
                    GoTo(gen.CellCentre(Vector2Int.zero) + new Vector3(0f, 0f, 7f));
                    yield return new WaitForSeconds(0.8f);
                    var used = new List<int>();
                    for (int call = 0; call < 16; call++)
                    {
                        gen.SpawnReinforcements(to, 1);
                        yield return null;
                        var one = GuardAI.All.Where(g => g && g.name.StartsWith("Backup_")).ToList();
                        used.Add(one.Count == 1 ? LandingOf(gen, one[0].transform.position) : -2);
                        foreach (var g in one) if (g) Destroy(g.gameObject);
                        yield return null;
                    }
                    bool noRepeat = true; for (int i = 1; i < used.Count; i++) if (used[i] == used[i - 1]) noRepeat = false;
                    ShaftCheck("sixteen calls, one officer each, with the player far from every door: never the same door twice running", noRepeat && used.All(u => u >= 0), $"(doors {string.Join(",", used)})");
                    ShaftCheck("... and every one of the three doors was used", used.Distinct().Count() == 3, $"(doors {string.Join(",", used)})");
                    yield return new WaitForSeconds(7.5f);
                }
            }

            // ================= the guard on duty: blind and deaf outside his room, and a hit in time stops the call
            yield return LoadLevel(hm, 2);
            yield return LoadLevel(hm, 1);
            {
                var ctl = OutpostController.Active;
                var duty = DutyGuard();
                ShaftCheck("gate: floor 1 has an outpost with its guard", ctl != null && duty != null && ctl.Door != null);
                if (ctl == null || duty == null || ctl.Door == null) { Log($"[Shaft] DONE fails={shaftFails}"); Quit(1); yield break; }
                gen.TryGetOutpostRoom(out var room);
                var door = ctl.Door;
                var outward = ctl.Outward;
                var post = duty.transform.position;
                // (this block tests the guard on duty alone: the floor's patrol guards are blind and deaf here, because which of them can see down the corridor in front of the outpost door depends on the layout, and one of them calling for backup is no fault of his)
                foreach (var otherGuard in GuardAI.All) if (otherGuard && otherGuard != duty) otherGuard.noticeGate = blindPos => false;
                ShaftCheck("gate: it is the room's rectangle", duty.noticeGate != null && duty.noticeGate(post) && !duty.noticeGate(door.Centre + outward * 2f) && duty.noticeGate(door.Centre - outward * 1f));

                // ---- what he hears: the door is still shut
                var outPos = door.Centre + outward * 2f;
                var inPos = post + new Vector3(1.5f, 0f, 0f);
                NoiseBus.EmitLocal(outPos, ShaftFlat(post, outPos) + 25f);        // loud enough for any ordinary guard standing where he stands
                yield return new WaitForSeconds(0.3f);
                ShaftCheck("gate: a loud noise outside the room is not heard (he stays on his post)", duty.State == GuardState.Patrol, $"({duty.State})");
                NoiseBus.EmitLocal(inPos, 30f);
                yield return new WaitForSeconds(0.3f);
                ShaftCheck("gate: a noise inside the room is heard (he goes to look)", duty.State == GuardState.Investigate, $"({duty.State})");
                yield return new WaitForSeconds(7f);
                var fwd = duty.transform.forward; fwd.y = 0f;
                ShaftCheck("gate: afterwards he is back on his post, facing the window", duty.State == GuardState.Patrol && ShaftFlat(duty.transform.position, post) < 0.7f && Vector3.Dot(fwd.normalized, duty.postForward) > 0.9f,
                    $"({duty.State}, {ShaftFlat(duty.transform.position, post):0.00} m off, facing {Vector3.Dot(fwd.normalized, duty.postForward):0.00})");

                // ---- what he sees: the lock opens, the door slides open and stays
                ctl.HostHackResult(true);
                yield return new WaitForSeconds(2f);
                ShaftCheck("gate: the door is open", !door.IsShut);
                // (the owner, 2026-10-17 night: "The outpost guard should be able to leave the outpost": once the lock is open the gate is open, he sees and hears like any guard and may chase out of the room)
                var realGate = duty.noticeGate;
                System.Func<Vector3, bool> roomOnly = p => room.Contains(p.x, p.z);
                var cone = ConeOf(duty);
                if (cone != null)
                {
                    var wide = room.Grow(0.45f);
                    ShaftCheck("gate: once the lock is open the cone he shows on the floor reaches out through the open door (he can leave the outpost)", cone.Any(v => !wide.Contains(v.x, v.z)));
                }
                else Log("[Shaft] SKIP gate: the cone on the floor is not drawn in this run (no transparent shader)");
                duty.noticeGate = roomOnly;       // (the rest of this block tests the calling, the hits and the knock-out inside the room, with the room-only gate as before)

                var through = post + (door.Centre - post) * 1.6f;       // a spot out in the corridor, in line with the open door
                GoTo(through);
                yield return new WaitForSeconds(0.7f);
                duty.noticeGate = null;
                bool seenUngated = GuardSees(duty, player);
                duty.noticeGate = realGate;
                bool seenOpenGate = GuardSees(duty, player);
                duty.noticeGate = roomOnly;
                ShaftCheck("gate: out in the corridor, in line with the open door, an ordinary guard would see the player", seenUngated);
                ShaftCheck("gate: with the door unlocked the guard on duty sees him too (he may leave the outpost)", seenOpenGate);
                bool seenGated = GuardSees(duty, player);
                ShaftCheck("gate: (the room-only gate used for the rest of this block does not see him out there)", !seenGated);
                yield return new WaitForSeconds(1.2f);
                ShaftCheck("gate: and he does not start to call for backup", duty.State == GuardState.Patrol && !duty.IsCalling && !hm.BackupCalled, $"({duty.State})");

                // ---- one step inside: he sees, stops and starts to call
                GoTo(door.Centre - outward * 1.0f);
                for (float waited = 0f; waited < 4f && !duty.IsCalling; waited += Time.deltaTime) yield return null;       // (spotting takes a moment, longer the further away: GuardRules.SpotSeconds)
                ShaftCheck("gate: one step inside the room he sees the player", GuardSees(duty, player));
                ShaftCheck("gate: he stops and starts to call for backup", duty.State == GuardState.Calling && duty.IsCalling && !hm.BackupCalled, $"({duty.State})");

                // ---- stepping out again drops the call
                GoTo(through);
                yield return new WaitForSeconds(0.7f);
                ShaftCheck("gate: the player steps back out: the call is dropped, no backup", !duty.IsCalling && !hm.BackupCalled && duty.State != GuardState.Calling, $"({duty.State})");
                // (he walks to where he last saw the player, looks round, and goes back to his post: wait for that before the next visit)
                float backAtPost = 0f;
                bool AtPost() { var f = duty.transform.forward; f.y = 0f; return duty.State == GuardState.Patrol && ShaftFlat(duty.transform.position, post) < 0.7f && Vector3.Dot(f.normalized, duty.postForward) > 0.9f; }
                while (backAtPost < 16f && !AtPost()) { backAtPost += Time.deltaTime; yield return null; }
                ShaftCheck("gate: he goes back to his post by himself and faces the window again", AtPost(), $"(after {backAtPost:0.0} s, {duty.State})");

                // ---- in again, and a hit in time cuts the call; ten put him down for good, with no backup ever called
                GoTo(door.Centre - outward * 1.0f);
                for (float waited = 0f; waited < 4f && !duty.IsCalling; waited += Time.deltaTime) yield return null;
                ShaftCheck("knock-out: in the room again he calls again", duty.IsCalling, $"({duty.State})");
                duty.WeaponHit(1f, player.gameObject, 1);
                ShaftCheck("knock-out: one hit cuts the call", !duty.IsCalling && duty.State == GuardState.Stunned, $"({duty.State})");
                for (int i = 0; i < 9; i++) duty.WeaponHit(0.2f, player.gameObject, 1);
                ShaftCheck("knock-out: ten hits put him down for good", duty.Dead);
                yield return new WaitForSeconds(3f);
                ShaftCheck("knock-out: backup was never called", !hm.BackupCalled && !hm.BackupActive);
            }

            // ================= left alone, he calls: the backup arrives in the guards' elevator
            yield return LoadLevel(hm, 2);
            yield return LoadLevel(hm, 1);
            {
                var ctl = OutpostController.Active;
                var duty = DutyGuard();
                ShaftCheck("alert: a fresh floor 1 has its guard on duty again", ctl != null && duty != null && !duty.Dead);
                if (ctl == null || duty == null) { Log($"[Shaft] DONE fails={shaftFails}"); Quit(1); yield break; }
                gen.TryGetOutpostRoom(out var room);
                ctl.HostHackResult(true);
                yield return new WaitForSeconds(2f);
                GoTo(ctl.Door.Centre - ctl.Outward * 1.0f);
                float waited = 0f;
                while (!hm.BackupCalled && waited < duty.callSeconds + 3f) { waited += Time.deltaTime; yield return null; }
                ShaftCheck("alert: left alone he calls for backup, after watching for about his two seconds", hm.BackupCalled && hm.BackupActive && waited > duty.callSeconds - 0.6f, $"(after {waited:0.0} s)");
                var fresh = GuardAI.All.Where(g => g && g.name.StartsWith("Backup_")).ToList();
                ShaftCheck("alert: two officers appear, inside the landing of one of the guards' doors", fresh.Count == 2 && fresh.All(g => LandingOf(gen, g.transform.position) >= 0), $"({fresh.Count})");
                ShaftCheck("alert: none of them in the outpost", fresh.All(g => !room.Grow(0.4f).Contains(g.transform.position.x, g.transform.position.z)));
                ShaftCheck("alert: a door is open for them", gen.EntryDoorMask != 0);
                yield return new WaitForSeconds(1f);
            }

            // ================= pictures of the doors the backup comes out of (only where there are graphics)
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) yield return ShaftPictures(gen);
            else Log("[Shaft] SKIP pictures of the stairwells and the guards' elevator: no graphics in this run");

            // ================= the boss floor keeps the car, without an outpost; the docks have none
            yield return LoadLevel(hm, TowerRules.BossFloor);
            ShaftCheck("boss floor: the guards' elevator is there, no outpost, no stairwells and no guard on duty", gen.HasGuardShaft && gen.StairCount == 0 && OutpostController.Active == null && DutyGuard() == null && gen.Current.Find("GuestElevator") != null);
            yield return LoadLevel(hm, 0);
            ShaftCheck("basement: no guards' elevator and no stairwells", !gen.HasGuardShaft && gen.StairCount == 0);

            Log($"[Shaft] DONE fails={shaftFails}");
            Quit(shaftFails == 0 ? 0 : 1);
        }

        /// <summary>
        /// Pictures (%TEMP%\be_shot_*.png) of each stairwell and of the guards' elevator, shut and open, seen from the corridor:
        /// to look at the doors, the signs and what stands round them.
        /// </summary>
        IEnumerator ShaftPictures(FloorGenerator gen)
        {
            var cam = new GameObject("ShaftCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.22f, 0.26f); cam.fieldOfView = 62f; cam.enabled = false;
            var doors = new System.Collections.Generic.List<(string name, Vector3 centre, Vector3 outDir, byte mask)>();
            if (gen.HasGuardShaft) doors.Add(("guest", gen.GuardShaftCentre, gen.GuardShaftOut, 1));
            for (int i = 0; i < gen.StairCount; i++) doors.Add(($"stairs{i}", gen.StairCentre(i), gen.StairOut(i), (byte)(2 << i)));
            foreach (var d in doors)
            {
                var lookAt = d.centre + d.outDir * 2.3f + Vector3.up * 1.3f;
                cam.transform.position = d.centre + d.outDir * 7.0f + Vector3.up * 1.7f;
                cam.transform.LookAt(lookAt);
                yield return null;
                ShotCam(cam, $"{d.name}_closed");
                var side = Vector3.Cross(Vector3.up, d.outDir);
                cam.transform.position = d.centre + d.outDir * 4.6f + side * 1.6f + Vector3.up * 1.7f;
                cam.transform.LookAt(lookAt);
                yield return null;
                ShotCam(cam, $"{d.name}_near");
                gen.NetEntryDoors(d.mask);
                yield return new WaitForSeconds(1.6f);
                cam.transform.position = d.centre + d.outDir * 7.0f + Vector3.up * 1.7f;
                cam.transform.LookAt(lookAt);
                yield return null;
                ShotCam(cam, $"{d.name}_open");
                gen.NetEntryDoors(0);
                yield return new WaitForSeconds(1.6f);
            }
            Destroy(cam.gameObject);
        }

        // ------------------------------------------------------------------ two processes: the client builds the same floor and sees the same backup

        int shaftNetFails;

        void ShaftNetCheck(string name, bool ok, string detail = "")
        {
            if (!ok) shaftNetFails++;
            Log($"[ShaftNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator ShaftHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            Signal("h_ready0");
            yield return WaitSig("c_ready0", 150);

            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return FloorReady(1, 120);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(3f);

            var duty = DutyGuard();
            bool inRoom = gen.TryGetOutpostRoom(out var room) && duty != null && room.Contains(duty.transform.position.x, duty.transform.position.z);
            ShaftNetCheck("host: the guard on duty stands in the outpost and the guards' elevator is built", inRoom && gen.HasGuardShaft && duty.stationary);
            var sp = gen.GuardShaftCentre;
            int total = GuardAI.All.Count(g => g);
            var cult = System.Globalization.CultureInfo.InvariantCulture;
            Signal("h_floor1", string.Format(cult, "{0} {1:0.###} {2:0.###} {3:0.###} {4:0.###} {5}", duty != null ? duty.NetId : -1, duty != null ? duty.transform.position.x : 0f, duty != null ? duty.transform.position.z : 0f, sp.x, sp.z, total));
            yield return WaitSig("c_sees", 120);

            // backup: it comes out of the guards' elevator (on both machines)
            hm.CallBackup(duty);
            yield return null;
            var fresh = GuardAI.All.Where(g => g && !g.IsPuppet && g.name.StartsWith("Backup_")).ToList();
            ShaftNetCheck("host: backup brings two officers, inside the landing of one of the guards' doors, with a door open", fresh.Count == 2 && fresh.All(g => LandingOf(gen, g.transform.position) >= 0) && gen.EntryDoorMask != 0, $"({fresh.Count}, mask {gen.EntryDoorMask})");
            Signal("h_backup", string.Join(" ", fresh.Select(g => g.NetId)) + "|" + gen.EntryDoorMask);
            yield return WaitSig("c_done", 90);
            Log($"[ShaftNet] host DONE fails={shaftNetFails}");
            Log("DONE shaftnethost");
            Quit(shaftNetFails == 0 ? 0 : 1);
        }

        IEnumerator ShaftClientScenario()
        {
            var net = NetSession.Instance;
            float deadline = Time.realtimeSinceStartup + 150f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.JoinLocal("127.0.0.1");
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return new WaitForSeconds(2f);
            }
            yield return FloorReady(0, 120);
            yield return WaitSig("h_ready0", 90);
            RunSync.SyncChecked = false;
            Signal("c_ready0");
            yield return WaitSig("h_floor1", 150);
            var hm = HeistManager.Instance;
            var gen = FindFirstObjectByType<FloorGenerator>();
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return FloorReady(1, 120);
            yield return WaitFor(() => RunSync.SyncChecked, 60, "floor checked against the host");
            yield return new WaitForSeconds(3f);

            var cult = System.Globalization.CultureInfo.InvariantCulture;
            var p = ReadSig("h_floor1").Trim().Split(' ');
            int id = int.Parse(p[0]), total = int.Parse(p[5]);
            var hostGuard = new Vector3(float.Parse(p[1], cult), 0f, float.Parse(p[2], cult));
            var hostShaft = new Vector3(float.Parse(p[3], cult), 0f, float.Parse(p[4], cult));
            var puppet = NetIds.Get<GuardAI>(id);
            ShaftNetCheck("client: the guard on duty is here with the host's id, a puppet standing at his post", puppet != null && puppet.IsPuppet && ShaftFlat(puppet.transform.position, hostGuard) < 0.6f,
                puppet != null ? $"({ShaftFlat(puppet.transform.position, hostGuard):0.00} m from the host's)" : "(missing)");
            ShaftNetCheck("client: the guards' elevator stands where the host's does", gen.HasGuardShaft && ShaftFlat(gen.GuardShaftCentre, hostShaft) < 0.05f);
            ShaftNetCheck("client: and so do the two stairwells", gen.StairCount == 2 && ShaftFlat(gen.StairCentre(0), gen.CellCentre(new Vector2Int(0, TowerManager.Instance.CurrentInfo.rows - 1))) < 0.05f
                && ShaftFlat(gen.StairCentre(1), gen.CellCentre(new Vector2Int(TowerManager.Instance.CurrentInfo.cols - 1, 0))) < 0.05f);
            ShaftNetCheck("client: the same number of guards", GuardAI.All.Count(g => g) == total, $"({GuardAI.All.Count(g => g)} vs {total})");
            Signal("c_sees");

            yield return WaitSig("h_backup", 90);
            var parts2 = ReadSig("h_backup").Trim().Split('|');
            var ids = parts2[0].Trim().Split(' ').Select(int.Parse).ToList();
            int hostMask = int.Parse(parts2[1]);
            yield return new WaitForSeconds(0.8f);
            ShaftNetCheck("client: the two backup officers are here, and the same doors are open as on the host", ids.Count == 2 && ids.All(i => NetIds.Get<GuardAI>(i) != null) && gen.EntryDoorMask == hostMask && hostMask != 0,
                $"({ids.Count(i => NetIds.Get<GuardAI>(i) != null)} of {ids.Count}, doors {gen.EntryDoorMask} vs {hostMask})");
            Signal("c_done");
            Log($"[ShaftNet] client DONE fails={shaftNetFails}");
            Log("DONE shaftnetclient");
            Quit(shaftNetFails == 0 ? 0 : 1);
        }
    }
}
