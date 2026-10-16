using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Which guards carry the pew. In the game today nobody does: <see cref="FirstArmedFloor"/> is "no floor" until the owner picks the
    /// floor from which guards are armed (one number, here). The test menu (F1) can arm every guard on every floor meanwhile, and arm or
    /// disarm the guards of the floor on show at once. The rule itself is <see cref="ArmedRules"/>.
    /// </summary>
    public static class ArmedGuards
    {
        /// <summary>The first floor whose guards are armed; <see cref="ArmedRules.Never"/> = none (the game as it is today).</summary>
        public static int FirstArmedFloor = ArmedRules.Never;

        /// <summary>Test menu: every guard on every floor is armed (a offline game only: the test menu is off in a networked run).</summary>
        public static bool Everywhere;

#if UNITY_EDITOR
        /// <summary>The QA tests arm the guards of a networked run (the host) to check that the clients see them armed.</summary>
        public static bool ForceForTests;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            FirstArmedFloor = ArmedRules.Never;
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

        /// <summary>Give every live guard of this floor a pew (test menu).</summary>
        public static int ArmAllNow()
        {
            int n = 0;
            foreach (var g in GuardAI.All)
            {
                if (!g || g.Dead || g.IsPuppet || g.Armed) continue;
                g.gameObject.AddComponent<GuardGun>();
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
                gun.Remove();
                n++;
            }
            return n;
        }
    }
}
