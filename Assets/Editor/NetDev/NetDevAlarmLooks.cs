using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's asks of 2026-10-24 about the alarm: the hallway loudspeakers are round discs flat against the ceiling and off the light panels ("they should just be attached to the ceiling like the attached image reference"), the Big Alarm turns on red spinning beacons in the
    /// corners near the ceiling (the small roof lamp of the lamp pack he added) and the existing intense music plays while the backup is on or the Big Alarm is. The pure rules are <c>NetDevTest.AlarmRulesTest</c> (PolicyTest); the Play half is the scenario <c>AlarmLooks</c> (<c>qa_regress_bd.ps1 -Methods AlarmLooks</c>;
    /// pictures need a real window: <c>ui.ps1 -Project C:\bd -Methods AlarmLooks</c>, %TEMP%\be_ui_alarm_*.png).
    /// </summary>
    public static partial class NetDevTest
    {
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
        {
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
                    if (Mathf.Abs(b.z) < 1e-3f && b.x > 13.11f && b.x < 16.89f) clearOfDoor = false;
            Check("beacons: none is hung at a doorway (the wall's face ends there, and a beacon keeps 0.9 m from its ends)", clearOfDoor);
            var shell3 = new List<Rc>(); var halls3 = new List<Rc>();
            for (int i = 0; i < 40; i++) { var h = new Rc(0f, i * 20f, 30f, i * 20f + 3f); halls3.Add(h); BeaconTestShell(h, shell3); }
            var thin = BeaconTestPlan(shell3, halls3, new List<Rc>(), 3);
            Check("beacons: a floor with a lot of hallways has at most 60 beacons and an empty floor has none", thin.Count == BeaconRules.MaxBeacons && BeaconRules.Plan(null, 1, null, null).Count == 0, $"({thin.Count})");
            float hmin = 1f, hmax = 0f; bool hashSteady = true;
            for (int i = 0; i < 2000; i++) { float v = BeaconRules.Hash01(i, i * 3, 5, 9); hmin = Mathf.Min(hmin, v); hmax = Mathf.Max(hmax, v); hashSteady &= v == BeaconRules.Hash01(i, i * 3, 5, 9); }
            Check("beacons: the random numbers of the plan are in [0, 1), spread over it, and the same every time", hmin >= 0f && hmax < 1f && hmin < 0.05f && hmax > 0.95f && hashSteady, $"({hmin:0.000} to {hmax:0.000})");
            Check("beacons: the lamp hangs 0.30 m under the ceiling, its ring's back 2 cm off the wall", Mathf.Abs(BeaconRules.Drop - 0.30f) < 1e-5f && Mathf.Abs(BeaconRules.Standoff - 0.02f) < 1e-5f);
            Check("beacons: the beam turns 150 degrees a second, every beacon from its own start, and the glow swells towards the beam (1 in line with it, 0.35 beside it)",
                Mathf.Abs(BeaconRules.Spin(1f, 0) - 150f) < 1e-3f && Mathf.Abs(BeaconRules.Spin(2.4f, 0)) < 1e-2f && Mathf.Abs(BeaconRules.Spin(0f, 1) - 47f) < 1e-3f && BeaconRules.Spin(100f, 7) >= 0f && BeaconRules.Spin(100f, 7) < 360f
                && Mathf.Abs(BeaconRules.Glow(0f) - 1f) < 1e-4f && Mathf.Abs(BeaconRules.Glow(90f) - 0.35f) < 1e-3f && Mathf.Abs(BeaconRules.Glow(180f) - 0.35f) < 1e-4f);

            // ---- the loudspeakers: a spot in the hallway that no light panel covers
            var corridor = new Rc(0f, 0f, 30f, 3f);
            var centre = new Vector2(15f, 1.5f);
            var none = PaRules.MountSpot(centre, corridor, new List<Rc>());
            var onLight = new Rc(14.4f, 1.2f, 15.6f, 1.8f);
            var moved = PaRules.MountSpot(centre, corridor, new List<Rc> { onLight });
            float rr = PaRules.DiscDiameter * 0.5f;
            var disc = new Rc(moved.x - rr, moved.y - rr, moved.x + rr, moved.y + rr);
            Check("loudspeakers: with no light panel in the way the speaker keeps the middle of the hallway; with a panel there it moves, to a spot the panel does not cover, inside the hallway, and not far",
                Vector2.Distance(none, centre) < 1e-4f && !disc.Overlaps(onLight) && moved.x >= corridor.x0 + 0.3f && moved.x <= corridor.x1 - 0.3f && moved.y >= corridor.z0 + 0.3f && moved.y <= corridor.z1 - 0.3f && Vector2.Distance(moved, centre) < 2.5f, $"({moved})");
            var blanket = new List<Rc> { new Rc(-1f, -1f, 31f, 4f) };
            Check("loudspeakers: a hallway covered with panels from end to end keeps the speaker where it was (nowhere is better)", Vector2.Distance(PaRules.MountSpot(centre, corridor, blanket), centre) < 1e-4f);
            Check("loudspeakers: the speaker is a round disc 0.4 m across and 5 cm thick against the ceiling, with a darker grille 0.3 m across under it", Mathf.Abs(PaRules.DiscDiameter - 0.4f) < 1e-5f && Mathf.Abs(PaRules.DiscHeight - 0.05f) < 1e-5f && PaRules.GrilleDiameter < PaRules.DiscDiameter && PaRules.CeilingGap <= 0.05f);

            // ---- the Esc menu's LEAVE RUN button: in a session and in a solo run, not on the menu scene (the owner, 2026-10-24)
            Check("esc menu: LEAVE RUN is there in a network session and in a solo run, and not on the menu scene", EscapeMenu.ShowsLeaveRun(true, false) && EscapeMenu.ShowsLeaveRun(false, false) && EscapeMenu.ShowsLeaveRun(true, true) && !EscapeMenu.ShowsLeaveRun(false, true));

            // ---- the music: the existing intense loop plays while the backup is on or the Big Alarm is, on a floor that is live and not being left
            Check("music: the intense music is wanted on a live floor while the backup is called or on, and while the Big Alarm is on; not while the elevator seals, not on a floor that is not live, not when all is quiet",
                MusicDirector.IntenseWanted(true, false, true, false, false) && MusicDirector.IntenseWanted(true, false, false, true, false) && MusicDirector.IntenseWanted(true, false, false, false, true)
                && !MusicDirector.IntenseWanted(true, true, true, true, true) && !MusicDirector.IntenseWanted(false, false, true, true, true) && !MusicDirector.IntenseWanted(true, false, false, false, false));
        }
    }

    public partial class NetDevRunner
    {
        int alarmFails;

        void AlarmCheck(string name, bool ok, string detail = "")
        {
            if (!ok) alarmFails++;
            Log($"[AlarmLooks] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        bool CanPicture => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null && !Application.isBatchMode;

        IEnumerator AlarmSnap(string name)
        {
            if (CanPicture) yield return Snap("alarm_" + name);
        }

        IEnumerator AlarmLooksScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            HeistManager.TestPartySize = 0;
            yield return DockPosterChecks();                                                                // (the basement's break room: its two posters, the owner, 2026-10-24)
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(1.5f);
            var gen = FindFirstObjectByType<FloorGenerator>();
            var player = WobblyPlayerRegistry.All[0];
            var bc = gen.Beacons;

            // ---- the loudspeakers: flat against the ceiling, off the light panels
            var speakers = FindObjectsByType<LoudSpeaker>(FindObjectsSortMode.None);
            float ceilingY = gen.CeilingUnderside;
            int flush = 0, covered = 0, round = 0;
            var frames = new List<Rc>();
            if (gen.PanelPlan != null) foreach (var p in gen.PanelPlan.panels) frames.Add(AfterHours.FrameRect(p));
            foreach (var sp in speakers)
            {
                var disc = sp.transform.Find("Disc");
                var rend = disc ? disc.GetComponent<Renderer>() : null;
                if (!rend) continue;
                var b = rend.bounds;
                if (b.max.y >= ceilingY - 0.08f && b.max.y <= ceilingY) flush++;
                if (Mathf.Abs(b.size.x - PaRules.DiscDiameter) < 0.02f && Mathf.Abs(b.size.z - PaRules.DiscDiameter) < 0.02f && b.size.y < 0.1f) round++;
                var r = new Rc(b.min.x, b.min.z, b.max.x, b.max.z);
                foreach (var f in frames) if (r.Overlaps(f)) { covered++; break; }
            }
            AlarmCheck("floor 1 has hallway loudspeakers", speakers.Length >= 3, $"({speakers.Length})");
            AlarmCheck("every loudspeaker is a round disc flat against the ceiling (its top within 8 cm of it)", speakers.Length > 0 && flush == speakers.Length && round == speakers.Length, $"({flush} flat, {round} round of {speakers.Length}; ceiling {ceilingY:0.00})");
            AlarmCheck("no loudspeaker sits on a light panel", frames.Count > 0 && covered == 0, $"({covered} of {speakers.Length} on a panel, {frames.Count} panels)");

            // ---- the beacons: planned, from the pack's lamp, dark and without any light until the Big Alarm
            AlarmCheck("floor 1 has alarm beacons on its walls", bc != null && bc.Count >= 6 && bc.Count <= BeaconRules.MaxBeacons, $"({(bc != null ? bc.Count : -1)})");
            if (bc == null) { Log($"[AlarmLooks] DONE fails={alarmFails}"); Quit(1); yield break; }
            AlarmCheck("they are the pack's small roof lamp (Resources/AlarmBeacon), with nothing solid on them", bc.FromPack && !bc.HasColliders);
            bool high = true;
            for (int i = 0; i < bc.Count; i++) high &= bc.PositionOf(i).y > ceilingY - 0.6f && bc.PositionOf(i).y < ceilingY;
            AlarmCheck("all of them hang just under the ceiling", high);
            AlarmCheck("every beacon is mounted on a wall: a ray straight back from the lamp hits the wall's face 2.7 cm behind its middle", bc.TouchingWall() == bc.Count, $"({bc.TouchingWall()} of {bc.Count})");
            float apart = float.MaxValue;
            for (int i = 0; i < bc.Count; i++) for (int j = i + 1; j < bc.Count; j++) { var d = bc.WallPointOf(i) - bc.WallPointOf(j); d.y = 0f; apart = Mathf.Min(apart, d.magnitude); }
            AlarmCheck("they are scattered: no two are closer than 9 m", apart >= BeaconRules.MinGap - 0.05f, $"(closest {apart:0.0} m)");
            AlarmCheck("quiet: the beacons are dark and cast no light, nobody is marked, the intense music is off", bc.Level == 0f && bc.LiveLights == 0 && !hm.BigAlarmActive && (MusicDirector.Instance == null || MusicDirector.Instance.Current != MusicDirector.Mood.Intense));

            // look at beacon 0 from inside its room: the player stands 6 m in front of its dome, turned towards it
            // the beacon with the most room in front of it, so that the picture shows the lamp on its wall and the room it looks into
            int pick = 0; float room = 0f;
            for (int i = 0; i < bc.Count; i++)
            {
                var fwdI = Quaternion.Euler(0f, bc.BeamYaw(i), 0f) * Vector3.forward;
                float free = Physics.Raycast(new Vector3(bc.PositionOf(i).x, 1.4f, bc.PositionOf(i).z), fwdI, out var hitI, 7f, ~0, QueryTriggerInteraction.Ignore) ? hitI.distance : 7f;
                if (free > room) { room = free; pick = i; }
            }
            var at = bc.PositionOf(pick);
            float yaw0 = bc.BeamYaw(pick);
            var facing = Quaternion.Euler(0f, yaw0, 0f) * Vector3.forward;
            float stand = Mathf.Max(1.5f, Mathf.Min(6f, room - 0.8f));
            player.TeleportTo(new Vector3(at.x + facing.x * stand, 1.1f, at.z + facing.z * stand), Quaternion.LookRotation(-facing));
            yield return new WaitForSeconds(1.0f);
            yield return AlarmSnap("0_quiet");

            // ---- the Big Alarm (a solo player's first sighting): the beacons turn red and spin, the intense music plays
            bool bigNow = hm.MarkSpotted(player);
            yield return new WaitForSeconds(1.3f);
            AlarmCheck("the Big Alarm goes on", bigNow && hm.BigAlarmActive);
            AlarmCheck("the beacons are fully lit, and up to 5 of them (the nearest) cast red light", bc.Level >= 0.99f && bc.LiveLights >= 1 && bc.LiveLights <= BeaconRules.MaxLive, $"(level {bc.Level:0.00}, {bc.LiveLights} lights)");
            AlarmCheck("the intense music is chosen while the Big Alarm goes", MusicDirector.Instance != null && MusicDirector.Instance.Current == MusicDirector.Mood.Intense, $"(mood {(MusicDirector.Instance != null ? MusicDirector.Instance.Current.ToString() : "no director")})");
            float y1 = bc.BeamYaw(0);
            yield return AlarmSnap("1_alarm_a");
            yield return new WaitForSeconds(0.4f);
            float y2 = bc.BeamYaw(0);
            yield return AlarmSnap("2_alarm_b");
            yield return new WaitForSeconds(0.5f);
            yield return AlarmSnap("3_alarm_c");
            AlarmCheck("the beam turns: in 0.4 s it moved about 60 degrees", Mathf.Abs(Mathf.DeltaAngle(y1, y2)) > 30f, $"({y1:0} -> {y2:0})");
            if (CanPicture)
            {
                // pictures from a camera of their own: the lamp from 4 m in front of it, and the room as the lamp sees it, three moments apart (be_shot_alarm_*.png)
                var cam = new GameObject("AlarmCam").AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox; cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f; cam.enabled = false;
                foreach (var tag in new[] { "a", "b", "c" })
                {
                    cam.fieldOfView = 40f;
                    float camBack = Mathf.Max(1.5f, Mathf.Min(4f, room - 0.8f));
                    cam.transform.position = new Vector3(at.x + facing.x * camBack, 1.9f, at.z + facing.z * camBack);
                    cam.transform.LookAt(at);
                    yield return null;
                    ShotCam(cam, "alarm_beacon_" + tag);
                    cam.fieldOfView = 75f;
                    cam.transform.position = at + facing * 0.9f + Vector3.down * 0.5f;
                    cam.transform.rotation = Quaternion.LookRotation(new Vector3(facing.x, -0.15f, facing.z));
                    yield return null;
                    ShotCam(cam, "alarm_room_" + tag);
                    yield return new WaitForSeconds(0.45f);
                }
                Destroy(cam.gameObject);
            }

            // ---- the Esc menu in a solo run has a LEAVE RUN button (a picture), and it takes the player back to the menu scene (the owner, 2026-10-24: "in solo mode there is no leave run button")
            AlarmCheck("(solo run) the Esc menu shows LEAVE RUN on a floor", !Net.NetSession.Online && EscapeMenu.ShowsLeaveRun(Net.NetSession.Online, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene));
            EscapeMenu.OpenForTests(false);
            yield return new WaitForSeconds(0.5f);
            yield return AlarmSnap("4_esc_solo");
            EscapeMenu.CloseAll();

            // ---- a new floor starts quiet again
            yield return LoadLevel(hm, 2);
            yield return new WaitForSeconds(1.0f);
            gen = FindFirstObjectByType<FloorGenerator>();
            var bc2 = gen.Beacons;
            AlarmCheck("floor 2: new beacons, dark again, no Big Alarm", bc2 != null && bc2 != bc && bc2.Level == 0f && !hm.BigAlarmActive);

            // ---- the button itself: LEAVE RUN in a solo run goes back to the menu scene
            EscapeMenu.LeaveRun();
            yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene, 30, "the menu scene after LEAVE RUN");
            AlarmCheck("LEAVE RUN in a solo run takes the player back to the menu scene and closes the Esc menu", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene && !EscapeMenu.Open);

            Log($"[AlarmLooks] DONE fails={alarmFails}");
            Quit(alarmFails == 0 ? 0 : 1);
        }
    }
}
