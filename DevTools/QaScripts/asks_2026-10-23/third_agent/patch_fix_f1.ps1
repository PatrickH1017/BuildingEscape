# Test flaw found by run b6 (Asks1023F, the busted teammate's body): "an open spot 8 m from the player" does not exist where the player stands after the guard's body tests (a small room): the block was skipped. It now tries 8, 7, 6 and 5.5 m
# (the walk of five metres that follows needs a clear line of at least that). Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = $Root + 'Editor\NetDev\NetDevAsks1023F.cs'; Open-Src $SC
Rep '                if (OpenSpot(player.transform.position + Vector3.up * 0.9f, 8f, out var spotA, true))' @'
                bool gotSpotA = false; Vector3 spotA = default;
                foreach (float reachA in new[] { 8f, 7f, 6f, 5.5f }) { if (OpenSpot(player.transform.position + Vector3.up * 0.9f, reachA, out spotA, true)) { gotSpotA = true; break; } }
                if (gotSpotA)
'@
Save-Src
"fix F1 patched"
