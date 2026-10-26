# Two old tests assumed the old flat cap of 24 guards (a backup call always sent two officers); with the floors' caps of 2026-10-24 (6 on floors 1 and 2, 8 with the Big Alarm) they are told what to expect.
# Test fixes only. Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Editor\NetDev\NetDevOutpost.cs')
Rep 'OutpostCheck("hack: two officers were sent", GuardAI.All.Count >= guardsBefore + 2, $"({guardsBefore} -> {GuardAI.All.Count} officers)");' 'OutpostCheck("hack: the officers were sent (two, or as many as the cap of the floor leaves room for: 6 on floor 1)", GuardAI.All.Count >= guardsBefore + Mathf.Min(2, hm.GuardCap - guardsBefore) && GuardAI.All.Count > guardsBefore, $"({guardsBefore} -> {GuardAI.All.Count} officers, cap {hm.GuardCap})");'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevPew.cs')
Rep @'
            int before2 = g2.Count;
            hm.CallBackup(null);
'@ @'
            int before2 = g2.Count;
            hm.MarkSpotted(WobblyPlayerRegistry.All[0]);                       // (the Big Alarm - the only player is the whole party: floor 2's six guards fill its cap of 6, the Big Alarm lifts it to 8 so that the call can send officers)
            hm.CallBackup(null);
'@
Save-Src
"guard alert tests patched"
