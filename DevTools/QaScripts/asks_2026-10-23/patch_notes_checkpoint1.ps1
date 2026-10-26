# Notes checkpoint 1 (2026-10-23): T26 and T27 join the list, the state at hand-off is written down. Patches the REAL project's DEVELOPER_NOTES.md (exact matches, line endings kept).
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$SC = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DEVELOPER_NOTES.md'; Open-Src $SC

Rep '(25 tasks): the breakdown, the order of work and the STATUS of every task' '(27 tasks): the breakdown, the order of work and the STATUS of every task'
Rep 'Two tasks (T24, T25) were added by the owner while the work was going.' 'Four tasks (T24, T25, T26, T27) were added by the owner while the work was going.'
Rep 'to get the dropped bullets".' 'to get the dropped bullets". - 26 (added) "Clicking Alt should not unlock the camera view only when holding it" - 27 (added, to the ammo task) "A circular loading bar for reloading progress and for the shotgun it should display a counter as well for each shell getting loaded".'
Rep '- T25 [ ] An eliminated guard drops 0-3 bullets (0 most likely, 3 least); a player collects them by running over the body' @'
- T25 [ ] An eliminated guard drops 0-3 bullets (0 most likely, 3 least); a player collects them by running over the body
- T26 [ ] Alt (the free look) unlocks the camera view only while it is HELD (a tap does nothing)
- T27 [ ] The reload progress is a circular loading bar; the shotgun also counts each shell as it goes in
'@.TrimEnd()
Rep '**Order of work** (cheap and certain first; each batch is built, tested in Unity in the scratch copy and applied before the next): A) T19, T20, T21, T10, T15, T18, T8, T17, T14, T4, T24, T25 -' '**Order of work** (cheap and certain first; each batch is built, tested in Unity in the scratch copy and applied before the next): A) T19, T20, T21, T10, T15, T18, T8, T17, T14, T26 (batch A1), then T4, T24 + T27, T25 (batch A2) -'
Rep '- T25: 0 / 1 / 2 / 3 bullets with 40 / 30 / 20 / 10 percent (one table of constants).' @'
- T26: I read "Clicking Alt should not unlock the camera view only when holding it" as: a TAP on Alt (or V) must do nothing; the view unlocks only while the key is held, and only after a quarter of a second (`FreeLookRules.HoldSeconds`), and locks the moment it is let go; a window that lost the focus (Alt+Tab) counts as let go. If what you see is something else (Alt stuck on, the cursor released), say so.
- T27: the reload ring sits in the ammunition panel (bottom right) where the straight bar was; the shotgun's ring shows "n / 6" (the shells in the gun, counting up as each shell is loaded over the reload's time). It is only the picture: the gun still takes every shell at the end of the reload (a reload of one missing shell takes as long as a full one, as before). Say if the shotgun should really load one shell at a time.
- T25: 0 / 1 / 2 / 3 bullets with 40 / 30 / 20 / 10 percent (one table of constants). EVERY eliminated guard rolls it (not only the armed ones: you said "eliminated guards"); an unarmed guard's bullets are pistol bullets. The boss drops none.
'@.TrimEnd()
Rep '- Meaning: an eliminated armed guard drops 0, 1, 2 or 3 bullets of his gun''s kind;' '- Meaning: an eliminated guard drops 0, 1, 2 or 3 bullets of his gun''s kind (an unarmed guard''s are pistol bullets);'

$handoff = @'
#### T26 - Alt unlocks the view only while it is held
**STATUS T26:** [ ] not started
- Meaning: see the decision above. Today `CameraFollow` free-looks while `Input.GetKey(Alt or V)` is true, so even a tap swings the view for the few frames the key is down, and a key that sticks (Alt+Tab, the Windows menu on an Alt tap) leaves the view unlocked.
- Where: `CameraFollow.Update` (`freeLookKey` LeftAlt, `altFreeLookKey` V, `FreeLooking`); the Keybinds screen already says "ALT / V look around (hold)".
- How: `FreeLookRules` (pure): `Held(held, keyDown, dt)` and `Unlocked(held)` with `HoldSeconds` 0.25; `CameraFollow` keeps the held time (unscaled), the key counts only while `Application.isFocused`, a menu being open resets it; `CameraFollow.TestFreeLookKey` is the hook of the Play check.
- Tests: pure (a 0.1 s tap never unlocks; held, it unlocks at 0.25 s; let go, it locks at once); Play (the rig: tap, hold, let go).

#### T27 - A circular loading bar for the reload; the shotgun counts its shells
**STATUS T27:** [ ] not started
- Meaning: the reload progress is drawn as a ring that fills; for the shotgun the ring also shows a counter that steps up for each shell as it is loaded.
- Where: `HudWidgets.AmmoPanel` / `AmmoModel` (the straight reload bar `HudTheme.Bar(... reloadProgress ...)` today), `GunController.OnGUI` (builds the model), `GunMagazine` (`ReloadProgress`; new `ReloadLoads` and `LoadedSoFar(now)` count the rounds the reload will load and how many are in so far).
- How: `HudWidgets.ProgressRing(centre, diameter, width, fraction, colour)` (a faint ring and a string of 90 dots filled clockwise from the top: IMGUI has no arcs) on the right of the panel while reloading, "RELOADING" under the gun's name; the shotgun's ring has "shells in the gun / capacity" in its middle. Picture only: the gun still loads everything at the end.
- Tests: pure (`ReloadLoads` is what is missing, no more than the reserve; `LoadedSoFar` steps 0..n over the reload and is 0 when not reloading); a picture of the ring (ui.ps1) so the owner can judge it.

**State at hand-off** (written in the MIDDLE of the session, 2026-10-23; the session rewrites it when it stops: if it still says "in the middle", the session ended without doing so):
- APPLIED to the real project: the Floor 7 doors (2.62) and the notes (this section). NOTHING of the 27 tasks is applied yet.
- THE SCRATCH COPY `C:\bd` (a short junction to `C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-Eternum\2cfb45a0-f269-4edd-ba06-8b8b2fc18a76\scratchpad\BE_dev`, a copy of the real project made on 2026-10-23; it may be gone) holds BATCH A1, compiled in Unity 6000.0.45f1: T8, T10, T14, T15, T17, T18, T19, T20, T21, T26. RESULT: compile 0 errors, ALL 1010 pure checks pass, and the Play scenario `Asks1023` (21 checks: T17, T20, T8, T21, T26, T14) ends `DONE fails=0` (the first run had one old test that builds a floor of exactly 12 guards and counted its rifles with the new patrol count - fixed in patch_a1c.ps1 - and the scenario stopped at T8 because my test dropped the loot in the arrival elevator car, where the game banks it: correct game behaviour, the scenario now stands in a room first). The patches and the new files are saved in `DevTools\QaScripts\asks_2026-10-23\` (README.txt there). The existing scenarios that touch batch A1 (Outpost, ComputerUi, R7, Etr, LootQa, InvUi, Demo, Guns, Combat, GuardCall, Offline, PewLoot, Pew, Doors, Sneak, Floors, Shaft) were running next (Prefix a3): read `C:\bqs\qa_summary_a3.txt` if it still exists; A1 goes into the real project when they pass (apply_checked.ps1).
- IN DESIGN, NOT YET CODED (what is needed is in the entries above): batch A2 = T4, T24 + T27, T25. New files `GuardDropRules.cs` and `AmmoRunOver.cs` are written and saved, not compiled. The plan: `GunMagazine` loses `MaxRounds` / `Room`, `GunRules.MaxClips` and `SpareClips`, `RangedWeapon.Spec.maxClips` go; `Backpack.TooManyClipsFor` and the `Room` checks in `Backpack.TakeClip`, `Vendor.BuyFor` and `RunSync.HostStore` go; `GunController.DrawSpare` goes and the ammo panel shows "rounds / reserve"; `Pickup.runOverOnly` (skipped by `ScanAround`, refused by `TryStore` / `HostStore`); `RunSync.HostRunOver`; `GuardDrops` rolls `GuardDropRules.Bullets(Random 0..99)` and makes a few brass cartridges (one per bullet) with `AmmoRunOver` on them; `CustodyRules.GrabSeconds` (2) and `GrabDone`, and `GuardAI.TryGrab` replaces `TryCatch(target)` in `UpdateChase`. Tests to rewrite for the removed limits: NetDevGunsTest (MaxClips / Room), NetDevGuns (the dealer's "three clips" checks and the Room checks), NetDevOutpost (the "full pistol" check), NetDevRound12 (the "twelve clips" check), NetDevDemo (the full-pistol check), NetDevTest (SpareClips), NetDevGuardsTest (DropRounds), NetDevCombat (what a guard drops).
- NOT STARTED: batches B, C, D (T13, T11, T9, T22, T6 + T23, T2 + T3, T16, T1, T7, T5, T12).
- HOW TO RESUME: read the entries above; make or refresh the scratch copy (the doors README); apply the saved patches in order (each has -Dry); compile and test with `qa_regress_bd.ps1`; then `apply_checked.ps1` into the real project; then change the tracker and the `STATUS` lines of the tasks that passed and write their "Words I changed" (so far: `HowToPlayContent.cs` - the tab "Hacked" is now "Commands" in two sentences; batch A2 will add the Vendor ammo blurb "clips you carry" -> "bullets you carry").
- OPEN QUESTIONS FOR THE OWNER (also in the decisions above): T26 (is a tap the problem, or does Alt stick?), T27 (should the shotgun really load shell by shell?), T25 (unarmed guards drop pistol bullets too), and the Vendor still sells ammunition in packs of one magazine (the "Pistol Clip" is 8 bullets for $120): per bullet instead?
'@.TrimEnd()
Rep '**State at hand-off:** (rewritten at the end of the session; if this line still says "nothing built yet", nothing below the tracker was done.) Nothing built yet.' $handoff
Save-Src
"notes checkpoint 1 patched"
