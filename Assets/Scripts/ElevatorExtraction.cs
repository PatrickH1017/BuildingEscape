using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Elevator drop zone. F banks the loot inside; once the floor quota is met, G rides up
    /// when every player is inside.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ElevatorExtraction : MonoBehaviour
    {
        public KeyCode bankKey = KeyCode.F;
        public KeyCode rideKey = KeyCode.G;

        readonly HashSet<HeavyLoot> inside = new();
        int playersInside;

        public static bool EveryoneAboard { get; private set; }

        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l) inside.Add(l);
            if (o.GetComponentInParent<WobblyPlayer>()) playersInside++;
        }

        void OnTriggerExit(Collider o)
        {
            var l = o.GetComponentInParent<HeavyLoot>(); if (l) inside.Remove(l);
            if (o.GetComponentInParent<WobblyPlayer>()) playersInside = Mathf.Max(0, playersInside - 1);
        }

        void Update()
        {
            EveryoneAboard = playersInside > 0 && playersInside >= WobblyPlayerRegistry.All.Count;
            if (playersInside > 0 && Input.GetKeyDown(bankKey)) Bank();
            if (EveryoneAboard && Input.GetKeyDown(rideKey)) TowerManager.Instance?.RideElevator();
        }

        void OnDestroy() { EveryoneAboard = false; }

        public void Bank()
        {
            inside.RemoveWhere(l => !l);
            foreach (var l in new List<HeavyLoot>(inside))
            {
                if (l.Broken) continue;
                HeistManager.Instance?.Bank(l);
                Destroy(l.gameObject);
            }
            inside.Clear();
        }
    }
}