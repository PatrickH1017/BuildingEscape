using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The weapon prefabs and the slash animation retargeted to the Party Characters rig. Lives at Assets/Resources/WeaponCatalog.asset, created by the editor
    /// (Employees Take Revenge > Setup Weapons). Since 2026-10-18 the weapons are the Synty Battle Royale models (the owner added the pack): the crowbar, the baseball bat,
    /// the fire axe (the hatchet) and the heavy pistol (the pew) with its silencer; the editor fills in how each one sits (turn, grip, muzzle, silencer seat).
    /// </summary>
    public class WeaponCatalog : ScriptableObject
    {
        public GameObject crowbar, bat, hatchet;
        [Tooltip("The pew pistol (Synty Battle Royale: SM_Wep_Pistol_Heavy_01) and its cartridge (SM_Wep_Pistol_Ammo_01), which is thrown out of the gun as a case.")]
        public GameObject pew, pewBullet;
        [Tooltip("slash1 from the Free Sword Animation pack, retargeted to the party character rig.")]
        public AnimationClip slash;

        [Header("How the models sit (the owner, 2026-10-18: the Synty weapons)")]
        [Tooltip("The silencer that goes on the pew's muzzle (Synty Battle Royale: SM_Wep_Attachment_Silencer_01). Empty = a plain dark tube.")]
        public GameObject silencer;
        [Tooltip("How each model must be turned (Euler degrees) so that a melee weapon runs along +Y with its head up and the pew's barrel runs along +X with its top +Y (the game's frame). The Synty melee models already stand that way; the pistol's barrel runs along +Z, a quarter turn about Y.")]
        public Vector3 crowbarTurn, batTurn, hatchetTurn, pewTurn;
        [Tooltip("Where the hand closes round each melee weapon, in the model's own space: on the handle's own axis, a hand's width from the end opposite the head. Measured by the editor; zero = the model's own origin.")]
        public Vector3 crowbarGrip, batGrip, hatchetGrip;
        [Tooltip("In the pistol model's own space: the middle of the muzzle face (the barrel's own line), and where the silencer model's origin goes so that its rear recess sits over the barrel's end, with how it is turned there. Measured by the editor.")]
        public Vector3 pewMuzzleFace, silencerAt, silencerTurn;
        [Tooltip("Which set of models the editor filled in last. It fills the Synty ones once, so a model chosen by hand is not put back.")]
        public int modelsVersion;

        /// <summary>
        /// The size and place of a model once it is turned by <paramref name="turn"/>, around the model's own origin: all its parts, or only those whose name <paramref name="part"/> accepts.
        /// Measured from a copy of the prefab that is gone again at once (a renderer only knows its bounds while it is alive). False when nothing was found to measure.
        /// </summary>
        public static bool Measure(GameObject prefab, Quaternion turn, System.Func<string, bool> part, out Bounds bounds)
        {
            bounds = default;
            if (!prefab) return false;
            var origin = new Vector3(0f, -4000f, 0f);
            var probe = Object.Instantiate(prefab, origin, turn);
            foreach (var c in probe.GetComponentsInChildren<Collider>()) c.enabled = false;
            bool any = false;
            foreach (var r in probe.GetComponentsInChildren<Renderer>())
            {
                if (part != null && !part(r.name)) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            if (Application.isPlaying) Object.Destroy(probe); else Object.DestroyImmediate(probe);
            bounds.center -= origin;
            return any;
        }
    }
}
