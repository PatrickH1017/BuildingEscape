# Round 2, batch 4: the Looks picture scenario, the layout accessor and the scenery stats. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevHarness.cs')
Rep @'
        public static void Doors()
        {
'@ @'
        public static void Looks()
        {
            SessionState.SetString("netdev.mode", "looks");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Doors()
        {
'@
Rep '                case "doors": yield return DoorsScenario(); break;' @'
                case "doors": yield return DoorsScenario(); break;
                case "looks": yield return LooksScenario(); break;
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Surfaces.cs')
Rep '        public Transform Scenery { get; private set; }' @'
        public Transform Scenery { get; private set; }
        public long SceneryMs { get; private set; }
        /// <summary>The plan of the floor on show (null on the docks, the boss floor, or when the floor could not be furnished), for the test tools.</summary>
        public Layout CurrentLayout => layout;
'@
Rep @'
            WindowCount = 0; CityTowers = 0; CityMeshes = 0; CityReach = 0f;
            if (outerSegs.Count == 0) return;
            try
            {
'@ @'
            WindowCount = 0; CityTowers = 0; CityMeshes = 0; CityReach = 0f; SceneryMs = 0;
            if (outerSegs.Count == 0) return;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
'@
Rep '                CityTowers = stats.towers; CityMeshes = stats.meshes; CityReach = stats.maxReach;' @'
                CityTowers = stats.towers; CityMeshes = stats.meshes; CityReach = stats.maxReach;
                SceneryMs = clock.ElapsedMilliseconds;
'@
Save-Src
Write-Output 'batch 4 patched'
