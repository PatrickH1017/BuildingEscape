using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Auto-registers every WobblyPlayer so guards/managers can enumerate them.</summary>
    [RequireComponent(typeof(WobblyPlayer))]
    public class WobblyPlayerRegistry : MonoBehaviour
    {
        public static readonly List<WobblyPlayer> All = new();
        WobblyPlayer self;
        /// <summary>
        /// True when every player who is still in the run is in <paramref name="aboard"/> (an elevator car). Players under
        /// arrest are out of the run until the vendor gets them out, so the car does not wait for them.
        /// </summary>
        public static bool AllAboard(HashSet<WobblyPlayer> aboard)
        {
            int needed = 0;
            foreach (var p in All)
            {
                if (!p || p.Custody == CustodyState.Busted) continue;
                needed++;
                if (!aboard.Contains(p)) return false;
            }
            return needed > 0;
        }

        void OnEnable() { self = GetComponent<WobblyPlayer>(); All.Add(self); }
        void OnDisable() { All.Remove(self); }
    }
}
