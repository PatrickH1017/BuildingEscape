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
        [Tooltip("Share of the floor's loot value that must be banked to unlock the elevator.")]
        public float quotaShare = 0.6f;

        public FloorInfo CurrentInfo { get; private set; }
        public bool Loading { get; private set; }
        /// <summary>The level the elevator goes to when it rides up from the basement.</summary>
        public int ResumeLevel { get; private set; } = 1;

        void Awake()
        {
            Instance = this;
            totalFloors = TowerRules.Levels; // 13 floors + the Loading Docks (overrides the value saved in the scene)
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            ResumeLevel = Mathf.Max(1, startLevel + 1);
            StartCoroutine(Net.NetSession.Online ? StartNetworked() : LoadFloor(startLevel));
        }

        /// <summary>Networked run: wait for the host's seed and every player, then build the same floor on every machine.</summary>
        IEnumerator StartNetworked()
        {
            float waited = 0f;
            while ((Net.RunSync.Instance == null || !Net.RunSync.Instance.EveryoneHere) && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (Net.RunSync.Instance != null) baseSeed = Net.RunSync.Instance.Seed;
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
                if (!down) BeginLevel(ResumeLevel);
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
            bool complete = h.Phase == HeistPhase.FloorComplete;
            if (TowerRules.IsDocks(level) && !TowerRules.IsDocks(h.Floor))
            {
                ResumeLevel = TowerRules.ResumeAfterLeaving(h.Floor, complete);
                if (complete) h.PayOutSurplus(); // money banked beyond the quota is yours to spend at the vendor
            }
            else if (!TowerRules.IsDocks(level)) ResumeLevel = level;
            BeginLevel(level);
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

            generator.Clear();
            yield return null; // let the old floor and its NavMesh go before baking the new one

            var info = generator.Generate(floor, baseSeed + floor * 7919);
            CurrentInfo = info;

            // The team arrives inside the elevator car, spread out around its middle.
            int count = WobblyPlayerRegistry.All.Count, i = 0;
            foreach (var p in Net.NetPlayer.Ordered())
            {
                var rb = p.GetComponent<Rigidbody>();
                if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                float x = (i - (count - 1) / 2f) * 1.2f;
                var pos = info.spawn + new Vector3(x, 1.1f, 0);
                var np = p.GetComponent<Net.NetPlayer>();
                if (np) np.PlaceAt(pos, Quaternion.identity);
                else { p.transform.position = pos; p.transform.rotation = Quaternion.identity; }
                i++;
            }

            PlaceCargo(cargo, info);

            HeistManager.Instance.StartFloor(floor, totalFloors, info.quota);
            Debug.Log(info.isHub
                ? $"Basement ({floor + 1}/{totalFloors}): safe zone {info.cols}x{info.rows} rooms, vendor open, {cargo.Count} items carried in"
                : $"{HeistManager.Instance.FloorLabel} ({floor + 1}/{totalFloors}): {info.cols}x{info.rows} rooms, ~{info.estimatedMinutes:0.0} min sweep, " +
                  $"{info.lootCount} loot (${info.lootValue}), quota ${info.quota}, {info.guards} guards, {cargo.Count} items carried in");
            Net.RunSync.Instance?.MarkFloorReady();
            Net.RunSync.Instance?.PublishChecksum();
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
