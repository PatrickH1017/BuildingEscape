Floor 7 doors, 2026-10-23 (DEVELOPER_NOTES.md section 2.62): the scripts of that session. REFERENCE COPIES: nothing here is needed to play, build or edit the game.

WHY A NEW WAY OF WORKING: the old QA tree C:\bqm was STALE that day (69 files differed from the real project and 59 files of the real project were missing in it: sections 2.38 - 2.61 had been written straight into the real project by sessions without Unity),
so the work was done in a fresh scratch copy of the real project instead:
  1. C:\bd = a junction (short path, the Synty shaders need it) to a folder in the session scratchpad that already held an imported project (with its Library). It was brought up to date with
        robocopy <real>\Assets C:\bd\Assets /E /XO   (and ProjectSettings, Packages)
     /XO copies only what is newer in the real project (the scratch files carry a later "touch" time, see README_QA_SCRIPTS.txt), then cmp_trees.ps1 -B C:\bd proved the code folders identical (720 files, 0 differences).
  2. The real project's code folders were hashed at that moment (snapshot_real.ps1 -Out snapshot_real_t0.tsv).
  3. All work and every test ran in C:\bd (qa_regress_bd.ps1 and run_method_bd.ps1 are qa_regress.ps1 / run_method.ps1 with the project path set to C:\bd; ui.ps1 takes -Project C:\bd as it is).
  4. apply_checked.ps1 put the finished work into the real project: ONLY the files that differ from the snapshot (the scratch's own changes), a backup of every file it replaces, and it REFUSES (lists as a conflict) any file the real
     project changed itself since the snapshot, so an older scratch file can never overwrite somebody else's edit. Scripts and the office catalog keep the line endings of the file they replace (new scripts get CRLF).

THE FILES
  snapshot_real.ps1            the T0 snapshot of the real project's code folders (hashes)
  apply_checked.ps1            the checked apply (.\apply_checked.ps1 -Backup <folder> [-WhatIf]; the snapshot path is a parameter)
  before_after.ps1             before / after pictures side by side from %TEMP%\be_shot_before_N.png and be_shot_after_N.png (and an overview sheet)
  patch_doorskin_1.ps1         the patch of the existing files (FloorGenerator.Doors.cs, PropTable.cs, NetDevTest.cs): exact matches only (patchlib.ps1)
  patch_harness_doorlooks.ps1  registers the DoorLooks scenario in NetDevHarness.cs
  patch_looks_picks.ps1        the last edit of the DoorLooks scenario (doors by number)
  qa_regress_bd.ps1, run_method_bd.ps1   the runners pointed at C:\bd

LOOK AT THE DOORS YOURSELF (in a scratch copy, never in the project the Editor has open):
  $env:BE_DOOR_TAG = 'after'; $env:BE_DOOR_PICKS = '6,0,2,12,19,23,4,18'; .\ui.ps1 -Methods DoorLooks -Project C:\bd -Prefix dl
  ($env:BE_DOOR_PACK = '0' shows the old doors in the same build; BE_DOOR_PICS = '0' only checks; BE_DOOR_FLOOR picks another floor; BE_DOOR_SHEETS = '1' adds contact sheets of the pack's own doors.)
  Pictures land in %TEMP%\be_shot_<tag>_*.png; the log ends with "[DoorLooks] DONE fails=0".
