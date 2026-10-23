using System;

namespace BuildingEscape
{
    /// <summary>
    /// A guard's health, and what a melee weapon takes off it (the owner, 2026-10-22: "right now melee weapons are not killing guards which is not the intent ... a health based system for guards. Melee weapons should deal a
    /// specific amount of damage"). Every base guard has <see cref="BaseHealth"/> = 100 HP; a hit of the crowbar takes <see cref="CrowbarDamage"/> = 15, the bat <see cref="BatDamage"/> = 25, the hatchet <see cref="HatchetDamage"/> = 35,
    /// so a base guard goes down after 7 crowbar, 4 bat or 3 hatchet hits. The numbers are here, in one place, so that they can be changed without touching the combat code (<see cref="MeleeWeapon.Specs"/> carries them to the weapons).
    ///
    /// It is built on the damage count a guard already had (<see cref="GuardAI.Damage"/>, in "hits": <see cref="GuardAI.hitsToKill"/> = 10 put a normal officer down for good, a bullet counts as its gun's <c>guardHits</c>): one hit of that
    /// count is <see cref="HpPerHit"/> = 10 HP, so a guard with the usual 10 hits to give has 100 HP and the guns kill exactly as before (a pew bullet of 4 hits is 40 HP). Bullets, blows and the rest take from the same pool.
    ///
    /// Nothing here knows how many guards there are, how many are alerted or how many are attacking: a blow takes the same off a guard whether he is alone or in a crowd (the owner's rule). Plain numbers and functions, no Unity types,
    /// so they are tested outside Unity.
    /// </summary>
    public static class GuardHealthRules
    {
        /// <summary>How many "hits" of the damage count put a base guard down for good (the default of <see cref="GuardAI.hitsToKill"/>).</summary>
        public const int BaseHitsToKill = 10;

        /// <summary>How many HP one "hit" of the guard's damage count is.</summary>
        public const float HpPerHit = 10f;

        /// <summary>The health of every base guard (100 HP), whether he is alone or one of a group.</summary>
        public const float BaseHealth = BaseHitsToKill * HpPerHit;

        /// <summary>
        /// What one hit of each melee weapon takes off a guard's health (HP): the crowbar the least (it opens safes as well), the hatchet the most. The same on every hit, whoever swings and however many guards are about.
        /// Seven, four and three hits put a base guard (100 HP) down.
        /// </summary>
        public const float CrowbarDamage = 15f, BatDamage = 25f, HatchetDamage = 35f;

        /// <summary>A guard whose damage is this close (in hits) to his full health counts as down: the float sums of shielded bullets must not leave him alive at 0.0000001 HP.</summary>
        public const float DownTolerance = 0.001f;

        /// <summary>HP to hits of the damage count, and back.</summary>
        public static float HpToHits(float hp) => hp / HpPerHit;
        public static float HitsToHp(float hits) => hits * HpPerHit;

        /// <summary>A guard's full health in HP, from the hits it takes to put him down (10 for a base guard: 100 HP; 30 for the boss: 300 HP).</summary>
        public static float MaxHealth(int hitsToKill) => hitsToKill * HpPerHit;

        /// <summary>His health in HP after <paramref name="damageInHits"/> hits of damage (never below 0).</summary>
        public static float HealthLeft(float damageInHits, int hitsToKill) => Math.Max(0f, MaxHealth(hitsToKill) - HitsToHp(damageInHits));

        /// <summary>Is he down for good? His health has reached 0 (or less): the damage has reached the hits it takes.</summary>
        public static bool IsDown(float damageInHits, int hitsToKill) => damageInHits >= hitsToKill - DownTolerance;

        /// <summary>
        /// The damage count of a guard after a melee blow that takes <paramref name="weaponDamage"/> HP. Nothing else goes into it: not the number of guards on the floor, not how many are alerted or attacking, not the guard's
        /// place in a group. A blow with no damage (0 or less) changes nothing.
        /// </summary>
        public static float DamageAfterBlow(float damageInHits, float weaponDamage) => weaponDamage > 0f ? damageInHits + HpToHits(weaponDamage) : damageInHits;

        /// <summary>How many blows of <paramref name="damagePerBlow"/> HP put a guard with <paramref name="maxHealth"/> HP down (7 crowbar, 4 bat, 3 hatchet blows for 100 HP); <see cref="int.MaxValue"/> when the blow does nothing.</summary>
        public static int BlowsToKill(float maxHealth, float damagePerBlow)
        {
            if (damagePerBlow <= 0f) return int.MaxValue;
            return Math.Max(1, (int)Math.Ceiling(Math.Max(0.01f, maxHealth) / damagePerBlow - 1e-4));
        }
    }
}
