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
            if (netRole == "host") { yield return RunHost(); yield break; }
            if (netRole == "client") { yield return RunClient(); yield break; }

            // ---- the menu
            yield return Until(() => SceneManager.GetActiveScene().name == NetConst.MenuScene, 40f, "the menu scene");
            yield return Wait(3f);
            yield return Shot("01_menu");
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
                if (floor == 4)
                {
                    var pew = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).FirstOrDefault(g => g && g.isActiveAndEnabled);
                    if (pew) yield return View("floor4_5_pew", pew.transform.position + new Vector3(1.1f, 1.0f, 1.1f), pew.transform.position);
                }
                var loot = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => p && p.isActiveAndEnabled && p.Value > 0 && Vector3.Distance(p.transform.position, info.spawn) > 10f)
                    .OrderBy(p => Vector3.Distance(p.transform.position, info.spawn)).FirstOrDefault();
                if (loot) yield return View($"floor{floor}_4_loot", loot.transform.position + new Vector3(2f, 1.7f, 2f), loot.transform.position);
                yield return Measure($"floor {floor} with the guards about", 5f);
                { var rig = FindFirstObjectByType<CameraFollow>(); if (rig) rig.enabled = true; }

                if (floor == 4)
                {
                    int pews = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).Count(g => g && g.isActiveAndEnabled);
                    Check("a pew lies in floor 4's outpost", pews == 1, $"({pews} pews on the floor)");
                    Check("the guards of floor 4 are armed", GuardAI.All.Count(g => g && g.Armed) >= 3, $"({GuardAI.All.Count(g => g && g.Armed)} armed)");
                }

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
                if (floor == 4) yield return Shot("docks_demo_complete");
            }

            Check("floor 4 done: the demo is over and the vendor has the pew", TowerRules.DemoOver(tm.ResumeLevel) && Vendor.PewForSale(tm.ResumeLevel));
            yield return Measure("docks, demo complete", 3f);
            Finish(checksFailed == 0 ? 0 : 3);
        }

        // ------------------------------------------------------------------ two built players, one machine (host and client over the loopback)

        bool FloorIs(int floor)
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            return hm != null && tm != null && !tm.Loading && hm.Floor == floor && hm.Phase != HeistPhase.Lobby;
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
                yield return Shot($"n4_client_floor{floor}");
                yield return Measure($"client on floor {floor}", 6f);
            }
            yield return Wait(4f);
            Finish(checksFailed == 0 ? 0 : 3);
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - startedAt > 900f) { Say("FAIL the autopilot ran over its 15 minutes"); checksFailed++; Finish(5); }
        }
    }
}
