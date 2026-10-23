using System.Collections;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Shows the equipped melee weapon in the player's right hand and swings it on left click with the
    /// retargeted slash1 animation. A swing stuns guards in front of you and shoves loose objects.
    /// With empty hands the same button throws a punch (<see cref="PunchRules"/>; the owner, 2026-10-18: "Add punching"): a short, quick blow that stuns a guard for a moment (and so frees a
    /// pinned friend), but does not count towards knocking him out. Swings, punches and what they hit are heard by teammates who are near (<see cref="Net.RunSync"/> announces them).
    /// </summary>
    public class MeleeController : MonoBehaviour
    {
        const string HandPath = "Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/RightHand";

        public float hitDelay = 0.22f;
        public float hitRadius = 1.4f;

        Backpack pack;
        PlayerVisual vis;
        PlayerGrab grab;
        WobblyPlayer wobbly;
        GameObject heldModel;
        Pickup shown;
        float nextSwing, swingStarted;
        bool swinging;

        /// <summary>Swings / punches a teammate made that this machine heard (it only counts them: the sound is played, and falls silent with the distance), and what they hit (for the tests).</summary>
        public int RemoteSwingsHeard { get; private set; }
        public int RemoteHitsHeard { get; private set; }
        /// <summary>Punches this player has thrown, and how many of them hit something (for the tests).</summary>
        public int PunchesThrown { get; private set; }
        public int PunchesLanded { get; private set; }

        void Awake()
        {
            pack = GetComponent<Backpack>();
            vis = GetComponent<PlayerVisual>();
            grab = GetComponent<PlayerGrab>();
            wobbly = GetComponent<WobblyPlayer>();
        }

        void Update()
        {
            if (!pack) return;
            if (!vis) vis = GetComponent<PlayerVisual>();
            if (swinging && Time.time > swingStarted + 3f) swinging = false;       // (a watchdog: no blow takes three seconds)
            var held = pack.Equipped;
            if (!held) held = null;
            var eq = held;
            if (eq && !eq.TryGetComponent(out MeleeWeapon _)) eq = null;   // a gun in the hand is the GunController's
            if (eq != shown || (heldModel && !shown)) Show(eq);          // (the second part: the weapon in the hand was destroyed - a crowbar that snapped on a safe - and Unity says a destroyed object equals null, so "eq != shown" alone never noticed and the crowbar stayed in the hand)

            if (held == null)
            {
                if (Input.GetMouseButtonDown(0) && !DartBoard.Playing) TryPunch();             // empty hands: a punch
                return;
            }
            if (!eq || !pack.Viewed || Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || TestMode.PanelOpen || ElevatorPanel.Open || OutpostUi.Open || swinging || Time.time < nextSwing) return;
            if (wobbly && wobbly.InCustody) return; // pinned or busted: no swinging
            if (grab && grab.Held) return; // hands full: LMB throws instead
            if (Input.GetMouseButtonDown(0) && !DartBoard.Playing) StartCoroutine(Swing(eq.GetComponent<MeleeWeapon>()));
        }

        /// <summary>
        /// Throws a punch if the player may (<see cref="PunchRules.CanPunch"/>: nothing in the hands, no menu up, not pinned or busted, not in the middle of another blow, the cooldown over). True when it was thrown.
        /// </summary>
        public bool TryPunch()
        {
            if (!pack) return false;
            var held = pack.Equipped;
            bool menu = !pack.Viewed || Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || TestMode.PanelOpen || ElevatorPanel.Open || OutpostUi.Open;
            if (!PunchRules.CanPunch(!held, !(grab && grab.Held), menu, wobbly && wobbly.InCustody, swinging, Time.time, nextSwing)) return false;
            StartCoroutine(Punch());
            return true;
        }

        void Show(Pickup eq)
        {
            if (heldModel) Destroy(heldModel);
            heldModel = null; shown = eq;
            if (!eq || !eq.TryGetComponent(out MeleeWeapon mw) || !MeleeWeapon.Available) return;
            var anim = vis ? vis.Anim : null;
            if (!anim) { shown = null; return; } // model not ready yet, retry next frame
            var hand = anim.RightHand;                    // a Tiny Toon person: the humanoid right hand
            if (!hand) hand = anim.transform.Find(HandPath);
            if (!hand) hand = FindDeep(anim.transform, "RightHand");
            if (!hand) { Debug.LogWarning("MeleeController: RightHand bone not found on the character."); return; }
            heldModel = MeleeWeapon.BuildHeld(mw.kind, hand);
            var firstPerson = anim.GetComponent<FirstPersonMelee>();
            if (!firstPerson) firstPerson = anim.gameObject.AddComponent<FirstPersonMelee>();
            firstPerson.Hold(hand, heldModel.transform, anim);                      // (in first person the arm is raised so that the weapon is in the picture)
        }

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r) return r; }
            return null;
        }

        IEnumerator Swing(MeleeWeapon mw)
        {
            if (!mw) yield break;
            swinging = true;
            swingStarted = Time.time;
            try
            {
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestSwing((byte)mw.kind); // teammates see (and hear) the swing
            var spec = mw.Data;
            var anim = vis ? vis.Anim : null;
            var clip = MeleeWeapon.Catalog ? MeleeWeapon.Catalog.slash : null;
            float speed = 1f;
            bool useClip = anim && clip && !clip.empty;
            if (useClip) anim.PlaySlash(clip, speed);
            float len = useClip ? clip.length / speed : 0.6f;
            if (!useClip) StartCoroutine(FallbackSwing(len)); // clip missing: still show a swing
            Debug.Log($"Swing with {spec.name} (animation clip: {(useClip ? "slash1" : "none, using fallback")})");

            GameAudio.Whoosh(transform.position);
            yield return new WaitForSeconds(hitDelay);
            Hit(spec, mw.kind);
            yield return new WaitForSeconds(Mathf.Max(0f, len - hitDelay));
            nextSwing = Time.time + Mathf.Max(0f, spec.cooldown - len);
            }
            finally { swinging = false; }       // (whatever happens inside, an exception or a stopped coroutine: the next swing must be possible)
        }

        /// <summary>A punch: the same attack animation, played faster, no weapon in the hand; the hit zone is smaller and the blow quieter than a weapon's.</summary>
        IEnumerator Punch()
        {
            swinging = true;
            swingStarted = Time.time;
            try
            {
            PunchesThrown++;
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestSwing(MeleeWeapon.PunchKind);
            var anim = vis ? vis.Anim : null;
            var clip = MeleeWeapon.Catalog ? MeleeWeapon.Catalog.slash : null;
            float speed = PunchRules.AnimSpeed;
            bool useClip = anim && clip && !clip.empty;
            if (useClip) anim.PlaySlash(clip, speed);
            float len = useClip ? clip.length / speed : 0.35f;

            GameAudio.PunchWhoosh(transform.position);
            yield return new WaitForSeconds(PunchRules.HitDelay);
            HitFists();
            yield return new WaitForSeconds(Mathf.Max(0f, len - PunchRules.HitDelay));
            nextSwing = Time.time + Mathf.Max(0f, PunchRules.Cooldown - len);
            }
            finally { swinging = false; }
        }

        /// <summary>A teammate swung or punched (<paramref name="kind"/>: 0-2 a weapon, <see cref="MeleeWeapon.PunchKind"/> a fist): play the animation and the sound (their machine does the hitting; the sound fades with the distance).</summary>
        public void PlayRemoteSwing(byte kind = 0)
        {
            bool punch = kind == MeleeWeapon.PunchKind;
            RemoteSwingsHeard++;
            GameAudio.RemoteWhoosh(transform.position, punch);
            if (!swinging) StartCoroutine(RemoteSwing(punch));
        }

        /// <summary>A teammate's blow landed (a weapon on a guard or the dummy, a fist): the sound of it, where it landed (their machine plays its own).</summary>
        public void PlayRemoteHit(byte kind, bool killed, Vector3 at)
        {
            RemoteHitsHeard++;
            GameAudio.MeleeHitAt(kind, killed, at);
        }

        IEnumerator RemoteSwing(bool punch)
        {
            swinging = true;
            var anim = vis ? vis.Anim : null;
            var clip = MeleeWeapon.Catalog ? MeleeWeapon.Catalog.slash : null;
            float speed = punch ? PunchRules.AnimSpeed : 1f;
            float len = punch ? 0.35f : 0.6f;
            if (anim && clip && !clip.empty) { anim.PlaySlash(clip, speed); len = clip.length / speed; }
            else if (!punch) StartCoroutine(FallbackSwing(len));
            yield return new WaitForSeconds(len);
            swinging = false;
        }

        /// <summary>Simple overhead arc of the held weapon, used only if the slash clip is unavailable.</summary>
        IEnumerator FallbackSwing(float len)
        {
            if (!heldModel) yield break;
            var t = heldModel.transform;
            var baseRot = t.rotation;
            for (float e = 0f; e < len; e += Time.deltaTime)
            {
                if (!heldModel) yield break;
                float k = e / len;
                float ang = Mathf.Lerp(-80f, 100f, k * k * (3f - 2f * k));
                t.rotation = Quaternion.AngleAxis(ang, transform.right) * baseRot;
                yield return null;
            }
            if (heldModel) t.localRotation = MeleeWeapon.HoldRotation;
        }

        void Hit(MeleeWeapon.Spec spec, MeleeWeapon.Kind kind)
        {
            // the hit zone sits in front of the player, towards where the crosshair points
            var centre = transform.position + Crosshair.SwingForward(transform) * 1.2f + Vector3.up * 0.3f;
            var cols = Physics.OverlapSphere(centre, hitRadius, ~0, QueryTriggerInteraction.Ignore);
            var seen = new System.Collections.Generic.HashSet<Object>();
            bool hitSomething = false;
            foreach (var c in cols)
            {
                if (c.transform.IsChildOf(transform)) continue;
                var dummy = c.GetComponentInParent<TrainingDummy>();
                if (dummy)
                {
                    if (!seen.Add(dummy)) continue;
                    if (!dummy.Down) { GameAudio.MeleeHit(kind, false, dummy.transform.position + Vector3.up); Announce((byte)kind, false, dummy.transform.position + Vector3.up); }
                    dummy.Hit(spec.damage, transform.position);
                    hitSomething = true;
                    continue;
                }
                var guard = c.GetComponentInParent<GuardAI>();
                if (guard)
                {
                    if (!seen.Add(guard)) continue;
                    bool wasDead = guard.Dead;
                    guard.MeleeHit(spec.stun, gameObject, spec.guardDamage);         // (a blow knocks over for the weapon's tier AND takes the weapon's damage off his 100 HP: the owner, 2026-10-22; before that a blow only knocked over)
                    if (!wasDead) { GameAudio.MeleeHit(kind, guard.Dead, guard.transform.position + Vector3.up); Announce((byte)kind, guard.Dead, guard.transform.position + Vector3.up); }
                    hitSomething = true;
                    if (guard.TryGetComponent(out Rigidbody grb))
                        grb.AddForce((guard.transform.position - transform.position).normalized * spec.knock, ForceMode.VelocityChange);
                    continue;
                }
                var rival = c.GetComponentInParent<WobblyPlayer>();                     // (the PVP room: a weapon hurts a rival inside it, see PvpRules)
                if (rival && rival.gameObject != gameObject)
                {
                    if (!seen.Add(rival)) continue;
                    if (PvpFighter.TryStrike(wobbly, rival, spec.damage)) { GameAudio.MeleeHit(kind, false, rival.transform.position + Vector3.up); Announce((byte)kind, false, rival.transform.position + Vector3.up); hitSomething = true; }
                    continue;
                }
                var rb = c.attachedRigidbody;
                if (!rb || rb.isKinematic || rb.GetComponent<WobblyPlayer>() || rb.GetComponent<HeavyLoot>()) continue;
                if (!seen.Add(rb)) continue;
                var dir = (rb.worldCenterOfMass - transform.position); dir.y = 0.2f;
                rb.AddForce(dir.normalized * spec.knock, ForceMode.VelocityChange);
                hitSomething = true;
            }
            NoiseBus.Emit(centre, hitSomething ? spec.noise : 6f, gameObject);
        }

        /// <summary>
        /// The punch lands: what is in front of the fists (a smaller zone than a weapon's). A guard is stunned for a moment (it cuts his radio call and frees a player he holds down) but the blow does not count towards
        /// knocking him out; the training dummy loses a little health; loose things are shoved a little.
        /// </summary>
        void HitFists()
        {
            var centre = transform.position + Crosshair.SwingForward(transform) * PunchRules.Reach + Vector3.up * 0.3f;
            var cols = Physics.OverlapSphere(centre, PunchRules.Radius, ~0, QueryTriggerInteraction.Ignore);
            var seen = new System.Collections.Generic.HashSet<Object>();
            bool hitSomething = false;
            foreach (var c in cols)
            {
                if (c.transform.IsChildOf(transform)) continue;
                var dummy = c.GetComponentInParent<TrainingDummy>();
                if (dummy)
                {
                    if (!seen.Add(dummy)) continue;
                    if (!dummy.Down) { GameAudio.PunchHit(dummy.transform.position + Vector3.up); Announce(MeleeWeapon.PunchKind, false, dummy.transform.position + Vector3.up); }
                    dummy.Hit(PunchRules.Damage, transform.position);
                    hitSomething = true;
                    continue;
                }
                var guard = c.GetComponentInParent<GuardAI>();
                if (guard)
                {
                    if (!seen.Add(guard)) continue;
                    if (!guard.Dead) { GameAudio.PunchHit(guard.transform.position + Vector3.up); Announce(MeleeWeapon.PunchKind, false, guard.transform.position + Vector3.up); }
                    guard.PunchHit(PunchRules.StunSeconds, gameObject);
                    hitSomething = true;
                    if (guard.TryGetComponent(out Rigidbody grb))
                        grb.AddForce((guard.transform.position - transform.position).normalized * PunchRules.Knock, ForceMode.VelocityChange);
                    continue;
                }
                var rival = c.GetComponentInParent<WobblyPlayer>();                     // (the PVP room: a fist hurts a rival inside it, see PvpRules)
                if (rival && rival.gameObject != gameObject)
                {
                    if (!seen.Add(rival)) continue;
                    if (PvpFighter.TryStrike(wobbly, rival, PvpRules.FistDamage)) { GameAudio.PunchHit(rival.transform.position + Vector3.up); Announce(MeleeWeapon.PunchKind, false, rival.transform.position + Vector3.up); hitSomething = true; }
                    continue;
                }
                var rb = c.attachedRigidbody;
                if (!rb || rb.isKinematic || rb.GetComponent<WobblyPlayer>() || rb.GetComponent<HeavyLoot>()) continue;
                if (!seen.Add(rb)) continue;
                var dir = (rb.worldCenterOfMass - transform.position); dir.y = 0.2f;
                rb.AddForce(dir.normalized * PunchRules.Knock, ForceMode.VelocityChange);
                hitSomething = true;
            }
            if (hitSomething) PunchesLanded++;
            NoiseBus.Emit(centre, hitSomething ? PunchRules.Noise : PunchRules.MissNoise, gameObject);
        }

        /// <summary>Tells the teammates (through the host) that a blow landed here, so that those who are near hear it too.</summary>
        static void Announce(byte kind, bool killed, Vector3 at)
        {
            if (Net.NetRole.Synced && Net.RunSync.Instance != null) Net.RunSync.Instance.RequestMeleeHit(kind, killed, at);
        }

        void OnDestroy() { if (heldModel) Destroy(heldModel); }
    }
}
