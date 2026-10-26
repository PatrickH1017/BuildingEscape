using Unity.Netcode;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Host-spawned object that ties a networked run together. It is split by concern:
    ///   RunSync.cs            seed + player count (this file)
    ///   RunSync.State.cs      run state mirror (phase, quota, cash, timer, elevator flags)
    ///   RunSync.Snapshots.cs  host -> clients stream of moving loot / items / guards
    ///   RunSync.Events.cs     host -> everyone: things that happened (stored, grabbed, banked, level load...)
    ///   RunSync.Requests.cs   client -> host: things a player wants to do (the host validates them)
    /// </summary>
    public partial class RunSync : NetworkBehaviour
    {
        public static RunSync Instance { get; private set; }

        readonly NetworkVariable<int> seed = new();
        readonly NetworkVariable<int> expected = new();
        readonly NetworkVariable<bool> started = new();

        public int Seed => seed.Value;
        public bool Started => started.Value;
        /// <summary>How many players the host says are in the run (the connected clients).</summary>
        public int ExpectedPlayers => expected.Value;
        /// <summary>Host: the players connected - everybody but the developers who watch (<see cref="NetSession.PlayersConnected"/>).</summary>
        int PlayerCount => NetSession.Instance != null ? NetSession.Instance.PlayersConnected : NetworkManager.ConnectedClientsIds.Count;
        /// <summary>Started, and every player object has arrived on this machine.</summary>
        public bool EveryoneHere => started.Value && NetPlayer.All.Count >= expected.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            SpawnSnapshots();
            SpawnDevView();                      // (a developer who watches listens for the host's debug record: RunSync.DevView.cs)
            IntroCinematic.Arm(transform);       // (the run-start cinematic: DEVELOPER_NOTES 2.19)
        }

        public override void OnNetworkDespawn()
        {
            DespawnSnapshots();
            DespawnDevView();
            if (Instance == this) Instance = null;
        }

        /// <summary>Host only.</summary>
        public void Begin(int runSeed, int playerCount)
        {
            seed.Value = runSeed;
            expected.Value = playerCount;
            started.Value = true;
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                // somebody who drops out while the others are still arriving must not leave them waiting for a player who is gone
                int players = PlayerCount;
                if (started.Value && expected.Value != players) expected.Value = players;
                HostPublishState();
                HostSendSnapshots();
                HostSendDevView();               // (only while a developer watches)
                IntroServerTick();
                HostPvpBoardTick();
            }
            else { ClientApplyState(); }
        }

        void LateUpdate()
        {
            if (IsSpawned && !IsServer) ClientSmoothPuppets();
        }
    }
}