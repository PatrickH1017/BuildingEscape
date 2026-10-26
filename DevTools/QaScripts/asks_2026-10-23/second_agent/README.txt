THE SECOND AGENT'S WORK ON THE OWNER'S TASK LIST OF 2026-10-23 (DEVELOPER_NOTES.md section 2.63 is the master document; this folder is its tool box)
=====================================================================================================================================

What is here
------------
patch_*.ps1      exact-match patch scripts (they use C:\bqs\patchlib.ps1 / patchlib2.ps1: Open-Src, Rep, RepRx, RepRegion, Save-Src). Every one has a -Dry switch that only checks that every anchor still matches.
                 They patch the SCRATCH copy C:\bd, never the real project; the real project only ever gets files that passed the tests (apply_checked.ps1, below).
t1_new\ t5_new\ t12_new\ t29_new\   the new files each patch writes (CRLF is added when it writes them).
qa_mp_bd.ps1     the two-machine runner (host = C:\bd, the second machine = C:\bdc, a copy of the same project; -Sync mirrors C:\bd into C:\bdc and compiles it first).
t12_draft\       OLD drafts of T12 (superseded by t12_new\).
patch_notes_*.ps1   the edits of DEVELOPER_NOTES.md (tracker, STATUS lines, hand-off paragraph).

The order the patches were applied to the scratch copy C:\bd
------------------------------------------------------------
 1. patch_fix_tests_2.ps1, patch_notes_a2.ps1, patch_notes_handoff2.ps1, patch_notes_priority.ps1        (batch A2 + B + C: already in the real project)
 2. patch_t1.ps1  (+ patch_fix_e.ps1)                    T1  floors 1-6 get the Floor 7 art (FloorArtRules)
 3. patch_t12.ps1                                        T12 guards' bodies can be dragged (protocol 28)
 4. patch_t5.ps1  (+ patch_fix_t5_tests.ps1, patch_fix_walkie1.ps1, patch_fix_c.ps1)   T5 walkie talkie (+ the old tests whose numbers moved by one, a test flake of Asks1023C)
 5. patch_t12b.ps1                                       T12 busted teammates' bodies
 6. patch_t29.ps1                                        T29 the developer spectator

How a task is built and checked (the method of every task)
----------------------------------------------------------
  C:\bqs\cmp_trees.ps1 -B C:\bd                        scratch copy against the real project (must show only the patches you expect)
  <patch>.ps1 -Dry                                      anchors match?   then  <patch>.ps1
  C:\bqs\qa_regress_bd.ps1 -Prefix x -Methods A,B,C     compile (7-8 minutes) + the pure checks (NetDevTest.PolicyTest) + the Play scenarios one after another; read C:\bqs\qa_summary_x.txt
  C:\bqs\qa_mp_bd.ps1 -Method DevSpectate -Prefix m1 [-Sync]    the two-machine scenarios (DevSpectate, DevDenied)
  C:\bqs\ui.ps1 -Methods WalkieLook -Project C:\bd -Prefix p    pictures from an Editor on a hidden desktop (ShotCam: %TEMP%\be_shot_*.png)
  DevTools\QaScripts\floor7_doors_2026-10-23\snapshot_real.ps1 -Out snapshot.tsv       hash the real project's code folders BEFORE applying
  DevTools\QaScripts\floor7_doors_2026-10-23\apply_checked.ps1 -Backup <dir> -Snapshot snapshot.tsv [-WhatIf]    copies only the scratch's own changes (refuses a file the real project changed since the snapshot)
A scenario is registered in NetDevHarness.cs in three places (the public static method, the mode list, the switch case) and its pure checks in NetDevTest.PolicyTest. A scenario's log must end with  DONE fails=N.
Never run Unity on the project the owner's Editor has open. One heavy Unity job at a time. Sleeping inside a command is blocked: run long jobs in the background and wait for their notification.
