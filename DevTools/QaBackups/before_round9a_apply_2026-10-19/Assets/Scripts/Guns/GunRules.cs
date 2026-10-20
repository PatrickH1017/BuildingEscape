using System;

namespace BuildingEscape
{
    /// <summary>
    /// The magazine of a gun: how many rounds are left, the pause between two shots, and the reload that has to follow an empty
    /// magazine. Plain C# (no Unity types) so it is tested outside Unity; the caller passes the time (Time.time), so the same rules run
    /// for the pistol a player holds and the one an armed guard carries.
    /// </summary>
    public sealed class GunMagazine
    {
        /// <summary>What pulling the trigger did.</summary>
        public enum Shot
        {
            /// <summary>A round left the gun (there are more in the magazine).</summary>
            Fired,
            /// <summary>The last round left the gun: the reload follows.</summary>
            FiredLast,
            /// <summary>The magazine is empty: a click, and the reload starts.</summary>
            Empty,
            /// <summary>The gun is being reloaded.</summary>
            Reloading,
            /// <summary>Too soon after the last shot.</summary>
            TooSoon,
        }

        /// <summary>What <see cref="Tick"/> noticed.</summary>
        public enum Event { None, ReloadStarted, ReloadFinished }

        public readonly int Capacity;
        public readonly float ReloadSeconds, FireInterval;
        /// <summary>When the last round has been fired, the reload starts by itself after this many seconds (the gun has to be reloaded after every magazine).</summary>
        public float AutoReloadDelay = 0.25f;

        /// <summary>An unlimited magazine: shots take no rounds (the basement: the owner, 2026-10-17, "unlimited ammo in the basement"). The fire interval still counts.</summary>
        public bool Unlimited;

        /// <summary>
        /// The rounds in the spare clips (the owner, 2026-10-18: "8 bullets per clip and 3 clips total"): a reload takes what it needs from them, and with none left an empty gun only clicks until more are
        /// bought. A gun that never runs short (an armed officer's) keeps the default, an endless reserve.
        /// </summary>
        public bool InfiniteReserve = true;
        public int Reserve { get; private set; }

        public int Rounds { get; private set; }
        public bool Reloading { get; private set; }
        public float ReloadStartedAt { get; private set; }
        public float ReloadEndsAt { get; private set; }
        public float NextShotAt { get; private set; }
        /// <summary>Rounds fired since the gun was made.</summary>
        public int Fired { get; private set; }
        /// <summary>Reloads finished since the gun was made.</summary>
        public int Reloads { get; private set; }

        public GunMagazine(int capacity, float reloadSeconds, float fireInterval)
        {
            Capacity = Math.Max(1, capacity);
            ReloadSeconds = Math.Max(0.05f, reloadSeconds);
            FireInterval = Math.Max(0.01f, fireInterval);
            Rounds = Capacity;
        }

        /// <summary>0..1: how far the reload has got (0 when none is going on).</summary>
        public float ReloadProgress(float now) =>
            !Reloading ? 0f : Math.Min(1f, Math.Max(0f, (now - ReloadStartedAt) / ReloadSeconds));

        /// <summary>Pull the trigger at time <paramref name="now"/>.</summary>
        public Shot TryFire(float now)
        {
            if (Reloading) return Shot.Reloading;
            if (Rounds <= 0 && !Unlimited) return Shot.Empty;
            if (now < NextShotAt) return Shot.TooSoon;
            if (!Unlimited) Rounds--;
            Fired++;
            NextShotAt = now + FireInterval;
            return Rounds == 0 ? Shot.FiredLast : Shot.Fired;
        }

        /// <summary>Start a reload (by hand, or because the trigger was pulled on an empty gun). False when it is full or already going.</summary>
        public bool StartReload(float now)
        {
            if (!CanReload) return false;
            Reloading = true;
            ReloadStartedAt = now;
            ReloadEndsAt = now + ReloadSeconds;
            return true;
        }

        /// <summary>Stop a reload that is going on (the gun was put away).</summary>
        public void CancelReload() { Reloading = false; }

        /// <summary>Call every frame: finishes a reload that is due, and starts the one an empty magazine needs.</summary>
        public Event Tick(float now)
        {
            if (Reloading)
            {
                if (now < ReloadEndsAt) return Event.None;
                Reloading = false;
                if (InfiniteReserve) Rounds = Capacity;
                else
                {
                    int take = Math.Min(Capacity - Rounds, Reserve);                 // (only what is missing: nothing is wasted)
                    Rounds += take;
                    Reserve -= take;
                }
                Reloads++;
                return Event.ReloadFinished;
            }
            if (Rounds <= 0 && now >= NextShotAt - FireInterval + AutoReloadDelay && StartReload(now)) return Event.ReloadStarted;
            return Event.None;
        }

        /// <summary>Fill the magazine at once (a test). The spare clips are not touched.</summary>
        public void Fill() { Rounds = Capacity; Reloading = false; }

        /// <summary>Can a reload start now: the gun is not full, not reloading already, and there is a clip to load (always, for an endless reserve)? Never in the basement, where the magazine is never short.</summary>
        public bool CanReload => !Reloading && Rounds < Capacity && !Unlimited && (InfiniteReserve || Reserve > 0);

        /// <summary>The magazine is empty and so is the reserve: the trigger only clicks until the dealer sells more.</summary>
        public bool OutOfAmmo => !Unlimited && !InfiniteReserve && Rounds <= 0 && Reserve <= 0 && !Reloading;

        /// <summary>Give the gun a real reserve of this many rounds (the player's pistol; a new one comes with two spare clips).</summary>
        public void SetReserve(int rounds) { InfiniteReserve = false; Reserve = Math.Max(0, rounds); }

        /// <summary>More rounds in the reserve (a clip from the dealer). Returns how many were added: none for a gun with an endless reserve.</summary>
        public int AddReserve(int rounds)
        {
            if (InfiniteReserve || rounds <= 0) return 0;
            Reserve += rounds;
            return rounds;
        }
    }

    /// <summary>The other pure rules of guns: where a shot with some spread goes, and which guards carry one.</summary>
    public static class GunRules
    {
        /// <summary>Ammunition is unlimited in the basement (the Loading Docks, floor 0); on every other floor a magazine is a magazine, and it is full again each time a floor starts.</summary>
        public static bool UnlimitedAmmo(int floor) => floor <= 0;

        /// <summary>
        /// A random direction inside a cone: the angles, in degrees, to turn the aim sideways (<paramref name="yaw"/>) and up
        /// (<paramref name="pitch"/>). Even over the disc, so a bullet is as likely to land anywhere inside the cone. The same seed always
        /// gives the same angles.
        /// </summary>
        public static void Spread(int seed, float coneDegrees, out float yaw, out float pitch)
        {
            var r = new SynthRandom(seed);
            float radius = coneDegrees * (float)Math.Sqrt(r.Value());
            float around = r.Value() * 6.2831853f;
            yaw = radius * (float)Math.Cos(around);
            pitch = radius * (float)Math.Sin(around);
        }

        /// <summary>How many spare clips these spare rounds make (a part of a clip counts as one): the "2 clips" on the screen.</summary>
        public static int SpareClips(int reserveRounds, int capacity) => capacity <= 0 ? 0 : (Math.Max(0, reserveRounds) + capacity - 1) / capacity;

        /// <summary>A guard is a worse shot the further away the player is: the cone he fires into at this distance.</summary>
        public static float GuardCone(float baseDegrees, float perMetreDegrees, float distance) =>
            baseDegrees + perMetreDegrees * Math.Max(0f, distance);

        /// <summary>
        /// How fast an armed officer goes for a player on this floor (the owner, 2026-10-18: "Officers with weapons should be more aggressive (faster to attack as the floors go up)"): the multipliers for the time he takes
        /// to aim, the pause between two shots and the reload. On the first armed floor they are a little under 1 (he was a bit slower before), on <see cref="ArmedRules.TopFloor"/> and above they are at their smallest.
        /// </summary>
        public static void GuardTempo(int floor, int firstArmedFloor, out float aim, out float shot, out float reload)
        {
            float t = (floor <= firstArmedFloor || ArmedRules.TopFloor <= firstArmedFloor) ? 0f : Math.Min(1f, (floor - firstArmedFloor) / (float)(ArmedRules.TopFloor - firstArmedFloor));
            aim = 0.8f + (0.3f - 0.8f) * t;
            shot = 0.9f + (0.55f - 0.9f) * t;
            reload = 0.95f + (0.7f - 0.95f) * t;
        }

        /// <summary>How many bullets knock a guard out: he goes down after <paramref name="hitsToKill"/> hits and a bullet counts as <paramref name="hitsPerBullet"/>.</summary>
        public static int BulletsToKill(int hitsToKill, int hitsPerBullet) =>
            (Math.Max(1, hitsToKill) + Math.Max(1, hitsPerBullet) - 1) / Math.Max(1, hitsPerBullet);

        /// <summary>
        /// Should an armed guard stand still and shoot (true), or keep running at the player (false)? He shoots when he sees a free player
        /// who is not too far to hit and not already within arm's reach (then he grabs him instead). While he reloads he stays put.
        /// </summary>
        public static bool ShouldStandAndShoot(bool seesTarget, float distance, float minRange, float maxRange, bool reloading) =>
            reloading || (seesTarget && distance >= minRange && distance <= maxRange);
    }

    /// <summary>Which guards carry a gun: those made on the first armed floor (4, see <see cref="ArmedGuards"/>) and above; the test menu can arm them everywhere.</summary>
    public static class ArmedRules
    {
        /// <summary>"No floor": nobody is armed by the floor rule.</summary>
        public const int Never = int.MaxValue;

        /// <summary>The highest office floor (the boss is above it): armed officers are at their fastest from here (<see cref="GunRules.GuardTempo"/>).</summary>
        public const int TopFloor = 12;

        /// <summary>
        /// Guards on <paramref name="floor"/> are armed when the floor is at or above the first armed floor, or when test mode arms
        /// them everywhere. (The docks have no guards.)
        /// </summary>
        public static bool IsArmed(int floor, int firstArmedFloor, bool everywhere) =>
            floor > 0 && (everywhere || (firstArmedFloor != Never && floor >= firstArmedFloor));
    }
}
