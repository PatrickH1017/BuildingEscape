# Batch K (two machines on the loopback, T5 + T12): the walkie talkie bought, held out and put away by a CLIENT, and a guard's body dragged by a CLIENT - the two things that cross the network and had only been tried on one machine.
# Registers the scenario DragNet (qa_mp_bd.ps1 -Method DragNet). Needs patch_t29.ps1 (its registration lines are the anchors). Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; the new file is written with CRLF.
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
Put-Crlf (Join-Path $here 'netk_new\NetDevAsks1023K.cs') ($A + 'Editor\NetDev\NetDevAsks1023K.cs')

$SC = $A + 'Editor\NetDev\NetDevHarness.cs'; Open-Src $SC
Rep '        public static void DevDenied() => Start("devdenied");' ('        public static void DevDenied() => Start("devdenied");' + "`n" + '        public static void DragNet() => Start("dragnet");')
Rep 'DevDeniedHostScenario() : DevDeniedClientScenario(); break;' ('DevDeniedHostScenario() : DevDeniedClientScenario(); break;' + "`n" + '                case "dragnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? DragNetHostScenario() : DragNetClientScenario(); break;')
Save-Src
"batch K (DragNet) registered"
