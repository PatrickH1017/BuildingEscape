# Marks R12b (swing doors) as APPLIED inside the hand-off block of section 2.23 (exact-match edits; run once).
. "$PSScriptRoot\patchlib.ps1"
$notes = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'
Open-Src $notes
if ($s.Contains('**APPLIED to the real project 18:58** (DoorSlide.cs')) { "already patched"; return }

Rep '| **WRITTEN, NOT APPLIED even to BE_dev:** `DevTools\QaScripts\round3\r3_swing.ps1` + `r3_swing_tests.ps1` (they patch BE_dev; see 3) | not run |' '| **APPLIED to the real project 18:58** (DoorSlide.cs, SlidingDoor.cs, FloorGenerator.Doors.cs, NetDevR3Test.cs, NetDevR3.cs; the scripts that made the change are `DevTools\QaScripts\round3\r3_swing.ps1` + `r3_swing_tests.ps1`, see 3) | 370 pure PASS (every swing door opens into its own room, the outpost''s out of it: 1750 doors on 60 floors, 0 wrong); scenario `DoorsSlide` 0 FAIL (35 swing doors on floor 1, 211 neighbouring pairs without overlap, 40 open leaves square to the wall); `Doors` 76, `Outpost` 82, `Sneak` 46, `Etr` 30, all 0 FAIL; picture `%TEMP%\be_shot_door_swing_open.png` looked at (hinged leaves open into the room) |'

Rep '(so section 3 of this block describes what is now in the code: ignore its "NOT run")' '(section 3 below describes what is now in the code)'

Rep '**3. R12b swing doors: the exact plan (written, not run).**' '**3. R12b swing doors: what was built (APPLIED 18:58; this is the plan as it was carried out).**'

Rep 'check the outpost door with its wired-glass panes and the keypad / alarm panel beside it (`Outpost` 82 PASS).' 'check the outpost door with its wired-glass panes and the keypad / alarm panel beside it (`Outpost` 82 PASS). **Done 18:58:** all of that was run on `BE_dev` (see the table), then applied. The leaves still have no colliders; a door that swings open is visual only, the blocker collider is the same as for a sliding door (shut = solid, open = none). Not looked at yet: a two-process run with a swing door being opened by a client (the door open state is the same 0..1 value `DoorLogic.Open` that the network already replicates, so nothing should differ).'
Save-Src
"swing marked applied"
