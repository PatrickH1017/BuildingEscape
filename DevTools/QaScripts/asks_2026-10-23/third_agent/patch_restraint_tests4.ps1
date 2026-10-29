# The two-machine restraint scenario (the owner's restraint, drag and rescue clips, 2026-10-28): NetDevRestraintNet.cs and its registration in the harness (RestraintNet).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Dry) { Write-Output 'dry ok would copy the scenario' } else {
  Copy-Item (Join-Path $here 'restraint\NetDevRestraintNet.cs') ($A + 'Editor\NetDev\NetDevRestraintNet.cs') -Force
  Write-Output 'copied NetDevRestraintNet.cs'
}
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void DragNet() => Start("dragnet");' @'
        public static void DragNet() => Start("dragnet");
        public static void RestraintNet() => Start("restraintnet");
'@
Rep '                case "dragnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? DragNetHostScenario() : DragNetClientScenario(); break;' @'
                case "dragnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? DragNetHostScenario() : DragNetClientScenario(); break;
                case "restraintnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? RestraintNetHostScenario() : RestraintNetClientScenario(); break;
'@
Save-Src
"restraint net scenario registered"
