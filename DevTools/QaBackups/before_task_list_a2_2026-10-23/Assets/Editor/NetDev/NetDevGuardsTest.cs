using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The pure rules of the combat model (the owner's queue of 2026-10-19): the shield tiers and what a bullet does through them, which guard carries what on which floor, how a guard's loadout is packed for the network, how an officer
        /// handles each gun, and which guns end a player's floor.
        /// </summary>
        static void GuardsTest()
        {
            var S = RangedWeapon.Specs;
            const RangedWeapon.Kind pewK = RangedWeapon.Kind.Pew, smgK = RangedWeapon.Kind.Smg, rifleK = RangedWeapon.Kind.Rifle, shotgunK = RangedWeapon.Kind.Shotgun, sniperK = RangedWeapon.Kind.Sniper;

            // ---- shields
            Check("shield: pistol and SMG guards wear a light one, the assault rifle a medium one, the shotgun a heavy one, the sniper none",
                GuardLoadouts.ShieldOf(pewK) == ShieldTier.Light && GuardLoadouts.ShieldOf(smgK) == ShieldTier.Light && GuardLoadouts.ShieldOf(rifleK) == ShieldTier.Medium
                && GuardLoadouts.ShieldOf(shotgunK) == ShieldTier.Heavy && GuardLoadouts.ShieldOf(sniperK) == ShieldTier.None);
            Check("shield: the heavier the shield the less of a bullet gets through (1, 0.75, 0.5, 0.3), and a piercing round ignores it",
                Mathf.Approximately(GuardLoadouts.BulletShare(ShieldTier.None), 1f) && Mathf.Approximately(GuardLoadouts.BulletShare(ShieldTier.Light), 0.75f) && Mathf.Approximately(GuardLoadouts.BulletShare(ShieldTier.Medium), 0.5f)
                && Mathf.Approximately(GuardLoadouts.BulletShare(ShieldTier.Heavy), 0.3f) && Mathf.Approximately(GuardLoadouts.DamageTaken(4f, ShieldTier.Heavy, true), 4f) && Mathf.Approximately(GuardLoadouts.DamageTaken(4f, ShieldTier.Medium), 2f));
            int B(RangedWeapon.Kind gun, ShieldTier tier, float health = 10f) => GuardLoadouts.BulletsToKill(health, S[(int)gun].guardHits, tier);
            Check("bullets: the pew needs 3 bullets for an unshielded guard, 4 through a light shield, 5 through a medium one (the vest of floor 4 was this), 9 through a heavy one",
                B(pewK, ShieldTier.None) == 3 && B(pewK, ShieldTier.Light) == 4 && B(pewK, ShieldTier.Medium) == 5 && B(pewK, ShieldTier.Heavy) == 9,
                $"({B(pewK, ShieldTier.None)}, {B(pewK, ShieldTier.Light)}, {B(pewK, ShieldTier.Medium)}, {B(pewK, ShieldTier.Heavy)})");
            Check("bullets: the sniper rifle needs 1 for an unshielded guard, 2 through a light or a medium shield, 4 through a heavy one; the rifle 3 / 5 (medium); the SMG 5 / 7 / 10 / 17",
                B(sniperK, ShieldTier.None) == 1 && B(sniperK, ShieldTier.Light) == 2 && B(sniperK, ShieldTier.Medium) == 2 && B(sniperK, ShieldTier.Heavy) == 4
                && B(rifleK, ShieldTier.None) == 3 && B(rifleK, ShieldTier.Medium) == 5
                && B(smgK, ShieldTier.None) == 5 && B(smgK, ShieldTier.Light) == 7 && B(smgK, ShieldTier.Medium) == 10 && B(smgK, ShieldTier.Heavy) == 17);
            Check("bullets: it takes a shotgun's pellets ten to knock out an unshielded guard and 34 through a heavy shield; the boss (30) takes 8 pew bullets",
                B(shotgunK, ShieldTier.None) == 10 && B(shotgunK, ShieldTier.Heavy) == 34 && B(pewK, ShieldTier.None, 30f) == 8);

            // ---- the guns that end a player's floor
            Check("elimination: only a shotgun and a sniper rifle end a player's floor, the pew, the SMG and the rifle only knock down",
                GuardLoadouts.Eliminates(shotgunK) && GuardLoadouts.Eliminates(sniperK) && !GuardLoadouts.Eliminates(pewK) && !GuardLoadouts.Eliminates(smgK) && !GuardLoadouts.Eliminates(rifleK));

            // ---- who carries what on which floor (the patrolling guards of a floor: 3 + floor, and two more from floor 3, twelve at most)
            void Counts(int floor, out int p, out int s, out int sh, out int r, out int sn) => GuardLoadouts.Counts(floor, GuardRules.PatrolCount(floor, 3), out p, out s, out sh, out r, out sn);
            bool none12 = true;
            for (int f = 1; f <= 2; f++) { Counts(f, out int p, out int s, out int sh, out int r, out int sn); none12 &= p + s + sh + r + sn == 0; }
            Counts(3, out int p3, out int s3, out int sh3, out int r3, out int sn3);
            Counts(4, out int p4, out int s4, out int sh4, out int r4, out int sn4);
            Counts(5, out int p5, out int s5, out int sh5, out int r5, out int sn5);
            Counts(6, out int p6, out int s6, out int sh6, out int r6, out int sn6);
            Counts(7, out int p7, out int s7, out int sh7, out int r7, out int sn7);
            Counts(8, out int p8, out int s8, out int sh8, out int r8, out int sn8);
            Counts(10, out int p10, out int s10, out int sh10, out int r10, out int sn10);
            Check("floors: nobody is armed on floors 1 and 2", none12);
            Check("floors: floor 3 and 4 (8 and 9 guards): only pistols, 2 and 3 of them", p3 == 2 && s3 + sh3 + r3 + sn3 == 0 && p4 == 3 && s4 + sh4 + r4 + sn4 == 0, $"(floor 3: {p3}, floor 4: {p4})");
            Check("floors: floor 5 (10 guards): the SMG joins the pistol, two of each", s5 == 2 && p5 == 2 && sh5 + r5 + sn5 == 0, $"(SMG {s5}, pistol {p5})");
            Check("floors: floor 6 and 7: one shotgun guard besides SMGs and pistols, no rifles", sh6 == 1 && s6 >= 1 && p6 >= 1 && r6 == 0 && sn6 == 0 && sh7 == 1 && s7 >= 1 && p7 >= 1 && r7 == 0 && sn7 == 0, $"(floor 6: {s6} SMG {p6} pistol {sh6} shotgun, floor 7: {s7} / {p7} / {sh7})");
            Check("floors: floor 8 on: only assault rifles and two shotguns - no pistols, no SMGs; no sniper before floor 10", sh8 == 2 && r8 >= 4 && p8 == 0 && s8 == 0 && sn8 == 0, $"(rifles {r8}, shotguns {sh8})");
            Check("floors: floor 10 on: exactly two snipers with the rifles (the most numerous of them) and the two shotguns", sn10 == 2 && sh10 == 2 && r10 >= 3 && r10 > sh10 && p10 == 0 && s10 == 0, $"(snipers {sn10}, shotguns {sh10}, rifles {r10})");
            float share = 0f; bool shareOk = true;
            for (int f = 1; f <= 13; f++) { float sh = GuardLoadouts.ArmedShare(f); shareOk &= sh >= share && sh <= 0.67f + 1e-4f; share = sh; }
            Check("floors: the armed share never falls from one floor to the next and is never more than two thirds", shareOk && GuardLoadouts.ArmedShare(2) == 0f && GuardLoadouts.ArmedShare(3) > 0f);

            // ---- the plan of a floor
            GuardLoadouts.Counts(10, 12, out _, out _, out _, out int r10of12, out _);                    // (the rifles of a floor of exactly twelve guards: floor 10 has 15 patrolling since 2026-10-23)
            var plan = GuardLoadouts.Plan(10, 12, new System.Random(7));
            var again = GuardLoadouts.Plan(10, 12, new System.Random(7));
            bool same = plan.Length == again.Length; for (int i = 0; same && i < plan.Length; i++) same &= plan[i] == again[i];
            Check("plan: floor 10 with 12 guards: 12 loadouts, two snipers with no shield, two shotguns with a heavy one, rifles with a medium one, the rest unarmed and unshielded",
                plan.Length == 12 && plan.Count(l => l.Armed && l.Kind == sniperK && l.Shield == ShieldTier.None) == 2 && plan.Count(l => l.Armed && l.Kind == shotgunK && l.Shield == ShieldTier.Heavy) == 2
                && plan.Count(l => l.Armed && l.Kind == rifleK && l.Shield == ShieldTier.Medium) == r10of12 && plan.Count(l => !l.Armed && l.Shield == ShieldTier.None) == 12 - 2 - 2 - r10of12,
                $"({string.Join(", ", plan.Select(l => l.ToString()))})");
            Check("plan: the same rng gives the same floor", same);
            var pl1 = GuardLoadouts.Plan(2, 7, new System.Random(3));
            Check("plan: floors 1 and 2 are all unarmed and unshielded, and an empty floor has an empty plan", pl1.Length == 7 && pl1.All(l => l == GuardLoadout.Unarmed) && GuardLoadouts.Plan(5, 0, null).Length == 0);
            bool shuffled = false;
            for (int seed = 1; seed < 30 && !shuffled; seed++) { var a = GuardLoadouts.Plan(5, 10, new System.Random(seed)); shuffled = a[0].Armed != GuardLoadouts.Plan(5, 10, new System.Random(seed + 100))[0].Armed || a.Any(l => l.Armed) && !a.Take(4).All(l => l.Armed); }
            Check("plan: the armed guards are spread among the unarmed ones, not the first in the row", shuffled);

            // ---- one more officer (backup, the guard on duty)
            Check("roll: nobody is armed before floor 3; on floor 5 the dice decide - below the share (0.4) armed, above it not",
                GuardLoadouts.Roll(2, 0.0, 0.0) == GuardLoadout.Unarmed && GuardLoadouts.Roll(5, 0.39, 0.0).Armed && !GuardLoadouts.Roll(5, 0.41, 0.0).Armed);
            bool noSniper = true;
            for (int i = 0; i < 100; i++) { var l = GuardLoadouts.Roll(12, 0.0, i / 100.0); noSniper &= !(l.Armed && l.Kind == sniperK) && l.Armed; }
            Check("roll: a reinforcement on floor 12 is armed with a rifle or a shotgun, never a sniper rifle", noSniper && GuardLoadouts.Roll(10, 0.0, 0.0).Kind == shotgunK && GuardLoadouts.Roll(10, 0.0, 0.999).Kind == rifleK);

            // ---- the loadout in the snapshot's kind byte
            bool packs = true;
            foreach (RangedWeapon.Kind k in new[] { pewK, smgK, rifleK, shotgunK, sniperK })
                foreach (ShieldTier t in new[] { ShieldTier.None, ShieldTier.Light, ShieldTier.Medium, ShieldTier.Heavy })
                    foreach (bool armed in new[] { true, false })
                    {
                        var l = new GuardLoadout(armed, k, t);
                        byte b = l.Pack();
                        packs &= b != 0 && (b & 1) == 1 && GuardLoadout.Unpack(b, armed) == l;
                    }
            Check("loadout: a guard's gun kind and shield pack into one byte that is never 0 and unpack to the same loadout", packs && GuardLoadout.Unarmed.Pack() != 0);

            // ---- how an officer handles his gun
            var tPew = GuardLoadouts.Tuning(pewK); var tSmg = GuardLoadouts.Tuning(smgK); var tRifle = GuardLoadouts.Tuning(rifleK); var tShotgun = GuardLoadouts.Tuning(shotgunK); var tSniper = GuardLoadouts.Tuning(sniperK);
            Check("tuning: the SMG and the rifle fire bursts with a pause between them; the pistol, the shotgun and the sniper rifle single shots",
                tSmg.burst > 1 && tSmg.burstPause > 0f && tRifle.burst > 1 && tRifle.burstPause > 0f && tPew.burst == 1 && tShotgun.burst == 1 && tSniper.burst == 1);
            Check("tuning: the sniper takes longest to aim, then the shotgun; they fire slowest; the shotgun works only close (10 m), the sniper rifle from far but no further than an officer sees (34 m)",
                tSniper.aimSeconds > tShotgun.aimSeconds && tShotgun.aimSeconds > tPew.aimSeconds && tSniper.fireInterval > tShotgun.fireInterval && tShotgun.fireInterval > tPew.fireInterval
                && tShotgun.engageMax <= 10f && tSniper.engageMax > tRifle.engageMax && tSniper.engageMax <= 34f);
            bool sane = true;
            foreach (var t in new[] { tPew, tSmg, tRifle, tShotgun, tSniper }) sane &= t.engageMin > 0f && t.engageMin < t.engageMax && t.magazine > 0 && t.reloadSeconds > 1f && t.aimSeconds > 0.2f && t.fireInterval > 0.05f && t.stumbleSeconds > 0.5f;
            Check("tuning: every gun has a real range, magazine, aim, rate, reload and stumble", sane);
            Check("drops: a guard drops one magazine of his gun's ammunition", GuardLoadouts.DropRounds(pewK) == S[(int)pewK].magazine && GuardLoadouts.DropRounds(smgK) == 24 && GuardLoadouts.DropRounds(rifleK) == 30 && GuardLoadouts.DropRounds(shotgunK) == 6 && GuardLoadouts.DropRounds(sniperK) == 5);
            Check("armed: the guards of the game are armed from floor 3 (it was 4)", ArmedGuards.DefaultFirstArmedFloor == 3 && GuardLoadouts.FirstArmedFloor == 3 && OutpostLoot.PewFloor == 3);
            Check("guard starts: the route seed is the same for the same run and floor, differs from floor to floor and from run to run, and an online run's guard seed follows the host's seed and is never 0",
                GuardRules.RouteSeed(1234, 3) == GuardRules.RouteSeed(1234, 3) && GuardRules.RouteSeed(1234, 3) != GuardRules.RouteSeed(1234, 4) && GuardRules.RouteSeed(1234, 3) != GuardRules.RouteSeed(1235, 3)
                && GuardRules.OnlineGuardSeed(77) == GuardRules.OnlineGuardSeed(77) && GuardRules.OnlineGuardSeed(77) != GuardRules.OnlineGuardSeed(78) && GuardRules.OnlineGuardSeed(0x3C6EF372) != 0);
            {
                var firstDraws = new System.Collections.Generic.HashSet<int>();
                for (int runSeed = 1; runSeed <= 200; runSeed++) firstDraws.Add(new System.Random(GuardRules.RouteSeed(runSeed, 2)).Next(10000));
                Check("guard starts: 200 different runs give about 200 different first picks for a floor's routes (a restarted run starts the guards elsewhere)", firstDraws.Count >= 190, $"({firstDraws.Count} different of 200)");
            }
        }
    }
}
