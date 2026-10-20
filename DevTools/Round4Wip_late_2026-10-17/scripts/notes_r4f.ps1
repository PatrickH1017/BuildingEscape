# DEVELOPER_NOTES 2.24, update 6 (2026-10-17 21:20): the round-4 work is APPLIED to the real project.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'

Open-Src (Join-Path $real 'DEVELOPER_NOTES.md')
Rep '#### 2.24 LAYOUT REMAKE (A7 / A9 / A10 / A11) - AS BUILT in the QA tree `BE_qa` (2026-10-17 21:00), NOT yet applied unless a later line says so' '#### 2.24 LAYOUT REMAKE (A7 / A9 / A10 / A11) - AS BUILT, and **APPLIED to the real project at 21:18** (44 files: 34 changed, 10 new; the replaced files are in `DevTools\QaBackups\before_round4_apply_2026-10-17_2118`, the older restore point in `before_synty_layouts_2026-10-17_2009`; code, editor tools and resources of the QA tree and the real project are identical by SHA-256; the owner''s Editor recompiles on focus: **menu "Employees Take Revenge > Setup Office Pack" / "Setup Characters" are only needed if a catalog shows as empty**)

**FINAL TEST STATE at the apply (QA tree = real project):** compile 0 errors, **392 pure PASS**; Play scenarios: `Looks` 30 PASS (the suites keep their glass front now: `DirectionSigns.Blocked` refuses a suite''s perimeter wall), `FloatQa` 4 PASS (nothing floats), `LootQa` 301 PASS, `Doors` 76, `Outpost` 82, `Sneak` 46, `Etr` 30, `Shaft` 93 (its gate checks follow A3), `Demo` 35, `Floors` 56 pictures with no magenta, `Offline`; walkway pictures (open, longopen, training) checked by eye. **NOT tested:** the online path of A4 (host pop-up, `ReturnPartyToLobby`: needs the two-process harness), eye-level pictures of the break room / a restroom door / a stairwell, the multi-process `qa_chain.ps1` runs, a BUILT player. Open: A5 (trade), R1b-R13 of 2.23, smoke detectors.'
Rep '| A3 | the outpost guard may leave the outpost | TODO |' '| A3 | the outpost guard may leave the outpost | APPLIED 21:18: `FloorGenerator.Outpost.PlaceOutpostGuard` gate = his room OR `outpostCtl.Unlocked` (after the hack he sees / hears like any guard, chases out through the door, walks back to his post); `Shaft` PASS 93 |'
Rep '| A4 | Exit button next to Restart after a wipe, host''s Leave with party / Leave solo pop-up | TODO |' '| A4 | Exit button next to Restart after a wipe, host''s Leave with party / Leave solo pop-up | APPLIED 21:18 (compiled; the OFFLINE path = Restart + Exit(menu scene) is simple; the ONLINE path is NOT tested: member Exit = Leave, host Exit = pop-up "Leave with party" (`NetSession.ReturnPartyToLobby`: the menu scene is loaded for everybody, `BackInLobby` in `Update`, `LobbyService.Unlock`) / "Leave solo" (`Leave`) / Stay) |'
Rep '| TODO (plan below) |' '| APPLIED 21:18 (see "2.24 LAYOUT REMAKE") |'
Rep '| TODO (part of the layout remake) |' '| APPLIED 21:18 (see "2.24 LAYOUT REMAKE") |' 2
Save-Src
Write-Output 'notes 2.24 update 6 patched'
