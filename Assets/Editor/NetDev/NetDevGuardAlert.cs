using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of the guards' alert (the owner, 2026-10-24, DEVELOPER_NOTES 2.64; offline, on the real floors): a guard that spots a player marks him (and, with a party of three, nobody else); the Big Alarm (a solo player: the first sighting) raises the
    /// guard cap from 6 to 8 on floor 1 and cannot be switched off; the cap holds for backup and for replacements; an eliminated guard is replaced after 45 s while it is quiet and after 20 s while the alert is on; the alert goes off when the marked player is pinned
    /// (not with the Big Alarm on). The pure half is <c>NetDevTest.AlertRulesTest</c>. Run: <c>qa_regress_bd.ps1 -Methods GuardAlert</c>; the log ends with <c>[GuardAlert] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int gaFails;

        void GaCheck(string name, bool ok, string detail = "")
        {
            if (!ok) gaFails++;
            Log($"[GuardAlert] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static int AllAlive() => GuardAI.All.Count(g => g && !g.Dead);

        IEnumerator GuardAlertScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            GuardDrops.TestRoll = 0;
            const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
            var fState = typeof(GuardAI).GetField("state", any);
            GaCheck("(test) the guard's state field is where the test expects it", fState != null);
            if (fState == null) { Quit(1); yield break; }

            // ---- a sighting marks the spotted player (a party of three: no Big Alarm yet), the cap is 6 on floor 1
            HeistManager.TestPartySize = 3;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            var player = WobblyPlayerRegistry.All[0];
            GaCheck("floor 1: nobody is marked, no Big Alarm, the cap is 6 (8 with the Big Alarm), and there are fewer guards than the cap", hm.MarkedCount == 0 && !hm.BigAlarmActive && !hm.BackupActive && hm.GuardCap == 6 && AlertRules.GuardCap(1, true) == 8 && AllAlive() <= 6, $"({AllAlive()} guards, cap {hm.GuardCap})");
            // a guard is put on duty facing the player, 6 m away on a clear line (up to five tries with different guards: a guard may stand where the real sight does not reach, which is not what this is about)
            var candidates = GuardAI.All.Where(g => g && !g.Dead && !g.IsBoss && !g.IsBackup && g.name != "Guard_Outpost").ToList();
            GuardAI watcher = null;
            float spottedAfter = -1f;
            foreach (var cand in candidates.Take(5))
            {
                if (!OpenSpot(cand.transform.position + Vector3.up * 0.9f, 6f, out var spot, true)) continue;
                fState.SetValue(cand, GuardState.Patrol);
                var to = spot - cand.transform.position; to.y = 0f;
                GuardSightFreeze(cand);                                                      // (he stands where he is: no route, no roaming, the agent still)
                cand.transform.rotation = Quaternion.LookRotation(to.normalized);              // (and looks at the spot)
                player.TeleportTo(spot + Vector3.up * 1.1f, Quaternion.identity);
                float t0 = Time.time;
                while (Time.time - t0 < 9f && !hm.IsMarked(player)) yield return null;
                if (hm.IsMarked(player)) { watcher = cand; spottedAfter = Time.time - t0; break; }
                Log($"[GuardAlert] INFO {cand.name} did not spot him in 9 s: state {cand.State}, meter {cand.NoticeProgress(player):0.00}, sees him {GsCanSee(cand, player)}, {Vector3.Distance(cand.transform.position, player.transform.position):0.0} m away");
            }
            GaCheck("a guard on duty 6 m from the player spotted him: he is marked (one of three: no Big Alarm yet), the backup is on", watcher != null && hm.IsMarked(player) && hm.MarkedCount == 1 && !hm.BigAlarmActive && hm.BackupActive, $"(marked {hm.IsMarked(player)}, count {hm.MarkedCount}, big {hm.BigAlarmActive}, backup {hm.BackupActive}, after {spottedAfter:0.0} s)");
            if (watcher != null)
            {
                GaCheck("the officers the call sent stop at the floor's cap of 6", AllAlive() <= 6, $"({AllAlive()} guards)");
                GaCheck("the Big Alarm is not on, so the alarm panel can still stand the backup down: the panel's line offers it", OutpostController.Active == null || !OutpostController.Active.PanelPrompt.Contains("cannot"), OutpostController.Active != null ? $"('{OutpostController.Active.PanelPrompt}')" : "(no outpost)");
            }

            // ---- the same sighting for a solo player is the Big Alarm: the cap is 8, the panel cannot switch it off
            HeistManager.TestPartySize = 0;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            player = WobblyPlayerRegistry.All[0];
            bool marked = hm.MarkSpotted(player);
            GaCheck("solo: the first sighting marks the only player and turns the Big Alarm on at once (100 % of the party)", marked && hm.IsMarked(player) && hm.BigAlarmActive && hm.GuardCap == 8, $"(marked {hm.IsMarked(player)}, big {hm.BigAlarmActive}, cap {hm.GuardCap})");
            hm.CallBackup(null);
            GaCheck("the backup call with the Big Alarm on is not stopped by the stand-down: StandDown() says no and the backup stays on", !hm.StandDown() && hm.BackupActive && hm.BigAlarmActive);
            var op = OutpostController.Active;
            if (op != null)
            {
                GaCheck("the alarm panel says the Big Alarm cannot be shut off, and pressing it does nothing", op.PanelPrompt.Contains("cannot") && !op.HostStandDown() && hm.BackupActive && hm.BigAlarmActive, $"('{op.PanelPrompt}')");
            }
            int peak = AllAlive();
            for (int i = 0; i < 5; i++) { hm.CallBackup(null); yield return new WaitForSeconds(0.3f); peak = Mathf.Max(peak, AllAlive()); }
            GaCheck("with the Big Alarm on the floor fills up to 8 guards and no further", peak == 8 && AllAlive() == 8, $"(peak {peak}, now {AllAlive()})");

            // ---- without it the same calls stop at 6
            HeistManager.TestPartySize = 3;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < 5; i++) { hm.CallBackup(null); yield return new WaitForSeconds(0.3f); }
            GaCheck("without the Big Alarm the calls stop at 6 guards on floor 1", AllAlive() == 6 && !hm.BigAlarmActive, $"({AllAlive()} guards)");

            // ---- replacements: 45 s while it is quiet, 20 s while the alert is on, and never above the cap
            HeistManager.TestPartySize = 3;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            var guards = AliveGuards();
            GaCheck("(test) floor 1 has guards to eliminate", guards.Count >= 3, $"({guards.Count})");
            if (guards.Count >= 3)
            {
                int alive0 = AllAlive();
                Eliminate(guards[0]);
                yield return null;
                GaCheck("an eliminated guard is waiting for his replacement", hm.ReplacementsWaiting == 1 && hm.ReplacementsMade == 0);
                hm.AgeReplacements(44f);
                yield return new WaitForSeconds(0.6f);
                GaCheck("floor 1, quiet: 44 seconds are not enough", hm.ReplacementsMade == 0 && AllAlive() == alive0 - 1, $"(made {hm.ReplacementsMade}, alive {AllAlive()} of {alive0})");
                hm.AgeReplacements(2f);
                yield return new WaitForSeconds(0.6f);
                GaCheck("floor 1, quiet: after 45 seconds a new guard comes", hm.ReplacementsMade == 1 && AllAlive() == alive0, $"(made {hm.ReplacementsMade}, alive {AllAlive()} of {alive0})");
            }

            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            guards = AliveGuards();
            if (guards.Count >= 4)
            {
                Eliminate(guards[0]); Eliminate(guards[1]); Eliminate(guards[2]);
                int aliveAfter = AllAlive();
                yield return null;
                hm.AgeReplacements(10f);
                yield return new WaitForSeconds(0.6f);
                GaCheck("floor 1, quiet: 10 seconds in, nobody has come", hm.ReplacementsMade == 0 && hm.ReplacementsWaiting == 3);
                hm.CallBackup(null);                                                          // (the alert comes on: the officers it sends are 2 more guards)
                int withOfficers = AllAlive();
                GaCheck("(test) the backup call sent officers, and there is room under the cap of 6 for a replacement", withOfficers > aliveAfter && withOfficers <= 5, $"({aliveAfter} -> {withOfficers})");
                hm.AgeReplacements(15f);                                                      // (25 s in: more than the 20 s of an alert, less than the 45 s of quiet)
                yield return new WaitForSeconds(0.8f);
                int expected = 6 - withOfficers;
                GaCheck("floor 1, alert on: 25 seconds are enough, the replacements come until the cap of 6 is reached and the rest are dropped", hm.ReplacementsMade >= 1 && hm.ReplacementsMade == expected && AllAlive() == 6 && hm.ReplacementsWaiting == 0, $"(alive {AllAlive()}, waiting {hm.ReplacementsWaiting}, made {hm.ReplacementsMade}, expected {expected}, had {withOfficers})");
            }

            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            guards = AliveGuards();
            if (guards.Count >= 4)
            {
                Eliminate(guards[0]); Eliminate(guards[1]); Eliminate(guards[2]);
                yield return null;
                hm.CallBackup(null);
                hm.AgeReplacements(5f);
                bool wasStoodDown = hm.StandDown();                                           // (the alert goes off before the 20 s are over: back to 45)
                yield return new WaitForSeconds(0.3f);
                hm.AgeReplacements(25f);                                                      // (30 s in: enough for an alert, not for quiet)
                yield return new WaitForSeconds(0.8f);
                GaCheck("floor 1, the alert went off again: 30 seconds are not enough any more (it is 45 s once more)", wasStoodDown && !hm.BackupActive && hm.ReplacementsMade == 0 && hm.ReplacementsWaiting == 3, $"(stood down {wasStoodDown}, made {hm.ReplacementsMade}, waiting {hm.ReplacementsWaiting})");
                hm.AgeReplacements(16f);
                yield return new WaitForSeconds(0.8f);
                GaCheck("... and they come at 45 seconds, up to the cap", hm.ReplacementsMade >= 1 && AllAlive() <= 6, $"(made {hm.ReplacementsMade}, alive {AllAlive()})");
            }

            // ---- the alert goes off when the marked player is pinned (a party of three: no Big Alarm); not with the Big Alarm on
            HeistManager.TestPartySize = 3;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            player = WobblyPlayerRegistry.All[0];
            var holder = GuardAI.All.FirstOrDefault(g => g && !g.Dead && !g.IsBoss);
            hm.MarkSpotted(player);
            hm.CallBackup(null);
            GaCheck("(test) one of three marked and the backup on", hm.IsMarked(player) && !hm.BigAlarmActive && hm.BackupActive);
            player.PinnedBy = holder;
            player.ApplyCustody(CustodyState.Pinned, 12f, 0);                                 // (pinned: the marked player is in custody)
            yield return new WaitForSeconds(0.4f);
            GaCheck("the marked player is pinned: the alert goes off, nobody is marked, and the respawn time is back to 45 s", !hm.BackupActive && !hm.BackupCalled && hm.MarkedCount == 0 && !hm.BigAlarmActive && !AlertRules.RespawnDue(30f, 1, hm.BackupActive), $"(backup {hm.BackupActive}, marked {hm.MarkedCount})");
            player.PinnedBy = null;
            player.ApplyCustody(CustodyState.Free, 0f, 0);

            HeistManager.TestPartySize = 0;
            yield return LoadLevel(hm, 1);
            yield return new WaitForSeconds(0.5f);
            player = WobblyPlayerRegistry.All[0];
            holder = GuardAI.All.FirstOrDefault(g => g && !g.Dead && !g.IsBoss);
            hm.MarkSpotted(player);
            hm.CallBackup(null);
            player.PinnedBy = holder;
            player.ApplyCustody(CustodyState.Pinned, 12f, 0);
            yield return new WaitForSeconds(0.4f);
            GaCheck("with the Big Alarm on the marked player's pin does not end the alert: it goes on to the end of the floor", hm.BackupActive && hm.BigAlarmActive && hm.MarkedCount == 1, $"(backup {hm.BackupActive}, big {hm.BigAlarmActive})");
            player.PinnedBy = null;
            player.ApplyCustody(CustodyState.Free, 0f, 0);

            // ---- a new floor starts clean
            yield return LoadLevel(hm, 2);
            yield return new WaitForSeconds(0.5f);
            GaCheck("floor 2: a new floor has nobody marked and no Big Alarm, and its cap is 6", hm.MarkedCount == 0 && !hm.BigAlarmActive && hm.GuardCap == 6);

            HeistManager.TestPartySize = 0;
            Log($"[GuardAlert] DONE fails={gaFails}");
            Quit(gaFails == 0 ? 0 : 1);
        }
    }
}
