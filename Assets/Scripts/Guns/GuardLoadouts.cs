using System;

namespace BuildingEscape
{
    /// <summary>
    /// How well an officer is protected (the owner, 2026-10-19: "light shield", "medium shield", "heavy shield", "no shield"). A shield takes a share of every bullet's damage; it does nothing against a melee blow (a blow knocks an
    /// officer over, it never counts towards knocking him out) and an armour-piercing round ignores it (the Deagle's, to come). The picture is the vest he wears (<see cref="GuardArmour"/>).
    /// </summary>
    public enum ShieldTier : byte { None = 0, Light = 1, Medium = 2, Heavy = 3 }

    /// <summary>What an officer carries and wears: a gun of one kind (or none) and a shield.</summary>
    public readonly struct GuardLoadout : IEquatable<GuardLoadout>
    {
        public readonly bool Armed;
        public readonly RangedWeapon.Kind Kind;
        public readonly ShieldTier Shield;

        public GuardLoadout(bool armed, RangedWeapon.Kind kind, ShieldTier shield)
        {
            Armed = armed;
            Kind = armed ? kind : RangedWeapon.Kind.Pew;
            Shield = shield;
        }

        /// <summary>No gun, no shield: the toon officers of the first floors.</summary>
        public static readonly GuardLoadout Unarmed = new GuardLoadout(false, RangedWeapon.Kind.Pew, ShieldTier.None);

        /// <summary>An officer with this gun and the shield that goes with it (<see cref="GuardLoadouts.ShieldOf"/>).</summary>
        public static GuardLoadout Of(RangedWeapon.Kind kind) => new GuardLoadout(true, kind, GuardLoadouts.ShieldOf(kind));

        public bool Equals(GuardLoadout o) => Armed == o.Armed && Kind == o.Kind && Shield == o.Shield;
        public override bool Equals(object obj) => obj is GuardLoadout o && Equals(o);
        public override int GetHashCode() => (Armed ? 1 : 0) | ((int)Kind << 1) | ((int)Shield << 5);
        public static bool operator ==(GuardLoadout a, GuardLoadout b) => a.Equals(b);
        public static bool operator !=(GuardLoadout a, GuardLoadout b) => !a.Equals(b);
        public override string ToString() => Armed ? Kind + " + " + Shield + " shield" : (Shield == ShieldTier.None ? "unarmed" : "unarmed + " + Shield + " shield");

        /// <summary>One byte for the host's snapshot: bit 0 set (it is a guard), bits 1-2 the shield, bits 3-5 the kind of gun (whether he carries it is the snapshot's own flag).</summary>
        public byte Pack() => (byte)(1 | ((int)Shield << 1) | ((int)Kind << 3));

        public static GuardLoadout Unpack(byte packed, bool armed) =>
            new GuardLoadout(armed, (RangedWeapon.Kind)((packed >> 3) & 7), (ShieldTier)((packed >> 1) & 3));
    }

    /// <summary>How an armed officer handles his gun (every number a placeholder for the owner to tune, one table in <see cref="GuardLoadouts.Tuning"/>).</summary>
    public sealed class GuardGunTuning
    {
        /// <summary>Closer than <see cref="engageMin"/> he runs at the player to grab him instead; further than <see cref="engageMax"/> he keeps running until he is in range (flat metres).</summary>
        public float engageMin, engageMax;
        /// <summary>Seconds between getting a player in his sights and the first shot (and after every reload / burst).</summary>
        public float aimSeconds;
        /// <summary>Seconds between two shots of a burst, the pause after a burst, and the reload after the last round.</summary>
        public float fireInterval, burstPause, reloadSeconds;
        /// <summary>Rounds in a burst (1 = single shots) and in the magazine (an officer never runs out of spares).</summary>
        public int burst, magazine;
        /// <summary>The cone he fires into: this many degrees plus <see cref="spreadPerMetre"/> for every metre.</summary>
        public float baseSpread, spreadPerMetre;
        /// <summary>What a hit does to a player who is not blown up: how long the stumble lasts, how hard he is shoved (m/s).</summary>
        public float stumbleSeconds, knockback;
    }

    /// <summary>
    /// Who carries what on which floor: the owner's ladder of 2026-10-19 as pure rules, free of Unity types so they can be tested outside it. The floors are theirs (the first armed floor 3: pistols with a light shield; floor 5 the SMG with
    /// a light shield, besides the pistol guards; floor 6 the shotgun with a heavy shield, "very few of these shotgun guards on all floors above this one"; floor 8 the assault rifle with a medium shield, "only assault rifle guards and a
    /// few shotgun guards"; floor 10 the sniper with no shield). The shares and the counts are my placeholders ("unarmed guards and armed guards in the game": how many of each was not said).
    /// </summary>
    public static class GuardLoadouts
    {
        public const int FirstArmedFloor = 3, SmgFloor = 5, ShotgunFloor = 6, RifleFloor = 8, SniperFloor = 10;

        /// <summary>The shield a guard with this gun wears: pistol and SMG light, assault rifle medium, shotgun heavy, sniper none.</summary>
        public static ShieldTier ShieldOf(RangedWeapon.Kind kind)
        {
            switch (kind)
            {
                case RangedWeapon.Kind.Pew:
                case RangedWeapon.Kind.Smg: return ShieldTier.Light;
                case RangedWeapon.Kind.Rifle: return ShieldTier.Medium;
                case RangedWeapon.Kind.Shotgun: return ShieldTier.Heavy;
                default: return ShieldTier.None;
            }
        }

        /// <summary>The share of a bullet's damage that gets through a shield (the vest of floor 4 took half: that is the medium one).</summary>
        public static float BulletShare(ShieldTier tier)
        {
            switch (tier)
            {
                case ShieldTier.Light: return 0.75f;
                case ShieldTier.Medium: return 0.5f;
                case ShieldTier.Heavy: return 0.3f;
                default: return 1f;
            }
        }

        /// <summary>The damage (in the guard's hits: ten knock a normal officer out) a bullet of <paramref name="bulletHits"/> hits does to an officer wearing this shield; a piercing round does all of it.</summary>
        public static float DamageTaken(float bulletHits, ShieldTier tier, bool piercing = false) =>
            piercing ? bulletHits : bulletHits * BulletShare(tier);

        /// <summary>How many bullets it takes to knock out an officer with <paramref name="healthHits"/> hits to give, wearing this shield.</summary>
        public static int BulletsToKill(float healthHits, float bulletHits, ShieldTier tier, bool piercing = false)
        {
            float each = Math.Max(0.01f, DamageTaken(bulletHits, tier, piercing));
            return Math.Max(1, (int)Math.Ceiling(Math.Max(0.01f, healthHits) / each - 1e-4));
        }

        /// <summary>The owner's rule: only an officer with a shotgun or a sniper rifle does more than knock a player down - his hit ends that player's floor.</summary>
        public static bool Eliminates(RangedWeapon.Kind kind) => kind == RangedWeapon.Kind.Shotgun || kind == RangedWeapon.Kind.Sniper;

        /// <summary>The share of a floor's patrolling officers who are armed (a placeholder): none before floor 3, a quarter on it, half from floor 8, a little more from floor 10 (so that the assault rifles stay the most numerous with two snipers and two shotguns among them), two thirds from floor 12 (the chaos floor).</summary>
        public static float ArmedShare(int floor)
        {
            if (floor < FirstArmedFloor) return 0f;
            switch (floor)
            {
                case 3: return 0.25f;
                case 4: return 0.33f;
                case 5:
                case 6: return 0.40f;
                case 7: return 0.45f;
                case 8:
                case 9: return 0.50f;
                case 10:
                case 11: return 0.58f;
                default: return 0.67f;
            }
        }

        /// <summary>
        /// How many of <paramref name="guards"/> patrolling officers carry each gun on this floor. Floor 3 and 4: pistols. Floor 5: SMGs and pistols half and half. From floor 6: one shotgun guard (two from floor 8). From floor 8 the rest are all
        /// assault rifles (no more pistols or SMGs). From floor 10: exactly two snipers (my reading of "assault rifle guards, a few sniper guards, and two sniper guards").
        /// </summary>
        public static void Counts(int floor, int guards, out int pistol, out int smg, out int shotgun, out int rifle, out int sniper)
        {
            pistol = smg = shotgun = rifle = sniper = 0;
            if (floor < FirstArmedFloor || guards <= 0) return;
            int armed = Math.Min(guards, Math.Max(1, (int)Math.Round(guards * ArmedShare(floor), MidpointRounding.AwayFromZero)));
            if (floor >= SniperFloor) sniper = Math.Min(2, armed);
            int left = armed - sniper;
            if (floor >= ShotgunFloor) shotgun = Math.Min(floor >= RifleFloor ? 2 : 1, left);
            left -= shotgun;
            if (floor >= RifleFloor) rifle = left;
            else if (floor >= SmgFloor) { smg = (left + 1) / 2; pistol = left - smg; }
            else pistol = left;
        }

        /// <summary>The loadouts of a floor's patrolling officers, in a random order from <paramref name="rng"/> (the same rng gives the same floor). Unarmed officers carry nothing and wear no shield.</summary>
        public static GuardLoadout[] Plan(int floor, int guards, Random rng)
        {
            if (guards <= 0) return new GuardLoadout[0];
            var plan = new GuardLoadout[guards];
            for (int i = 0; i < guards; i++) plan[i] = GuardLoadout.Unarmed;
            Counts(floor, guards, out int pistol, out int smg, out int shotgun, out int rifle, out int sniper);
            int at = 0;
            at = Fill(plan, at, pistol, RangedWeapon.Kind.Pew);
            at = Fill(plan, at, smg, RangedWeapon.Kind.Smg);
            at = Fill(plan, at, shotgun, RangedWeapon.Kind.Shotgun);
            at = Fill(plan, at, rifle, RangedWeapon.Kind.Rifle);
            Fill(plan, at, sniper, RangedWeapon.Kind.Sniper);
            if (rng != null)
                for (int i = plan.Length - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    var t = plan[i]; plan[i] = plan[j]; plan[j] = t;
                }
            return plan;
        }

        static int Fill(GuardLoadout[] plan, int at, int count, RangedWeapon.Kind kind)
        {
            for (int i = 0; i < count && at < plan.Length; i++) plan[at++] = GuardLoadout.Of(kind);
            return at;
        }

        /// <summary>
        /// One officer who is not part of a floor's plan (backup, the guard on duty in an outpost): armed with the floor's armed share, and which gun by the weights of the floor's plan (never a sniper). Both rolls are from 0 up to (not including) 1.
        /// </summary>
        public static GuardLoadout Roll(int floor, double armedRoll01, double kindRoll01)
        {
            if (floor < FirstArmedFloor || armedRoll01 >= ArmedShare(floor)) return GuardLoadout.Unarmed;
            Counts(floor, 12, out int pistol, out int smg, out int shotgun, out int rifle, out _);
            int total = pistol + smg + shotgun + rifle;
            if (total <= 0) return GuardLoadout.Unarmed;
            double x = Math.Min(0.999999, Math.Max(0.0, kindRoll01)) * total;
            if (x < pistol) return GuardLoadout.Of(RangedWeapon.Kind.Pew);
            x -= pistol;
            if (x < smg) return GuardLoadout.Of(RangedWeapon.Kind.Smg);
            x -= smg;
            if (x < shotgun) return GuardLoadout.Of(RangedWeapon.Kind.Shotgun);
            return GuardLoadout.Of(RangedWeapon.Kind.Rifle);
        }

        /// <summary>How an officer handles a gun of this kind (placeholders): the SMG and the rifle fire bursts, the shotgun and the sniper rifle take their time and are the ones that end a player's floor.</summary>
        public static GuardGunTuning Tuning(RangedWeapon.Kind kind)
        {
            switch (kind)
            {
                case RangedWeapon.Kind.Smg:
                    return new GuardGunTuning { engageMin = 3f, engageMax = 18f, aimSeconds = 0.45f, fireInterval = 0.14f, burstPause = 1.1f, reloadSeconds = 2.4f, burst = 4, magazine = 24, baseSpread = 3.2f, spreadPerMetre = 0.35f, stumbleSeconds = 0.9f, knockback = 3.5f };
                case RangedWeapon.Kind.Rifle:
                    return new GuardGunTuning { engageMin = 3f, engageMax = 28f, aimSeconds = 0.6f, fireInterval = 0.2f, burstPause = 1.0f, reloadSeconds = 2.8f, burst = 3, magazine = 30, baseSpread = 1.8f, spreadPerMetre = 0.2f, stumbleSeconds = 1.4f, knockback = 6f };
                case RangedWeapon.Kind.Shotgun:
                    return new GuardGunTuning { engageMin = 2.5f, engageMax = 10f, aimSeconds = 0.9f, fireInterval = 1.7f, burstPause = 0f, reloadSeconds = 3.0f, burst = 1, magazine = 6, baseSpread = 0f, spreadPerMetre = 0f, stumbleSeconds = 1.8f, knockback = 7f };
                case RangedWeapon.Kind.Sniper:
                    return new GuardGunTuning { engageMin = 6f, engageMax = 34f, aimSeconds = 1.6f, fireInterval = 2.5f, burstPause = 0f, reloadSeconds = 3.4f, burst = 1, magazine = 5, baseSpread = 0.4f, spreadPerMetre = 0.02f, stumbleSeconds = 1.8f, knockback = 7f };
                default:
                    return new GuardGunTuning { engageMin = 3f, engageMax = 22f, aimSeconds = 0.5f, fireInterval = 0.85f, burstPause = 0f, reloadSeconds = 2.2f, burst = 1, magazine = 10, baseSpread = 2.5f, spreadPerMetre = 0.28f, stumbleSeconds = 1.8f, knockback = 7f };
            }
        }
    }
}
