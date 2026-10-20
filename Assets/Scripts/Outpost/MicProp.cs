using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The microphone on the outpost's desk next to the computer (the owner's R9 of 2.23: "The microphone should be next to the computer and visible when a player is sitting at the computer. When speaking on the
    /// microphone it should show the player model's hand pressing the microphone button"). A desk microphone: a round base with a big red push button and a ring that lights while the PA is live, a gooseneck
    /// and a black head. The player at the computer who is speaking through the speakers (V, <see cref="PaState"/>) has his right arm out to the button (<see cref="PaMicPose"/>).
    /// </summary>
    public sealed class MicProp : MonoBehaviour
    {
        public static MicProp Active { get; private set; }

        Transform button;
        Vector3 buttonRest;                                                                   // (where the button sits when nobody presses it, in the microphone's own coordinates)
        float press;                                                                          // 0 = up, 1 = pushed in
        Renderer ring;
        MaterialPropertyBlock block;
        bool wasLive;
        static readonly int ColorId = Shader.PropertyToID("_Color"), EmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Where the button is, in the world (a hand pressing it ends here).</summary>
        public Vector3 ButtonWorld => button ? transform.TransformPoint(buttonRest) + Vector3.up * 0.008f : transform.position;
        /// <summary>The button is pushed in (the PA is live).</summary>
        public bool Pressed => press > 0.5f;
        /// <summary>The ring is lit (the PA is live).</summary>
        public bool LedOn => PaState.Live;

        public void Init(Transform buttonTransform, Renderer ringRenderer)
        {
            button = buttonTransform; ring = ringRenderer;
            buttonRest = button.localPosition;
            Active = this;
        }

        void OnDisable() { if (Active == this) Active = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = null; }

        void Update()
        {
            bool live = PaState.Live;
            if (ring)
            {
                block ??= new MaterialPropertyBlock();
                var c = live ? new Color(0.15f, 1f, 0.20f) : new Color(0.30f, 0.05f, 0.05f);
                block.SetColor(ColorId, c);
                block.SetColor(EmissionId, c * (live ? 1.6f : 0.5f));
                ring.SetPropertyBlock(block);
            }
            if (button)
            {
                press = Mathf.MoveTowards(press, live ? 1f : 0f, Time.deltaTime / 0.08f);
                button.localPosition = buttonRest + Vector3.down * (0.004f * press);
            }
            if (live != wasLive)
            {
                wasLive = live;
                GameAudio.PlaySound(SoundId.KeypadBeep, transform.position, 0.5f, live ? 1.3f : 0.8f, true, 12f, 2f, 0);       // (the click of the button: on, off)
            }
        }
    }

    /// <summary>A two-bone reach for an arm: turns the upper arm and the forearm so that the hand goes towards a point (as far as the arm is long), whatever the rig's own bone axes are.</summary>
    public static class ArmReach
    {
        /// <summary>
        /// Moves <paramref name="end"/> (the hand) towards <paramref name="target"/> by turning <paramref name="upper"/> and <paramref name="lower"/>, blended with what they were by <paramref name="weight"/> (0 = untouched,
        /// 1 = full). The elbow keeps the side it bent to. Call it after the animation has been applied (LateUpdate).
        /// </summary>
        public static void TwoBone(Transform upper, Transform lower, Transform end, Vector3 target, float weight)
        {
            if (!upper || !lower || !end || weight <= 0f) return;
            Vector3 s = upper.position, e = lower.position, w = end.position;
            float l1 = Vector3.Distance(s, e), l2 = Vector3.Distance(e, w);
            if (l1 < 0.01f || l2 < 0.01f) return;
            Vector3 toTarget = target - s;
            if (toTarget.sqrMagnitude < 1e-6f) return;
            float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(l1 - l2) + 0.02f, (l1 + l2) * 0.995f);
            Vector3 dir = toTarget.normalized;
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float a = Mathf.Acos(cosA);                                                  // the angle at the shoulder between the way to the target and the way to the elbow
            Vector3 side = Vector3.Cross(dir, Vector3.Cross(e - s, dir));                 // the elbow's own side, square to the line to the target
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(dir, Vector3.up);
            side.Normalize();
            Vector3 e2 = s + dir * (l1 * Mathf.Cos(a)) + side * (l1 * Mathf.Sin(a));
            Quaternion u0 = upper.rotation;
            upper.rotation = Quaternion.Slerp(u0, Quaternion.FromToRotation(e - s, e2 - s) * u0, weight);
            e = lower.position; w = end.position;                                         // (the forearm and the hand came along with the upper arm)
            Vector3 toGoal = target - e;
            Vector3 goal = e + toGoal.normalized * Mathf.Min(l2, toGoal.magnitude);
            Quaternion l0 = lower.rotation;
            lower.rotation = Quaternion.Slerp(l0, Quaternion.FromToRotation(w - e, goal - e) * l0, weight);
        }

        /// <summary>How long the arm is, shoulder to hand.</summary>
        public static float Length(Transform upper, Transform lower, Transform end) =>
            upper && lower && end ? Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, end.position) : 0f;

        /// <summary>
        /// How a body has to move to reach a point with its arm. Nothing if the shoulder is within <paramref name="reach"/> of <paramref name="target"/> as it stands; else bent over from the waist
        /// (<paramref name="pivot"/>) up to <paramref name="comfortDegrees"/>; else, at that bend, slid towards the target by up to <paramref name="maxSlide"/> metres; else bent further, up to
        /// <paramref name="maxDegrees"/>, at the full slide. False (and nothing to do) when it is out of reach even then: the arm alone points at it. Pure.
        /// </summary>
        public static bool PlanLean(Vector3 pivot, Vector3 shoulder, float reach, Vector3 target, out float degrees, out float slide,
                                    float comfortDegrees = 35f, float maxDegrees = 50f, float maxSlide = 0.55f)
        {
            degrees = 0f; slide = 0f;
            Vector3 flat = target - shoulder; flat.y = 0f;
            if (reach < 0.05f || flat.sqrMagnitude < 1e-4f) return false;
            flat.Normalize();
            Vector3 axis = Vector3.Cross(Vector3.up, flat);                                   // (a positive turn about it tips the body towards the target)
            Vector3 arm = shoulder - pivot;
            float r = reach * 0.95f;
            bool Near(float a, float s) => Vector3.Distance(pivot + flat * s + Quaternion.AngleAxis(a, axis) * arm, target) <= r;
            if (Near(0f, 0f)) return true;
            for (float a = 2.5f; a <= comfortDegrees + 0.01f; a += 2.5f) if (Near(a, 0f)) { degrees = a; return true; }
            for (float s = 0.05f; s <= maxSlide + 0.001f; s += 0.05f) if (Near(comfortDegrees, s)) { degrees = comfortDegrees; slide = s; return true; }
            for (float a = comfortDegrees + 2.5f; a <= maxDegrees + 0.01f; a += 2.5f) if (Near(a, maxSlide)) { degrees = a; slide = maxSlide; return true; }
            degrees = 0f; slide = 0f;
            return false;
        }

        /// <summary>Bends the body from the waist towards <paramref name="toward"/> (only its direction on the floor counts) by <paramref name="degrees"/>, blended by <paramref name="weight"/> (0 = untouched).</summary>
        public static void Bend(Transform waist, Vector3 toward, float degrees, float weight)
        {
            if (!waist || degrees <= 0f || weight <= 0f) return;
            Vector3 flat = toward - waist.position; flat.y = 0f;
            if (flat.sqrMagnitude < 1e-4f) return;
            waist.rotation = Quaternion.Slerp(waist.rotation, Quaternion.AngleAxis(degrees, Vector3.Cross(Vector3.up, flat.normalized)) * waist.rotation, weight);
        }
    }

    /// <summary>
    /// The player who is speaking through the speakers has his right hand on the microphone's button (a picture on every machine: <see cref="PaState.LiveBy"/> is the slot of the speaker, and he has to be in the
    /// outpost). If the button is further than his arm he bends over from the waist and his model slides towards the desk (the capsule, and with it everything physical, stays where it is); when it is
    /// out of reach even so, the arm alone points at it. Everything eases in and out over a quarter of a second. Created automatically.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public sealed class PaMicPose : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<PaMicPose>()) return;
            var go = new GameObject("PaMicPose");
            DontDestroyOnLoad(go);
            go.AddComponent<PaMicPose>();
        }

        readonly Dictionary<WobblyPlayer, float> weights = new Dictionary<WobblyPlayer, float>();
        readonly Dictionary<Transform, Vector3> slid = new Dictionary<Transform, Vector3>();       // (a model that is sliding towards the desk: where it sits when it stands still)
        readonly List<WobblyPlayer> gone = new List<WobblyPlayer>();
        readonly List<Transform> lost = new List<Transform>();

        static int SlotOf(WobblyPlayer p)
        {
            if (p.TryGetComponent(out Net.NetPlayer np) && np.IsSpawned) return Mathf.Max(0, np.Slot);
            return WobblyPlayerRegistry.All.IndexOf(p);
        }

        /// <summary>The body is turned towards the point (within about 70 degrees; the body follows the camera's aim, which stands still while the computer is in use): nobody presses a button behind his back.</summary>
        static bool Faces(Transform body, Vector3 point)
        {
            Vector3 f = body.forward; f.y = 0f;
            Vector3 d = point - body.position; d.y = 0f;
            if (f.sqrMagnitude < 1e-4f || d.sqrMagnitude < 1e-4f) return true;
            return Vector3.Dot(f.normalized, d.normalized) > 0.34f;
        }

        void LateUpdate()
        {
            var mic = MicProp.Active;
            var ctl = OutpostController.Active;
            gone.Clear();
            foreach (var kv in weights) if (!kv.Key) gone.Add(kv.Key);
            foreach (var p in gone) weights.Remove(p);
            lost.Clear();
            foreach (var kv in slid) if (!kv.Key) lost.Add(kv.Key);
            foreach (var m in lost) slid.Remove(m);
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                bool want = mic != null && PaState.Live && SlotOf(p) == PaState.LiveBy && (ctl == null || ctl.IsInside(p.transform.position)) && Faces(p.transform, mic.ButtonWorld);
                weights.TryGetValue(p, out float w);
                w = Mathf.MoveTowards(w, want ? 1f : 0f, Time.deltaTime / 0.25f);
                weights[p] = w;
                var vis = p.GetComponent<PlayerVisual>();
                var cha = vis ? vis.Anim : null;
                var model = cha ? cha.transform : null;
                if (model && slid.TryGetValue(model, out var home)) model.localPosition = home;        // (back where it stands: this frame's slide is worked out from there)
                if (w <= 0.001f || mic == null || !model) { if (model) slid.Remove(model); continue; }
                var animator = model.GetComponent<Animator>();
                if (!animator || !animator.isHuman) continue;
                var arm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm); var fore = animator.GetBoneTransform(HumanBodyBones.RightLowerArm); var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                if (!arm || !fore || !hand) continue;
                Vector3 target = mic.ButtonWorld;
                if (spine && ArmReach.PlanLean(spine.position, arm.position, ArmReach.Length(arm, fore, hand), target, out float degrees, out float slide))
                {
                    if (slide > 0.001f && model.parent)
                    {
                        if (!slid.ContainsKey(model)) slid[model] = model.localPosition;
                        Vector3 flat = target - arm.position; flat.y = 0f;
                        model.localPosition += model.parent.InverseTransformVector(flat.normalized * (slide * w));
                    }
                    ArmReach.Bend(spine, target, degrees, w);
                }
                ArmReach.TwoBone(arm, fore, hand, target, w);
            }
        }
    }
}
