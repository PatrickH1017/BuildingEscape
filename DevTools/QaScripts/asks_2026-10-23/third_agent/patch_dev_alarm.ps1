# The dev menu's buttons that get rid of the wanted level / the backup did nothing (the owner, 2026-10-27: "The dev menu buttons for getting rid of wanted level/backup don't work"): they called HeistManager.StandDown, which is the guards' ALARM PANEL rule - it refuses while the Big Alarm is on (since 2026-10-24 a solo
# player's first sighting turns it on) and when nothing but marks are going. New HeistManager.DevClearAlert: the backup is called off whatever started it, the Big Alarm and the marks are cleared, the officers the backup called leave, every other officer who was after somebody goes back to his round. The alarm panel keeps its rule.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $Dry) { Copy-Item (Join-Path $here 'combat2\NetDevBulletDamage.cs') ($A + 'Editor\NetDev\NetDevBulletDamage.cs') -Force; Write-Output 'copied NetDevBulletDamage.cs' }

Open-Src ($A + 'Scripts\GuardAI.cs')
Rep '        void CutRadioAndRelease()' @'
        /// <summary>The dev menu's "alarm off" (the owner, 2026-10-27): he gives up whatever he was doing about the players - a chase, an investigation, a radio call - and goes back to his round (host / offline). A body on the floor is left alone.</summary>
        public void DevCalm()
        {
            if (Dead || IsPuppet) return;
            ReleaseHold(true);
            calling = false; callTimer = 0f; callAnyway = false; callOnGetUp = false;
            target = null; hitBy = null; noticed.Clear();
            if (State != GuardState.Stunned) { State = GuardState.Patrol; waitUntil = 0f; GoNextPatrol(); }
        }

        void CutRadioAndRelease()
'@
Save-Src
Open-Src ($A + 'Scripts\HeistManager.cs')
Rep '        /// <summary>The party: how many players are in the run (<see cref="TestPartySize"/> when a test sets it).</summary>' @'
        /// <summary>
        /// The dev menu's "turn the alarm off / cancel the backup" (the owner, 2026-10-27: "the dev menu buttons for getting rid of wanted level/backup don't work"; host / offline): the backup is called off whatever started it, the Big Alarm - which the alarm panel can never shut off (<see cref="StandDown"/>
        /// refuses) - and every mark are cleared, the officers the backup called leave and every other officer who was after somebody goes back to his round. True when anything was on.
        /// </summary>
        public bool DevClearAlert()
        {
            if (Phase != HeistPhase.Heist || !Net.NetRole.Simulates) return false;
            bool was = backupClock.Active || BackupCalled || alertState.BigAlarm || alertState.MarkedCount > 0;
            backupClock.Reset();
            BackupCalled = false;
            alertState.Reset(); netBigAlarm = false;
            radiosJammedUntil = Time.time + CustodyRules.StandDownJamSeconds;
            foreach (var g in new System.Collections.Generic.List<GuardAI>(GuardAI.All))
            {
                if (!g || g.Dead || g.IsPuppet) continue;
                if (g.IsBackup) { was = true; Destroy(g.gameObject); }
                else g.DevCalm();
            }
            if (was) Announce("ALL QUIET - the alarm is off and no more officers are coming");
            return was;
        }

        /// <summary>The party: how many players are in the run (<see cref="TestPartySize"/> when a test sets it).</summary>
'@
Save-Src
Open-Src ($A + 'Scripts\TestMode.cs')
Rep 'hm.StandDown() ? "Backup called off: no more officers are coming" : "No backup call going - nothing to cancel"' 'hm.DevClearAlert() ? "Backup called off: the alarm and the marks are cleared, the extra officers are gone" : "No backup call or alarm going - nothing to cancel"'
Rep 'hm && hm.StandDown() ? "Reinforcements called off" : "No reinforcements going - nothing to switch off"' 'hm && hm.DevClearAlert() ? "Alarm off: the reinforcements are gone and nobody is marked" : "No reinforcements or alarm going - nothing to switch off"'
Rep '            GUILayout.Label("Reinforcements: " + (hm && hm.BackupActive ? "ON (an officer every 30 s)" : "off"));' '            GUILayout.Label("Reinforcements: " + (hm && hm.BackupActive ? "ON (an officer every 30 s)" : "off") + "     Big Alarm: " + (hm && hm.BigAlarmActive ? "ON" : "off") + "     Marked: " + (hm ? hm.MarkedCount : 0));'
Save-Src
Open-Src ($A + 'Scripts\Dev\DevCommands.cs')
Rep 'case DevCommand.CancelBackup: hm.StandDown(); return "stand down";' 'case DevCommand.CancelBackup: hm.DevClearAlert(); return "alarm off";'
Save-Src
"dev alarm patched"
