# The owner, 2026-10-23, after the playtest with friends: "The guard pin time should be 1 second next to a player (right now is too long)". T4 had made it 2 seconds (CustodyRules.GrabSeconds): now 1.
# Patches the scratch copy C:\bd (CustodyRules.cs and the two T4 tests, Asks1023BTest.cs and Asks1023B.cs); exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }

Open-Src ($Root + 'Scripts\CustodyRules.cs')
Rep '        public const float GrabSeconds = 2f;' '        public const float GrabSeconds = 1f;          // (2 until the friends'' playtest of 2026-10-23, then the owner: "The guard pin time should be 1 second next to a player (right now is too long)")'
Save-Src

Open-Src ($Root + 'Editor\NetDev\NetDevAsks1023BTest.cs')
Rep 'a guard must hold on to the player for 2 seconds before he pins him' 'a guard must hold on to the player for 1 second before he pins him'
Rep 'a guard needs 2 seconds in reach to pin: the clock is full after 100 steps of 0.02 s and not after 99", CustodyRules.GrabSeconds == 2f && steps == 100);' 'a guard needs 1 second in reach to pin: the clock is full after 50 steps of 0.02 s and not after 49", CustodyRules.GrabSeconds == 1f && steps == 50);'
Rep '1.9 s in reach is not enough, 2 s is", !CustodyRules.GrabDone(CustodyRules.GrabAfter(0f, true, 1.9f)) && CustodyRules.GrabDone(CustodyRules.GrabAfter(0f, true, 2f)));' '0.9 s in reach is not enough, 1 s is", !CustodyRules.GrabDone(CustodyRules.GrabAfter(0f, true, 0.9f)) && CustodyRules.GrabDone(CustodyRules.GrabAfter(0f, true, 1f)));'
Rep 'half a second away takes a whole second off' '0.3 s away takes 0.6 s off'
Rep 'Math.Abs(CustodyRules.GrabAfter(1.5f, false, 0.5f) - 0.5f) < 1e-5f' 'Math.Abs(CustodyRules.GrabAfter(0.8f, false, 0.3f) - 0.2f) < 1e-5f'
Rep 'the clock never goes above 2 s, and a negative step changes nothing", CustodyRules.GrabAfter(1.9f, true, 10f) == 2f && CustodyRules.GrabAfter(1f, true, -3f) == 1f);' 'the clock never goes above 1 s, and a negative step changes nothing", CustodyRules.GrabAfter(0.9f, true, 10f) == 1f && CustodyRules.GrabAfter(0.5f, true, -3f) == 0.5f);'
Rep 'float held = CustodyRules.GrabAfter(0f, true, 1.5f);' 'float held = CustodyRules.GrabAfter(0f, true, 0.75f);'
Rep 'held = CustodyRules.GrabAfter(held, false, 0.5f);' 'held = CustodyRules.GrabAfter(held, false, 0.2f);'
Rep 'held = CustodyRules.GrabAfter(held, true, 0.4f);' 'held = CustodyRules.GrabAfter(held, true, 0.2f);'
Rep '1.5 s in reach, half a second away and 0.4 s back is 0.9 s: not pinned yet", Math.Abs(held - 0.9f)' '0.75 s in reach, 0.2 s away and 0.2 s back is 0.55 s: not pinned yet", Math.Abs(held - 0.55f)'
Save-Src

Open-Src ($Root + 'Editor\NetDev\NetDevAsks1023B.cs')
Rep 'a guard needs 2 seconds of contact to pin' 'a guard needs 1 second of contact to pin'
Rep 'float contactStart = -1f, heldAt15 = -1f; int grabsAt15 = -1, grabsAt22 = -1;' 'float contactStart = -1f, heldAt06 = -1f; int grabsAt06 = -1, grabsAt13 = -1;'
Rep 'if (heldAt15 < 0f && since >= 1.5f) { heldAt15 = grabber.GrabHeld; grabsAt15 = grabber.GrabsDone - grabs0; }' 'if (heldAt06 < 0f && since >= 0.6f) { heldAt06 = grabber.GrabHeld; grabsAt06 = grabber.GrabsDone - grabs0; }'
Rep 'if (grabsAt22 < 0 && since >= 2.3f) { grabsAt22 = grabber.GrabsDone - grabs0; break; }' 'if (grabsAt13 < 0 && since >= 1.3f) { grabsAt13 = grabber.GrabsDone - grabs0; break; }'
Rep 'A23B("T4: after 1.5 s of contact the clock is at about 1.5 s and he has not grabbed yet", heldAt15 > 1.2f && heldAt15 < 1.8f && grabsAt15 == 0, $"(clock {heldAt15:0.00} s, grabs {grabsAt15})");' 'A23B("T4: after 0.6 s of contact the clock is at about 0.6 s and he has not grabbed yet", heldAt06 > 0.4f && heldAt06 < 0.85f && grabsAt06 == 0, $"(clock {heldAt06:0.00} s, grabs {grabsAt06})");'
Rep 'A23B("T4: after 2.3 s of contact the grab has gone off once (the clock ran out)", grabsAt22 == 1, $"(grabs {grabsAt22})");' 'A23B("T4: after 1.3 s of contact the grab has gone off once (the clock ran out)", grabsAt13 == 1, $"(grabs {grabsAt13})");'
Rep 'grabber.GrabHeld < 0.9f)' 'grabber.GrabHeld < 0.6f)'
Rep 'before >= 0.8f && after < before - 0.3f' 'before >= 0.5f && after < before - 0.3f'
Save-Src
"pin time 1 s patched"
