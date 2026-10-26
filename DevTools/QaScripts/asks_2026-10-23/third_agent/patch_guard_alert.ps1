# The owner, 2026-10-24 (the complete task list): "In multiplayer when a player gets spotted it should only mark that player not all players, if another player is spotted they should now be marked as well";
# "If a player eliminates a guard on floors 1-2, another guard should spawn after 45 seconds when backup is not alerted (until floor cap is reached), when backup is alerted the respawn time should drop to 20 seconds";
# "The Big Alarm acts as when 50% or more of the party (except for duos, in duos only when both players get marked the big alarm activates) has been marked then the cap of guards should raise to the given number";
# "Guards should be capped at 6 on floors 1-2 but when the Big Alarm is alerted the cap should raise to 8 (floors 3-13 should have a cap of 13)";
# "If the big alarm is not active and the marked player(s) get pinned, the security alert should turn off and the respawn rate of backup guards should go back to 45 seconds";
# "Once the big alarm is activated there is no turning it off, players must beat the quota and fight their way out"; "Any changes mentioned here should be applied to all floors ... if we don't provide just use a placeholder".
# AlertRules / AlertState (pure) + HeistManager (marks, Big Alarm, respawn queue, caps, the alert going off) + GuardAI (a call marks the spotted player; a chasing guard marks a second player he notices and chases marked players only) + RunSync (the Big Alarm flag)
# + the alarm panel (it cannot shut the Big Alarm off). Patches the scratch copy C:\bd; exact matches only (patchlib); new files are written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 'alert_new\AlertRules.cs') ($A + 'Scripts\AlertRules.cs')
Put-Crlf (Join-Path $here 'alert_new\NetDevAlertRules.cs') ($A + 'Editor\NetDev\NetDevAlertRules.cs')
Put-Crlf (Join-Path $here 'alert_new\NetDevGuardAlert.cs') ($A + 'Editor\NetDev\NetDevGuardAlert.cs')

# ---------------------------------------------------------------- HeistManager
Open-Src ($A + 'Scripts\HeistManager.cs')
Rep '        Vector3 lastBackupPos;' @'
        Vector3 lastBackupPos;

        // ---- marked players and the Big Alarm (the owner, 2026-10-24): a sighting marks THAT player and nobody else; the alert goes off when every marked player is pinned (unless the Big Alarm is on); the Big Alarm stays on until the floor ends
        readonly AlertState alertState = new AlertState();
        bool netBigAlarm;
        /// <summary>Tests: count the party as this many players (0: count the players there are).</summary>
        public static int TestPartySize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAlertStatics() { TestPartySize = 0; }

        /// <summary>The Big Alarm is on on this floor: enough of the party was marked (<see cref="AlertRules.BigAlarmReached"/>). It never goes off before the floor ends.</summary>
        public bool BigAlarmActive => Net.NetRole.Simulates ? alertState.BigAlarm : netBigAlarm;
        /// <summary>How many players are marked now (host / offline).</summary>
        public int MarkedCount => alertState.MarkedCount;
        /// <summary>Is this player marked (host / offline)? A guard that spots a player marks him and nobody else.</summary>
        public bool IsMarked(WobblyPlayer p) => p && alertState.IsMarked(p.GetInstanceID());
        /// <summary>The most guards alive at once on this floor now: 6 on floors 1 and 2, 8 with the Big Alarm; 13 from floor 3 (<see cref="AlertRules.GuardCap"/>).</summary>
        public int GuardCap => AlertRules.GuardCap(Floor, BigAlarmActive);
'@
Rep '            radiosJammedUntil = 0f;' @'
            radiosJammedUntil = 0f;
            alertState.Reset(); netBigAlarm = false;           // (a new floor: nobody is marked, no Big Alarm)
'@
Rep '                TickReplacements();' @'
                TickReplacements();
                TickAlert();
'@
Rep 'int runTotal, int cash, int surplusPaid, int timeLeft, bool backupCalled, int backupIn = -1)' 'int runTotal, int cash, int surplusPaid, int timeLeft, bool backupCalled, int backupIn = -1, bool bigAlarm = false)'
Rep '            netBackupIn = backupIn;' @'
            netBackupIn = backupIn; netBigAlarm = bigAlarm;
'@
Rep '        public void CallBackup(GuardAI by)' '        public void CallBackup(GuardAI by, WobblyPlayer spotted = null)'
Rep @'
            lastBackupPos = by ? by.transform.position : Vector3.zero;
            BackupCalled = true;
            backupClock.Start(CustodyRules.BackupInterval(Floor));
            SendOfficers(2, lastBackupPos, "BACKUP CALLED");
'@ @'
            lastBackupPos = by ? by.transform.position : Vector3.zero;
            bool bigNow = MarkSpotted(spotted, false);                                  // (the player the guard spotted is marked, nobody else; an alarm or a body marks nobody)
            BackupCalled = true;
            backupClock.Start(CustodyRules.BackupInterval(Floor));
            SendOfficers(2, lastBackupPos, bigNow ? "BIG ALARM" : "BACKUP CALLED");
'@
Rep @'
            if (Phase != HeistPhase.Heist || !Net.NetRole.Simulates) return false;
            bool wasOn = backupClock.Active || BackupCalled;
'@ @'
            if (Phase != HeistPhase.Heist || !Net.NetRole.Simulates) return false;
            if (alertState.BigAlarm) return false;                                       // (the Big Alarm is never shut off: the owner, 2026-10-24)
            bool wasOn = backupClock.Active || BackupCalled;
'@
Rep @'
            backupClock.Reset();
            BackupCalled = false;
            radiosJammedUntil = Time.time + CustodyRules.StandDownJamSeconds;
'@ @'
            backupClock.Reset();
            BackupCalled = false;
            alertState.ClearMarks();
            radiosJammedUntil = Time.time + CustodyRules.StandDownJamSeconds;
'@
Rep @'
            var target = lastBackupPos;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.Custody == CustodyState.Free) { target = p.transform.position; break; }
'@ @'
            var target = lastBackupPos;
            bool found = false;
            foreach (var p in WobblyPlayerRegistry.All)                                  // (the officers go for a MARKED player who is free, the owner, 2026-10-24)
                if (p && p.Custody == CustodyState.Free && IsMarked(p)) { target = p.transform.position; found = true; break; }
            if (!found && alertState.MarkedCount == 0)                                   // (an alarm with nobody marked: the team, as before)
                foreach (var p in WobblyPlayerRegistry.All)
                    if (p && p.Custody == CustodyState.Free) { target = p.transform.position; break; }
'@
Rep '        void TickBackup(float dt)' @'
        /// <summary>The party: how many players are in the run (<see cref="TestPartySize"/> when a test sets it).</summary>
        int PartySize()
        {
            if (TestPartySize > 0) return TestPartySize;
            int n = 0;
            foreach (var p in WobblyPlayerRegistry.All) if (p) n++;
            return n;
        }

        /// <summary>
        /// A guard spotted <paramref name="p"/> (host / offline; the owner, 2026-10-24: "when a player gets spotted it should only mark that player not all players, if another player is spotted they should now be marked as well"): that player is marked and nobody else.
        /// When enough of the party is marked (<see cref="AlertRules.BigAlarmReached"/>: half of it, both players of a duo) the Big Alarm goes on and stays on. True when this mark turned it on; <paramref name="announce"/> puts the banner up for it.
        /// </summary>
        public bool MarkSpotted(WobblyPlayer p, bool announce = true)
        {
            if (!p || !Net.NetRole.Simulates || Phase != HeistPhase.Heist) return false;
            bool big = alertState.Mark(p.GetInstanceID(), PartySize());
            if (big && announce) Announce("BIG ALARM");
            return big;
        }

        /// <summary>Host / offline, every frame: the alert goes off when every marked player is in custody (pinned or busted) and the Big Alarm is not on (the owner, 2026-10-24: "the security alert should turn off and the respawn rate of backup guards should go back to 45 seconds").</summary>
        void TickAlert()
        {
            if (alertState.MarkedCount == 0 || alertState.BigAlarm) return;
            int free = 0;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && alertState.IsMarked(p.GetInstanceID()) && p.Custody == CustodyState.Free) free++;
            if (!alertState.ShouldEnd(free)) return;
            alertState.ClearMarks();
            if (!(backupClock.Active || BackupCalled)) return;
            backupClock.Reset();
            BackupCalled = false;
            Announce("ALL QUIET - the alarm is off and no more officers are coming");
        }

        void TickBackup(float dt)
'@
Rep '            replacementsDue.Add(GuardRules.ReplacementDue(Time.time));' '            replacementsDue.Add(Time.time);                      // (when he went down: the wait is judged when it is over, by the alert as it is then)'
Rep '        /// <summary>A guard went down for good (host / offline): 15 seconds later (<see cref="GuardRules.ReplacementSeconds"/>) a new guard comes out of a stairwell. Only while the floor is a live heist; the boss is never replaced.</summary>' '        /// <summary>A guard went down for good (host / offline): a new guard comes out of a stairwell after <see cref="AlertRules.RespawnSeconds"/> (the owner, 2026-10-24: 45 s while it is quiet, 20 s while the backup is on, on floors 1 and 2). Only while the floor is a live heist; the boss is never replaced.</summary>'
Rep '        /// <summary>Tests: how many replacement guards came out of a stairwell on this floor, and how many are waiting for their 15 seconds.</summary>' '        /// <summary>Tests: how many replacement guards came out of a stairwell on this floor, and how many are still waiting.</summary>'
Rep '        public int ReplacementsWaiting => replacementsDue.Count;' @'
        public int ReplacementsWaiting => replacementsDue.Count;
        /// <summary>Tests: the guards waiting for their replacement have been waiting <paramref name="seconds"/> longer.</summary>
        public void AgeReplacements(float seconds) { for (int i = 0; i < replacementsDue.Count; i++) replacementsDue[i] -= seconds; }
'@
Rep '        /// <summary>The replacements that are due come out of a stairwell, as long as fewer guards are alive than the cap (<see cref="GuardRules.MaxGuardsAlive"/>); a replacement that finds the floor full is not made.</summary>' '        /// <summary>The replacements that are due come out of a stairwell, as long as fewer guards are alive than the floor''s cap (<see cref="GuardCap"/>); a replacement that finds the floor full is not made.</summary>'
Rep @'
                if (Time.time < replacementsDue[i]) continue;
                replacementsDue.RemoveAt(i);
                int alive = 0;
                foreach (var g in GuardAI.All) if (g && !g.Dead) alive++;
                if (!GuardRules.ReplacementWanted(alive)) continue;
'@ @'
                if (!AlertRules.RespawnDue(Time.time - replacementsDue[i], Floor, BackupActive)) continue;        // (45 s while it is quiet, 20 s while the backup is on: AlertRules.RespawnSeconds)
                replacementsDue.RemoveAt(i);
                int alive = 0;
                foreach (var g in GuardAI.All) if (g && !g.Dead) alive++;
                if (!GuardRules.ReplacementWanted(alive, GuardCap)) continue;
'@
Rep '            int max = GuardRules.MaxGuardsAlive;' '            int max = GuardCap;                                  // (the floor''s cap, the owner, 2026-10-24: 6, 8 with the Big Alarm; 13 from floor 3)'
Save-Src

# ---------------------------------------------------------------- GuardRules: the 15 seconds are gone (AlertRules has the times)
Open-Src ($A + 'Scripts\GuardRules.cs')
Rep @'
        /// <summary>A guard who is eliminated is replaced: this many seconds later a new guard comes out of a stairwell (the owner, 2026-10-23: "If a guard is eliminated after 15 seconds a new guard should come from a stairwell").</summary>
        public const float ReplacementSeconds = 15f;

        /// <summary>The time the replacement of a guard who was eliminated at <paramref name="now"/> is due.</summary>
        public static float ReplacementDue(float now) => now + ReplacementSeconds;

'@ @'
        // (a guard who is eliminated is replaced after 15 s until 2026-10-23, since 2026-10-24 after the times of AlertRules.RespawnSeconds)

'@
Rep '        /// <summary>The most guards alive at once while the alarm is on (the owner, 2026-10-23: "the cap while the alarm is on should stay at 24 (this number may change later on)"): the patrols and the reinforcements together. The one place to change it.</summary>' '        /// <summary>The ceiling above every floor''s cap (the owner, 2026-10-23: 24): since 2026-10-24 the floors have their own caps, <see cref="AlertRules.GuardCap"/> (6, or 8 with the Big Alarm, on floors 1 and 2; 13 from floor 3).</summary>'
Save-Src

# ---------------------------------------------------------------- GuardAI
Open-Src ($A + 'Scripts\GuardAI.cs')
Rep '                HeistManager.Instance?.CallBackup(this);' '                HeistManager.Instance?.CallBackup(this, target && target.Custody == CustodyState.Free ? target : null);          // (he marks the player he spotted, or the one who shot him, and nobody else; the call for a body marks nobody)'
Rep @'
        void UpdateChase()
        {
'@ @'
        void UpdateChase()
        {
            ScanForPlayers(true);          // (the owner, 2026-10-24: a second player he notices while he chases is marked as well; the others are not his business)
'@
Rep @'
        WobblyPlayer FindVisibleFreePlayer()
        {
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.Custody == CustodyState.Free && CanSee(p)) return p;
            return null;
        }
'@ @'
        WobblyPlayer FindVisibleFreePlayer()
        {
            var hm = HeistManager.Instance;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.Custody == CustodyState.Free && (hm == null || hm.IsMarked(p)) && CanSee(p)) return p;          // (a MARKED player: one who was spotted; the owner, 2026-10-24)
            return null;
        }
'@
Rep '        void ScanForPlayers()' '        void ScanForPlayers(bool chasing = false)'
Rep @'
            float dt = Time.deltaTime;
            float factor = IsBackup ? GuardRules.BackupSpotFactor : 1f;
            float far = sightRange * Mathf.Max(1f, longSightFactor);
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                bool inSight = CanSee(p);
'@ @'
            float dt = Time.deltaTime;
            float factor = IsBackup ? GuardRules.BackupSpotFactor : 1f;
            float far = sightRange * Mathf.Max(1f, longSightFactor);
            var hm = HeistManager.Instance;
            if (chasing && hm == null) return;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                if (chasing && (hm.IsMarked(p) || p.Custody != CustodyState.Free)) continue;          // (chasing: only a player who is not marked yet can be noticed now)
                bool inSight = CanSee(p);
'@
Rep '                if (meter.progress >= 1f) { noticed.Remove(p); StartCall(p); return; }' @'
                if (meter.progress >= 1f)
                {
                    noticed.Remove(p);
                    if (chasing) { hm.MarkSpotted(p); continue; }          // (he is after somebody already: the second player is marked, nothing else changes)
                    StartCall(p);
                    return;
                }
'@
Save-Src

# ---------------------------------------------------------------- RunSync: the Big Alarm flag
Open-Src ($A + 'Scripts\Net\RunSync.State.cs')
Rep '        readonly NetworkVariable<bool> vBackupCalled = new(), vAboard = new(), vSealing = new();' '        readonly NetworkVariable<bool> vBackupCalled = new(), vAboard = new(), vSealing = new(), vBigAlarm = new();      // (the Big Alarm: the owner, 2026-10-24)'
Rep '            vBackupIn.Value = hm.BackupIn;' @'
            vBackupIn.Value = hm.BackupIn;
            vBigAlarm.Value = hm.BigAlarmActive;
'@
Rep 'vTimeLeft.Value, vBackupCalled.Value, vBackupIn.Value);' 'vTimeLeft.Value, vBackupCalled.Value, vBackupIn.Value, vBigAlarm.Value);'
Save-Src

# ---------------------------------------------------------------- the alarm panel cannot shut the Big Alarm off
Open-Src ($A + 'Scripts\Outpost\OutpostController.cs')
Rep '        public string PanelPrompt => AlertOn ? "[E] Shut off the alarm and call off the backup" : "Alarm panel - all quiet";' @'
        public string PanelPrompt => BigAlarmOn ? "Alarm panel - the Big Alarm cannot be shut off" : AlertOn ? "[E] Shut off the alarm and call off the backup" : "Alarm panel - all quiet";

        /// <summary>The Big Alarm is on (the owner, 2026-10-24: "Once the big alarm is activated there is no turning it off"): the panel can do nothing about it.</summary>
        static bool BigAlarmOn => HeistManager.Instance != null && HeistManager.Instance.BigAlarmActive;
'@
Rep '            if (!AlertOn) { Backpack.NoticeToLocal("Nothing to switch off - the alarm is quiet"); return; }' @'
            if (BigAlarmOn) { Backpack.NoticeToLocal("The Big Alarm cannot be shut off"); return; }
            if (!AlertOn) { Backpack.NoticeToLocal("Nothing to switch off - the alarm is quiet"); return; }
'@
Rep @'
            if (!Net.NetRole.Simulates) return false;
            bool hadAlarm = AlarmOn;
'@ @'
            if (!Net.NetRole.Simulates || BigAlarmOn) return false;
            bool hadAlarm = AlarmOn;
'@
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            MenuLayoutTest();' @'
            MenuLayoutTest();
            AlertRulesTest();
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevAsks1023CTest.cs')
Rep 'Check("replacement (T13): a guard eliminated at time t is replaced at t + 15 s", GuardRules.ReplacementSeconds == 15f && Math.Abs(GuardRules.ReplacementDue(100f) - 115f) < 1e-5f);' 'Check("replacement (T13, changed 2026-10-24): a guard eliminated is replaced after 45 s while it is quiet and 20 s while the backup is on (floors 1 and 2)", AlertRules.RespawnSeconds(2, false) == 45f && AlertRules.RespawnSeconds(2, true) == 20f && AlertRules.RespawnDue(45f, 2, false) && !AlertRules.RespawnDue(44f, 2, false));'
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevAsks1023C.cs')
Rep '            // ---- T13: 15 seconds after a guard is eliminated a new guard comes out of a stairwell' '            // ---- T13 (changed 2026-10-24: 45 seconds while it is quiet, 20 while the backup is on; the numbers and the cap are the scenario GuardAlert''s): after a guard is eliminated a new guard comes out of a stairwell'
Rep @'
                float t0 = Time.time;
                while (Time.time - t0 < 14f) yield return null;
                A23C("T13: after 14 seconds no new guard has come", hm.ReplacementsMade == 0 && AliveGuards().Count == alive0 - 1, $"(made {hm.ReplacementsMade}, alive {AliveGuards().Count} of {alive0})");
                float madeAt = -1f;
                while (Time.time - t0 < 19f && hm.ReplacementsMade == 0) yield return null;
                if (hm.ReplacementsMade > 0) madeAt = Time.time - t0;
                var fresh = GuardAI.All.FirstOrDefault(g => g && g.name.StartsWith("Replacement_"));
                A23C("T13: a new guard came after about 15 seconds", madeAt >= 14f && madeAt <= 16.5f && fresh != null, $"(made after {madeAt:0.0} s)");
'@ @'
                hm.AgeReplacements(40f);                                                       // (40 s in: the 45 s of a quiet floor are not over)
                yield return new WaitForSeconds(1.5f);
                A23C("T13: 41 seconds after, no new guard has come", hm.ReplacementsMade == 0 && AliveGuards().Count == alive0 - 1, $"(made {hm.ReplacementsMade}, alive {AliveGuards().Count} of {alive0})");
                hm.AgeReplacements(6f);
                float t0 = Time.time;
                while (Time.time - t0 < 3f && hm.ReplacementsMade == 0) yield return null;
                var fresh = GuardAI.All.FirstOrDefault(g => g && g.name.StartsWith("Replacement_"));
                A23C("T13: a new guard came once the 45 seconds were over", hm.ReplacementsMade == 1 && fresh != null, $"(made {hm.ReplacementsMade})");
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PropScan()' @'
        public static void GuardAlert()
        {
            SessionState.SetString("netdev.mode", "guardalert");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PropScan()
'@
Rep 'mode != "audiodevicesui" &&' 'mode != "audiodevicesui" && mode != "guardalert" &&'
Rep '                case "audiodevicesui": yield return AudioDevicesUiScenario(); break;' @'
                case "audiodevicesui": yield return AudioDevicesUiScenario(); break;
                case "guardalert": yield return GuardAlertScenario(); break;
'@
Save-Src
"guard alert patched"
