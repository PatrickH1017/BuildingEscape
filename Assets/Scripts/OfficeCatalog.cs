using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// References to the "Office Pack - Free" prefabs so the floor generator can use them at runtime.
    /// Lives at Assets/Resources/OfficeCatalog.asset, created automatically by the editor
    /// (Overtime Escape > Setup Office Pack).
    /// </summary>
    public class OfficeCatalog : ScriptableObject
    {
        public GameObject desk1, desk2, pc, printer;
    }
}
