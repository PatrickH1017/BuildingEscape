# Inserts section 2.23 (round 3, the owner's list of 2026-10-17 evening) into the real DEVELOPER_NOTES.md, before "## 3. Things to check when playing".
. "$PSScriptRoot\patchlib.ps1"
$notes = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'
Open-Src $notes
if ($s.Contains('### 2.23 Employees Take Revenge: round 3')) { "section 2.23 exists already: nothing to do"; return }
$sec = @'
### 2.23 Employees Take Revenge: round 3 of 2026-10-17 evening (be57) - THE OWNER'S LIST, STATUS AND HAND-OFF

**Who and how.** Agent 4 (session `e7511809`, resumed) - scratch tree `BE_dev` in its scratchpad (`C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad`; `BE_dev` was byte-identical to the real project at 17:45, SHA-256 manifests `baseline_real_1745.tsv` / `baseline_dev_1745.tsv` there). The owner's rules are unchanged (8.1: no git commands, apply when verified with a backup, never run Unity on the real project, the owner writes the game text, they/them). The owner also said, again: **"As always please remember if the session or weekly limit is almost reached please create detailed handoff notes for the next developer and agent to pickup where we leave off."** This section is that hand-off: the status table below is kept up to date at every milestone. The owner said they would post "a reference image" with this list; **none reached the agent** (nothing new in the project, Desktop, Downloads or Pictures at 17:40), so the screens are designed in the toon look of the HUD (`MenuKit`, `HudTheme`); if the owner has a picture, compare against it.

**The owner's requests, verbatim, in the order given** (R0 is the reply to the last report; R1-R12 are "Tasks for after the above"):
- **R0** "Yes please start the inventory restyle next" (the in-run inventory UI, `Backpack.DrawInventory`, Tab; item 9 of 2.22). Also: "The offices with glass walls should also have glass doors" (already done in round 2, 2.22 item 7).
- **R1** "Each floor should have at least one or multiple Executive Suites(the ones with glass walls and doors) and they should have the safes') with a locked door that can be unlocked at the computer. They should have the room numbers on the door almost as if they are painted on"
- **R2** "Each floor should have a Server Room with a locked door that can be unlocked at the computer, inside each Server Room should be an expensive chip players can take"
- **R3** "When a player sits down at at the police computer it should pop up an upload button and when the player clicks it, it should display a loading bar and say on the interface "Uploading File(s): F_U_BOSSMAN" then it should go to the sign in screen"
- **R4** "After the player logs in it should have three tabs, the current Safes tab, another one called Nav, and a tab called Hacked. Inside the Nav tab should be a 2D map of the current floor with a live tracker of each player(with their username and an icon of their character) and a live tracker of each officer( with an icon of the officer) and room numbers in each room. The Nav tab should also show where the Executive Suite(s) are and where the Server Room per floor is. Inside the Hacked tab should look like a terminal and should prompt the player to type a Room Number and then type either Lock or Unlock to lock or unlock the door for the room connected to the number they typed for that current floor(when typing Lock or Unlock on the computer caps should not matter)(cops should not be able to unlock a door locked by the computer). When a Executive Suite or Server Room is locked by the computer the door should make a loud metallic lock sound."
- **R5** "There should be keyboard sounds for when typing on the outpost keyboard and mouse click sounds for clicking the buttons"
- **R6** "Players should be able to lock the outpost door once inside"
- **R7** "There should be speakers throughout the hallways and the player at the computer should be able to hold M to speak on the microphone throughout the hallways so players and officers can hear. When a player speaks on the microphone within distance of officers, all officers other than officers currently pinning a player return to the outpost looking for the player on the computer but they cannot get into the room if the player on the computer locked it."
- **R8** "On the Nav tab map on the computer there should be an icon for each microphone in the hallway visible on the map and the player on the computer should be able to select each microphone they like, whether it be singular, multiple or all to speak through. There should be a Select all and Deselect all buttons as well for the microphones"
- **R9** "The microphone should be next to the computer and visible when a player is sitting at the computer. When speaking on the microphone it should show the player models hand pressing the microphone button"

**STATUS TABLE** (TODO = not started; BUILT = in `BE_dev`, tested, NOT yet applied to the real project; APPLIED = in the real project)

| # | What | Status |
|---|---|---|
| R0 | inventory restyle (MenuKit toon look) | TODO |
| R0b | glass doors on glass-front offices | done in round 2 (2.22 item 7); to be re-checked with R1 |
| R1 | Executive Suites (glass walls + doors, safes, computer-locked door, painted room number) | TODO |
| R2 | Server Room per floor (computer-locked door, expensive chip) | TODO |
| R3 | computer: sit down -> UPLOAD button -> loading bar "Uploading File(s): F_U_BOSSMAN" -> sign-in | TODO |
| R4 | tabs Safes / Nav (2D map, live players and officers, room numbers, suites, server room) / Hacked (terminal: room number + lock/unlock, case-insensitive, cops cannot undo it, loud metallic sound for suites and server rooms) | TODO |
| R5 | keyboard clicks while typing at the computer, mouse clicks on its buttons | TODO |
| R6 | lock the outpost door from inside | TODO |
| R7 | speakers in the hallways, hold M at the computer to speak through them, officers answer (not those pinning someone), cannot enter a locked room | TODO |
| R8 | microphone icons on the Nav map: select one / several / all, Select all and Deselect all | TODO |
| R9 | microphone prop next to the computer, the seated player's hand presses its button while speaking | TODO |

(Details, decisions and test results are added below as each row moves; if this table still says TODO for everything, nothing was applied.)

'@
Rep '## 3. Things to check when playing' ($sec + "## 3. Things to check when playing")
Save-Src
