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
                if (!cat && System.IO.File.Exists(AssetPath)) return;      // (a catalog that exists but does not load: its script is not imported yet - rebuilding now would overwrite it with a blank one)
                if (!cat || cat.characters.Length == 0 || !cat.atlas || !cat.male.idle || HasFileNameLabels(cat)) Build(false);
            };
        }

        /// <summary>An earlier version named players after their files ("Female", "Male A"); those are renamed on the next build.</summary>
        static bool HasFileNameLabels(CharacterCatalog cat) =>
            cat.characters.Any(c => c != null && c.role == ToonRole.Player && (string.IsNullOrEmpty(c.label) || c.label == LabelOf(c.id)));

        [MenuItem("Employees Take Revenge/Setup Characters", priority = 32)]
        static void Menu() => Build(true);

        /// <summary>For the batch-mode tests.</summary>
        internal static void BuildNow(bool log) => Build(log);

        /// <summary>Batch entry point: <c>-executeMethod BuildingEscape.EditorTools.CharacterCatalogBuilder.BuildBatch</c>.</summary>
        public static void BuildBatch() { Build(true); EditorApplication.Exit(0); }

        const string SyntyCharacters = "Assets/Synty/PolygonOffice/Prefabs/Characters";

        /// <summary>The Synty office pack's colour atlas: read from the serialized data of its material (the material's own Shader Graph shader is missing in this pipeline, so its properties cannot be asked for).</summary>
        static Texture2D SyntyAtlas()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Synty/PolygonOffice")) return null;
            var path = AssetDatabase.FindAssets("PolygonOffice_01_A t:Material", new[] { "Assets/Synty/PolygonOffice" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "PolygonOffice_01_A");
            var mat = path != null ? AssetDatabase.LoadAssetAtPath<Material>(path) : null;
            if (!mat) return null;
            var envs = new SerializedObject(mat).FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; envs != null && i < envs.arraySize; i++)
            {
                var e = envs.GetArrayElementAtIndex(i);
                if (e.FindPropertyRelative("first").stringValue == "_Albedo_Map") return e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture2D;
            }
            return null;
        }

        /// <summary>
        /// The four characters the owner showed in four pictures (2026-10-17 night, "The Dock workers should be these character models"): the man with the cap (Cleaner Male 01), the blonde woman in dark grey
        /// (Boss Female 01), the man with glasses and headphones (Developer Male 02), the man with the red beard and the vest (Business Male 02). In the order of the pictures they are Dock worker 1 to 4
        /// and the first four default looks (PlayerAppearance.DefaultToons).
        /// </summary>
        public static readonly string[] DockWorkerIds = { "syn_cleaner_male_01", "syn_boss_female_01", "syn_developer_male_02", "syn_business_male_02" };

        /// <summary>
        /// Who of the Synty people a player may be: everybody but the security guards (the owner: "let people choose between most of them for now (not officers of course)": a player must not look like
        /// a guard) and the boss of the cutscene (a player must not be him).
        /// </summary>
        static ToonRole SyntyRole(string id) => id == IntroContent.BossModelId ? ToonRole.Other : id.Contains("_security_") ? ToonRole.Guard : ToonRole.Player;

        /// <summary>"SM_Chr_Business_Female_01" -> "syn_business_female_01" (the Synty people; attachments are not people).</summary>
        static string SyntyId(string prefabName)
        {
            var n = prefabName.ToLowerInvariant();
            return n.StartsWith("sm_chr_") ? "syn_" + n.Substring(7) : null;
        }

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

        const string HumanRoot = "Assets/Kevin Iglesias/Human Animations/Animations";

        /// <summary>The basic movement of Kevin Iglesias' Human Animations (the non-root-motion clips: the capsule moves, not the clip). Empty when the pack is absent.</summary>
        static ToonClipSet ReadHuman(string g)
        {
            string dir = HumanRoot + (g == "F" ? "/Female" : "/Male");
            AnimationClip Get(string sub, string name)
            {
                var path = $"{dir}/{sub}/Human{g}@{name}.fbx";
                if (!File.Exists(path)) return null;
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            }
            return new ToonClipSet
            {
                idle = Get("Idles", "Idle01"),
                walk = Get("Movement/Walk", "Walk01_Forward"),
                run = Get("Movement/Run", "Run01_Forward"),
                jump = Get("Movement/Jump", "Jump01"),
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

        /// <summary>"Toon 3" style names this builder generated itself (so they may be renumbered).</summary>
        static bool IsPlaceholderLabel(string label) => label.StartsWith("Toon ") && int.TryParse(label.Substring(5), out _);

        static ToonRole RoleOf(string id)
        {
            if (id.Contains("police") || id.Contains("cop") || id.Contains("guard") || id.Contains("officer")
                || id.Contains("swat") || id.Contains("security") || id.Contains("sheriff")) return ToonRole.Guard;
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

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var cat = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(AssetPath);
            bool isNew = !cat;
            if (isNew) cat = ScriptableObject.CreateInstance<CharacterCatalog>();

            // A character already in the catalog keeps what was edited on it (label, role, clip set); only entries
            // still carrying the generated defaults are refreshed.
            var existing = new Dictionary<string, ToonCharacter>();
            foreach (var old in cat.characters) if (old != null && !string.IsNullOrEmpty(old.id)) existing[old.id] = old;

            var list = new List<ToonCharacter>();
            foreach (var path in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                var id = IdOf(prefab.name);
                if (!CharacterCatalog.ValidId(id) || list.Any(c => c.id == id)) continue;
                var entry = new ToonCharacter { id = id, label = null, prefab = prefab, role = RoleOf(id), clips = SetOf(id) };
                if (existing.TryGetValue(id, out var old))
                {
                    entry.role = old.role; entry.clips = old.clips;
                    // keep a hand-edited label; the generated ones ("Male A", "Toon 3") are regenerated below
                    if (!string.IsNullOrEmpty(old.label) && old.label != LabelOf(id) && !IsPlaceholderLabel(old.label)) entry.label = old.label;
                }
                list.Add(entry);
            }
            // players first (pick order in Customize), then guards, then the rest
            // the Synty office people (the owner, 2026-10-17 night: "completely change the playable characters to those office asset pack characters"): they are the players; the Tiny Toon
            // people that used to be players stay in the catalog (old saved looks still resolve, the guards and the zombie need theirs) but are no longer offered in Customize
            var syntyAtlas = SyntyAtlas();
            var synty = new List<ToonCharacter>();
            if (syntyAtlas != null && AssetDatabase.IsValidFolder(SyntyCharacters))
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SyntyCharacters }))
                {
                    var spath = AssetDatabase.GUIDToAssetPath(guid);
                    var sprefab = AssetDatabase.LoadAssetAtPath<GameObject>(spath);
                    var sid = sprefab ? SyntyId(sprefab.name) : null;
                    if (sid == null || sid.Contains("attachment") || !CharacterCatalog.ValidId(sid) || synty.Any(c => c.id == sid)) continue;
                    synty.Add(new ToonCharacter { id = sid, label = null, prefab = sprefab, role = SyntyRole(sid), clips = sid.Contains("female") ? ToonClipSetId.HumanFemale : ToonClipSetId.HumanMale, atlas = syntyAtlas });
                }
            if (synty.Count > 0) foreach (var c in list) if (c.role == ToonRole.Player || (c.role == ToonRole.Guard && synty.Any(s => s.role == ToonRole.Guard))) c.role = ToonRole.Other;      // (the officers are the Synty security people when the pack is there)
            list.AddRange(synty);
            // (the four dock workers the owner showed come first, in the order of his pictures; then everybody else by id)
            list = list.OrderBy(c => c.role).ThenBy(c => { int at = System.Array.IndexOf(DockWorkerIds, c.id); return at < 0 ? int.MaxValue : at; }).ThenBy(c => c.id).ToList();
            // One plain list for Customize: players are "Toon 1, Toon 2, ..." until the owner names them in the asset.
            int playerNumber = 0;
            foreach (var c in list)
            {
                if (c.role == ToonRole.Player) playerNumber++;
                if (c.role == ToonRole.Player && c.atlas) c.label = "Dock worker " + playerNumber;            // (the Synty people; the runtime names them the same way: CharacterCatalog.DockLabel)
                else if (string.IsNullOrEmpty(c.label)) c.label = c.role == ToonRole.Player ? $"Toon {playerNumber}" : LabelOf(c.id);
            }
            cat.characters = list.ToArray();
            cat.atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            cat.male = ReadSet(fbx, "m");
            cat.female = ReadSet(fbx, "f");
            cat.zombie = ReadSet(fbx, "z");
            cat.humanMale = ReadHuman("M");
            cat.humanFemale = ReadHuman("F");
            if (isNew) AssetDatabase.CreateAsset(cat, AssetPath); else EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log)
                Debug.Log($"Character catalog ready: {string.Join(", ", list.Select(c => $"{c.id} ({c.role})"))}; "
                    + $"male idle={(cat.male.idle ? "ok" : "MISSING")} run={(cat.male.run ? "ok" : "MISSING")} death={(cat.male.death ? "ok" : "MISSING")}");
        }
    }
}
