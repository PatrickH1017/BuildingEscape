using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>Where a player stands with the police: free, held down by an officer, or arrested.</summary>
    public enum CustodyState : byte { Free = 0, Pinned = 1, Busted = 2 }

    /// <summary>
    /// The pinning / busted / backup / buy-back rules as plain numbers and pure functions (no Unity types), so they can
    /// be unit tested without a scene. The scene objects (WobblyPlayer, GuardAI, HeistManager, Vendor) only apply them.
    ///
    ///   - An officer that touches a free player PINS them: held down on the ground.
    ///   - A pinned player has <see cref="PinSeconds"/> until they are BUSTED (arrested).
    ///   - Hitting the officer that is pinning someone (melee or a thrown item) frees the pinned player.
    ///   - When nobody is free any more (everybody pinned or busted) the whole team is busted: the run is over.
    ///   - After a guard calls backup, ANOTHER officer is called every <see cref="BackupIntervalSeconds"/> until the floor ends.
    ///   - Busted players can be bought back from the vendor once the team is at the Loading Docks.
    /// </summary>
    public static class CustodyRules
    {
        /// <summary>Seconds a pinned player has before they are busted.</summary>
        public const float PinSeconds = 12f;

        /// <summary>After backup has been called, one more officer is called this often (on floor 1; see <see cref="BackupInterval"/> for the higher floors).</summary>
        public const float BackupIntervalSeconds = 30f;

        /// <summary>The same on floor 12 and above.</summary>
        public const float BackupIntervalTopFloorSeconds = 12f;

        /// <summary>
        /// Seconds between the officers a backup call keeps sending, by floor (the owner, 2026-10-18: "Backup timing should speed up with the higher floors"):
        /// 30 s on floor 1, shrinking in equal steps to 12 s on floor 12 and above.
        /// </summary>
        public static float BackupInterval(int floor)
        {
            float t = Math.Min(1f, Math.Max(0f, (floor - 1) / 11f));
            return BackupIntervalSeconds + (BackupIntervalTopFloorSeconds - BackupIntervalSeconds) * t;
        }

        /// <summary>A player who was just freed cannot be pinned again for this long (so the officer cannot re-pin at once).</summary>
        public const float PinImmunitySeconds = 3f;

        /// <summary>
        /// How long an officer must hold on to a free player - the player within his arm's reach, nothing solid between them - before the player is pinned (the owner, 2026-10-23: "Guards should not pin so quickly in melee range,
        /// it should take 2 seconds of being next to them").
        /// </summary>
        public const float GrabSeconds = 1f;          // (2 until the friends' playtest of 2026-10-23, then the owner: "The guard pin time should be 1 second next to a player (right now is too long)")

        /// <summary>The grab clock after <paramref name="dt"/> more seconds: it fills while the player is in reach and drains twice as fast once they get away (never below 0, never above <see cref="GrabSeconds"/>).</summary>
        public static float GrabAfter(float held, bool inReach, float dt)
        {
            dt = Math.Max(0f, dt);
            held = inReach ? held + dt : held - dt * 2f;
            return Math.Min(GrabSeconds, Math.Max(0f, held));
        }

        /// <summary>Has he held on for the whole <see cref="GrabSeconds"/>?</summary>
        public static bool GrabDone(float held) => held >= GrabSeconds - 0.0001f;

        /// <summary>
        /// After the alarm panel in a guard outpost has been used the officers' radios are jammed for this long: an officer who was in the
        /// middle of keying his radio when the button was pressed cannot start the backup all over again at once.
        /// </summary>
        public const float StandDownJamSeconds = 10f;

        /// <summary>Are the radios still jammed? (<paramref name="jamUntil"/> is the time the alarm panel's jam ends; 0 = never used.)</summary>
        public static bool RadioJammed(float now, float jamUntil) => now < jamUntil;

        /// <summary>What the vendor says to a customer when a friend of theirs is under arrest.</summary>
        public const string BuyBackLine = "One of your friends got got. You're lucky I have someone on the inside";

        /// <summary>What the vendor says on arrival when nobody is under arrest.</summary>
        public const string GreetingLine = "Glad you made it. Make it fast and make it a good deal.";

        /// <summary>The vendor's price to get a player out, by the level they were busted on (0 = docks, 1.. = office floors).</summary>
        public static int BuyBackPrice(int bustedOnLevel) => BaseBuyBack + PerLevelBuyBack * Math.Max(0, bustedOnLevel);

        public const int BaseBuyBack = 300;
        public const int PerLevelBuyBack = 50;

        /// <summary>The team is down when there is at least one player and none of them is free.</summary>
        public static bool TeamDown(int freePlayers, int totalPlayers) => totalPlayers > 0 && freePlayers <= 0;

        /// <summary>Counts the free players in a list of states.</summary>
        public static int CountFree(IEnumerable<CustodyState> states)
        {
            int n = 0;
            foreach (var s in states) if (s == CustodyState.Free) n++;
            return n;
        }

        /// <summary>Seconds left on a pin after <paramref name="dt"/> more seconds (never below 0).</summary>
        public static float PinLeftAfter(float left, float dt) => Math.Max(0f, left - Math.Max(0f, dt));

        /// <summary>Whole seconds to show on a countdown (rounds up, so "1" shows until the very end).</summary>
        public static int ShownSeconds(float left) => (int)Math.Ceiling(Math.Max(0f, left));

        /// <summary>Can this player be pinned right now?</summary>
        public static bool CanPin(CustodyState state, float now, float immuneUntil) => state == CustodyState.Free && now >= immuneUntil;

        /// <summary>The line the vendor opens with: the buy-back line while a friend is under arrest, else his greeting.</summary>
        public static string VendorLine(bool anyBusted) => anyBusted ? BuyBackLine : GreetingLine;

        /// <summary>Can <paramref name="cash"/> pay for getting this player out?</summary>
        public static bool CanAffordBuyBack(int cash, int bustedOnLevel) => cash >= BuyBackPrice(bustedOnLevel);
    }

    /// <summary>
    /// The repeating backup call. Off until the first backup call of a floor; from then on it fires once every
    /// <see cref="CustodyRules.BackupIntervalSeconds"/> of play. Pure, so the timing can be tested.
    /// </summary>
    public sealed class BackupClock
    {
        public float Interval { get; private set; }
        public bool Active { get; private set; }
        /// <summary>Seconds until the next repeat call (meaningful while <see cref="Active"/>).</summary>
        public float Left { get; private set; }
        /// <summary>How many repeat calls have fired since it started.</summary>
        public int Repeats { get; private set; }

        public BackupClock(float interval = CustodyRules.BackupIntervalSeconds) { Interval = Math.Max(0.01f, interval); }

        /// <summary>The first backup call of the floor: the countdown to the next officer starts (with this interval when one is given: it depends on the floor). Later calls change nothing.</summary>
        public void Start(float interval = 0f)
        {
            if (Active) return;
            if (interval > 0f) Interval = Math.Max(0.01f, interval);
            Active = true; Left = Interval; Repeats = 0;
        }

        /// <summary>The floor ended, or the alarm panel called the backup off: no more officers are called until the next <see cref="Start"/>.</summary>
        public void Reset() { Active = false; Left = 0f; Repeats = 0; }

        /// <summary>Advance by <paramref name="dt"/>; returns true when another officer is due now (at most once per call).</summary>
        public bool Tick(float dt)
        {
            if (!Active) return false;
            Left -= Math.Max(0f, dt);
            if (Left > 0f) return false;
            Left += Interval;
            if (Left <= 0f) Left = Interval; // a huge step still only fires once
            Repeats++;
            return true;
        }

        /// <summary>Whole seconds to show, or -1 while the clock is off.</summary>
        public int ShownSeconds => Active ? CustodyRules.ShownSeconds(Left) : -1;
    }
}
