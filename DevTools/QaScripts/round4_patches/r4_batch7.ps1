# Round 4, batch 7 (A7, step 1): the catalog builder gets a batch entry, the hiding-place height follows the Synty furniture (desks and tables are 0.75 m, the old ones were 0.95 m). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\OfficeCatalogBuilder.cs')
Rep '        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");' '        /// <summary>Batch entry point: <c>-executeMethod BuildingEscape.EditorTools.OfficeCatalogBuilder.BuildBatch</c> (the catalog is rebuilt, the Editor quits).</summary>
        public static void BuildBatch() { AssetDatabase.Refresh(); Build(true); EditorApplication.Exit(0); }

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");'
Rep '/// Creates Assets/Resources/OfficeCatalog.asset: one prefab for every prop in PropTable (the VNB Low-Poly Office
    /// Set, the Office Essentials pack, the Pandazole kitchen props and the coffee shop pack).' '/// Creates Assets/Resources/OfficeCatalog.asset: one prefab for every prop in PropTable (since 2026-10-17 night all from the Synty POLYGON
    /// Office pack; before that the VNB Low-Poly Office Set, the Office Essentials pack, the Pandazole kitchen props and the coffee shop pack).'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeLayout.cs')
Rep '            bool hide = solid && onFloor && ph >= 0.9f && Math.Max(fx, fz) >= 0.4f && def.cat != PropCat.Chair;' '            bool hide = solid && onFloor && ph >= HideMinHeight && Math.Max(fx, fz) >= 0.4f && def.cat != PropCat.Chair;'
Rep '        static int FaceQ(char side) => side == ''S'' ? 0 : side == ''N'' ? 2 : side == ''W'' ? 1 : 3;' '        /// <summary>
        /// How tall a piece of furniture must be to duck behind. It was 0.9 m while the desks were 0.95 m; the Synty office pack''s desks and tables are 0.75 m (a person crouches behind a desk all the same), so it is
        /// 0.7 m now and the hiding places per room stay about what they were (the owner, 2026-10-17 night: "use the office assets"; the rules stay).
        /// </summary>
        public const float HideMinHeight = 0.7f;

        static int FaceQ(char side) => side == ''S'' ? 0 : side == ''N'' ? 2 : side == ''W'' ? 1 : 3;'
Save-Src
Write-Output 'round 4 batch 7 patched'
