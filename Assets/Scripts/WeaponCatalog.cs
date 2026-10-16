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
        [Tooltip("The pew pistol (Guns pack: GunsAssetPack/guns/Prefabs/pew.prefab) and its cartridge (bulletPEW.prefab), which is thrown out of the gun as a case.")]
        public GameObject pew, pewBullet;
        [Tooltip("slash1 from the Free Sword Animation pack, retargeted to the party character rig.")]
        public AnimationClip slash;
    }
}
