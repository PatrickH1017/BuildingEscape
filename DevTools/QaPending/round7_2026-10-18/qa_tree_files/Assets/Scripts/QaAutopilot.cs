using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BuildingEscape.Net;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace BuildingEscape
{
    /// <summary>
    /// A way to test a BUILT player without a person: start the exe with <c>-be-autopilot</c> (and optionally <c>-be-out &lt;folder&gt;</c>) and it plays
    /// the offline loop by itself - the menu, a solo run, the docks, floors 1 to 4 (each one built, looked at from the elevator, the guards'
    /// elevator, both ends of the outpost and a piece of loot, then finished and left for the docks) and the docks with the demo over - taking a
    /// picture at every stop (and counting magenta pixels in it: an error shader, a stripped shader) and writing what it saw to the player log
    /// (lines starting "[Autopilot]") and to <c>autopilot_report.txt</c>, then quits. It measures frame times as it goes.
    /// Without the switch it is not even created, so a player never meets it; it is NOT a cheat (nothing is reachable from inside the game).
    /// Used for the Q/A of the demo build (DEVELOPER_NOTES 2.17 / 8.10). Uses only public game API.
    /// </summary>
    public sealed class QaAutopilot : MonoBehaviour
    {
        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        static bool Has(string name) => System.Environment.GetCommandLineArgs().Contains(name);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Has("-be-autopilot") || FindFirstObjectByType<QaAutopilot>()) return;
            var go = new GameObject("QaAutopilot");
            DontDestroyOnLoad(go);
            go.AddComponent<QaAutopilot>();
        }

        string outDir;
        readonly StringBuilder report = new StringBuilder();
        readonly List<string> errorLines = new List<string>();
        int errors, exceptions, checksFailed;
        bool finished;
        float startedAt;

        void Awake()
        {
            outDir = Arg("-be-out") ?? Path.Combine(Application.persistentDataPath, "autopilot");
            Directory.CreateDirectory(outDir);
            Application.logMessageReceived += OnLog;
            startedAt = Time.realtimeSinceStartup;
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }

        void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            if (type == LogType.Exception) exceptions++;
            if (errorLines.Count < 40) errorLines.Add($"{type}: {condition}");
        }

        void Say(string line)
        {
            Debug.Log("[Autopilot] " + line);
            report.AppendLine(line);
            try { File.WriteAllText(Path.Combine(outDir, "autopilot_report.txt"), report.ToString()); } catch { }
        }

        void Check(string name, bool ok, string detail = "")
        {
            if (!ok) checksFailed++;
            Say($"{(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        void Finish(int code)
        {
            if (finished) return;
            finished = true;
            Say($"finished after {Time.realtimeSinceStartup - startedAt:0} s: checks failed {checksFailed}, errors logged {errors} (exceptions {exceptions})");
            foreach (var l in errorLines) Say("logged: " + l);
            Application.Quit(code);
        }

        IEnumerator Until(System.Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (!cond()) { Say($"FAIL timeout ({timeout:0} s) waiting for {what}"); checksFailed++; Finish(4); while (true) yield return null; }
        }

        static IEnumerator Wait(float seconds) { float t = 0f; while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; } }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            var px = tex.GetPixels32();
            int pink = 0;
            foreach (var c in px) if (c.r > 200 && c.g < 70 && c.b > 200) pink++;
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Check($"picture {name}: no magenta pixels (an error or stripped shader)", pink < px.Length * 0.002f, $"({pink} of {px.Length} px)");
        }

        IEnumerator Measure(string label, float seconds)
        {
            var dts = new List<float>(512);
            int gc0 = System.GC.CollectionCount(0);
            float t = 0f;
            while (t < seconds) { yield return null; float dt = Time.unscaledDeltaTime; dts.Add(dt); t += dt; }
            dts.Sort();
            float avg = dts.Average(), p95 = dts[Mathf.Min(dts.Count - 1, (int)(dts.Count * 0.95f))], worst = dts[dts.Count - 1];
            Say($"perf {label}: {dts.Count} frames in {t:0.0} s = {dts.Count / t:0} fps, frame time avg {avg * 1000f:0.0} ms, 95th percentile {p95 * 1000f:0.0} ms, worst {worst * 1000f:0.0} ms; GC runs {System.GC.CollectionCount(0) - gc0}, managed memory {System.GC.GetTotalMemory(false) / 1048576f:0.0} MB");
        }

        /// <summary>The player and the camera go to a spot and look at a point; waits a moment so the room is drawn, then takes the picture.</summary>
        IEnumerator View(string name, Vector3 eye, Vector3 look)
        {
            var rig = FindFirstObjectByType<CameraFollow>();
            if (rig) rig.enabled = false;
            var player = WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0] : null;
            if (player)
            {
                var flat = new Vector3(eye.x, 0f, eye.z);
                var at = NavMesh.SamplePosition(flat, out var hit, 4f, NavMesh.AllAreas) ? hit.position : flat;
                var d = look - eye; d.y = 0f;
                player.TeleportTo(at + Vector3.up * 1.1f, d.sqrMagnitude > 0.01f ? Quaternion.LookRotation(d.normalized) : player.transform.rotation);
            }
            var cam = Camera.main;
            if (cam)
            {
                cam.transform.position = eye;
                cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized);
            }
            // the player's own body must not stand in front of the lens
            var hidden = new List<Renderer>();
            if (player) foreach (var r in player.GetComponentsInChildren<Renderer>()) if (r && r.enabled) { r.enabled = false; hidden.Add(r); }
            yield return Wait(0.6f);
            if (cam) { cam.transform.position = eye; cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized); }
            yield return Shot(name);
            foreach (var r in hidden) if (r) r.enabled = true;
        }

        IEnumerator Start()
        {
            Say($"start: game {Application.version}, Unity {Application.unityVersion}, {SystemInfo.operatingSystem}, GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsMemorySize} MB), {Screen.width}x{Screen.height}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}, vSync {QualitySettings.vSyncCount}, runInBackground {Application.runInBackground}, shaders: Standard {(Shader.Find("Standard") != null)}, Legacy Diffuse {(Shader.Find("Legacy Shaders/Diffuse") != null)}, elevator mirror {(Shader.Find("BuildingEscape/ElevatorMirror") != null)}");

            // ---- two built players over the loopback: -be-autopilot-net host | client
            string netRole = Arg("-be-autopilot-net");
            // (the run-start cinematic is skipped by every test except the ones that test it: introsolo | introhost | introclient)
            IntroCinematic.SkipForTests = netRole == null || !netRole.StartsWith("intro");
            if (netRole == "host") { yield return RunHost(); yield break; }
            if (netRole == "client") { yield return RunClient(); yield break; }
            if (netRole == "introsolo") { yield return RunIntroSolo(); yield break; }
            if (netRole == "introoffline") { yield return RunIntroOffline(); yield break; }
            if (netRole == "introshots") { yield return RunIntroShots(); yield break; }
            if (netRole == "introhost") { yield return RunIntroHost(); yield break; }
            if (netRole == "introclient") { yield return RunIntroClient(); yield break; }

            // ---- the menu
            yield return Until(() => SceneManager.GetActiveScene().name == NetConst.MenuScene, 40f, "the menu scene");
            yield return Wait(3f);
            yield return Shot("01_menu");
            {
                bool dockHere = FindFirstObjectByType<LobbyDockLife>() != null;
                var music = MusicDirector.Instance;
                Check("the lobby has its loading dock behind the characters, and the lobby song is in this build and plays", dockHere && music != null && music.HasLobbySong && music.LobbyPlaying,
                    $"(dock {dockHere}, music director {music != null}, song loaded {music != null && music.HasLobbySong}, playing {music != null && music.LobbyPlaying})");
            }
            yield return Measure("menu", 3f);

            // ---- a solo run, the docks
            SceneManager.LoadScene(NetConst.RunScene);
            yield return Until(() => TowerManager.Instance != null && HeistManager.Instance != null && !TowerManager.Instance.Loading && HeistManager.Instance.Phase == HeistPhase.Hub, 120f, "the docks");
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            yield return Wait(3f);
            Check("a solo run starts in the docks, offline", hm.Floor == 0 && !NetSession.Online && WobblyPlayerRegistry.All.Count == 1, $"(players {WobblyPlayerRegistry.All.Count})");
            yield return Shot("02_docks_arrival");
            yield return Measure("docks", 4f);
            var vendor = Vendor.Current;
            if (vendor && gen) yield return View("03_docks_vendor", gen.DockTrailerEntrance + Vector3.up * 1.7f, gen.DockCounter + Vector3.up * 1.3f);
            var dummy = TrainingDummy.Current;
            if (dummy) yield return View("04_docks_dummy", dummy.transform.position + new Vector3(-3f, 1.7f, -3f), dummy.transform.position + Vector3.up * 1.0f);
            { var rig = FindFirstObjectByType<CameraFollow>(); if (rig) rig.enabled = true; }
            Check("the docks have a vendor and a training dummy", vendor != null && dummy != null);
            Check("before the demo is over the vendor has no pew for sale", !Vendor.PewForSale(tm.ResumeLevel));

            // ---- floors 1 to 4
            for (int floor = 1; floor <= 4; floor++)
            {
                tm.RideTo(floor);
                yield return Until(() => tm.Loading || hm.Floor == floor, 15f, $"the ride to floor {floor} to begin");
                yield return Until(() => !tm.Loading && hm.Floor == floor && hm.Phase == HeistPhase.Heist, 150f, $"floor {floor}");
                hm.godMode = true; hm.freezeTimer = true;
                foreach (var g in GuardAI.All) if (g) { g.sightRange = 0.1f; g.hearingMultiplier = 0f; }     // (they are only to be looked at)
                yield return Wait(3f);
                var info = tm.CurrentInfo;
                Check($"floor {floor} is built", info.floor == floor && info.rooms > 4 && info.quota > 0, $"({info.cols}x{info.rows} cells, {info.rooms} rooms, quota {info.quota}, loot {info.lootValue}, {info.guards} guards)");
                yield return EtrFloorChecks(floor, gen, hm, tm);
                yield return Shot($"floor{floor}_0_elevator");
                yield return Measure($"floor {floor} in the elevator", 3f);

                if (gen.HasGuardShaft)
                    yield return View($"floor{floor}_1_guard_elevator", gen.GuardShaftCentre + gen.GuardShaftOut * 7f + Vector3.up * 1.7f, gen.GuardShaftCentre + gen.GuardShaftOut * 3f + Vector3.up * 1.2f);
                if (gen.TryGetOutpostRoom(out var room))
                {
                    var middle = new Vector3(room.Cx, 0.9f, room.Cz);
                    yield return View($"floor{floor}_2_outpost", new Vector3(room.x0 + 0.7f, 1.9f, room.z0 + 0.7f), middle);
                    yield return View($"floor{floor}_3_outpost_back", new Vector3(room.x1 - 0.7f, 1.9f, room.z1 - 0.7f), middle);
                }
                else Check($"floor {floor} has a guard outpost", false);
                if (floor == OutpostLoot.PewFloor)
                {
                    var pew = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).FirstOrDefault(g => g && g.isActiveAndEnabled);
                    if (pew) yield return View($"floor{floor}_5_pew", pew.transform.position + new Vector3(1.1f, 1.0f, 1.1f), pew.transform.position);
                }
                var loot = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => p && p.isActiveAndEnabled && p.Value > 0 && Vector3.Distance(p.transform.position, info.spawn) > 10f)
                    .OrderBy(p => Vector3.Distance(p.transform.position, info.spawn)).FirstOrDefault();
                if (loot) yield return View($"floor{floor}_4_loot", loot.transform.position + new Vector3(2f, 1.7f, 2f), loot.transform.position);
                yield return Measure($"floor {floor} with the guards about", 5f);
                { var rig = FindFirstObjectByType<CameraFollow>(); if (rig) rig.enabled = true; }

                if (floor == OutpostLoot.PewFloor)
                {
                    int pews = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).Count(g => g && g.isActiveAndEnabled);
                    Check($"a pew lies in floor {floor}'s outpost", pews == 1, $"({pews} pews on the floor)");
                }
                if (floor == ArmedGuards.DefaultFirstArmedFloor)
                    Check($"the guards of floor {floor} are armed", GuardAI.All.Count(g => g && g.Armed) >= 3, $"({GuardAI.All.Count(g => g && g.Armed)} armed)");

                hm.DebugCompleteFloor();
                yield return Wait(1.5f);
                Check($"floor {floor}: the quota is met", hm.Phase == HeistPhase.FloorComplete);
                yield return Shot($"floor{floor}_9_quota_met");

                tm.RideTo(0);
                yield return Until(() => tm.Loading || hm.Floor == 0, 15f, "the ride to the docks to begin");
                yield return Until(() => !tm.Loading && hm.Floor == 0 && hm.Phase == HeistPhase.Hub, 120f, "the docks again");
                hm.godMode = true; hm.freezeTimer = true;
                yield return Wait(2f);
                Check($"floor {floor} done: back in the docks, the elevator resumes at floor {floor + 1}", tm.ResumeLevel == floor + 1, $"(resume {tm.ResumeLevel})");
                bool pewNow = floor >= Vendor.PewAfterFloor;
                Check($"floor {floor} done: the vendor's pew is {(pewNow ? "for sale" : "not for sale yet")}", Vendor.PewForSale(tm.ResumeLevel) == pewNow);
                if (floor == 4) yield return Shot("docks_demo_complete");
            }

            Check("floor 4 done: the demo is over and the vendor has the pew", TowerRules.DemoOver(tm.ResumeLevel) && Vendor.PewForSale(tm.ResumeLevel));
            yield return Measure("docks, demo complete", 3f);
            Finish(checksFailed == 0 ? 0 : 3);
        }

        // ------------------------------------------------------------------ the 2026-10-17 changes, looked at in the built game

        static readonly string[] EtrFurniture = { "monitor", "printer", "copy machine", "water cooler", "keyboard", "crate", "briefcase", "microwave", "coffee machine", "projector", "desk phone", "notepad", "roll of tape", "croissant", "takeaway" };

        /// <summary>
        /// On every floor of the solo run: the main hallway doors have their windows, the security-office signs are on the walls, nothing that is furniture is loot and the valuables come
        /// in many kinds, the loot scanner marks the loot of the room the player stands in (and no other), and the way back to the basement is shut until the quota is met.
        /// </summary>
        IEnumerator EtrFloorChecks(int floor, FloorGenerator gen, HeistManager hm, TowerManager tm)
        {
            // the hallway doors: every wide door has its windows (2 leaves x 2 faces x (2 windows and a push bar) = 12 pictures), the rooms' doors have none
            var dm = FindFirstObjectByType<DoorManager>();
            int wide = 0, dressed = 0;
            if (dm != null)
                foreach (var d in dm.Doors)
                {
                    if (!d.Wide) continue;
                    wide++;
                    if (d.LowLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane") + d.HighLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane") == 12) dressed++;
                }
            Check($"floor {floor}: every main hallway door has its windows", dm != null && wide == dressed, $"({dressed} of {wide} wide doors)");
            var signs = gen.PlanSigns();
            Check($"floor {floor}: the security-office signs are on the walls and lead to the outpost", signs.Count(s => s.security) >= 3, $"({signs.Count(s => s.security)} of {signs.Count} signs)");

            // the loot: nothing that is furniture, many kinds
            var loot = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => p && p.isActiveAndEnabled && p.Value >= LootScanRules.MinValue && !p.IsHeld).ToList();
            var furniture = loot.Where(p => EtrFurniture.Any(w => p.Label.ToLowerInvariant().Contains(w) && !p.Label.ToLowerInvariant().Contains("golden"))).ToList();
            int kinds = loot.Select(p => p.Label).Distinct().Count(l => Valuables.All.Any(v => v.label == l));
            Check($"floor {floor}: no furniture is loot, the valuables come in {kinds} kinds", furniture.Count == 0 && kinds >= 9, $"({loot.Count} things worth money; furniture: {string.Join(", ", furniture.Select(p => p.Label).Distinct().Take(4))})");

            // the scanner: in a room with several things, hold the key
            var scanner = LootScanner.Instance;
            var byRoom = loot.GroupBy(p => gen.RoomSpotAt(p.transform.position).index).Where(g => g.Key >= 0 && g.Count() >= 2).OrderByDescending(g => g.Count()).FirstOrDefault();
            if (scanner != null && byRoom != null)
            {
                var spot = gen.RoomSpotAt(byRoom.First().transform.position);
                var player = WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0] : null;
                if (player) player.TeleportTo(new Vector3(spot.bounds.Cx, 1.1f, spot.bounds.Cz), Quaternion.identity);
                yield return Wait(0.8f);
                LootScanner.TestHold = true;
                yield return Wait(1.5f);
                var inRoom = loot.Where(p => gen.RoomSpotAt(p.transform.position).index == spot.index).ToList();
                bool all = inRoom.All(p => scanner.IsMarked(p)), onlyThose = scanner.MarkCount == inRoom.Count;
                Check($"floor {floor}: holding Z scans the room the player is in (its {inRoom.Count} valuables marked, nothing from the next room)", all && onlyThose && LootScanner.Scanning, $"({scanner.MarkCount} marks, '{scanner.StatusLine}')");
                if (floor == 1) yield return Shot("floor1_6_scan");
                LootScanner.TestHold = false;
                yield return Wait(0.3f);
            }
            else Check($"floor {floor}: there is a room with loot to scan", false);

            // the basement gate: the elevator panel will not take the team down before the quota is met
            var car = ElevatorExtraction.Current;
            if (car != null && !hm.godMode == false)
            {
                var pl = WobblyPlayerRegistry.All[0];
                pl.TeleportTo(car.panelPosition + Vector3.up * 0.2f, Quaternion.identity);
                yield return Wait(0.6f);
                car.HostPress(pl.transform, pl.GetComponent<Backpack>(), ElevatorExtraction.SelectBase + 0);
                yield return Wait(0.3f);
                Check($"floor {floor}: before the quota is met the panel will not choose the Loading Docks", ElevatorExtraction.Destination == -1 && hm.Phase == HeistPhase.Heist, $"(destination {ElevatorExtraction.Destination})");
                var spawn = tm.CurrentInfo.spawn;
                pl.TeleportTo(spawn + Vector3.up * 1.1f, Quaternion.identity);
                yield return Wait(0.3f);
            }
        }

        // ------------------------------------------------------------------ two built players, one machine (host and client over the loopback)

        bool FloorIs(int floor)
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            return hm != null && tm != null && !tm.Loading && hm.Floor == floor && hm.Phase != HeistPhase.Lobby;
        }

        /// <summary>
        /// Round 6 (the outpost door's code lock over the network), floor 1, run by the host and the client at the same moment. The host opens the lock (what a hack does); the client stands inside the room and locks the door
        /// with the terminal's code (<see cref="DoorManager.ComputerLock"/> with the name SECURITY: the request goes to the host, which checks that the player is inside); both machines must then show the door
        /// hard-locked and shut. After a pause the client unlocks it with the code: both must show it unlocked and open (the host opens it too, as a hack does, because the clients open it when the Unlocked bit rises).
        /// </summary>
        IEnumerator OutpostLockNet(bool host)
        {
            string who = host ? "host" : "client";
            var ctl = OutpostController.Active;
            Check($"{who}: floor 1 has its outpost with a door", ctl != null && ctl.Door != null);
            if (ctl == null || ctl.Door == null) yield break;
            if (host)
            {
                Check("host: the outpost door is locked for the hack before anybody touches it", ctl.Door.Locked && !ctl.Unlocked && !ctl.Door.ComputerLocked);
                yield return Wait(2f);
                ctl.HostHackResult(true);
                yield return Until(() => ctl.Unlocked && !ctl.Door.IsShut, 20f, "the lock to open on the host");
                Check("host: the lock is open and the door slid open", ctl.Unlocked && !ctl.Door.Locked && !ctl.Door.ComputerLocked);
                yield return Until(() => ctl.Door.ComputerLocked && !ctl.Unlocked, 90f, "the client's code lock to arrive");
                Check("host: the client's SECURITY LOCK arrived: hard-locked, the hack lock shut again, the door shut", ctl.Door.ComputerLocked && ctl.Door.Locked && !ctl.Unlocked && (ctl.Bits & 1) == 0);
                yield return Wait(1.5f);
                Check("host: ...and the door is shut", ctl.Door.IsShut);
                yield return Until(() => !ctl.Door.ComputerLocked && ctl.Unlocked, 90f, "the client's code unlock to arrive");
                yield return Wait(1.5f);
                Check("host: the client's UNLOCK arrived: unlocked, and the door is open", ctl.Unlocked && !ctl.Door.Locked && !ctl.Door.ComputerLocked && !ctl.Door.IsShut && (ctl.Bits & 1) != 0);
            }
            else
            {
                yield return Until(() => ctl.Unlocked && !ctl.Door.IsShut, 60f, "the host's lock to open here");
                Check("client: the host's open lock arrived: unlocked, the door open", ctl.Unlocked && !ctl.Door.Locked);
                var wob = NetPlayer.Local != null ? NetPlayer.Local.GetComponent<WobblyPlayer>() : null;
                var rb = wob != null ? wob.GetComponent<Rigidbody>() : null;
                Check("client: my own body is here", wob != null && rb != null);
                if (wob == null || rb == null) yield break;
                var inside = ctl.KeypadPosition - ctl.Outward * 2.2f;
                inside.y = 1.1f;
                wob.TeleportTo(inside, Quaternion.LookRotation(ctl.Outward));
                rb.linearVelocity = Vector3.zero;
                yield return Wait(2.5f);                                        // (the host's copy of me has to arrive inside the room)
                Check("client: I stand inside the outpost room", ctl.IsInside(wob.transform.position));
                var doors = DoorManager.Active;
                Check("client: SECURITY LOCK is accepted by the terminal", doors != null && doors.ComputerLock(1, "  Security ", true));
                yield return Until(() => ctl.Door.ComputerLocked && !ctl.Unlocked, 40f, "the host's verdict (locked) to arrive here");
                yield return Wait(1.5f);
                Check("client: the host's verdict arrived: hard-locked here too, the hack lock shut, the door shut", ctl.Door.ComputerLocked && ctl.Door.Locked && !ctl.Unlocked && ctl.Door.IsShut);
                yield return Wait(6f);                                          // (the host looks at it too)
                Check("client: SECURITY UNLOCK is accepted by the terminal", doors != null && doors.ComputerLock(1, "security", false));
                yield return Until(() => !ctl.Door.ComputerLocked && ctl.Unlocked, 40f, "the host's verdict (unlocked) to arrive here");
                yield return Wait(1.5f);
                Check("client: the host's verdict arrived: unlocked here too, and the door is open", ctl.Unlocked && !ctl.Door.Locked && !ctl.Door.ComputerLocked && !ctl.Door.IsShut);
            }
        }

        IEnumerator RunHost()
        {
            Say("network test: HOST");
            yield return Until(() => SceneManager.GetActiveScene().name == NetConst.MenuScene && NetSession.Instance != null, 40f, "the menu");
            yield return Wait(2f);
            var net = NetSession.Instance;
            net.HostLocal();
            yield return Until(() => net.State == NetSessionState.InLobby, 30f, "the host's lobby");
            yield return Shot("n1_host_lobby");
            yield return Until(() => net.Roster.Count >= 2, 240f, "a client to join");
            yield return Wait(2f);
            Check("two players are in the lobby", net.Roster.Count == 2, $"({net.Roster.Count})");
            yield return Shot("n2_host_lobby_two");
            net.StartRun();
            yield return Until(() => net.State == NetSessionState.InRun, 90f, "the run to start");
            yield return Until(() => FloorIs(0), 90f, "the docks");
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            yield return Wait(6f);
            Check("the host sees both players in the docks", NetPlayer.All.Count == 2, $"({NetPlayer.All.Count})");
            yield return Shot("n3_host_docks");
            for (int floor = 1; floor <= 2; floor++)
            {
                tm.RideTo(floor);
                yield return Until(() => tm.Loading || hm.Floor == floor, 15f, $"the ride to floor {floor}");
                yield return Until(() => FloorIs(floor), 150f, $"floor {floor}");
                hm.godMode = true; hm.freezeTimer = true;
                yield return Wait(8f);
                Check($"floor {floor} is built on the host with its guards", GuardAI.All.Count(g => g) >= 3 && NetPlayer.All.Count == 2, $"({GuardAI.All.Count(g => g)} guards, {NetPlayer.All.Count} players)");
                if (floor == 1) yield return OutpostLockNet(true);
                yield return Shot($"n4_host_floor{floor}");
                yield return Measure($"host on floor {floor} with a client", 6f);
            }
            yield return Wait(10f);
            Finish(checksFailed == 0 ? 0 : 3);
        }

        IEnumerator RunClient()
        {
            Say("network test: CLIENT");
            yield return Until(() => SceneManager.GetActiveScene().name == NetConst.MenuScene && NetSession.Instance != null, 40f, "the menu");
            yield return Wait(2f);
            var net = NetSession.Instance;
            float deadline = Time.realtimeSinceStartup + 200f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.JoinLocal("127.0.0.1");
                float t = 0f;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return Wait(2f);
            }
            Check("joined the host's lobby", net.State == NetSessionState.InLobby);
            // (NetSession.State only turns to InRun on the host; a client stays "InLobby" and simply finds itself in the run scene: wait for the docks)
            yield return Until(() => FloorIs(0), 240f, "the docks (the host starts the run)");
            yield return Wait(6f);
            Check("the client sees both players in the docks", NetPlayer.All.Count == 2, $"({NetPlayer.All.Count})");
            Check("my own body stands in the elevator, not in the void", NetPlayer.Local != null && NetPlayer.Local.transform.position.y > -1f && NetPlayer.Local.transform.position.y < 3f);
            yield return Shot("n3_client_docks");
            for (int floor = 1; floor <= 2; floor++)
            {
                yield return Until(() => FloorIs(floor), 240f, $"floor {floor} (the host rides up)");
                yield return Wait(8f);
                var guards = GuardAI.All.Where(g => g).ToList();
                Check($"floor {floor} is built here too: the guards are puppets", guards.Count >= 3 && guards.All(g => g.IsPuppet), $"({guards.Count} guards, {guards.Count(g => g.IsPuppet)} puppets)");
                Check($"the quota is the host's", TowerManager.Instance.CurrentInfo.quota == HeistManager.Instance.Quota && HeistManager.Instance.Quota > 0, $"({HeistManager.Instance.Quota})");
                if (floor == 1) yield return OutpostLockNet(false);
                yield return Shot($"n4_client_floor{floor}");
                yield return Measure($"client on floor {floor}", 6f);
            }
            yield return Wait(4f);
            Finish(checksFailed == 0 ? 0 : 3);
        }

        // ------------------------------------------------------------------ the run-start cinematic (the Intro) in a built player (DEVELOPER_NOTES 2.19)

        IEnumerator StartRunAsHost(bool waitForClient)
        {
            yield return Until(() => SceneManager.GetActiveScene().name == NetConst.MenuScene && NetSession.Instance != null, 40f, "the menu");
            yield return Wait(2f);
            var net = NetSession.Instance;
            net.HostLocal();
            yield return Until(() => net.State == NetSessionState.InLobby, 30f, "the host's lobby");
            if (waitForClient) { yield return Until(() => net.Roster.Count >= 2, 240f, "a client to join"); yield return Wait(2f); }
            net.StartRun();
        }

        /// <summary>A lobby of one, START RUN: the Intro starts by itself and holds the player still; with <c>-be-intro-skip</c> Space is held after the first line
        /// (1 of 1 is half of the party or more), otherwise it plays to its end (27 s); then the player is free in the docks.</summary>
        IEnumerator RunIntroSolo()
        {
            bool skipTest = Has("-be-intro-skip");
            Say(skipTest ? "intro test: SOLO, Space held after the first line" : "intro test: SOLO, played to its end");
            yield return StartRunAsHost(false);
            yield return Until(() => IntroCinematic.Playing, 120f, "the Intro to start after START RUN");
            Check("the Intro starts by itself when a run starts", IntroCinematic.Playing);
            Check("the player is held still while it plays (the screen switch is on)", OutpostUi.Open && OutpostUi.Current == OutpostUi.Kind.Intro);
            Check("the cutscene is in the docks: a TV on the wall, the player's character and a coworker watching it", IntroCinematic.Current != null && IntroCinematic.Current.HasTv && IntroCinematic.Current.WorkerCount == 2 && IntroCinematic.Current.WorkerIsToon(0) && IntroCinematic.Current.WorkerIsToon(1), $"(Tiny Toon people: {(IntroCinematic.Current != null ? IntroCinematic.Current.WorkerCount : -1)} workers)");
            yield return Until(() => IntroCinematic.Clock >= 3.8f, 20f, "the first line");
            yield return Shot("i1_boss_first_line");
            if (skipTest)
            {
                float at = Time.realtimeSinceStartup;
                IntroCinematic.TestHold = true;
                yield return Until(() => !IntroCinematic.Playing, 10f, "the Intro to end after Space is held");
                float took = Time.realtimeSinceStartup - at;
                IntroCinematic.TestHold = false;
                Check("holding Space for a second skips it (1 of 1)", took >= 0.9f && took < 5f && RunSync.Instance != null && RunSync.Instance.IntroSkipped, $"({took:0.0} s)");
            }
            else
            {
                yield return Until(() => IntroCinematic.Clock >= 12.8f, 30f, "the desk shot");
                yield return Shot("i2_desk_bonuses");
                yield return Until(() => IntroCinematic.Clock >= 20.8f, 30f, "the jet");
                yield return Shot("i3_window_jet");
                yield return Until(() => IntroCinematic.Clock >= 25.5f, 30f, "the last line of the boss");
                yield return Shot("i4_boss_last_line");
                yield return Until(() => IntroCinematic.Clock >= 31.0f, 30f, "the workers' line");
                yield return Shot("i4b_worker_line");
                yield return Until(() => !IntroCinematic.Playing, 20f, "the Intro to end by itself");
                Check("the Intro ends by itself after its 35 s and was shown once", !IntroCinematic.Playing && IntroCinematic.TimesPlayed == 1);
            }
            Check("the player is free again", !OutpostUi.Open);
            yield return Until(() => FloorIs(0), 30f, "the docks");
            yield return Wait(1.5f);
            yield return Shot("i5_docks_after_intro");
            Finish(checksFailed == 0 ? 0 : 3);
        }

        /// <summary>PLAY SOLO in the menu loads the run scene with no network session at all (no RunSync): the Intro must still play, on this machine's own clock.
        /// With <c>-be-intro-skip</c> Space is held after the first line (1 of 1 skips it), otherwise it plays to its end (27 s).</summary>
        IEnumerator RunIntroOffline()
        {
            bool skipTest = Has("-be-intro-skip");
            Say(skipTest ? "intro test: PLAY SOLO (offline), Space held after the first line" : "intro test: PLAY SOLO (offline), played to its end");
            yield return Until(() => SceneManager.GetActiveScene().name == NetConst.MenuScene && NetSession.Instance != null, 40f, "the menu");
            yield return Wait(2f);
            SceneManager.LoadScene(NetConst.RunScene);                          // exactly what the PLAY SOLO button does
            yield return Until(() => IntroCinematic.Playing, 60f, "the Intro to start after PLAY SOLO");
            Check("the Intro starts by itself in a solo run (no network session)", IntroCinematic.Playing && !NetSession.Online && RunSync.Instance == null);
            Check("the player is held still while it plays (the screen switch is on)", OutpostUi.Open && OutpostUi.Current == OutpostUi.Kind.Intro);
            Check("the cutscene is in the docks: a TV on the wall, the player's character and a coworker watching it", IntroCinematic.Current != null && IntroCinematic.Current.HasTv && IntroCinematic.Current.WorkerCount == 2 && IntroCinematic.Current.WorkerIsToon(0) && IntroCinematic.Current.WorkerIsToon(1), $"(Tiny Toon people: {(IntroCinematic.Current != null ? IntroCinematic.Current.WorkerCount : -1)} workers)");
            yield return Until(() => IntroCinematic.Clock >= 3.8f, 20f, "the first line");
            yield return Shot("io1_boss_first_line");
            if (skipTest)
            {
                float at = Time.realtimeSinceStartup;
                IntroCinematic.TestHold = true;
                yield return Until(() => !IntroCinematic.Playing, 10f, "the Intro to end after Space is held");
                float took = Time.realtimeSinceStartup - at;
                IntroCinematic.TestHold = false;
                Check("holding Space for a second skips it (1 of 1)", took >= 0.9f && took < 5f, $"({took:0.0} s)");
            }
            else
            {
                yield return Until(() => IntroCinematic.Clock >= 20.8f, 30f, "the jet");
                yield return Shot("io2_window_jet");
                yield return Until(() => IntroCinematic.Clock >= 31.0f, 30f, "the workers' line");
                yield return Shot("io2b_worker_line");
                yield return Until(() => !IntroCinematic.Playing, 30f, "the Intro to end by itself");
                Check("the Intro ends by itself after its 35 s and was shown once", !IntroCinematic.Playing && IntroCinematic.TimesPlayed == 1);
            }
            Check("the player is free again", !OutpostUi.Open);
            yield return Until(() => FloorIs(0), 30f, "the docks");
            yield return Wait(1.5f);
            yield return Shot("io3_docks_after_intro");
            Finish(checksFailed == 0 ? 0 : 3);
        }

        /// <summary>For the LOOK of the Intro: a picture at two moments of every beat (35 % and 92 % through it): s0a, s0b ... s6a, s6b. <c>-be-intro-model &lt;id&gt;</c> tries another boss.</summary>
        IEnumerator RunIntroShots()
        {
            var model = Arg("-be-intro-model");
            if (!string.IsNullOrEmpty(model)) BossOffice.ModelOverride = model;
            Say($"intro shots: two pictures of every beat, boss model {(string.IsNullOrEmpty(model) ? IntroContent.BossModelId : model)}");
            yield return StartRunAsHost(false);
            yield return Until(() => IntroCinematic.Playing, 120f, "the Intro to start");
            for (int i = 0; i < IntroRules.Beats.Length; i++)
            {
                var b = IntroRules.Beats[i];
                for (int k = 0; k < 2; k++)
                {
                    float at = b.start + (k == 0 ? 0.35f : 0.92f) * b.Length;
                    yield return Until(() => IntroCinematic.Clock >= at || !IntroCinematic.Playing, 40f, $"beat {i}");
                    if (!IntroCinematic.Playing) break;
                    yield return Shot($"s{i}{(k == 0 ? "a" : "b")}");
                }
            }
            yield return Until(() => !IntroCinematic.Playing, 20f, "the end");
            Finish(checksFailed == 0 ? 0 : 3);
        }

        /// <summary>Host of two: nobody holds Space for the first seconds (it keeps playing), then the client holds it: 1 of 2 is half of the party, so it ends for both.</summary>
        IEnumerator RunIntroHost()
        {
            Say("intro test: HOST of two (a client holds Space; 1 of 2 must skip it for everybody)");
            yield return StartRunAsHost(true);
            yield return Until(() => IntroCinematic.Playing, 120f, "the Intro to start");
            var sync = RunSync.Instance;
            Check("the Intro plays on the host with a client in the party", IntroCinematic.Playing && sync != null && sync.IntroParty == 2, $"(party {(sync != null ? sync.IntroParty : -1)})");
            yield return Until(() => IntroCinematic.Clock >= 6f, 30f, "6 s into the Intro");
            yield return Shot("ih1_host_intro");
            Check("nobody is holding Space: it keeps playing", IntroCinematic.Playing && !sync.IntroSkipped);
            yield return Until(() => !IntroCinematic.Playing, 40f, "the client's hold to end the Intro");
            Check("the client alone (1 of 2 is half of the party) skipped it for everybody", sync.IntroSkipped && IntroCinematic.Clock < 0f);
            yield return Until(() => FloorIs(0), 30f, "the docks");
            yield return Wait(2f);
            Check("the host is free again", !OutpostUi.Open);
            yield return Shot("ih2_host_docks_after_intro");
            yield return Wait(4f);
            Finish(checksFailed == 0 ? 0 : 3);
        }

        IEnumerator RunIntroClient()
        {
            Say("intro test: CLIENT of two (holds Space)");
            yield return Until(() => SceneManager.GetActiveScene().name == NetConst.MenuScene && NetSession.Instance != null, 40f, "the menu");
            yield return Wait(2f);
            var net = NetSession.Instance;
            float deadline = Time.realtimeSinceStartup + 200f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.JoinLocal("127.0.0.1");
                float t = 0f;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return Wait(2f);
            }
            Check("joined the host's lobby", net.State == NetSessionState.InLobby);
            yield return Until(() => IntroCinematic.Playing, 150f, "the Intro to start");
            Check("the Intro plays on the client too", IntroCinematic.Playing);
            Check("the client's player is held still", OutpostUi.Current == OutpostUi.Kind.Intro);
            yield return Until(() => IntroCinematic.Clock >= 8f, 30f, "8 s into the Intro");
            yield return Shot("ic1_client_intro");
            float at = Time.realtimeSinceStartup;
            IntroCinematic.TestHold = true;
            yield return Until(() => !IntroCinematic.Playing, 20f, "the Intro to end after Space is held");
            float took = Time.realtimeSinceStartup - at;
            IntroCinematic.TestHold = false;
            Check("it ended for the client a second or two after he held Space", took >= 0.9f && took < 6f, $"({took:0.0} s)");
            yield return Until(() => FloorIs(0), 30f, "the docks");
            yield return Wait(2f);
            Check("the client is free again", !OutpostUi.Open);
            yield return Shot("ic2_client_docks_after_intro");
            yield return Wait(3f);
            Finish(checksFailed == 0 ? 0 : 3);
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - startedAt > 900f) { Say("FAIL the autopilot ran over its 15 minutes"); checksFailed++; Finish(5); }
        }
    }
}
