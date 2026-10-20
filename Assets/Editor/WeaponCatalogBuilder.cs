using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Builds Assets/Resources/WeaponCatalog.asset and Slash1_Party.anim.
    /// The sword pack's slash1 targets bones named "EEJANAIBot:Hips/..." and also animates a "Sword" object;
    /// the party character's bones have the same names without the prefix, so this copies the clip with
    /// the prefix stripped, drops the sword and the position/scale curves (different body proportions).
    /// </summary>
    [InitializeOnLoad]
    public static class WeaponCatalogBuilder
    {
        const string Pack = "Assets/Donthrukat/Simple low poly melee weapon pack/Prefabs/";
        const string SlashSrc = "Assets/EEJANAI_Team/FreeSwordAnimations/Animations/slash1.anim";
        const string ClipPath = "Assets/Resources/Slash1_Party.anim";
        const string CatalogPath = "Assets/Resources/WeaponCatalog.asset";
        const string GunPack = "Assets/GunsAssetPack/guns/Prefabs/";
        const string Synty = "Assets/Synty/PolygonBattleRoyale/Prefabs/Weapons/";
        const int SyntyVersion = 1;

        static WeaponCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
                if (!cat) { if (!System.IO.File.Exists(CatalogPath)) Build(false); }      // (a catalog that exists but does not load: its script is not imported yet - rebuilding now would overwrite it with a blank one)
                else
                {
                    if (!cat.pew || !cat.pewBullet) FillGuns(cat, false);
                    if (cat.modelsVersion < SyntyVersion) FillSynty(cat, false);
                    if (cat.longGuns == null || cat.longGuns.Length < LongGunNames.Length || !cat.longGuns[0].prefab) FillLongGuns(cat, false);
                }
            };
        }

        /// <summary>The pew and its cartridge from the Guns pack (only what is missing is filled).</summary>
        static void FillGuns(WeaponCatalog cat, bool log)
        {
            if (!cat.pew) cat.pew = Load<GameObject>(GunPack + "pew.prefab");
            if (!cat.pewBullet) cat.pewBullet = Load<GameObject>(GunPack + "bulletPEW.prefab");
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log(cat.pew ? "Pew set in the weapon catalog." : "Guns pack not found where expected (Assets/GunsAssetPack/guns/Prefabs/pew.prefab).");
        }

        /// <summary>
        /// The weapons from the Synty Battle Royale pack (the owner, 2026-10-18): the crowbar, the bat, the fire axe (the hatchet), the heavy pistol (the pew) with its silencer, and a cartridge for the case that is thrown out.
        /// The melee models lie as the pack makes them (along +Y, head up: no turn); the pistol and the silencer run along +Z, so the pistol is turned a quarter about Y to put its barrel along the game's +X. What the game cannot
        /// measure for itself - a build cannot read a mesh's vertices - is measured here and stored: where the hand closes on each handle, the middle of the pistol's muzzle face, and where the silencer's origin goes so that its
        /// rear recess sits over the end of the barrel on the barrel's own line. Done once (<see cref="WeaponCatalog.modelsVersion"/>), so a model chosen by hand is not put back.
        /// </summary>
        static void FillSynty(WeaponCatalog cat, bool log)
        {
            var crowbar = Load<GameObject>(Synty + "SM_Wep_Crowbar_01.prefab");
            var bat = Load<GameObject>(Synty + "SM_Wep_Bat_01.prefab");
            var axe = Load<GameObject>(Synty + "SM_Wep_Fireaxe_01.prefab");
            var pistol = Load<GameObject>(Synty + "SM_Wep_Pistol_Heavy_01.prefab");
            var silencer = Load<GameObject>(Synty + "SM_Wep_Attachment_Silencer_01.prefab");
            var cartridge = Load<GameObject>(Synty + "SM_Wep_Pistol_Ammo_01.prefab");
            if (!crowbar || !bat || !axe || !pistol || !silencer)
            {
                if (log) Debug.LogWarning("The Synty Battle Royale weapons were not found where expected (" + Synty + "): the old models stay.");
                return;
            }
            if (!MuzzleFace(pistol, out var face) || !SilencerSeat(silencer, out var seat))
            {
                Debug.LogWarning("The Synty pistol or silencer could not be measured (no mesh to read): the old models stay.");
                return;
            }
            cat.crowbar = crowbar; cat.bat = bat; cat.hatchet = axe;
            cat.crowbarTurn = cat.batTurn = cat.hatchetTurn = Vector3.zero;
            cat.crowbarGrip = HandleGrip(crowbar, MeleeWeapon.Kind.Crowbar);
            cat.batGrip = HandleGrip(bat, MeleeWeapon.Kind.Bat);
            cat.hatchetGrip = HandleGrip(axe, MeleeWeapon.Kind.Hatchet);
            cat.pew = pistol; cat.pewTurn = new Vector3(0f, 90f, 0f);
            cat.pewMuzzleFace = face;
            cat.silencer = silencer; cat.silencerAt = face - new Vector3(0f, 0f, seat); cat.silencerTurn = Vector3.zero;
            if (cartridge) cat.pewBullet = cartridge;
            cat.modelsVersion = SyntyVersion;
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log($"Weapon catalog: the Synty weapons are in (muzzle face {face:F4}, silencer seat {seat:F4}, grips {cat.crowbarGrip:F3} {cat.batGrip:F3} {cat.hatchetGrip:F3}).");
        }

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
        static bool MuzzleFace(GameObject pistol, out Vector3 face)
        {
            face = default;
            var mf = pistol.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return false;
            var vs = mf.sharedMesh.vertices;
            if (vs.Length == 0) return false;
            float front = float.MinValue;
            foreach (var v in vs) front = Mathf.Max(front, v.z);
            Bounds ring = default; bool any = false;
            foreach (var v in vs)
            {
                if (v.z < front - 0.0005f) continue;
                if (!any) { ring = new Bounds(v, Vector3.zero); any = true; } else ring.Encapsulate(v);
            }
            face = new Vector3(ring.center.x, ring.center.y, front);
            return any;
        }

        /// <summary>Where the silencer's rear recess ends (its own z): the barrel's end goes in as far as this. The first centre vertex behind the rear face is the bottom of the recess.</summary>
        static bool SilencerSeat(GameObject silencer, out float seatZ)
        {
            seatZ = 0f;
            var mf = silencer.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return false;
            bool any = false; float z = float.MaxValue;
            foreach (var v in mf.sharedMesh.vertices)
                if (new Vector2(v.x, v.y).sqrMagnitude < 0.0005f * 0.0005f && v.z < z) { z = v.z; any = true; }
            seatZ = z;
            return any;
        }

        /// <summary>
        /// Where the hand closes round a melee weapon (model space): <see cref="MeleeWeapon.GripFromEnd"/> metres (as held) up from the end opposite the head - the model runs along +Y, head up, so that is its lowest end -
        /// on the handle's own axis, which is the middle of the ring of vertices nearest to that height (the pack's origin is not always on the handle: the crowbar's is a hand's width beside it).
        /// </summary>
        static Vector3 HandleGrip(GameObject prefab, MeleeWeapon.Kind kind)
        {
            var mf = prefab.GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) return Vector3.zero;
            var vs = mf.sharedMesh.vertices;
            var b = mf.sharedMesh.bounds;
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float scale = longest > 0.0001f ? MeleeWeapon.Specs[(int)kind].length / longest : 1f;
            float y = b.min.y + MeleeWeapon.GripFromEnd[(int)kind] / scale;
            float nearest = float.MaxValue, ringY = y;
            foreach (var v in vs) { float d = Mathf.Abs(v.y - y); if (d < nearest) { nearest = d; ringY = v.y; } }
            Bounds ring = default; bool any = false;
            foreach (var v in vs)
            {
                if (Mathf.Abs(v.y - ringY) > 0.004f) continue;
                if (!any) { ring = new Bounds(v, Vector3.zero); any = true; } else ring.Encapsulate(v);
            }
            return any ? new Vector3(ring.center.x, y, ring.center.z) : new Vector3(0f, y, 0f);
        }

        /// <summary>For the QA runs (<c>-executeMethod</c>): fills the catalog with the Synty models now, the same as the first editor start with the pack in, and exits.</summary>
        public static void UpgradeBatch()
        {
            try
            {
                var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
                if (!cat) { Debug.LogError("[Weapons] no catalog"); return; }
                cat.modelsVersion = 0;
                FillSynty(cat, true);
                Debug.Log($"[Weapons] catalog: crowbar {(cat.crowbar ? cat.crowbar.name : "-")}, bat {(cat.bat ? cat.bat.name : "-")}, hatchet {(cat.hatchet ? cat.hatchet.name : "-")}, pew {(cat.pew ? cat.pew.name : "-")}, silencer {(cat.silencer ? cat.silencer.name : "-")}, bullet {(cat.pewBullet ? cat.pewBullet.name : "-")}, version {cat.modelsVersion}");
            }
            finally { EditorApplication.Exit(0); }
        }

        [MenuItem("Employees Take Revenge/Setup Weapons", priority = 31)]
        static void Menu() => Build(true);

        static T Load<T>(string p) where T : Object => AssetDatabase.LoadAssetAtPath<T>(p);

        static void Build(bool log)
        {
            var crowbar = Load<GameObject>(Pack + "Crowbars/crowbar_1.prefab");
            var bat = Load<GameObject>(Pack + "Baseballbats/baseballbat_1.prefab");
            var hatchet = Load<GameObject>(Pack + "Axes/axe_1.prefab");
            var src = Load<AnimationClip>(SlashSrc);
            if (!crowbar || !bat || !hatchet || !src)
            {
                if (log) Debug.LogWarning("Weapon pack or Free Sword Animation pack not found where expected.");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

            var dst = Load<AnimationClip>(ClipPath);
            bool isNew = !dst;
            if (isNew) dst = new AnimationClip();
            dst.ClearCurves();
            dst.frameRate = src.frameRate;
            dst.legacy = false;
            int kept = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                if (string.IsNullOrEmpty(b.path) || b.path.EndsWith("Sword")) continue;
                if (b.propertyName.StartsWith("m_LocalPosition") || b.propertyName.StartsWith("m_LocalScale")) continue;
                var curve = AnimationUtility.GetEditorCurve(src, b);
                var nb = new EditorCurveBinding { path = b.path.Replace("EEJANAIBot:", ""), type = b.type, propertyName = b.propertyName };
                AnimationUtility.SetEditorCurve(dst, nb, curve);
                kept++;
            }
            var settings = AnimationUtility.GetAnimationClipSettings(src);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(dst, settings);
            if (isNew) AssetDatabase.CreateAsset(dst, ClipPath); else EditorUtility.SetDirty(dst);

            var cat = Load<WeaponCatalog>(CatalogPath);
            if (!cat) { cat = ScriptableObject.CreateInstance<WeaponCatalog>(); AssetDatabase.CreateAsset(cat, CatalogPath); }
            cat.crowbar = crowbar; cat.bat = bat; cat.hatchet = hatchet; cat.slash = dst;
            cat.modelsVersion = 0;
            FillGuns(cat, false);
            FillSynty(cat, false);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log($"Weapon catalog ready ({kept} slash curves retargeted).");
        }
    }
}
