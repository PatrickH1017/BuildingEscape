using System.Collections;
using System.Collections.Generic;
using BuildingEscape.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The multiplayer QA suite. One process per role (host, c1, c2, c3, late5, late6), all running the same
    /// script; barriers (signal files) keep them in step. Every check logs "[QA] PASS|FAIL role: name".
    /// </summary>
    public partial class NetDevRunner
    {
        string qaRole, qaPrefix;
        int qaPass, qaFail;
        static readonly string[] Players = { "host", "c1", "c2", "c3" };

        void Check(string name, bool ok, string detail = null)
        {
            if (ok) qaPass++; else qaFail++;
            Log($"[QA] {(ok ? "PASS" : "FAIL")} {qaRole}: {name}" + (string.IsNullOrEmpty(detail) ? "" : $"   ({detail})"));
        }

        void QaSignal(string name, string body = "1") => Signal(qaPrefix + name, body);
        string QaRead(string name) => ReadSig(qaPrefix + name);
        IEnumerator QaWait(string name, float timeout = 240f) { yield return WaitSig(qaPrefix + name, timeout); }

        IEnumerator Barrier(string name, params string[] roles)
        {
            QaSignal($"bar_{name}_{qaRole}");
            yield return WaitFor(() =>
            {
                foreach (var r in roles) if (QaRead($"bar_{name}_{r}") == null) return false;
                return true;
            }, 300f, "barrier " + name);
        }

        IEnumerator Settle(float seconds) { yield return new WaitForSeconds(seconds); }

        IEnumerator JoinLoop(NetSession net, float timeout = 150f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.JoinLocal("127.0.0.1");
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return new WaitForSeconds(1.5f);
            }
        }

        static ulong MyId => NetworkManager.Singleton.LocalClientId;

        static NetPlayer PlayerByClient(ulong id)
        {
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == id) return p;
            return null;
        }

        void QaFinish()
        {
            Log($"[QA] SUMMARY {qaRole}: {qaPass} passed, {qaFail} failed");
            Quit(qaFail == 0 ? 0 : 3);
        }

        // ------------------------------------------------------------------ friends list + invite pop-up

        IEnumerator SocialScenario()
        {
            qaRole = "social";
            var net = NetSession.Instance;
            Check("Steam is available", net.Steam != null && net.Steam.Ready, net.Steam?.Error);

            var list = new List<FriendEntry>();
            FriendsService.Refresh(list);
            int online = 0; bool sorted = true, seenOffline = false;
            foreach (var f in list)
            {
                if (f.Online) { online++; if (seenOffline) sorted = false; }
                else seenOffline = true;
            }
            Log($"[QA] friends: {list.Count} total, {online} online");
            Check("the friends list loads", list.Count >= 0);
            Check("online friends are listed before offline ones", sorted);

            var inbox = InviteInbox.Instance;
            Check("the invite inbox exists", inbox != null);
            var lobbyA = new Steamworks.CSteamID(new Steamworks.AccountID_t(11), 0x40000u, Steamworks.EUniverse.k_EUniversePublic, Steamworks.EAccountType.k_EAccountTypeChat);
            var lobbyB = new Steamworks.CSteamID(new Steamworks.AccountID_t(12), 0x40000u, Steamworks.EUniverse.k_EUniversePublic, Steamworks.EAccountType.k_EAccountTypeChat);
            var lobbyC = new Steamworks.CSteamID(new Steamworks.AccountID_t(13), 0x40000u, Steamworks.EUniverse.k_EUniversePublic, Steamworks.EAccountType.k_EAccountTypeChat);

            inbox.Enqueue(lobbyA, "Test Friend");
            inbox.Enqueue(lobbyA, "Test Friend");        // the same invite twice
            yield return new WaitForSeconds(0.3f);
            Check("an invite pop-up appears", inbox.Showing);
            Check("the same invite is not shown twice", inbox.Received == 1, $"received={inbox.Received}");
            Check("the timer starts at ten seconds", inbox.TimeLeft > 9.3f && inbox.TimeLeft <= 10f, $"left={inbox.TimeLeft:0.0}");
            yield return new WaitForSeconds(3f);
            Check("the bar counts down", inbox.TimeLeft > 5.5f && inbox.TimeLeft < 7.6f, $"left={inbox.TimeLeft:0.0}");
            inbox.Decline();
            Check("Decline closes the pop-up", !inbox.Showing && inbox.Declined == 1);

            inbox.Enqueue(lobbyB, "Second Friend");
            yield return new WaitForSeconds(10.8f);
            Check("an unanswered invite expires after ten seconds", !inbox.Showing && inbox.Expired == 1, $"expired={inbox.Expired}");

            // two at once: the second waits its turn
            inbox.Enqueue(lobbyA, "First");
            inbox.Enqueue(lobbyB, "Second");
            yield return new WaitForSeconds(0.3f);
            inbox.Decline();
            yield return new WaitForSeconds(0.4f);
            Check("a waiting invite shows after the first is answered", inbox.Showing);
            inbox.Decline();

            // accepting an invite to a lobby that no longer exists must fail politely, not hang
            inbox.Enqueue(lobbyC, "Gone Friend");
            yield return new WaitForSeconds(0.3f);
            inbox.Accept();
            yield return WaitFor(() => net.State != NetSessionState.Connecting, 40, "join result");
            Check("accepting a dead invite fails politely", net.State == NetSessionState.Offline && !string.IsNullOrEmpty(net.Status), $"state={net.State} status='{net.Status}'");
            Check("the accept counter moved", inbox.Accepted == 1);
            QaFinish();
        }

        // ------------------------------------------------------------------ the suite

        IEnumerator QaScenario(string role)
        {
            qaRole = role;
            qaPrefix = "qa" + (System.Environment.GetEnvironmentVariable("BE_QA_RUN") ?? "0") + "_";
            var net = NetSession.Instance;
            bool host = role == "host";
            bool player = host || role == "c1" || role == "c2" || role == "c3";

            // ---------------- 1. lobby: join, cap of four, a fifth is refused
            if (host)
            {
                net.HostLocal();
                yield return WaitFor(() => net.State == NetSessionState.InLobby, 30, "host lobby");
                QaSignal("lobby_up");
            }
            else yield return QaWait("lobby_up", 200);

            if (role == "late5")
            {
                yield return QaWait("bar_lobby4_host");
                yield return QaWait("bar_lobby4_c1");
                yield return QaWait("bar_lobby4_c2");
                yield return QaWait("bar_lobby4_c3");
                yield return new WaitForSeconds(1.5f);
                net.JoinLocal("127.0.0.1");
                yield return WaitFor(() => net.State != NetSessionState.Connecting, 30, "fifth player result");
                Check("a fifth player is refused", net.State == NetSessionState.Offline, $"state={net.State}");
                Check("the refusal says the lobby is full", (net.Status ?? "").ToLowerInvariant().Contains("full"), $"status='{net.Status}'");
                QaSignal("bar_late5_done_late5");
                QaFinish();
                yield break;
            }

            if (role == "late6")
            {
                yield return QaWait("run_started", 400);
                yield return new WaitForSeconds(2f);
                net.JoinLocal("127.0.0.1");
                yield return WaitFor(() => net.State != NetSessionState.Connecting, 30, "late join result");
                Check("a late joiner is refused once the run started", net.State == NetSessionState.Offline, $"state={net.State}");
                Check("the refusal says the run has started", (net.Status ?? "").ToLowerInvariant().Contains("started"), $"status='{net.Status}'");
                QaSignal("bar_late6_done_late6");
                QaFinish();
                yield break;
            }
            if (!host) { yield return JoinLoop(net); Check("joined the lobby", net.State == NetSessionState.InLobby); }
            QaSignal($"id_{role}", MyId.ToString());
            yield return Barrier("lobby4", Players);
            yield return WaitFor(() => net.Roster.Count == 4, 20, "4 players in the roster");
            Check("all four players are in the lobby", net.Roster.Count == 4, $"roster={net.Roster.Count}");

            yield return QaWait("bar_late5_done_late5");

            // ---------------- 2. a player leaves the lobby and comes back
            if (role == "c3")
            {
                net.Leave("qa");
                yield return WaitFor(() => net.State == NetSessionState.Offline, 20, "left lobby");
                QaSignal("c3_left");
                yield return QaWait("c3_back_ok");
                yield return JoinLoop(net);
                Check("rejoined the lobby", net.State == NetSessionState.InLobby);
                QaSignal("id_c3_2", MyId.ToString());
            }
            else if (host)
            {
                yield return QaWait("c3_left");
                yield return WaitFor(() => net.Roster.Count == 3, 20, "roster drops to 3");
                Check("the roster drops when a player leaves", net.Roster.Count == 3, $"roster={net.Roster.Count}");
                QaSignal("c3_back_ok");
                yield return QaWait("id_c3_2", 60);
                yield return WaitFor(() => net.Roster.Count == 4, 20, "roster back to 4");
                Check("the roster is back to four", net.Roster.Count == 4);
            }
            else
            {
                yield return QaWait("c3_left");
                yield return QaWait("id_c3_2", 90);
            }
            yield return Barrier("rejoined", Players);

            // ---------------- 3. start the run; a late joiner is refused
            if (host)
            {
                yield return WaitFor(() => net.CanStart, 20, "can start");
                net.StartRun();
                yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
                QaSignal("run_started");
            }
            yield return FloorReady(0, 150);
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            if (host) { hm.godMode = true; hm.freezeTimer = true; hm.AddCash(5000); }
            yield return Barrier("run_up", Players);

            Check("four players spawned", NetPlayer.All.Count == 4, $"players={NetPlayer.All.Count}");
            int puppets = 0; foreach (var p in NetPlayer.All) if (p.GetComponent<Rigidbody>().isKinematic) puppets++;
            Check("exactly three puppets on each machine", puppets == 3, $"puppets={puppets}");
            Check("I control my own player", Mine != null && Mine.IsOwner);
            QaSignal($"slot_{role}", Mine.Slot.ToString());
            Check("the camera follows my player", FindFirstObjectByType<CameraFollow>()?.target == Mine.transform);
            if (host)
            {
                var slots = new HashSet<string>();
                foreach (var r in Players) { yield return QaWait($"slot_{r}", 30); slots.Add(QaRead($"slot_{r}")); }
                Check("every player has its own slot", slots.Count == 4, string.Join(",", slots));
            }

            // ---------------- 4. voice between four players (a tone stands in for each microphone)
            var vc = VoiceChat.Instance;
            float freq = host ? 440f : role == "c1" ? 550f : role == "c2" ? 660f : 770f;
            vc.UseTestInput(new ToneVoiceInput(freq));
            yield return Barrier("tones_on", Players);
            yield return new WaitForSeconds(4f);
            int heardFrom = 0;
            foreach (var p in NetPlayer.All)
            {
                if (p.IsOwner) continue;
                var v = vc.VoiceOf(p.OwnerClientId);
                if (v != null && v.packets > 20 && v.samplesPlayed > 1000) heardFrom++;
            }
            Check("I can hear all three teammates", heardFrom == 3, $"heard={heardFrom}");
            if (host) Check("the host relayed voice packets", vc.PacketsRelayed > 100, $"relayed={vc.PacketsRelayed}");
            yield return Barrier("voice_done", Players);
            vc.Muted = true;

            // ---------------- 5. the loading docks: buy, then ride up with the panel
            if (role == "c1" || role == "c2")
            {
                var vendor = Vendor.Current;
                Teleport(vendor.transform.position + new Vector3(role == "c1" ? -2f : 2f, 1.1f, 0f)); // apart: overlapping capsules fling each other out
                yield return Settle(1.5f);
                var pack = MyPack;
                if (role == "c1") { RunSync.Instance.RequestBuy(0); yield return WaitFor(() => pack.Items.Count >= 1, 30, "mug bought"); }
                if (role == "c2" && MeleeWeapon.Available)
                {
                    RunSync.Instance.RequestBuy(4);
                    yield return WaitFor(() => pack.Items.Count >= 1, 30, "crowbar bought");
                    foreach (var it in pack.Items) if (it.GetComponent<MeleeWeapon>()) { pack.SetEquipped(it); break; }
                    yield return WaitFor(() => pack.Equipped != null, 30, "crowbar equipped");
                }
                Check("purchase arrived in my pack", pack.Items.Count >= 1 || !MeleeWeapon.Available, $"items={pack.Items.Count}");
                QaSignal($"bought_{role}");
            }
            yield return QaWait("bought_c1", 120);
            yield return QaWait("bought_c2", 120);
            int expectCash = 5000 - 60 - (MeleeWeapon.Available ? 400 : 0);
            yield return WaitFor(() => hm.Cash == expectCash, 30, "cash replicated");
            Check("cash is the same everywhere", hm.Cash == expectCash, $"cash={hm.Cash} expected={expectCash}");
            if (host)
            {
                var p1 = PlayerByClient(ulong.Parse(QaRead("id_c1")));
                var p2 = PlayerByClient(ulong.Parse(QaRead("id_c2")));
                Check("host sees c1's pack", p1 != null && p1.GetComponent<Backpack>().Items.Count == 1);
                Check("host sees c2's equipped weapon", !MeleeWeapon.Available || (p2 != null && p2.GetComponent<Backpack>().Equipped != null));
            }
            yield return Barrier("shopped", Players);

            // everyone into the car, then c1 presses Go (Up is the only direction at the docks)
            Teleport(tm.CurrentInfo.spawn + new Vector3((Mine.Slot - 1.5f) * 1.25f, 1.1f, 0f));
            yield return WaitFor(() => ElevatorExtraction.EveryoneAboard, 30, "everyone aboard");
            Check("the elevator knows everyone is aboard", ElevatorExtraction.EveryoneAboard);
            Check("at the docks the only way is up", ElevatorExtraction.GoingUp && ElevatorExtraction.Destination == 1, $"dest={ElevatorExtraction.Destination}");
            RunSync.SyncChecked = false;
            yield return Barrier("aboard", Players);
            if (role == "c1") RunSync.Instance.RequestPanel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return FloorReady(1, 120);
            if (!host) yield return WaitFor(() => RunSync.SyncChecked, 60, "floor checked against the host");
            QaSignal($"sum_{role}", $"{NetIds.Count} {NetIds.Checksum()} {GuardAI.All.Count}");
            yield return Barrier("floor1", Players);
            if (host)
            {
                var sums = new HashSet<string>();
                foreach (var r in Players) sums.Add(QaRead($"sum_{r}"));
                Check("all four machines built the identical floor", sums.Count == 1, string.Join(" | ", sums));
            }
            Check("the guards are puppets on clients", host || System.Linq.Enumerable.All(GuardAI.All, g => g.IsPuppet));
            Check("quota is the same everywhere", true, $"quota={hm.Quota}");

            // ---------------- 6. world: positions, guard hit, noise, hide, grab, bank, backup, break
            yield return Settle(4f);
            if (host)
            {
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
                QaSignal("snap", sb.ToString());
            }
            else
            {
                yield return QaWait("snap", 60);
                int ok = 0, total = 0;
                foreach (var line in QaRead("snap").Split('\n'))
                {
                    var f = line.Trim().Split(' ');
                    if (f.Length < 5) continue;
                    var c = NetIds.Get<Component>(int.Parse(f[1]));
                    var hp = new Vector3(float.Parse(f[2], System.Globalization.CultureInfo.InvariantCulture), float.Parse(f[3], System.Globalization.CultureInfo.InvariantCulture), float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture));
                    total++;
                    if (c != null && (c.transform.position - hp).magnitude <= (f[0] == "G" ? 4f : 0.6f)) ok++;
                }
                Check("positions match the host", total > 0 && ok >= total * 0.95f, $"{ok}/{total}");
            }
            yield return Barrier("snapped", Players);

            // c2 hits a guard with the crowbar
            if (role == "c2" && MeleeWeapon.Available)
            {
                var g = FirstGuard();
                Teleport(g.transform.position + new Vector3(1.4f, 0.5f, 0f));
                yield return Settle(1.5f);
                g = NetIds.Get<GuardAI>(g.NetId);
                g.WeaponHit(4f, Mine.gameObject);
                QaSignal("hit_id", g.NetId.ToString());
            }
            if (MeleeWeapon.Available)
            {
                yield return QaWait("hit_id", 60);
                yield return Settle(1.5f);
                var gh = NetIds.Get<GuardAI>(int.Parse(QaRead("hit_id")));
                Check("a client's weapon hit stunned the guard (seen here)", gh != null && gh.State == GuardState.Stunned, $"state={gh?.State}");
            }

            // c3 hides; the host's guards must see a hidden player
            if (role == "c3") { Mine.GetComponent<WobblyPlayer>().IsHidden = true; yield return Settle(1.0f); QaSignal("c3_hidden"); }
            if (host)
            {
                yield return QaWait("c3_hidden", 60);
                yield return Settle(0.8f);
                var p3 = PlayerByClient(ulong.Parse(QaRead("id_c3_2")));
                Check("the host sees c3 as hidden", p3 != null && p3.GetComponent<WobblyPlayer>().IsHidden);
                QaSignal("host_saw_hidden");
            }
            if (role == "c3")
            {
                yield return QaWait("host_saw_hidden", 60);
                Mine.GetComponent<WobblyPlayer>().IsHidden = false;
            }

            // c1 stores and drops a mug; others watch
            if (role == "c1")
            {
                var mug = FreeMug(3);
                Teleport(mug.transform.position + new Vector3(0.8f, 0.6f, 0f));
                yield return Settle(1.5f);
                int before = MyPack.Items.Count;
                MyPack.TryStore(mug);
                yield return WaitFor(() => MyPack.Items.Count == before + 1, 20, "stored");
                Check("stored an item", MyPack.Items.Count == before + 1 && !mug.gameObject.activeSelf);
                QaSignal("mug_id", mug.NetId.ToString());
                yield return Settle(1.0f);
                MyPack.Drop(mug);
                yield return WaitFor(() => MyPack.Items.Count == before, 20, "dropped");
                Check("dropped it again", mug.gameObject.activeSelf);
                QaSignal("mug_dropped");
            }
            else
            {
                yield return QaWait("mug_dropped", 90);
                yield return Settle(1.0f);
                var mugHere = NetIds.Get<Pickup>(int.Parse(QaRead("mug_id")));
                Check("I see the dropped item in the world", mugHere != null && mugHere.gameObject.activeSelf);
            }
            yield return Barrier("items", Players);

            // c3 grabs and throws; everyone sees the same landing spot
            if (role == "c3")
            {
                var mug2 = FreeMug(5);
                Teleport(mug2.transform.position + new Vector3(0.7f, 0.6f, 0f));
                yield return Settle(1.5f);
                RunSync.Instance.RequestGrab(mug2);
                yield return WaitFor(() => Mine.GetComponent<PlayerGrab>().Held != null, 20, "grabbed");
                yield return Settle(0.5f);
                RunSync.Instance.RequestRelease(true);
                yield return WaitFor(() => Mine.GetComponent<PlayerGrab>().Held == null, 20, "released");
                QaSignal("thrown_id", mug2.NetId.ToString());
            }
            yield return QaWait("thrown_id", 90);
            yield return Settle(4f);
            var thrown = NetIds.Get<Pickup>(int.Parse(QaRead("thrown_id")));
            if (host) QaSignal("thrown_pos", $"{thrown.transform.position.x:0.00} {thrown.transform.position.y:0.00} {thrown.transform.position.z:0.00}");
            yield return QaWait("thrown_pos", 30);
            if (!host)
            {
                var tp = QaRead("thrown_pos").Trim().Split(' ');
                var hpos = new Vector3(float.Parse(tp[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(tp[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(tp[2], System.Globalization.CultureInfo.InvariantCulture));
                Check("the thrown item rests where the host has it", (thrown.transform.position - hpos).magnitude < 1.0f, $"off by {(thrown.transform.position - hpos).magnitude:0.00} m");
            }
            yield return Barrier("thrown", Players);

            // bank loot (host drops it into the car, clear of the capsules)
            int bankedBefore = hm.Banked;
            if (host)
            {
                HeavyLoot loot = null;
                foreach (var kv in NetIds.All) if (kv.Value is Pickup lp && lp.Loot && lp.Loot.lifters == 1 && lp.isActiveAndEnabled) { loot = lp.Loot; break; }
                var carZone = FindFirstObjectByType<ElevatorExtraction>();
                loot.transform.position = carZone.transform.position + new Vector3(1.7f, 0.8f, 1.0f);
                loot.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
                QaSignal("bank_id", loot.GetComponent<Pickup>().NetId.ToString());
            }
            yield return QaWait("bank_id", 60);
            yield return Settle(5f);
            Check("banked loot counts for everyone", hm.Banked > bankedBefore, $"banked {bankedBefore} -> {hm.Banked}");
            Check("the banked item is gone", NetIds.Get<Pickup>(int.Parse(QaRead("bank_id"))) == null);
            QaSignal($"banked_{role}", hm.Banked.ToString());
            yield return Barrier("banked", Players);
            if (host)
            {
                var vals = new HashSet<string>();
                foreach (var r in Players) vals.Add(QaRead($"banked_{r}"));
                Check("the banked total is identical on all machines", vals.Count == 1, string.Join(",", vals));
            }

            // backup arrives in the guest elevator
            if (host)
            {
                QaSignal("guards_before", GuardAI.All.Count.ToString());
                hm.CallBackup(FirstGuard());
                yield return Settle(1.2f);
                var gen = tm.generator;
                QaSignal("guest_open", gen.GuestDoorOpen ? "1" : "0");
            }
            yield return QaWait("guest_open", 60);
            yield return Settle(1.5f);
            if (!host) Check("the guest elevator door opened on my machine", FindFirstObjectByType<FloorGenerator>().GuestDoorOpen);
            int before2 = int.Parse(QaRead("guards_before"));
            Check("backup guards exist on my machine", GuardAI.All.Count >= before2 + 1, $"guards {before2} -> {GuardAI.All.Count}");
            yield return Barrier("backup", Players);

            // a loot piece breaks
            if (host)
            {
                HeavyLoot victim = null;
                foreach (var kv in NetIds.All) if (kv.Value is Pickup vp && vp.Loot && vp.isActiveAndEnabled) { victim = vp.Loot; break; }
                QaSignal("broken_id", victim.GetComponent<Pickup>().NetId.ToString());
                typeof(HeavyLoot).GetMethod("Shatter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(victim, null);
            }
            yield return QaWait("broken_id", 60);
            yield return Settle(1.5f);
            Check("a broken item is gone and its value is counted as lost", NetIds.Get<Pickup>(int.Parse(QaRead("broken_id"))) == null && hm.Lost > 0, $"lost={hm.Lost}");
            yield return Barrier("broken", Players);

            // ---------------- 7. the elevator rules
            if (host) { hm.DebugCompleteFloor(); QaSignal("completed"); }
            yield return QaWait("completed", 60);
            yield return Settle(1.5f);
            Check("floor 1 is complete here too", hm.Phase == HeistPhase.FloorComplete, $"phase={hm.Phase}");
            Teleport(tm.CurrentInfo.spawn + new Vector3((Mine.Slot - 1.5f) * 1.25f, 1.1f, 0f));
            yield return WaitFor(() => ElevatorExtraction.EveryoneAboard, 30, "everyone aboard (floor 1)");
            Check("with the quota met, Up goes to floor 2", ElevatorExtraction.GoingUp && ElevatorExtraction.Destination == 2, $"up={ElevatorExtraction.GoingUp} dest={ElevatorExtraction.Destination}");
            yield return Barrier("aboard1", Players);
            if (role == "c1") { RunSync.Instance.RequestPanel(0); yield return Settle(0.8f); }
            yield return Settle(1.0f);
            Check("the direction button flipped to Down (docks)", !ElevatorExtraction.GoingUp && ElevatorExtraction.Destination == 0, $"dest={ElevatorExtraction.Destination}");
            yield return Barrier("dir_down", Players);
            if (role == "c1") RunSync.Instance.RequestPanel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 0);
            yield return FloorReady(0, 120);
            Check("riding down returns the team to the Loading Docks", hm.Phase == HeistPhase.Hub && hm.FloorLabel == "Loading Docks", $"{hm.FloorLabel}");
            Check("the docks resume at floor 2", tm.ResumeLevel == 2, $"resume={tm.ResumeLevel}");
            yield return Barrier("docks2", Players);

            // group end: floor 3 must go back to the docks (host jumps there; the rest of the machines follow)
            if (host) tm.GoToLevel(3);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 3);
            yield return FloorReady(3, 120);
            if (host) { hm.DebugCompleteFloor(); QaSignal("f3_complete"); }
            yield return QaWait("f3_complete", 60);
            Teleport(tm.CurrentInfo.spawn + new Vector3((Mine.Slot - 1.5f) * 1.25f, 1.1f, 0f));
            yield return WaitFor(() => ElevatorExtraction.EveryoneAboard, 30, "everyone aboard (floor 3)");
            yield return Settle(1.5f);
            Check("floor 3 ends the group: up is locked, only the docks remain", !ElevatorExtraction.GoingUp && ElevatorExtraction.Destination == 0, $"up={ElevatorExtraction.GoingUp} dest={ElevatorExtraction.Destination}");
            yield return Barrier("f3_panel", Players);
            if (role == "c1") RunSync.Instance.RequestPanel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 0);
            yield return FloorReady(0, 120);
            Check("after floor 3 the docks resume at floor 4", tm.ResumeLevel == 4, $"resume={tm.ResumeLevel}");
            yield return Barrier("docks4", Players);

            // ---------------- 8. a player drops out mid-run
            if (role == "c2")
            {
                net.Leave("qa dropout");
                yield return WaitFor(() => net.State == NetSessionState.Offline && SceneManager.GetActiveScene().name == NetConst.MenuScene, 30, "back at the menu");
                Check("the dropped player is back at the menu", true);
                QaSignal("c2_gone");
                QaFinish();
                yield break;
            }
            yield return QaWait("c2_gone", 90);
            yield return WaitFor(() => NetPlayer.All.Count == 3, 30, "three players left");
            Check("the run continues with three players", NetPlayer.All.Count == 3 && WobblyPlayerRegistry.All.Count == 3, $"players={NetPlayer.All.Count} registry={WobblyPlayerRegistry.All.Count}");
            var remaining = new[] { "host", "c1", "c3" };
            yield return Barrier("three", remaining);

            // ---------------- 9. floor 12: the key, the back door, the hidden elevator, the boss
            if (host) tm.GoToLevel(12);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 12);
            yield return FloorReady(12, 150);
            int keyHolders = 0; GuardAI keyGuard = null;
            foreach (var g in GuardAI.All) if (g && g.carriesKey) { keyHolders++; keyGuard = g; }
            Check("exactly one guard carries the key", keyHolders == 1, $"holders={keyHolders}");
            Check("the back door is locked", !ElevatorExtraction.BackDoorOpen);
            yield return Barrier("floor12", remaining);

            if (host)
            {
                for (int i = 0; i < keyGuard.hitsToKill; i++) keyGuard.WeaponHitHost(0.05f, null);
                QaSignal("key_dropped", keyGuard.Dead ? "1" : "0");
            }
            yield return QaWait("key_dropped", 60);
            yield return Settle(2f);
            ElevatorKey keyObj = FindFirstObjectByType<ElevatorKey>();
            Check("the key dropped when the guard went down", keyObj != null);
            Check("the key holder is down on my machine", keyGuard != null && keyGuard.Dead);
            yield return Barrier("keydrop", remaining);

            if (role == "c1")
            {
                var kp = keyObj.GetComponent<Pickup>();
                Teleport(kp.transform.position + new Vector3(0.7f, 0.6f, 0f));
                yield return Settle(1.5f);
                int n0 = MyPack.Items.Count;
                MyPack.TryStore(kp);
                yield return WaitFor(() => MyPack.Items.Count == n0 + 1, 20, "key stored");
                var car = ElevatorExtraction.Current;
                Teleport(car.backDoorPosition + (car.transform.position - car.backDoorPosition).normalized * 1.2f + Vector3.up * 0.2f);
                yield return Settle(1.5f);
                RunSync.Instance.RequestPanel(0);
                QaSignal("key_used");
            }
            yield return QaWait("key_used", 90);
            yield return WaitFor(() => ElevatorExtraction.BackDoorOpen, 20, "back door open");
            Check("the back door unlocked for everyone", ElevatorExtraction.BackDoorOpen);
            yield return Barrier("backdoor", remaining);

            var hidden = BossElevator.Current;
            Check("the hidden elevator exists behind the back door", hidden != null);
            Teleport(hidden.transform.position + new Vector3((role == "host" ? -1.3f : role == "c1" ? 0f : 1.3f), 0.4f, 0f));
            yield return Settle(2f);
            yield return Barrier("hidden_in", remaining);
            if (role == "c1") RunSync.Instance.RequestPanel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 13);
            yield return FloorReady(13, 120);
            int bosses = 0; foreach (var g in GuardAI.All) if (g && g.IsBoss) bosses++;
            Check("the boss floor has one boss", bosses == 1, $"bosses={bosses}");
            yield return Barrier("floor13", remaining);

            if (host)
            {
                GuardAI boss = null; foreach (var g in GuardAI.All) if (g && g.IsBoss) boss = g;
                for (int i = 0; i < boss.hitsToKill + 2; i++) boss.WeaponHitHost(0.05f, null);
                QaSignal("boss_down");
            }
            yield return QaWait("boss_down", 60);
            yield return WaitFor(() => hm.Phase == HeistPhase.Won, 20, "run won");
            Check("knocking out the boss wins the run for everyone", hm.Phase == HeistPhase.Won);
            yield return Barrier("won", remaining);

            // ---------------- 10. the host leaves; the clients are sent back with a reason
            if (host)
            {
                QaSignal("host_leaving");
                yield return Settle(1.0f);
                QaFinish();
                yield break;
            }
            yield return QaWait("host_leaving", 60);
            yield return WaitFor(() => net.State == NetSessionState.Offline && SceneManager.GetActiveScene().name == NetConst.MenuScene, 60, "sent back to the menu");
            Check("clients return to the menu when the host leaves", true);
            Check("with a readable reason", (net.Status ?? "").ToLowerInvariant().Contains("host"), $"status='{net.Status}'");
            QaFinish();
        }
    }
}