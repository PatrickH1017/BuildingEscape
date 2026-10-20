# Round 4: the Synty sheet tool builds its atlas material AFTER the new scene is made (a scene change unloads unused assets and killed the material made before it),
# and can force a re-import of the pack's shader graphs (they failed once, when the QA tree sat under a path that was too long for the Shader Graph package).
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\SyntySheet.cs')
Rep '            Sheet(chars, Path.Combine(dir, "synty_chars_native.png"), null, 6, 256);
            if (atlas) Sheet(chars, Path.Combine(dir, "synty_chars_atlas.png"), ToonCharacterFactory.AtlasMaterialFor(atlas), 6, 256);' '            if (System.Environment.GetEnvironmentVariable("SYNTY_REIMPORT") == "1") ReimportShaderGraphs();
            Sheet(chars, Path.Combine(dir, "synty_chars_native.png"), null, 6, 256);
            if (atlas) Sheet(chars, Path.Combine(dir, "synty_chars_atlas.png"), atlas, 6, 256);'
Rep '        static void Sheet(List<string> paths, string file, Material forceMaterial, int cols, int tile)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);' '        /// <summary>Imports the pack''s shader graphs again, sub graphs first, and says how each one came out.</summary>
        public static void ReimportShaderGraphs()
        {
            var graphs = AssetDatabase.FindAssets("", new[] { "Assets/Synty" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".shadergraph") || p.EndsWith(".shadersubgraph")).OrderBy(p => p.EndsWith(".shadersubgraph") ? 0 : 1).ToList();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var p in graphs) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log($"[Synty] re-imported {graphs.Count} shader graphs in {watch.Elapsed.TotalSeconds:0} s");
            foreach (var p in graphs.Where(g => g.EndsWith(".shadergraph")))
            {
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(p);
                Debug.Log($"[Synty] graph {Path.GetFileNameWithoutExtension(p)}: {(sh ? sh.name + (sh.isSupported ? " supported" : " NOT SUPPORTED") + (ShaderUtil.ShaderHasError(sh) ? " HAS ERRORS" : "") : "no shader")}");
            }
        }

        static void Sheet(List<string> paths, string file, Texture2D forceAtlas, int cols, int tile)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var forceMaterial = forceAtlas ? ToonCharacterFactory.AtlasMaterialFor(forceAtlas) : null;'
Save-Src
Write-Output 'sheet fix patched'
