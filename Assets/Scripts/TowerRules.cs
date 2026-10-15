namespace BuildingEscape
{
    /// <summary>
    /// The shape of the tower in one place. Level 0 is the Loading Docks (the vendor, no guards); levels 1..13 are
    /// office floors. The team may do three floors in a row, then must go back down to the docks (3, 6, 9, 12).
    /// Floor 12 holds the key to the hidden elevator that goes to floor 13, where the boss is.
    /// </summary>
    public static class TowerRules
    {
        public const int OfficeFloors = 13;
        /// <summary>The docks plus the office floors.</summary>
        public const int Levels = OfficeFloors + 1;
        public const int GroupSize = 3;
        public const int KeyFloor = 12;
        public const int BossFloor = 13;
        /// <summary>The quota on floor 1: the value of loot the team must drop in the elevator.</summary>
        public const int FirstQuota = 4300;

        /// <summary>
        /// The quota for an office floor: $4,300 on floor 1, then rising with the floor (loot is worth more and
        /// there is more of it higher up). 0 for the docks and the boss floor, which have no quota.
        /// </summary>
        public static int QuotaFor(int level)
        {
            if (level <= 0 || IsBoss(level)) return 0;
            double f = level;
            double share = 0.35 + 0.25 * System.Math.Min(1.0, System.Math.Max(0.0, (f - 1.0) / 9.0));
            double growth = (8.0 + f) * (1.0 + 0.12 * f) * share / (9.0 * 1.12 * 0.35); // 1.0 on floor 1
            return (int)(System.Math.Round(FirstQuota * growth / 100.0) * 100.0);
        }

        public static bool IsDocks(int level) => level <= 0;
        public static bool IsBoss(int level) => level == BossFloor;

        /// <summary>The last floor of a group of three (3, 6, 9, 12): the elevator will not go up from here.</summary>
        public static bool IsGroupEnd(int level) => level > 0 && level < BossFloor && level % GroupSize == 0;

        public static string Label(int level) => IsDocks(level) ? "Loading Docks" : IsBoss(level) ? "Floor 13 - Boss" : $"Floor {level}";

        /// <summary>
        /// Where the service elevator goes when it goes up from <paramref name="level"/>, or -1 if it cannot:
        /// not on the boss floor, not from the end of a group, and not before the floor's quota is met.
        /// From the docks it resumes at <paramref name="resume"/> (the next unfinished floor).
        /// </summary>
        public static int UpFrom(int level, bool floorComplete, int resume)
        {
            if (IsDocks(level)) return resume;
            if (IsBoss(level) || IsGroupEnd(level)) return -1;
            return floorComplete ? level + 1 : -1;
        }

        /// <summary>Down always returns to the Loading Docks (not from the docks, not from the boss floor).</summary>
        public static int DownFrom(int level) => IsDocks(level) || IsBoss(level) ? -1 : 0;

        /// <summary>The floor to come back to after leaving <paramref name="level"/> for the docks.</summary>
        public static int ResumeAfterLeaving(int level, bool floorComplete) => floorComplete ? level + 1 : level;
    }
}