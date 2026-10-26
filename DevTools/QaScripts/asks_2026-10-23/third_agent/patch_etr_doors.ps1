# The Etr scenario's door check was left behind by the Floor 7 doors (2026-10-23, DEVELOPER_NOTES 2.62): a wide hallway door on floors 1 to 7 is the pack's glass door now ("PackPanel" leaves), not the old 12 "Pane" pictures.
# QaAutopilot.EtrFloorChecks was updated then; this makes NetDevEtr.cs count the same way. A test fix only.
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Editor\NetDev\NetDevEtr.cs')
Rep '                    if (d.Wide) { wide++; if (panes == 12) wideDressed++; if (firstWide == null) firstWide = d; }' @'
                    int packs = d.LowLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "PackPanel") + d.HighLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "PackPanel");
                    if (d.Wide) { wide++; if (panes == 12 || packs >= 2) wideDressed++; if (firstWide == null) firstWide = d; }          // (the old windows, or since 2026-10-23 the pack's own glass door)
'@
Save-Src
"etr doors patched"
