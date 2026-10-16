using System;

namespace BuildingEscape
{
    /// <summary>
    /// The rules of the safes and of the notes that tell the team about them, in one place and free of Unity types (so they can be
    /// tested outside Unity). Everything is worked out from the run's seed and the floor number alone, with an integer hash, so every
    /// machine of a networked run - and the guard outpost on an earlier floor, which has to name a safe on a floor that has not been
    /// built yet - gets exactly the same answer.
    ///
    ///  - A floor (1 to 12) has a safe with a 1 in 5 chance; never more than one.
    ///  - Every safe has a 4-digit code. The note about it either gives the code and the room, or only the room ("it has a safe, nobody
    ///    knows the code"): then it takes a crowbar, which breaks 35% of the time.
    ///  - The note sits in the guard outpost of the same floor or of one of the two floors below it (so a safe can be announced a
    ///    floor or two ahead), and an outpost holds at most one note.
    ///  - The cash inside is half the floor's quota: $2,150 on floor 1.
    /// </summary>
    public static class SafeRules
    {
        /// <summary>One floor in this many has a safe.</summary>
        public const int OneIn = 5;
        public const int FirstFloor = 1;
        /// <summary>The highest floor that can have a safe (the floor with the hidden hallway; the boss floor has none).</summary>
        public const int LastFloor = TowerRules.KeyFloor;
        /// <summary>The chance that a crowbar breaks on a safe it has pried open.</summary>
        public const float CrowbarBreakChance = 0.35f;
        /// <summary>How many floors below a safe its note may be found (0 = the same floor only).</summary>
        public const int MaxLookAhead = 2;
        /// <summary>Share of the safes whose note gives the code (the rest only say where the safe is).</summary>
        public const int NoteGivesCodePercent = 62;
        /// <summary>The cash inside, as a share of the floor's quota.</summary>
        public const float CashShareOfQuota = 0.5f;

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

        const int SaltExists = 1, SaltCode = 2, SaltNoteCode = 3, SaltSource = 4, SaltRoom = 5, SaltCash = 6;

        // ------------------------------------------------------------------ which floors have a safe

        /// <summary>Can this floor have a safe at all?</summary>
        public static bool IsSafeFloor(int floor) => floor >= FirstFloor && floor <= LastFloor;

        /// <summary>Does this floor have a safe? (1 in 5, decided by the run seed.)</summary>
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

        // ------------------------------------------------------------------ where the note is

        /// <summary>
        /// The floor whose guard outpost holds the note about the safe on <paramref name="safeFloor"/> (0 if that floor has no safe).
        /// It is the safe's own floor half of the time, one floor below 30% of the time and two floors below 20% of the time - or
        /// the next floor up if that outpost already holds another safe's note (an outpost holds only one).
        /// </summary>
        public static int SourceFloor(int runSeed, int safeFloor)
        {
            if (!HasSafe(runSeed, safeFloor)) return 0;
            var taken = new bool[LastFloor + 1];
            for (int g = FirstFloor; g <= safeFloor; g++)
            {
                if (!HasSafe(runSeed, g)) continue;
                int source = Choose(runSeed, g, taken);
                taken[source] = true;
                if (g == safeFloor) return source;
            }
            return safeFloor;   // (cannot happen)
        }

        static int Choose(int runSeed, int g, bool[] taken)
        {
            uint roll = Hash(runSeed, g, SaltSource) % 100U;
            int back = roll < 50 ? 0 : roll < 80 ? 1 : 2;
            for (int f = Math.Max(FirstFloor, g - back); f <= g; f++)
                if (!taken[f]) return f;
            return g;
        }

        /// <summary>The floor whose safe the guard outpost on <paramref name="outpostFloor"/> has a note about (0: no note there).</summary>
        public static int NoteFor(int runSeed, int outpostFloor)
        {
            if (outpostFloor < FirstFloor || outpostFloor > LastFloor) return 0;
            var taken = new bool[LastFloor + 1];
            for (int g = FirstFloor; g <= LastFloor; g++)
            {
                if (!HasSafe(runSeed, g)) continue;
                int source = Choose(runSeed, g, taken);
                taken[source] = true;
                if (source == outpostFloor) return g;
            }
            return 0;
        }

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
