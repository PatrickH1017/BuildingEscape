using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The guards' alert on a floor, without Unity types so it is tested outside Unity (the owner, 2026-10-24, DEVELOPER_NOTES 2.64): who is MARKED (a guard that spots a player marks that player and nobody else), when the BIG ALARM goes on
    /// (half of the party is marked, both players of a duo), how many guards there may be (6 on floors 1 and 2, 8 with the Big Alarm; 13 from floor 3), how long a guard who was eliminated takes to be replaced (45 s while it is quiet, 20 s while the
    /// alert is on) and when the alert goes off again (every marked player is pinned, unless the Big Alarm is on: that never goes off). The numbers the owner gave are for floors 1 and 2 and for floors 3 to 13; wherever he gave none the same numbers
    /// are used as a PLACEHOLDER (marked below), so there is one place to change them.
    /// </summary>
    public static class AlertRules
    {
        /// <summary>Floors 1 to this one have the "small" numbers.</summary>
        public const int SmallFloors = 2;
        /// <summary>The most guards alive at once on floors 1 and 2, and with the Big Alarm on (the owner, 2026-10-24).</summary>
        public const int SmallCap = 6, SmallCapBigAlarm = 8;
        /// <summary>The most guards alive at once from floor 3 on (the owner: "floors 3-13 should have a cap of 13"; he gave one number, so it is the same with the Big Alarm).</summary>
        public const int LargeCap = 13, LargeCapBigAlarm = 13;
        /// <summary>Seconds until a guard who was eliminated is replaced on floors 1 and 2: while it is quiet, and while the alert (backup) is on (the owner, 2026-10-24).</summary>
        public const float SmallQuietRespawn = 45f, SmallAlertedRespawn = 20f;
        /// <summary>PLACEHOLDER (the owner has not given the numbers for floors 3 and up): the same as floors 1 and 2.</summary>
        public const float LargeQuietRespawn = 45f, LargeAlertedRespawn = 20f;

        /// <summary>The most guards alive at once on <paramref name="floor"/>, with the Big Alarm on or off.</summary>
        public static int GuardCap(int floor, bool bigAlarm) =>
            floor <= SmallFloors ? (bigAlarm ? SmallCapBigAlarm : SmallCap) : (bigAlarm ? LargeCapBigAlarm : LargeCap);

        /// <summary>How long a guard who was eliminated takes to be replaced on <paramref name="floor"/>, with the alert (backup) on or off.</summary>
        public static float RespawnSeconds(int floor, bool alerted) =>
            floor <= SmallFloors ? (alerted ? SmallAlertedRespawn : SmallQuietRespawn) : (alerted ? LargeAlertedRespawn : LargeQuietRespawn);

        /// <summary>Is a replacement that has been waiting <paramref name="waited"/> seconds due? The wait is judged by the alert as it is NOW: the alert coming on shortens it, going off lengthens it again.</summary>
        public static bool RespawnDue(float waited, int floor, bool alerted) => waited >= RespawnSeconds(floor, alerted);

        /// <summary>How many marked players turn the Big Alarm on (the owner, 2026-10-24: "50% or more of the party (except for duos, in duos only when both players get marked)"): half of the party rounded up, and both players of a duo.</summary>
        public static int BigAlarmMarks(int party) => party <= 0 ? 0 : party == 2 ? 2 : (party + 1) / 2;

        /// <summary>Is the Big Alarm on: are <paramref name="marked"/> players of a party of <paramref name="party"/> marked?</summary>
        public static bool BigAlarmReached(int marked, int party) => party > 0 && marked >= BigAlarmMarks(party);

        /// <summary>
        /// Does the alert go off (the owner, 2026-10-24: "If the big alarm is not active and the marked player(s) get pinned, the security alert should turn off")? When some are marked, none of them is free any more (they are pinned or busted) and the
        /// Big Alarm is not on.
        /// </summary>
        public static bool AlertEnds(bool bigAlarm, int marked, int markedFree) => !bigAlarm && marked > 0 && markedFree == 0;
    }

    /// <summary>
    /// The marks of one floor and its Big Alarm (host only). Players are told apart by a number. A mark stays until the alert goes off (<see cref="ClearMarks"/>) or the floor ends (<see cref="Reset"/>); the Big Alarm stays until the floor ends.
    /// </summary>
    public sealed class AlertState
    {
        readonly HashSet<int> marked = new HashSet<int>();

        public bool BigAlarm { get; private set; }
        public int MarkedCount => marked.Count;
        public bool IsMarked(int player) => marked.Contains(player);

        /// <summary>A new floor: nobody is marked and the Big Alarm is off.</summary>
        public void Reset() { marked.Clear(); BigAlarm = false; }

        /// <summary>The alert went off: nobody is marked any more (the Big Alarm, if it is on, stays).</summary>
        public void ClearMarks() => marked.Clear();

        /// <summary>This player was spotted. True when this mark turned the Big Alarm on (<paramref name="party"/> players in the run). A player who is marked already changes nothing.</summary>
        public bool Mark(int player, int party)
        {
            if (!marked.Add(player)) return false;
            if (BigAlarm || !AlertRules.BigAlarmReached(marked.Count, party)) return false;
            BigAlarm = true;
            return true;
        }

        /// <summary>Does the alert go off now, with <paramref name="markedFree"/> of the marked players still free?</summary>
        public bool ShouldEnd(int markedFree) => AlertRules.AlertEnds(BigAlarm, marked.Count, markedFree);
    }
}
