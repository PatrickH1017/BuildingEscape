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
        /// <summary>Started, and every player object has arrived on this machine.</summary>
        public bool EveryoneHere => started.Value && NetPlayer.All.Count >= expected.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            SpawnSnapshots();
        }

        public override void OnNetworkDespawn()
        {
            DespawnSnapshots();
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
            if (IsServer) { HostPublishState(); HostSendSnapshots(); }
            else { ClientApplyState(); }
        }

        void LateUpdate()
        {
            if (IsSpawned && !IsServer) ClientSmoothPuppets();
        }
    }
}