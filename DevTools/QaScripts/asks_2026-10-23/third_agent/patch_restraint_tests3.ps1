# The restraint scenario of stage 4 (the owner's restraint, drag and rescue clips, 2026-10-28): NetDevRestraintRescue.cs and its registration in the harness (RestraintRescue).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy the scenarios' } else {
  Copy-Item (Join-Path $here 'restraint\NetDevRestraintRescue.cs') ($A + 'Editor\NetDev\NetDevRestraintRescue.cs') -Force
  Copy-Item (Join-Path $here 'restraint\NetDevRestraint.cs') ($A + 'Editor\NetDev\NetDevRestraint.cs') -Force
  Copy-Item (Join-Path $here 'restraint\NetDevRestraintGuard.cs') ($A + 'Editor\NetDev\NetDevRestraintGuard.cs') -Force
  Write-Output 'copied NetDevRestraintRescue.cs (and the two older scenarios, extended)'
}
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void RestraintGuard()' @'
        public static void RestraintRescue()
        {
            SessionState.SetString("netdev.mode", "restraintrescue");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void RestraintGuard()
'@
Rep ' && mode != "restraintguard" && ' ' && mode != "restraintguard" && mode != "restraintrescue" && '
Rep '                case "restraintguard": yield return RestraintGuardScenario(); break;' @'
                case "restraintguard": yield return RestraintGuardScenario(); break;
                case "restraintrescue": yield return RestraintRescueScenario(); break;
'@
Save-Src
"restraint rescue scenario registered"
