# Round 10, batch G03: the Combat Play Mode scenario (registered in the harness).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor\NetDev'

function NewFile([string]$target, [string]$draft) {
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
  if ($env:R2_DRY) { Write-Output "dry run: would write $target"; return }
  [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "written $target"
}
NewFile "$Ed\NetDevCombat.cs" 'C:\bqs\r10_draft\NetDevCombat.cs'

Open-Src "$Ed\NetDevHarness.cs"
Rep @'
        public static void GunItems()
        {
            SessionState.SetString("netdev.mode", "gunitems");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@ @'
        public static void GunItems()
        {
            SessionState.SetString("netdev.mode", "gunitems");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Combat()
        {
            SessionState.SetString("netdev.mode", "combat");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@
Rep @'
&& mode != "gunui" && mode != "ui" &&
'@ @'
&& mode != "gunui" && mode != "combat" && mode != "ui" &&
'@
Rep @'
                case "gunui": yield return GunUiScenario(); break;
'@ @'
                case "gunui": yield return GunUiScenario(); break;
                case "combat": yield return CombatScenario(); break;
'@
Save-Src
