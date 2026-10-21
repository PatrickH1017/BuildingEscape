# Round 12, batch G44 (the owner, 2026-10-20): "More assets have been added and the attached images show the 5 characters that should be playable and present in the intro cinematic".
# Step 1 of the five characters: the catalog builder also reads the Synty CONSTRUCTION pack's people (Assets/Synty/PolygonConstruction/Prefabs/Characters, one shared Humanoid rig,
# its own colour atlas), as "Other" (not offered yet), and a candidate sheet scenario (CharSheet) renders them (and the office people) in threes so the five of the owner's pictures can be picked by eye.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$E = 'C:\bqm\Assets\Editor'

Open-Src "$E\CharacterCatalogBuilder.cs"
Rep @'
        static Texture2D SyntyAtlas()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Synty/PolygonOffice")) return null;
            var path = AssetDatabase.FindAssets("PolygonOffice_01_A t:Material", new[] { "Assets/Synty/PolygonOffice" }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "PolygonOffice_01_A");
'@ @'
        static Texture2D SyntyAtlas(string folder = "Assets/Synty/PolygonOffice", string material = "PolygonOffice_01_A")
        {
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            var path = AssetDatabase.FindAssets(material + " t:Material", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == material);
'@
Rep '        const string SyntyCharacters = "Assets/Synty/PolygonOffice/Prefabs/Characters";' @'
        const string SyntyCharacters = "Assets/Synty/PolygonOffice/Prefabs/Characters";
        /// <summary>The Synty Construction pack's people (the owner, 2026-10-20: "More assets have been added"): one Humanoid rig, one colour atlas, the characters dressed as the pack makes them (hard hats, caps, vests).</summary>
        const string ConstructionCharacters = "Assets/Synty/PolygonConstruction/Prefabs/Characters";
'@
Rep @'
            if (synty.Count > 0) foreach (var c in list) if (c.role == ToonRole.Player
'@ @'
            // the Construction people (the owner, 2026-10-20): the builders and inspectors of the Synty Construction pack; not offered until the owner's five are picked (SyntyRole)
            var buildAtlas = SyntyAtlas("Assets/Synty/PolygonConstruction", "PolygonConstruction_01_A");
            if (buildAtlas != null && AssetDatabase.IsValidFolder(ConstructionCharacters))
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ConstructionCharacters }))
                {
                    var spath = AssetDatabase.GUIDToAssetPath(guid);
                    var sprefab = AssetDatabase.LoadAssetAtPath<GameObject>(spath);
                    var sid = sprefab ? SyntyId(sprefab.name) : null;
                    if (sid == null || sid.Contains("attach") || !CharacterCatalog.ValidId(sid) || synty.Any(c => c.id == sid)) continue;
                    synty.Add(new ToonCharacter { id = sid, label = null, prefab = sprefab, role = SyntyRole(sid), clips = sid.Contains("female") ? ToonClipSetId.HumanFemale : ToonClipSetId.HumanMale, atlas = buildAtlas });
                }
            if (synty.Count > 0) foreach (var c in list) if (c.role == ToonRole.Player
'@
Rep '        static ToonRole SyntyRole(string id) => id == IntroContent.BossModelId ? ToonRole.Other : id.Contains("_security_") ? ToonRole.Guard : ToonRole.Player;' '        static ToonRole SyntyRole(string id) => id == IntroContent.BossModelId ? ToonRole.Other : id.Contains("_security_") ? ToonRole.Guard : id.StartsWith("syn_builder_") || id.StartsWith("syn_inspector_") ? ToonRole.Other : ToonRole.Player;'
Save-Src

# the harness: a public entry and a mode
Open-Src "$E\NetDev\NetDevHarness.cs"
Rep @'
        public static void GunSheet()
        {
'@ @'
        public static void CharSheet()
        {
            SessionState.SetString("netdev.mode", "charsheet");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunSheet()
        {
'@
Rep '                case "gunsheet": yield return GunSheetScenario(); break;' @'
                case "charsheet": yield return CharSheetScenario(); break;
                case "gunsheet": yield return GunSheetScenario(); break;
'@
Save-Src
Write-Output 'round 12 batch G44 patched'
