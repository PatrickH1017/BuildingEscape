# The restraint scenarios (the owner's restraint, drag and rescue clips, 2026-10-28): NetDevRestraint.cs and its registration in the harness (Restraint).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy the restraint scenarios' } else {
  Copy-Item (Join-Path $here 'restraint\NetDevRestraint.cs') ($A + 'Editor\NetDev\NetDevRestraint.cs') -Force
  Write-Output 'copied NetDevRestraint.cs'
}
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void MeleeStance()' @'
        public static void Restraint()
        {
            SessionState.SetString("netdev.mode", "restraint");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void MeleeStance()
'@
Rep ' && mode != "meleestance" && ' ' && mode != "meleestance" && mode != "restraint" && '
Rep '                case "meleestance": yield return MeleeStanceScenario(); break;' @'
                case "meleestance": yield return MeleeStanceScenario(); break;
                case "restraint": yield return RestraintScenario(); break;
'@
Save-Src
"restraint tests registered"
