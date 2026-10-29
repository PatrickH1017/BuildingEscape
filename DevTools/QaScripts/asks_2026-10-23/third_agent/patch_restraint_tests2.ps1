# The restraint scenario of stages 2 and 3 (the owner's restraint, drag and rescue clips, 2026-10-28): NetDevRestraintGuard.cs and its registration in the harness (RestraintGuard).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy the scenario' } else {
  Copy-Item (Join-Path $here 'restraint\NetDevRestraintGuard.cs') ($A + 'Editor\NetDev\NetDevRestraintGuard.cs') -Force
  Write-Output 'copied NetDevRestraintGuard.cs'
}
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void Restraint()' @'
        public static void RestraintGuard()
        {
            SessionState.SetString("netdev.mode", "restraintguard");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Restraint()
'@
Rep ' && mode != "restraint" && ' ' && mode != "restraint" && mode != "restraintguard" && '
Rep '                case "restraint": yield return RestraintScenario(); break;' @'
                case "restraint": yield return RestraintScenario(); break;
                case "restraintguard": yield return RestraintGuardScenario(); break;
'@
Save-Src
"restraint guard scenario registered"
