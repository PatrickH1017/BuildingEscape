using System.Collections;
using System.Collections.Generic;
using BuildingEscape.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.EditorTools
{
    /// <summary>Batch-mode Play Mode scenarios for the networking layer (dev verification only).</summary>
    [InitializeOnLoad]
    public static class NetDevHarness
    {
        static NetDevHarness()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode) return;
                var mode = SessionState.GetString("netdev.mode", "");
                if (mode == "") return;
                var go = new GameObject("NetDevRunner");
                go.AddComponent<NetDevRunner>().mode = mode;
            };
        }

        static void Start(string mode)
        {
            SessionState.SetString("netdev.mode", mode);
            EditorSceneManager.OpenScene("Assets/Scenes/Boot.unity");
            EditorApplication.isPlaying = true;
        }

        public static void Steam() => Start("steam");
        public static void Host() => Start("host");
        public static void Client() => Start("client");
        public static void WorldHost() => Start("worldhost");
        public static void WorldClient() => Start("worldclient");
        public static void Qa() => Start("qa");
        public static void Spawn() => Start("spawn");
        public static void Social() => Start("social");
        public static void Shots() => Start("shots");
        public static void Toon() => Start("toon");
        public static void Floors()
        {
            SessionState.SetString("netdev.mode", "floors");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Doors()
        {
            SessionState.SetString("netdev.mode", "doors");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Outpost()
        {
            SessionState.SetString("netdev.mode", "outpost");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Sneak()
        {
            SessionState.SetString("netdev.mode", "sneak");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Pew()
        {
            SessionState.SetString("netdev.mode", "pew");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void MenusLobby() => Start("menuslobby");
        public static void MenusRun() => Start("menusrun");
        public static void Menus()
        {
            SessionState.SetString("netdev.mode", "menus");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PewLoot()
        {
            SessionState.SetString("netdev.mode", "pewloot");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunNet() => Start("gunnet");
        public static void OutpostNet() => Start("outpostnet");
        public static void Demo()
        {
            SessionState.SetString("netdev.mode", "demo");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void DemoNet() => Start("demonet");
        public static void MeleeNet() => Start("meleenet");
        public static void LootQa()
        {
            SessionState.SetString("netdev.mode", "lootqa");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void DemoUi()
        {
            SessionState.SetString("netdev.mode", "demoui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Shaft()
        {
            SessionState.SetString("netdev.mode", "shaft");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void ShaftNet() => Start("shaftnet");
        public static void Ui()
        {
            SessionState.SetString("netdev.mode", "ui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PewLook()
        {
            SessionState.SetString("netdev.mode", "pewlook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Armed() => Start("armed");
        public static void ArmedHost() => Start("armedhost");
        public static void ArmedClient() => Start("armedclient");
        public static void VoiceHost() => Start("voicehost");
        public static void VoiceClient() => Start("voiceclient");
        public static void Offline()
        {
            SessionState.SetString("netdev.mode", "offline");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void SockHost() => Start("sockhost");
        public static void SockClient() => Start("sockclient");
        public static void SteamHost() => Start("steamhost");
        public static void SteamClient() => Start("steamclient");
    }

    public partial class NetDevRunner : MonoBehaviour
    {
        public string mode;
        float started;

        static void Log(string m) => Debug.Log("[NetDev] " + m);

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            started = Time.realtimeSinceStartup;
            if (mode != "offline" && mode != "sneak" && mode != "floors" && mode != "doors" && mode != "outpost" && mode != "pew" && mode != "pewlook" && mode != "ui" && mode != "menus" && mode != "shaft" && mode != "pewloot" && mode != "demo" && mode != "demoui" && mode != "lootqa")
                yield return WaitFor(() => NetSession.Instance != null && SceneManager.GetActiveScene().name == NetConst.MenuScene, 30, "menu");
            switch (mode)
            {
                case "steam": yield return SteamScenario(); break;
                case "host": case "sockhost": yield return HostScenario(); break;
                case "client": case "sockclient": yield return ClientScenario(); break;
                case "steamhost": yield return SteamHostScenario(); break;
                case "shots": yield return ShotsScenario(); break;
                case "toon": yield return ToonScenario(); break;
                case "sneak": yield return SneakScenario(); break;
                case "floors": yield return FloorsScenario(); break;
                case "doors": yield return DoorsScenario(); break;
                case "outpost": yield return OutpostScenario(); break;
                case "pew": yield return PewScenario(); break;
                case "pewlook": yield return PewLookScenario(); break;
                case "ui": yield return UiScenario(); break;
                case "shaft": yield return ShaftScenario(); break;
                case "pewloot": yield return PewLootScenario(); break;
                case "demo": yield return DemoScenario(); break;
                case "demoui": yield return DemoUiScenario(); break;
                case "meleenet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? MeleeHostScenario() : MeleeClientScenario(); break;
                case "lootqa": yield return LootQaScenario(); break;
                case "demonet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? DemoHostScenario() : DemoClientScenario(); break;
                case "gunnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? GunHostScenario() : GunClientScenario(); break;
                case "outpostnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? OutpostNetHostScenario() : OutpostNetClientScenario(); break;
                case "shaftnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? ShaftHostScenario() : ShaftClientScenario(); break;
                case "menus": yield return MenusScenario(); break;
                case "menuslobby": yield return MenusLobbyScenario(); break;
                case "menusrun": yield return MenusRunScenario(); break;
                case "armed": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? ArmedHostScenario() : ArmedClientScenario(); break;
                case "armedhost": yield return ArmedHostScenario(); break;
                case "armedclient": yield return ArmedClientScenario(); break;
                case "offline": yield return OfflineScenario(); break;
                case "voicehost": yield return VoiceScenario(true); break;
                case "voiceclient": yield return VoiceScenario(false); break;
                case "social": yield return SocialScenario(); break;
                case "qa": yield return QaScenario(System.Environment.GetEnvironmentVariable("BE_QA_ROLE")); break;
                case "spawn": yield return SpawnScenario(System.Environment.GetEnvironmentVariable("BE_QA_ROLE")); break;
                case "worldhost": yield return WorldHostScenario(); break;
                case "worldclient": yield return WorldClientScenario(); break;
                case "steamclient": yield return SteamClientScenario(); break;
            }
        }

        void Update()
        {
            if (Time.realtimeSinceStartup - started > 400f) { Log("FAIL overall timeout"); Quit(1); }
        }

        // PlayerPrefs live in the registry and are shared by every project with this company/product name,
        // so a test that sets a look must put the previous value back before exiting.
        static string savedLook;
        static bool lookChanged;
        static void SetTestLook(string look)
        {
            savedLook = PlayerPrefs.HasKey(PlayerAppearance.PrefKey) ? PlayerPrefs.GetString(PlayerAppearance.PrefKey) : null;
            lookChanged = true;
            PlayerPrefs.SetString(PlayerAppearance.PrefKey, look);
        }

        static void Quit(int code)
        {
            if (lookChanged)
            {
                if (savedLook == null) PlayerPrefs.DeleteKey(PlayerAppearance.PrefKey);
                else PlayerPrefs.SetString(PlayerAppearance.PrefKey, savedLook);
                PlayerPrefs.Save();
                lookChanged = false;
            }
            EditorApplication.Exit(code);
        }

        bool failed;
        IEnumerator WaitFor(System.Func<bool> cond, float timeout, string what)
        {
            float t = 0;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (!cond()) { Log($"FAIL timeout waiting for {what}"); failed = true; Quit(1); }
        }

        // ------------------------------------------------------------------ Steam lobby round trip

        IEnumerator SteamScenario()
        {
            var net = NetSession.Instance;
            Log($"steam ready={net.Steam.Ready} name={net.Steam.LocalName} error={net.Steam.Error}");
            if (!net.Steam.Ready) { Quit(2); yield break; }

            net.HostSteam(LobbyVisibility.CodeOnly);
            yield return WaitFor(() => net.State == NetSessionState.InLobby || net.State == NetSessionState.Offline, 40, "lobby");
            Log($"code-only lobby: state={net.State} host={net.IsHost} code={net.LobbyCode} roster={net.Roster.Count} status='{net.Status}'");

            net.SetLook("FF0000-2-3-110-90");
            yield return WaitFor(() => net.Roster.Count > 0 && net.Roster[0].Look == "FF0000-2-3-110-90", 15, "look in lobby member data");
            Log($"look replicated through lobby: {net.Roster[0].Look}");
            var probe = new LobbyService();
            List<LobbyListing> res = null;
            for (int attempt = 1; attempt <= 4; attempt++)
            {
                yield return new WaitForSeconds(attempt == 1 ? 0f : 3f);
                res = null; probe.Search(net.LobbyCode, l => res = l);
                yield return WaitFor(() => res != null, 30, "code search");
                Log($"search by code attempt {attempt}: found={res.Count}");
                if (res.Count > 0) break;
            }
            Log($"search by code: found={res.Count}" + (res.Count > 0 ? $" members={res[0].Members}/{res[0].Max} versionOk={res[0].VersionOk} name='{res[0].Name}'" : ""));

            string code = net.LobbyCode;
            res = null; probe.Search(null, l => res = l);
            yield return WaitFor(() => res != null, 30, "browse");
            bool listed = res.Exists(x => x.Code == code);
            Log($"public browse: {res.Count} lobbies, code-only lobby listed={listed} (expected False)");

            net.SetReady(true);
            yield return new WaitForSeconds(1f);
            Log($"ready flag: LocalReady={net.LocalReady} rosterReady={(net.Roster.Count > 0 ? net.Roster[0].Ready.ToString() : "n/a")}");

            net.Leave();
            yield return WaitFor(() => net.State == NetSessionState.Offline && SceneManager.GetActiveScene().name == NetConst.MenuScene, 20, "leave");
            Log("left lobby, back at menu");

            net.HostSteam(LobbyVisibility.Public);
            yield return WaitFor(() => net.State == NetSessionState.InLobby || net.State == NetSessionState.Offline, 40, "public lobby");
            code = net.LobbyCode;
            yield return new WaitForSeconds(2f);
            res = null; probe.Search(null, l => res = l);
            yield return WaitFor(() => res != null, 30, "browse 2");
            Log($"public lobby {code} listed in browser={res.Exists(x => x.Code == code)} (expected True)");

            net.Leave();
            yield return WaitFor(() => net.State == NetSessionState.Offline, 20, "leave 2");
            probe.Dispose();
            Log("DONE steam");
            Quit(0);
        }

        // ------------------------------------------------------------------ gameplay sync (two processes, local transport)

        static string SigFile(string n) => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_sig_" + n + ".txt");
        static void Signal(string n, string body = "1") => System.IO.File.WriteAllText(SigFile(n), body);
        static string ReadSig(string n)
        {
            try { return System.IO.File.Exists(SigFile(n)) ? System.IO.File.ReadAllText(SigFile(n)) : null; }
            catch (System.IO.IOException) { return null; }
        }
        IEnumerator WaitSig(string n, float timeout) { yield return WaitFor(() => ReadSig(n) != null, timeout, "signal " + n); }

        static void ClearSignals()
        {
            foreach (var f in System.IO.Directory.GetFiles(System.IO.Path.GetTempPath(), "be_sig_*.txt")) System.IO.File.Delete(f);
        }

        static NetPlayer Mine => NetPlayer.Local;
        static Backpack MyPack => Mine ? Mine.GetComponent<Backpack>() : null;

        IEnumerator FloorReady(int floor, float timeout)
        {
            yield return WaitFor(() => HeistManager.Instance != null && TowerManager.Instance != null && !TowerManager.Instance.Loading
                                        && HeistManager.Instance.Floor == floor && HeistManager.Instance.Phase != HeistPhase.Lobby, timeout, "floor " + floor);
        }

        static Pickup FreeMug(int skip = 0)
        {
            int n = 0;
            foreach (var kv in NetIds.All)
                if (kv.Value is Pickup p && p.isActiveAndEnabled && !p.IsHeld && p.GetComponent<Throwable>() && !p.Loot && n++ >= skip) return p;
            return null;
        }

        static GuardAI FirstGuard(int skip = 0)
        {
            int n = 0;
            foreach (var kv in NetIds.All) if (kv.Value is GuardAI g && !g.Dead && n++ >= skip) return g;
            return null;
        }

        static void Teleport(Vector3 p) { if (Mine) Mine.PlaceAt(p, Quaternion.identity); }

        /// <summary>Solo play (Greybox opened directly, no network): the old rules must be untouched.</summary>
        IEnumerator OfflineScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            Log($"[Solo] basement: net online={NetSession.Online} synced={NetRole.Synced} cash={hm.Cash} players={WobblyPlayerRegistry.All.Count}");
            TowerManager.Instance.GoToLevel(1);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 60, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            int puppets = 0; foreach (var gd in GuardAI.All) if (gd && gd.IsPuppet) puppets++;
            Log($"[Solo] floor 1: objects={NetIds.Count} guards={GuardAI.All.Count} puppets={puppets} (must be 0) quota={hm.Quota}");

            // guards are the Tiny Toon police officers (when the pack is set up) and walk their patrol
            yield return new WaitForSeconds(3f);
            int cops = 0, moving = 0;
            foreach (var gd in GuardAI.All)
            {
                var ca = gd ? gd.GetComponentInChildren<CharacterAnimator>() : null;
                if (!ca || !ca.ClipMode) continue;
                cops++;
                if (ca.DebugWeights()[0] < 0.5f) moving++;
            }
            Log($"[Solo] guards as police officers: {cops}/{GuardAI.All.Count}, walking or running: {moving}");

            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var mug = FreeMug(1);
            player.transform.position = mug.transform.position + new Vector3(0.8f, 0.6f, 0f);
            yield return new WaitForSeconds(1.0f);
            bool stored = pack.TryStore(mug);
            Log($"[Solo] stored={stored} pack={pack.Items.Count} mugActive={mug.gameObject.activeSelf}");
            pack.Drop(mug);
            yield return new WaitForSeconds(0.5f);
            Log($"[Solo] dropped: pack={pack.Items.Count} mugActive={mug.gameObject.activeSelf}");

            var guard = FirstGuard();
            guard.WeaponHit(4f, player.gameObject);
            Log($"[Solo] guard hit directly: state={guard.State} hits={guard.Hits}");

            HeavyLoot loot = null;
            foreach (var kv in NetIds.All) if (kv.Value is Pickup lp && lp.Loot && lp.Loot.lifters == 1) { loot = lp.Loot; break; }
            var car = FindFirstObjectByType<ElevatorExtraction>();
            int before = hm.Banked;
            loot.transform.position = car.transform.position + new Vector3(1.7f, 0.8f, 1.0f);
            loot.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(4f);
            Log($"[Solo] banked +{hm.Banked - before}");

            hm.DebugCompleteFloor();
            // the team gets into the car and uses the two buttons: direction (Up -> Down), then go
            player.transform.position = TowerManager.Instance.CurrentInfo.spawn + Vector3.up * 1.1f;
            yield return new WaitForSeconds(1.5f);
            var panel = ElevatorExtraction.Current;
            Log($"[Solo] panel before: up={ElevatorExtraction.GoingUp} destination={ElevatorExtraction.Destination} aboard={ElevatorExtraction.EveryoneAboard}");
            panel.HostPress(player.transform, pack, ElevatorExtraction.SelectBase + 0);   // the wall panel: choose the Loading Docks, then GO
            yield return new WaitForSeconds(0.3f);
            Log($"[Solo] panel after choosing the docks: up={ElevatorExtraction.GoingUp} destination={ElevatorExtraction.Destination}");
            panel.HostPress(player.transform, pack, 1);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 0);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 0, 60, "back down");
            Log($"[Solo] back in the basement: phase={hm.Phase} objects={NetIds.Count}");
            Log("DONE offline");
            Quit(0);
        }

        /// <summary>Voice chat: each side talks (a tone stands in for the microphone); check it arrives and is played.</summary>
        IEnumerator VoiceScenario(bool host)
        {
            var net = NetSession.Instance;
            if (host)
            {
                ClearSignals();
                net.HostLocal();
                yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
                yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
                net.StartRun();
                yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            }
            else
            {
                float deadline = Time.realtimeSinceStartup + 150f;
                while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
                {
                    net.JoinLocal("127.0.0.1");
                    float t = 0;
                    while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                    if (net.State != NetSessionState.InLobby) yield return new WaitForSeconds(2f);
                }
            }
            yield return FloorReady(0, 120);
            if (host) { HeistManager.Instance.godMode = true; }
            yield return new WaitForSeconds(1f);

            int heard = 0;
            System.Action<NoiseEvent> count = n => { if (n.Loudness >= 4f) heard++; };
            if (host) NoiseBus.OnNoise += count;

            var vc = VoiceChat.Instance;
            vc.UseTestInput(new ToneVoiceInput(host ? 440f : 880f));
            Signal(host ? "h_talking" : "c_talking");
            yield return WaitSig(host ? "c_talking" : "h_talking", 60);
            yield return new WaitForSeconds(4f);

            ulong other = 0; bool found = false;
            foreach (var p in NetPlayer.All) if (!p.IsOwner) { other = p.OwnerClientId; found = true; }
            var v = found ? vc.VoiceOf(other) : null;
            Log($"[Voice] {(host ? "H" : "C")} sent={vc.PacketsSent} received={vc.PacketsReceived} relayed={vc.PacketsRelayed} speaking={vc.Speaking} micLevel={vc.MicLevel:0.00}");
            Log($"[Voice] {(host ? "H" : "C")} voice of speaker {other}: exists={v != null} packets={(v ? v.packets : 0)} queued={(v ? v.samplesQueued : 0)} played={(v ? v.samplesPlayed : 0)} peak={(v ? v.lastLevel : 0):0.00} playing={(v && v.Playing)}");
            if (host)
            {
                NoiseBus.OnNoise -= count;
                Log($"[Voice] H guards heard {heard} noise event(s) while the tones played");
            }
            Signal(host ? "h_voice_done" : "c_voice_done");
            yield return WaitSig(host ? "c_voice_done" : "h_voice_done", 60);
            Log("DONE voice");
            Quit(0);
        }

        IEnumerator WorldHostScenario()
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
            hm.AddCash(5000);
            Log($"[World] H basement ready: phase={hm.Phase} cash={hm.Cash} objects={NetIds.Count}");
            Signal("h_ready0");

            // ---- the client buys at the vendor
            yield return WaitSig("c_bought", 90);
            var cp = NetPlayer.BySlot(1);
            var pack = cp.GetComponent<Backpack>();
            Log($"[World] H sees the client's pack: {pack.Items.Count} item(s), equipped={(pack.Equipped ? pack.Equipped.Label : "none")}, cash now {hm.Cash}");
            foreach (var it in pack.Items) Log($"[World] H   pack item: {it.Label} active={it.gameObject.activeSelf} id={it.NetId}");

            // ---- ride up (the client presses the key)
            yield return WaitSig("c_ride_sent", 60);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || HeistManager.Instance.Floor == 1);
            yield return FloorReady(1, 90);
            Log($"[World] H on floor {hm.Floor}: phase={hm.Phase} quota={hm.Quota} objects={NetIds.Count} checksum={NetIds.Checksum()} guards={GuardAI.All.Count}");
            hm.godMode = true;       // the test pokes guards; do not let them bust the team
            hm.freezeTimer = true;
            yield return new WaitForSeconds(5f);

            // ---- snapshot comparison data
            var sb = new System.Text.StringBuilder();
            int shown = 0;
            var ordered = new List<KeyValuePair<int, Component>>();
            foreach (var kv in NetIds.All) if (kv.Value is GuardAI) ordered.Add(kv);
            foreach (var kv in NetIds.All) if (!(kv.Value is GuardAI)) ordered.Add(kv);
            foreach (var kv in ordered)
            {
                if (shown >= 40) break;
                var c = kv.Value; if (!c || !c.gameObject.activeInHierarchy) continue;
                var p = c.transform.position;
                sb.AppendLine($"{(c is GuardAI ? "G" : "P")} {kv.Key} {p.x:0.00} {p.y:0.00} {p.z:0.00}");
                shown++;
            }
            Signal("h_snap", sb.ToString());
            Log($"[World] H wrote {shown} object positions");

            // ---- the client hits a guard
            yield return WaitSig("c_hit", 90);
            var gid = int.Parse(ReadSig("c_hit").Trim());
            yield return new WaitForSeconds(1.2f);
            var g = NetIds.Get<GuardAI>(gid);
            Log($"[World] H guard {gid} after the client's hit: state={g.State} hits={g.Hits}");

            // ---- the client makes noise next to a guard
            yield return WaitSig("c_noise", 60);
            var nid = int.Parse(ReadSig("c_noise").Trim());
            yield return new WaitForSeconds(1.0f);
            var ng = NetIds.Get<GuardAI>(nid);
            Log($"[World] H guard {nid} after the client's noise: state={ng.State}");

            // ---- store / drop / grab are driven by the client; report what the host sees
            yield return WaitSig("c_stored", 60);
            Log($"[World] H client pack now holds {pack.Items.Count} item(s)");
            yield return WaitSig("c_dropped", 60);
            Log($"[World] H client pack now holds {pack.Items.Count} item(s) after the drop");
            yield return WaitSig("c_grabbed", 60);
            var cg = cp.GetComponent<PlayerGrab>();
            Log($"[World] H client is holding: {(cg.Held ? cg.Held.name : "nothing")}");
            yield return WaitSig("c_released", 60);
            Log($"[World] H client is holding: {(cg.Held ? cg.Held.name : "nothing")} after the throw");

            // ---- bank a piece of loot by putting it in the car
            HeavyLoot loot = null;
            foreach (var kv in NetIds.All) if (kv.Value is Pickup lp && lp.Loot && lp.Loot.lifters == 1 && lp.isActiveAndEnabled) { loot = lp.Loot; break; }
            var car = FindFirstObjectByType<ElevatorExtraction>();
            int lootId = loot.GetComponent<Pickup>().NetId;
            int before = hm.Banked;
            loot.transform.position = car.transform.position + new Vector3(1.7f, 0.8f, 1.0f); // clear of the capsules in the car
            loot.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            Signal("h_banked_id", lootId.ToString());
            yield return new WaitForSeconds(4f);
            Log($"[World] H banked {hm.Banked - before} (loot {lootId} exists={NetIds.Get<Pickup>(lootId) != null})");

            // ---- the mug the client threw: where did it end up?
            var thrownId = int.Parse(ReadSig("c_thrown").Trim());
            var tm = NetIds.Get<Pickup>(thrownId);
            Signal("h_thrown", $"{tm.transform.position.x:0.00} {tm.transform.position.y:0.00} {tm.transform.position.z:0.00}");
            Log($"[World] H thrown mug {thrownId} rests at {tm.transform.position}");

            // ---- backup call: a new guard appears everywhere
            int guardsBefore = GuardAI.All.Count;
            hm.CallBackup(FirstGuard());
            yield return new WaitForSeconds(2f);
            Signal("h_backup", $"{GuardAI.All.Count} {NetIds.Count} {NetIds.Checksum()}");
            Log($"[World] H backup: guards {guardsBefore} -> {GuardAI.All.Count}, objects={NetIds.Count} checksum={NetIds.Checksum()}");

            yield return WaitSig("c_backup_checked", 30);

            // ---- the client is knocked over by the host (routed to the owner), and hides
            var cw = cp.GetComponent<WobblyPlayer>();
            cw.Stumble(Vector3.up * 2f, 2f);
            Signal("h_stumbled");
            yield return WaitSig("c_hidden", 30);
            yield return new WaitForSeconds(0.5f);
            Log($"[World] H the client's player is hidden here: {cw.IsHidden}");

            // ---- a loot item breaks
            HeavyLoot victim = null;
            foreach (var kv in NetIds.All) if (kv.Value is Pickup vp && vp.Loot && vp.isActiveAndEnabled && vp.Loot != loot) { victim = vp.Loot; break; }
            int victimId = victim.GetComponent<Pickup>().NetId;
            int lostBefore = hm.Lost;
            typeof(HeavyLoot).GetMethod("Shatter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(victim, null);
            yield return new WaitForSeconds(1f);
            Signal("h_broken", victimId.ToString());
            Log($"[World] H loot {victimId} shattered: lost {lostBefore} -> {hm.Lost}, exists={NetIds.Get<Pickup>(victimId) != null}");

            // ---- complete the floor with a piece of loot left in the car: it rides down with the team
            HeavyLoot rider = null;
            foreach (var kv in NetIds.All) if (kv.Value is Pickup rp && rp.Loot && rp.isActiveAndEnabled && rp.Loot.lifters == 1 && rp.Loot != loot) { rider = rp.Loot; break; }
            string riderName = rider.displayName;
            int riderId = rider.GetComponent<Pickup>().NetId;
            rider.transform.position = car.transform.position + new Vector3(-1.7f, 0.8f, 1.0f);
            rider.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            rider.GetComponent<Pickup>().IsHeld = true; // carried loot is not banked by the car: it rides down with the team
            yield return new WaitForSeconds(0.5f);
            Signal("h_ride_down", riderName);
            yield return WaitSig("c_in_car", 30);
            Log($"[World] H floor complete: everyone aboard={ElevatorExtraction.EveryoneAboard}, car holds {car.CargoIds().Length} item(s)");
            hm.DebugCompleteFloor();
            Signal("h_complete");
            yield return new WaitUntil(() => TowerManager.Instance.Loading || HeistManager.Instance.Floor == 0);
            yield return FloorReady(0, 90);
            yield return new WaitForSeconds(2f);
            Pickup arrived = null;
            foreach (var kv in NetIds.All) if (kv.Value is Pickup ap && ap.Loot && ap.Loot.displayName == riderName) arrived = ap;
            Signal("h_down", arrived ? $"{arrived.NetId} {arrived.transform.position.x:0.0} {arrived.transform.position.z:0.0}" : "none");
            if (arrived) arrived.IsHeld = false;
            Log($"[World] H back in the basement: phase={hm.Phase} cash={hm.Cash} surplus={hm.SurplusPaid} cargo \"{riderName}\" -> id {(arrived ? arrived.NetId : -1)} active={(arrived && arrived.isActiveAndEnabled)} objects={NetIds.Count}");
            // ---- the client sells that loot from their pack at the vendor
            int cashBefore = hm.Cash;
            yield return WaitSig("c_sold", 60);
            Log($"[World] H after the client sold: cash {cashBefore} -> {hm.Cash}, loot exists={(arrived ? NetIds.Get<Pickup>(arrived.NetId) != null : false)}");
            yield return WaitSig("c_done", 60);
            Log("DONE worldhost");
            Quit(0);
        }

        IEnumerator WorldClientScenario()
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
            var hm = HeistManager.Instance;
            yield return WaitSig("h_ready0", 90);

            // ---- buy at the vendor
            var vendor = Vendor.Current;
            Log($"[World] C basement: phase={hm.Phase} cash={hm.Cash} vendor={(vendor != null)} objects={NetIds.Count}");
            Teleport(vendor.transform.position + Vector3.up * 1.1f);
            yield return new WaitForSeconds(1.5f);
            var pack = MyPack;
            RunSync.Instance.RequestBuy(0);                       // coffee mug
            yield return WaitFor(() => pack.Items.Count >= 1, 20, "mug in pack");
            if (MeleeWeapon.Available)
            {
                RunSync.Instance.RequestBuy(4);                   // crowbar
                yield return WaitFor(() => pack.Items.Count >= 2, 20, "crowbar in pack");
                foreach (var it in pack.Items) if (it.GetComponent<MeleeWeapon>()) { pack.SetEquipped(it); break; }
                yield return WaitFor(() => pack.Equipped != null, 20, "crowbar equipped");
            }
            Log($"[World] C pack: {pack.Items.Count} item(s), equipped={(pack.Equipped ? pack.Equipped.Label : "none")}, cash={hm.Cash}");
            Signal("c_bought");

            // ---- back into the car, then ride up
            Teleport(TowerManager.Instance.CurrentInfo.spawn + Vector3.up * 1.1f);
            yield return WaitFor(() => ElevatorExtraction.EveryoneAboard, 30, "everyone aboard (replicated)");
            Log($"[World] C everyone aboard={ElevatorExtraction.EveryoneAboard}; asking to ride up");
            RunSync.Instance.RequestPanel(1);                    // Go (direction is Up by default)
            Signal("c_ride_sent");
            yield return new WaitForSeconds(2f);
            yield return FloorReady(1, 90);
            yield return WaitFor(() => RunSync.SyncChecked, 30, "floor checksum from the host");
            Log($"[World] C on floor {hm.Floor}: phase={hm.Phase} quota={hm.Quota} objects={NetIds.Count} checksum={NetIds.Checksum()} guards={GuardAI.All.Count} " +
                $"guardsPuppet={System.Linq.Enumerable.Count(GuardAI.All, x => x && x.IsPuppet)}");
            yield return WaitSig("h_snap", 60);

            // ---- compare against the host's positions
            int match = 0, total = 0, guardMatch = 0, guardTotal = 0;
            foreach (var line in ReadSig("h_snap").Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var f = line.Trim().Split(' ');
                if (f.Length < 5) continue;
                int id = int.Parse(f[1]);
                var hp = new Vector3(float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture),
                                     float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture),
                                     float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture));
                var c = NetIds.Get<Component>(id);
                bool guard = f[0] == "G";
                if (guard) guardTotal++; else total++;
                if (c == null) continue;
                float tol = guard ? 4f : 0.5f;
                if ((c.transform.position - hp).magnitude <= tol) { if (guard) guardMatch++; else match++; }
            }
            Log($"[World] C positions match the host: pickups {match}/{total}, guards {guardMatch}/{guardTotal}");

            // ---- hit a guard with the crowbar
            var g = FirstGuard();
            Teleport(g.transform.position + new Vector3(1.4f, 0.5f, 0f));
            yield return new WaitForSeconds(1.5f);
            g = NetIds.Get<GuardAI>(g.NetId);
            g.WeaponHit(4f, Mine.gameObject);
            Signal("c_hit", g.NetId.ToString());
            yield return new WaitForSeconds(2f);
            Log($"[World] C guard {g.NetId} after my hit: state={g.State} puppet={g.IsPuppet}");

            // ---- noise
            var g2 = FirstGuard(1) ?? FirstGuard();
            Teleport(g2.transform.position + new Vector3(3f, 0.5f, 0f));
            yield return new WaitForSeconds(1.5f);
            NoiseBus.Emit(Mine.transform.position, 30f, Mine.gameObject);
            Signal("c_noise", g2.NetId.ToString());
            yield return new WaitForSeconds(1.5f);

            // ---- store, drop
            var mug = FreeMug(2);
            Teleport(mug.transform.position + new Vector3(0.8f, 0.6f, 0f));
            yield return new WaitForSeconds(1.5f);
            int packBefore = pack.Items.Count;
            pack.TryStore(mug);
            yield return WaitFor(() => pack.Items.Count == packBefore + 1, 15, "stored");
            Log($"[World] C stored: pack {packBefore} -> {pack.Items.Count}, mug active={mug.gameObject.activeSelf}");
            Signal("c_stored");
            yield return new WaitForSeconds(1.0f);
            pack.Drop(mug);
            yield return WaitFor(() => pack.Items.Count == packBefore, 15, "dropped");
            yield return new WaitForSeconds(1.0f);
            Log($"[World] C dropped: pack {pack.Items.Count}, mug active={mug.gameObject.activeSelf} at {mug.transform.position}");
            Signal("c_dropped");

            // ---- grab and throw a mug
            var mug2 = FreeMug(3);
            Teleport(mug2.transform.position + new Vector3(0.7f, 0.6f, 0f));
            yield return new WaitForSeconds(1.5f);
            var pg = Mine.GetComponent<PlayerGrab>();
            RunSync.Instance.RequestGrab(mug2);
            yield return WaitFor(() => pg.Held != null, 15, "grabbed");
            Log($"[World] C holding {pg.Held.name}");
            Signal("c_grabbed");
            yield return new WaitForSeconds(1.0f);
            RunSync.Instance.RequestRelease(true);
            yield return WaitFor(() => pg.Held == null, 15, "released");
            yield return new WaitForSeconds(0.5f);
            Log($"[World] C released; mug is a puppet here: {(mug2.TryGetComponent(out Rigidbody mrb) ? mrb.isKinematic.ToString() : "n/a")}");
            Signal("c_released");
            Signal("c_thrown", mug2.NetId.ToString());

            // ---- bank
            yield return WaitSig("h_banked_id", 60);
            int lootId = int.Parse(ReadSig("h_banked_id").Trim());
            yield return new WaitForSeconds(5f);
            Log($"[World] C after the host banked loot {lootId}: banked={hm.Banked} loot exists={NetIds.Get<Pickup>(lootId) != null}");

            yield return WaitSig("h_thrown", 60);
            var tp = ReadSig("h_thrown").Trim().Split(' ');
            var hpos = new Vector3(float.Parse(tp[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(tp[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(tp[2], System.Globalization.CultureInfo.InvariantCulture));
            Log($"[World] C thrown mug is {(mug2.transform.position - hpos).magnitude:0.00} m from where the host has it");

            yield return WaitSig("h_backup", 60);
            var bk = ReadSig("h_backup").Trim().Split(' ');
            yield return new WaitForSeconds(1.5f);
            Log($"[World] C backup: guards {GuardAI.All.Count} (host {bk[0]}), objects={NetIds.Count} (host {bk[1]}), checksum {NetIds.Checksum()} (host {bk[2]})");
            Signal("c_backup_checked");

            yield return WaitSig("h_stumbled", 30);
            var mw = Mine.GetComponent<WobblyPlayer>();
            yield return WaitFor(() => mw.IsStumbling, 5, "stumble reached me");
            Log($"[World] C I was knocked over by the host: stumbling={mw.IsStumbling}");
            mw.IsHidden = true;
            yield return new WaitForSeconds(1.0f);
            Signal("c_hidden");

            yield return WaitSig("h_broken", 60);
            int vid = int.Parse(ReadSig("h_broken").Trim());
            yield return new WaitForSeconds(1f);
            Log($"[World] C loot {vid} after the host broke it: exists={NetIds.Get<Pickup>(vid) != null}, lost={hm.Lost}");

            yield return WaitSig("h_ride_down", 60);
            Teleport(TowerManager.Instance.CurrentInfo.spawn + Vector3.up * 1.1f);
            yield return new WaitForSeconds(1.5f);
            Signal("c_in_car");
            yield return WaitSig("h_complete", 30);
            yield return new WaitForSeconds(0.5f);
            RunSync.Instance.RequestPanel(0);                    // Up -> Down
            yield return new WaitForSeconds(0.6f);
            RunSync.Instance.RequestPanel(1);                    // Go
            yield return new WaitUntil(() => TowerManager.Instance.Loading || HeistManager.Instance.Floor == 0);
            yield return FloorReady(0, 90);
            yield return new WaitForSeconds(2.5f);
            yield return WaitSig("h_down", 60);
            var dn = ReadSig("h_down").Trim().Split(' ');
            string rn = ReadSig("h_ride_down").Trim();
            Pickup got = null;
            foreach (var kv in NetIds.All) if (kv.Value is Pickup ap && ap.Loot && ap.Loot.displayName == rn) got = ap;
            Log($"[World] C back in the basement: phase={hm.Phase} cash={hm.Cash} cargo \"{rn}\" -> id {(got ? got.NetId : -1)} (host {dn[0]}) active={(got && got.isActiveAndEnabled)} objects={NetIds.Count}");

            // ---- put the loot in my pack and sell it
            if (got != null)
            {
                got.IsHeld = false;
                mw.IsHidden = false;
                Teleport(got.transform.position + new Vector3(0.9f, 0.6f, 0f));
                yield return new WaitForSeconds(1.5f);
                int n0 = pack.Items.Count;
                pack.TryStore(got);
                yield return WaitFor(() => pack.Items.Count == n0 + 1, 15, "loot in pack");
                Log($"[World] C loot in my pack ({pack.Items.Count} items); weight {pack.Weight:0.#} kg");
                Teleport(Vendor.Current.transform.position + Vector3.up * 1.1f);
                yield return new WaitForSeconds(1.5f);
                int cash0 = hm.Cash;
                int gid = got.NetId;
                RunSync.Instance.RequestSell();
                yield return WaitFor(() => NetIds.Get<Pickup>(gid) == null && hm.Cash > cash0, 20, "sold");
                Log($"[World] C sold from my pack: cash {cash0} -> {hm.Cash}, loot exists={NetIds.Get<Pickup>(gid) != null}");
                Signal("c_sold");
            }
            else Signal("c_sold");
            Signal("c_done");
            Log("DONE worldclient");
            Quit(0);
        }

        // ------------------------------------------------------------------ screenshots of the 3D lobby

        static void Shot(string name) => ShotCam(Camera.main, name);

        static void ShotCam(Camera cam, string name)
        {
            // (a batch run with -nographics has no device to draw with: Camera.Render crashes Unity there, so the picture is skipped)
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { Log("SKIP picture " + name + " (no graphics device in this run)"); return; }
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_shot_" + name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            int pink = RenderAudit.CountMagenta(tex.GetPixels32());
            Log("saved " + path + $" magenta={pink}");
            if (pink >= 200) Log($"FAIL magenta in {name}: {pink} pixels");
        }

        IEnumerator ShotsScenario()
        {
            var stage = LobbyStage.Instance;
            Log($"stage present={stage != null} characters={(stage ? stage.CharacterCount : -1)}");
            yield return new WaitForSeconds(2f);
            Shot("solo");
            stage.DebugShow(4);
            yield return new WaitForSeconds(2.5f);
            Shot("four");
            stage.DebugShow(5);
            yield return new WaitForSeconds(2.5f);
            Shot("five");
            stage.Customizing = true;
            stage.SetLocalAppearance(PlayerAppearance.Random(new System.Random(3)));
            yield return new WaitForSeconds(3f);
            Shot("customize");
            Log("DONE shots");
            Quit(0);
        }

        // ------------------------------------------------------------------ two processes over the real Steam transport

        static string CodeFile => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_lobby_code.txt");

        IEnumerator SteamHostScenario()
        {
            var net = NetSession.Instance;
            if (System.IO.File.Exists(CodeFile)) System.IO.File.Delete(CodeFile);
            net.HostSteam(LobbyVisibility.CodeOnly);
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 40, "steam host lobby");
            System.IO.File.WriteAllText(CodeFile, net.LobbyCode);
            Log($"STEAMHOST lobby code={net.LobbyCode}");
            var nm = Unity.Netcode.NetworkManager.Singleton;
            yield return WaitFor(() => nm.ConnectedClientsIds.Count >= 2, 90, "a Steam client over the transport");
            Log($"STEAMHOST connected clients={nm.ConnectedClientsIds.Count} roster={net.Roster.Count}");
            yield return new WaitForSeconds(4f);
            Log("DONE steamhost");
            Quit(0);
        }

        IEnumerator SteamClientScenario()
        {
            var net = NetSession.Instance;
            string code = null;
            yield return WaitFor(() =>
            {
                try { if (System.IO.File.Exists(CodeFile)) code = System.IO.File.ReadAllText(CodeFile).Trim(); }
                catch (System.IO.IOException) { /* host is still writing it */ }
                return !string.IsNullOrEmpty(code) && code.Length == NetConst.CodeLength;
            }, 120, "lobby code file");
            Log($"STEAMCLIENT joining code {code}");
            net.JoinByCode(code);
            yield return WaitFor(() => net.State == NetSessionState.InLobby || (net.State == NetSessionState.Offline && net.Status != "" && net.Status != null), 60, "join result");
            Log($"STEAMCLIENT state={net.State} status='{net.Status}' roster={net.Roster.Count}");
            yield return new WaitForSeconds(5f);
            Log($"STEAMCLIENT after 5s state={net.State} netConnected={Unity.Netcode.NetworkManager.Singleton.IsConnectedClient}");
            Log("DONE steamclient");
            Quit(0);
        }

        // ------------------------------------------------------------------ two-process local run

        IEnumerator HostScenario()
        {
            var net = NetSession.Instance;
            if (mode == "sockhost") net.DevHostSteamSockets(); else net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            Log($"hosting: roster={net.Roster.Count}");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client to join");
            Log($"roster={net.Roster.Count} canStart={net.CanStart} stageCharacters={(LobbyStage.Instance ? LobbyStage.Instance.CharacterCount : -1)}");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return WaitFor(() => HeistManager.Instance != null && HeistManager.Instance.Phase != HeistPhase.Lobby, 60, "floor loaded");
            yield return new WaitForSeconds(6f);
            Report("HOST");
            // a 5th/late joiner check is covered by JoinPolicy; now leave so the client sees the host go
            yield return new WaitForSeconds(6f);
            Log("DONE host");
            Quit(0);
        }

        IEnumerator ClientScenario()
        {
            var net = NetSession.Instance;
            float deadline = Time.realtimeSinceStartup + 150f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                if (mode == "sockclient") net.DevJoinSteamSockets(); else net.JoinLocal("127.0.0.1");
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) { yield return new WaitForSeconds(2f); }
            }
            Log($"client state={net.State} roster={net.Roster.Count} status='{net.Status}'");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == NetConst.RunScene, 120, "run scene");
            yield return WaitFor(() => HeistManager.Instance != null && HeistManager.Instance.Phase != HeistPhase.Lobby, 60, "floor loaded");
            yield return new WaitForSeconds(6f);
            Report("CLIENT");
            // host quits shortly after: we should be sent back to the menu with a reason
            yield return WaitFor(() => net.State == NetSessionState.Offline && SceneManager.GetActiveScene().name == NetConst.MenuScene, 60, "host-left return to menu");
            Log($"after host left: state={net.State} scene={SceneManager.GetActiveScene().name} status='{net.Status}'");
            Log("DONE client");
            Quit(0);
        }

        void Report(string who)
        {
            var info = TowerManager.Instance.CurrentInfo;
            Log($"{who} floor: {info.cols}x{info.rows} quota={info.quota} lootValue={info.lootValue} guards={info.guards} spawn={info.spawn}");
            Log($"{who} netPlayers={NetPlayer.All.Count} registry={WobblyPlayerRegistry.All.Count} phase={HeistManager.Instance.Phase}");
            foreach (var p in NetPlayer.All)
            {
                var rb = p.GetComponent<Rigidbody>();
                var smr = p.GetComponentInChildren<SkinnedMeshRenderer>();
                var body = smr && smr.sharedMaterials.Length > 0 ? (Color32)smr.sharedMaterials[0].color : default;
                bool hat = p.GetComponentInChildren<Transform>() && p.transform.Find("Model_Player/Armature/Hips/Spine/Spine1/Spine2/Head/customize_objects") is Transform a && a.childCount > 0;
                Log($"{who}   slot={p.Slot} owner={p.IsOwner} kinematic={rb.isKinematic} wobblyEnabled={p.GetComponent<WobblyPlayer>().enabled} pos={p.transform.position} body=#{body.r:X2}{body.g:X2}{body.b:X2} hat={hat}");
            }
            var cam = FindFirstObjectByType<CameraFollow>();
            var np = cam && cam.target ? cam.target.GetComponent<NetPlayer>() : null;
            Log($"{who} camera follows own player={(np != null && np.IsOwner)}");
            int offline = 0;
            foreach (var w in FindObjectsByType<WobblyPlayer>(FindObjectsSortMode.None)) if (!w.GetComponent<NetPlayer>()) offline++;
            Log($"{who} leftover offline players={offline} (expected 0)");
        }
    }
}
