using System;
using System.Linq;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch A2: the guard that needs 2 seconds of contact to pin (T4), ammunition that is bullets with no limit (T24), the 0 to 3 bullets an eliminated guard drops (T25),
    /// the reload ring (T27) and a second set of armour (T28). Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half is <c>NetDevAsks1023B.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023BTest()
        {
            // ---- T4: the grab clock (a guard must hold on to the player for 1 second before he pins him)
            {
                float t = 0f; int steps = 0;
                while (!CustodyRules.GrabDone(t) && steps < 1000) { t = CustodyRules.GrabAfter(t, true, 0.02f); steps++; }
                Check("grab (T4): a guard needs 1 second in reach to pin: the clock is full after 50 steps of 0.02 s and not after 49", CustodyRules.GrabSeconds == 1f && steps == 50);
                Check("grab (T4): 0.9 s in reach is not enough, 1 s is", !CustodyRules.GrabDone(CustodyRules.GrabAfter(0f, true, 0.9f)) && CustodyRules.GrabDone(CustodyRules.GrabAfter(0f, true, 1f)));
                Check("grab (T4): out of reach the clock drains twice as fast (0.3 s away takes 0.6 s off), never below 0", Math.Abs(CustodyRules.GrabAfter(0.8f, false, 0.3f) - 0.2f) < 1e-5f && CustodyRules.GrabAfter(0.3f, false, 5f) == 0f && CustodyRules.GrabAfter(0f, false, 1f) == 0f);
                Check("grab (T4): the clock never goes above 1 s, and a negative step changes nothing", CustodyRules.GrabAfter(0.9f, true, 10f) == 1f && CustodyRules.GrabAfter(0.5f, true, -3f) == 0.5f);
                float held = CustodyRules.GrabAfter(0f, true, 0.75f);
                held = CustodyRules.GrabAfter(held, false, 0.2f);
                held = CustodyRules.GrabAfter(held, true, 0.2f);
                Check("grab (T4): 0.75 s in reach, 0.2 s away and 0.2 s back is 0.55 s: not pinned yet", Math.Abs(held - 0.55f) < 1e-4f && !CustodyRules.GrabDone(held));
            }

            // ---- T24: ammunition is bullets; a player carries as many as he can find or buy
            {
                var pew = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
                var smg = RangedWeapon.Specs[(int)RangedWeapon.Kind.Smg];
                var bought = RangedWeapon.NewMagazine(pew);
                int added = 0;
                for (int i = 0; i < 40; i++) added += bought.AddReserve(pew.magazine);
                Check("bullets (T24): the pistol's reserve has no upper limit: forty more magazines' worth are all taken", added == 40 * pew.magazine && bought.Reserve == 42 * pew.magazine);
                var found = RangedWeapon.NewMagazine(smg, 0);
                Check("bullets (T24): a gun that was found holds its one magazine and takes any number of bullets", found.Rounds == smg.magazine && found.Reserve == 0 && found.AddReserve(1000) == 1000 && found.Reserve == 1000);
                var endless = new GunMagazine(8, 2f, 0.3f);
                Check("bullets (T24): an officer's gun (an endless reserve) takes nothing", endless.InfiniteReserve && endless.AddReserve(8) == 0);
                Check("bullets (T24): the dealer's words for a gun's ammunition no longer say clip or magazine", RangedWeapon.Specs.All(s => !s.ammoPlural.ToLowerInvariant().Contains("clip") && !s.ammoPlural.ToLowerInvariant().Contains("magazine")));
            }

            // ---- T27: the reload's loads (the shotgun's counter) and the ring's dots
            {
                var m = new GunMagazine(8, 2f, 0.3f);
                m.SetReserve(20);
                m.TryFire(0f); m.TryFire(1f); m.TryFire(2f);
                bool started = m.StartReload(10f);
                Check("reload ring (T27): a reload of three missing rounds has 3 loads, and the counter steps 0, 1, 2, 3 over the reload",
                    started && m.ReloadLoads == 3 && m.LoadedSoFar(10f) == 0 && m.LoadedSoFar(10f + 2f * 0.34f) == 1 && m.LoadedSoFar(10f + 2f * 0.67f) == 2 && m.LoadedSoFar(12f) == 3);
                var low = new GunMagazine(8, 2f, 0.3f);
                low.SetReserve(2);
                for (int i = 0; i < 5; i++) low.TryFire(i);
                low.StartReload(20f);
                Check("reload ring (T27): with fewer bullets in the reserve than rounds missing the reload loads only those (2 of 5 missing)", low.ReloadLoads == 2 && low.LoadedSoFar(22f) == 2);
                var idle = new GunMagazine(8, 2f, 0.3f);
                Check("reload ring (T27): nothing is loading when no reload is going on", idle.ReloadLoads == 0 && idle.LoadedSoFar(5f) == 0);
                Check("reload ring (T27): the ring is 60 dots; none are lit at 0, half at 0.5, all at 1, and never more or fewer", HudWidgets.RingDots == 60 && HudWidgets.RingDotsLit(0f) == 0 && HudWidgets.RingDotsLit(0.5f) == 30 && HudWidgets.RingDotsLit(1f) == 60 && HudWidgets.RingDotsLit(-1f) == 0 && HudWidgets.RingDotsLit(2f) == 60);
            }

            // ---- T25: an eliminated guard drops 0 to 3 bullets, 0 the most likely and 3 the least likely
            {
                var p = GuardDropRules.Percent;
                Check("guard drops (T25): the odds of 0, 1, 2 and 3 bullets fall in that order (0 the likeliest, 3 the least likely) and add up to a hundred", p.Length == 4 && p[0] > p[1] && p[1] > p[2] && p[2] > p[3] && p[3] > 0 && p.Sum() == 100);
                var count = new int[4];
                for (int roll = 0; roll < 100; roll++) count[GuardDropRules.Bullets(roll)]++;
                Check("guard drops (T25): the hundred rolls give each count the share the odds say, and the edges are 39 -> 0, 40 -> 1, 69 -> 1, 70 -> 2, 89 -> 2, 90 -> 3",
                    count.SequenceEqual(p) && GuardDropRules.Bullets(39) == 0 && GuardDropRules.Bullets(40) == 1 && GuardDropRules.Bullets(69) == 1 && GuardDropRules.Bullets(70) == 2 && GuardDropRules.Bullets(89) == 2 && GuardDropRules.Bullets(90) == 3);
                Check("guard drops (T25): a roll outside 0..99 is clamped, and never more than 3 bullets", GuardDropRules.Bullets(-5) == 0 && GuardDropRules.Bullets(500) == 3 && GuardDropRules.MaxBullets == 3);
                Check("guard drops (T25): a player runs over the bullets within 0.9 m on the floor and 1.6 m up or down, and not farther",
                    GuardDropRules.RunsOver(0.5f, 1.0f) && GuardDropRules.RunsOver(0.9f, 1.6f) && GuardDropRules.RunsOver(0f, -1.6f) && !GuardDropRules.RunsOver(0.91f, 0f) && !GuardDropRules.RunsOver(0.2f, 1.7f) && !GuardDropRules.RunsOver(0.2f, -1.7f));
            }

            // ---- T28: a second set of armour, to bring to a teammate, very heavy in the pack
            {
                var L = ShieldTier.Light; var M = ShieldTier.Medium; var H = ShieldTier.Heavy; var N = ShieldTier.None;
                Check("armour (T28): a pack carries two pieces at most; a spare weighs 3.5 times its worn weight up to 12 kg (Light 8.75, Medium 12, Heavy 12, none 0)",
                    ArmourRules.MaxCarried == 2 && Math.Abs(ArmourRules.SpareWeight(L) - 8.75f) < 1e-4f && ArmourRules.SpareWeight(M) == 12f && ArmourRules.SpareWeight(H) == 12f && ArmourRules.SpareWeight(N) == 0f);
                Check("armour (T28): a spare piece is heavier than the same piece worn", new[] { L, M, H }.All(t => ArmourRules.SpareWeight(t) > ArmourRules.Weight(t)));
                Check("armour (T28): one piece weighs its normal weight, whichever way round the pair is given, and no piece weighs nothing",
                    ArmourRules.PackWeight(M, N) == ArmourRules.Weight(M) && ArmourRules.PackWeight(N, M) == ArmourRules.Weight(M) && ArmourRules.PackWeight(N, N) == 0f);
                Check("armour (T28): two pieces: the better one is worn at its weight and the other is the spare at its spare weight",
                    Math.Abs(ArmourRules.PackWeight(L, M) - (ArmourRules.Weight(M) + ArmourRules.SpareWeight(L))) < 1e-4f && ArmourRules.PackWeight(L, M) == ArmourRules.PackWeight(M, L) && Math.Abs(ArmourRules.PackWeight(H, H) - 20f) < 1e-4f);
                bool fits = true;
                foreach (var a in new[] { L, M, H }) foreach (var b in new[] { L, M, H }) fits &= ArmourRules.PackWeight(a, b) <= 20.0001f;
                Check("armour (T28): any two pieces fit an empty pack of 20 kg (the worst pair, two Heavy ones, is exactly 20 kg: no room left for loot)", fits);
            }
        }
    }
}
