using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The health-based melee (the owner, 2026-10-22: "melee weapons are not killing guards ... a health based system for guards"): every base guard has 100 HP, a crowbar hit takes 15, a bat hit 25, a hatchet hit 35, so he goes down after
        /// 7, 4 or 3 hits; the same whatever the number of guards; bullets and blows take from the same pool. Pure: it runs the rules of <see cref="GuardHealthRules"/> the way <see cref="GuardAI.MeleeHitHost"/> uses them (the Play Mode
        /// scenarios in NetDevCombat / NetDevR7 / NetDevMeleeNet run the guard itself).
        /// </summary>
        public static void MeleeHealthTest()
        {
            var specs = MeleeWeapon.Specs;
            float crowbar = specs[(int)MeleeWeapon.Kind.Crowbar].guardDamage, bat = specs[(int)MeleeWeapon.Kind.Bat].guardDamage, hatchet = specs[(int)MeleeWeapon.Kind.Hatchet].guardDamage;

            Check("melee health: a base guard has exactly 100 HP (ten hits of 10 HP), the boss 300", Mathf.Approximately(GuardHealthRules.BaseHealth, 100f) && Mathf.Approximately(GuardHealthRules.MaxHealth(GuardHealthRules.BaseHitsToKill), 100f) && Mathf.Approximately(GuardHealthRules.MaxHealth(30), 300f));
            Check("melee health: a crowbar hit takes 15, a bat hit 25, a hatchet hit 35", Mathf.Approximately(crowbar, 15f) && Mathf.Approximately(bat, 25f) && Mathf.Approximately(hatchet, 35f));
            Check("melee health: crowbar < bat < hatchet (the crowbar also opens safes)", crowbar < bat && bat < hatchet);
            Check("melee health: 7 crowbar, 4 bat or 3 hatchet hits put a 100 HP guard down", GuardHealthRules.BlowsToKill(100f, crowbar) == 7 && GuardHealthRules.BlowsToKill(100f, bat) == 4 && GuardHealthRules.BlowsToKill(100f, hatchet) == 3);

            // ---- blow by blow, from a fresh guard: health falls by exactly the weapon's damage each time, never resets, and he is down at the expected blow and not before
            bool sequenceOk = true;
            foreach (var dmg in new[] { crowbar, bat, hatchet })
            {
                float damage = 0f, lastHealth = GuardHealthRules.HealthLeft(0f, GuardHealthRules.BaseHitsToKill);
                sequenceOk &= Mathf.Approximately(lastHealth, 100f) && !GuardHealthRules.IsDown(0f, GuardHealthRules.BaseHitsToKill);
                int expected = GuardHealthRules.BlowsToKill(100f, dmg), blows = 0;
                while (!GuardHealthRules.IsDown(damage, GuardHealthRules.BaseHitsToKill) && blows < 50)
                {
                    damage = GuardHealthRules.DamageAfterBlow(damage, dmg);
                    blows++;
                    float health = GuardHealthRules.HealthLeft(damage, GuardHealthRules.BaseHitsToKill);
                    sequenceOk &= health < lastHealth && Mathf.Abs(health - Mathf.Max(0f, 100f - blows * dmg)) < 0.001f;
                    lastHealth = health;
                }
                sequenceOk &= blows == expected && lastHealth <= 0.001f;
            }
            Check("melee health: blow by blow health falls by exactly the weapon's damage (no reset) and he is down at the 7th crowbar, 4th bat, 3rd hatchet hit and not before", sequenceOk);

            // ---- the group does not matter: the same blow takes the same off every guard, alone or among many, and one guard's blow never touches the next
            bool groupOk = true;
            foreach (int crowd in new[] { 1, 3, 8, 24 })
            {
                var damages = new List<float>();
                for (int i = 0; i < crowd; i++) damages.Add(0f);
                for (int i = 0; i < crowd; i++) damages[i] = GuardHealthRules.DamageAfterBlow(damages[i], bat);          // each guard takes one bat hit
                for (int i = 0; i < crowd; i++) groupOk &= Mathf.Approximately(GuardHealthRules.HealthLeft(damages[i], GuardHealthRules.BaseHitsToKill), 75f);
                damages[0] = GuardHealthRules.DamageAfterBlow(damages[0], bat);                                           // a second hit on the first only
                groupOk &= Mathf.Approximately(GuardHealthRules.HealthLeft(damages[0], GuardHealthRules.BaseHitsToKill), 50f);
                for (int i = 1; i < crowd; i++) groupOk &= Mathf.Approximately(GuardHealthRules.HealthLeft(damages[i], GuardHealthRules.BaseHitsToKill), 75f);
            }
            Check("melee health: a guard loses the same HP to the same blow alone or in a crowd of 3, 8 or 24, and a blow on one guard leaves the others alone", groupOk);

            // ---- a guard who is down takes nothing more that counts: his health stays at 0
            float down = 0f; for (int i = 0; i < 3; i++) down = GuardHealthRules.DamageAfterBlow(down, hatchet);
            float more = down; for (int i = 0; i < 5; i++) more = GuardHealthRules.DamageAfterBlow(more, hatchet);
            Check("melee health: health never goes below 0 (a guard hit past his last HP is at 0, not negative)", GuardHealthRules.IsDown(down, GuardHealthRules.BaseHitsToKill) && Mathf.Approximately(GuardHealthRules.HealthLeft(down, GuardHealthRules.BaseHitsToKill), 0f) && Mathf.Approximately(GuardHealthRules.HealthLeft(more, GuardHealthRules.BaseHitsToKill), 0f));
            Check("melee health: a blow with no damage changes nothing", Mathf.Approximately(GuardHealthRules.DamageAfterBlow(4.5f, 0f), 4.5f) && Mathf.Approximately(GuardHealthRules.DamageAfterBlow(4.5f, -3f), 4.5f));

            // ---- bullets and blows take from the same pool, and the guns kill exactly as before
            float pewHits = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;
                        Check("melee health: a pew bullet (2.5 hits) is 25 HP, four of them put a guard down and three do not", Mathf.Approximately(GuardHealthRules.HitsToHp(pewHits), 25f) && GunRules.BulletsToKill(GuardHealthRules.BaseHitsToKill, pewHits) == 4 && !GuardHealthRules.IsDown(3 * pewHits, GuardHealthRules.BaseHitsToKill) && GuardHealthRules.IsDown(4 * pewHits, GuardHealthRules.BaseHitsToKill));
            float mixed = RangedWeapon.Specs[(int)RangedWeapon.Kind.Rifle].guardHits;                                                                                      // one pew bullet: 40 HP gone
            for (int i = 0; i < 3; i++) mixed = GuardHealthRules.DamageAfterBlow(mixed, crowbar);                         // three crowbar hits: 45 more, 15 left
            bool aliveAfterThree = !GuardHealthRules.IsDown(mixed, GuardHealthRules.BaseHitsToKill) && Mathf.Approximately(GuardHealthRules.HealthLeft(mixed, GuardHealthRules.BaseHitsToKill), 15f);
            mixed = GuardHealthRules.DamageAfterBlow(mixed, crowbar);
            Check("melee health: a bullet and then crowbar hits share one health (40 + 4 x 15 puts him down at the fourth hit)", aliveAfterThree && GuardHealthRules.IsDown(mixed, GuardHealthRules.BaseHitsToKill));

            // ---- what was not asked for stays as it was: the dummy's and the PVP room's per-hit damage (the owner's numbers are for guards)
            Check("melee health: the training dummy's and the PVP room's damage per hit is untouched (crowbar 14, bat 11, hatchet 22)", Mathf.Approximately(specs[0].damage, 14f) && Mathf.Approximately(specs[1].damage, 11f) && Mathf.Approximately(specs[2].damage, 22f));
        }
    }
}
