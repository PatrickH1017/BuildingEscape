# Writes the hand-off block and the last status changes into section 2.23 of the real DEVELOPER_NOTES.md (exact-match edits; run once).
. "$PSScriptRoot\patchlib.ps1"
$notes = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'
Open-Src $notes
if ($s.Contains('2.23 HAND-OFF (written 2026-10-17')) { "hand-off block exists already"; return }

# the requests that came mid-turn after R13: officers' sight, the swing doors, the hand-off
Rep '- **R13** (sent a little later) "There should be more signs displaying where which rooms are"' '- **R13** (sent a little later) "There should be more signs displaying where which rooms are", then "Don''t add too many directional signs where it''s easy but make them more valuable so if you find one you can definitely find the correct room"
- **R14** "Officers should also see much further down a hallway if it is a straight line of sight"
- **R12b** (after the slide doors were built) "For any door that can''t slide properly into the wall please make it a swing door and see how it works"
- Also asked: "In the inventory UI, equipable items should be separate from loot" (done inside R0) and "Please right detailed handoff notes" (the hand-off block below).'

# the table
Rep '| R13 | more signs that show where which rooms are (more direction signs; plan: also signs for the Executive Suites and the Server Room, and a floor directory near the elevators) | TODO |' '| R13 | wayfinding: FEWER but complete signs - floor-map boards with YOU ARE HERE, plus EXECUTIVE SUITES / SERVER ROOM direction signs (design in the hand-off, 4.8) | TODO |
| R14 | officers see much further down a straight hallway (x3.15 = 44 m in a +-9 degree beam; the cone on the floor shows it) | BUILT in BE_dev, verified (357-369 pure PASS, Sneak 46); files copied to `DevTools\Round3Wip`; APPLY pending |
| R12b | doors that cannot slide swing open instead (hinged leaves, into the room; the outpost door out into the corridor) | WRITTEN (`DevTools\QaScripts\round3\r3_swing.ps1` + `r3_swing_tests.ps1`), NOT run |'
Save-Src

& "$PSScriptRoot\notes_set.ps1" -Row 'R10' -Status 'BUILT in BE_dev, verified (CityLook 0 FAIL, Looks 28, FloatQa); files in `DevTools\Round3Wip`; APPLY pending'
& "$PSScriptRoot\notes_set.ps1" -Row 'R12' -Status 'BUILT in BE_dev, verified (pure: 60 real floors, 0 overlaps; Doors 76; DoorsSlide 0 FAIL); files in `DevTools\Round3Wip`; APPLY pending. The 21 % of doors that cannot slide still narrow into their jambs until R12b is run'
$hand = [IO.File]::ReadAllText("$PSScriptRoot\handoff_r3.txt")
& "$PSScriptRoot\notes_set.ps1" -Append $hand
"done"
