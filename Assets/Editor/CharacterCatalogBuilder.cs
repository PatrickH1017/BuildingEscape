using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Creates Assets/Resources/CharacterCatalog.asset from the Tiny Toon People pack: every prefab in a
    /// "prefabs" folder becomes a character, and the animation FBXs fill the male / female / zombie clip sets.
    /// Role and clip set come from the prefab name (police = guard, zombie = other, "female" = female clips),
    /// so adding more characters to the pack and running the setup again is enough.
    /// </summary>
    [InitializeOnLoad]
    public static class CharacterCatalogBuilder
    {
        const string PackRoot = "Assets/ToonyTinyPeople";
        const string AssetPath = "Assets/Resources/CharacterCatalog.asset";

        static CharacterCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                var cat = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(AssetPath);
                if (!cat || cat.characters.Length == 0 || !cat.atlas || !cat.male.idle) Build(false);
            };
        }

        [MenuItem("Overtime Escape/Setup Characters", priority = 32)]
        static void Menu() => Build(true);

        /// <summary>For the batch-mode tests.</summary>
        internal static void BuildNow(bool log) => Build(log);

        static AnimationClip ClipIn(Dictionary<string, string> fbx, string baseName)
        {
            if (!fbx.TryGetValue(baseName, out var path)) return null;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        static ToonClipSet ReadSet(Dictionary<string, string> fbx, string p)
        {
            return new ToonClipSet
            {
                idle = ClipIn(fbx, p + "_idle_a"),
                walk = ClipIn(fbx, p + "_walk"),
                run = ClipIn(fbx, p + "_run"),
                death = ClipIn(fbx, p + "_death_a"),
                attack = ClipIn(fbx, p + "_melee_combat_attack_a") ?? ClipIn(fbx, p + "_attack_a"),
                combatIdle = ClipIn(fbx, p + "_melee_combat_idle"),
                combatRun = ClipIn(fbx, p + "_melee_combat_run"),
                pistolIdle = ClipIn(fbx, p + "_pistol_idle_a"),
                pistolRun = ClipIn(fbx, p + "_pistol_run"),
                pistolShoot = ClipIn(fbx, p + "_pistol_shoot"),
            };
        }

        /// <summary>"TT_demo_male_A" -> "male_a": lower case letters, digits and underscores only.</summary>
        static string IdOf(string prefabName)
        {
            var n = prefabName.ToLowerInvariant();
            foreach (var prefix in new[] { "tt_demo_", "tt_" })
                if (n.StartsWith(prefix)) { n = n.Substring(prefix.Length); break; }
            var sb = new System.Text.StringBuilder();
            foreach (char c in n) sb.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_');
            return sb.ToString().Trim('_');
        }

        static string LabelOf(string id)
        {
            var words = id.Split('_').Where(w => w.Length > 0).Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1));
            return string.Join(" ", words);
        }

        static ToonRole RoleOf(string id)
        {
            if (id.Contains("police") || id.Contains("cop") || id.Contains("guard") || id.Contains("officer")) return ToonRole.Guard;
            if (id.Contains("zombie")) return ToonRole.Other;
            return ToonRole.Player;
        }

        static ToonClipSetId SetOf(string id)
        {
            if (id.Contains("zombie")) return ToonClipSetId.Zombie;
            if (id.Contains("female") || id.StartsWith("f_")) return ToonClipSetId.Female;
            return ToonClipSetId.Male;
        }

        static void Build(bool log)
        {
            if (!AssetDatabase.IsValidFolder(PackRoot)) { if (log) Debug.LogWarning("Tiny Toon People pack not found under " + PackRoot); return; }

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PackRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.ToLowerInvariant().Contains("/prefabs/"))
                .OrderBy(p => p)
                .ToList();
            var fbx = new Dictionary<string, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PackRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.ToLowerInvariant().Contains("/animation/")) continue;
                fbx[Path.GetFileNameWithoutExtension(path).ToLowerInvariant()] = path;
            }
            var atlasPath = AssetDatabase.FindAssets("t:Texture2D", new[] { PackRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.ToLowerInvariant().Contains("/models/"));
            if (prefabs.Count == 0 || fbx.Count == 0 || atlasPath == null)
            {
                if (log) Debug.LogWarning($"Tiny Toon People pack is not ready yet (prefabs {prefabs.Count}, animation files {fbx.Count}, atlas {(atlasPath ?? "none")}).");
                return;
            }

            var list = new List<ToonCharacter>();
            foreach (var path in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                var id = IdOf(prefab.name);
                if (!CharacterCatalog.ValidId(id) || list.Any(c => c.id == id)) continue;
                list.Add(new ToonCharacter { id = id, label = LabelOf(id), prefab = prefab, role = RoleOf(id), clips = SetOf(id) });
            }
            // players first (pick order in Customize), then guards, then the rest
            list = list.OrderBy(c => c.role).ThenBy(c => c.id).ToList();

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var cat = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(AssetPath);
            bool isNew = !cat;
            if (isNew) cat = ScriptableObject.CreateInstance<CharacterCatalog>();
            cat.characters = list.ToArray();
            cat.atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            cat.male = ReadSet(fbx, "m");
            cat.female = ReadSet(fbx, "f");
            cat.zombie = ReadSet(fbx, "z");
            if (isNew) AssetDatabase.CreateAsset(cat, AssetPath); else EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log)
                Debug.Log($"Character catalog ready: {string.Join(", ", list.Select(c => $"{c.id} ({c.role})"))}; "
                    + $"male idle={(cat.male.idle ? "ok" : "MISSING")} run={(cat.male.run ? "ok" : "MISSING")} death={(cat.male.death ? "ok" : "MISSING")}");
        }
    }
}
