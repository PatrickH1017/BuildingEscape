# Round 12, batch G15 (the owner, 2026-10-19 night: "take the picture off the center of the dart board" and "the dart that is already on it"): the dart board in the docks gets the clean mesh
# (DartBoardMeshBuilder saved the board's own piece of the pack's mesh as Resources/DartBoardClean), and the Round12 scenario checks it.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\FloorGenerator.Dock.cs'
Rep @'
        void BuildDartBoard(Transform parent)
        {
            if (!Application.isPlaying || !PropTable.TryGet("dartboard", out var def)) return;
            float eastInner = cols * cellSize - 0.2f;                                // (the inner face of the east wall)
            var go = PlaceKey("dartboard", eastInner - def.D * 0.5f, BreakRoomDoor.z, 270f, parent, 1.7f - def.H * 0.5f, false);
            if (go != null) go.AddComponent<DartBoard>();
        }
'@ @'
        /// <summary>The pack's dart board with its portrait and its dart taken off (Resources/DartBoardClean, made by the Editor tool DartBoardMeshBuilder).</summary>
        public const string DartBoardCleanMesh = "DartBoardClean";

        void BuildDartBoard(Transform parent)
        {
            if (!Application.isPlaying || !PropTable.TryGet("dartboard", out var def)) return;
            float eastInner = cols * cellSize - 0.2f;                                // (the inner face of the east wall)
            var go = PlaceKey("dartboard", eastInner - def.D * 0.5f, BreakRoomDoor.z, 270f, parent, 1.7f - def.H * 0.5f, false);
            if (go == null) return;
            // the owner, 2026-10-19 night: "take the picture off the center of the dart board ... and the dart that is already on it": the pack's board has a portrait pinned over the bull and a dart stuck in it, all in one mesh
            var clean = Resources.Load<Mesh>(DartBoardCleanMesh);
            if (clean != null) foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.sharedMesh = clean;
            go.AddComponent<DartBoard>();
        }
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevRound12.cs'
Rep @'
            R12Check("the dart board hangs in the docks", board != null);
            if (board != null)
            {
                float width = gen.CellColumns * 18f;
'@ @'
            R12Check("the dart board hangs in the docks", board != null);
            if (board != null)
            {
                var boardMeshes = board.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).Where(mm => mm != null).ToList();
                R12Check("the board has no portrait over the bull and no dart stuck in it: it is the clean mesh (one piece, the 160 triangles of the disc, under 5 cm deep)",
                    boardMeshes.Count == 1 && boardMeshes[0].name == FloorGenerator.DartBoardCleanMesh && boardMeshes[0].GetIndexCount(0) / 3 == 160 && boardMeshes[0].bounds.size.z < 0.05f,
                    $"({boardMeshes.Count} mesh(es): {string.Join(", ", boardMeshes.Select(mm => mm.name + " " + mm.GetIndexCount(0) / 3 + " triangles"))})");
                float width = gen.CellColumns * 18f;
'@
Save-Src
Write-Output 'round 12 batch G15 patched'
