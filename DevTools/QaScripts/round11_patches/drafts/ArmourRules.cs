using System;

namespace BuildingEscape
{
    /// <summary>
    /// The armour a player can find and wear (the owner, 2026-10-19: "On the third floor players should have the chance of finding Light Armour instead of a weapon in the safe. After finding the Light Armour if the player who found the armour
    /// makes it back to the Loading Docks, everyone in the party should be able to buy the Light Armour from the Vendor from the Vendor. The same should go for the Medium Armour after found on the 7th floor and the Heavy Armour found on the 10th
    /// floor"). Pure rules, free of Unity types so they can be tested outside it. The tiers are the guards' shield tiers (<see cref="ShieldTier"/>: light, medium, heavy).
    /// What armour DOES is my placeholder (the owner has not said): a guard's bullet knocks the wearer down for less time and shoves him less - by the same shares a shield takes of a bullet (<see cref="GuardLoadouts.BulletShare"/>: 75 %, 50 %, 30 %) -
    /// and it does not stop the blast of a shotgun or a sniper rifle. A piece of armour is an item with a weight, worn while it is in the pack (so heavy armour costs room for loot).
    /// </summary>
    public static class ArmourRules
    {
        /// <summary>The floors where each tier is first found (the owner's ladder).</summary>
        public const int LightFloor = 3, MediumFloor = 7, HeavyFloor = 10;

        public static int FirstFloor(ShieldTier tier)
        {
            switch (tier)
            {
                case ShieldTier.Light: return LightFloor;
                case ShieldTier.Medium: return MediumFloor;
                case ShieldTier.Heavy: return HeavyFloor;
                default: return int.MaxValue;
            }
        }

        /// <summary>The tier that is first found on this floor, or <see cref="ShieldTier.None"/> when no armour is.</summary>
        public static ShieldTier NewTierOn(int floor) => floor == LightFloor ? ShieldTier.Light : floor == MediumFloor ? ShieldTier.Medium : floor == HeavyFloor ? ShieldTier.Heavy : ShieldTier.None;

        public static bool IsArmour(ShieldTier tier) => tier != ShieldTier.None;

        public static string Name(ShieldTier tier)
        {
            switch (tier)
            {
                case ShieldTier.Light: return "Light Armour";
                case ShieldTier.Medium: return "Medium Armour";
                case ShieldTier.Heavy: return "Heavy Armour";
                default: return "No armour";
            }
        }

        /// <summary>What a piece weighs in the pack, in kilograms (placeholders: the pack holds 20 kg, so heavy armour takes two fifths of it).</summary>
        public static float Weight(ShieldTier tier) => tier == ShieldTier.Heavy ? 8f : tier == ShieldTier.Medium ? 5f : 2.5f;

        /// <summary>What the dealer asks (placeholders, dear).</summary>
        public static int Price(ShieldTier tier) => tier == ShieldTier.Heavy ? 2800 : tier == ShieldTier.Medium ? 1400 : 600;

        /// <summary>The share of a guard's bullet that gets through the wearer's armour: how long it knocks him down and how hard it shoves him (1 with no armour).</summary>
        public static float HitShare(ShieldTier tier) => GuardLoadouts.BulletShare(tier);

        /// <summary>How long a guard's bullet knocks a player down who wears this armour.</summary>
        public static float StumbleSeconds(float baseSeconds, ShieldTier tier) => baseSeconds * HitShare(tier);

        /// <summary>Armour of the better tier of two.</summary>
        public static ShieldTier Better(ShieldTier a, ShieldTier b) => (ShieldTier)Math.Max((int)a, (int)b);

        /// <summary>What a player is told who already wears armour and goes for another piece (a placeholder: the owner writes the words).</summary>
        public const string AlreadyWearing = "You already wear armour";

        // ------------------------------------------------------------------ in the safes

        /// <summary>The chance (percent) that a safe that is not the first of its floor holds armour instead of a gun: on the floors where a tier is first found, and on every other floor from 3 on.</summary>
        public const int UnlockFloorPercent = 50, OtherFloorPercent = 25;

        public static bool IsUnlockFloor(int floor) => floor == LightFloor || floor == MediumFloor || floor == HeavyFloor;

        /// <summary>The chance (percent) for one safe (not the first of its floor: a gun is always in it) to hold armour.</summary>
        public static int SafePercent(int floor) => floor < LightFloor ? 0 : IsUnlockFloor(floor) ? UnlockFloorPercent : OtherFloorPercent;

        /// <summary>How likely each tier is to be the armour a safe on this floor holds, by tier (index = the tier's number, 0 = none = never): nothing before its floor, three times as likely on its own floor as later.</summary>
        public static int[] TierWeights(int floor)
        {
            var w = new int[4];
            for (int t = 1; t <= 3; t++)
            {
                int first = FirstFloor((ShieldTier)t);
                if (floor < first) continue;
                w[t] = floor == first ? 3 : 1;
            }
            return w;
        }
    }
}
