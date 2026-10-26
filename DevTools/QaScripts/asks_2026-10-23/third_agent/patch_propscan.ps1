# The owner's playtest of 2026-10-23: "Plants also should not spawn in the middle of rooms (along the walls are good) and floating items such as fire extinguishers should not be floating randomly": the scenario PropScan (NetDevPropScan.cs) lists the offenders on floors 1 to 4.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$t = [IO.File]::ReadAllText((Join-Path $here 'playtest_new\NetDevPropScan.cs')).Replace("`r`n", "`n").Replace("`n", "`r`n")
if ($Dry) { Write-Output "dry ok (would write) NetDevPropScan.cs" } else { [IO.File]::WriteAllText($Root + 'Editor\NetDev\NetDevPropScan.cs', $t, (New-Object Text.UTF8Encoding($false))); Write-Output "wrote NetDevPropScan.cs" }
Open-Src ($Root + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void SafetyNet()' @'
        public static void PropScan()
        {
            SessionState.SetString("netdev.mode", "propscan");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void SafetyNet()
'@
Rep '&& mode != "safetynet")' '&& mode != "safetynet" && mode != "propscan")'
Rep '                case "safetynet": yield return SafetyNetScenario(); break;' @'
                case "safetynet": yield return SafetyNetScenario(); break;
                case "propscan": yield return PropScanScenario(); break;
'@
Save-Src
"propscan registered"
