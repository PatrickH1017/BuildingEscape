using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Fires the gun a player has equipped (the pew): the pistol is shown in the right hand with the arm raised at the crosshair,
    /// left mouse fires (hold it to keep firing), R reloads, and after ten shots the magazine is empty and the gun has to be reloaded
    /// (it starts by itself, the trigger of an empty gun only clicks). A bullet goes from the muzzle to wherever the crosshair points; it
    /// stops at the first wall, desk or door, knocks a guard down for good in two hits, and makes a loud noise that guards come to look at.
    ///
    /// Not networked yet: the pew only comes from the test menu (which is off in a networked run), so in a networked run this does nothing.
    /// Added to every player by WobblyPlayer, like MeleeController.
    /// </summary>
    public sealed class GunController : MonoBehaviour
    {
        /// <summary>The gun this player holds, or null.</summary>
        public RangedWeapon Gun => gun;
        /// <summary>The gun's model in the hand (its arm pose and muzzle), or null.</summary>
        public HeldGun Held => held;
        /// <summary>The crosshair of the player the camera follows is on a guard or the training dummy that a bullet would hit.</summary>
        public static bool LocalAimsAtFoe { get; private set; }
        /// <summary>Bullets this player has fired (for the tests).</summary>
        public int ShotsFired { get; private set; }
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
            if (Net.NetSession.Online) wanted = null;                   // (not networked yet: see the summary)
            if (wanted != gun && !ChangeTo(wanted)) return;
            if (!gun) { if (pack.Viewed) LocalAimsAtFoe = false; return; }

            float now = Time.time;
            var mag = gun.Magazine;
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
            if (!usable) return;

            if (Input.GetKeyDown(KeyCode.R) && mag.StartReload(now)) reloadStep = 0;
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
            held = HeldGun.Attach(anim, next.kind);
            if (!held) return false;
            gun = next;
            reloadStep = next.Magazine.Reloading ? 0 : 99;
            GunSounds.Prewarm();
            GunSounds.Draw(transform.position, true);
            return true;
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
            Land(imp, dir, spec);

            GunFx.Get().Shot(muzzle, imp.point, imp.hit, held ? held.Grip.right : transform.right);
            GunSounds.Shot(muzzle, true);
            NoiseBus.Emit(muzzle, spec.noise, gameObject);               // every guard within earshot comes to look
            if (held) held.Kick();
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

        // ------------------------------------------------------------------ the ammunition on screen

        /// <summary>The ammunition panel (bottom right): the gun's name, rounds left (orange at 3 or fewer, red at none), a bullet per round, the reload bar.</summary>
        void OnGUI()
        {
            if (!gun || !pack || !pack.Viewed || Event.current.type != EventType.Repaint) return;
            var mag = gun.Magazine;
            HudWidgets.AmmoPanel(new HudWidgets.AmmoModel
            {
                name = gun.Data.name, rounds = mag.Rounds, capacity = mag.Capacity, reloading = mag.Reloading,
                reloadProgress = mag.Reloading ? mag.ReloadProgress(Time.time) : 0f,
            });
        }
    }
}
