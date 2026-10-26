using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Steamworks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.Net
{
    public enum NetSessionState { Offline, Connecting, InLobby, StartingRun, InRun }

    public struct RosterEntry
    {
        public string Name;
        public bool Ready, IsHost, IsLocal;
        /// <summary>PlayerAppearance string; empty when unknown (local test mode).</summary>
        public string Look;
    }

    /// <summary>
    /// The one place that turns "I want to play with friends" into a running Netcode session:
    ///   Steam lobby (find / invite / cap of 4)  ->  Steam relay transport  ->  Netcode host + clients
    ///   -> host starts the run -> everyone loads the run scene and gets a player.
    /// The menu only calls the public methods and reads State / Status / Roster.
    /// A "local" mode (UnityTransport on localhost) exists for testing without Steam.
    /// Lives on the persistent Net object next to NetworkManager, SteamService and both transports.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }

        /// <summary>True while a Netcode session is running (a networked run is on).</summary>
        public static bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        [Tooltip("Prefab with NetworkObject + NetPlayer. Spawned for every client when the run scene has loaded.")]
        public GameObject playerPrefab;
        [Tooltip("Prefab with NetworkObject + RunSync. Spawned by the host with the run seed.")]
        public GameObject runSyncPrefab;
        public float connectTimeoutSeconds = 20f;
        public ushort localPort = 7777;

        public NetSessionState State { get; private set; }
        /// <summary>Latest message for the menu (progress or the reason something failed).</summary>
        public string Status { get; private set; } = "";
        public bool IsHost { get; private set; }
        public bool UsingSteam { get; private set; }
        public int PendingSeed { get; private set; }
        public IReadOnlyList<RosterEntry> Roster => roster;
        public string LobbyCode => lobby != null && lobby.InLobby ? lobby.Code : "";
        public bool LocalReady { get; private set; }
        public SteamService Steam => steam;
        /// <summary>The Steam transport (traffic counters, connection limit); for diagnostics and the QA tests.</summary>
        public SteamNetworkTransport SteamTransport => steamTransport;
        public event Action RosterChanged;
        /// <summary>This machine is a developer who watches somebody else's match (the owner, 2026-10-23): it took no seat in the Steam lobby, has no player and is invisible to the party. See <see cref="DevAccessRules"/>.</summary>
        public bool IsSpectator { get; private set; }
        /// <summary>Host: is this connection a developer who watches (and no player)?</summary>
        public bool IsSpectatorClient(ulong clientId) => spectatorIds.Contains(clientId);
        /// <summary>Host: how many developers watch now.</summary>
        public int SpectatorCount
        {
            get
            {
                int n = 0;
                if (nm != null && nm.IsListening && nm.IsServer) foreach (var id in nm.ConnectedClientsIds) if (spectatorIds.Contains(id)) n++;
                return n;
            }
        }
        /// <summary>Host: the players connected - everybody but the developers who watch. The party size, the player limit, the party list, the intro's vote and the spawns count these.</summary>
        public int PlayersConnected => nm != null && nm.IsListening && nm.IsServer ? DevAccessRules.PlayersAmong(nm.ConnectedClientsIds.Count, SpectatorCount) : 0;

        NetworkManager nm;
        UnityTransport localTransport;
        SteamNetworkTransport steamTransport;
        SteamService steam;
        LobbyService lobby;
        CSteamID hostAtJoin;
        float connectDeadline;
        int approving;
        bool leaving;
        bool spectateNext;                                    // (the next lobby search is a developer's: SpectateByCode)
        readonly HashSet<ulong> spectatorIds = new();         // host: the connections let in as developers who watch (DevAccessRules); nobody but the host knows them

        // Local test mode has no Steam lobby: the host tells clients who is connected (Steam mode reads the lobby).
        const string RosterMessage = "be.roster";
        readonly List<ulong> localRosterIds = new();
        float nextRosterSend;

        readonly List<RosterEntry> roster = new();
        readonly List<LobbyMember> members = new();

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            nm = GetComponent<NetworkManager>();
            localTransport = GetComponent<UnityTransport>();
            steamTransport = GetComponent<SteamNetworkTransport>();
            steam = GetComponent<SteamService>();
            nm.ConnectionApprovalCallback = OnApproval;
            if (steamTransport != null) steamTransport.MaxConnections = NetConst.MaxPlayers + (DevAccess.Compiled ? DevAccessRules.MaxSpectators : 0);   // the other players (MaxPlayers - 1) and one more being checked, and the developers who watch (only a build with the dev tools lets them in)
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        IEnumerator Start()
        {
            // Launched from a Steam invite ("+connect_lobby <id>")? Join that lobby once Steam is up.
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "+connect_lobby");
            if (i < 0 || i + 1 >= args.Length || !ulong.TryParse(args[i + 1], out var id)) yield break;
            yield return new WaitUntil(() => steam != null && steam.Ready);
            JoinSteam(new CSteamID(id));
        }

        void Update()
        {
            // resend now and then: a client that has just connected may not have its handler registered yet
            if (IsHost && !UsingSteam && State == NetSessionState.InLobby && Time.unscaledTime >= nextRosterSend)
            {
                nextRosterSend = Time.unscaledTime + 1f;
                SendRoster();
            }
            if (State == NetSessionState.InRun && nm != null && nm.IsListening && SceneManager.GetActiveScene().name == NetConst.MenuScene) BackInLobby();       // (the host brought the party back)
            if (State == NetSessionState.Connecting && connectDeadline > 0 && Time.unscaledTime > connectDeadline)
                Leave("Could not reach the host (timed out).");
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            lobby?.Dispose();
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            Instance = null;
        }

        // ------------------------------------------------------------------ menu API: Steam

        public void HostSteam(LobbyVisibility visibility)
        {
            if (!CanBegin() || !EnsureSteam()) return;
            Begin("Creating lobby...");
            lobby.Create(visibility, steam.LocalName + "'s lobby", (ok, err) =>
            {
                if (!ok) { Leave(err); return; }
                steamTransport.PrepareHost();
                if (!StartNetcode(true, steamTransport, Payload())) { Leave("Could not start hosting."); return; }
                UsingSteam = true; IsHost = true;
                lobby.PublishPresence();
                EnterLobby();
            });
        }

        public void JoinSteam(CSteamID lobbyId)
        {
            if (!CanBegin() || !EnsureSteam()) return;
            Begin("Joining lobby...");
            lobby.Join(lobbyId, (ok, err) =>
            {
                if (!ok) { Leave(err); return; }
                if (lobby.State != NetConst.StateOpen) { Leave("That run has already started."); return; }
                if (SteamMatchmaking.GetLobbyData(lobbyId, NetConst.KeyVersion) != NetConst.BuildVersion)
                { Leave(JoinPolicy.Message(JoinVerdict.WrongVersion)); return; }

                hostAtJoin = lobby.Owner;
                connectDeadline = Time.unscaledTime + ConnectSeconds;   // the lobby took its share; the relay connection gets its own
                steamTransport.PrepareClient(hostAtJoin.m_SteamID);
                if (!StartNetcode(false, steamTransport, Payload())) { Leave("Could not start the connection."); return; }
                UsingSteam = true; IsHost = false;
                Status = "Connecting to host...";
            });
        }

        public void JoinByCode(string code)
        {
            if (!CanBegin() || !EnsureSteam()) return;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length != NetConst.CodeLength) { Status = $"Codes are {NetConst.CodeLength} characters."; return; }
            Begin("Looking for lobby " + code + "...");
            SearchByCode(code, 3);
        }

        // A lobby that was created a moment ago can take a couple of seconds to become searchable.
        void SearchByCode(string code, int attemptsLeft)
        {
            lobby.Search(code, list =>
            {
                if (list.Count == 0 && attemptsLeft > 1) { StartCoroutine(RetrySearch(code, attemptsLeft - 1)); return; }
                State = NetSessionState.Offline;
                bool asSpectator = spectateNext; spectateNext = false;
                if (list.Count == 0) { Status = "No open lobby with code " + code + "."; return; }
                if (!list[0].VersionOk) { Status = JoinPolicy.Message(JoinVerdict.WrongVersion); return; }
#if ETR_DEV_TOOLS || UNITY_EDITOR
                if (asSpectator) { ConnectSpectator(list[0]); return; }
#endif
                JoinSteam(list[0].Id);
            }, spectateNext);
        }

        IEnumerator RetrySearch(string code, int attemptsLeft)
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (State == NetSessionState.Connecting) SearchByCode(code, attemptsLeft);
        }

        public void Browse(Action<List<LobbyListing>> done)
        {
            if (!EnsureSteam()) { done(new List<LobbyListing>()); return; }
            lobby.Search(null, done);
        }

        public void InviteFriends() { lobby?.ShowInviteDialog(); }

        /// <summary>Invite one Steam friend to this lobby (from the in-game friends list).</summary>
        public bool InviteFriend(CSteamID friend) => lobby != null && lobby.Invite(friend);

        /// <summary>The Steam lobby this machine is in (nil when not in one).</summary>
        public CSteamID CurrentLobby => lobby != null ? lobby.Current : CSteamID.Nil;

        /// <summary>Publishes this player's chosen look to the lobby so friends see it on the stage.</summary>
        public void SetLook(string look) { if (UsingSteam) lobby?.SetLook(look); }

        // ------------------------------------------------------------------ menu API: local test

        public void HostLocal()
        {
            if (!CanBegin()) return;
            Begin("Starting local host...");
            localTransport.SetConnectionData("127.0.0.1", localPort, "0.0.0.0");
            if (!StartNetcode(true, localTransport, Payload())) { Leave("Could not start the local host."); return; }
            UsingSteam = false; IsHost = true;
            EnterLobby();
        }

        public void JoinLocal(string ip)
        {
            if (!CanBegin()) return;
            Begin("Connecting to " + ip + "...");
            localTransport.SetConnectionData(string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(), localPort);
            if (!StartNetcode(false, localTransport, Payload())) { Leave("Could not start the connection."); return; }
            UsingSteam = false; IsHost = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Dev only: the real Steam transport code over loopback IP (see SteamNetworkTransport.devDirectIp).
        public void DevHostSteamSockets()
        {
            if (!CanBegin() || !EnsureSteam()) return;
            Begin("Starting Steam-socket host...");
            steamTransport.devDirectIp = true;
            steamTransport.PrepareHost();
            if (!StartNetcode(true, steamTransport, Payload())) { Leave("Could not start the Steam-socket host."); return; }
            UsingSteam = false; IsHost = true;
            EnterLobby();
        }

        public void DevJoinSteamSockets()
        {
            if (!CanBegin() || !EnsureSteam()) return;
            Begin("Connecting over Steam sockets...");
            steamTransport.devDirectIp = true;
            if (!StartNetcode(false, steamTransport, Payload())) { Leave("Could not start the Steam-socket connection."); return; }
            UsingSteam = false; IsHost = false;
        }
#endif

        // ------------------------------------------------------------------ lobby / run

        public void SetReady(bool ready)
        {
            LocalReady = ready;
            if (UsingSteam) lobby?.SetReady(ready);
        }

        /// <summary>Host: everyone is connected and (on Steam) ready.</summary>
        public bool CanStart
        {
            get
            {
                if (!IsHost || State != NetSessionState.InLobby) return false;
                if (PlayersConnected != roster.Count) return false; // someone is still connecting (a developer who watches is no player)
                if (!UsingSteam) return true;
                foreach (var r in roster) if (!r.IsHost && !r.Ready) return false;
                return true;
            }
        }

        /// <summary>QA only: the run seed the host starts the run with instead of a random one (0: random). The built-player autopilot picks one whose safes hold what it wants to take (a safe holds a gun only by luck).</summary>
        public int QaSeed;

        public void StartRun()
        {
            if (!CanStart) return;
            State = NetSessionState.StartingRun;
            Status = "Starting the run...";
            lobby?.Lock();
            PendingSeed = QaSeed != 0 ? QaSeed : UnityEngine.Random.Range(1, int.MaxValue);
            nm.SceneManager.OnLoadEventCompleted += OnRunSceneLoaded;
            nm.SceneManager.LoadScene(NetConst.RunScene, LoadSceneMode.Single);
        }

        void OnRunSceneLoaded(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            nm.SceneManager.OnLoadEventCompleted -= OnRunSceneLoaded;
            foreach (var id in timedOut) nm.DisconnectClient(id, "Timed out while loading the run.");

            // Everybody starts in the middle of the first elevator car, held still until the floor is built (a player spawned at the
            // origin stood outside the map and fell).
            var players = new List<ulong>(completed.Count);
            foreach (var id in completed) if (!spectatorIds.Contains(id)) players.Add(id);          // (a developer who watches gets no body and no slot)
            int slot = 0, count = players.Count;
            var tower = TowerManager.Instance;
            foreach (var id in players)
            {
                var at = tower != null ? tower.PreFloorSpawn(slot, count) : Vector3.zero;
                var go = Instantiate(playerPrefab, at, Quaternion.identity);
                go.GetComponent<NetPlayer>().AssignSlot(slot++);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
            }
            var sync = Instantiate(runSyncPrefab);
            sync.GetComponent<NetworkObject>().Spawn(true);
            sync.GetComponent<RunSync>().Begin(PendingSeed, players.Count);
            State = NetSessionState.InRun;
            Status = "";
        }

        /// <summary>
        /// Host: after a run (the end screen: Exit, "Leave with party") everybody goes back to the lobby together; the connection stays, the party can start another run. Anybody else, and a session that
        /// is not in a run, just leaves. The players and the run's objects are spawned with their scene, so loading the menu scene removes them on every machine.
        /// </summary>
        public void ReturnPartyToLobby()
        {
            if (!IsHost || State != NetSessionState.InRun || nm == null || !nm.IsListening) { Leave(); return; }
            nm.SceneManager.LoadScene(NetConst.MenuScene, LoadSceneMode.Single);
        }

        /// <summary>The menu scene is on screen again while the session is still connected (the host brought the party back): the lobby it is.</summary>
        void BackInLobby()
        {
            State = NetSessionState.InLobby;
            Status = "";
            LocalReady = false;
            if (UsingSteam) { lobby?.SetReady(false); if (IsHost) lobby?.Unlock(); }
            RebuildRoster();
        }

        /// <summary>Leave the lobby or run and return to the menu. Safe to call in any state.</summary>
        public void Leave(string reason = null)
        {
            if (leaving) return;
            leaving = true;
            Status = reason ?? "";
            StartCoroutine(LeaveRoutine());
        }

        IEnumerator LeaveRoutine()
        {
            if (nm.IsListening || nm.ShutdownInProgress)
            {
                nm.Shutdown();
                while (nm.ShutdownInProgress) yield return null;
            }
            lobby?.ClearPresence();
            lobby?.Leave();
            roster.Clear();
            IsHost = UsingSteam = LocalReady = IsSpectator = false;
            spectatorIds.Clear();
            State = NetSessionState.Offline;
            connectDeadline = 0;
            leaving = false;
            RosterChanged?.Invoke();
            if (SceneManager.GetActiveScene().name != NetConst.MenuScene) SceneManager.LoadScene(NetConst.MenuScene);
        }

        // ------------------------------------------------------------------ Netcode wiring

        bool CanBegin()
        {
            if (State == NetSessionState.Offline && !leaving) return true;
            Status = "Already in a session.";
            return false;
        }

        void Begin(string status)
        {
            State = NetSessionState.Connecting;
            Status = status;
            LocalReady = false;
            connectDeadline = Time.unscaledTime + ConnectSeconds;
        }

        /// <summary>How long a connection attempt may take: the first Steam relay connection can need several seconds to set up.</summary>
        float ConnectSeconds => Mathf.Max(connectTimeoutSeconds, 30f);

        bool EnsureSteam()
        {
            if (steam == null || !steam.Ready) steam?.Retry();
            if (steam == null || !steam.Ready)
            {
                Status = steam?.Error ?? "Steam is not available.";
                return false;
            }
            if (lobby == null)
            {
                lobby = new LobbyService();
                lobby.Changed += OnLobbyChanged;
                lobby.JoinRequested += id => { if (State == NetSessionState.Offline) { MenuRegistry.CloseAll(); JoinSteam(id); } };
            }
            return true;
        }

        bool StartNetcode(bool host, NetworkTransport transport, byte[] connectionData)
        {
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.ConnectionData = connectionData;
            return host ? nm.StartHost() : nm.StartClient();
        }

        void EnterLobby()
        {
            if (UsingSteam && PlayerAppearance.TryLoadSaved(out var mine)) lobby.SetLook(mine.Serialize());
            State = NetSessionState.InLobby;
            connectDeadline = 0;
            Status = "";
            RebuildRoster();
        }

        void OnClientConnected(ulong id)
        {
            if (!IsHost && id == nm.LocalClientId)
            {
                if (!UsingSteam && nm.CustomMessagingManager != null)
                    nm.CustomMessagingManager.RegisterNamedMessageHandler(RosterMessage, OnRoster);
                EnterLobby();
            }
            else
            {
                RebuildRoster();
                SendRoster();
            }
        }

        /// <summary>Host, local mode: send the list of connected clients to everyone.</summary>
        void SendRoster()
        {
            if (!IsHost || UsingSteam || !nm.IsListening || nm.CustomMessagingManager == null) return;
            var ids = new List<ulong>(nm.ConnectedClientsIds);
            ids.RemoveAll(spectatorIds.Contains);                 // (a developer who watches is in nobody's list)
            using var w = new FastBufferWriter(2 + ids.Count * 8, Unity.Collections.Allocator.Temp);
            w.WriteValueSafe((byte)ids.Count);
            foreach (var id in ids) w.WriteValueSafe(id);
            foreach (var id in ids)
                if (id != NetworkManager.ServerClientId)
                    nm.CustomMessagingManager.SendNamedMessage(RosterMessage, id, w, NetworkDelivery.ReliableSequenced);
        }

        void OnRoster(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out byte n);
            localRosterIds.Clear();
            for (int i = 0; i < n; i++) { r.ReadValueSafe(out ulong id); localRosterIds.Add(id); }
            RebuildRoster();
        }

        void OnClientDisconnected(ulong id)
        {
            spectatorIds.Remove(id);
            if (!IsHost && id == nm.LocalClientId)
            {
                // A reason we set ourselves (full, wrong version...) is shown as is; a plain transport
                // drop arrives as Netcode's technical "[Disconnect Event]..." text, so say it in words.
                string why = nm.DisconnectReason;
                bool transportDrop = string.IsNullOrEmpty(why) || why.StartsWith("[Disconnect Event]");
                Leave(!transportDrop ? why
                    : State == NetSessionState.Connecting ? "Could not connect to the host." : "The host left the game.");
            }
            else RebuildRoster();
        }

        void OnLobbyChanged()
        {
            // The Steam lobby owner is the host; if ownership moves, the host is gone.
            if (!IsHost && lobby != null && lobby.InLobby && hostAtJoin.IsValid() && lobby.Owner != hostAtJoin)
            {
                Leave("The host left the lobby.");
                return;
            }
            RebuildRoster();
        }

        void RebuildRoster()
        {
            roster.Clear();
            if (UsingSteam && lobby != null && lobby.InLobby)
            {
                lobby.GetMembers(members);
                foreach (var m in members)
                {
                    roster.Add(new RosterEntry { Name = m.Name, Ready = m.Ready, IsHost = m.IsOwner, IsLocal = m.IsLocal, Look = m.Look });
                    if (m.IsLocal) LocalReady = m.Ready;
                }
            }
            else if (nm.IsListening && !IsSpectator)
            {
                int n = 1;
                IEnumerable<ulong> ids = IsHost || localRosterIds.Count == 0 ? nm.ConnectedClientsIds : localRosterIds;
                foreach (var id in ids)
                {
                    if (IsHost && spectatorIds.Contains(id)) continue;          // (a developer who watches is no player and no name in the list)
                    roster.Add(new RosterEntry { Name = "Player " + n++, Ready = true, IsHost = id == NetworkManager.ServerClientId, IsLocal = id == nm.LocalClientId, Look = "" });
                }
            }
            RosterChanged?.Invoke();
        }

        // ------------------------------------------------------------------ join approval (host)

        static byte[] Payload(bool spectator = false)
        {
            ulong id = SteamService.Instance != null && SteamService.Instance.Ready ? SteamService.Instance.LocalId.m_SteamID : 0UL;
#if UNITY_EDITOR
            if (spectator && QaAccount != 0UL) id = QaAccount;
#endif
            return Encoding.UTF8.GetBytes(DevAccessRules.Payload(NetConst.BuildVersion, id, spectator));
        }

        void OnApproval(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            res.CreatePlayerObject = false; // players are spawned once the run scene has loaded
            if (req.ClientNetworkId == NetworkManager.ServerClientId) { res.Approved = true; return; }
            res.Pending = true;
            StartCoroutine(ApproveRoutine(req.ClientNetworkId, req.Payload, res));
        }

        IEnumerator ApproveRoutine(ulong clientId, byte[] payload, NetworkManager.ConnectionApprovalResponse res)
        {
            approving++;
            string text = payload == null ? "" : Encoding.UTF8.GetString(payload);
            DevAccessRules.ParsePayload(text, out string version, out ulong claimed, out bool spectator);
            if (spectator)
            {
                ApproveSpectator(clientId, version, claimed, res);          // (a developer who asks to watch: no lobby seat to check, the account list decides)
                approving--;
                yield break;
            }

            bool member = !UsingSteam;
            if (UsingSteam)
            {
                // The Steam identity comes from the transport, never from the payload. Give the
                // lobby's member list a moment to catch up with a client that has just joined.
                for (float t = 0; t < 3f && !member; t += Time.unscaledDeltaTime)
                {
                    if (steamTransport.TryGetRemoteSteamId(clientId, out var id) && id.m_SteamID == claimed && lobby.IsMember(id))
                        member = true;
                    else yield return null;
                }
            }

            int others = PlayersConnected + (approving - 1); // players connected + other joins still being checked (a developer who watches takes no seat)
            var verdict = JoinPolicy.Evaluate(State >= NetSessionState.StartingRun, others, version, NetConst.BuildVersion, member);
            res.Approved = verdict == JoinVerdict.Allow;
            res.Reason = JoinPolicy.Message(verdict);
            res.Pending = false;
            approving--;
        }

        // ------------------------------------------------------------------ a developer who watches (DevAccessRules)

        /// <summary>
        /// Host: a connection that asks to watch the match as a developer (the owner, 2026-10-23). Steam says which account it is (the Editor's local test mode takes the claim), the list says whether that account is a developer's, and the rules say whether this build, the run, the room and the version
        /// allow it (<see cref="DevAccessRules.Judge"/>). A developer who is let in is remembered here and nowhere else: he has no player, no seat in the party list and no message goes to anybody about him.
        /// </summary>
        void ApproveSpectator(ulong clientId, string version, ulong claimed, NetworkManager.ConnectionApprovalResponse res)
        {
            var verdict = DevAccessRules.Judge(DevAccess.Compiled, State >= NetSessionState.StartingRun, SpectatorCount, version == NetConst.BuildVersion,
                ProvesAccount(clientId, claimed) && DevAccounts.IsDeveloper(claimed));
            if (verdict == SpectateVerdict.Allow) spectatorIds.Add(clientId);
            res.Approved = verdict == SpectateVerdict.Allow;
            res.Reason = DevAccessRules.Message(verdict);
            res.Pending = false;
        }

        /// <summary>Host: is the connection really the account it says? On Steam the transport knows who connected; the Editor's local test mode has no Steam, so there a test's claim is taken (no build has that line).</summary>
        bool ProvesAccount(ulong clientId, ulong claimed)
        {
            if (claimed == 0UL) return false;
            if (UsingSteam) return steamTransport != null && steamTransport.TryGetRemoteSteamId(clientId, out var id) && id.m_SteamID == claimed;
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

#if ETR_DEV_TOOLS || UNITY_EDITOR
        /// <summary>
        /// A developer joins the party with this lobby code as an invisible spectator (the owner, 2026-10-23; the terminal's <c>spectate CODE</c>). He does NOT join the Steam lobby - so he takes no seat in it and the party's list never shows him - but connects straight to the host, whose Steam ID the
        /// lobby names (<see cref="NetConst.KeyHost"/>). The host lets him in only if Steam says he is on the developer list, the run has not started and this is a build with the dev tools. Returns "" when the search was started, else the reason it was not.
        /// </summary>
        public string SpectateByCode(string code)
        {
            if (!DevAccess.Compiled) return DevAccessRules.Message(SpectateVerdict.NotInThisBuild);
            if (!CanBegin()) return Status;
            if (!EnsureSteam()) return Status;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length != NetConst.CodeLength) return $"Codes are {NetConst.CodeLength} characters.";
            if (!DevAccounts.IsDeveloper(steam.LocalId.m_SteamID)) return DevAccessRules.Message(SpectateVerdict.NotADeveloper);
            Begin("Looking for lobby " + code + "...");
            spectateNext = true;
            SearchByCode(code, 3);
            return "";
        }

        void ConnectSpectator(LobbyListing l)
        {
            if (!CanBegin()) return;
            if (l.Host == 0UL) { Status = "That lobby does not name its host (an older build)."; return; }
            Begin("Connecting to the host...");
            hostAtJoin = new CSteamID(l.Host);
            steamTransport.PrepareClient(l.Host);
            IsSpectator = true;                                            // (before the connection: the payload says so)
            if (!StartNetcode(false, steamTransport, Payload(true))) { IsSpectator = false; Leave("Could not start the connection."); return; }
            UsingSteam = true; IsHost = false;
            Status = "Connecting to host...";
        }
#endif

#if UNITY_EDITOR
        /// <summary>QA only (the Editor): the Steam account the next spectator connection over the local transport claims - the local transport has no Steam to say who connects, so a test names one.</summary>
        public static ulong QaAccount;

        /// <summary>QA only (the Editor): connects to a local host as a developer who watches, claiming <paramref name="account"/> (the host takes the claim in the Editor only).</summary>
        public void QaJoinLocalSpectator(string ip, ulong account)
        {
            if (!CanBegin()) return;
            QaAccount = account;
            Begin("Connecting to " + ip + "...");
            localTransport.SetConnectionData(string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(), localPort);
            IsSpectator = true;
            if (!StartNetcode(false, localTransport, Payload(true))) { IsSpectator = false; Leave("Could not start the connection."); return; }
            UsingSteam = false; IsHost = false;
        }
#endif
    }
}