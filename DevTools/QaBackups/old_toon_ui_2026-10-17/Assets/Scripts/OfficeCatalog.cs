using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>One prop from an asset pack: the key the room generator asks for (see PropTable) and its prefab.</summary>
    [System.Serializable]
    public class PropEntry
    {
        public string key;
        public GameObject prefab;
    }

    /// <summary>
    /// References to the office asset-pack prefabs so the floor generator can use them at runtime.
    /// Lives at Assets/Resources/OfficeCatalog.asset, created automatically by the editor
    /// (Employees Take Revenge > Setup Office Pack).
    /// </summary>
    public class OfficeCatalog : ScriptableObject
    {
        [Tooltip("Used by Test Mode's spawn buttons.")]
        public GameObject desk1, desk2, pc, printer;

        [Tooltip("Every prop the room generator can place: furniture, appliances, desk items, decoration.")]
        public List<PropEntry> props = new List<PropEntry>();

        Dictionary<string, GameObject> map;

        public bool HasProps => props != null && props.Count > 0;

        void OnEnable() { map = null; }
        void OnValidate() { map = null; }

        /// <summary>The prefab for a prop key (null if that pack is not in the project).</summary>
        public GameObject Get(string key)
        {
            if (map == null)
            {
                map = new Dictionary<string, GameObject>();
                if (props != null)
                    foreach (var e in props)
                        if (e != null && !string.IsNullOrEmpty(e.key) && e.prefab) map[e.key] = e.prefab;
            }
            return map.TryGetValue(key, out var g) ? g : null;
        }
    }
}
