using System;

namespace BuildingEscape
{
    /// <summary>
    /// The rules of the safes and of the notes that tell the team about them, in one place and free of Unity types (so they can be
    /// tested outside Unity). Everything is worked out from the run's seed and the floor number alone, with an integer hash, so every
    /// machine of a networked run - and the guard outpost on an earlier floor, which has to name a safe on a floor that has not been
    /// built yet - gets exactly the same answer.
    ///
    ///  - A floor (1 to 12) has a safe (every floor does, see OneIn). Floors 1 and 2 have one, with cash in it; from floor 3 on the safes hold weapons (pew pistols) and there are several: two on floor 3, three on floors 4 to 6, four from floor 7 (the owner, 2026-10-18).
    ///  - Every safe has a 4-digit code. The note about it either gives the code and the room, or only the room ("it has a safe, nobody
    ///    knows the code"): then it takes a crowbar, which breaks 35% of the time.
    ///  - The record of a floor's safes is on the computer of that floor's own guard outpost, and the computer shows the safes of that floor and no other (the owner, 2026-10-18: "only the safes for that current floor").
    ///  - The cash inside (floors 1 and 2) is half the floor's quota: $2,150 on floor 1. The computer's record says, for every safe of its floor, the room, the code (always for the first safe of a weapon floor, else most of the time) and what is inside.
    /// </summary>
    public static class SafeRules
    {
        /// <summary>One floor in this many has a safe (the owner, 2026-10-18, after the first playtests: "There should be a safe on every floor": 1; it was 5).</summary>
        public const int OneIn = 1;
        public const int FirstFloor = 1;
        /// <summary>The highest floor that can have a safe (the floor with the hidden hallway; the boss floor has none).</summary>
        public const int LastFloor = TowerRules.KeyFloor;
        /// <summary>The chance that a crowbar breaks on a safe it has pried open.</summary>
        public const float CrowbarBreakChance = 0.35f;
        /// <summary>Share of the safes whose note gives the code (the rest only say where the safe is).</summary>
        public const int NoteGivesCodePercent = 62;
        /// <summary>The cash inside, as a share of the floor's quota.</summary>
        public const float CashShareOfQuota = 0.5f;
        /// <summary>From this floor on the safes hold weapons, not cash (the owner, 2026-10-18); floors 1 and 2 still hold cash.</summary>
        public const int WeaponFirstFloor = 3;
        /// <summary>The most safes one floor can have.</summary>
        public const int MaxSafes = 4;
        /// <summary>The number of the pew pistol among what a safe holds (the same number as <see cref="OutpostLoot.PewKind"/>).</summary>
        public const int PewItem = 3;

        // ------------------------------------------------------------------ hashing

        /// <summary>Good 32-bit mixing (Chris Wellons' "lowbias32").</summary>
        static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352dU;
                x ^= x >> 15; x *= 0x846ca68bU;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>A number from the run seed and two more numbers; the same inputs always give the same output, on every machine.</summary>
        public static uint Hash(int seed, int a, int b)
        {
            unchecked
            {
                uint h = Mix((uint)seed ^ 0x9E3779B9U);
                h = Mix(h + (uint)a * 0x85EBCA6BU + 0x1234567U);
                h = Mix(h + (uint)b * 0xC2B2AE35U + 0x7654321U);
                return h;
            }
        }

        /// <summary>The hash as a number in [0, 1).</summary>
        public static float Unit(uint hash) => (hash >> 8) / 16777216f;

        const int SaltExists = 1, SaltCode = 2, SaltNoteCode = 3, SaltRoom = 5, SaltCash = 6, SaltIndexCode = 31, SaltIndexNote = 32, SaltContents = 33;

        // ------------------------------------------------------------------ which floors have a safe

        /// <summary>Can this floor have a safe at all?</summary>
        public static bool IsSafeFloor(int floor) => floor >= FirstFloor && floor <= LastFloor;

        /// <summary>Does this floor have a safe? (Every office floor does; <see cref="OneIn"/> could make it a lottery again.)</summary>
        public static bool HasSafe(int runSeed, int floor) =>
            IsSafeFloor(floor) && Hash(runSeed, floor, SaltExists) % OneIn == 0;

        /// <summary>The 4-digit code of the safe on this floor (1000 to 9999), whether or not the floor has a safe.</summary>
        public static int Code(int runSeed, int floor) => 1000 + (int)(Hash(runSeed, floor, SaltCode) % 9000U);

        /// <summary>Does the note about the safe on this floor give its code? (If not, it only says which room the safe is in.)</summary>
        public static bool NoteGivesCode(int runSeed, int floor) =>
            Hash(runSeed, floor, SaltNoteCode) % 100U < (uint)NoteGivesCodePercent;

        /// <summary>A number the planner uses to choose the safe's room among those that can take one.</summary>
        public static uint RoomRoll(int runSeed, int floor) => Hash(runSeed, floor, SaltRoom);

        /// <summary>The floors that have a safe, in order.</summary>
        public static int[] SafeFloors(int runSeed)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int f = FirstFloor; f <= LastFloor; f++) if (HasSafe(runSeed, f)) list.Add(f);
            return list.ToArray();
        }

        // ------------------------------------------------------------------ several safes, and weapons in them (the owner, 2026-10-18)

        /// <summary>Do the safes of this floor hold weapons (floor 3 and up) rather than cash (floors 1 and 2)?</summary>
        public static bool IsWeaponFloor(int floor) => floor >= WeaponFirstFloor;

        /// <summary>Do the safes of this floor hold cash (floors 1 and 2)?</summary>
        public static bool HoldsCash(int floor) => IsSafeFloor(floor) && !IsWeaponFloor(floor);

        /// <summary>How many safes this floor has: one on floors 1 and 2, two on floor 3, three on floors 4 to 6, four from floor 7 ("floor three and up should also have multiple safes").</summary>
        public static int SafeCount(int runSeed, int floor)
        {
            if (!HasSafe(runSeed, floor)) return 0;
            if (floor < WeaponFirstFloor) return 1;
            return floor == WeaponFirstFloor ? 2 : floor < 7 ? 3 : MaxSafes;
        }

        /// <summary>The code of safe number <paramref name="index"/> (0 first) on this floor; the first one is <see cref="Code(int,int)"/>, and no two safes of a floor have the same.</summary>
        public static int Code(int runSeed, int floor, int index)
        {
            if (index <= 0) return Code(runSeed, floor);
            for (int attempt = 0; ; attempt++)
            {
                int code = 1000 + (int)(Hash(runSeed, floor, SaltIndexCode + index * 101 + attempt * 1009) % 9000U);
                bool taken = false;
                for (int i = 0; i < index; i++) if (Code(runSeed, floor, i) == code) taken = true;
                if (!taken || attempt > 30) return code;
            }
        }

        /// <summary>
        /// Does the computer's record give the code of safe number <paramref name="index"/> on this floor (otherwise it only says where the safe is, and it takes a crowbar)? On a floor with weapons the first
        /// safe's code is always on record, so that the team always has a safe to open; the others are on record <see cref="NoteGivesCodePercent"/> percent of the time. Floors 1 and 2: as before.
        /// </summary>
        public static bool RecordGivesCode(int runSeed, int floor, int index)
        {
            if (index < 0 || index >= SafeCount(runSeed, floor)) return false;
            if (!IsWeaponFloor(floor)) return NoteGivesCode(runSeed, floor);
            if (index == 0) return true;
            return Hash(runSeed, floor, SaltIndexNote + index * 103) % 100U < (uint)NoteGivesCodePercent;
        }

        /// <summary>Is there a safe on this floor whose code the record does not give (so that a crowbar is wanted)?</summary>
        public static bool AnySafeNeedsCrowbar(int runSeed, int floor)
        {
            int n = SafeCount(runSeed, floor);
            for (int i = 0; i < n; i++) if (!RecordGivesCode(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>
        /// What a weapon safe holds, as item numbers (<see cref="PewItem"/>): one or two pew pistols. The pew is the only gun there is, and a melee weapon is longer than the safe is wide, so a safe
        /// holds guns only. Nothing for a safe on a floor with cash, or one that does not exist.
        /// </summary>
        public static int[] WeaponContents(int runSeed, int floor, int index)
        {
            if (!IsWeaponFloor(floor) || index < 0 || index >= SafeCount(runSeed, floor)) return new int[0];
            int n = 1 + (int)(Hash(runSeed, floor, SaltContents + index * 107) % 2U);
            var items = new int[n];
            for (int i = 0; i < n; i++) items[i] = PewItem;
            return items;
        }

        // ------------------------------------------------------------------ where the record is

        /// <summary>
        /// The floor whose guard outpost holds the record of the safes on <paramref name="safeFloor"/> (0 if that floor has no safe): its own. The computer of a floor lists the safes of that floor and no other (the owner,
        /// 2026-10-18: "the Safes tab on the computer should only have the safes for that current floor"); a safe used to be announced a floor or two ahead.
        /// </summary>
        public static int SourceFloor(int runSeed, int safeFloor) => HasSafe(runSeed, safeFloor) ? safeFloor : 0;

        /// <summary>The floor whose safes the guard outpost on <paramref name="outpostFloor"/> has a record of (0: no record there): its own.</summary>
        public static int NoteFor(int runSeed, int outpostFloor) => HasSafe(runSeed, outpostFloor) ? outpostFloor : 0;

        // ------------------------------------------------------------------ what is inside

        /// <summary>The cash in the safe on this floor: half the floor's quota ($2,150 on floor 1).</summary>
        public static int Cash(int floor) => (int)(Math.Round(TowerRules.QuotaFor(floor) * (double)CashShareOfQuota / 50.0) * 50.0);

        /// <summary>How many bundles of cash the safe holds on this floor (a few more on higher floors).</summary>
        public static int Bundles(int floor) => 4 + Math.Min(4, Math.Max(0, floor - 1) / 3);

        /// <summary>The value of each bundle: the cash shared out in $5 steps (the first bundles get the odd dollars), adding up to exactly <see cref="Cash"/>.</summary>
        public static int[] Split(int total, int count)
        {
            if (count < 1) count = 1;
            var parts = new int[count];
            int each = total / count / 5 * 5;
            int left = total;
            for (int i = 0; i < count; i++) { parts[i] = each; left -= each; }
            for (int i = 0; left > 0; i = (i + 1) % count)
            {
                int add = Math.Min(5, left);
                parts[i] += add; left -= add;
            }
            return parts;
        }

        /// <summary>Does the crowbar break after prying a safe open? <paramref name="roll"/> is a random number in [0, 1).</summary>
        public static bool CrowbarBreaks(float roll) => roll < CrowbarBreakChance;

        // ------------------------------------------------------------------ the codes on the keypad

        /// <summary>The code as the four digits it is typed with.</summary>
        public static string CodeText(int code) => code.ToString("D4");

        /// <summary>
        /// Adds a digit to the code being typed (up to 4 digits); returns the new text. A non-digit is ignored.
        /// </summary>
        public static string AddDigit(string typed, char digit)
        {
            typed = typed ?? "";
            if (digit < '0' || digit > '9' || typed.Length >= 4) return typed;
            return typed + digit;
        }

        /// <summary>Removes the last typed digit.</summary>
        public static string Backspace(string typed) => string.IsNullOrEmpty(typed) ? "" : typed.Substring(0, typed.Length - 1);

        /// <summary>Is this the safe's code?</summary>
        public static bool CodeMatches(string typed, int code) => typed != null && typed.Length == 4 && typed == CodeText(code);
    }
}
