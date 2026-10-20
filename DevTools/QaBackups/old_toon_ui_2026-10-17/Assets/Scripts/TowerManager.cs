using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Runs the climb: generates each level, puts the team in the elevator car, rides up (or back
    /// down to the basement safe zone), and carries whatever is inside the car to the next level.
    /// </summary>
    public class TowerManager : MonoBehaviour
    {
        public static TowerManager Instance { get; private set; }

        public FloorGenerator generator;
        [Tooltip("Levels including the basement (0 = Basement, 1..10 = floors).")]
        public int totalFloors = 11;
        [Tooltip("Level the run starts on. Basement (0) first, then up.")]
        public int startLevel = 0;
        public int baseSeed = 20240;
        [Tooltip("The seed for the floor safes and the notes about them. 0 = a new random one each run (offline the floors themselves repeat, the safes do not). Online it comes from the host's seed.")]
        public int intelSeedOverride = 0;
        [Tooltip("Share of the floor's loot value that must be banked to unlock the elevator.")]
        public float quotaShare = 0.6f;

        /// <summary>The run's seed for the floor safes and the notes about them (see <see cref="SafeRules"/>).</summary>
        public int IntelSeed { get; private set; } = 1;

        public FloorInfo CurrentInfo { get; private set; }
        public bool Loading { get; private set; }
        /// <summary>True from the moment a floor has been built (and the team put in its elevator) until the next one starts building.</summary>
        public bool HasFloor { get; private set; }

        /// <summary>
        /// Where a player stands before any floor exists: in the middle of the elevator car of the first floor (the car is always in the
        /// first cell), spread out by seat. Players spawn here, held still, so nobody appears outside the map.
        /// </summary>
        public Vector3 PreFloorSpawn(int seat, int count)
        {
            float cell = generator ? generator.cellSize : 18f;
            var car = new Vector3(cell * 0.5f, 0f, cell * 0.5f + 0.5f);
            return car + new Vector3((seat - (count - 1) / 2f) * 1.2f, 1.1f, 0f);
        }
        /// <summary>The level the elevator goes to when it rides up from the basement.</summary>
        public int ResumeLevel { get; private set; } = 1;

        void Awake()
        {
            Instance = this;
            totalFloors = TowerRules.Levels; // 13 floors + the Loading Docks (overrides the value saved in the scene)
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        const float StartWaitSeconds = 150f;     // longer than the host's own wait for slow loaders (NetworkManager.LoadSceneTimeOut is 120 s)
        const float OutOfWorldY = -30f;          // nothing is ever built this low: a player down here fell through or off the map
        float nextRescue;

        /// <summary>A player of this machine that is somewhere far below every floor is put back in the elevator (it fell out of the world).</summary>
        void Update()
        {
            if (!HasFloor || Loading || Time.unscaledTime < nextRescue) return;
            nextRescue = Time.unscaledTime + 0.5f;
            var all = WobblyPlayerRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (!p || p.transform.position.y > OutOfWorldY || !Net.NetPlayer.IsLocalPlayer(p)) continue;
                Debug.LogWarning($"TowerManager: {p.name} fell out of the world (y={p.transform.position.y:0}); putting them back in the elevator.");
                int seat = p.TryGetComponent(out Net.NetPlayer np) && np.IsSpawned ? Mathf.Max(0, np.Slot) : 0;
                var spot = CurrentInfo.spawn + new Vector3((seat - 2f) * 1.2f, 1.1f, 0f);
                if (np) np.PlaceAt(spot, Quaternion.identity); else p.TeleportTo(spot, Quaternion.identity);
            }
        }

        void Start()
        {
            ResumeLevel = Mathf.Max(1, startLevel + 1);
            SafeIntel.Reset();      // a new run: nothing is known about any safe yet
            IntelSeed = intelSeedOverride != 0 ? intelSeedOverride : Random.Range(1, int.MaxValue);
            StartCoroutine(Net.NetSession.Online ? StartNetworked() : LoadFloor(startLevel));
        }

        /// <summary>Networked run: wait for the host's seed and every player, then build the same floor on every machine.</summary>
        IEnumerator StartNetworked()
        {
            // (the host only starts the run once every machine has loaded the scene, which can take a while on a slow one)
            float waited = 0f;
            while ((Net.RunSync.Instance == null || !Net.RunSync.Instance.EveryoneHere) && waited < StartWaitSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Net.RunSync.Instance == null)
            {
                // without the host's seed this machine would build a different floor from everybody else's
                Debug.LogError("TowerManager: the host never started the run; leaving.");
                Net.NetSession.Instance?.Leave("The run did not start (the host did not answer).");
                yield break;
            }
            if (!Net.RunSync.Instance.EveryoneHere)
                Debug.LogWarning($"TowerManager: {Net.NetPlayer.All.Count} players arrived of {Net.RunSync.Instance.ExpectedPlayers} expected; starting without the rest.");
            baseSeed = Net.RunSync.Instance.Seed;
            IntelSeed = baseSeed ^ 0x2D5AFE1;      // (the same on every machine: the host's seed)
            yield return LoadFloor(startLevel);
        }

        /// <summary>
        /// Called by the elevator once everyone is aboard. Up (default): next floor, or from the basement
        /// back to where the team left off. Down: only from a completed floor, to the basement.
        /// </summary>
        public void RideElevator(bool down = false)
        {
            var h = HeistManager.Instance;
            if (Loading || h == null) return;
            if (Net.NetRole.IsRemoteClient) return; // only the host decides when the elevator goes

            if (h.Phase == HeistPhase.Hub)
            {
                if (!down && !TowerRules.Beyond(ResumeLevel)) BeginLevel(ResumeLevel);   // (the demo ends at TowerRules.DemoLastFloor)
            }
            else if (h.Phase == HeistPhase.FloorComplete)
            {
                // legacy path (test mode): a finished floor sends the team to the docks
                ResumeLevel = TowerRules.ResumeAfterLeaving(h.Floor, true);
                h.PayOutSurplus();
                BeginLevel(0);
            }
        }

        /// <summary>
        /// The service elevator was confirmed to go to <paramref name="level"/> (the rules were checked on the
        /// panel). Leaving a floor for the docks remembers where to resume; surplus cash is paid out.
        /// </summary>
        public void RideTo(int level)
        {
            var h = HeistManager.Instance;
            if (Loading || h == null || Net.NetRole.IsRemoteClient) return;
            if (level == h.Floor || level < 0 || level >= totalFloors) return;
            if (TowerRules.Beyond(level)) return;                  // the demo ends at TowerRules.DemoLastFloor (test mode's GoToLevel is not stopped)
            bool complete = h.Phase == HeistPhase.FloorComplete;
            if (TowerRules.IsDocks(level) && !TowerRules.IsDocks(h.Floor))
            {
                ResumeLevel = TowerRules.ResumeAfterLeaving(h.Floor, complete);
                if (complete) h.PayOutSurplus(); // money banked beyond the quota is yours to spend at the vendor
            }
            else if (!TowerRules.IsDocks(level)) ResumeLevel = level;
            BeginLevel(level);
        }

        /// <summary>
        /// Test mode: a new seed for the safes and the notes about them (the floors themselves stay as they are), and the floor on show built
        /// again with it. Whatever the notes taught is forgotten.
        /// </summary>
        public void RerollIntel()
        {
            if (Loading || Net.NetSession.Online || Net.NetRole.IsRemoteClient) return;
            SetIntelSeed(Random.Range(1, int.MaxValue));
            var h = HeistManager.Instance;
            BeginLevel(h != null ? h.Floor : startLevel);
        }

        /// <summary>Test mode and the QA scenarios: use this seed for the safes and the notes from the next floor on (nothing is rebuilt).</summary>
        public void SetIntelSeed(int seed)
        {
            if (Net.NetSession.Online) return;
            IntelSeed = seed != 0 ? seed : 1;
            SafeIntel.Reset();
        }

        /// <summary>Test mode: jump straight to a level (0 = basement).</summary>
        public void GoToLevel(int level)
        {
            if (Loading) return;
            level = Mathf.Clamp(level, 0, totalFloors - 1);
            if (level > 0) ResumeLevel = level;
            if (Net.NetRole.IsRemoteClient) return;
            BeginLevel(level);
        }

        /// <summary>Starts a level: offline just builds it; the host tells everybody (itself included) to build it.</summary>
        void BeginLevel(int level)
        {
            var sync = Net.RunSync.Instance;
            if (Net.NetRole.IsHost && sync != null)
            {
                var car = generator.Current ? generator.Current.GetComponentInChildren<ElevatorExtraction>() : null;
                sync.BroadcastLoadLevel(level, car ? car.CargoIds() : new ushort[0], ResumeLevel);
            }
            else StartCoroutine(LoadFloor(level));
        }

        /// <summary>
        /// Networked: the host named the level and what rides in the car. A machine that is still building the last floor (a slow
        /// one) finishes that first and then builds this one: two floors must never be generated at the same time.
        /// </summary>
        public void NetLoadLevel(int level, ushort[] cargo, int resume)
        {
            if (Loading) { pendingLoads.Enqueue(new PendingLoad(level, cargo, resume)); return; }
            ResumeLevel = resume;
            StartCoroutine(LoadFloor(level, cargo));
        }

        readonly struct PendingLoad
        {
            public readonly int Level, Resume;
            public readonly ushort[] Cargo;
            public PendingLoad(int level, ushort[] cargo, int resume) { Level = level; Cargo = cargo; Resume = resume; }
        }

        readonly Queue<PendingLoad> pendingLoads = new Queue<PendingLoad>();

        /// <summary>Client: where the elevator goes from the basement (the host replicates it).</summary>
        public void NetSetResume(int level) { if (!Loading) ResumeLevel = level; }

        /// <summary>Builds one level, and then any level the host asked for in the meantime.</summary>
        IEnumerator LoadFloor(int floor, ushort[] cargoIds = null)
        {
            Loading = true;
            // Run the build by hand so that an exception in it cannot leave Loading set (the elevator would be dead for the rest of the
            // run). A yield cannot sit inside a try that has a catch, so only the MoveNext is guarded.
            var build = BuildFloor(floor, cargoIds);
            while (true)
            {
                object step;
                try
                {
                    if (!build.MoveNext()) break;
                    step = build.Current;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"TowerManager: building level {floor} failed - the elevator works again, but this floor is not usable.");
                    Debug.LogException(e);
                    break;
                }
                yield return step;
            }
            Loading = false;
            if (pendingLoads.Count == 0) ReleaseTeam();   // (another floor already waiting: keep them held until that one is built)

            if (pendingLoads.Count > 0)
            {
                var next = pendingLoads.Dequeue();
                ResumeLevel = next.Resume;
                yield return LoadFloor(next.Level, next.Cargo);
            }
        }

        IEnumerator BuildFloor(int floor, ushort[] cargoIds)
        {
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.TryGetComponent(out PlayerGrab grab)) grab.Release(false);

            // Anything inside the elevator car rides along: lift it out before the old floor goes.
            var cargo = new List<Rigidbody>();
            if (cargoIds != null)
            {
                // networked: the host named what is in the car, so every machine carries the same things
                foreach (var id in cargoIds)
                {
                    var cp = Net.NetIds.Get<Pickup>(id);
                    if (cp && cp.TryGetComponent(out Rigidbody crb)) cargo.Add(crb);
                }
                foreach (var rb in cargo) rb.transform.SetParent(transform, true);
            }
            else if (generator.Current)
            {
                var car = generator.Current.GetComponentInChildren<ElevatorExtraction>();
                if (car) cargo = car.TakeCargo();
                foreach (var rb in cargo) rb.transform.SetParent(transform, true);
            }

            HasFloor = false;
            HoldTeam();        // nobody falls while there is no floor under them
            generator.Clear();
            yield return null; // let the old floor and its NavMesh go before baking the new one

            var info = generator.Generate(floor, baseSeed + floor * 7919);
            CurrentInfo = info;

            // The team arrives inside the elevator car, spread out around its middle.
            PlaceTeam(info.spawn);
            HasFloor = true;

            PlaceCargo(cargo, info);

            HeistManager.Instance.StartFloor(floor, totalFloors, info.quota);
            Debug.Log(info.isHub
                ? $"Basement ({floor + 1}/{totalFloors}): safe zone {info.cols}x{info.rows} rooms, vendor open, {cargo.Count} items carried in"
                : $"{HeistManager.Instance.FloorLabel} ({floor + 1}/{totalFloors}): {info.cols}x{info.rows} rooms, ~{info.estimatedMinutes:0.0} min sweep, " +
                  $"{info.lootCount} loot (${info.lootValue}), quota ${info.quota}, {info.guards} guards, {cargo.Count} items carried in");
            Net.RunSync.Instance?.MarkFloorReady();
            Net.RunSync.Instance?.PublishChecksum();
        }

        /// <summary>Holds every body this machine drives still (the floor under them is gone); the other machines' bodies follow over the network.</summary>
        void HoldTeam()
        {
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                if (p.TryGetComponent(out Net.NetPlayer np)) np.Hold();
                else p.HoldStill(true);
            }
        }

        /// <summary>Puts every player in the elevator car, spread out around its middle, and lets them go.</summary>
        void PlaceTeam(Vector3 carSpawn)
        {
            int count = WobblyPlayerRegistry.All.Count, i = 0;
            foreach (var p in Net.NetPlayer.Ordered())
            {
                var pos = carSpawn + new Vector3((i - (count - 1) / 2f) * 1.2f, 1.1f, 0f);
                i++;
                if (p.TryGetComponent(out Net.NetPlayer np)) np.PlaceAt(pos, Quaternion.identity);
                else { p.TeleportTo(pos, Quaternion.identity); p.HoldStill(false); }
            }
        }

        /// <summary>The build ended (well or not): nobody stays held.</summary>
        void ReleaseTeam()
        {
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                if (p.TryGetComponent(out Net.NetPlayer np)) np.Release();
                else p.HoldStill(false);
            }
        }

        /// <summary>Sets carried loot/items down in two rows at the back of the new elevator car.</summary>
        void PlaceCargo(List<Rigidbody> cargo, FloorInfo info)
        {
            int n = 0;
            foreach (var rb in cargo)
            {
                if (!rb) continue;
                int slot = n % 10, layer = n / 10;
                int col = slot % 5, row = slot / 5;
                n++;

                rb.transform.SetParent(generator.Current, true);
                if (rb.TryGetComponent(out Pickup carried)) carried.RenewNetId(); // new floor, new ids (same order everywhere)
                rb.transform.rotation = Quaternion.identity;
                var c = rb.GetComponentInChildren<Collider>();
                float half = c ? c.bounds.extents.y : 0.3f;
                rb.transform.position = info.spawn + new Vector3((col - 2) * 1.2f, half + 0.1f + layer * 1.1f, 1.2f + row);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
