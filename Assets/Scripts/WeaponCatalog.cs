using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The weapon prefabs and the slash animation retargeted to the Party Characters rig. Lives at Assets/Resources/WeaponCatalog.asset, created by the editor
    /// (Employees Take Revenge > Setup Weapons). Since 2026-10-18 the weapons are the Synty Battle Royale models (the owner added the pack): the crowbar, the baseball bat,
    /// the fire axe (the hatchet) and the heavy pistol (the pew) with its silencer; the editor fills in how each one sits (turn, grip, muzzle, silencer seat). Since 2026-10-19 it also holds
    /// the sub machine gun, the assault rifle, the shotgun and the sniper rifle of the same pack (<see cref="longGuns"/>).
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
        [Tooltip("The red dot sight that sits on top of the sub machine gun, the rifle and the shotgun (Synty Battle Royale: SM_Wep_Attachment_Redot_01; the owner, 2026-10-20). Empty = none.")]
        public GameObject redDot;
        [Tooltip("How each model must be turned (Euler degrees) so that a melee weapon runs along +Y with its head up and the pew's barrel runs along +X with its top +Y (the game's frame). The Synty melee models already stand that way; the pistol's barrel runs along +Z, a quarter turn about Y.")]
        public Vector3 crowbarTurn, batTurn, hatchetTurn, pewTurn;
        [Tooltip("Where the hand closes round each melee weapon, in the model's own space: on the handle's own axis, a hand's width from the end opposite the head. Measured by the editor; zero = the model's own origin.")]
        public Vector3 crowbarGrip, batGrip, hatchetGrip;
        [Tooltip("In the pistol model's own space: the middle of the muzzle face (the barrel's own line), and where the silencer model's origin goes so that its rear recess sits over the barrel's end, with how it is turned there. Measured by the editor.")]
        public Vector3 pewMuzzleFace, silencerAt, silencerTurn;
        [Tooltip("Which set of models the editor filled in last. It fills the Synty ones once, so a model chosen by hand is not put back.")]
        public int modelsVersion;

        /// <summary>One gun of the Synty pack and how it sits (the owner, 2026-10-19: the SMG, rifle, shotgun and sniper). Everything is in the model's own space, before it is turned and scaled.</summary>
        [System.Serializable]
        public class GunModel
        {
            public GameObject prefab;
            [Tooltip("Euler degrees that put the barrel along +X with the top +Y (the game's frame). The Synty guns run along +Z, a quarter turn about Y.")]
            public Vector3 turn = new Vector3(0f, 90f, 0f);
            [Tooltip("The middle of the muzzle face: the front-most ring of the whole model, on the barrel's own line. Measured by the editor.")]
            public Vector3 muzzleFace;
            [Tooltip("Where the right hand closes (the middle of the pistol grip, behind the trigger) and where the left hand holds (under the forend, on the pump, on the foregrip); zero foregrip = a gun held in one hand. Measured by the editor.")]
            public Vector3 grip, foregrip;
            [Tooltip("Where the silencer model's origin goes so that its rear recess sits over the end of the barrel, and how it is turned there (the guns that take one: the SMG and the rifle). Measured by the editor.")]
            public Vector3 silencerAt, silencerTurn;
            [Tooltip("Where the red dot sight's origin goes (on top of the body, on the barrel's own line) and how it is turned there, for the guns that carry one (the SMG, the rifle, the shotgun); zero sightAt = no sight. Measured by the editor.")]
            public Vector3 sightAt, sightTurn;
            [Tooltip("The empty case that is thrown out of the gun (empty: the pistol's cartridge).")]
            public GameObject casing;
            [Tooltip("Part of a name of the magazine's mesh, for the spare clip model lying in a safe; empty = none.")]
            public string magazinePart;
        }

        [Tooltip("The sub machine gun, the assault rifle, the shotgun and the sniper rifle, in the order of RangedWeapon.Kind after the pew (Smg, Rifle, Shotgun, Sniper).")]
        public GunModel[] longGuns;

        [Header("Body armour (the owner, 2026-10-23: the Synty Battle Royale armour)")]
        [Tooltip("The armour lying about, Light / Medium / Heavy (Synty Battle Royale: SM_PickUp_Armor_01 to 03). Empty = the game's boxes (ArmourItem).")]
        public GameObject[] armourPickups = new GameObject[3];
        [Tooltip("The armour worn on the chest, Light / Medium / Heavy (Synty Battle Royale: SM_Chr_Attach_Male_Armor_01 to 03), scaled to the wearer (ArmourFitRules). Empty = the game's boxes (GuardArmour).")]
        public GameObject[] armourWorn = new GameObject[3];

        /// <summary>The model of a piece of this tier lying about, or null when the catalog has none (then the boxes are used).</summary>
        public GameObject ArmourPickup(ShieldTier tier) { int i = (int)tier - 1; return armourPickups != null && i >= 0 && i < armourPickups.Length ? armourPickups[i] : null; }

        /// <summary>The model of the vest of this tier worn on the chest, or null when the catalog has none.</summary>
        public GameObject ArmourWorn(ShieldTier tier) { int i = (int)tier - 1; return armourWorn != null && i >= 0 && i < armourWorn.Length ? armourWorn[i] : null; }

        GunModel pewModel;

        /// <summary>The model entry of a gun (<paramref name="kind"/> is <see cref="RangedWeapon.Kind"/> as a number): the pew's from its own fields above, the others' from <see cref="longGuns"/>. Null when there is none.</summary>
        public GunModel Gun(int kind)
        {
            if (kind == 0)
            {
                if (pewModel == null) pewModel = new GunModel();
                pewModel.prefab = pew; pewModel.turn = pewTurn; pewModel.muzzleFace = pewMuzzleFace;
                pewModel.silencerAt = silencerAt; pewModel.silencerTurn = silencerTurn;
                pewModel.casing = pewBullet; pewModel.magazinePart = "_mag";
                return pewModel;
            }
            int i = kind - 1;
            return longGuns != null && i >= 0 && i < longGuns.Length ? longGuns[i] : null;
        }

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
