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

        void Awake() { Instance = this; }

        void Start()
        {
            ResumeLevel = Mathf.Max(1, startLevel + 1);
            StartCoroutine(LoadFloor(startLevel));
        }

        /// <summary>
        /// Called by the elevator once everyone is aboard. Up (default): next floor, or from the basement
        /// back to where the team left off. Down: only from a completed floor, to the basement.
        /// </summary>
        public void RideElevator(bool down = false)
        {
            var h = HeistManager.Instance;
            if (Loading || h == null) return;

            if (h.Phase == HeistPhase.Hub)
            {
                if (!down) StartCoroutine(LoadFloor(ResumeLevel));
            }
            else if (h.Phase == HeistPhase.FloorComplete)
            {
                // A finished floor is closed for good: the elevator only goes down to the basement,
                // and from there up to the next unfinished floor.
                ResumeLevel = h.Floor + 1;
                StartCoroutine(LoadFloor(0));
            }
        }

        /// <summary>Test mode: jump straight to a level (0 = basement).</summary>
        public void GoToLevel(int level)
        {
            if (Loading) return;
            level = Mathf.Clamp(level, 0, totalFloors - 1);
            if (level > 0) ResumeLevel = level;
            StartCoroutine(LoadFloor(level));
        }

        IEnumerator LoadFloor(int floor)
        {
            Loading = true;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p) p.GetComponent<PlayerGrab>()?.Release(false);

            // Anything inside the elevator car rides along: lift it out before the old floor goes.
            var cargo = new List<Rigidbody>();
            if (generator.Current)
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
            foreach (var p in WobblyPlayerRegistry.All)
            {
                var rb = p.GetComponent<Rigidbody>();
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                float x = (i - (count - 1) / 2f) * 1.2f;
                p.transform.position = info.spawn + new Vector3(x, 1.1f, 0);
                p.transform.rotation = Quaternion.identity;
                i++;
            }

            PlaceCargo(cargo, info);

            HeistManager.Instance.StartFloor(floor, totalFloors, info.quota);
            Debug.Log(info.isHub
                ? $"Basement ({floor + 1}/{totalFloors}): safe zone {info.cols}x{info.rows} rooms, vendor open, {cargo.Count} items carried in"
                : $"{HeistManager.Instance.FloorLabel} ({floor + 1}/{totalFloors}): {info.cols}x{info.rows} rooms, ~{info.estimatedMinutes:0.0} min sweep, " +
                  $"{info.lootCount} loot (${info.lootValue}), quota ${info.quota}, {info.guards} guards, {cargo.Count} items carried in");
            Loading = false;
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
                rb.transform.rotation = Quaternion.identity;
                var c = rb.GetComponentInChildren<Collider>();
                float half = c ? c.bounds.extents.y : 0.3f;
                rb.transform.position = info.spawn + new Vector3((col - 2) * 1.2f, half + 0.1f + layer * 1.1f, 1.2f + row);
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
