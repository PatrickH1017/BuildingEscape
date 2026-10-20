# Adds the owner's three extra requests (R10-R12) to section 2.23 of the real DEVELOPER_NOTES.md (exact-match patch).
. "$PSScriptRoot\patchlib.ps1"
$notes = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'
Open-Src $notes
if ($s.Contains('- **R10**')) { "R10 exists already: nothing to do"; return }
Rep @'
- **R9** "The microphone should be next to the computer and visible when a player is sitting at the computer. When speaking on the microphone it should show the player models hand pressing the microphone button"
'@ @'
- **R9** "The microphone should be next to the computer and visible when a player is sitting at the computer. When speaking on the microphone it should show the player models hand pressing the microphone button"
- Three more requests, sent while R0-R9 were being read (2026-10-17 evening, same message thread):
- **R10** "There should be streets in the outside city with a few low res cars with headlights driving around and as you go up in floors the street should look seem further away"
- **R11** "Also overall everything looks very blocky(thick) like Roblox, is there a way we can make it look a bit more realistic but still match the cartoon vibe we have. A bit more similar to R.E.P.O where the characters are funky but the world looks pretty good"
- **R12** "The doors also currently slide into nothing so the glass looks like it is bending when a door is shut, they should technically slide into a wall, granted the wall next to the door is a size capable of holding a sliding door of that size within it." And right after: "Doors next to each other should not overlap when both sliding into the wall"
'@
Rep @'
| R9 | microphone prop next to the computer, the seated player's hand presses its button while speaking | TODO |
'@ @'
| R9 | microphone prop next to the computer, the seated player's hand presses its button while speaking | TODO |
| R10 | streets and a few low-poly cars with headlights in the night city; the street looks farther away on higher floors | TODO |
| R11 | a less blocky, more realistic look that still fits the cartoon characters (R.E.P.O. as the example) | TODO |
| R12 | doors slide INTO the wall next to them (pocket) when the wall is long enough, instead of narrowing into nothing (the glass no longer bends); two doors next to each other never overlap when both slide into the wall between them | TODO |
'@
Save-Src
