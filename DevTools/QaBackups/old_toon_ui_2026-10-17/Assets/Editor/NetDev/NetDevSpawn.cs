using System.Collections;
using System.Collections.Generic;
using System.Text;
using BuildingEscape.Net;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// "Where do the players stand when a run starts?" One process per role (host, c1, c2, c3, c4), the same script in each.
    /// Every machine watches every player from the moment the player objects exist until the first floor has been ready
    /// for a while, and reports how far anyone fell, whether anyone was ever far from the elevator once the floor was ready,
    /// and whether every machine saw the same players in the same places.
    /// Environment: BE_PLAYERS (how many, default 2), BE_RUN (a number that keeps the signal files of two runs apart),
    /// BE_SPAWN_DELAY (seconds a role holds back before building its floor, to imitate a slow machine: "role=seconds,...").
    /// </summary>
    public partial class NetDevRunner
    {
        string spRole, spPrefix;
        int spPass, spFail;

        void SpCheck(string name, bool ok, string detail = null)
        {
            if (ok) spPass++; else spFail++;
            Log($"[Spawn] {(ok ? "PASS" : "FAIL")} {spRole}: {name}" + (string.IsNullOrEmpty(detail) ? "" : $"   ({detail})"));
        }

        static string SpEnv(string name, string fallback) => System.Environment.GetEnvironmentVariable(name) ?? fallback;

        IEnumerator SpawnScenario(string role)
        {
            spRole = role;
            spPrefix = "spawn" + SpEnv("BE_RUN", "0") + "_";
            int players = int.Parse(SpEnv("BE_PLAYERS", "2"));
            bool host = role == "host";
            var net = NetSession.Instance;
            System.Func<string, string> sig = s => spPrefix + s;

            string transport = SpEnv("BE_TRANSPORT", "local");   // local (UnityTransport on loopback) or sock (the real Steam transport code over loopback)
            string sim = SpEnv("BE_SIM", "");                    // "delay,jitter,drop%": a bad network for the local transport
            if (sim != "" && transport == "local")
            {
                var s = sim.Split(',');
                net.GetComponent<UnityTransport>().SetDebugSimulatorParameters(int.Parse(s[0]), int.Parse(s[1]), int.Parse(s[2]));
                Log($"[Spawn] {role}: simulated network: {s[0]} ms delay, {s[1]} ms jitter, {s[2]} % loss");
            }
            if (host)
            {
                if (transport == "sock") net.DevHostSteamSockets(); else net.HostLocal();
                yield return WaitFor(() => net.State == NetSessionState.InLobby, 30, "host lobby");
                Signal(sig("lobby_up"));
                yield return WaitFor(() => net.Roster.Count >= players && net.CanStart, 240, players + " players in the lobby");
                net.StartRun();
                yield return WaitFor(() => net.State == NetSessionState.InRun, 90, "run start");
            }
            else
            {
                yield return WaitFor(() => ReadSig(sig("lobby_up")) != null, 240, "the host's lobby");
                if (transport == "sock")
                {
                    float until = Time.realtimeSinceStartup + 150f;
                    while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < until)
                    {
                        net.DevJoinSteamSockets();
                        float w = 0;
                        while (net.State == NetSessionState.Connecting && w < 10f) { w += Time.unscaledDeltaTime; yield return null; }
                        if (net.State != NetSessionState.InLobby) { if (net.State != NetSessionState.Offline) net.Leave(); yield return new WaitForSeconds(2f); }
                    }
                }
                else yield return JoinLoop(net);
                SpCheck("joined the lobby", net.State == NetSessionState.InLobby);
                yield return WaitFor(() => net.State == NetSessionState.InRun || NetPlayer.All.Count > 0, 240, "the run");
            }

            // ---- watch every player from the moment the first one exists
            float t0 = Time.realtimeSinceStartup, firstPlayerAt = -1f, floorReadyAt = -1f;
            var minY = new Dictionary<int, float>();
            var lowestWhileWaiting = float.MaxValue;
            var farAfterReady = new List<string>();
            var trace = new StringBuilder();
            var hm = HeistManager.Instance;
            float nextTrace = 0f, nextFine = 0f;
            var fine = new StringBuilder();
            bool floorWasReady = false, probed = false;
            Vector3 spawn = default;
            float watchUntil = float.MaxValue;
            while (Time.realtimeSinceStartup < watchUntil && Time.realtimeSinceStartup - t0 < 180f)
            {
                var tm = TowerManager.Instance;
                bool ready = tm != null && !tm.Loading && HeistManager.Instance != null && HeistManager.Instance.Phase != HeistPhase.Lobby && HeistManager.Instance.Floor == 0;
                if (NetPlayer.All.Count > 0 && firstPlayerAt < 0) firstPlayerAt = Time.realtimeSinceStartup;
                if (ready && !floorWasReady)
                {
                    floorWasReady = true;
                    floorReadyAt = Time.realtimeSinceStartup;
                    spawn = tm.CurrentInfo.spawn;
                    watchUntil = floorReadyAt + 8f;
                    Log($"[Spawn] {role}: floor ready {(firstPlayerAt < 0 ? -1 : floorReadyAt - firstPlayerAt):0.0} s after the first player appeared; elevator spawn {spawn}");
                    Log($"[Spawn] {role}: registry={WobblyPlayerRegistry.All.Count} ordered={NetPlayer.Ordered().Count} netplayers={NetPlayer.All.Count}");
                    foreach (var dp in NetPlayer.All)
                        Log($"[Spawn] {role}: player slot {dp.Slot} owner={dp.IsOwner} spawned={dp.IsSpawned} kinematic={dp.GetComponent<Rigidbody>().isKinematic} at {dp.transform.position} netTransform={(dp.GetComponent<Unity.Netcode.Components.NetworkTransform>() != null)} wobblyEnabled={dp.GetComponent<WobblyPlayer>().enabled}");
                }
                if (floorWasReady && !probed && Time.realtimeSinceStartup - floorReadyAt > 1.5f)
                {
                    probed = true;
                    Log($"[Spawn] {role}: physics: timeScale={Time.timeScale} gravity={Physics.gravity} simulationMode={Physics.simulationMode} autoSync={Physics.autoSyncTransforms}");
                    var me = Mine;
                    if (me != null)
                    {
                        var rbm = me.GetComponent<Rigidbody>();
                        Log($"[Spawn] {role}: my body at {me.transform.position} vel={rbm.linearVelocity} useGravity={rbm.useGravity} layer={LayerMask.LayerToName(me.gameObject.layer)} constraints={rbm.constraints} collisionDetection={rbm.collisionDetectionMode}");
                    }
                    foreach (var pt in new[] { spawn, spawn + new Vector3(3f, 0f, 0f), spawn + new Vector3(0f, 0f, 6f), new Vector3(2f, 0f, 2f) })
                    {
                        if (Physics.Raycast(pt + Vector3.up * 5f, Vector3.down, out var rh, 30f, ~0, QueryTriggerInteraction.Ignore))
                            Log($"[Spawn] {role}: ground under {pt}: {rh.collider.name} (parent {(rh.collider.transform.parent ? rh.collider.transform.parent.name : "-")}) top at y={rh.point.y:0.00} layer={LayerMask.LayerToName(rh.collider.gameObject.layer)}");
                        else Log($"[Spawn] {role}: NOTHING to stand on under {pt}");
                    }
                    var root = FindFirstObjectByType<FloorGenerator>()?.Current;
                    Log($"[Spawn] {role}: floor root {(root ? root.name : "none")} children={(root ? root.childCount : 0)} colliders={(root ? root.GetComponentsInChildren<Collider>().Length : 0)}");
                }
                foreach (var p in NetPlayer.All)
                {
                    int slot = p.Slot;
                    float y = p.transform.position.y;
                    if (!minY.TryGetValue(slot, out var m) || y < m) minY[slot] = y;
                    if (!floorWasReady && p.IsOwner && y < lowestWhileWaiting) lowestWhileWaiting = y;
                    if (floorWasReady && Time.realtimeSinceStartup - floorReadyAt > 4f)
                    {
                        var d = p.transform.position - spawn; d.y = 0f;
                        if (d.magnitude > 8f || p.transform.position.y < spawn.y - 1f || p.transform.position.y > spawn.y + 4f)
                            farAfterReady.Add($"slot {p.Slot} at {p.transform.position}");
                    }
                }
                if (Mine != null && Time.realtimeSinceStartup >= nextFine)
                {
                    nextFine = Time.realtimeSinceStartup + 0.1f;
                    var mb = Mine; var mrb = mb.GetComponent<Rigidbody>();
                    fine.AppendLine($"t={Time.realtimeSinceStartup - t0:0.00} frame={Time.frameCount} fixed={Time.fixedTime:0.00} tr=({mb.transform.position.x:0.00},{mb.transform.position.y:0.00},{mb.transform.position.z:0.00}) rb=({mrb.position.x:0.00},{mrb.position.y:0.00},{mrb.position.z:0.00}) v=({mrb.linearVelocity.x:0.0},{mrb.linearVelocity.y:0.0},{mrb.linearVelocity.z:0.0}) kin={mrb.isKinematic} loading={TowerManager.Instance?.Loading} floor={HeistManager.Instance?.Floor} phase={HeistManager.Instance?.Phase}{(floorWasReady ? " READY" : "")}");
                }
                if (Time.realtimeSinceStartup >= nextTrace && NetPlayer.All.Count > 0)
                {
                    nextTrace = Time.realtimeSinceStartup + 1f;
                    trace.Append($"t={Time.realtimeSinceStartup - t0:0.0}");
                    foreach (var p in NetPlayer.All) trace.Append($" [s{p.Slot} y={p.transform.position.y:0.0}{(p.IsOwner ? "*" : "")}]");
                    trace.AppendLine(floorWasReady ? " ready" : "");
                }
                yield return null;
            }
            Log($"[Spawn] {role}: trace\n{trace}");
            Log($"[Spawn] {role}: fine trace of my body\n{fine}");
            SpCheck("the player objects appeared", firstPlayerAt >= 0);
            SpCheck("the first floor became ready", floorWasReady, floorWasReady ? "" : $"loading={TowerManager.Instance?.Loading}");
            if (floorWasReady)
            {
                SpCheck("all players are here", NetPlayer.All.Count == players, $"{NetPlayer.All.Count}/{players}");
                Log($"[Spawn] {role}: my player fell to y={lowestWhileWaiting:0.0} before the floor was ready; lowest y per slot after spawning: " + Dump(minY));
                var mineNow = Mine;
                SpCheck("my player is on the floor next to the elevator", mineNow != null && Vector3.Distance(new Vector3(mineNow.transform.position.x, 0, mineNow.transform.position.z), new Vector3(spawn.x, 0, spawn.z)) < 8f
                    && mineNow.transform.position.y > spawn.y - 1f, mineNow != null ? mineNow.transform.position.ToString() : "no player");
                SpCheck("nobody was ever far from the elevator once the floor was ready", farAfterReady.Count == 0, string.Join("; ", farAfterReady.GetRange(0, Mathf.Min(3, farAfterReady.Count))));
                SpCheck("nobody dropped out of the world", AllAbove(minY, -60f), Dump(minY));
            }

            // everybody has finished watching before anybody throws a body into the abyss (the others would see it fall)
            string[] allRoles = { "host", "c1", "c2", "c3", "c4" };
            Signal(sig("watched_" + role));
            for (int i = 0; i < players; i++) yield return WaitFor(() => ReadSig(sig("watched_" + allRoles[i])) != null, 180, "everybody finished watching");

            // ---- a body that ends up far below the map is put back in the elevator
            var lostBody = Mine != null ? Mine.GetComponent<WobblyPlayer>() : null;
            if (lostBody != null)
            {
                lostBody.TeleportTo(new Vector3(40f, -120f, 40f), Quaternion.identity);
                float rescueDeadline = Time.realtimeSinceStartup + 8f;
                while (lostBody.transform.position.y < -2f && Time.realtimeSinceStartup < rescueDeadline) yield return null;
                yield return new WaitForSeconds(0.5f);
                SpCheck("a player who falls out of the world is put back in the elevator", lostBody.transform.position.y > -1f && lostBody.transform.position.y < 3f,
                    $"at {lostBody.transform.position}");
            }

            // ---- optional: five players walking about on floor 1 for a while; what did it cost in network traffic?
            if (SpEnv("BE_SOAK", "0") == "1" && floorWasReady)
            {
                var tmSoak = TowerManager.Instance;
                var hmSoak = HeistManager.Instance;
                if (host) { hmSoak.godMode = true; hmSoak.freezeTimer = true; Signal(sig("soak_go")); tmSoak.GoToLevel(1); }
                yield return WaitFor(() => ReadSig(sig("soak_go")) != null, 120, "soak start");
                yield return new WaitUntil(() => tmSoak.Loading || hmSoak.Floor == 1);
                yield return FloorReady(1, 150);
                yield return new WaitForSeconds(3f);
                var tr = net.SteamTransport;
                long s0 = tr != null ? tr.BytesSent : 0, r0 = tr != null ? tr.BytesReceived : 0, m0 = tr != null ? tr.MessagesSent + tr.MessagesReceived : 0;
                float soakStart = Time.realtimeSinceStartup, nextTurn = 0f;
                var rng = new System.Random(role.GetHashCode());
                var body = Mine != null ? Mine.GetComponent<WobblyPlayer>() : null;
                int frames = 0; float worst = 0f;
                while (Time.realtimeSinceStartup - soakStart < 20f)
                {
                    if (body != null && Time.realtimeSinceStartup >= nextTurn)
                    {
                        nextTurn = Time.realtimeSinceStartup + 1.5f;
                        body.testMove = new Vector3((float)rng.NextDouble() * 2f - 1f, 0f, (float)rng.NextDouble() * 2f - 1f);
                        body.testSprint = rng.Next(3) == 0;
                    }
                    frames++; worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                    yield return null;
                }
                if (body != null) { body.testMove = null; body.testSprint = false; }
                float secs = Time.realtimeSinceStartup - soakStart;
                if (tr != null)
                    Log($"[Spawn] {role}: TRAFFIC over {secs:0} s with {players} players on floor 1 ({GuardAI.All.Count} guards): sent {(tr.BytesSent - s0) / secs / 1024f:0.0} KB/s, received {(tr.BytesReceived - r0) / secs / 1024f:0.0} KB/s, {(tr.MessagesSent + tr.MessagesReceived - m0) / secs:0} messages/s, {tr.SendFailures} failed sends");
                Log($"[Spawn] {role}: frame time over the soak: {secs / frames * 1000f:0.0} ms average, worst {worst * 1000f:0} ms ({frames} frames)");
                SpCheck("nobody fell out of the world while walking about", Mine != null && Mine.transform.position.y > -5f, Mine != null ? Mine.transform.position.ToString() : "no player");
                if (tr != null) SpCheck("the Steam transport sent every message it was given", tr.SendFailures == 0, $"{tr.SendFailures} failed");
            }

            // ---- do all machines agree on where everyone is?
            var sb = new StringBuilder();
            foreach (var p in NetPlayer.All) sb.AppendLine($"{p.Slot} {p.transform.position.x:0.00} {p.transform.position.y:0.00} {p.transform.position.z:0.00}");
            Signal(sig("pos_" + role), sb.ToString());
            string[] roles = { "host", "c1", "c2", "c3", "c4" };
            for (int i = 0; i < players; i++) yield return WaitFor(() => ReadSig(sig("pos_" + roles[i])) != null, 120, "positions of " + roles[i]);
            if (host)
            {
                int agree = 0, total = 0;
                var mine = Parse(sb.ToString());
                for (int i = 1; i < players; i++)
                    foreach (var kv in Parse(ReadSig(sig("pos_" + roles[i]))))
                    {
                        total++;
                        if (mine.TryGetValue(kv.Key, out var hp) && Vector3.Distance(hp, kv.Value) < 1.5f) agree++;
                        else Log($"[Spawn] host: slot {kv.Key} is at {(mine.TryGetValue(kv.Key, out var h2) ? h2.ToString() : "?")} here but at {kv.Value} on {roles[i]}");
                    }
                SpCheck("every client sees every player where the host does", agree == total && total > 0, $"{agree}/{total}");
            }
            Signal(sig("done_" + role));
            for (int i = 0; i < players; i++) yield return WaitFor(() => ReadSig(sig("done_" + roles[i])) != null, 120, "everybody done");
            Log($"[Spawn] SUMMARY {role}: {spPass} passed, {spFail} failed");
            Quit(spFail == 0 ? 0 : 3);
        }

        static string Dump(Dictionary<int, float> d)
        {
            var sb = new StringBuilder();
            foreach (var kv in d) sb.Append($"s{kv.Key}={kv.Value:0.0} ");
            return sb.ToString();
        }

        static bool AllAbove(Dictionary<int, float> d, float y)
        {
            foreach (var kv in d) if (kv.Value < y) return false;
            return true;
        }

        static Dictionary<int, Vector3> Parse(string text)
        {
            var d = new Dictionary<int, Vector3>();
            foreach (var line in (text ?? "").Split('\n'))
            {
                var p = line.Trim().Split(' ');
                if (p.Length != 4) continue;
                var c = System.Globalization.CultureInfo.InvariantCulture;
                d[int.Parse(p[0])] = new Vector3(float.Parse(p[1], c), float.Parse(p[2], c), float.Parse(p[3], c));
            }
            return d;
        }
    }
}
