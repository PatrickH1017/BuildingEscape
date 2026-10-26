THE THIRD AGENT'S WORK ON THE OWNER'S TASK LIST OF 2026-10-23 (DEVELOPER_NOTES.md section 2.63 is the master document; second_agent\README.txt describes the method and the tools, this folder only adds to it)
==============================================================================================================================================================================================

Applied to the real project (2026-10-23, 21:25; backups in DevTools\QaBackups\before_task_list_d_2026-10-23\), in this order into the scratch copy C:\bd after the second agent's T1, T12, T5:
  patch_floor6.ps1            "When making the new build it should go until floor 6 instead of floor 4": TowerRules.DemoLastFloor 4 -> 6 and every test / autopilot loop that assumed four floors now reads the constant.
  second_agent\patch_t12b.ps1 busted teammates' bodies can be dragged (the third agent's patch_t12c.ps1 below rewrote how the body follows).
  second_agent\patch_t29.ps1  the developer spectator, part 1.
  patch_t29b.ps1              T29 part 2: free camera (F), overlay (O), network panel (N), and the host -> spectator record (health, target; RunSync.DevView.cs). New files: t29b_new\.  -Root <dir> patches another tree.
  patch_t12c.ps1              a dragged body follows the PATH the player walked (DraggableBodyRules.Trail), not a spot behind where he faces. New files: t12c_new\ (pure checks Asks1023J).
  patch_netk.ps1              the two-machine scenario DragNet (a client buys / holds out / puts away the walkie talkie; a client drags a guard's body). New file: netk_new\NetDevAsks1023K.cs.
  patch_autopilot_bullets.ps1 the built-player autopilot's guard drop test expects bullets that are run over (not a clip taken with E).
  patch_fix_f1.ps1            test flaw: the busted-teammate block of Asks1023F looks for an open spot at 8, 7, 6 and 5.5 m.
  patch_fix_k1.ps1            a REAL BUG found by DragNet: a body dragged by a CLIENT was let go at once on the host (isActiveAndEnabled on a player whose WobblyPlayer is switched off on every machine but his own); and a race in the older DemoNet client.
  apply_chain_to_scratch.ps1  runs the chain above in order (the fixes F1 and K1 were applied by hand afterwards).
  run_mp_series.ps1           the two-machine runs m1 .. m5 in a row (qa_mp_bd.ps1: host C:\bd, second machine C:\bdc, -Sync mirrors and compiles it first).
  patch_notes_third1.ps1, patch_notes_third2.ps1, patch_notes_handoff3.ps1   the edits of DEVELOPER_NOTES.md (tracker, STATUS lines, decisions, words, open questions, the hand-off block).

The friends' build and its self-play (2026-10-23):
  build_third.ps1            the whole shipping run: real project -> C:\bqfinal, IL2CPP player, send copy (+ README_SEND_2026-10-23.txt), safety scan, zip, then the built game plays itself (solo, then two built players). Summary: C:\bqs\ship_summary.txt. Usage: .\build_third.ps1 -Name 2026-10-23b -Raw Final33_2026-10-23 -Tag fm2   (each ship takes about 1.5 GB of C:).
  diag_build.ps1             the same without the send copy / scan / zip: builds the SCRATCH copy C:\bd into C:\bqs\diag_build (replaced every time) and lets it play itself (C:\bqs\final_ap_summary.txt, C:\bqs\<Tag>_ap_*). Use it to check an autopilot change before it is applied to the real project.
  patch_autopilot_floor6.ps1 the first built self-play of the floor-6 build (Playtest_2026-10-23a: solo 116 PASS / 7 FAIL, host 36 / 2, client 44 / 1) found out-of-date CHECKS, not game bugs: the first-person arms check (the body is not drawn in first person since 2026-10-20), the hall doors' window count (floors 1 to 7 have the pack's glass doors) and a race in the two-machine guard drop test (+ diagnostics "[diag]" in its report). Patches QaAutopilot.cs.
  final_autopilot_6_floors.ps1 a copy of C:\bqs\final_autopilot.ps1 with the timeouts raised from 900 s to 2100 s (six floors take longer).
  make_readme_send.ps1       writes README_SEND_2026-10-23.txt (the friends' README for floors 1 to 6) from C:\bqs\README_SEND.txt; final_build.ps1 takes it with -Readme. Its result: README_SEND_2026-10-23.txt.

NOT applied yet (written, apply cleanly on a staging copy of the code, never compiled or tested):
  patch_t7_probe.ps1 + t7_new\ T7, first step only: the scenario PvpMap prints a text map of the PVP room (one character per 0.25 m) and lists every solid thing in it, to design the battlefield on. The battlefield itself (the Battle Royale props) is not designed yet.
  patch_t6.ps1  + t6_new\     T6 + T23: vending machines sell the consumables; the black machine; the green screen (the first agent's C:\bqs\c2, renamed to the scenarios Asks1023V / Asks1023VUi, anchors updated).
  patch_t16.ps1 + t16_new\    T16: the computer look-around and E at the microphone (the first agent's C:\bqs\c3, renamed to Asks1023W). Needs patch_t6.ps1 first (its registration anchors).

The Walkie talkie model (T5): the SpawnCampGames model is a Blender file (Assets\SpawnCampGames\WalkieTalkie\Models\WalkieTalkie.blend). Unity can only import a .blend when Blender is installed, and this machine has none (the .blend file association points to a Blender 5.2 that
is gone, Program Files holds only an empty "Blender 4.5" folder). The .blend.meta of the real project has an empty internalIDToNameTable: it was never imported here either. Until Blender is installed (free, blender.org; the installer blender-5.2.1-windows-x64.msi is still in the owner's
Downloads folder) the walkie is drawn as a plain dark box, in the hand and on the floor; with Blender installed Unity imports the model by itself the next time the project is opened (no code change). NetDevWalkieProbe.Run / .Reimport (second_agent\t5_new) print what Unity made of the file.
