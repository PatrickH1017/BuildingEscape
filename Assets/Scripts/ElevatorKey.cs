using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The key a guard on floor 12 carries. It is an ordinary pickup (it goes in the backpack); using it at the
    /// service elevator's back door unlocks the hidden elevator to the boss floor.
    /// </summary>
    public class ElevatorKey : MonoBehaviour
    {
        /// <summary>The key a player has in their pack, or null.</summary>
        public static Pickup InPack(Backpack pack)
        {
            if (pack == null) return null;
            foreach (var p in pack.Items)
                if (p && p.GetComponent<ElevatorKey>()) return p;
            return null;
        }
    }
}