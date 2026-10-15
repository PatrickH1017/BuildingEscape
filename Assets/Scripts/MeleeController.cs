using System.Collections;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Shows the equipped melee weapon in the player's right hand and swings it on left click with the
    /// retargeted slash1 animation. A swing stuns guards in front of you and shoves loose objects.
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
        float nextSwing;
        bool swinging;

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
            var eq = pack.Equipped;
            if (!eq) eq = null;
            if (eq != shown) Show(eq);

            if (!eq || !pack.Viewed || Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || TestMode.PanelOpen || ElevatorPanel.Open || swinging || Time.time < nextSwing) return;
            if (wobbly && wobbly.InCustody) return; // pinned or busted: no swinging
            if (grab && grab.Held) return; // hands full: LMB throws instead
            if (Input.GetMouseButtonDown(0)) StartCoroutine(Swing(eq.GetComponent<MeleeWeapon>()));
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
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestSwing(); // teammates see the swing
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
            swinging = false;
        }

        /// <summary>A teammate swung: play the animation only (their machine does the hitting).</summary>
        public void PlayRemoteSwing()
        {
            if (!swinging) StartCoroutine(RemoteSwing());
        }

        IEnumerator RemoteSwing()
        {
            swinging = true;
            var anim = vis ? vis.Anim : null;
            var clip = MeleeWeapon.Catalog ? MeleeWeapon.Catalog.slash : null;
            float len = 0.6f;
            if (anim && clip && !clip.empty) { anim.PlaySlash(clip, 1f); len = clip.length; }
            else StartCoroutine(FallbackSwing(len));
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
                    if (!dummy.Down) GameAudio.MeleeHit(kind, false, dummy.transform.position + Vector3.up);
                    dummy.Hit(spec.damage, transform.position);
                    hitSomething = true;
                    continue;
                }
                var guard = c.GetComponentInParent<GuardAI>();
                if (guard)
                {
                    if (!seen.Add(guard)) continue;
                    bool wasDead = guard.Dead;
                    guard.WeaponHit(spec.stun, gameObject);
                    if (!wasDead) GameAudio.MeleeHit(kind, guard.Dead, guard.transform.position + Vector3.up);
                    hitSomething = true;
                    if (guard.TryGetComponent(out Rigidbody grb))
                        grb.AddForce((guard.transform.position - transform.position).normalized * spec.knock, ForceMode.VelocityChange);
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

        void OnDestroy() { if (heldModel) Destroy(heldModel); }
    }
}
