using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The gun in a guard's hand. Added to a guard when an armed guard is wanted (<see cref="ArmedGuards"/>, the loadout of <see cref="GuardLoadouts"/>); it shows his gun in his right hand and, in a chase, makes him stop and shoot
    /// instead of only running at the player:
    ///   - after the backup call, while he sees a free player who is between <see cref="engageMin"/> and <see cref="engageMax"/> metres away he stands still, raises the gun, takes <see cref="aimSeconds"/> to aim and then fires
    ///     (one shot every <see cref="fireInterval"/> seconds, or bursts of <see cref="burstShots"/> with a pause between them: the SMG and the rifle);
    ///   - the bullet goes into a cone that gets wider with the distance (a shotgun's pellets each into their own), stops at walls and doors, and passes his colleagues;
    ///   - a player it hits is knocked down (a stumble: he can be pinned while he is down) unless god mode is on in test mode - but a hit from a SHOTGUN or a SNIPER rifle ends that player's floor with a blast (the owner, 2026-10-19:
    ///     "Only shotgun/sniper officers eliminate the player as in not just knockdown but end for the current floor"; <see cref="HeistManager.Blast"/>);
    ///   - after a magazine he has to reload, like the gun in a player's hand;
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
        [Tooltip("Rounds in a burst, and the pause after one (a pistol, a shotgun and a sniper rifle fire single shots: one round, no pause).")]
        public int burstShots = 1;
        public float burstPause;
        [Tooltip("Seconds a reload takes after the magazine is empty.")]
        public float reloadSeconds = 2.2f;
        [Tooltip("Rounds in his magazine.")]
        public int magazineSize = MagazineSize;
        [Tooltip("The bullet goes into a cone of this many degrees, plus the next number for every metre of distance.")]
        public float baseSpreadDegrees = 2.5f;
        public float spreadPerMetre = 0.28f;

        [Header("What a hit does to a player")]
        public float stumbleSeconds = 1.8f;
        [Tooltip("Metres per second the bullet shoves the player along its way.")]
        public float knockback = 7f;

        /// <summary>Rounds in an officer's pistol. (The player's pistol holds eight since 2026-10-18; the officers keep ten and never run out.)</summary>
        public const int MagazineSize = 10;

        /// <summary>He has the gun in his hand (a guard whose gun was taken away is not armed any more).</summary>
        public bool Armed { get; private set; } = true;
        /// <summary>Rounds fired / shots that hit a player (for the tests).</summary>
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
        int burstLeft = 1;
        float nextBurstAt;

        void Awake()
        {
            ai = GetComponent<GuardAI>();
            Configure(kind);
            ai.AttachGun(this);
        }

        void OnDestroy() { if (ai) ai.DetachGun(this); }

        /// <summary>
        /// Sets the gun's kind and everything that goes with it (<see cref="GuardLoadouts.Tuning"/>: his aim, rate, burst, reload, spread and engagement range), the magazine and the model that is built for it. The higher the floor, the faster
        /// he is (<see cref="GunRules.GuardTempo"/>: the owner, 2026-10-18: armed officers "faster to attack as the floors go up").
        /// </summary>
        public void Configure(RangedWeapon.Kind k)
        {
            kind = k;
            var t = GuardLoadouts.Tuning(k);
            engageMin = t.engageMin; engageMax = t.engageMax; aimSeconds = t.aimSeconds;
            fireInterval = t.fireInterval; burstShots = Mathf.Max(1, t.burst); burstPause = t.burstPause; reloadSeconds = t.reloadSeconds; magazineSize = t.magazine;
            baseSpreadDegrees = t.baseSpread; spreadPerMetre = t.spreadPerMetre; stumbleSeconds = t.stumbleSeconds; knockback = t.knockback;
            var hm = HeistManager.Instance;
            GunRules.GuardTempo(hm != null ? hm.Floor : 0, ArmedGuards.FirstArmedFloor, out float aimMul, out float shotMul, out float reloadMul);
            aimSeconds *= aimMul; fireInterval *= shotMul; burstPause *= shotMul; reloadSeconds *= reloadMul;
            mag = new GunMagazine(magazineSize, reloadSeconds, fireInterval);          // (an endless reserve: an officer reloads for ever)
            burstLeft = burstShots;
            nextBurstAt = 0f;
            reloadStep = 99;
            if (held) { held.Remove(); held = null; }                                   // (a gun of another kind in his hand: the right one is built again)
        }

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

        RangedWeapon.Spec Spec => RangedWeapon.Specs[(int)kind];

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
            if (now - aimingSince >= aimSeconds && seen && now >= nextBurstAt)
            {
                switch (mag.TryFire(now))
                {
                    case GunMagazine.Shot.Fired:
                    case GunMagazine.Shot.FiredLast:
                        Shoot(target);
                        if (--burstLeft <= 0) { burstLeft = burstShots; nextBurstAt = now + burstPause; }         // (the SMG and the rifle: a burst, then a pause)
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
            var spec = Spec;
            var muzzle = MuzzlePosition();
            var to = aimPoint - muzzle;
            float distance = to.magnitude;
            var dir = distance > 0.01f ? to / distance : transform.forward;
            int pellets = spec.Pellets;
            float cone = GunRules.GuardCone(baseSpreadDegrees, spreadPerMetre, distance);
            if (pellets > 1) cone = Mathf.Max(cone, spec.spread);                    // (a shotgun's pellets spread as they do in a player's hands)
            int seed = Random.Range(1, int.MaxValue);
            Impact first = default, struck = default;
            Vector3 struckDir = dir;
            bool hitPlayer = false;
            var fx = GunFx.Get();
            var right = held ? held.Grip.right : transform.right;
            for (int i = 0; i < pellets; i++)
            {
                GunRules.Spread(GunRules.PelletSeed(seed, i), cone, out float yaw, out float pitch);
                var d = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(muzzle, d, spec.range, transform, true);
                if (i == 0) first = imp;
                else fx.Pellet(muzzle, imp.point, imp.hit);
                if (imp.player != null && !hitPlayer) { hitPlayer = true; struck = imp; struckDir = d; }
            }
            ShowShot(first.point, first.hit);
            var sync = Net.RunSync.Instance;
            if (sync != null) sync.BroadcastGuardShot(ai.NetId, first.point, first.hit);
            NoiseBus.Emit(muzzle, spec.noise, gameObject);               // the other guards come to look
            if (hitPlayer) HitPlayer(struck, struckDir);
        }

        void HitPlayer(Impact imp, Vector3 dir)
        {
            PlayerHits++;
            GameAudio.BulletHit(false, imp.point);
            var hm = HeistManager.Instance;
            if (hm != null && hm.godMode) return;                        // test mode: the shots are for show
            if (hm != null && GuardLoadouts.Eliminates(kind) && hm.Blast(imp.player, ai)) return;      // (a shotgun or a sniper rifle: that player's floor is over)
            var worn = PlayerArmour.TierOf(imp.player);                      // (armour: the bullet knocks him down for less time and shoves him less - the owner has not said what armour does, these are my numbers)
            imp.player.Stumble((dir * knockback + Vector3.up * 2.5f) * ArmourRules.HitShare(worn), ArmourRules.StumbleSeconds(stumbleSeconds, worn));
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

        /// <summary>The flash, the tracer, the case and the sound of a shot ending at <paramref name="end"/> (the gun's own: a rifle's flash and crack are not a pistol's).</summary>
        public void ShowShot(Vector3 end, bool impact)
        {
            ShotsSeen++;
            var spec = Spec;
            var muzzle = MuzzlePosition();
            GunFx.Get().Shot(muzzle, end, impact, held ? held.Grip.right : transform.right, false, held ? held.PortPosition : null, spec.FlashScale, RangedWeapon.CasingOf(kind));
            GunSounds.Shot(muzzle, false, false, spec.sounds, spec.Pitch);
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
                if (up && !wasRaised) GunSounds.Aim(MuzzlePosition(), false, Spec.sounds);
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
            if (raised && !wasRaised) GunSounds.Aim(MuzzlePosition(), false, Spec.sounds);      // he brings the gun up: a rustle that gives him away
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
            var set = Spec.sounds;
            var times = GunSounds.ReloadTimes(set);
            if (!mag.Reloading || reloadStep >= times.Length) return;
            float scale = mag.ReloadSeconds / 2.2f;
            while (reloadStep < times.Length && now - mag.ReloadStartedAt >= times[reloadStep] * scale)
            {
                GunSounds.ReloadStep(reloadStep, MuzzlePosition(), false, set);
                reloadStep++;
            }
        }
    }
}
