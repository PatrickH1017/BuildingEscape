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

        NetworkManager nm;
        UnityTransport localTransport;
        SteamNetworkTransport steamTransport;
        SteamService steam;
        LobbyService lobby;
        CSteamID hostAtJoin;
        float connectDeadline;
        int approving;
        bool leaving;

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
            if (steamTransport != null) steamTransport.MaxConnections = NetConst.MaxPlayers;   // the other players (MaxPlayers - 1) and one more being checked
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
                if (list.Count == 0) { Status = "No open lobby with code " + code + "."; return; }
                if (!list[0].VersionOk) { Status = JoinPolicy.Message(JoinVerdict.WrongVersion); return; }
                JoinSteam(list[0].Id);
            });
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
                if (nm.ConnectedClientsIds.Count != roster.Count) return false; // someone is still connecting
                if (!UsingSteam) return true;
                foreach (var r in roster) if (!r.IsHost && !r.Ready) return false;
                return true;
            }
        }

        public void StartRun()
        {
            if (!CanStart) return;
            State = NetSessionState.StartingRun;
            Status = "Starting the run...";
            lobby?.Lock();
            PendingSeed = UnityEngine.Random.Range(1, int.MaxValue);
            nm.SceneManager.OnLoadEventCompleted += OnRunSceneLoaded;
            nm.SceneManager.LoadScene(NetConst.RunScene, LoadSceneMode.Single);
        }

        void OnRunSceneLoaded(string scene, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            nm.SceneManager.OnLoadEventCompleted -= OnRunSceneLoaded;
            foreach (var id in timedOut) nm.DisconnectClient(id, "Timed out while loading the run.");

            // Everybody starts in the middle of the first elevator car, held still until the floor is built (a player spawned at the
            // origin stood outside the map and fell).
            int slot = 0, count = completed.Count;
            var tower = TowerManager.Instance;
            foreach (var id in completed)
            {
                var at = tower != null ? tower.PreFloorSpawn(slot, count) : Vector3.zero;
                var go = Instantiate(playerPrefab, at, Quaternion.identity);
                go.GetComponent<NetPlayer>().AssignSlot(slot++);
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(id, true);
            }
            var sync = Instantiate(runSyncPrefab);
            sync.GetComponent<NetworkObject>().Spawn(true);
            sync.GetComponent<RunSync>().Begin(PendingSeed, completed.Count);
            State = NetSessionState.InRun;
            Status = "";
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
            IsHost = UsingSteam = LocalReady = false;
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
                lobby.JoinRequested += id => { if (State == NetSessionState.Offline) JoinSteam(id); };
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
            else if (nm.IsListening)
            {
                int n = 1;
                IEnumerable<ulong> ids = IsHost || localRosterIds.Count == 0 ? nm.ConnectedClientsIds : localRosterIds;
                foreach (var id in ids)
                    roster.Add(new RosterEntry { Name = "Player " + n++, Ready = true, IsHost = id == NetworkManager.ServerClientId, IsLocal = id == nm.LocalClientId, Look = "" });
            }
            RosterChanged?.Invoke();
        }

        // ------------------------------------------------------------------ join approval (host)

        static byte[] Payload() =>
            Encoding.UTF8.GetBytes(NetConst.BuildVersion + "|" + (SteamService.Instance != null && SteamService.Instance.Ready
                ? SteamService.Instance.LocalId.m_SteamID : 0UL));

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
            int bar = text.LastIndexOf('|');
            string version = bar < 0 ? text : text.Substring(0, bar);
            ulong claimed = bar >= 0 && ulong.TryParse(text.Substring(bar + 1), out var c) ? c : 0;

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

            int others = nm.ConnectedClientsIds.Count + (approving - 1); // connected + other joins still being checked
            var verdict = JoinPolicy.Evaluate(State >= NetSessionState.StartingRun, others, version, NetConst.BuildVersion, member);
            res.Approved = verdict == JoinVerdict.Allow;
            res.Reason = JoinPolicy.Message(verdict);
            res.Pending = false;
            approving--;
        }
    }
}