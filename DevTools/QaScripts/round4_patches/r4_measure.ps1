# Round 4 (A7): a measuring tool for the Synty prefabs: size (x y z), where the middle of the bounds lies against the pivot, the lowest point, the renderer count.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\SyntySheet.cs')
Rep '        /// <summary>Imports the pack''s shader graphs again, sub graphs first, and says how each one came out.</summary>' '        /// <summary>
        /// Measures every prefab under the pack''s Props / Buildings / Generic folders (size, offset of the bounds'' middle from the pivot, lowest point, renderers) into the tab separated file
        /// synty_measure.tsv in SYNTY_SHEET_DIR: the numbers PropTable needs (batch mode, no graphics needed).
        /// </summary>
        public static void Measure()
        {
            var dir = System.Environment.GetEnvironmentVariable("SYNTY_SHEET_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
            var all = AssetDatabase.FindAssets("t:Prefab", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sb = new System.Text.StringBuilder("path\tsx\tsy\tsz\tcx\tcz\tminY\trenderers\n");
            foreach (var p in all)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!prefab) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var rs = inst.GetComponentsInChildren<Renderer>(true).Where(x => x && !(x is ParticleSystemRenderer)).ToArray();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
                    sb.Append(p.Substring(Root.Length + 1)).Append("\t").Append(b.size.x.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(b.size.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t")
                      .Append(b.size.z.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(b.center.x.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(b.center.z.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t")
                      .Append(b.min.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(rs.Length).Append("\n");
                }
                Object.DestroyImmediate(inst);
            }
            File.WriteAllText(Path.Combine(dir, "synty_measure.tsv"), sb.ToString());
            Debug.Log("[Synty] measured " + all.Count + " prefabs into " + Path.Combine(dir, "synty_measure.tsv"));
            EditorApplication.Exit(0);
        }

        /// <summary>Imports the pack''s shader graphs again, sub graphs first, and says how each one came out.</summary>'
Save-Src
Write-Output 'measure tool patched'
