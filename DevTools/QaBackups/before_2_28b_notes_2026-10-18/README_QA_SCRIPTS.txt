Q/A and build scripts of the agents' sessions (2026-10-16). REFERENCE COPIES: nothing here is needed to play, build or edit the game, and Unity ignores this folder (it is outside Assets).
They are what the agents used to test the game and to make the friends' builds. DEVELOPER_NOTES.md sections 2.18, 2.19, 8.6 and 8.10 explain the method; read those first.

Layout they expect: a scratch folder (the agents used their session "scratchpad" under %LOCALAPPDATA%\Temp\claude\...) that holds
  - these scripts,
  - BE_qa : a COPY of the project (everything except Library; refill Library\PackageCache from the real project before every Unity run: the scripts do it),
  - and they create Builds\, Package\, Unzipped\, the logs, the pictures and qa_summary_<tag>.txt next to them.
At the top of each script: $real = the real project, $unity = the Unity 6000.0.45f1 editor, and in qa_mp.ps1 / qa_sync_clients.ps1 $old = the folder that holds the client copies (BE_dev_c ... BE_dev_late). Adjust them for your machine.
Never run Unity on the owner's open project (Unity locks it) and never start two multi-process runs at once (they share the loopback port and %TEMP%\be_sig_*.txt; the scripts wait on %TEMP%\be_mp_busy.txt).
Windows deletes OLD files under %TEMP% (the scratch copies lose their asset packs): run a "touch" over a copy after every robocopy mirror (memory note in the agents' folder; DEVELOPER_NOTES 8.6).
2026-10-18 (round 8): that happened again to the QA tree C:\bq (a junction into a session scratchpad under %TEMP%). The QA tree is now C:\bqm, a real folder OUTSIDE %TEMP% (nothing to touch), made by copying the pristine build copy C:\bqfinal WITHOUT its Library and importing from scratch (about 2 minutes). Do NOT copy a Library between folders: the copy (C:\bqn) did not resolve any script GUID ("The referenced script (Unknown) on this Behaviour is missing"). The scripts here point at C:\bqm.

  qa_final.ps1   everything for a friends' build: tooling, compile + pure tests, build, package (zip + README), the unzipped build plays itself solo and as host + client.   .\qa_final.ps1 -Tag f4 -Date 2026-10-16-intro
  qa_regress.ps1 compile + pure tests, then single-process Play Mode scenarios one after another.  .\qa_regress.ps1 -Prefix q1 [-Methods Offline,Demo,Shaft:gfx]   (-Methods @() = pure tests only)
  qa_mp.ps1      multi-process scenarios (one Unity per player).                                    .\qa_mp.ps1 -Prefix m1 -Runs 'Armed:host,c1','DemoNet:host,c1'
  qa_intro.ps1   builds the player (or tests the exe given with -Exe) and lets the BUILT game play the run-start cinematic: a lobby of one (HOST LOCAL + START RUN) and PLAY SOLO (no network session), each played out and with Space held, then host + client.
  qa_offline.ps1 just the two PLAY SOLO cinematic tests (build or -Exe).
  ui_run.ps1     runs an Editor method of a project copy in a real, NON-batch Editor on a hidden desktop (pictures of IMGUI; the Editor's own Play Mode): .\ui_run.ps1 -Project <copy> -Class NetDevHarness -Method IntroOffline -Tag x   (NetDevHarness.IntroOffline = Play pressed on the Greybox scene, IntroSolo = Boot, menu, PLAY SOLO).
  qa_shots.ps1   builds the player and takes two pictures of every beat of the cinematic (look at them after any change to BossOffice or the camera rigs; -Model male_a tries another boss).
  qa_new_apply.ps1, qa_sync_clients.ps1   put the Q/A tooling into an OLD tree / mirror BE_qa into the client copies (the repo already has the tooling).
  cmp_trees.ps1  compares two project trees by content (ignores line endings).
  patchlib.ps1   Open-Src / Rep / Save-Src: patch a source file by exact match, keeping its line endings (a patch that does not match exactly stops).
  hd.ps1         runs a program on a hidden Windows desktop (a game window never shows or takes focus); prints the exit code.
  qa_new\README_DEMO.txt   the friends' README that qa_final.ps1 puts in the zip.

  --- added 2026-10-17 (the nine requests, DEVELOPER_NOTES 2.21) ---
  qa_chain.ps1   the unattended chain: render audit (pink shaders), the IMGUI picture scenarios on a hidden desktop, then every multi-process run (qa_mp.ps1).   .\qa_chain.ps1 -Prefix c2 -MpRuns 'Armed:host,c1','Qa:host,c1,c2,c3,c4,late6:5'
  ui_bq.ps1      runs one NetDevHarness method of BE_qa in a real, NON-batch Editor on a hidden desktop. The run-start cutscene and the loot scanner need this (IMGUI is not drawn in batch mode, and the cutscene is off in batch mode on purpose):
                 .\ui_bq.ps1 -Method IntroTv|IntroOffline|IntroSolo|EtrScan -Tag u1      (pictures land in %TEMP%\be_ui_*.png / be_intro_*.png)
  etr_apply.ps1  applies the tested BE_qa to the real project: every changed or new script, editor tool, resource, setting and DevTools file, with a backup (-Backup <dir>); scripts keep the EOL of the file they replace; -WhatIf lists first.
  deliver_build.ps1  copies the tested build (zip + unzipped copy + QA results) into the repo's Builds\ folder and checks it by SHA-256.   .\deliver_build.ps1 -Name Demo_2026-10-17_etr -Date 2026-10-17-etr -Tag f10 -IntroTag in10
  The game is "Employees Take Revenge" now: the exe is EmployeesTakeRevenge.exe and the package names are EmployeesTakeRevenge_Demo_<date>.

ROUND 2 (2026-10-17 afternoon): round2_patches\r2_batch1..15.ps1 are the changes made to the QA tree in order (read DEVELOPER_NOTES 2.22 first); r2_pipeline.ps1 = single-process scenarios + client sync + qa_chain in one go (about 40 minutes, nothing else may use Unity or the loopback port meanwhile); new scenarios: Looks (qa_regress.ps1 -Methods Looks:gfx), FloatQa, ComputerUi (ui_bq.ps1 -Method ComputerUi). etr_apply.ps1 now also moves files that were removed from the QA tree into the backup folder.

ROUND 8, 2026-10-18 evening (DEVELOPER_NOTES 2.27 update 8): round8_patches\r8_f12..r8_f20.ps1 are the changes made to the QA tree in order (the Synty weapons, the silencer by weapon class and its exit points, the gold watch in safes, the blue UI test and then the blue UI as the only look); ui_final_*.cs.txt are the final HudTheme / MenuKit.
  uipics.ps1    runs the ui.ps1 picture scenarios one after another and collects every be_*.png they write into C:\bqs\uipics\<Tag>.   .\uipics.ps1 -Tag final -Methods Ui,InvUi,DemoUi,Menus,MenusLobby,MenusRun
  montage.ps1   two pictures of the same screen side by side with a caption bar.      piccmp.ps1  how many pixels differ between two folders of pictures.
  picdiffimg.ps1 a picture of WHERE two pictures differ (red).                         piccrop.ps1 the same region of two pictures, enlarged, one above the other.
  Weapon pictures: ui.ps1 -Methods WeaponSheet (the items lying, the pistol from above / end on, an open safe with pistol, clip, cash and watch), PewLook (the silenced pistol in every character's hand), MeleeLook (crowbar, bat, fire axe in the hand and frame by frame in the swing).
  QA-only editor tools: Assets\Editor\NetDev\WeaponInspect.cs (-executeMethod BuildingEscape.EditorTools.WeaponInspect.Muzzle prints the pistol's muzzle ring and the silencer's rings and exits) and WeaponCatalogBuilder.UpgradeBatch (fills the weapon catalog with the Synty models and exits). run_method.ps1 does NOT pass -quit: the method must call EditorApplication.Exit(0).