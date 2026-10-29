# The melee hold scenario (the owner's Melee_Hold_Animations, 2026-10-28): NetDevMeleeStance.cs and its registration in the harness (MeleeStance).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy NetDevMeleeStance.cs' } else {
  Copy-Item (Join-Path $here 'melee\NetDevMeleeStance.cs') ($A + 'Editor\NetDev\NetDevMeleeStance.cs') -Force
  Write-Output 'copied NetDevMeleeStance.cs'
}
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void GunStance()' @'
        public static void MeleeStance()
        {
            SessionState.SetString("netdev.mode", "meleestance");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunStance()
'@
Rep ' && mode != "gunstance" && ' ' && mode != "gunstance" && mode != "meleestance" && '
Rep '                case "gunstance": yield return GunStanceScenario(); break;' @'
                case "gunstance": yield return GunStanceScenario(); break;
                case "meleestance": yield return MeleeStanceScenario(); break;
'@
Save-Src
"melee tests registered"
