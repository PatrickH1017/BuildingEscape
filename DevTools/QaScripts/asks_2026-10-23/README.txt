The owner's task list of 2026-10-23 (DEVELOPER_NOTES.md section 2.63): the scripts and the new files of the work IN FLIGHT. REFERENCE COPIES: nothing here is needed to play, build or edit the game.
READ DEVELOPER_NOTES.md 2.63 FIRST ("State at hand-off" says what is applied to the real project, what is only in the scratch copy, and what to do next). This folder only keeps the tools of the work.

HOW THE WORK IS DONE (the same as floor7_doors_2026-10-23\README.txt - read that one for the tools): a scratch copy of the real project at C:\bd (a short junction, the Synty shaders need a short path), everything compiled and tested there
with qa_regress_bd.ps1 / run_method_bd.ps1 (copies in floor7_doors_2026-10-23), and only verified work goes into the real project with apply_checked.ps1 (it refuses a file the real project changed itself since the snapshot).
NEVER run Unity on the project the owner's Editor has open. The scratch copy may be gone when you read this: make a new one the way the doors README says (robocopy of Assets, ProjectSettings and Packages to a short path), then run the patches below in order
(each has a -Dry switch: it checks every exact match and saves nothing; a script that fails a match stops with the text it could not find, nothing is half-saved by the dry run).

THE FILES (the order they were run against C:\bd)
  patchlib.ps1, patchlib2.ps1   the exact-match patch helpers (Open-Src, Rep, RepRx, RepRegion, Save-Src; line endings kept); the patch scripts dot-source them from C:\bqs
  patch_a1.ps1    batch A1: T19 elevator buttons, T20 weights (half), T21 scan delay (3 s), T10 vendor weapon card lines, T15 computer tabs (Nav, Safes, Commands; Nav opens first), T18 every safe's code, T8 drop all loot (part 1),
                  T17 floor 1's crowbar, T14 guards by floor (cap 24 alive, 18 patrolling), and the harness registration of the scenario "asks1023"
  patch_a1b.ps1  T8, part 2: Pickup.CountsAsLoot never counts a weapon; the pieces of a "drop all" are laid out in rings (Backpack.DropSpread)
  patch_a1c.ps1  the old policy-plan test that builds a floor of exactly 12 guards counts its rifles for 12 guards
  patch_t26.ps1   T26: Alt (or V) must be HELD 0.25 s before the view unlocks (FreeLookRules, CameraFollow)
  new_files\Assets\...   the new source files the patches rely on or the scenarios use (copy them to the same relative path under the project; new scripts get CRLF when applied):
                  Scripts\Interior\ElevatorPanelRules.cs, Scripts\Hud\WeaponCardRules.cs, Scripts\FreeLookRules.cs, Editor\NetDev\NetDevAsks1023Test.cs (pure checks), Editor\NetDev\NetDevAsks1023.cs (the Play scenario),
                  Scripts\Guns\GuardDropRules.cs and Scripts\Guns\AmmoRunOver.cs (T25, written but NOT yet compiled when this was saved)
  The copies in new_files are a SNAPSHOT of the scratch copy at the time of the last notes update; the scratch copy (or the real project, once applied) has the newer versions.

RUN THE CHECKS (in the scratch copy; a done-marker is "finished <time>" at the end of C:\bqs\qa_summary_<Prefix>.txt):
  .\qa_regress_bd.ps1 -Prefix a1 -Methods Asks1023     compile + all pure checks (about 4 min), then the Play scenario; the scenario log ends with "[Asks1023] DONE fails=0"
  (the scenario files are NetDevAsks1023*.cs; a scenario is registered in NetDevHarness.cs in three places: the public static method, the exclusion list "mode != ..." and the case in the switch)
