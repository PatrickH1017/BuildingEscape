# Round 9, batch G04 (the owner, 2026-10-18/19: "Yes, add the SMG, rifle, shotgun and sniper guns from the Synty pack"): the data foundation.
#  - WeaponCatalog: a GunModel entry per gun (prefab, turn, muzzle face, hands, silencer seat, casing), Gun(kind)
#  - RangedWeapon: Kind { Pew, Smg, Rifle, Shotgun, Sniper }, a Spec per kind (pellets, hold, sounds, unlock floor, ammunition), a Layout per kind, found guns start with one clip
#  - AudioCatalog: the Assault Rifle and Shotgun recordings of the Free Weapon SFX pack
#  - WeaponCatalogBuilder: measures the four guns (muzzle face, grip behind the trigger, foregrip, silencer seat) and fills the catalog
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}

FromDraft "$Src\WeaponCatalog.cs" 'C:\bqs\gun_draft\WeaponCatalog.cs'
FromDraft "$Src\Guns\RangedWeapon.cs" 'C:\bqs\gun_draft\RangedWeapon.cs'

# ---- the audio catalog: the rifle's and the shotgun's recordings
Open-Src "$Src\AudioCatalog.cs"
Rep @'
        [Tooltip("Drawing the gun (equipping it), and the rustle of a guard bringing his up to aim.")]
        public AudioClip[] gunEquip, gunAim;
'@ @'
        [Tooltip("Drawing the gun (equipping it), and the rustle of a guard bringing his up to aim.")]
        public AudioClip[] gunEquip, gunAim;

        [Header("Guns: the assault rifle and the shotgun of the same pack (the owner, 2026-10-19)")]
        [Tooltip("The assault rifle's sounds (the pack's Assault Rifle folder): gunshots with their tails, the reload in order (magazine out, drawn, in, charge), drawing it and bringing it up. The sniper rifle uses them too.")]
        public AudioClip[] rifleShot, rifleTail, rifleReload, rifleEquip, rifleAim;
        [Tooltip("The shotgun's sounds (the pack's Shotgun folder): gunshots with their tails, the reload in order (charge, pump back, shell drawn, chambering, pump forward), drawing it and bringing it up.")]
        public AudioClip[] shotgunShot, shotgunTail, shotgunReload, shotgunEquip, shotgunAim;
'@
Save-Src

Open-Src "$Ed\AudioCatalogBuilder.cs"
Rep 'else if (cat.gunShot == null || cat.gunShot.Length == 0) { FillGuns(cat);' 'else if (cat.gunShot == null || cat.gunShot.Length == 0 || cat.rifleShot == null || cat.rifleShot.Length == 0 || cat.shotgunShot == null || cat.shotgunShot.Length == 0) { FillGuns(cat);'
Rep @'
            cat.gunAim = Clips("sfx_wpn_hgn_foley_aim_01", "sfx_wpn_hgn_foley_aim_02");
        }
'@ @'
            cat.gunAim = Clips("sfx_wpn_hgn_foley_aim_01", "sfx_wpn_hgn_foley_aim_02");
            // the assault rifle (also the sniper rifle's) and the shotgun, from the same pack
            cat.rifleShot = Clips("sfx_wpn_ar_gunshot_01", "sfx_wpn_ar_gunshot_02", "sfx_wpn_ar_gunshot_03");
            cat.rifleTail = Clips("sfx_wpn_ar_tail_01", "sfx_wpn_ar_tail_02");
            cat.rifleReload = Clips("sfx_wpn_ar_reload_01_magOut", "sfx_wpn_ar_reload_02_magDraw", "sfx_wpn_ar_reload_03_magIn", "sfx_wpn_ar_reload_04_charge");
            cat.rifleEquip = Clips("sfx_wpn_ar_foley_equip_01", "sfx_wpn_ar_foley_equip_02");
            cat.rifleAim = Clips("sfx_wpn_ar_foley_aim_01", "sfx_wpn_ar_foley_aim_02");
            cat.shotgunShot = Clips("sfx_wpn_sgn_gunshot_01", "sfx_wpn_sgn_gunshot_02", "sfx_wpn_sgn_gunshot_03");
            cat.shotgunTail = Clips("sfx_wpn_sgn_tail_01", "sfx_wpn_sgn_tail_02");
            cat.shotgunReload = Clips("sfx_wpn_sgn_reload_01_charge", "sfx_wpn_sgn_reload_02_pumpBack", "sfx_wpn_sgn_reload_03_shellDraw", "sfx_wpn_sgn_reload_04_chambering", "sfx_wpn_sgn_reload_05_pumpForward");
            cat.shotgunEquip = Clips("sfx_wpn_sgn_foley_equip_01", "sfx_wpn_sgn_foley_equip_02");
            cat.shotgunAim = Clips("sfx_wpn_sgn_foley_aim_01", "sfx_wpn_sgn_foley_aim_02");
        }

        /// <summary>For the QA runs (<c>-executeMethod</c>): fills the guns' sounds now, the same as the first editor start with the new fields empty, and exits.</summary>
        public static void FillGunsBatch()
        {
            try
            {
                var cat = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
                if (!cat) { Debug.LogError("[Audio] no catalog"); return; }
                FillGuns(cat);
                EditorUtility.SetDirty(cat);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Audio] guns: handgun {cat.gunShot.Length}/{cat.gunTail.Length}/{cat.gunReload.Length}/{cat.gunEquip.Length}/{cat.gunAim.Length}, rifle {cat.rifleShot.Length}/{cat.rifleTail.Length}/{cat.rifleReload.Length}/{cat.rifleEquip.Length}/{cat.rifleAim.Length}, shotgun {cat.shotgunShot.Length}/{cat.shotgunTail.Length}/{cat.shotgunReload.Length}/{cat.shotgunEquip.Length}/{cat.shotgunAim.Length}");
            }
            finally { EditorApplication.Exit(0); }
        }
'@
Save-Src

# ---- the weapon catalog builder: the four long guns
Open-Src "$Ed\WeaponCatalogBuilder.cs"
Rep @'
                    if (cat.modelsVersion < SyntyVersion) FillSynty(cat, false);
'@ @'
                    if (cat.modelsVersion < SyntyVersion) FillSynty(cat, false);
                    if (cat.longGuns == null || cat.longGuns.Length < LongGunNames.Length || !cat.longGuns[0].prefab) FillLongGuns(cat, false);
'@
Rep @'
        /// <summary>The middle of the pistol's muzzle face: the front-most ring of vertices of its body mesh (the end of the barrel), in the model's own space.</summary>
'@ @'
        // ------------------------------------------------------------------ the long guns (the owner, 2026-10-19)

        static readonly string[] LongGunNames = { "SM_Wep_SubMGun_Lite_01", "SM_Wep_Rifle_Assault_01", "SM_Wep_Shotgun_01", "SM_Wep_Sniper_Mil_01" };
        static readonly string[] LongGunCasings = { "SM_Wep_Pistol_Ammo_01", "SM_Wep_Bullet_Ammo_01", "SM_Wep_Shotgun_Ammo_01", "SM_Wep_Ammo_BulletLarge_01" };
        static readonly string[] LongGunMagazines = { "_mag", "_mag", "", "_mag" };
        /// <summary>Which of the four are given a silencer (the sub machine gun and the rifle: the owner's rule by weapon class).</summary>
        static readonly bool[] LongGunSilenced = { true, true, false, false };

        /// <summary>
        /// The sub machine gun (SM_Wep_SubMGun_Lite_01), the assault rifle (SM_Wep_Rifle_Assault_01), the shotgun (SM_Wep_Shotgun_01) and the sniper rifle (SM_Wep_Sniper_Mil_01) of the Synty pack, and how each sits: the muzzle face (the front-most
        /// ring of the whole model), where the right hand closes (behind the trigger) and where the left hand holds (the foregrip, the underside of the handguard, the pump, the underside of the stock's front), and for the two that take a silencer where its
        /// origin goes. All measured from a copy of the prefab, in the model's own space; the guns run along +Z, like the pistol, so they are turned a quarter about Y.
        /// </summary>
        static void FillLongGuns(WeaponCatalog cat, bool log)
        {
            var silencer = cat.silencer ? cat.silencer : Load<GameObject>(Synty + "SM_Wep_Attachment_Silencer_01.prefab");
            if (!silencer || !SilencerSeat(silencer, out float seat)) { Debug.LogWarning("The Synty silencer could not be measured: the long guns are not set up."); return; }
            var list = new WeaponCatalog.GunModel[LongGunNames.Length];
            for (int i = 0; i < list.Length; i++)
            {
                var prefab = Load<GameObject>(Synty + LongGunNames[i] + ".prefab");
                if (!prefab) { Debug.LogWarning("Synty gun not found: " + LongGunNames[i]); return; }
                var gm = new WeaponCatalog.GunModel { prefab = prefab, turn = new Vector3(0f, 90f, 0f), casing = Load<GameObject>(Synty + LongGunCasings[i] + ".prefab"), magazinePart = LongGunMagazines[i] };
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                inst.transform.localScale = Vector3.one;
                try
                {
                    gm.muzzleFace = FrontFace(inst);
                    var trigger = FindPart(inst, "trigger");
                    var behindTrigger = trigger ? PartBounds(trigger).center : new Vector3(0f, 0f, 0.05f);
                    gm.grip = behindTrigger + new Vector3(0f, -0.045f, -0.035f);           // (the palm closes round the pistol grip: a little below and behind the trigger)
                    switch (i)
                    {
                        case 0: { var fg = FindPart(inst, "_grip_"); gm.foregrip = fg ? PartBounds(fg).center : new Vector3(0f, -0.03f, 0.20f); break; }                  // the vertical foregrip
                        case 1: gm.foregrip = Underside(inst, 0.33f, 0.47f); break;                                                                                         // under the handguard
                        case 2: { var pump = FindPart(inst, "pump"); var pb = pump ? PartBounds(pump) : new Bounds(new Vector3(0f, 0.06f, 0.48f), Vector3.zero); gm.foregrip = new Vector3(pb.center.x, pb.min.y - 0.012f, pb.center.z); break; }   // under the pump
                        default: gm.foregrip = Underside(inst, 0.22f, 0.32f); break;                                                                                         // under the front of the stock
                    }
                }
                finally { Object.DestroyImmediate(inst); }
                if (LongGunSilenced[i]) { gm.silencerAt = gm.muzzleFace - new Vector3(0f, 0f, seat); gm.silencerTurn = Vector3.zero; }
                list[i] = gm;
                if (log) Debug.Log($"[Weapons] {LongGunNames[i]}: muzzle face {gm.muzzleFace:F4}, grip {gm.grip:F4}, foregrip {gm.foregrip:F4}, silencer at {gm.silencerAt:F4}, casing {(gm.casing ? gm.casing.name : "-")}");
            }
            cat.longGuns = list;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log("[Weapons] the long guns are in the catalog.");
        }

        static IEnumerable<MeshFilter> Meshes(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true);

        static Transform FindPart(GameObject root, string lowerName)
        {
            foreach (var mf in Meshes(root)) if (mf.name.ToLowerInvariant().Contains(lowerName)) return mf.transform;
            return null;
        }

        /// <summary>The box of one part in the root's own space (all its vertices).</summary>
        static Bounds PartBounds(Transform part)
        {
            var root = part.root;
            Bounds b = default; bool any = false;
            foreach (var mf in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = root.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
                }
            }
            return b;
        }

        /// <summary>The middle of the muzzle face of a whole gun: the ring of vertices within half a millimetre of the front-most one, over every part, in the root's own space.</summary>
        static Vector3 FrontFace(GameObject root)
        {
            float front = float.MinValue;
            foreach (var mf in Meshes(root))
            {
                if (!mf.sharedMesh) continue;
                foreach (var v in mf.sharedMesh.vertices) front = Mathf.Max(front, root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)).z);
            }
            Bounds ring = default; bool any = false;
            foreach (var mf in Meshes(root))
            {
                if (!mf.sharedMesh) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = root.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (w.z < front - 0.0005f) continue;
                    if (!any) { ring = new Bounds(w, Vector3.zero); any = true; } else ring.Encapsulate(w);
                }
            }
            return any ? new Vector3(ring.center.x, ring.center.y, front) : new Vector3(0f, 0f, front);
        }

        /// <summary>Where the left hand goes under a forend: between <paramref name="z0"/> and <paramref name="z1"/> along the barrel, the lowest vertices of the body's own mesh (not the magazine) give the underside; the hand sits a centimetre below it.</summary>
        static Vector3 Underside(GameObject root, float z0, float z1)
        {
            float low = float.MaxValue; Bounds at = default; bool any = false;
            foreach (var mf in Meshes(root))
            {
                if (!mf.sharedMesh || mf.name.ToLowerInvariant().Contains("_mag")) continue;
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var w = root.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (w.z < z0 || w.z > z1) continue;
                    if (w.y < low - 0.0005f) { low = w.y; at = new Bounds(w, Vector3.zero); any = true; }
                    else if (w.y < low + 0.003f) { at.Encapsulate(w); }
                }
            }
            return any ? new Vector3(at.center.x, low - 0.012f, (z0 + z1) * 0.5f) : new Vector3(0f, -0.04f, (z0 + z1) * 0.5f);
        }

        /// <summary>For the QA runs (<c>-executeMethod</c>): measures the long guns now (the same as the first editor start with them missing) and exits.</summary>
        public static void LongGunsBatch()
        {
            try
            {
                var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
                if (!cat) { Debug.LogError("[Weapons] no catalog"); return; }
                FillLongGuns(cat, true);
            }
            finally { EditorApplication.Exit(0); }
        }

        /// <summary>The middle of the pistol's muzzle face: the front-most ring of vertices of its body mesh (the end of the barrel), in the model's own space.</summary>
'@
Rep @'
using UnityEditor;
using UnityEngine;
'@ @'
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
'@
Save-Src
