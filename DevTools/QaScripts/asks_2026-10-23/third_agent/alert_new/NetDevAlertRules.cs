using System;

namespace BuildingEscape.EditorTools
{
    /// <summary>The pure rules of the guards' alert (the owner, 2026-10-24: marked players, the Big Alarm, the guard caps, the replacement times, the alert going off). Called from <c>NetDevTest.PolicyTest</c>; the Play half is the scenario <c>GuardAlert</c>.</summary>
    public static partial class NetDevTest
    {
        public static void AlertRulesTest()
        {
            Check("alert: guards are capped at 6 on floors 1 and 2 and at 8 there once the Big Alarm is on; from floor 3 to 13 the cap is 13 (the owner gave one number for them)",
                AlertRules.GuardCap(1, false) == 6 && AlertRules.GuardCap(2, false) == 6 && AlertRules.GuardCap(1, true) == 8 && AlertRules.GuardCap(2, true) == 8
                && AlertRules.GuardCap(3, false) == 13 && AlertRules.GuardCap(7, false) == 13 && AlertRules.GuardCap(13, false) == 13 && AlertRules.GuardCap(3, true) == 13 && AlertRules.GuardCap(13, true) == 13);
            Check("alert: an eliminated guard is replaced after 45 s while it is quiet and after 20 s while the alert is on (floors 1 and 2), and floors 3 and up use the same until the owner says otherwise",
                AlertRules.RespawnSeconds(1, false) == 45f && AlertRules.RespawnSeconds(2, false) == 45f && AlertRules.RespawnSeconds(1, true) == 20f && AlertRules.RespawnSeconds(2, true) == 20f
                && AlertRules.RespawnSeconds(5, false) == 45f && AlertRules.RespawnSeconds(5, true) == 20f);
            Check("alert: a replacement is due when it has waited its time as the alert stands NOW (44.9 s is not enough while it is quiet, 20 s is enough while the alert is on, and the alert going off asks for 45 s again)",
                !AlertRules.RespawnDue(44.9f, 1, false) && AlertRules.RespawnDue(45f, 1, false) && !AlertRules.RespawnDue(19.9f, 1, true) && AlertRules.RespawnDue(20f, 1, true) && AlertRules.RespawnDue(30f, 2, true) && !AlertRules.RespawnDue(30f, 2, false));
            Check("alert: the Big Alarm needs 1 marked player of a solo player, both of a duo, 2 of 3, 2 of 4 and 3 of 5 (half of the party rounded up; a duo needs both)",
                AlertRules.BigAlarmMarks(1) == 1 && AlertRules.BigAlarmMarks(2) == 2 && AlertRules.BigAlarmMarks(3) == 2 && AlertRules.BigAlarmMarks(4) == 2 && AlertRules.BigAlarmMarks(5) == 3 && AlertRules.BigAlarmMarks(0) == 0
                && AlertRules.BigAlarmReached(1, 1) && !AlertRules.BigAlarmReached(1, 2) && AlertRules.BigAlarmReached(2, 2) && !AlertRules.BigAlarmReached(1, 3) && AlertRules.BigAlarmReached(2, 3) && AlertRules.BigAlarmReached(2, 4) && !AlertRules.BigAlarmReached(2, 5) && AlertRules.BigAlarmReached(3, 5) && !AlertRules.BigAlarmReached(0, 0));
            Check("alert: the alert goes off when somebody is marked and none of the marked players is free, unless the Big Alarm is on; nobody marked, or one marked player still free, keeps it going",
                AlertRules.AlertEnds(false, 1, 0) && AlertRules.AlertEnds(false, 2, 0) && !AlertRules.AlertEnds(false, 2, 1) && !AlertRules.AlertEnds(false, 0, 0) && !AlertRules.AlertEnds(true, 1, 0) && !AlertRules.AlertEnds(true, 3, 0));

            // ---- the marks of a floor: a trio
            var s = new AlertState();
            bool a = s.Mark(11, 3), a2 = s.Mark(11, 3);
            Check("alert: a sighting marks that player only (one of three marked: no Big Alarm), and marking the same player again changes nothing",
                !a && !a2 && s.MarkedCount == 1 && s.IsMarked(11) && !s.IsMarked(12) && !s.IsMarked(13) && !s.BigAlarm);
            bool b = s.Mark(12, 3);
            Check("alert: another player spotted is marked too, and with two of three marked the Big Alarm goes on", b && s.BigAlarm && s.MarkedCount == 2 && s.IsMarked(11) && s.IsMarked(12) && !s.IsMarked(13));
            bool third = s.Mark(13, 3);
            Check("alert: a third mark does not turn the Big Alarm on again; with it on the alert never goes off however many are pinned, clearing the marks leaves it on, and only a new floor ends it",
                !third && s.MarkedCount == 3 && !s.ShouldEnd(0) && BigStays(s));

            // ---- a duo: both, a solo player: the first sighting
            var duo = new AlertState();
            bool d1 = duo.Mark(1, 2), d2 = duo.Mark(2, 2);
            Check("alert: in a duo one marked player is no Big Alarm and the second one is", !d1 && d2 && duo.BigAlarm);
            var solo = new AlertState();
            Check("alert: a solo player who is spotted is the Big Alarm at once (he is 100 % of the party)", solo.Mark(7, 1) && solo.BigAlarm);

            // ---- the alert goes off when the marked players are pinned
            var t = new AlertState();
            t.Mark(1, 3);                                                      // (one of three: no Big Alarm)
            bool whileFree = t.ShouldEnd(1), whenPinned = t.ShouldEnd(0);
            Check("alert: with one of three marked the alert stays while he is free and goes off when he is pinned", !t.BigAlarm && !whileFree && whenPinned);
            var q = new AlertState();
            q.Mark(1, 5);                                                      // (one of five: no Big Alarm)
            bool end = q.ShouldEnd(0);
            q.ClearMarks();                                                    // (the alert went off)
            bool cleared = q.MarkedCount == 0 && !q.BigAlarm && !q.ShouldEnd(0);
            q.Mark(2, 5); q.Mark(3, 5);                                        // (the marks start again from nothing: 2 of 5 is no Big Alarm, the third is)
            bool notYet = !q.BigAlarm;
            bool fifth = q.Mark(4, 5);
            Check("alert: after the alert went off the marks start again from nothing (2 of 5 marked is no Big Alarm, the third mark is)", end && cleared && notYet && fifth && q.BigAlarm);
            s.Reset();
            Check("alert: a new floor starts clean: nobody marked, the Big Alarm off", s.MarkedCount == 0 && !s.BigAlarm && !s.IsMarked(11));
        }

        static bool BigStays(AlertState s)
        {
            s.ClearMarks();
            return s.BigAlarm && s.MarkedCount == 0 && !s.ShouldEnd(0);
        }
    }
}
