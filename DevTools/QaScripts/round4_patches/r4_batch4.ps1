# Round 4, batch 4 (A6 + A8, first slice): the Synty office people are registered in the CharacterCatalog as the players ("Dock worker N"), the factory dresses them with their own atlas,
# the Customize list shows only them. The defaults, the cutscene cast and the tests follow in batch 5. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\CharacterCatalog.cs')
Rep '        public ToonRole role;
        public ToonClipSetId clips;
    }' @'
        public ToonRole role;
        public ToonClipSetId clips;
        [Tooltip("This character's own colour atlas (the Synty office people); empty = the catalog's atlas (the Tiny Toon people).")]
        public Texture2D atlas;
    }
'@
Rep '        /// <summary>Is this a safe id to put in a look string?</summary>' @'
        /// <summary>What Customize calls a character: the players are loading dock workers ("Dock worker 1", "Dock worker 2" ... in catalog order; the owner, 2026-10-17 night: it helps players understand they are
        /// roleplaying as one), everybody else keeps the label they have.</summary>
        public string DockLabel(ToonCharacter c)
        {
            if (c == null) return "";
            if (c.role == ToonRole.Player)
            {
                int n = 0;
                foreach (var p in characters)
                {
                    if (p == null || !p.prefab || p.role != ToonRole.Player) continue;
                    n++;
                    if (p == c || p.id == c.id) return "Dock worker " + n;
                }
            }
            return string.IsNullOrEmpty(c.label) ? c.id : c.label;
        }

        /// <summary>Is this a safe id to put in a look string?</summary>
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\ToonCharacterFactory.cs')
Rep '            var mat = AtlasMaterial(cat);' '            var mat = ch.atlas ? AtlasMaterialFor(ch.atlas) : AtlasMaterial(cat);'
Rep '        /// <summary>The pack''s single Standard-shader material (colour atlas, matt).</summary>' @'
        static readonly System.Collections.Generic.Dictionary<Texture2D, Material> atlasMats = new System.Collections.Generic.Dictionary<Texture2D, Material>();

        /// <summary>A Standard material with one character's own atlas (the Synty people: their Shader Graph material is pink in this pipeline).</summary>
        static Material AtlasMaterialFor(Texture2D atlas)
        {
            if (atlasMats.TryGetValue(atlas, out var m) && m) return m;
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (!sh) return null;
            m = new Material(sh) { name = "Atlas_" + atlas.name, mainTexture = atlas, color = Color.white };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            atlasMats[atlas] = m;
            return m;
        }

        /// <summary>The pack's single Standard-shader material (colour atlas, matt).</summary>
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\CharacterCatalogBuilder.cs')
Rep '        /// <summary>For the batch-mode tests.</summary>
        internal static void BuildNow(bool log) => Build(log);' @'
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

        /// <summary>"SM_Chr_Business_Female_01" -> "syn_business_female_01" (the Synty people; attachments are not people; the bosses are not dock workers).</summary>
        static string SyntyId(string prefabName)
        {
            var n = prefabName.ToLowerInvariant();
            return n.StartsWith("sm_chr_") ? "syn_" + n.Substring(7) : null;
        }
'@
Rep '            list = list.OrderBy(c => c.role).ThenBy(c => c.id).ToList();' @'
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
                    synty.Add(new ToonCharacter { id = sid, label = null, prefab = sprefab, role = sid.Contains("_boss_") ? ToonRole.Other : ToonRole.Player, clips = sid.Contains("female") ? ToonClipSetId.Female : ToonClipSetId.Male, atlas = syntyAtlas });
                }
            if (synty.Count > 0) foreach (var c in list) if (c.role == ToonRole.Player) c.role = ToonRole.Other;
            list.AddRange(synty);
            list = list.OrderBy(c => c.role).ThenBy(c => c.id).ToList();
'@
Rep '                if (string.IsNullOrEmpty(c.label)) c.label = c.role == ToonRole.Player ? $"Toon {playerNumber}" : LabelOf(c.id);' '                if (c.role == ToonRole.Player && c.atlas) c.label = "Dock worker " + playerNumber;            // (the Synty people; the runtime names them the same way: CharacterCatalog.DockLabel)
                else if (string.IsNullOrEmpty(c.label)) c.label = c.role == ToonRole.Player ? $"Toon {playerNumber}" : LabelOf(c.id);'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Net\LobbyMenu.cs')
Rep '                toonNames = new string[toons.Count + 1];
                toonNames[0] = "PARTY";
                for (int i = 0; i < toons.Count; i++) toonNames[i + 1] = (toons[i].label ?? toons[i].id).ToUpperInvariant();' '                toonNames = new string[toons.Count];          // (no PARTY choice any more: every player is a dock worker; the Party character is only the fallback of a machine without the pack)
                for (int i = 0; i < toons.Count; i++) toonNames[i] = catalog.DockLabel(toons[i]).ToUpperInvariant();'
Rep '            if (toonNames != null && toons != null && toons.Count > 0 && toonNames.Length == toons.Count + 1)' '            if (toonNames != null && toons != null && toons.Count > 0 && toonNames.Length == toons.Count)'
Rep '                int current = 0;
                for (int i = 0; i < toons.Count; i++) if (edit.model == toons[i].id) current = i + 1;
                int pick = MenuKit.Choice(f, toonNames, current, 3, HudTheme.Quota, 42f, 16f);
                if (pick != current) { edit.model = pick == 0 ? null : toons[pick - 1].id; changed = true; }' '                int current = 0;
                for (int i = 0; i < toons.Count; i++) if (edit.model == toons[i].id) current = i;
                int pick = MenuKit.Choice(f, toonNames, current, 3, HudTheme.Quota, 42f, 16f);
                if (pick != current || edit.model != toons[pick].id) { edit.model = toons[pick].id; changed = true; }'
Save-Src
Write-Output 'round 4 batch 4 patched'
