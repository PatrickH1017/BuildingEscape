using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A gun in a character's right hand (a player's or an armed guard's): builds the model on the hand bone and, while the gun is
    /// raised, bends the arm so the barrel points where the owner is aiming. The rigs have no aiming animation, so the right arm is
    /// turned by code after the animation has run each frame (LateUpdate): the upper arm and the forearm point along the aim and the hand
    /// is turned so the gun is level and points exactly that way. With the gun lowered the animation carries it as it is.
    /// Whoever owns the gun sets <see cref="Raise"/> (0 lowered .. 1 raised) and <see cref="AimDirection"/> (a world direction) each frame.
    /// </summary>
    [DefaultExecutionOrder(100)]     // after the animation and the character animator have posed the body; the guard's flashlight (200) follows the muzzle
    public sealed class HeldGun : MonoBehaviour
    {
        const string HandPath = "Armature/Hips/Spine/Spine1/Spine2/RightShoulder/RightArm/RightForeArm/RightHand";
        /// <summary>Degrees the barrel kicks up when a shot is fired.</summary>
        const float KickDegrees = 9f;

        public RangedWeapon.Kind Kind { get; private set; }
        /// <summary>The transform the model hangs on: +Z is the barrel, +Y the top of the gun.</summary>
        public Transform Grip { get; private set; }
        /// <summary>The front of the barrel (where the bullet and the flash come from).</summary>
        public Transform Muzzle { get; private set; }
        public GameObject Model { get; private set; }

        /// <summary>0..1: how far the arm is raised to point the gun along <see cref="AimDirection"/> (smoothed here).</summary>
        public float Raise;
        /// <summary>The world direction the barrel should point in while raised.</summary>
        public Vector3 AimDirection = Vector3.forward;
        /// <summary>The raise reached now (what the arm is doing this frame).</summary>
        public float RaiseNow => raiseNow;
        /// <summary>The rig has a shoulder - elbow - wrist chain this can bend (otherwise the gun just rides on the hand).</summary>
        public bool HasArm => hasArm;
        /// <summary>The bones and lengths of the arm (for the tests and for debugging a new rig).</summary>
        public string ArmInfo() => hand == null ? "no hand"
            : $"{(upper ? upper.name : "?")} > {(fore ? fore.name : "?")} > {hand.name}, upper arm {(fore && upper ? (fore.position - upper.position).magnitude : -1f):0.000} m, forearm {(fore ? (hand.position - fore.position).magnitude : -1f):0.000} m, chain {(hasArm ? "ok" : "NOT used")}";

        Transform upper, fore, hand;
        bool hasArm;
        float raiseNow, kick;

        /// <summary>Puts a gun in the right hand of this character. Null if the hand cannot be found.</summary>
        public static HeldGun Attach(CharacterAnimator anim, RangedWeapon.Kind kind)
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
            held.Model = RangedWeapon.BuildHeld(kind, hand, out var grip, out var muzzle);
            held.Grip = grip;
            held.Muzzle = muzzle;
            grip.localRotation = GripRotation(anim.ClipMode);
            held.FindArm();
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
                if (right.sqrMagnitude > 1e-4f) aim = Quaternion.AngleAxis(-kick * KickDegrees, right.normalized) * aim;
            }

            float b = raiseNow;
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
    }
}
