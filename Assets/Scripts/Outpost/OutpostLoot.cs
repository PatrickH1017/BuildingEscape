using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>What a small valuable in a guard outpost is (it decides how it looks and what it is called).</summary>
    public enum OutpostValuable { Envelope, Watch, Chips }

    /// <summary>What one guard outpost holds besides its furniture: a note about a safe (maybe), weapons (maybe) and a few small valuables (maybe).</summary>
    public sealed class OutpostStash
    {
        /// <summary>The floor of the safe the note in this outpost is about; 0 when there is no note here.</summary>
        public int noteSafeFloor;
        /// <summary>Does the note give the safe's code (otherwise it only names the room)?</summary>
        public bool noteGivesCode;
        /// <summary>Weapons, as <see cref="MeleeWeapon.Kind"/> numbers (0 crowbar, 1 bat, 2 hatchet).</summary>
        public readonly List<int> weapons = new List<int>();
        /// <summary>Small valuables, as <see cref="OutpostValuable"/> numbers, and what each is worth.</summary>
        public readonly List<int> valuableKinds = new List<int>();
        public readonly List<int> valuableValues = new List<int>();

        public bool HasNote => noteSafeFloor > 0;
        public int ItemCount => (HasNote ? 1 : 0) + weapons.Count + valuableKinds.Count;
        public int ValuablesTotal { get { int t = 0; foreach (var v in valuableValues) t += v; return t; } }
    }

    /// <summary>
    /// What a guard outpost holds, decided from the run's seed and the floor alone (so every machine fills the same outpost the same way).
    /// Not every outpost has everything: the note exists only where a safe's note was placed (<see cref="SafeRules.NoteFor"/>), and the
    /// weapons and the valuables are rolled. Higher floors have better weapons (a hatchet needs the fourth floor), more of them, and
    /// valuables worth more (they follow the floor's quota).
    /// </summary>
    public static class OutpostLoot
    {
        public const int CrowbarKind = 0, BatKind = 1, HatchetKind = 2;
        /// <summary>The most things one outpost can hold (the planner makes this many places for them).</summary>
        public const int MaxItems = 6;
        public const int MaxWeapons = 2, MaxValuables = 3;

        const int SaltWeapon = 21, SaltCount = 22, SaltKind = 23, SaltValuables = 24, SaltValue = 25, SaltValuableKind = 26, SaltCrowbar = 27;

        /// <summary>The chance (percent) that an outpost on this floor has a weapon: 40% on floor 1 up to 75% on floor 12.</summary>
        public static int WeaponPercent(int floor) => 40 + Math.Min(11, Math.Max(0, floor - 1)) * 35 / 11;

        /// <summary>How likely each weapon is on this floor (crowbar, bat, hatchet; they need not add up to 100).</summary>
        public static int[] WeaponWeights(int floor)
        {
            if (floor <= 3) return new[] { 50, 50, 0 };
            if (floor <= 6) return new[] { 35, 35, 30 };
            if (floor <= 9) return new[] { 30, 25, 45 };
            return new[] { 25, 20, 55 };
        }

        /// <summary>The chance (percent) that an outpost has small valuables lying about.</summary>
        public const int ValuablesPercent = 60;

        /// <summary>The worth of a typical small valuable on this floor: about 3.5% of the floor's quota, in $25 steps ($150 on floor 1).</summary>
        public static int ValuableUnit(int floor) => Math.Max(25, (int)(Math.Round(TowerRules.QuotaFor(floor) * 0.035 / 25.0) * 25.0));

        public static string ValuableName(int kind) =>
            kind == (int)OutpostValuable.Watch ? "Gold Watch" : kind == (int)OutpostValuable.Chips ? "Casino Chips" : "Cash Envelope";

        public static string WeaponName(int kind) => kind == BatKind ? "Baseball Bat" : kind == HatchetKind ? "Hatchet" : "Crowbar";

        /// <summary>What the outpost on this floor holds.</summary>
        public static OutpostStash Roll(int runSeed, int floor)
        {
            var stash = new OutpostStash();
            if (floor < 1) return stash;

            // the note about a safe (one per outpost, if this outpost was given one)
            int safeFloor = SafeRules.NoteFor(runSeed, floor);
            if (safeFloor > 0)
            {
                stash.noteSafeFloor = safeFloor;
                stash.noteGivesCode = SafeRules.NoteGivesCode(runSeed, safeFloor);
            }

            // weapons: a note that gives no code means a crowbar is needed, and the guards often keep one close by
            var wanted = SafeRules.Unit(SafeRules.Hash(runSeed, floor, SaltWeapon)) * 100f;
            bool noCodeNote = stash.HasNote && !stash.noteGivesCode;
            bool crowbarHere = noCodeNote && SafeRules.Hash(runSeed, floor, SaltCrowbar) % 100U < 60U;
            if (crowbarHere) stash.weapons.Add(CrowbarKind);
            if (wanted < WeaponPercent(floor))
            {
                int count = 1;
                if (floor >= 4 && SafeRules.Hash(runSeed, floor, SaltCount) % 100U < 25U) count = 2;
                var weights = WeaponWeights(floor);
                for (int i = 0; i < count && stash.weapons.Count < MaxWeapons; i++)
                    stash.weapons.Add(PickWeapon(weights, SafeRules.Hash(runSeed, floor, SaltKind + i * 100)));
            }

            // small valuables
            if (SafeRules.Hash(runSeed, floor, SaltValuables) % 100U < (uint)ValuablesPercent)
            {
                uint r = SafeRules.Hash(runSeed, floor, SaltValuables + 1);
                int count = 1 + (int)(r % 100U < 20U ? 2 : r % 100U < 60U ? 1 : 0);
                int unit = ValuableUnit(floor);
                for (int i = 0; i < count && stash.valuableKinds.Count < MaxValuables; i++)
                {
                    int kind = (int)(SafeRules.Hash(runSeed, floor, SaltValuableKind + i * 100) % 3U);
                    float spread = 0.7f + 0.7f * SafeRules.Unit(SafeRules.Hash(runSeed, floor, SaltValue + i * 100));
                    float kindFactor = kind == (int)OutpostValuable.Watch ? 1.4f : kind == (int)OutpostValuable.Envelope ? 0.8f : 1f;
                    int value = Math.Max(25, (int)(Math.Round(unit * spread * kindFactor / 5.0) * 5.0));
                    stash.valuableKinds.Add(kind);
                    stash.valuableValues.Add(value);
                }
            }

            while (stash.ItemCount > MaxItems) stash.valuableKinds.RemoveAt(stash.valuableKinds.Count - 1);   // (cannot happen: 1 + 2 + 3 = 6)
            while (stash.valuableValues.Count > stash.valuableKinds.Count) stash.valuableValues.RemoveAt(stash.valuableValues.Count - 1);
            return stash;
        }

        static int PickWeapon(int[] weights, uint hash)
        {
            int total = 0;
            foreach (var w in weights) total += w;
            if (total <= 0) return CrowbarKind;
            int roll = (int)(hash % (uint)total);
            for (int i = 0; i < weights.Length; i++)
            {
                if (roll < weights[i]) return i;
                roll -= weights[i];
            }
            return CrowbarKind;
        }
    }
}
