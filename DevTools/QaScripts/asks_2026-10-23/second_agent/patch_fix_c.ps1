# Test flaw found by run b3 of Asks1023C: the T13 block (the replacement guard) counted the guards alive, but a guard who walked past the body in those 14 seconds saw it (T11) and called backup, which put two officers on the floor.
# The block is about the replacement only: the body's alarm is switched off for it (the T11 block, after it, tests the alarm on its own).
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = 'C:\bd\Assets\Editor\NetDev\NetDevAsks1023C.cs'; Open-Src $SC
Rep @'
                int alive0 = guards.Count;
                var victim = guards[0];
                Eliminate(victim);
'@.TrimEnd() @'
                int alive0 = guards.Count;
                var victim = guards[0];
                Eliminate(victim);
                typeof(GuardAI).GetProperty("BodyAlarmRaised").GetSetMethod(true).Invoke(victim, new object[] { true });          // (this block is about the replacement: no guard who walks past this body calls backup for it - the next block tests the alarm on its own)
'@.TrimEnd()
Save-Src
"fix C patched"
