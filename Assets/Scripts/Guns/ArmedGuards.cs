using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Which guards carry the pew: every guard made on <see cref="DefaultFirstArmedFloor"/> (floor 4, the owner's choice, 2026-10-16) and
    /// above, backup and the guard on duty in an outpost included (one number, here). The pew itself lies in the outpost of that floor
    /// (<see cref="OutpostLoot.PewFloor"/>), so the players can answer. The test menu (F1) can arm every guard on every floor, change the
    /// floor, and arm or disarm the guards of the floor on show at once. The rule itself is <see cref="ArmedRules"/>.
    /// </summary>
    public static class ArmedGuards
    {
        /// <summary>The floor from which guards are armed in the game: 3 (the owner, 2026-10-19: "The first level(3rd floor) of armed guards should have pistols with light shield"; it was 4 until then).</summary>
        public const int DefaultFirstArmedFloor = GuardLoadouts.FirstArmedFloor;

        /// <summary>The first floor whose guards are armed; <see cref="ArmedRules.Never"/> = none (the test menu can set it).</summary>
        public static int FirstArmedFloor = DefaultFirstArmedFloor;

        /// <summary>Test menu: every guard on every floor is armed (a offline game only: the test menu is off in a networked run).</summary>
        public static bool Everywhere;

#if UNITY_EDITOR
        /// <summary>The QA tests arm the guards of a networked run (the host) to check that the clients see them armed.</summary>
        public static bool ForceForTests;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FirstArmedFloor = DefaultFirstArmedFloor;
            Everywhere = false;
#if UNITY_EDITOR
            ForceForTests = false;
#endif
        }

        /// <summary>Should a guard made on this floor carry a gun? (A remote client's guards are puppets: the host decides and tells it.)</summary>
        public static bool ShouldArm(int floor)
        {
            if (Net.NetRole.IsRemoteClient) return false;
            bool everywhere = Everywhere && !Net.NetSession.Online;
#if UNITY_EDITOR
            everywhere |= ForceForTests;
#endif
            return ArmedRules.IsArmed(floor, FirstArmedFloor, everywhere);
        }

        /// <summary>
        /// Is the old rule in force instead of the floors' loadouts (<see cref="GuardLoadouts"/>)? Yes when a test or the test menu changed the first armed floor or armed everybody: then every guard of an armed floor carries a pew
        /// and wears no shield, and nothing else.
        /// </summary>
        static bool LegacyRule()
        {
            bool legacy = (Everywhere && !Net.NetSession.Online) || FirstArmedFloor != DefaultFirstArmedFloor;
#if UNITY_EDITOR
            legacy |= ForceForTests;
#endif
            return legacy;
        }

        /// <summary>
        /// The loadouts of the <paramref name="guards"/> patrolling officers of a floor, in the order they are made (host / offline: a remote client's guards are puppets and get theirs from the host). Give it an rng of its own, so
        /// that the maze and the loot stay what they were; the same rng gives the same floor.
        /// </summary>
        public static GuardLoadout[] PlanFor(int floor, int guards, System.Random rng)
        {
            var plan = new GuardLoadout[Mathf.Max(0, guards)];
            if (plan.Length == 0 || Net.NetRole.IsRemoteClient) return plan;               // (all unarmed: the default)
            if (LegacyRule())
            {
                var l = ShouldArm(floor) ? new GuardLoadout(true, RangedWeapon.Kind.Pew, ShieldTier.None) : GuardLoadout.Unarmed;
                for (int i = 0; i < plan.Length; i++) plan[i] = l;
                return plan;
            }
            return GuardLoadouts.Plan(floor, guards, rng);
        }

        /// <summary>One officer outside the plan - backup, or the guard on duty in an outpost: armed with the floor's share and a gun by the floor's weights. <paramref name="rng"/>: use one of its own while the floor is built (every machine builds it), none for a call at run time.</summary>
        public static GuardLoadout RollFor(int floor, System.Random rng = null)
        {
            if (Net.NetRole.IsRemoteClient) return GuardLoadout.Unarmed;
            if (LegacyRule()) return ShouldArm(floor) ? new GuardLoadout(true, RangedWeapon.Kind.Pew, ShieldTier.None) : GuardLoadout.Unarmed;
            return rng != null ? GuardLoadouts.Roll(floor, rng.NextDouble(), rng.NextDouble()) : GuardLoadouts.Roll(floor, Random.value, Random.value);
        }

        /// <summary>Give every live guard of this floor a pew (test menu).</summary>
        public static int ArmAllNow()
        {
            int n = 0;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.Dead || g.IsPuppet || g.Armed) continue;
                g.SetLoadout(new GuardLoadout(true, RangedWeapon.Kind.Pew, g.Shield));      // (a pew, and the shield he already wears)
                g.SyncGun();
                n++;
            }
            return n;
        }

        /// <summary>Test menu: every live guard of the floor carries this kind of gun with the shield that goes with it (<see cref="GuardLoadouts.ShieldOf"/>), or nothing when <paramref name="kind"/> is null. Returns how many were changed.</summary>
        public static int SetAllNow(RangedWeapon.Kind? kind)
        {
            int n = 0;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.Dead || g.IsPuppet || g.IsBoss) continue;
                g.SetLoadout(kind.HasValue ? GuardLoadout.Of(kind.Value) : GuardLoadout.Unarmed);
                g.SyncGun();
                n++;
            }
            return n;
        }

        /// <summary>Take the pew off every guard (test menu); guards made later are armed again only if a rule says so.</summary>
        public static int DisarmAll()
        {
            int n = 0;
            foreach (var g in GuardAI.All)
            {
                if (!g) continue;
                var gun = g.GetComponent<GuardGun>();
                if (!gun) continue;
                g.SetLoadout(new GuardLoadout(false, RangedWeapon.Kind.Pew, g.Shield));
                g.SyncGun();                                           // (takes the gun away)
                n++;
            }
            return n;
        }
    }
}
