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
        void OnEnable() { self = GetComponent<WobblyPlayer>(); All.Add(self); }
        void OnDisable() { All.Remove(self); }
    }
}
