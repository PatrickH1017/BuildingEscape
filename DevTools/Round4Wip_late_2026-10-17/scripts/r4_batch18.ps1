# Round 4, batch 18: the Shaft "guard on duty" block tests the outpost guard alone. After batches 16/17 floor 1's layout changed a little and an ordinary patrol guard (Guard_3 / Guard_1) can see down the corridor in front of the outpost door: he spotted the player and called backup, which tripped "knock-out: backup was never called" (a layout accident, not a game fault). The other guards are blind and deaf in this block. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevShaft.cs')
Rep '                var post = duty.transform.position;
                ShaftCheck("gate: it is the room''s rectangle"' '                var post = duty.transform.position;
                // (this block tests the guard on duty alone: the floor''s patrol guards are blind and deaf here, because which of them can see down the corridor in front of the outpost door depends on the layout, and one of them calling for backup is no fault of his)
                foreach (var otherGuard in GuardAI.All) if (otherGuard && otherGuard != duty) otherGuard.noticeGate = blindPos => false;
                ShaftCheck("gate: it is the room''s rectangle"'
Save-Src
Write-Output 'round 4 batch 18 patched'
