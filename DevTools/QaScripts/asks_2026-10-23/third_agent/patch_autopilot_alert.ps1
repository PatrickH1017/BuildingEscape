# The built-player self-play (QaAutopilot) checks the owner's asks of the evening of 2026-10-24 in the BUILT game (the lesson of the friends' playtest: every player-facing flow gets a built-player check):
# the scan's one pop-up and its error sound, the checklist's fifth line, every floor's alert rules, and the Big Alarm for a solo player.
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\QaAutopilot.cs')
Rep @'
                    Check("solo: J opens the journal on its CHECKLIST tab", IntelScreens.Instance.JournalTab == 1 && OutpostUi.Current == OutpostUi.Kind.Journal, $"(tab {IntelScreens.Instance.JournalTab})");
'@ @'
                    Check("solo: J opens the journal on its CHECKLIST tab", IntelScreens.Instance.JournalTab == 1 && OutpostUi.Current == OutpostUi.Kind.Journal, $"(tab {IntelScreens.Instance.JournalTab})");
                    Check("solo: the checklist has five lines and the fifth is the guards' outpost (not done yet)", ChecklistRules.Count == 5 && ChecklistRules.Label(ScanKind.Outpost) == ChecklistRules.OutpostLabel && !FloorChecklistTracker.IsDone(ScanKind.Outpost));
'@
Rep '                yield return EtrFloorChecks(floor, gen, hm, tm);' @'
                yield return EtrFloorChecks(floor, gen, hm, tm);
                Check($"floor {floor}: the guards' alert starts quiet: nobody marked, no Big Alarm, and the cap is {AlertRules.GuardCap(floor, false)} guards ({AlertRules.GuardCap(floor, true)} with the Big Alarm)", hm.MarkedCount == 0 && !hm.BigAlarmActive && hm.GuardCap == AlertRules.GuardCap(floor, false) && hm.GuardCap == (floor <= 2 ? 6 : 13), $"(marked {hm.MarkedCount}, big {hm.BigAlarmActive}, cap {hm.GuardCap})");
'@
Rep @'
                Check($"floor {floor}: holding Z scans the room the player is in (its {inRoom.Count} valuables marked, nothing from the next room)", all && onlyThose && LootScanner.Scanning, $"({scanner.MarkCount} marks, '{scanner.StatusLine}')");
'@ @'
                Check($"floor {floor}: holding Z scans the room the player is in (its {inRoom.Count} valuables marked, nothing from the next room)", all && onlyThose && LootScanner.Scanning, $"({scanner.MarkCount} marks, '{scanner.StatusLine}')");
                Check($"floor {floor}: the scan's only pop-up says Item Marked, and it is up now that the scan marked things", scanner.PopupText == LootScanRules.MarkedText, $"('{scanner.PopupText}')");
'@
Rep @'
                LootScanner.TestHold = false;
                yield return Wait(0.3f);
            }
            else Check($"floor {floor}: there is a room with loot to scan", false);
'@ @'
                LootScanner.TestHold = false;
                yield return Wait(0.1f);
                int beeps0 = LootScanner.ErrorBeeps;                                              // (the owner, 2026-10-24: a press while the 2 s cooldown holds the scan back makes an error sound, no pop-up)
                LootScanner.TestKeyDown = true;
                yield return Wait(0.1f);
                LootScanner.TestKeyDown = false;
                Check($"floor {floor}: pressing Z in the 2 s cooldown makes the error sound once", LootScanner.ErrorBeeps == beeps0 + 1, $"({LootScanner.ErrorBeeps - beeps0} sounds)");
                yield return Wait(0.3f);
            }
            else Check($"floor {floor}: there is a room with loot to scan", false);
            if (floor == TowerRules.DemoLastFloor && WobblyPlayerRegistry.All.Count > 0)
            {
                var spotted = WobblyPlayerRegistry.All[0];
                bool bigNow = hm.MarkSpotted(spotted);                                             // (a solo player who is spotted is the whole party: the Big Alarm, the owner, 2026-10-24)
                Check($"floor {floor}: a solo player who is spotted is marked and the Big Alarm goes on (the cap is {AlertRules.GuardCap(floor, true)}); the stand-down refuses", bigNow && hm.BigAlarmActive && hm.IsMarked(spotted) && !hm.StandDown(), $"(big {hm.BigAlarmActive}, cap {hm.GuardCap})");
            }
'@
Save-Src
"autopilot alert patched"
