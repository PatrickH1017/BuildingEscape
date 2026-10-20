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
        // a teammate's gun: where his last bullet went (his arm follows it for a moment, then points the way he faces)
        Vector3 remoteAim;
        float remoteAimUntil;
        float nextAmmoNotice;

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
            if (wanted != gun && !ChangeTo(wanted)) return;
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
            if (viewed)
            {
                var aimPoint = AimPoint(out bool foe);
                LocalAimsAtFoe = usable && foe;
                lastAim = aimPoint;
                PoseArm(mag, aimPoint, BodyFree());
            }
            else PoseRemote();
            if (!usable) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                if (mag.StartReload(now)) reloadStep = 0;
                else if (mag.OutOfAmmo) OutOfAmmoNotice();
            }
            if (Input.GetMouseButton(0)) Fire();
        }

        /// <summary>The body is on its feet with free hands (not pinned, knocked over, hiding or carrying something).</summary>
        bool BodyFree()
        {
            if (wobbly && (wobbly.InCustody || wobbly.IsStumbling || wobbly.IsHidden)) return false;
            if (grab && grab.Held) return false;                         // hands full: LMB throws instead
            return true;
        }

        /// <summary>No menu is open, the cursor is in the game and the body is free.</summary>
        bool CanUse() => !Crosshair.MenuOpen && Cursor.lockState == CursorLockMode.Locked && BodyFree();

        /// <summary>Switch to another gun (or none): the old one's reload is cancelled and its model goes. False while the character's model is not ready yet.</summary>
        bool ChangeTo(RangedWeapon next)
        {
            if (gun && gun.Magazine.Reloading) gun.Magazine.CancelReload();
            if (held) { held.Remove(); held = null; }
            gun = null;
            if (!next) return true;
            var anim = vis ? vis.Anim : null;
            if (!anim || !RangedWeapon.Available) return false;
            held = HeldGun.Attach(anim, next.kind, next.silenced);
            if (!held) return false;
            gun = next;
            reloadStep = next.Magazine.Reloading ? 0 : 99;
            GunSounds.Prewarm();
            GunSounds.Draw(transform.position, pack.Viewed);
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
            held.Raise = 1f;
        }

        /// <summary>
        /// Another machine's player fired and the host says the bullet ended at <paramref name="end"/> (<paramref name="result"/>: 0 flew off, 1 a wall or
        /// a desk, 2 a guard, 3 a guard now down for good, 4 the training dummy): the flash, the tracer, the shot and what it hit, from his gun.
        /// </summary>
        public void ShowRemoteShot(Vector3 end, byte result)
        {
            if (!gun) return;                                            // (his gun is not drawn on this machine yet)
            ShotsSeen++;
            var muzzle = held ? held.Muzzle.position : transform.position + Vector3.up * 0.3f + transform.forward * 0.6f;
            GunFx.Get().Shot(muzzle, end, result != 0, held ? held.Grip.right : transform.right, gun.silenced);
            GunSounds.Shot(muzzle, false, gun.silenced);
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
            return imp.point;
        }

        /// <summary>The arm: raised at the crosshair, or tilted up while the gun is being reloaded.</summary>
        void PoseArm(GunMagazine mag, Vector3 aimPoint, bool bodyFree)
        {
            if (!held) return;
            if (!bodyFree) { held.Raise = 0f; return; }
            var from = held.Grip.position;
            var dir = aimPoint - from;
            if (dir.sqrMagnitude < 0.25f) dir = transform.forward;
            if (mag.Reloading)
            {
                var flat = transform.forward; flat.y = 0f;
                var right = Vector3.Cross(Vector3.up, flat.normalized);
                held.AimDirection = Quaternion.AngleAxis(-58f, right) * flat.normalized;
                held.Raise = 0.9f;
            }
            else
            {
                held.AimDirection = dir.normalized;
                held.Raise = 1f;
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
            var aimPoint = AimPoint(out _);
            var dir = aimPoint - muzzle;
            var cam = Camera.main;
            // (a wall right in front of the gun puts the aim point behind or beside the muzzle: shoot along the view then)
            if (dir.sqrMagnitude < 0.04f || (cam && Vector3.Dot(dir, cam.transform.forward) < 0f)) dir = cam ? cam.transform.forward : transform.forward;
            dir.Normalize();
            GunRules.Spread(Random.Range(1, int.MaxValue), spec.spread, out float yaw, out float pitch);
            dir = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

            var imp = Ballistics.Trace(muzzle, dir, spec.range, transform, false);
            LastMuzzle = muzzle; LastDirection = dir; LastImpact = imp;
            bool synced = Net.NetRole.Synced;
            if (!synced) Land(imp, dir, spec);                           // (in a networked run the host decides what the bullet does)

            GunFx.Get().Shot(muzzle, imp.point, imp.hit, held ? held.Grip.right : transform.right, gun.silenced);
            GunSounds.Shot(muzzle, true, gun.silenced);
            if (held) held.Kick();
            if (synced)
            {
                // the same hit sound at once (a guess: the host may disagree), then the host does the rest and tells everybody else
                if (imp.hit && ((imp.guard != null && !imp.guard.Dead) || (imp.dummy != null && !imp.dummy.Down))) GameAudio.BulletHit(false, imp.point);
                Net.RunSync.Instance.RequestGunShot(muzzle, dir, imp.guard != null ? imp.guard.NetId : -1);
            }
            else NoiseBus.Emit(muzzle, gun.NoiseRadius, gameObject);          // every guard within earshot comes to look
        }

        /// <summary>What the bullet does where it lands.</summary>
        void Land(Impact imp, Vector3 dir, RangedWeapon.Spec spec)
        {
            if (!imp.hit) return;
            if (imp.guard != null)
            {
                bool wasDown = imp.guard.Dead;
                imp.guard.WeaponHit(spec.stun, gameObject, spec.guardHits);
                if (!wasDown) GameAudio.BulletHit(imp.guard.Dead, imp.point);
                if (imp.guard.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
            }
            else if (imp.dummy != null)
            {
                if (!imp.dummy.Down) GameAudio.BulletHit(false, imp.point);
                imp.dummy.Hit(spec.damage, transform.position);
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
            if (!mag.Reloading || reloadStep >= GunSounds.ReloadStepTimes.Length) return;
            float scale = mag.ReloadSeconds / 2.2f;
            while (reloadStep < GunSounds.ReloadStepTimes.Length && now - mag.ReloadStartedAt >= GunSounds.ReloadStepTimes[reloadStep] * scale)
            {
                GunSounds.ReloadStep(reloadStep, transform.position, true);
                reloadStep++;
            }
        }

        /// <summary>No round in the gun and no spare clip: say where to get more (at most every few seconds).</summary>
        void OutOfAmmoNotice()
        {
            if (!pack || !pack.Viewed || Time.time < nextAmmoNotice) return;
            nextAmmoNotice = Time.time + 3f;
            Backpack.NoticeToLocal("Out of ammo - the dealer in the Loading Docks sells pistol clips");
        }

        // ------------------------------------------------------------------ the ammunition on screen

        /// <summary>The ammunition panel (bottom right): the gun's name, rounds left (orange at 3 or fewer, red at none), a bullet per round, the reload bar.</summary>
        void OnGUI()
        {
            if (!gun || !pack || !pack.Viewed || Event.current.type != EventType.Repaint) return;
            var mag = gun.Magazine;
            HudWidgets.AmmoPanel(new HudWidgets.AmmoModel
            {
                name = gun.Data.name, rounds = mag.Rounds, capacity = mag.Capacity, reloading = mag.Reloading, unlimited = mag.Unlimited,
                reloadProgress = mag.Reloading ? mag.ReloadProgress(Time.time) : 0f,
            });
            if (!mag.Unlimited && !mag.InfiniteReserve) DrawSpare(mag);
        }

        /// <summary>The spare clips, on a small tag just above the ammunition panel: orange with one clip or less, red with none.</summary>
        static void DrawSpare(GunMagazine mag)
        {
            float s = HudTheme.S, w = 196f * s, gap = 6f * s, h = 24f * s;
            var r = new Rect(Screen.width - 14f * s - w, Screen.height - 14f * s - 68f * s - gap - h, w, h);          // (the panel is 68 high)
            int clips = GunRules.SpareClips(mag.Reserve, mag.Capacity);
            string text = mag.Reserve <= 0 ? "NO SPARE CLIPS" : "SPARE  " + mag.Reserve + "  (" + clips + (clips == 1 ? " clip)" : " clips)");
            var fill = mag.Reserve <= 0 ? HudTheme.Danger : mag.Reserve <= mag.Capacity ? HudTheme.Warning : HudTheme.Info;
            HudTheme.Chip(r, text, fill, 13f);
        }
    }
}
