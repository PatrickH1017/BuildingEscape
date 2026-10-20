# Adds the owner's request R13 (more signs) to section 2.23 of the real DEVELOPER_NOTES.md (exact-match patch).
. "$PSScriptRoot\patchlib.ps1"
$notes = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'
Open-Src $notes
if ($s.Contains('- **R13**')) { "R13 exists already: nothing to do"; return }
Rep @'
- **R12** "The doors also currently slide into nothing
'@ @'
- **R13** (sent a little later) "There should be more signs displaying where which rooms are"
- **R12** "The doors also currently slide into nothing
'@
Rep @'
| R12 | doors slide INTO the wall
'@ @'
| R13 | more signs that show where which rooms are (more direction signs; plan: also signs for the Executive Suites and the Server Room, and a floor directory near the elevators) | TODO |
| R12 | doors slide INTO the wall
'@
Save-Src
