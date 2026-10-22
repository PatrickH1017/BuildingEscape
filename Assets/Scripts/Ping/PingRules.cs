using UnityEngine;

namespace BuildingEscape
{
    /// <summary>What a ping can be put on: an item lying about, or a doorway.</summary>
    public enum PingTargetKind : byte { Item = 0, Door = 1 }

    /// <summary>The kind of thing a ping marks (it decides the little sign in the marker): loot, a weapon or other gear, something that is used up, a door.</summary>
    public enum PingCategory : byte { Loot = 0, Gear = 1, Consumable = 2, Door = 3 }

    /// <summary>
    /// The rules of the ping (the owner, 2026-10-21: "Add a ping system. Players can ping by pressing down on the scroll wheel and whatever is in their crosshairs will be the target. The ping should only work on lootable items, consumable items,
    /// weapons and doors."). Pure: how long a ping lasts and how it fades, what a thing counts as, how far a ping may be placed. <see cref="PingSystem"/> works the input and draws the markers; <c>RunSync.Ping.cs</c> carries them to the team.
    /// </summary>
    public static class PingRules
    {
        /// <summary>How long a marker stays (seconds), how long its first pop takes, and how long it fades out at the end.</summary>
        public const float Seconds = 8f, PopSeconds = 0.35f, FadeOutSeconds = 1.2f;

        /// <summary>How far away something can be pinged (metres: a straight look down a long hallway) and how much more the host allows (the sender's view of the world is a moment old).</summary>
        public const float MaxRange = 80f, HostSlack = 10f;

        /// <summary>The time between two pings of one player (seconds); the host also keeps a rate limit per player.</summary>
        public const float Cooldown = 0.5f;

        /// <summary>From this worth (dollars) up a thing is loot; a thing that is worth less (a mug) is something to throw.</summary>
        public const int LootMinValue = 50;

        /// <summary>The middle mouse button (a click of the scroll wheel).</summary>
        public const int MouseButton = 2;

        /// <summary>How opaque a marker is <paramref name="age"/> seconds after it was made: in at once, steady, out over the last <see cref="FadeOutSeconds"/>; 0 before it and after <see cref="Seconds"/>.</summary>
        public static float Alpha(float age)
        {
            if (age < 0f || age >= Seconds) return 0f;
            if (age > Seconds - FadeOutSeconds) return Mathf.Clamp01((Seconds - age) / FadeOutSeconds);
            return Mathf.Clamp01(age / 0.08f);
        }

        /// <summary>The size of a marker (1 = full): it pops in a little over full size and settles (the same pop as the scanner's marks).</summary>
        public static float Pop(float age)
        {
            if (age <= 0f) return 0f;
            if (age >= PopSeconds) return 1f;
            float t = age / PopSeconds - 1f;                                // (easeOutBack: 0 at the start, a little over 1 on the way, 1 at the end)
            return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
        }

        /// <summary>What an item is, for its marker: a weapon or armour is gear, a spare clip or anything worth less than <see cref="LootMinValue"/> is something that is used up, the rest is loot.</summary>
        public static PingCategory CategoryOf(bool weapon, bool armour, bool ammo, int value)
        {
            if (weapon || armour) return PingCategory.Gear;
            if (ammo) return PingCategory.Consumable;
            return value >= LootMinValue ? PingCategory.Loot : PingCategory.Consumable;
        }

        /// <summary>The single letter in the marker's diamond.</summary>
        public static string Sign(PingCategory c) => c == PingCategory.Loot ? "$" : c == PingCategory.Gear ? "!" : c == PingCategory.Door ? "D" : "+";

        /// <summary>Does the host take a ping at <paramref name="at"/> from a player standing at <paramref name="from"/>? It must be a real place within the range (plus the slack).</summary>
        public static bool HostAccepts(Vector3 from, Vector3 at)
        {
            if (float.IsNaN(at.x + at.y + at.z) || float.IsInfinity(at.x) || float.IsInfinity(at.y) || float.IsInfinity(at.z)) return false;
            return (at - from).sqrMagnitude <= (MaxRange + HostSlack) * (MaxRange + HostSlack);
        }

        /// <summary>The name on a marker: what it is, and for loot what it is worth and, when it takes more than one to carry, how many ("Gold Watch  $700", "Prototype Server  $2,500  (x2 carry)").</summary>
        public static string NameOf(string label, PingCategory c, bool locked = false, int value = 0, int lifters = 1)
        {
            if (c == PingCategory.Door) return locked ? "Locked door" : "Door";
            string name = string.IsNullOrWhiteSpace(label) ? "Item" : label;
            if (c != PingCategory.Loot || value <= 0) return name;
            string money = "$" + value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            return lifters > 1 ? $"{name}  {money}  (x{lifters} carry)" : $"{name}  {money}";
        }
    }
}
