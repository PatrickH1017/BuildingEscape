using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The pure rules of the armour (the owner's queue of 2026-10-19): the three tiers and the floors they are found on, what a piece weighs and costs, what it does to a guard's bullet, where it lies (a safe that is not the first of its floor holds it
        /// instead of a gun), and what bringing it back to the docks unlocks.
        /// </summary>
        static void ArmourTest()
        {
            const ShieldTier none = ShieldTier.None, light = ShieldTier.Light, medium = ShieldTier.Medium, heavy = ShieldTier.Heavy;

            // ---- the tiers
            Check("armour: Light Armour is found on floor 3, Medium on 7, Heavy on 10 - the owner's ladder - and nothing else is armour",
                ArmourRules.FirstFloor(light) == 3 && ArmourRules.FirstFloor(medium) == 7 && ArmourRules.FirstFloor(heavy) == 10 && ArmourRules.NewTierOn(3) == light && ArmourRules.NewTierOn(7) == medium && ArmourRules.NewTierOn(10) == heavy
                && ArmourRules.NewTierOn(4) == none && ArmourRules.NewTierOn(12) == none && !ArmourRules.IsArmour(none) && ArmourRules.IsArmour(light));
            Check("armour: the names are Light Armour, Medium Armour and Heavy Armour; the heavier the piece the more it weighs and the more it costs",
                ArmourRules.Name(light) == "Light Armour" && ArmourRules.Name(medium) == "Medium Armour" && ArmourRules.Name(heavy) == "Heavy Armour"
                && ArmourRules.Weight(light) < ArmourRules.Weight(medium) && ArmourRules.Weight(medium) < ArmourRules.Weight(heavy) && ArmourRules.Price(light) < ArmourRules.Price(medium) && ArmourRules.Price(medium) < ArmourRules.Price(heavy)
                && ArmourRules.Weight(heavy) < 12f);
            Check("armour: a guard's bullet knocks the wearer down for 75 % / 50 % / 30 % of the time (the shares a shield takes of a bullet), none for 100 %",
                Mathf.Approximately(ArmourRules.HitShare(none), 1f) && Mathf.Approximately(ArmourRules.HitShare(light), 0.75f) && Mathf.Approximately(ArmourRules.HitShare(medium), 0.5f) && Mathf.Approximately(ArmourRules.HitShare(heavy), 0.3f)
                && Mathf.Approximately(ArmourRules.StumbleSeconds(1.8f, heavy), 0.54f) && Mathf.Approximately(ArmourRules.StumbleSeconds(1.8f, none), 1.8f) && ArmourRules.Better(light, heavy) == heavy && ArmourRules.Better(medium, none) == medium);

            // ---- the numbers of the items in a safe
            Check("armour: in a safe the pieces are the numbers 21, 22 and 23 and round-trip; they are no guns, and 20 and 24 are not armour",
                SafeRules.ArmourItemNumber(light) == 21 && SafeRules.ArmourItemNumber(medium) == 22 && SafeRules.ArmourItemNumber(heavy) == 23 && SafeRules.IsArmourItem(21) && SafeRules.IsArmourItem(23) && !SafeRules.IsArmourItem(20) && !SafeRules.IsArmourItem(24)
                && !SafeRules.IsGunItem(21) && SafeRules.ArmourOfItem(22) == medium && !SafeRules.IsArmourItem(SafeRules.PewItem));

            // ---- which tier is likely where
            var w3 = ArmourRules.TierWeights(3); var w7 = ArmourRules.TierWeights(7); var w8 = ArmourRules.TierWeights(8); var w10 = ArmourRules.TierWeights(10); var w12 = ArmourRules.TierWeights(12);
            Check("armour: no armour before floor 3; floors 3-6 hold Light only, floor 7 Light and Medium (Medium three times as likely), floor 8-9 the same weight each, floor 10 all three (Heavy three times as likely), later all three alike",
                ArmourRules.TierWeights(2).Sum() == 0 && w3[1] > 0 && w3[2] == 0 && w3[3] == 0 && ArmourRules.TierWeights(6)[2] == 0 && w7[1] == 1 && w7[2] == 3 && w7[3] == 0 && w8[1] == w8[2] && w8[3] == 0 && w10[1] == 1 && w10[2] == 1 && w10[3] == 3 && w12[1] == w12[2] && w12[2] == w12[3]);
            Check("armour: the chance for a safe to hold it is 50 % on the floors where a tier is first found (3, 7, 10), 25 % on the others from floor 3, none before",
                ArmourRules.SafePercent(2) == 0 && ArmourRules.SafePercent(3) == 50 && ArmourRules.SafePercent(7) == 50 && ArmourRules.SafePercent(10) == 50 && ArmourRules.SafePercent(4) == 25 && ArmourRules.SafePercent(9) == 25 && ArmourRules.SafePercent(12) == 25);

            // ---- what the safes hold, over many runs
            int firstHas = 0; bool deterministic = true, oneEach = true, noClip = true, available = true, clipsOk = true;
            int eligible3 = 0, armour3 = 0, eligible4 = 0, armour4 = 0, anyLight = 0, anyMedium = 0, anyHeavy = 0;
            bool lightOnly3to6 = true, mediumNotBefore7 = true, heavyNotBefore10 = true;
            for (int sd = 0; sd < 300; sd++)
                for (int f = 1; f <= 12; f++)
                {
                    int rs = sd * 53 + 11, n = SafeRules.SafeCount(rs, f);
                    for (int i = 0; i < n; i++)
                    {
                        var a = SafeRules.WeaponContents(rs, f, i); var b = SafeRules.WeaponContents(rs, f, i);
                        if (f < 3) continue;
                        oneEach &= a.Length <= 1;
                        deterministic &= a.Length == b.Length && (a.Length == 0 || a[0] == b[0]) && SafeRules.HoldsArmour(rs, f, i) == SafeRules.HoldsArmour(rs, f, i);
                        bool arm = a.Length > 0 && SafeRules.IsArmourItem(a[0]);
                        if (i == 0 && arm) firstHas++;          // (no safe is sure to hold a gun any more, so the first safe of a floor may hold armour too)
                        clipsOk &= arm == SafeRules.HoldsArmour(rs, f, i);
                        if (i > 0 && f == 3) { eligible3++; if (arm) armour3++; }
                        if (i > 0 && f == 4) { eligible4++; if (arm) armour4++; }
                        if (arm)
                        {
                            var t = SafeRules.ArmourOfItem(a[0]);
                            available &= f >= ArmourRules.FirstFloor(t);
                            noClip &= !SafeRules.HasSideClip(rs, f, i);
                            if (f <= 6) lightOnly3to6 &= t == light;
                            if (f < 7) mediumNotBefore7 &= t != medium;
                            if (f < 10) heavyNotBefore10 &= t != heavy;
                            if (t == light) anyLight++; else if (t == medium) anyMedium++; else anyHeavy++;
                        }
                    }
                }
            Check("armour: a safe holds at most one thing (a gun, a piece of armour or nothing), the same on every machine, any safe may hold armour (the first of a floor too), no armour before its floor, and none with a spare pistol clip",
                oneEach && deterministic && firstHas > 0 && clipsOk && available && noClip && lightOnly3to6 && mediumNotBefore7 && heavyNotBefore10, $"(one each {oneEach}, deterministic {deterministic}, armour in a first safe {firstHas} times, available {available}, no clip {noClip}, light only on 3-6 {lightOnly3to6}, no medium before 7 {mediumNotBefore7}, no heavy before 10 {heavyNotBefore10})");
            float p3 = eligible3 > 0 ? armour3 / (float)eligible3 : 0f, p4 = eligible4 > 0 ? armour4 / (float)eligible4 : 0f;
            Check("armour: over 300 runs the second safe of floor 3 holds Light Armour about half the time and the other safes of floor 4 about a quarter of the time, and every tier turns up",
                p3 > 0.40f && p3 < 0.60f && p4 > 0.17f && p4 < 0.33f && anyLight > 0 && anyMedium > 0 && anyHeavy > 0, $"(floor 3: {armour3} of {eligible3} = {p3:0.00}, floor 4: {armour4} of {eligible4} = {p4:0.00}; light {anyLight}, medium {anyMedium}, heavy {anyHeavy})");

            // ---- what bringing a piece back to the docks unlocks (the pack itself is looked at in the Play Mode scenario)
            RunUnlocks.Reset();
            bool fresh = !RunUnlocks.HasArmour(light) && !RunUnlocks.HasArmour(medium) && !RunUnlocks.HasArmour(heavy) && !RunUnlocks.HasArmour(none) && RunUnlocks.Mask == 0;
            bool first = RunUnlocks.UnlockArmour(medium), again = RunUnlocks.UnlockArmour(medium);
            bool only = RunUnlocks.HasArmour(medium) && !RunUnlocks.HasArmour(light) && !RunUnlocks.HasArmour(heavy);
            RunUnlocks.Unlock(RangedWeapon.Kind.Sniper); RunUnlocks.UnlockArmour(heavy);
            bool bits = RunUnlocks.Mask == ((1 << (int)RangedWeapon.Kind.Sniper) | (1 << (8 + (int)medium)) | (1 << (8 + (int)heavy))) && RunUnlocks.Has(RangedWeapon.Kind.Sniper) && !RunUnlocks.Has(RangedWeapon.Kind.Pew) && !RunUnlocks.UnlockArmour(none);
            RunUnlocks.NetSet(RunUnlocks.Mask);
            bool mirrored = RunUnlocks.HasArmour(heavy);
            RunUnlocks.Reset();
            Check("armour: nothing is unlocked at the start; unlocking a tier unlocks that tier only (true once, then false); the guns' bits and the armour's do not meet", fresh && first && !again && only && bits && mirrored && !RunUnlocks.HasArmour(medium) && RunUnlocks.Mask == 0);

            // ---- the dealer
            int Index(ShieldTier t) => System.Array.FindIndex(Vendor.Catalog, p => p.isArmour && p.armour == t);
            int il = Index(light), im = Index(medium), ih = Index(heavy);
            RunUnlocks.Reset();
            bool offBefore = !Vendor.OnOffer(il, 5) && !Vendor.OnOffer(im, 5) && !Vendor.OnOffer(ih, 5);
            RunUnlocks.UnlockArmour(medium);
            bool offAfter = Vendor.OnOffer(im, 1) && !Vendor.OnOffer(il, 1) && !Vendor.OnOffer(ih, 1);
            RunUnlocks.Reset();
            Check("armour: the dealer has the three pieces at the end of his list (before the walkie talkie) - priced by the rules - and sells a tier only once it was brought back to the docks (whatever floor the elevator goes to next)",
                il >= 0 && im == il + 1 && ih == im + 1 && ih == Vendor.Catalog.Length - 2 && Vendor.Catalog[Vendor.Catalog.Length - 1].isWalkie && Vendor.Catalog[il].name == "Light Armour" && Vendor.Catalog[im].name == "Medium Armour" && Vendor.Catalog[ih].name == "Heavy Armour"
                && Vendor.Catalog[il].price == ArmourRules.Price(light) && Vendor.Catalog[im].price == ArmourRules.Price(medium) && Vendor.Catalog[ih].price == ArmourRules.Price(heavy) && offBefore && offAfter);

            // ---- vertical safes (the owner, 2026-10-19): a safe that holds a shotgun or a sniper rifle
            bool tallOk = true; int tallSeen = 0, flatSeen = 0;
            for (int sd = 0; sd < 150; sd++)
                for (int f = 1; f <= 12; f++)
                {
                    int rs = sd * 61 + 7, n = SafeRules.SafeCount(rs, f);
                    for (int i = 0; i < n; i++)
                    {
                        var items = SafeRules.WeaponContents(rs, f, i);
                        bool gunTall = items.Length > 0 && SafeRules.IsGunItem(items[0]) && (SafeRules.GunOfItem(items[0]) == RangedWeapon.Kind.Shotgun || SafeRules.GunOfItem(items[0]) == RangedWeapon.Kind.Sniper);
                        tallOk &= SafeRules.HoldsTallGun(rs, f, i) == gunTall;
                        if (gunTall) tallSeen++; else flatSeen++;
                    }
                }
            Check("safes: a safe is the vertical kind exactly when it holds a shotgun or a sniper rifle (never with armour, a pistol, an SMG or a rifle; never on floors 1-2, nor before floor 6)", tallOk && tallSeen > 0 && flatSeen > tallSeen && !SafeRules.HoldsTallGun(1, 2, 0) && !SafeRules.HoldsTallGun(5, 4, 0) && !SafeRules.HoldsTallGun(5, 5, 0), $"({tallSeen} vertical, {flatSeen} not)");
        }
    }
}
