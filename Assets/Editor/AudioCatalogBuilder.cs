using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Builds Assets/Resources/AudioCatalog.asset from the Footsteps and Deadly Kombat packs.</summary>
    [InitializeOnLoad]
    public static class AudioCatalogBuilder
    {
        const string CatalogPath = "Assets/Resources/AudioCatalog.asset";

        static AudioCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                var cat = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
                if (!cat) Build(false);
                else if (cat.gunShot == null || cat.gunShot.Length == 0) { FillGuns(cat); EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets(); }
            };
        }

        /// <summary>The pew's sounds from the Free Weapon SFX pack (Handgun): shots with their tails, the five steps of a reload, the draw.</summary>
        static void FillGuns(AudioCatalog cat)
        {
            cat.gunShot = Clips("sfx_wpn_hgn_gunshot_01", "sfx_wpn_hgn_gunshot_02", "sfx_wpn_hgn_gunshot_03");
            cat.gunTail = Clips("sfx_wpn_hgn_tail_01", "sfx_wpn_hgn_tail_02");
            cat.gunReload = Clips("sfx_wpn_hgn_reload_01_slideLock", "sfx_wpn_hgn_reload_02_magOut", "sfx_wpn_hgn_reload_03_magDraw",
                                  "sfx_wpn_hgn_reload_04_magIn", "sfx_wpn_hgn_reload_05_charge");
            cat.gunEquip = Clips("sfx_wpn_hgn_foley_equip_01", "sfx_wpn_hgn_foley_equip_02");
            cat.gunAim = Clips("sfx_wpn_hgn_foley_aim_01", "sfx_wpn_hgn_foley_aim_02");
        }

        [MenuItem("Overtime Escape/Setup Audio", priority = 32)]
        static void Menu() => Build(true);

        static AudioClip[] Clips(params string[] names)
        {
            var list = new List<AudioClip>();
            foreach (var n in names)
            {
                foreach (var guid in AssetDatabase.FindAssets(n + " t:AudioClip"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant() != n.ToLowerInvariant()) continue;
                    var c = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (c) { list.Add(c); break; }
                }
            }
            return list.ToArray();
        }

        static void Build(bool log)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var cat = AssetDatabase.LoadAssetAtPath<AudioCatalog>(CatalogPath);
            if (!cat) { cat = ScriptableObject.CreateInstance<AudioCatalog>(); AssetDatabase.CreateAsset(cat, CatalogPath); }

            cat.stepsPlayer = Clips("Footstep_Shoe_01", "Footstep_Shoe_02", "Footstep_Shoe_03", "Footstep_Shoe_04", "Footstep_Shoe_05");
            cat.stepsGuard = Clips("Footstep_Boots_01", "Footstep_Boots_02", "Footstep_Boots_03", "Footstep_Boots_04",
                                   "Footstep_Boots_05", "Footstep_Boots_06", "Footstep_Boots_07");
            cat.whoosh = Clips("punch_short_whoosh_16", "punch_short_whoosh_30");
            cat.hitBat = Clips("body_hit_large_32", "body_hit_large_44", "body_hit_large_76");
            cat.hitCrowbar = Clips("metal_punch_06", "body_hit_large_44");
            cat.hitHatchet = Clips("blade_hit_07", "blade_hit_08");
            cat.killBat = Clips("wood_bat_finisher_01", "wood_bat_finisher_05");
            cat.killCrowbar = Clips("metal_punch_finisher_07", "body_hit_finisher_23");
            cat.killHatchet = Clips("blade_hit_08", "body_hit_finisher_42");
            cat.layerBone = Clips("bone_breaking_03", "bone_breaking_53");
            cat.layerGore = Clips("guts_and_gore_59", "guts_and_gore_68");
            FillGuns(cat);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log($"Audio catalog ready: {cat.stepsPlayer.Length} player steps, {cat.stepsGuard.Length} guard steps, " +
                               $"{cat.killBat.Length + cat.killCrowbar.Length + cat.killHatchet.Length} kill sounds.");
        }
    }
}
