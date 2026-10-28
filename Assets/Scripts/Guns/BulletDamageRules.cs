using System;

namespace BuildingEscape
{
    /// <summary>The four kinds of ammunition (the owner, 2026-10-27): the pistol and the sub machine gun fire light ammo, the rifle medium, the sniper rifle heavy, the shotgun its shells.</summary>
    public enum AmmoClass { Light, Medium, Heavy, Shotgun }

    /// <summary>What a bullet does to what it hits: the shot's feedback for the shooter (the hit marker and its sound).</summary>
    public enum HitKind { None = 0, Hit = 1, Head = 2, Kill = 3 }

    /// <summary>
    /// What a bullet does to an officer (the owner, 2026-10-27: "Bullets should now do a set amount of damage ... pistols and sub have light ammo, rifle is medium ammo, the sniper is heavy ammo, and the shotgun is shotgun shells. Light ammo 25 per hit (40 per headshot), medium
    /// ammo 40 per hit (60 per headshot), heavy ammo 100 per hit, shotgun ammo 75 damage if within range. Headshots should deal more damage than body shots"), without Unity types so it is tested outside Unity. Damage is in HP (an officer has 100: <see cref="GuardHealthRules"/>).
    /// THE OWNER'S NUMBERS are light 25 / 40, medium 40 / 60, heavy 100 and the shotgun's 75; the heavy and shotgun HEADSHOT numbers are mine (he gave none and said a headshot does more than a body shot): <see cref="HeavyHead"/>, <see cref="ShotgunHead"/>.
    /// An officer's shield still takes its share of what is left (<see cref="GuardLoadouts.DamageTaken"/>): unchanged.
    /// </summary>
    public static class BulletDamageRules
    {
        public const float LightBody = 25f, LightHead = 40f;
        public const float MediumBody = 40f, MediumHead = 60f;
        public const float HeavyBody = 100f, HeavyHead = 150f;                    // (the head number is a PLACEHOLDER: the owner gave 100 per hit only)
        public const float ShotgunBody = 75f, ShotgunHead = 100f;                 // (the head number is a PLACEHOLDER: the owner gave 75 only)

        /// <summary>A hit above this share of the body's height (counted from the feet) is a headshot: the top fifth, the head and the neck (a PLACEHOLDER: the owner said nothing about where the head is).</summary>
        public const float HeadFrom = 0.80f;

        public static AmmoClass ClassOf(RangedWeapon.Kind kind)
        {
            switch (kind)
            {
                case RangedWeapon.Kind.Rifle: return AmmoClass.Medium;
                case RangedWeapon.Kind.Sniper: return AmmoClass.Heavy;
                case RangedWeapon.Kind.Shotgun: return AmmoClass.Shotgun;
                default: return AmmoClass.Light;                                   // (the pew and the sub machine gun)
            }
        }

        /// <summary>The HP one bullet (a shotgun shot) takes off an unshielded officer.</summary>
        public static float Damage(AmmoClass ammo, bool head)
        {
            switch (ammo)
            {
                case AmmoClass.Medium: return head ? MediumHead : MediumBody;
                case AmmoClass.Heavy: return head ? HeavyHead : HeavyBody;
                case AmmoClass.Shotgun: return head ? ShotgunHead : ShotgunBody;
                default: return head ? LightHead : LightBody;
            }
        }

        public static float Damage(RangedWeapon.Kind kind, bool head) => Damage(ClassOf(kind), head);

        /// <summary>The same in the officers' own unit, hits of 10 HP (<see cref="GuardAI.BulletHitHost"/>).</summary>
        public static float Hits(RangedWeapon.Kind kind, bool head) => GuardHealthRules.HpToHits(Damage(kind, head));

        /// <summary>Did a bullet that hit at height <paramref name="hitY"/> hit the head? The body stands from <paramref name="feetY"/> and is <paramref name="height"/> tall.</summary>
        public static bool IsHeadshot(float hitY, float feetY, float height) => height > 0f && hitY >= feetY + height * HeadFrom;

        /// <summary>What the shooter is told: a headshot, a plain hit or the elimination (an elimination wins over a headshot).</summary>
        public static HitKind Feedback(bool killed, bool head) => killed ? HitKind.Kill : head ? HitKind.Head : HitKind.Hit;

        /// <summary>The bigger of two feedbacks (a shotgun shot is many pellets: the shooter hears the most that happened).</summary>
        public static HitKind Stronger(HitKind a, HitKind b) => (int)a >= (int)b ? a : b;

        /// <summary>How many bullets (shots) put an unshielded officer with <paramref name="hp"/> HP down, every one a body shot (<paramref name="head"/> false) or every one a headshot.</summary>
        public static int ShotsToKill(RangedWeapon.Kind kind, float hp, bool head) => Math.Max(1, (int)Math.Ceiling(Math.Max(0.01f, hp) / Damage(kind, head) - 1e-4));

        public static string ClassName(AmmoClass ammo)
        {
            switch (ammo)
            {
                case AmmoClass.Medium: return "Medium ammo";
                case AmmoClass.Heavy: return "Heavy ammo";
                case AmmoClass.Shotgun: return "Shotgun shells";
                default: return "Light ammo";
            }
        }
    }

    /// <summary>
    /// One shot's bookkeeping, so that a shotgun shot (eight pellets) deals its damage ONCE to each officer (the owner: "shotgun ammo 75 damage if within range"): first every pellet's impact is noted (<see cref="Note"/>: a pellet in the head makes the officer's share a headshot), then the damage is
    /// taken once for each (<see cref="Take"/>). For a gun with a single bullet it changes nothing. Targets are any objects (officers, the training dummy).
    /// </summary>
    public sealed class ShotTally
    {
        readonly object[] keys = new object[16];
        readonly bool[] heads = new bool[16];
        readonly bool[] taken = new bool[16];
        int n;

        public void Reset() { Array.Clear(keys, 0, n); n = 0; }

        int IndexOf(object target)
        {
            for (int i = 0; i < n; i++) if (ReferenceEquals(keys[i], target)) return i;
            return -1;
        }

        int Add(object target)
        {
            if (n >= keys.Length) return -1;
            keys[n] = target; heads[n] = false; taken[n] = false;
            return n++;
        }

        /// <summary>A pellet of this shot hit <paramref name="target"/> (in the head or not).</summary>
        public void Note(object target, bool head)
        {
            int i = IndexOf(target);
            if (i < 0) i = Add(target);
            if (i >= 0 && head) heads[i] = true;
        }

        /// <summary>Is this the first time the shot's damage is dealt to <paramref name="target"/>? <paramref name="head"/>: any pellet of the shot hit him in the head.</summary>
        public bool Take(object target, out bool head)
        {
            int i = IndexOf(target);
            if (i < 0) i = Add(target);
            if (i < 0) { head = false; return true; }
            head = heads[i];
            if (taken[i]) return false;
            taken[i] = true;
            return true;
        }

        public bool HeadOf(object target) { int i = IndexOf(target); return i >= 0 && heads[i]; }
    }
}
