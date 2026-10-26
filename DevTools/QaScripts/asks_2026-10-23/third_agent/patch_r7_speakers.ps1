# R7 checked that the hallway loudspeakers hang at PaRules.Height (a box under the ceiling). Since 2026-10-24 they are round discs flat against the ceiling (the owner: "they should just be attached to the ceiling"): the check says so. Test fix only.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
Open-Src ($A + 'Editor\NetDev\NetDevR7.cs')
Rep '                R7Check("they hang under the ceiling", spk.All(x => Mathf.Abs(x.transform.position.y - PaRules.Height) < 0.01f));' @'
                float discY = FindFirstObjectByType<FloorGenerator>().CeilingUnderside - PaRules.CeilingGap - PaRules.DiscHeight * 0.5f;
                R7Check("they are round discs flat against the ceiling (the owner, 2026-10-24: attached to the ceiling)", spk.All(x => Mathf.Abs(x.transform.position.y - discY) < 0.01f), $"(disc middle at {discY:0.000})");
'@
Save-Src
"r7 speakers patched"
