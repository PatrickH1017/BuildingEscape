# Round 4, batch 8: the room-by-room overhead picture scenario (NetDevHarness.RoomShots) is wired into the harness. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void FloatQa()' '        public static void RoomShots()
        {
            SessionState.SetString("netdev.mode", "roomshots");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void FloatQa()'
Rep 'mode != "floors" && mode != "looks"' 'mode != "floors" && mode != "roomshots" && mode != "looks"'
Rep '                case "floors": yield return FloorsScenario(); break;' '                case "floors": yield return FloorsScenario(); break;
                case "roomshots": yield return RoomShotsScenario(); break;'
Save-Src
Write-Output 'round 4 batch 8 patched'
