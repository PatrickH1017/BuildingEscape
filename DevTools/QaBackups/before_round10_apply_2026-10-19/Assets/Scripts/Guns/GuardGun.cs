using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The pew in a guard's hand. Added to a guard when an armed guard is wanted (<see cref="ArmedGuards"/>); it shows the pistol in his
    /// right hand and, in a chase, makes him stop and shoot instead of only running at the player:
    ///   - after the backup call, while he sees a free player who is between <see cref="engageMin"/> and <see cref="engageMax"/> metres away
    ///     he stands still, raises the gun, takes <see cref="aimSeconds"/> to aim and then fires one shot every <see cref="fireInterval"/> seconds;
    ///   - the bullet goes into a cone that gets wider with the distance, stops at walls and doors, and passes his colleagues;
    ///   - a player it hits is knocked down (a stumble: he can be pinned while he is down) unless god mode is on in test mode;
    ///   - after ten shots he has to reload (2.2 s, standing still: the time to rush him), like the pew in a player's hand;
    ///   - within arm's reach he does what an unarmed officer does: grabs and pins.
    /// Shooting is decided on the host (or offline); a client's copy only shows the gun and plays the shots the host announces.
    /// </summary>
    [RequireComponent(typeof(GuardAI))]
    public sealed class GuardGun : MonoBehaviour
    {
        public RangedWeapon.Kind kind = RangedWeapon.Kind.Pew;

        [Header("When he shoots")]
        [Tooltip("Closer than this he runs at the player to grab him instead (flat distance, metres).")]
        public float engageMin = 3f;
        [Tooltip("Further than this he keeps running until he is in range.")]
        public float engageMax = 22f;
        [Tooltip("Seconds between getting a player in his sights and the first shot (and after every reload).")]
        public float aimSeconds = 0.5f;

        [Header("How he shoots")]
        [Tooltip("Seconds between two shots (the player's pew fires every 0.3).")]
        public float fireInterval = 0.85f;
        [Tooltip("Seconds a reload takes after the magazine is empty (ten shots).")]
        public float reloadSeconds = 2.2f;
        [Tooltip("The bullet goes into a cone of this many degrees, plus the next number for every metre of distance.")]
        public float baseSpreadDegrees = 2.5f;
        public float spreadPerMetre = 0.28f;

        [Header("What a hit does to a player")]
        public float stumbleSeconds = 1.8f;
        [Tooltip("Metres per second the bullet shoves the player along its way.")]
        public float knockback = 7f;

        /// <summary>Rounds in an officer's magazine. (The player's pistol holds eight since 2026-10-18; the officers keep ten and never run out.)</summary>
        public const int MagazineSize = 10;

        /// <summary>He has the gun in his hand (a guard whose pew was taken away is not armed any more).</summary>
        public bool Armed { get; private set; } = true;
        /// <summary>Rounds fired / bullets that hit a player (for the tests).</summary>
        public int ShotsFired => mag != null ? mag.Fired : 0;
        public int PlayerHits { get; private set; }
        public int Reloads => mag != null ? mag.Reloads : 0;
        public GunMagazine Magazine => mag;
        /// <summary>The model is in his hand.</summary>
        public bool HasModel => held != null;
        public HeldGun Held => held;
        /// <summary>The front of the barrel, or null while there is no model (the flashlight sits there when he is armed).</summary>
        public Transform Muzzle => held ? held.Muzzle : null;
        /// <summary>Shots this machine has seen him fire (a client counts the ones the host announced).</summary>
        public int ShotsSeen { get; private set; }

        GuardAI ai;
        WobblyPlayer aimedAt;
        Collider aimedCollider;
        GunMagazine mag;
        HeldGun held;
        float aimingSince = -1f;
        float wantRaise;
        Vector3 aimPoint;
        int reloadStep = 99;
        bool wasRaised;

        void Awake()
        {
            ai = GetComponent<GuardAI>();
            // (the owner, 2026-10-18: armed officers "faster to attack as the floors go up": the higher the floor, the shorter the aim, the pause between shots and the reload)
            var hm = HeistManager.Instance;
            GunRules.GuardTempo(hm != null ? hm.Floor : 0, ArmedGuards.FirstArmedFloor, out float aimMul, out float shotMul, out float reloadMul);
            aimSeconds *= aimMul; fireInterval *= shotMul; reloadSeconds *= reloadMul;
            mag = new GunMagazine(MagazineSize, reloadSeconds, fireInterval);          // (an endless reserve: an officer reloads for ever)
            ai.AttachGun(this);
        }

        void OnDestroy() { if (ai) ai.DetachGun(this); }

        /// <summary>Take the gun away.</summary>
        public void Remove()
        {
            Armed = false;
            if (held) held.Remove();
            held = null;
            Destroy(this);
        }

        // ------------------------------------------------------------------ the model

        void TryBuild()
        {
            if (held) return;
            var visual = GetComponent<GuardVisual>();
            var anim = visual ? visual.Anim : null;
            if (!anim) return;                                           // (his model is not there yet: tried again next frame)
            held = HeldGun.Attach(anim, kind);
            if (held) GunSounds.Prewarm();
        }

        // ------------------------------------------------------------------ deciding to shoot (host / offline: called by GuardAI's chase)

        /// <summary>
        /// Called every frame of a chase. Returns true when he should stand still and deal with the target (aim, shoot, reload), false when he
        /// should keep running at him.
        /// </summary>
        public bool Engage(WobblyPlayer target, bool seen, float distance)
        {
            if (!Armed || !target) return false;
            float now = Time.time;
            bool stand = GunRules.ShouldStandAndShoot(seen, distance, engageMin, engageMax, mag.Reloading);
            if (!stand)
            {
                aimingSince = -1f;
                wantRaise = 0f;
                return false;
            }

            if (target != aimedAt) { aimedAt = target; aimedCollider = target.GetComponent<Collider>(); }
            aimPoint = ChestOf(target, aimedCollider);
            wantRaise = 1f;
            if (mag.Reloading) { aimingSince = -1f; return true; }
            if (aimingSince < 0f) aimingSince = now;
            if (now - aimingSince >= aimSeconds && seen)
            {
                switch (mag.TryFire(now))
                {
                    case GunMagazine.Shot.Fired:
                    case GunMagazine.Shot.FiredLast:
                        Shoot(target);
                        break;
                    case GunMagazine.Shot.Empty:
                        if (mag.StartReload(now)) reloadStep = 0;
                        break;
                }
            }
            return true;
        }

        void Shoot(WobblyPlayer target)
        {
            var spec = RangedWeapon.Specs[(int)kind];
            var muzzle = MuzzlePosition();
            var to = aimPoint - muzzle;
            float distance = to.magnitude;
            var dir = distance > 0.01f ? to / distance : transform.forward;
            GunRules.Spread(Random.Range(1, int.MaxValue), GunRules.GuardCone(baseSpreadDegrees, spreadPerMetre, distance), out float yaw, out float pitch);
            dir = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

            var imp = Ballistics.Trace(muzzle, dir, spec.range, transform, true);
            ShowShot(imp.point, imp.hit);
            var sync = Net.RunSync.Instance;
            if (sync != null) sync.BroadcastGuardShot(ai.NetId, imp.point, imp.hit);
            NoiseBus.Emit(muzzle, spec.noise, gameObject);               // the other guards come to look
            if (imp.player != null) HitPlayer(imp, dir);
        }

        void HitPlayer(Impact imp, Vector3 dir)
        {
            PlayerHits++;
            GameAudio.BulletHit(false, imp.point);
            var hm = HeistManager.Instance;
            if (hm != null && hm.godMode) return;                        // test mode: the shots are for show
            imp.player.Stumble(dir * knockback + Vector3.up * 2.5f, stumbleSeconds);
        }

        /// <summary>
        /// The upper body of a player: a body's pivot is its middle (the capsule is centred on it), so the chest is a little above the
        /// middle of its collider (which follows a crouch).
        /// </summary>
        public static Vector3 ChestOf(WobblyPlayer p, Collider col)
        {
            if (col != null) { var b = col.bounds; return b.center + Vector3.up * (b.extents.y * 0.3f); }
            return p.transform.position + Vector3.up * 0.3f;
        }

        Vector3 MuzzlePosition() =>
            held ? held.Muzzle.position : transform.position + Vector3.up * 0.3f + transform.forward * 0.6f;

        // ------------------------------------------------------------------ what a shot looks and sounds like (every machine)

        /// <summary>The flash, the tracer, the case and the sound of a shot ending at <paramref name="end"/>.</summary>
        public void ShowShot(Vector3 end, bool impact)
        {
            ShotsSeen++;
            var muzzle = MuzzlePosition();
            GunFx.Get().Shot(muzzle, end, impact, held ? held.Grip.right : transform.right);
            GunSounds.Shot(muzzle, false);
            if (held) held.Kick();
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (!Armed) return;
            if (!held) { TryBuild(); if (!held) return; }

            bool down = ai.Dead || ai.State == GuardState.Stunned;
            if (ai.IsPuppet)
            {
                // a client does not know what he is aiming at: raised while he chases, the gun pointed the way he faces
                bool up = !down && ai.State == GuardState.Chase;
                if (up) { held.Raise = 1f; held.AimDirection = transform.forward; }
                else if (down) held.Raise = 0f;
                else LowReady();
                if (up && !wasRaised) GunSounds.Aim(MuzzlePosition(), false);
                wasRaised = up;
                return;
            }

            float now = Time.time;
            var ev = mag.Tick(now);
            if (ev == GunMagazine.Event.ReloadStarted) reloadStep = 0;
            else if (ev == GunMagazine.Event.ReloadFinished) reloadStep = 99;
            PlayReloadSounds(now);

            if (down || ai.State != GuardState.Chase) { wantRaise = 0f; aimingSince = -1f; }
            bool raised = wantRaise > 0.5f && !down;
            if (raised && !wasRaised) GunSounds.Aim(MuzzlePosition(), false);      // he brings the gun up: a rustle that gives him away
            wasRaised = raised;
            if (mag.Reloading)
            {
                // the gun tilts up while he changes the magazine
                var flat = transform.forward; flat.y = 0f;
                held.AimDirection = Quaternion.AngleAxis(-58f, Vector3.Cross(Vector3.up, flat.normalized)) * flat.normalized;
                held.Raise = 0.9f;
            }
            else if (raised)
            {
                var dir = aimPoint - held.Grip.position;
                held.AimDirection = dir.sqrMagnitude > 0.25f ? dir.normalized : transform.forward;
                held.Raise = 1f;
            }
            else if (down) held.Raise = 0f;
            else LowReady();
        }

        /// <summary>The gun held out low in front of him, pointing at the floor ahead, while he is not aiming (it shows in his hand instead of hanging against his leg).</summary>
        void LowReady()
        {
            var fwd = transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            held.AimDirection = (fwd * 0.75f + Vector3.down * 0.65f).normalized;
            held.Raise = 0.55f;
        }

        void PlayReloadSounds(float now)
        {
            if (!mag.Reloading || reloadStep >= GunSounds.ReloadStepTimes.Length) return;
            float scale = mag.ReloadSeconds / 2.2f;
            while (reloadStep < GunSounds.ReloadStepTimes.Length && now - mag.ReloadStartedAt >= GunSounds.ReloadStepTimes[reloadStep] * scale)
            {
                GunSounds.ReloadStep(reloadStep, MuzzlePosition(), false);
                reloadStep++;
            }
        }
    }
}
