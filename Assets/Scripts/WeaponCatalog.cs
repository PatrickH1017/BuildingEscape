using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The melee weapon prefabs (Simple low poly melee weapon pack) and the slash animation retargeted
    /// to the Party Characters rig. Lives at Assets/Resources/WeaponCatalog.asset, created by the editor
    /// (Overtime Escape > Setup Weapons).
    /// </summary>
    public class WeaponCatalog : ScriptableObject
    {
        public GameObject crowbar, bat, hatchet;
        [Tooltip("slash1 from the Free Sword Animation pack, retargeted to the party character rig.")]
        public AnimationClip slash;
    }
}
