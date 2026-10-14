using System.Collections;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Runs the 10-floor climb: generates each floor, resets the team to its spawn, rides the elevator up.</summary>
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

        void Awake() { Instance = this; }
        void Start() { StartCoroutine(LoadFloor(startLevel)); }

        /// <summary>Called by the elevator once the floor is complete and everyone is aboard.</summary>
        public void RideElevator()
        {
            var h = HeistManager.Instance;
            if (Loading || h == null || h.Phase != HeistPhase.FloorComplete) return;
            StartCoroutine(LoadFloor(h.Floor + 1));
        }

        IEnumerator LoadFloor(int floor)
        {
            Loading = true;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p) p.GetComponent<PlayerGrab>()?.Release(false);
            generator.Clear();
            yield return null; // let the old floor and its NavMesh go before baking the new one

            var info = generator.Generate(floor, baseSeed + floor * 7919);
            CurrentInfo = info;

            int i = 0;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                var rb = p.GetComponent<Rigidbody>();
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                p.transform.position = info.spawn + new Vector3(i * 1.2f, 1.1f, 0);
                p.transform.rotation = Quaternion.identity;
                i++;
            }

            HeistManager.Instance.StartFloor(floor, totalFloors, info.quota);
            Debug.Log($"{HeistManager.Instance.FloorLabel} ({floor + 1}/{totalFloors}): {info.cols}x{info.rows} rooms, ~{info.estimatedMinutes:0.0} min sweep, " +
                      $"{info.lootCount} loot (${info.lootValue}), quota ${info.quota}, {info.guards} guards");
            Loading = false;
        }
    }
}