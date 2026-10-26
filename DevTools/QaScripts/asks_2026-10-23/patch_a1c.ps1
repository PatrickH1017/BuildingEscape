# Batch A1, third patch: the policy-plan test that builds a floor of exactly 12 guards must count its rifles for 12 guards (the patrol count of floor 10 is 15 now: T14). Patches the scratch copy C:\bd.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'

$SC = $A + 'Editor\NetDev\NetDevGuardsTest.cs'; Open-Src $SC
Rep '            var plan = GuardLoadouts.Plan(10, 12, new System.Random(7));' ('            GuardLoadouts.Counts(10, 12, out _, out _, out _, out int r10of12, out _);                    // (the rifles of a floor of exactly twelve guards: floor 10 has 15 patrolling since 2026-10-23)' + "`n" + '            var plan = GuardLoadouts.Plan(10, 12, new System.Random(7));')
Rep 'l.Shield == ShieldTier.Medium) == r10 && plan.Count(l => !l.Armed && l.Shield == ShieldTier.None) == 12 - 2 - 2 - r10,' 'l.Shield == ShieldTier.Medium) == r10of12 && plan.Count(l => !l.Armed && l.Shield == ShieldTier.None) == 12 - 2 - 2 - r10of12,'
Save-Src
"A1c patched"
