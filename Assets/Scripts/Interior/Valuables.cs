using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The kinds of small valuable a floor is made rich with. The first three are the guard outposts' (<see cref="OutpostValuable"/> has the same numbers).
    /// </summary>
    public enum ValuableKind { Envelope, Watch, Chips, Phone, Wallet, Tablet, Laptop, Camera, Headphones, Console, Jewelry, Pen, Cash }

    /// <summary>One kind of small valuable: what it is called, what it is worth, how much it weighs and how much room it takes on a desk.</summary>
    public sealed class ValuableDef
    {
        public readonly ValuableKind kind;
        public readonly string label;
        /// <summary>Dollars on floor 1 at an average spread (the floor's scale, <see cref="Valuables.Scale"/>, and the spread multiply it).</summary>
        public readonly int value;
        /// <summary>Pack weight (the backpack takes 20; the weights are shown in pounds).</summary>
        public readonly float weight;
        /// <summary>How often it is picked, relative to the others.</summary>
        public readonly int share;
        /// <summary>The room it takes on a surface, in metres (width, depth); the laptop needs a roomy spot.</summary>
        public readonly float width, depth;

        public ValuableDef(ValuableKind kind, string label, int value, float weight, int share, float width, float depth)
        {
            this.kind = kind; this.label = label; this.value = value; this.weight = weight; this.share = share; this.width = width; this.depth = depth;
        }

        /// <summary>Does it need a roomy spot (more than a small one holds)?</summary>
        public bool Roomy => Math.Max(width, depth) > Valuables.SmallSpot;
    }

    /// <summary>
    /// The owner, 2026-10-17: "Loot needs to be more consistent as in rather than some monitors be interactable and some not, make the loot more
    /// stuff like watches and phones and such". So what a floor holds to steal is a short list of small valuables (this catalog) lying on desks, tables
    /// and counters - every one of a kind can be picked up, none of the furniture can - plus the few big treasures (<c>FloorGenerator.LootTable</c>).
    /// Plain data and arithmetic (no Unity types) so it is tested outside Unity; what a floor gets follows from the floor's seed alone, so every
    /// player's machine builds the same things.
    /// </summary>
    public static class Valuables
    {
        /// <summary>A spot this big (metres) holds any valuable but the roomy one (the laptop).</summary>
        public const float SmallSpot = 0.25f;

        public static readonly ValuableDef[] All =
        {
            new ValuableDef(ValuableKind.Envelope,   "Cash Envelope",     150, 0.20f, 10, 0.24f, 0.16f),
            new ValuableDef(ValuableKind.Watch,      "Gold Watch",        700, 0.15f,  6, 0.08f, 0.21f),
            new ValuableDef(ValuableKind.Chips,      "Casino Chips",      250, 0.35f,  4, 0.09f, 0.09f),
            new ValuableDef(ValuableKind.Phone,      "Smartphone",        400, 0.40f, 12, 0.08f, 0.16f),
            new ValuableDef(ValuableKind.Wallet,     "Wallet",            250, 0.30f,  8, 0.11f, 0.13f),
            new ValuableDef(ValuableKind.Tablet,     "Tablet",            550, 1.00f,  6, 0.21f, 0.15f),
            new ValuableDef(ValuableKind.Laptop,     "Laptop",           1000, 2.50f,  5, 0.34f, 0.26f),
            new ValuableDef(ValuableKind.Camera,     "Camera",            650, 1.20f,  4, 0.14f, 0.10f),
            new ValuableDef(ValuableKind.Headphones, "Headphones",        350, 0.70f,  6, 0.19f, 0.17f),
            new ValuableDef(ValuableKind.Console,    "Handheld Console",  450, 0.60f,  4, 0.20f, 0.09f),
            new ValuableDef(ValuableKind.Jewelry,    "Jewelry Box",      1200, 0.30f,  2, 0.10f, 0.13f),
            new ValuableDef(ValuableKind.Pen,        "Gold Pen",          300, 0.10f,  5, 0.16f, 0.04f),
            new ValuableDef(ValuableKind.Cash,       "Cash Stack",        400, 0.50f,  7, 0.16f, 0.07f),
        };

        public static ValuableDef Of(ValuableKind kind) => All[(int)kind];
        public static ValuableDef Of(int kind) => All[Math.Max(0, Math.Min(All.Length - 1, kind))];

        /// <summary>How many valuables a floor holds on its desks: 67 on floor 1, 76 on floor 4, 100 on floor 12 (fewer if the rooms have no room for them).</summary>
        public static int Count(int floor) => 64 + 3 * Math.Max(1, floor);

        /// <summary>
        /// How much the floor's valuables are worth next to floor 1's: the same as the floor's quota is next to floor 1's, so a floor always holds about the same
        /// number of quotas of loot (the QA wants 8 to 13 times the quota on the floor).
        /// </summary>
        public static double Scale(int floor) => floor < 1 ? 1.0 : Math.Max(1.0, TowerRules.QuotaFor(floor) / (TowerRules.FirstQuota * TowerRules.QuotaMultiplier));

        /// <summary>What one valuable is worth: its catalog value, the floor's scale and a spread of 0.8 to 1.3 (<paramref name="unit"/> is a number from 0 to 1), in $5 steps.</summary>
        public static int ValueOf(ValuableDef def, int floor, double unit)
        {
            double spread = 0.8 + 0.5 * Math.Max(0.0, Math.Min(1.0, unit));
            return Math.Max(5, (int)(Math.Round(def.value * Scale(floor) * spread / 5.0) * 5.0));
        }

        /// <summary>A room theme likes some valuables more than others (a conference table has laptops on it, a canteen table wallets and handhelds).</summary>
        public static int ThemeBias(string theme, ValuableKind kind)
        {
            switch (theme)
            {
                case "conference": case "meeting": case "training":
                    return kind == ValuableKind.Laptop ? 4 : kind == ValuableKind.Tablet ? 3 : kind == ValuableKind.Phone || kind == ValuableKind.Pen ? 2 : 1;
                case "office": case "longopen": case "open":
                    return kind == ValuableKind.Phone || kind == ValuableKind.Wallet || kind == ValuableKind.Pen ? 2 : kind == ValuableKind.Watch || kind == ValuableKind.Laptop ? 2 : 1;
                case "break": case "canteen": case "lounge": case "cafe_seating": case "nook":
                    return kind == ValuableKind.Console || kind == ValuableKind.Headphones || kind == ValuableKind.Wallet ? 3 : kind == ValuableKind.Cash || kind == ValuableKind.Phone ? 2 : 1;
                case "cafe_counter":
                    return kind == ValuableKind.Cash ? 5 : kind == ValuableKind.Wallet || kind == ValuableKind.Phone ? 2 : 1;
                case "server": case "copy":
                    return kind == ValuableKind.Laptop || kind == ValuableKind.Tablet ? 3 : kind == ValuableKind.Camera ? 2 : 1;
                case "storage":
                    return kind == ValuableKind.Camera || kind == ValuableKind.Envelope ? 3 : kind == ValuableKind.Jewelry ? 2 : 1;
                case "lobby": case "hall": case "hub": case "zone":
                    return kind == ValuableKind.Watch || kind == ValuableKind.Wallet || kind == ValuableKind.Jewelry ? 2 : 1;
                default:
                    return 1;
            }
        }

        /// <summary>
        /// Which kind lies at a spot: a weighted pick (the catalog's shares times the room theme's liking) from <paramref name="unit"/> (0 to 1). A spot without room
        /// for the roomy kinds never gets one.
        /// </summary>
        public static ValuableDef Pick(string theme, bool roomy, double unit)
        {
            long total = 0;
            foreach (var d in All) if (roomy || !d.Roomy) total += d.share * (long)ThemeBias(theme, d.kind);
            long at = (long)(Math.Max(0.0, Math.Min(0.999999, unit)) * total);
            foreach (var d in All)
            {
                if (!roomy && d.Roomy) continue;
                long w = d.share * (long)ThemeBias(theme, d.kind);
                if (at < w) return d;
                at -= w;
            }
            return All[(int)ValuableKind.Envelope];
        }

        /// <summary>A number from 0 to 1 that depends on the seed and the arguments only (the same on every machine).</summary>
        public static double Unit(int seed, int a, int b, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)(a + 1) * 40503u + 0x9E3779B9u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)(b + 1) * 69069u + 0x85EBCA6Bu;
                h *= 2246822519u;
                h ^= (uint)salt * 3266489917u;
                h ^= h >> 15;
                h *= 2654435761u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }
    }
}
