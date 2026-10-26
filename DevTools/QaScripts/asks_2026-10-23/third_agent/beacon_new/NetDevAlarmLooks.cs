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
        public static void AlarmRulesTest()
        {
            // ---- the beacons: the corners of the hallways (a long one also along its walls) and two opposite corners of every big room
            var hall = new Rc(0f, 0f, 30f, 3f);
            var shortHall = new Rc(0f, 10f, 3f, 12f);
            var big = new Rc(40f, 0f, 52f, 10f);
            var small = new Rc(60f, 0f, 63f, 4f);
            var plan = BeaconRules.Plan(new List<Rc> { hall, shortHall }, new List<Rc> { big, small });
            int inHall = plan.Count(b => b.hall), inRooms = plan.Count(b => !b.hall);
            Check("beacons: a 30 m hallway gets two corners and one beacon along its wall, a 3 m hallway and a 12 m2 room none, a 120 m2 room two opposite corners", plan.Count == 5 && inHall == 3 && inRooms == 2, $"({plan.Count}: {inHall} in hallways, {inRooms} in rooms)");
            bool inside = true, facing = true;
            foreach (var b in plan)
            {
                var r = b.hall ? hall : big;
                inside &= b.x >= r.x0 && b.x <= r.x1 && b.z >= r.z0 && b.z <= r.z1;
                float dx = r.Cx - b.x, dz = r.Cz - b.z, len = Mathf.Sqrt(dx * dx + dz * dz);
                float fx = Mathf.Sin(b.yaw * Mathf.Deg2Rad), fz = Mathf.Cos(b.yaw * Mathf.Deg2Rad);
                if (len > 0.5f) facing &= (fx * dx + fz * dz) / len > 0.97f;
            }
            Check("beacons: every beacon is inside its room, and its dome faces the middle of the room", inside && facing, $"(inside {inside}, facing {facing})");
            var corner = plan[0];
            Check("beacons: a corner beacon hangs 0.32 m from both walls", Mathf.Abs(corner.x - BeaconRules.CornerInset) < 1e-4f && Mathf.Abs(corner.z - BeaconRules.CornerInset) < 1e-4f);
            var many = new List<Rc>(); for (int i = 0; i < 40; i++) many.Add(new Rc(0f, i * 5f, 30f, i * 5f + 3f));
            var thin = BeaconRules.Plan(many, null);
            Check("beacons: a floor with a lot of hallways is thinned to at most 60 beacons and an empty floor has none", thin.Count <= BeaconRules.MaxBeacons && thin.Count >= 30 && BeaconRules.Plan(null, null).Count == 0, $"({thin.Count})");
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
            AlarmCheck("floor 1 has alarm beacons in its corners", bc != null && bc.Count >= 6 && bc.Count <= BeaconRules.MaxBeacons, $"({(bc != null ? bc.Count : -1)})");
            if (bc == null) { Log($"[AlarmLooks] DONE fails={alarmFails}"); Quit(1); yield break; }
            AlarmCheck("they are the pack's small roof lamp (Resources/AlarmBeacon), with nothing solid on them", bc.FromPack && !bc.HasColliders);
            bool high = true;
            for (int i = 0; i < bc.Count; i++) high &= bc.PositionOf(i).y > ceilingY - 0.6f && bc.PositionOf(i).y < ceilingY;
            AlarmCheck("all of them hang just under the ceiling", high);
            AlarmCheck("quiet: the beacons are dark and cast no light, nobody is marked, the intense music is off", bc.Level == 0f && bc.LiveLights == 0 && !hm.BigAlarmActive && (MusicDirector.Instance == null || MusicDirector.Instance.Current != MusicDirector.Mood.Intense));

            // look at beacon 0 from inside its room: the player stands 6 m in front of its dome, turned towards it
            var at = bc.PositionOf(0);
            float yaw0 = bc.BeamYaw(0);
            var facing = Quaternion.Euler(0f, yaw0, 0f) * Vector3.forward;
            player.TeleportTo(new Vector3(at.x + facing.x * 6f, 1.1f, at.z + facing.z * 6f), Quaternion.LookRotation(-facing));
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
                    cam.transform.position = new Vector3(at.x + facing.x * 4f, 1.9f, at.z + facing.z * 4f);
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

            // ---- a new floor starts quiet again
            yield return LoadLevel(hm, 2);
            yield return new WaitForSeconds(1.0f);
            gen = FindFirstObjectByType<FloorGenerator>();
            var bc2 = gen.Beacons;
            AlarmCheck("floor 2: new beacons, dark again, no Big Alarm", bc2 != null && bc2 != bc && bc2.Level == 0f && !hm.BigAlarmActive);

            Log($"[AlarmLooks] DONE fails={alarmFails}");
            Quit(alarmFails == 0 ? 0 : 1);
        }
    }
}
