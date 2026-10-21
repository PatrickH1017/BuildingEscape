# Round 12, batch G45 (the owner, 2026-10-20): "More assets have been added and the attached images show the 5 characters that should be playable and present in the intro cinematic".
# The five of his pictures: (1) the man in the orange hi-vis vest over a teal shirt with a brown cap = Builder HighVis 01 + a cap, (2) the woman in overalls with the tool belt and a yellow hard hat = Builder Female 01 + a hard hat,
# (3) the bearded man in a tank top and knee pads with an orange hard hat and goggles = Builder Singlet 01 + hard hat with goggles + beard, (4) the older man in the hi-vis harness with a white hard hat and a grey beard = Builder Harness 01 + hard hat + grey beard,
# (5) the woman in the red plaid shirt with glasses and a laptop = Office Developer Female 02. They are the playable dock workers (Dock worker 1 to 5, in that order: the default looks of the five slots, and the five workers of the intro cinematic, which
# cast from PlayerAppearance.DefaultToons); everybody else stays in the catalog (old saved looks, friends' looks) but is not offered. The builders' head attachments (the pack's prefabs leave the heads bare) are the catalog's `dress`: parented to the
# Head bone with no offset (that is how the pack's pivots are made), in the colour theme the owner's pictures show (Textures 01 yellow, 02 orange, 03 blue, 04 white for a hard hat).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor'

Open-Src "$A\CharacterCatalog.cs"
Rep '    [System.Serializable]
    public class ToonCharacter
    {' @'
    /// <summary>A head attachment of a character (a hat, a hard hat, goggles or a beard of the Synty Construction pack): parented to the Head bone with no offset (the pack's pivots are made for that). <see cref="atlas"/> is the colour theme it is drawn in; empty = the character's own atlas.</summary>
    [System.Serializable]
    public class ToonDress
    {
        public GameObject prefab;
        public Texture2D atlas;
    }

    [System.Serializable]
    public class ToonCharacter
    {
'@
Rep '        public Texture2D atlas;
    }

    /// <summary>
    /// The Tiny Toon People characters' @'
        public Texture2D atlas;
        [Tooltip("What this character wears on the head (the Construction builders: the pack's character prefabs leave the heads bare). Empty = nothing.")]
        public ToonDress[] dress;
    }

    /// <summary>
    /// The Tiny Toon People characters'
'@
Save-Src

Open-Src "$A\PlayerAppearance.cs"
Rep '        /// The dock workers (the Synty office people) a player is by default, in slot order: the four characters the owner showed (the man with the cap, the blonde woman in dark grey, the man with glasses' '        /// The dock workers a player is by default, in slot order: since 2026-10-20 the five characters of the owner''s pictures (the hi-vis man with the cap, the woman with the yellow hard hat, the bearded man with the orange hard hat, the older man with the white hard hat, the woman in the plaid shirt);
        /// before that the four characters the owner showed (the man with the cap, the blonde woman in dark grey, the man with glasses'
Rep '        public static readonly string[] DefaultToons = { "syn_cleaner_male_01", "syn_boss_female_01", "syn_developer_male_02", "syn_business_male_02", "syn_cleaner_female_01" };' '        public static readonly string[] DefaultToons = { "syn_builder_highvis_01", "syn_builder_female_01", "syn_builder_singlet_01", "syn_builder_harness_01", "syn_developer_female_02" };'
Save-Src

Open-Src "$A\ToonCharacterFactory.cs"
Rep '            model.transform.localScale = new Vector3(s * look.width, s * look.height, s * look.width);' @'
            model.transform.localScale = new Vector3(s * look.width, s * look.height, s * look.width);
            AddDress(model.transform, ch, cat);                                  // (after the fit: a hard hat must not make the body smaller)
'@
Rep '        /// <summary>Scale that makes the model <see cref="Height"/> metres tall in the host''s space.</summary>' @'
        /// <summary>The character's head attachments (a cap, a hard hat with goggles, a beard): each is parented to the Head bone with no offset and drawn in its own colour theme (the owner, 2026-10-20: the builders of his pictures wear them).</summary>
        static void AddDress(Transform model, ToonCharacter ch, CharacterCatalog cat)
        {
            if (ch.dress == null || ch.dress.Length == 0) return;
            Transform head = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") { head = t; break; }
            if (!head) return;
            foreach (var d in ch.dress)
            {
                if (d == null || !d.prefab) continue;
                var item = Object.Instantiate(d.prefab, head, false);
                item.name = "Dress_" + d.prefab.name;
                item.transform.localPosition = Vector3.zero; item.transform.localRotation = Quaternion.identity; item.transform.localScale = Vector3.one;
                foreach (var c in item.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
                var atlas = d.atlas ? d.atlas : ch.atlas ? ch.atlas : cat.atlas;
                var mat = atlas ? AtlasMaterialFor(atlas) : null;
                if (!mat) continue;
                foreach (var r in item.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
            }
        }

        /// <summary>Scale that makes the model <see cref="Height"/> metres tall in the host''s space.</summary>
'@
Save-Src

Open-Src "$E\CharacterCatalogBuilder.cs"
Rep 'HasFileNameLabels(cat) || StunnedMissing(cat)) Build(false);' 'HasFileNameLabels(cat) || StunnedMissing(cat) || ConstructionStale(cat)) Build(false);'
Rep '        [MenuItem("Employees Take Revenge/Setup Characters", priority = 32)]' @'
        /// <summary>The Construction pack is in and the catalog has no builders, or builders without their hats yet: it is rebuilt on the next editor start.</summary>
        static bool ConstructionStale(CharacterCatalog cat)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Synty/PolygonConstruction")) return false;
            var c = cat.Find("syn_builder_highvis_01");
            return c == null || c.dress == null || c.dress.Length == 0;
        }

        const string AttachmentDir = "Assets/Synty/PolygonConstruction/Prefabs/Characters/Attachments/";
        const string ThemeTexture = "Assets/Synty/PolygonConstruction/Textures/PolygonConstruction_Texture_0{0}_A.png";

        /// <summary>
        /// What the owner's pictures (2026-10-20) show on the builders' heads, which the pack's character prefabs leave bare: the man in the hi-vis vest a brown cap, the woman a yellow hard hat, the bearded man (tank top, knee pads) an orange hard hat with goggles and a brown
        /// beard, the older man a white hard hat and a grey beard. The colour of a hat is the pack's colour theme it is drawn in (Textures 01 yellow, 02 orange, 03 blue, 04 white for a hard hat; a cap is red, blue, green or tan).
        /// </summary>
        static readonly Dictionary<string, (string attachment, int theme)[]> Dress = new Dictionary<string, (string attachment, int theme)[]>
        {
            ["syn_builder_highvis_01"] = new[] { ("SM_Chr_Attach_Hat_01", 4) },
            ["syn_builder_female_01"] = new[] { ("SM_Chr_Attach_Helmet_01", 1) },
            ["syn_builder_singlet_01"] = new[] { ("SM_Chr_Attach_Helmet_Goggles_01", 2), ("SM_Chr_Attach_Beard_01", 1) },
            ["syn_builder_harness_01"] = new[] { ("SM_Chr_Attach_Helmet_01", 4), ("SM_Chr_Attach_Beard_Grey_01", 1) },
        };

        static ToonDress[] DressOf(string id)
        {
            if (!Dress.TryGetValue(id, out var items)) return new ToonDress[0];
            var list = new List<ToonDress>();
            foreach (var (name, theme) in items)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AttachmentDir + name + ".prefab");
                if (!prefab) continue;
                list.Add(new ToonDress { prefab = prefab, atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(string.Format(ThemeTexture, theme)) });
            }
            return list.ToArray();
        }

        [MenuItem("Employees Take Revenge/Setup Characters", priority = 32)]
'@
RepRegion '        /// The four characters the owner showed in four pictures (2026-10-17 night' '        public static readonly string[] DockWorkerIds = { "syn_cleaner_male_01", "syn_boss_female_01", "syn_developer_male_02", "syn_business_male_02" };' @'
        /// The five characters of the owner's pictures (2026-10-20, "the attached images show the 5 characters that should be playable and present in the intro cinematic"): the man in the orange hi-vis vest with the brown cap (Builder HighVis 01), the woman with the
        /// yellow hard hat (Builder Female 01), the bearded man with the orange hard hat and goggles (Builder Singlet 01), the older man with the white hard hat (Builder Harness 01) and the woman in the red plaid shirt (Developer Female 02, of the office pack): the only
        /// ones a player may be, Dock worker 1 to 5 in the order of the pictures, the default looks of the five slots (PlayerAppearance.DefaultToons) and the five workers of the intro cinematic. (Before that: the four characters of 2026-10-17 night, the man with the cap,
        /// the blonde woman in dark grey, the man with glasses and headphones, the man with the red beard and the vest; they and the rest of the office people are still in the catalog, for old saved looks, but are not offered.)
        /// </summary>
        public static readonly string[] DockWorkerIds = { "syn_builder_highvis_01", "syn_builder_female_01", "syn_builder_singlet_01", "syn_builder_harness_01", "syn_developer_female_02" };
'@
Rep '        static ToonRole SyntyRole(string id) => id == IntroContent.BossModelId ? ToonRole.Other : id.Contains("_security_") ? ToonRole.Guard : id.StartsWith("syn_builder_") || id.StartsWith("syn_inspector_") ? ToonRole.Other : ToonRole.Player;' '        static ToonRole SyntyRole(string id) => id == IntroContent.BossModelId ? ToonRole.Other : id.Contains("_security_") ? ToonRole.Guard : System.Array.IndexOf(DockWorkerIds, id) >= 0 ? ToonRole.Player : ToonRole.Other;'
Rep 'role = SyntyRole(sid), clips = sid.Contains("female") ? ToonClipSetId.HumanFemale : ToonClipSetId.HumanMale, atlas = buildAtlas });' 'role = SyntyRole(sid), clips = sid.Contains("female") ? ToonClipSetId.HumanFemale : ToonClipSetId.HumanMale, atlas = buildAtlas, dress = DressOf(sid) });'
Rep '// the Construction people (the owner, 2026-10-20): the builders and inspectors of the Synty Construction pack; not offered until the owner''s five are picked (SyntyRole)' '// the Construction people (the owner, 2026-10-20): the builders and inspectors of the Synty Construction pack; four of the owner''s five are builders (they wear their hats: DressOf)'
Save-Src

Open-Src "$E\NetDev\NetDevR4Test.cs"
Rep 'bool allOffice = players.Count >= 12, atlas = true' 'bool allOffice = players.Count == 5, atlas = true'
Rep 'Check($"dock workers: the {players.Count} playable characters are all Synty office people with their own atlas", allOffice && atlas);' 'Check($"dock workers: the {players.Count} playable characters (the owner''s five, 2026-10-20) are Synty people (the Construction builders, the office developer) with their own atlas", allOffice && atlas);'
RepRegion '            bool four = players.Count >= 4;' 'and the first four defaults", four);' @'
            bool five = players.Count == 5;
            for (int i = 0; five && i < 5; i++) five &= players[i].id == CharacterCatalogBuilder.DockWorkerIds[i] && PlayerAppearance.DefaultToons[i] == players[i].id;
            Check("dock workers: Dock worker 1-5 are the five characters of the owner's pictures (hi-vis vest and cap, yellow hard hat, bearded man with goggles, older man with the white hard hat, plaid shirt) and the five defaults", five);
            var dressCounts = new[] { 1, 1, 2, 2, 0 };
            bool dressed = five;
            for (int i = 0; dressed && i < 5; i++) dressed &= (players[i].dress == null ? 0 : players[i].dress.Count(d => d != null && d.prefab != null && d.atlas != null)) == dressCounts[i];
            Check("dock workers: the four builders wear what the pictures show (a cap, a hard hat, a hard hat with goggles and a beard, a hard hat and a grey beard), each in its own colour theme; the developer wears nothing extra", dressed);
'@
RepRegion '            Check("dock workers: the security guards are not offered to players' 'players.Any(p => p.id == "syn_developer_male_01"));' @'
            Check("dock workers: exactly the owner's five are offered; the security guards and the pack's other people stay in the catalog (old saved looks, friends' looks) but are not offered", players.Count == 5 && !players.Any(p => p.id.Contains("security")) && cat.Find("syn_security_male_01") != null && cat.Find("syn_security_female_01") != null
                && cat.Find("syn_business_female_01") != null && !players.Any(p => p.id == "syn_business_female_01") && cat.Find("syn_cleaner_male_01") != null && !players.Any(p => p.id == "syn_cleaner_male_01") && cat.Find("syn_builder_overalls_01") != null && !players.Any(p => p.id == "syn_builder_overalls_01"));
'@
Save-Src

Open-Src "$E\NetDev\NetDevToon.cs"
Rep '            Log($"[Toon] DONE fails={toonFails}");' @'
            // ---- 7. the owner's five dock workers (2026-10-20): playable in his order, the builders wearing what his pictures show on their heads
            var five = new[] { "syn_builder_highvis_01", "syn_builder_female_01", "syn_builder_singlet_01", "syn_builder_harness_01", "syn_developer_female_02" };
            var wantDress = new[] { 1, 1, 2, 2, 0 };
            ToonCheck("the playable characters are exactly the owner's five, in his order", cat.Players().Select(p => p.id).SequenceEqual(five), $"({string.Join(", ", cat.Players().Select(p => p.id))})");
            foreach (var h in hosts) Destroy(h);
            yield return null;
            var fiveHosts = new List<GameObject>();
            for (int i = 0; i < five.Length; i++)
            {
                var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                host.name = "Five_" + five[i];
                host.transform.SetParent(root.transform, false);
                host.transform.localPosition = new Vector3((i - 2) * 1.15f, 0f, 0f);
                host.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Object.Destroy(host.GetComponent<Collider>());
                var fa = PartyCharacterFactory.Attach(host, new CharacterLook(five[i], Color.white, 1, null, 1f, 1f, five[i]));
                fiveHosts.Add(host);
                var worn = host.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Dress_")).ToList();
                var ch5 = cat.Find(five[i]);
                bool onHead = worn.All(t => t.parent != null && t.parent.name == "Head");
                bool theme = worn.Count == 0 || (ch5.dress != null && worn.All(t => { var r = t.GetComponent<Renderer>(); return r && r.sharedMaterial && ch5.dress.Any(d => d.atlas && r.sharedMaterial.mainTexture == d.atlas); }));
                ToonCheck($"{five[i]}: dressed as a dock worker, wearing {wantDress[i]} head item(s) on the Head bone in their own colour theme", fa != null && worn.Count == wantDress[i] && onHead && theme, $"(worn {string.Join(", ", worn.Select(t => t.name))})");
            }
            yield return new WaitForSeconds(0.9f);
            cam.fieldOfView = 30f;
            cam.transform.localPosition = new Vector3(0f, 0.35f, -6.4f);
            cam.transform.LookAt(root.transform.position + new Vector3(0f, 0.05f, 0f));
            yield return null;
            ShotCam(cam, "toon_five");
            foreach (var h in fiveHosts) Destroy(h);

            Log($"[Toon] DONE fails={toonFails}");
'@
Save-Src
Write-Output 'round 12 batch G45 patched'
