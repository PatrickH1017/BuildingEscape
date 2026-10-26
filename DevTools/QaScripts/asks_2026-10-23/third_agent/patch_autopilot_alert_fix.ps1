# The built-player self-play of 2026-10-24e failed one of its own checks on floor 6: the "alert starts quiet" check ran AFTER EtrFloorChecks, which (on the last floor) marks the solo player to test the Big Alarm. The check goes BEFORE it. A test-order fix only; the game is the same.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\QaAutopilot.cs')
Rep @'
                yield return EtrFloorChecks(floor, gen, hm, tm);
                Check($"floor {floor}: the guards' alert starts quiet
'@ @'
                Check($"floor {floor}: the guards' alert starts quiet
'@
Rep 'hm.GuardCap == (floor <= 2 ? 6 : 13), $"(marked {hm.MarkedCount}, big {hm.BigAlarmActive}, cap {hm.GuardCap})");' @'
hm.GuardCap == (floor <= 2 ? 6 : 13), $"(marked {hm.MarkedCount}, big {hm.BigAlarmActive}, cap {hm.GuardCap})");
                yield return EtrFloorChecks(floor, gen, hm, tm);          // (the last floor's checks mark the solo player to try the Big Alarm: the quiet check above has to come first)
'@
Save-Src
"autopilot alert fix patched"
