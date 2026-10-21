using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A gun in a character's hands (a player's or an armed guard's): builds the model on the right hand bone and, while the gun is
    /// raised, bends the arms so the barrel points where the owner is aiming. The rigs have no aiming animation, so the arms are
    /// turned by code after the animation has run each frame (LateUpdate).
    /// A pistol is held in the right hand alone: the upper arm and the forearm point along the aim and the hand is turned so the gun is level and points exactly that way.
    /// A long gun (<see cref="RangedWeapon.HoldStyle.TwoHand"/>, the owner, 2026-10-19: the sub machine gun, the rifle, the shotgun and the sniper rifle) is held the way a soldier holds one: the right hand on the pistol grip close to the
    /// shoulder with the elbow bent, the left hand on the foregrip (the forend, the pump) - both arms solved to those two points (a two-bone solver, the elbows kept down and out). The gun is scaled to the arms of whoever holds it
    /// (the rigs are very different sizes: a toon person's arm is a third of a metre, a Party character's nearly a metre), and when the left hand could not reach the gun is pulled back towards the chest until it can.
    /// With the gun lowered the animation carries it as it is. Whoever owns the gun sets <see cref="Raise"/> (0 lowered .. 1 raised) and <see cref="AimDirection"/> (a world direction) each frame.
    /// </summary>
    [DefaultExecutionOrder(100)]     // after the animation and the character animator have posed the body; the guard's flashlight (200) follows the muzzle
    public sealed class HeldGun : MonoBehaviour
    {
        const string HandPath = "Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/RightHand";
        const string LeftHandPath = "Armature/Hips/Spine/Spine1/Spine2/LeftShoulder/LeftArm/LeftForeArm/LeftHand";
        /// <summary>The arm of the characters the guns are built for (the Synty office people: upper arm 0.33 m, forearm 0.26 m); another rig's guns are scaled by its arm over this.</summary>
        public const float ReferenceArm = 0.585f;
        /// <summary>A long gun is a little smaller than the arms alone would make it, so that the left hand can reach the foregrip with the right hand close to the shoulder.</summary>
        const float FitSize = 0.88f;

        public RangedWeapon.Kind Kind { get; private set; }
        /// <summary>The transform the model hangs on: +Z is the barrel, +Y the top of the gun.</summary>
        public Transform Grip { get; private set; }
        /// <summary>The front of the barrel (where the bullet and the flash come from).</summary>
        public Transform Muzzle { get; private set; }
        /// <summary>Where the empty case leaves the gun: the ejection port on the right of the slide (null when the model has none).</summary>
        public Transform Port { get; private set; }
        public Vector3? PortPosition => Port ? Port.position : (Vector3?)null;
        /// <summary>Where the left hand holds a gun carried in both hands (null for a pistol).</summary>
        public Transform Foregrip { get; private set; }
        public GameObject Model { get; private set; }

        /// <summary>0..1: how far the arm is raised to point the gun along <see cref="AimDirection"/> (smoothed here).</summary>
        public float Raise;
        /// <summary>The world direction the barrel should point in while raised.</summary>
        public Vector3 AimDirection = Vector3.forward;
        /// <summary>The raise reached now (what the arm is doing this frame).</summary>
        public float RaiseNow => raiseNow;
        /// <summary>The rig has a shoulder - elbow - wrist chain this can bend (otherwise the gun just rides on the hand).</summary>
        public bool HasArm => hasArm;
        /// <summary>The gun is held in both hands: the rig has the left arm too.</summary>
        public bool TwoHanded => twoHand;
        /// <summary>How far the left hand is from the foregrip this frame (m; for the tests: it should be about zero).</summary>
        public float LeftHandGap { get; private set; }
        /// <summary>The gun's scale for this character (its arm over <see cref="ReferenceArm"/>; 1 for a pistol).</summary>
        public float BodyScale { get; private set; } = 1f;
        /// <summary>The bones and lengths of the arm (for the tests and for debugging a new rig).</summary>
        public string ArmInfo() => hand == null ? "no hand"
            : $"{(upper ? upper.name : "?")} > {(fore ? fore.name : "?")} > {hand.name}, upper arm {(fore && upper ? (fore.position - upper.position).magnitude : -1f):0.000} m, forearm {(fore ? (hand.position - fore.position).magnitude : -1f):0.000} m, chain {(hasArm ? "ok" : "NOT used")}{(twoHand ? ", left arm ok" : "")}";

        /// <summary>How the two-handed hold was fitted to this body (for the tests and the pictures).</summary>
        public string FitInfo() => $"half the shoulder width {half:0.000} m, arm {armLength:0.000} m, gun {fitFwd:0.00} arms in front of the shoulders and {fitLat:0.00} of half the chest to the right of the middle, gun scale {BodyScale:0.00}";

        /// <summary>Roll of the left hand about the barrel, in degrees (tuned with the pictures of every rig; the mirror of the right hand's grip is the start).</summary>
        public float LeftRoll;

        Transform upper, fore, hand, lUpper, lFore, lHand;
        bool hasArm, twoHand;
        float raiseNow, kick, armLength;

        /// <summary>Puts a gun in the hands of this character. Null if the right hand cannot be found.</summary>
        public static HeldGun Attach(CharacterAnimator anim, RangedWeapon.Kind kind, bool silenced = false)
        {
            if (!anim || !RangedWeapon.Available) return null;
            var hand = anim.RightHand;                                    // a Tiny Toon person: the humanoid right hand
            if (!hand) hand = anim.transform.Find(HandPath);
            if (!hand) hand = FindDeep(anim.transform, "RightHand");
            if (!hand) { Debug.LogWarning("HeldGun: RightHand bone not found on the character."); return null; }

            var held = anim.gameObject.GetComponent<HeldGun>();
            if (held) held.Remove();
            held = anim.gameObject.AddComponent<HeldGun>();
            held.Kind = kind;
            held.hand = hand;
            held.FindArm();
            var spec = RangedWeapon.Specs[(int)kind];
            bool wantsTwo = spec.hold == RangedWeapon.HoldStyle.TwoHand;
            float bodyScale = 1f;
            if (wantsTwo)
            {
                held.FindLeftArm(anim);
                if (held.twoHand && !held.FitTwoHand(kind, out bodyScale)) { held.twoHand = false; bodyScale = held.armLength > 0.05f ? Mathf.Clamp(held.armLength / ReferenceArm, 0.45f, 1.9f) * FitSize : 1f; }
            }
            held.BodyScale = bodyScale;
            held.Model = RangedWeapon.BuildHeld(kind, hand, out var grip, out var muzzle, silenced, held.BodyScale);
            held.Grip = grip;
            held.Muzzle = muzzle;
            held.Port = grip.Find("Port");
            held.Foregrip = grip.Find("Foregrip");
            grip.localRotation = GripRotation(anim.ClipMode);
            if (held.twoHand && !held.Foregrip) held.twoHand = false;     // (a model without a foregrip is held in one hand)
            return held;
        }

        /// <summary>How the gun sits in the hand: the barrel along the fingers, the top of the gun up the back of the hand.</summary>
        static Quaternion GripRotation(bool tinyToon)
        {
            // the same bone directions the melee weapons are held by (MeleeWeapon.HoldRotation): the head of a weapon runs along
            // the fingers, the flat of it along FlatInHand
            var fingers = new Vector3(-0.982f, 0.116f, -0.152f);
            var back = new Vector3(0.190f, 0.668f, -0.719f);
            return Quaternion.LookRotation(fingers, back) * Quaternion.Euler(0f, 0f, tinyToon ? 0f : 0f);
        }

        void FindArm()
        {
            fore = hand ? hand.parent : null;
            upper = fore ? fore.parent : null;
            hasArm = false;
            if (!hand || !fore || !upper) return;
            float a = (fore.position - upper.position).magnitude, b = (hand.position - fore.position).magnitude;
            hasArm = a > 0.03f && a < 2f && b > 0.03f && b < 2f;          // (a rig that is not shoulder - elbow - wrist: the arm is left alone)
            armLength = hasArm ? a + b : 0f;
        }

        /// <summary>The left arm: the humanoid left hand, or the Party rig's by its path, or by name; its parents are the forearm and the upper arm.</summary>
        void FindLeftArm(CharacterAnimator anim)
        {
            twoHand = false;
            var l = anim.LeftHand;
            if (!l) l = anim.transform.Find(LeftHandPath);
            if (!l) l = FindDeep(anim.transform, "LeftHand");
            if (!l) l = FindDeep(anim.transform, "Hand_L");
            if (!l || !l.parent || !l.parent.parent) return;
            float a = (l.parent.position - l.parent.parent.position).magnitude, b = (l.position - l.parent.position).magnitude;
            if (!(a > 0.03f && a < 2f && b > 0.03f && b < 2f) || !hasArm) return;
            lHand = l; lFore = l.parent; lUpper = lFore.parent;
            twoHand = true;
        }

        // how the gun is placed in both hands, worked out once for this body and gun (FitTwoHand): the right hand's distance in front of the shoulders (a share of the arm), and how far right of the middle of the chest the gun's line is (a share of half the shoulder width)
        float fitFwd = 0.3f, fitLat = 0.5f, half = 0.2f;

        /// <summary>
        /// Can this body hold this gun in both hands? Works out where the gun goes so that the left hand reaches its foregrip and the right hand its grip with the arms a little bent: as far forward and as far to the right of the chest's
        /// middle as the arms allow (a wide chibi chest with short arms has to hold it in the middle and close; a tall soldier holds it at the shoulder). The gun is made smaller until it can be done. False when it cannot (then the gun is held in one hand).
        /// </summary>
        bool FitTwoHand(RangedWeapon.Kind kind, out float bodyScale)
        {
            bodyScale = 1f;
            var lay = RangedWeapon.LayoutOf(kind);
            if (!lay.hasForegrip || !twoHand) return false;
            float L = armLength;
            half = Mathf.Max(0.02f, Mathf.Abs(Vector3.Dot(upper.position - lUpper.position, transform.right)) * 0.5f);
            float s0 = Mathf.Clamp(L / ReferenceArm, 0.45f, 1.9f) * FitSize;
            for (int attempt = 0; attempt < 8 && s0 >= 0.30f; attempt++)
            {
                var d = (lay.foregrip - lay.grip) * lay.scale * s0;                     // the foregrip from the grip: x along the barrel, y up
                float bestScore = -1f, bestK = 0f, bestLat = 0f;
                foreach (float latShare in new[] { 0.6f, 0.35f, 0f })
                {
                    float lat = latShare * half;
                    for (float k = 0.44f; k >= 0.08f; k -= 0.02f)
                    {
                        float fwd = k * L;
                        float reachLeft = new Vector3(half + lat, 0.28f * L - d.y, fwd + d.x).magnitude;
                        float reachRight = new Vector3(half - lat, 0.28f * L, fwd).magnitude;
                        if (reachLeft > 0.96f * L || reachRight > 0.96f * L) continue;
                        float score = k + 0.5f * latShare;
                        if (score > bestScore) { bestScore = score; bestK = k; bestLat = latShare; }
                    }
                }
                if (bestScore >= 0f) { fitFwd = bestK; fitLat = bestLat; bodyScale = s0; return true; }
                s0 *= 0.9f;
            }
            return false;
        }

        static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r) return r; }
            return null;
        }

        /// <summary>A shot was fired: the barrel kicks up.</summary>
        public void Kick() { kick = 1f; }

        /// <summary>Take the gun away (the model is destroyed).</summary>
        public void Remove()
        {
            if (Grip) Destroy(Grip.gameObject);
            Destroy(this);
        }

        void LateUpdate()
        {
            if (!Grip || !hasArm) return;
            raiseNow = Mathf.MoveTowards(raiseNow, Mathf.Clamp01(Raise), Time.deltaTime * 6f);
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);
            if (raiseNow < 0.001f) return;

            var aim = AimDirection;
            if (aim.sqrMagnitude < 1e-4f) aim = transform.forward;
            aim.Normalize();
            // never straight up or down: the gun's "up" is undefined there
            if (Mathf.Abs(aim.y) > 0.97f) aim = new Vector3(aim.x, Mathf.Sign(aim.y) * 0.97f, aim.z).normalized;
            if (kick > 0f)
            {
                var right = Vector3.Cross(Vector3.up, aim);
                if (right.sqrMagnitude > 1e-4f) aim = Quaternion.AngleAxis(-kick * RangedWeapon.Specs[(int)Kind].KickDegrees, right.normalized) * aim;
            }

            float b = raiseNow;
            if (twoHand) { TwoHandPose(aim, b); return; }
            // the whole arm points along the aim (the shoulder stays where the animation has it)
            var du = fore.position - upper.position;
            if (du.sqrMagnitude > 1e-6f)
                upper.rotation = Quaternion.Slerp(upper.rotation, Quaternion.FromToRotation(du, aim) * upper.rotation, b);
            var df = hand.position - fore.position;
            if (df.sqrMagnitude > 1e-6f)
                fore.rotation = Quaternion.Slerp(fore.rotation, Quaternion.FromToRotation(df, aim) * fore.rotation, b);
            // the hand: the grip (child of the hand) must come out level and pointing along the aim
            var want = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Inverse(Grip.localRotation);
            hand.rotation = Quaternion.Slerp(hand.rotation, want, b);
        }

        // ------------------------------------------------------------------ a gun in both hands

        /// <summary>The right hand near the shoulder on the pistol grip, the left on the foregrip; the gun level and pointing along <paramref name="aim"/>.</summary>
        void TwoHandPose(Vector3 aim, float b)
        {
            float L = armLength;
            var f = aim;
            var right = Vector3.Cross(Vector3.up, f);
            if (right.sqrMagnitude < 1e-4f) right = transform.right;
            right.Normalize();
            var up = Vector3.Cross(f, right);                                           // (the aim's own up: level gun, whatever the pitch)
            var gunRot = Quaternion.LookRotation(f, up);
            var sR = upper.position; var sL = lUpper.position;
            // where the foregrip is from the grip, with the gun turned the way it is going to be
            var toForegrip = gunRot * Vector3.Scale(Foregrip.localPosition, Grip.lossyScale);
            // the right hand: in front of and below the shoulder and towards the middle of the chest, where FitTwoHand put it (so that the left hand reaches the foregrip)
            var pr = sR + f * (fitFwd * L) - up * (0.28f * L) - right * (half * (1f - fitLat));
            var tl = pr + toForegrip;
            SolveArm(upper, fore, hand, pr, -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
            SolveArm(lUpper, lFore, lHand, tl, -up * 0.8f - right * 0.6f, b);
            lHand.rotation = Quaternion.Slerp(lHand.rotation, gunRot * Quaternion.Euler(0f, 0f, LeftRoll) * MirrorX(Quaternion.Inverse(Grip.localRotation)), b);
            LeftHandGap = (lHand.position - Foregrip.position).magnitude;
        }

        /// <summary>The mirror image of a rotation across the plane the gun's right and left sides are on: the left hand is the right one seen in a mirror.</summary>
        static Quaternion MirrorX(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        /// <summary>
        /// A two-bone arm: the wrist goes to <paramref name="target"/> (as near as the arm reaches) and the elbow to the side the pole points to. Works from the bones' own directions, so it does not care how a rig names or turns its
        /// bones; <paramref name="weight"/> blends from the pose the animation gave to the solved one.
        /// </summary>
        static void SolveArm(Transform up, Transform fo, Transform ha, Vector3 target, Vector3 pole, float weight)
        {
            var s = up.position;
            float l1 = (fo.position - s).magnitude, l2 = (ha.position - fo.position).magnitude;
            var to = target - s;
            if (l1 < 1e-4f || l2 < 1e-4f || to.sqrMagnitude < 1e-8f) return;
            float d = Mathf.Clamp(to.magnitude, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.002f);
            var dir = to.normalized;
            float cosA = Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            var perp = Vector3.ProjectOnPlane(pole, dir);
            if (perp.sqrMagnitude < 1e-6f) perp = Vector3.ProjectOnPlane(Vector3.down, dir);
            perp.Normalize();
            var elbow = s + dir * (l1 * cosA) + perp * (l1 * sinA);
            var wrist = s + dir * d;
            up.rotation = Quaternion.Slerp(up.rotation, Quaternion.FromToRotation(fo.position - s, elbow - s) * up.rotation, weight);
            fo.rotation = Quaternion.Slerp(fo.rotation, Quaternion.FromToRotation(ha.position - fo.position, wrist - fo.position) * fo.rotation, weight);
        }
    }
}
