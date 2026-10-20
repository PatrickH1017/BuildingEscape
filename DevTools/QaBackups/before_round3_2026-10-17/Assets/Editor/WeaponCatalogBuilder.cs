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

        static WeaponCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
                if (!cat) Build(false);
                else if (!cat.pew || !cat.pewBullet) FillGuns(cat, false);
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
            FillGuns(cat, false);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log($"Weapon catalog ready ({kept} slash curves retargeted).");
        }
    }
}
