# The lamps the owner added (Assets\New Solution Studio\PBR Lamps Pack, 2026-10-24): a picture scenario that shows each prefab in six views, to tell which is the red beacon of his reference picture. Patches the scratch copy C:\bd; the pack itself is copied from the real project first (robocopy).
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
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 'lamp_new\NetDevLampPreview.cs') ($A + 'Editor\NetDev\NetDevLampPreview.cs')
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PropScan()' @'
        public static void LampPreview()
        {
            SessionState.SetString("netdev.mode", "lamppreview");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PropScan()
'@
Rep 'mode != "guardalert" &&' 'mode != "guardalert" && mode != "lamppreview" &&'
Rep '                case "guardalert": yield return GuardAlertScenario(); break;' @'
                case "guardalert": yield return GuardAlertScenario(); break;
                case "lamppreview": yield return LampPreviewScenario(); break;
'@
Save-Src
"lamp preview patched"
