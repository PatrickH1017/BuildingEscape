# The stance scenario (the owner's gun stances, 2026-10-27): NetDevGunStance.cs and its registration in the harness (GunStance).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy NetDevGunStance.cs' } else {
  Copy-Item (Join-Path $here 'stance\NetDevGunStance.cs') ($A + 'Editor\NetDev\NetDevGunStance.cs') -Force
  Write-Output 'copied NetDevGunStance.cs'
}
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void AimHip()' @'
        public static void GunStance()
        {
            SessionState.SetString("netdev.mode", "gunstance");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void AimHip()
'@
Rep ' && mode != "aimhip" && ' ' && mode != "aimhip" && mode != "gunstance" && '
Rep '                case "aimhip": yield return AimHipScenario(); break;' @'
                case "aimhip": yield return AimHipScenario(); break;
                case "gunstance": yield return GunStanceScenario(); break;
'@
Save-Src
"stance tests registered"
