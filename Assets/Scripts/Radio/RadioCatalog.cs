using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What the dock radio is made of: the Synty Construction pack's radio and the tracks of its three stations (the owner, 2026-10-27: the Midnight Mode vol.1 soundtrack, the FREE Energy Hard Rock Music Pack and Urban Jazz).
    /// <c>Resources/RadioCatalog</c>, filled by Employees Take Revenge > Setup Dock Radio (Editor/RadioSetup), which also sets the tracks' import settings (streamed, compressed: the packs are 680 MB of WAV).
    /// </summary>
    [CreateAssetMenu(menuName = "Employees Take Revenge/Radio Catalog")]
    public sealed class RadioCatalog : ScriptableObject
    {
        public const string ResourceName = "RadioCatalog";

        [System.Serializable]
        public sealed class Station
        {
            public string name;
            public AudioClip[] tracks = new AudioClip[0];
        }

        [Tooltip("The radio on the boxes in the corner (SM_Prop_Radio_01 of POLYGON Construction).")]
        public GameObject radio;
        [Tooltip("The stations, in the order of the radio's buttons (RadioRules.Names).")]
        public Station[] stations = new Station[0];

        static RadioCatalog instance;
        static bool loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; loaded = false; }

        /// <summary>The catalog, or null when it has not been set up (then the dock has no radio).</summary>
        public static RadioCatalog Instance
        {
            get
            {
                if (!loaded) { loaded = true; instance = Resources.Load<RadioCatalog>(ResourceName); }
                return instance;
            }
        }

        /// <summary>The tracks of a station (none when it is not set up).</summary>
        public AudioClip[] Tracks(int station) => station >= 0 && station < stations.Length && stations[station] != null && stations[station].tracks != null ? stations[station].tracks : new AudioClip[0];
    }
}
