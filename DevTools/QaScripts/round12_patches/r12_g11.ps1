# Round 12, batch G11: the tests follow G10: the intro's new length and camera set-up, floor 3 without guns in its safes (the Armour and Outpost scenarios and the built-player autopilot take their pistol from a floor 4 safe),
# the pistol's twelve clips (G08), the new Round12 pure tests and scenario, the vendor's bay in the Etr scenario.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$E = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$E\NetDevIntroTest.cs"
Rep 'Check("intro: the beats follow one another from 0 to the total (35 s) with no gap or overlap", contiguous && longEnough && IntroRules.Total == 35f && beats.Length == 10);' 'Check("intro: the beats follow one another from 0 to the total (37.8 s) with no gap or overlap", contiguous && longEnough && IntroRules.Total == 37.8f && beats.Length == 10);'
Rep 'Check("intro: the cutscene uses all five camera set-ups in the docks (across the room, behind the workers, close on the TV, their faces, the two who talk)", used.Count == 5 && docks);' 'Check("intro: the cutscene uses all six camera set-ups in the docks (across the room, behind the workers, close on the TV, their faces, the two who talk, the vendor''s bay)", used.Count == 6 && docks);'
Rep 'IntroRules.BeatAt(34.99f)' 'IntroRules.BeatAt(37.79f)'
Save-Src

Open-Src "$E\NetDevR7Test.cs"
Rep 'if (!SafeRules.HoldsArmour(rs, f, i)) { clipSafes++; if (clip) clipHits++; }' 'if (!SafeRules.HoldsArmour(rs, f, i) && SafeRules.SafesHoldGuns(f)) { clipSafes++; if (clip) clipHits++; }'
Save-Src

Open-Src "$E\NetDevArmour.cs"
Rep 'if (SafeRules.HoldsGun(s, 3, 0) && SafeRules.HoldsArmour(s, 3, 1) && SafeRules.PickArmour(s, 3, 1, 0) == ShieldTier.Light) seed = s;' 'if (!SafeRules.HoldsArmour(s, 3, 0) && SafeRules.HoldsArmour(s, 3, 1) && SafeRules.PickArmour(s, 3, 1, 0) == ShieldTier.Light) seed = s;          // (floor 3 holds no gun in its safes: the first safe is empty)'
Rep 'floor 3: the first safe holds a gun, the second Light Armour instead of one;' 'floor 3: the first safe holds nothing (floor 3 has no gun in its safes), the second Light Armour;'
Rep 'safe0.GunCount == 1 && safe1.GunCount == 1' 'safe0.GunCount == 0 && safe1.GunCount == 1'
Save-Src

Open-Src "$E\NetDevOutpost.cs"
Rep '!SideItemsOn(seed, 3)' '!SideItemsOn(seed, 4)'
Rep '!WatchOn(seed, 3)' '!WatchOn(seed, 4)'
Rep '!GunsOn(seed, 3)' '!FirstClipSafeHoldsGun(seed, 4)'
Rep 'foreach (int wsFloor in new[] { 3, 7 })' 'foreach (int wsFloor in new[] { 4, 7 })'
Rep 'SafeIntel.Add(wsFloor == 3 ? 7 : 3, 0, 1, "999", 1111);' 'SafeIntel.Add(wsFloor == 4 ? 7 : 4, 0, 1, "999", 1111);'
Rep 'if (wsFloor == 3)' 'if (wsFloor == 4)'
Rep @'
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HoldsGun(runSeed, floor, i)) return true;
            return false;
        }
'@ @'
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HoldsGun(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>Does the first safe of this floor that holds a spare clip also hold a gun (the test takes the pistol and the clip from that safe)? Floor 3 holds neither: the first weapon floor with guns in its safes is 4.</summary>
        static bool FirstClipSafeHoldsGun(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HasSideClip(runSeed, floor, i)) return SafeRules.HoldsGun(runSeed, floor, i);
            return false;
        }
'@
Save-Src

# the pure tests and the scenario of this round
Open-Src "$E\NetDevTest.cs"
Rep 'ArmourTest();' @'
ArmourTest();
            Round12Test();
'@
Save-Src

Open-Src "$E\NetDevHarness.cs"
Rep 'mode != "assetpreview" && mode != "citynew" && mode != "checklist"' 'mode != "assetpreview" && mode != "citynew" && mode != "round12" && mode != "checklist"'
Rep @'
        public static void CityNew()
'@ @'
        public static void Round12()
        {
            SessionState.SetString("netdev.mode", "round12");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void CityNew()
'@
Rep @'
                case "citynew": yield return CityNewScenario(); break;
'@ @'
                case "citynew": yield return CityNewScenario(); break;
                case "round12": yield return Round12Scenario(); break;
'@
Save-Src

# the vendor's bay in the intro's TV scenario: the door is shut while the workers talk, and rolled up in the last beat
Open-Src "$E\NetDevEtr.cs"
Rep 'if (i == IntroRules.BeatAt(IntroRules.JetStart)) at = IntroRules.JetStart + 1.5f;' @'
if (i == IntroRules.BeatAt(IntroRules.JetStart)) at = IntroRules.JetStart + 1.5f;
                if (beats[i].dock == DockShot.Bay) at = beats[i].start + 0.2f;
'@
Rep @'
                yield return null; yield return null;
                var c = intro.DockCamera.transform.position;
'@ @'
                yield return null; yield return null;
                if (beats[i].dock == DockShot.TwoShot) EtrCheck("tv: while the workers talk the vendor's bay door is still shut", DockBayDoor.Current != null && DockBayDoor.Current.IsShut);
                if (beats[i].dock == DockShot.Bay)
                {
                    yield return new WaitForSeconds(IntroRules.BayOpensAt - at + DockBayDoor.RollSeconds + 0.3f);
                    EtrCheck("tv: after the workers' line the vendor's bay door has rolled up and he is there to be seen (the owner, 2026-10-19 night)", DockBayDoor.Current != null && DockBayDoor.Current.IsOpen);
                }
                var c = intro.DockCamera.transform.position;
'@
Save-Src

# the built-player autopilot: the safe and guard steps of the network test run on floor 4 (floor 3's safes hold no pistol and no clip any more), and the Intro lasts 37.8 s
Open-Src 'C:\bqm\Assets\Scripts\QaAutopilot.cs'
Rep 'for (int i = 0, n = SafeRules.SafeCount(intel, 3); i < n; i++)' 'for (int i = 0, n = SafeRules.SafeCount(intel, 4); i < n; i++)'
Rep 'if (SafeRules.HoldsGun(intel, 3, i) && SafeRules.HasSideClip(intel, 3, i) && SafeRules.HasSideCash(intel, 3, i) && SafeRules.HasSideWatch(intel, 3, i)) return s;' 'if (SafeRules.HoldsGun(intel, 4, i) && SafeRules.HasSideClip(intel, 4, i) && SafeRules.HasSideCash(intel, 4, i) && SafeRules.HasSideWatch(intel, 4, i)) return s;'
Rep 'for (int floor = 1; floor <= 3; floor++)' 'for (int floor = 1; floor <= 4; floor++)' 2
Rep 'if (floor == 3) { yield return SafesNet(true, floor); yield return GuardsNet(true, floor); }' 'if (floor == 4) { yield return SafesNet(true, floor); yield return GuardsNet(true, floor); }'
Rep 'if (floor == 3) { yield return SafesNet(false, floor); yield return GuardsNet(false, floor); }' 'if (floor == 4) { yield return SafesNet(false, floor); yield return GuardsNet(false, floor); }'
Rep 'this run''s floor-3 safes hold a pew' 'this run''s floor-4 safes hold a pew'
Rep 'the Intro ends by itself after its 35 s and was shown once' 'the Intro ends by itself after its 37.8 s and was shown once' 2
Save-Src
