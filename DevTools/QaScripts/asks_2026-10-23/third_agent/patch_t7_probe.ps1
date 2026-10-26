# T7, first step: the PVP room map (scenario PvpMap, a text picture of the room and a list of what stands in it), to design the battlefield on. Needs patch_t6.ps1 and patch_t16.ps1 first (their registration lines are the anchors).
# Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; the new file is written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 't7_new\NetDevPvpMap.cs') ($A + 'Editor\NetDev\NetDevPvpMap.cs')
Put-Crlf (Join-Path $here 't7_new\NetDevPvpLineup.cs') ($A + 'Editor\NetDev\NetDevPvpLineup.cs')
$SC = $A + 'Editor\NetDev\NetDevHarness.cs'; Open-Src $SC
Rep '        public static void Asks1023W()' @'
        public static void PvpMap()
        {
            SessionState.SetString("netdev.mode", "pvpmap");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PvpLineupUi()
        {
            SessionState.SetString("netdev.mode", "pvplineupui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Asks1023W()
'@
Rep '&& mode != "asks1023w")' '&& mode != "asks1023w" && mode != "pvpmap" && mode != "pvplineupui")'
Rep '                case "asks1023w": yield return Asks1023WScenario(); break;' @'
                case "asks1023w": yield return Asks1023WScenario(); break;
                case "pvpmap": yield return PvpMapScenario(); break;
                case "pvplineupui": yield return PvpLineupUiScenario(); break;
'@
Save-Src
"PvpMap registered"
