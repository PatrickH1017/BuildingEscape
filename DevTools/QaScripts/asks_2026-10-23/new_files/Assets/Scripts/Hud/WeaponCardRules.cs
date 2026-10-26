using System;
using System.Globalization;

namespace BuildingEscape
{
    /// <summary>
    /// The two lines on the Vendor's cards for weapons (the owner, 2026-10-23, with a picture: "The Vendor UI for weapons should say Damage: x / Stun Time: x"): what one hit takes off a guard's health (a base guard has 100 HP) and how long a hit stuns him.
    /// Pure text and numbers, no Unity types; the numbers come from the weapons' own specs (see <c>Vendor</c>), so a card never says something the weapon does not do.
    /// </summary>
    public static class WeaponCardRules
    {
        /// <summary>A number as the cards write it: whole when it is whole, else one decimal ("15", "2.5").</summary>
        public static string Number(float value)
        {
            float rounded = (float)Math.Round(value);
            return Math.Abs(value - rounded) < 0.05f ? ((int)rounded).ToString(CultureInfo.InvariantCulture) : value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>"Damage: 15": the health, in HP, one hit takes off a guard.</summary>
        public static string DamageLine(float hp) => "Damage: " + Number(hp);

        /// <summary>"Stun Time: 4s": how many seconds a hit stuns a guard (0s for a weapon that does not stun, a gun).</summary>
        public static string StunLine(float seconds) => "Stun Time: " + Number(Math.Max(0f, seconds)) + "s";

        /// <summary>What a bullet of a gun takes off a guard: the hits of the damage count it is worth, in HP.</summary>
        public static float GunDamage(int guardHits) => GuardHealthRules.HitsToHp(Math.Max(0, guardHits));
    }
}
