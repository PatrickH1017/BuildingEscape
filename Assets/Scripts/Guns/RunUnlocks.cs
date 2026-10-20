using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What the party has unlocked in this run (the owner, 2026-10-19: "The Vendor should sell ammo for the specific guns after finding them" and, of the armour, "if the player who found the armour makes it back to the Loading Docks,
    /// everyone in the party should be able to buy the Light Armour from the Vendor"): a gun found on the floors and carried back to the Loading Docks unlocks that gun's ammunition at the dealer, for everybody. Whoever is in the docks
    /// with the gun in the pack counts; it does not have to be the player who found it, nor does the gun have to stay in the pack afterwards. The state belongs to the run: <see cref="Reset"/> clears it when a run starts. The host
    /// decides (<see cref="CollectFrom"/>, called by the docks' clock in HeistManager) and mirrors the mask to the clients (RunSync); the dealer's shop reads <see cref="Has"/>.
    /// </summary>
    public static class RunUnlocks
    {
        static int mask;

        /// <summary>One bit per <see cref="RangedWeapon.Kind"/>: the guns that were found and brought back.</summary>
        public static int Mask => mask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { mask = 0; }

        /// <summary>Has this gun been found and brought back to the docks in this run?</summary>
        public static bool Has(RangedWeapon.Kind kind) => (mask & (1 << (int)kind)) != 0;

        /// <summary>Unlock a gun. True when it was not unlocked before.</summary>
        public static bool Unlock(RangedWeapon.Kind kind)
        {
            int bit = 1 << (int)kind;
            if ((mask & bit) != 0) return false;
            mask |= bit;
            return true;
        }

        /// <summary>The bit of an armour tier (from bit 8: the guns have the first eight).</summary>
        static int ArmourBit(ShieldTier tier) => 1 << (8 + (int)tier);

        /// <summary>Has this tier of armour been found and brought back to the docks in this run (the dealer sells it to everybody then)?</summary>
        public static bool HasArmour(ShieldTier tier) => tier != ShieldTier.None && (mask & ArmourBit(tier)) != 0;

        /// <summary>Unlock a tier of armour. True when it was not unlocked before.</summary>
        public static bool UnlockArmour(ShieldTier tier)
        {
            if (tier == ShieldTier.None) return false;
            int bit = ArmourBit(tier);
            if ((mask & bit) != 0) return false;
            mask |= bit;
            return true;
        }

        /// <summary>A new run: nothing is unlocked.</summary>
        public static void Reset() { mask = 0; }

        /// <summary>A client: the host's mask.</summary>
        public static void NetSet(int hostMask) { mask = hostMask; }

        /// <summary>Unlocks every gun in this pack (the host, or offline). Returns how many were new.</summary>
        public static int CollectFrom(Backpack pack)
        {
            if (!pack) return 0;
            int added = 0;
            foreach (var item in pack.Items)
            {
                if (!item) continue;
                if (item.TryGetComponent(out RangedWeapon gun) && Unlock(gun.kind)) added++;
                else if (item.TryGetComponent(out ArmourItem armour) && UnlockArmour(armour.tier)) added++;      // (armour found and brought back: the dealer sells that tier to everybody, the owner, 2026-10-19)
            }
            return added;
        }
    }
}
