using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Fires the gun a player has equipped (the pew): the pistol is shown in the right hand with the arm raised at the crosshair,
    /// left mouse fires (hold it to keep firing), R reloads, and after eight shots the magazine is empty and the gun has to be reloaded from a
    /// spare clip (it starts by itself, the trigger of an empty gun only clicks; with no spare clip left it stays empty until the dealer sells
    /// more). A bullet goes from the muzzle to wherever the crosshair points; it stops at the first wall, desk or door, knocks a guard down
    /// for good in three hits, and makes a loud noise that guards come to look at.
    ///
    /// In a networked run the shooter's machine shows its own flash, tracer and sound at once and tells the host what it fired
    /// (<see cref="Net.RunSync.RequestGunShot"/>); the HOST traces the bullet, decides what it hit and tells everybody else where it ended, and
    /// every other machine shows the shot in the shooter's hand (<see cref="ShowRemoteShot"/>) - a teammate's pew is in his hand, the arm
    /// raised the way he faces, on every machine. Added to every player by WobblyPlayer, like MeleeController.
    /// </summary>
    public sealed class GunController : MonoBehaviour
    {
        /// <summary>The gun this player holds, or null.</summary>
        public RangedWeapon Gun => gun;
        /// <summary>The gun's model in the hand (its arm pose and muzzle), or null.</summary>
        public HeldGun Held => held;
        /// <summary>The crosshair of the player the camera follows is on a guard or the training dummy that a bullet would hit.</summary>
        public static bool LocalAimsAtFoe { get; private set; }
        /// <summary>A test hook: true gives the basement finite magazines too (the gun checks shoot the dummy in the docks). Never set in the game.</summary>
        public static bool LimitedAmmoForTests;
        /// <summary>Bullets this player has fired (for the tests).</summary>
        public int ShotsFired { get; private set; }
        /// <summary>Shots of this player's gun that another machine fired and this one showed (a networked run; for the tests).</summary>
        public int ShotsSeen { get; private set; }
        /// <summary>Where the last bullet started, which way it went and what it hit (for the tests).</summary>
        public Vector3 LastMuzzle { get; private set; }
        public Vector3 LastDirection { get; private set; }
        public Impact LastImpact { get; private set; }

        Backpack pack;
        PlayerVisual vis;
        PlayerGrab grab;
        WobblyPlayer wobbly;
        CameraFollow rig;
        float nextRigLookup;
        RangedWeapon gun;
        HeldGun held;
        int reloadStep = 99;
        Vector3 lastAim;
        // the camera's aim line of the last AimPoint (set only when the crosshair is on a guard or the dummy): where it starts and which way it goes
        Vector3 aimStart, aimDir;
        /// <summary>How far (m) from the shooter the camera's aim line may start for a bullet to leave it instead of the muzzle (the host takes a start up to <c>GunMuzzleSlack</c>, 4 m, from the shooter: this leaves it room for the lag).</summary>
        const float CameraLineSlack = 3f;
        // a teammate's gun: where his last bullet went (his arm follows it for a moment, then points the way he faces)
        Vector3 remoteAim;
        float remoteAimUntil;
        float nextAmmoNotice;

        // ---- hip fire and aiming (the owner, 2026-10-27; AimRules, HitFeedback, BulletDamageRules)
        /// <summary>The gun is up at the sights (the right mouse button held); otherwise it is fired from the hip, into a wider cone. For a teammate: what his machine says (a networked run).</summary>
        public bool Aiming { get; private set; }
        /// <summary>A test's own aiming button (null: the real right mouse button).</summary>
        public static bool? TestAim;
        /// <summary>A test's own point to aim the shots at (null: where the crosshair points).</summary>
        public static Vector3? TestAimPoint;
        readonly Impact[] pelletImpacts = new Impact[16];
        readonly Vector3[] pelletDirs = new Vector3[16];
        readonly ShotTally tally = new ShotTally();

        void Awake()
        {
            pack = GetComponent<Backpack>();
            vis = GetComponent<PlayerVisual>();
            grab = GetComponent<PlayerGrab>();
            wobbly = GetComponent<WobblyPlayer>();
        }

        void OnDisable() { if (pack && pack.Viewed) LocalAimsAtFoe = false; }

        void OnDestroy() { if (held) held.Remove(); }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (!pack) return;
            if (!vis) vis = GetComponent<PlayerVisual>();
            RangedWeapon wanted = null;
            var eq = pack.Equipped;
            if (eq) eq.TryGetComponent(out wanted);
            if (!ReferenceEquals(wanted, gun) && !ChangeTo(wanted)) return;          // (a gun that was destroyed while it was in the hand compares equal to "none" with ==: its model must still go)
            if (!gun) { if (pack.Viewed) LocalAimsAtFoe = false; return; }

            float now = Time.time;
            var mag = gun.Magazine;
            mag.Unlimited = !LimitedAmmoForTests && GunRules.UnlimitedAmmo(HeistManager.Instance ? HeistManager.Instance.Floor : 1);       // (the basement: unlimited ammunition)
            var ev = mag.Tick(now);
            if (ev == GunMagazine.Event.ReloadStarted) reloadStep = 0;
            else if (ev == GunMagazine.Event.ReloadFinished) reloadStep = 99;
            PlayReloadSounds(mag, now);

            bool viewed = pack.Viewed;
            bool usable = viewed && CanUse();
            Aiming = viewed ? (TestAim ?? (usable && Input.GetMouseButton(1))) : wobbly && wobbly.IsAiming;
            if (viewed && wobbly) wobbly.IsAiming = Aiming;
            if (viewed)
            {
                var aimPoint = AimPoint(out bool foe);
                LocalAimsAtFoe = usable && foe;
                lastAim = aimPoint;
                PoseArm(mag, aimPoint, BodyFree());
            }
            else PoseRemote();
            if (!usable) { if (viewed) trigger.Pull(false, TriggerHeld()); return; }       // (a click that begins now must not become a shot when the menu closes on it: see TriggerLatch)

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (mag.StartReload(now)) reloadStep = 0;
                else if (mag.OutOfAmmo) OutOfAmmoNotice();
            }
            if (trigger.Pull(!DartBoard.Playing, TriggerHeld())) Fire();
        }

        TriggerLatch trigger;

        /// <summary>A test's own fire button (null: the real mouse): the tests cannot press a mouse button.</summary>
        public static bool? TestTrigger;

        /// <summary>A test's own answer to "is the cursor locked in the game" (null: the real one): a batch run cannot lock the cursor.</summary>
        public static bool? TestCursorLocked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTestTrigger() { TestTrigger = null; TestCursorLocked = null; TestAim = null; TestAimPoint = null; }

        static bool TriggerHeld() => TestTrigger ?? Input.GetMouseButton(0);

        /// <summary>Is the click that is held right now one the gun is ignoring (it began while a menu or the elevator panel was up)? For the tests.</summary>
        public bool TriggerLatched => trigger.Latched;

        /// <summary>The body is on its feet with free hands (not pinned, knocked over, hiding or carrying something).</summary>
        bool BodyFree()
        {
            if (wobbly && (wobbly.InCustody || wobbly.IsStumbling || wobbly.IsHidden)) return false;
            if (grab && grab.Held) return false;                         // hands full: LMB throws instead
            return true;
        }

        /// <summary>No menu is open, the cursor is in the game and the body is free.</summary>
        bool CanUse() => !Crosshair.MenuOpen && (TestCursorLocked ?? Cursor.lockState == CursorLockMode.Locked) && BodyFree();

        /// <summary>Switch to another gun (or none): the old one's reload is cancelled and its model goes. False while the character's model is not ready yet.</summary>
        bool ChangeTo(RangedWeapon next)
        {
            if (gun && gun.Magazine.Reloading) gun.Magazine.CancelReload();
            if (held) { held.Remove(); held = null; }
            gun = null;
            if (!next) return true;
            var anim = vis ? vis.Anim : null;
            if (!anim || !RangedWeapon.Available) return false;
            held = HeldGun.Attach(anim, next.kind, next.silenced, true);          // (a player's gun: its Blender animation - the dock worker's pistol, PistolAnimation; a long gun's, WeaponAnimation)
            if (!held) return false;
            gun = next;
            reloadStep = next.Magazine.Reloading ? 0 : 99;
            GunSounds.Prewarm();
            GunSounds.Draw(transform.position, pack.Viewed, next.Data.sounds);
            return true;
        }

        /// <summary>A teammate's gun: raised at where he last shot, or the way he faces; lowered when his body is not free.</summary>
        void PoseRemote()
        {
            if (!held) return;
            if (!BodyFree()) { held.Raise = 0f; return; }
            var aim = Time.time < remoteAimUntil ? remoteAim : transform.forward;
            if (aim.sqrMagnitude < 1e-4f) aim = transform.forward;
            held.AimDirection = aim.normalized;
            held.Raise = Aiming ? 1f : AimRules.HipRaise;                       // (a teammate's gun at his hip or at his sights: the owner, 2026-10-27)
        }

        /// <summary>
        /// Another machine's player fired and the host says the bullet ended at <paramref name="end"/> (<paramref name="result"/>: 0 flew off, 1 a wall or
        /// a desk, 2 a guard, 3 a guard now down for good, 4 the training dummy): the flash, the tracer, the shot and what it hit, from his gun.
        /// </summary>
        public void ShowRemoteShot(Vector3 end, byte result, int seed = 0, bool aiming = true)
        {
            if (!gun) return;                                            // (his gun is not drawn on this machine yet)
            ShotsSeen++;
            var spec = gun.Data;
            var muzzle = held ? held.Muzzle.position : transform.position + Vector3.up * 0.3f + transform.forward * 0.6f;
            var fx = GunFx.Get();
            fx.Shot(muzzle, end, result != 0, held ? held.Grip.right : transform.right, gun.silenced, held ? held.PortPosition : null, spec.FlashScale, RangedWeapon.CasingOf(gun.kind));
            for (int i = 1; i < spec.Pellets; i++)
            {
                // the other pellets of a shotgun shot: his aim with their own spread (the seed is the same on every machine), drawn where each would land here
                var aim = end - muzzle;
                if (aim.sqrMagnitude < 0.01f) break;
                GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread * PlayerBuffs.SpreadFactor(gameObject) * AimRules.SpreadFactor(aiming), out float yaw, out float pitch);
                var d = Quaternion.LookRotation(aim.normalized) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(muzzle, d, spec.range, transform, false);
                fx.Pellet(muzzle, imp.point, imp.hit);
            }
            GunSounds.Shot(muzzle, false, gun.silenced, spec.sounds, spec.Pitch);
            if (result >= 2) GameAudio.BulletHit(result == 3, end);
            if (held) held.Kick();
            remoteAim = end - muzzle;
            remoteAimUntil = Time.time + 1f;
        }

        // ------------------------------------------------------------------ aiming

        CameraFollow Rig
        {
            get
            {
                if (!rig && Time.unscaledTime >= nextRigLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextRigLookup = Time.unscaledTime + 0.5f; }
                return rig;
            }
        }

        /// <summary>
        /// Where the crosshair points: the camera's ray from the player's own depth (so a wall between the camera and the player does not
        /// count) to the first thing in the way, or out to the gun's range. <paramref name="foe"/>: that thing is a guard or the dummy.
        /// </summary>
        Vector3 AimPoint(out bool foe)
        {
            foe = false;
            if (TestAimPoint.HasValue) return TestAimPoint.Value;
            float range = gun.Data.range;
            var cam = Camera.main;
            var r = Rig;
            if (!cam || (r && r.FreeLooking))
            {
                var f = transform.forward; f.y = 0f;
                return transform.position + Vector3.up * 0.3f + f.normalized * range;
            }
            var o = cam.transform.position;
            var d = cam.transform.forward;
            var p = transform.position;
            float t0 = AimMath.StartT(AimMath.Depth(o.x, o.y, o.z, d.x, d.y, d.z, p.x, p.y, p.z));
            var start = o + d * t0;
            var imp = Ballistics.Trace(start, d, range, transform, false);
            foe = imp.hit && ((imp.guard != null && !imp.guard.Dead) || (imp.dummy != null && !imp.dummy.Down));
            aimStart = start; aimDir = d;
            return imp.point;
        }

        /// <summary>
        /// The arm: raised at the crosshair, or tilted up while the gun is being reloaded. In first person with a sight on the gun (<see cref="HeldGun.FirstPersonReload"/>) the gun stays aimed and the reload is shown by the gun itself, which
        /// moves out of the sight line and back with the reload's progress (<see cref="ReloadMotionRules"/>, the owner, 2026-10-23); the sounds' times set where its steps fall. A pistol held with the dock worker's Blender animation
        /// (<see cref="HeldGun.Animated"/>, 2026-10-27) stays aimed and plays the reload clip at the reload's progress, in both views; a long gun held with its Blender animation goes down to the low ready for the reload in third person.
        /// </summary>
        void PoseArm(GunMagazine mag, Vector3 aimPoint, bool bodyFree)
        {
            if (!held) return;
            if (!bodyFree) { held.Raise = 0f; held.ReloadProgress = 0f; return; }
            var from = held.Grip.position;
            var dir = aimPoint - from;
            if (dir.sqrMagnitude < 0.25f) dir = transform.forward;
            if (held.Animated && !held.TwoHanded)
            {
                held.ReloadProgress = mag.Reloading ? Mathf.Max(0.001f, mag.ReloadProgress(Time.time)) : 0f;
                held.SlideLocked = mag.Rounds <= 0;
                held.AimDirection = dir.normalized;
                held.Raise = mag.Reloading || Aiming ? 1f : AimRules.HipRaise;          // (the hip: the raise clip stops half way; the sights: all the way up; the owner, 2026-10-27)
            }
            else if (mag.Reloading && held.Animated && !held.FirstPersonReload)
            {
                // a long gun with its Blender animation, in third person: it goes down to the low ready while the magazine is changed (its file has no reload) and comes up again after
                held.ReloadProgress = 0f;
                held.AimDirection = dir.normalized;
                held.Raise = 0f;
            }
            else if (mag.Reloading && held.FirstPersonReload)
            {
                if (held.ReloadSteps == null) held.ReloadSteps = ReloadMotionRules.StepFractions(GunSounds.ReloadTimes(gun.Data.sounds));
                held.ReloadProgress = mag.ReloadProgress(Time.time);
                held.AimDirection = dir.normalized;
                held.Raise = 1f;
            }
            else if (mag.Reloading)
            {
                held.ReloadProgress = 0f;
                var flat = transform.forward; flat.y = 0f;
                var right = Vector3.Cross(Vector3.up, flat.normalized);
                held.AimDirection = Quaternion.AngleAxis(-58f, right) * flat.normalized;
                held.Raise = 0.9f;
            }
            else
            {
                held.ReloadProgress = 0f;
                held.AimDirection = dir.normalized;
                held.Raise = Aiming ? 1f : AimRules.HipRaise;                           // (hip fire unless the right mouse button is held: the owner, 2026-10-27)
            }
        }

        // ------------------------------------------------------------------ firing

        /// <summary>Pull the trigger now (the mouse calls this; the tests too). True if a bullet left the gun.</summary>
        public bool Fire()
        {
            if (!gun) return false;
            var spec = gun.Data;
            var mag = gun.Magazine;
            float now = Time.time;
            var muzzlePos = held ? held.Muzzle.position : transform.position + Vector3.up * 0.3f;
            switch (mag.TryFire(now))
            {
                case GunMagazine.Shot.Empty:
                    GunSounds.Dry(muzzlePos, true);
                    if (mag.StartReload(now)) reloadStep = 0;
                    else if (mag.OutOfAmmo) OutOfAmmoNotice();
                    return false;
                case GunMagazine.Shot.Reloading:
                case GunMagazine.Shot.TooSoon:
                    return false;
            }
            Shoot(spec, muzzlePos);
            return true;
        }

        void Shoot(RangedWeapon.Spec spec, Vector3 muzzle)
        {
            ShotsFired++;
            var aimPoint = AimPoint(out bool onFoe);
            var dir = aimPoint - muzzle;
            var cam = Camera.main;
            // (a wall right in front of the gun puts the aim point behind or beside the muzzle: shoot along the view then)
            if (dir.sqrMagnitude < 0.04f || (cam && Vector3.Dot(dir, cam.transform.forward) < 0f)) dir = cam ? cam.transform.forward : transform.forward;
            dir.Normalize();
            // The bullet leaves the MUZZLE but the red crosshair is the CAMERA's line, and the gun is lower than the eye and to one side of it: a cubicle panel, a desk edge or a doorway can stand in the muzzle's line to a
            // guard that the camera's line clears, so the crosshair said "hit" and the bullet hit the panel (QA 2026-10-21, "the attack does not connect even though it definitely should"). When the crosshair is on a
            // guard (or the dummy) and the muzzle's own line reaches neither, the bullets leave the camera's line instead (the tracer is still drawn from the muzzle), as long as nothing solid is between the muzzle and
            // where that line starts and that start is near the shooter. The host is sent that start in place of the muzzle and traces the same line.
            var origin = muzzle;
            if (onFoe)
            {
                var straight = Ballistics.Trace(muzzle, dir, spec.range, transform, false);
                if (straight.guard == null && straight.dummy == null)
                {
                    var toStart = aimStart - muzzle;
                    float gap = toStart.magnitude;
                    bool nearShooter = (aimStart - transform.position).sqrMagnitude <= CameraLineSlack * CameraLineSlack;
                    if (nearShooter && (gap < 0.05f || !Ballistics.Trace(muzzle, toStart, gap, transform, false).hit))
                    {
                        origin = aimStart;
                        dir = aimDir.normalized;
                    }
                }
            }
            int seed = Random.Range(1, int.MaxValue);                    // (the shot's own: every pellet's spread comes from it, here and on the host)
            bool synced = Net.NetRole.Synced;
            var fx = GunFx.Get();
            var rightSide = held ? held.Grip.right : transform.right;
            var port = held ? held.PortPosition : null;
            var casing = RangedWeapon.CasingOf(gun.kind);
            bool hitSounded = false, foeSeen = false;
            int pellets = spec.Pellets;
            float cone = spec.spread * PlayerBuffs.SpreadFactor(gameObject) * AimRules.SpreadFactor(Aiming);          // (from the hip a shot scatters wider)
            tally.Reset();
            for (int i = 0; i < pellets; i++)
            {
                GunRules.Spread(GunRules.PelletSeed(seed, i), cone, out float yaw, out float pitch);
                var d = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(origin, d, spec.range, transform, false);
                pelletImpacts[i] = imp; pelletDirs[i] = d;
                if (imp.guard != null && !imp.guard.Dead) tally.Note(imp.guard, imp.guard.IsHeadHit(imp.point.y));
                if (i == 0) { LastMuzzle = muzzle; LastDirection = d; LastImpact = imp; }
                if (synced && imp.hit && ((imp.guard != null && !imp.guard.Dead) || (imp.dummy != null && !imp.dummy.Down))) foeSeen = true;
                if (i == 0) fx.Shot(muzzle, imp.point, imp.hit, rightSide, gun.silenced, port, spec.FlashScale, casing);
                else fx.Pellet(muzzle, imp.point, imp.hit);
            }
            if (!synced)                                                      // (in a networked run the host decides what the bullets do)
            {
                var feedback = HitKind.None;
                for (int i = 0; i < pellets; i++) Land(pelletImpacts[i], pelletDirs[i], spec, ref hitSounded, ref feedback);
                HitFeedback.Play(feedback);                                   // (the hit marker and its sound: a hit, a headshot, an elimination; the owner, 2026-10-27)
            }
            GunSounds.Shot(muzzle, true, gun.silenced, spec.sounds, spec.Pitch);
            if (held) held.Kick();
            if (synced)
            {
                // the same hit sound at once (a guess: the host may disagree), then the host does the rest and tells everybody else
                if (foeSeen) { GameAudio.BulletHit(false, LastImpact.point); HitFeedback.Guess(); }          // (the host's answer adds the headshot or the elimination: RunSync.ShotFeedbackRpc)
                Net.RunSync.Instance.RequestGunShot(origin, dir, LastImpact.guard != null ? LastImpact.guard.NetId : -1, seed, Aiming);
            }
            else NoiseBus.Emit(muzzle, gun.NoiseRadius, gameObject);          // every guard within earshot comes to look
        }

        /// <summary>What a bullet (a pellet) does where it lands. <paramref name="soundPlayed"/>: the hit sound of this shot has been played (a shotgun's eight pellets make one).</summary>
        void Land(Impact imp, Vector3 dir, RangedWeapon.Spec spec, ref bool soundPlayed, ref HitKind feedback)
        {
            if (!imp.hit) return;
            if (imp.guard != null)
            {
                var g = imp.guard;
                bool wasDown = g.Dead;
                // a set amount for each kind of ammunition, more for a headshot, and a shotgun's shot is dealt once to each officer however many pellets hit him (the owner, 2026-10-27)
                if (!wasDown && tally.Take(g, out bool head))
                {
                    g.BulletHitHost(gun.HitsFor(head), false, gameObject);          // (a bullet damages, it never knocks over: the owner, 2026-10-19)
                    feedback = BulletDamageRules.Stronger(feedback, BulletDamageRules.Feedback(g.Dead, head));
                    if (!soundPlayed) { GameAudio.BulletHit(g.Dead, imp.point); soundPlayed = true; }
                }
                if (g.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
            }
            else if (imp.dummy != null)
            {
                if (!imp.dummy.Down)
                {
                    feedback = BulletDamageRules.Stronger(feedback, HitKind.Hit);
                    if (!soundPlayed) { GameAudio.BulletHit(false, imp.point); soundPlayed = true; }
                }
                if (tally.Take(imp.dummy, out _)) imp.dummy.Hit(BulletDamageRules.Damage(gun.kind, false), transform.position);
            }
            else if (imp.body != null && imp.body.TryGetComponent(out WobblyPlayer rival) && rival.gameObject != gameObject)
            {
                if (PvpFighter.TryStrike(GetComponent<WobblyPlayer>(), rival, PvpRules.BulletDamage)) GameAudio.BulletHit(false, imp.point);          // (the PVP room: a bullet hurts a rival inside it)
            }
            else if (imp.body != null)
            {
                imp.body.AddForceAtPosition(dir * Mathf.Min(spec.knock * 0.4f, 8f), imp.point, ForceMode.VelocityChange);
            }
        }

        // ------------------------------------------------------------------ the reload

        /// <summary>The five sounds of a reload, each at its time (the reload may be longer or shorter than the recordings were cut for).</summary>
        void PlayReloadSounds(GunMagazine mag, float now)
        {
            var times = GunSounds.ReloadTimes(gun.Data.sounds);
            if (!mag.Reloading || reloadStep >= times.Length) return;
            float scale = mag.ReloadSeconds / 2.2f;
            while (reloadStep < times.Length && now - mag.ReloadStartedAt >= times[reloadStep] * scale)
            {
                GunSounds.ReloadStep(reloadStep, transform.position, true, gun.Data.sounds);
                reloadStep++;
            }
        }

        /// <summary>No round in the gun and no bullets in the reserve: say where to get more (at most every few seconds).</summary>
        void OutOfAmmoNotice()
        {
            if (!pack || !pack.Viewed || Time.time < nextAmmoNotice) return;
            nextAmmoNotice = Time.time + 3f;
            Backpack.NoticeToLocal("Out of ammo - the dealer in the Loading Docks sells " + (gun ? gun.Data.ammoPlural : "pistol bullets"));
        }

        // ------------------------------------------------------------------ the ammunition on screen

        /// <summary>The ammunition panel (bottom right): the gun's name, rounds left (orange at 3 or fewer, red at none), a bullet per round, the reload bar.</summary>
        void OnGUI()
        {
            if (!gun || !pack || !pack.Viewed || Event.current.type != EventType.Repaint) return;
            var mag = gun.Magazine;
            HudWidgets.AmmoPanel(new HudWidgets.AmmoModel
            {
                name = gun.Data.name, rounds = mag.Rounds, capacity = mag.Capacity, reserve = mag.InfiniteReserve ? -1 : mag.Reserve, reloading = mag.Reloading, unlimited = mag.Unlimited,
                reloadProgress = mag.Reloading ? mag.ReloadProgress(Time.time) : 0f,
                loads = mag.ReloadLoads, loaded = mag.LoadedSoFar(Time.time), counted = gun.kind == RangedWeapon.Kind.Shotgun,
            });
        }


    }
}
