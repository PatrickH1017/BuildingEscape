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
            if (Rounds <= 0) return Shot.Empty;
            if (now < NextShotAt) return Shot.TooSoon;
            Rounds--;
            Fired++;
            NextShotAt = now + FireInterval;
            return Rounds == 0 ? Shot.FiredLast : Shot.Fired;
        }

        /// <summary>Start a reload (by hand, or because the trigger was pulled on an empty gun). False when it is full or already going.</summary>
        public bool StartReload(float now)
        {
            if (Reloading || Rounds >= Capacity) return false;
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
                Rounds = Capacity;
                Reloads++;
                return Event.ReloadFinished;
            }
            if (Rounds <= 0 && now >= NextShotAt - FireInterval + AutoReloadDelay && StartReload(now)) return Event.ReloadStarted;
            return Event.None;
        }

        /// <summary>Fill the magazine at once (a test).</summary>
        public void Fill() { Rounds = Capacity; Reloading = false; }
    }

    /// <summary>The other pure rules of guns: where a shot with some spread goes, and which guards carry one.</summary>
    public static class GunRules
    {
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

        /// <summary>A guard is a worse shot the further away the player is: the cone he fires into at this distance.</summary>
        public static float GuardCone(float baseDegrees, float perMetreDegrees, float distance) =>
            baseDegrees + perMetreDegrees * Math.Max(0f, distance);

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

    /// <summary>Which guards carry a gun. Nobody does in the game today: it is switched on from the test menu until the owner picks the floor.</summary>
    public static class ArmedRules
    {
        /// <summary>"No floor": nobody is armed by the floor rule.</summary>
        public const int Never = int.MaxValue;

        /// <summary>
        /// Guards on <paramref name="floor"/> are armed when the floor is at or above the first armed floor, or when test mode arms
        /// them everywhere. (The docks have no guards.)
        /// </summary>
        public static bool IsArmed(int floor, int firstArmedFloor, bool everywhere) =>
            floor > 0 && (everywhere || (firstArmedFloor != Never && floor >= firstArmedFloor));
    }
}
