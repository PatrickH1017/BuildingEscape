using System.Collections.Generic;
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

        // first person (the owner, 2026-10-20): the gun is shouldered, not carried at the chest - the grip a forearm's length in front of the eyes, a little right of the line of sight and below it, the barrel along it (metres from the camera)
        const float FpForward = 0.42f, FpRight = 0.075f, FpDown = 0.215f, FpLeftRoll = 70f;

        /// <summary>How far the camera is at this body's eyes: 0 in third person (and for every other body) up to 1 in first person (a blend of the zoom, so the hold does not pop), and the camera.</summary>
        float FirstPersonShare(out Transform cam)
        {
            cam = null;
            var r = CameraFollow.Current;
            if (!r || !r.ViewedBody || !transform.IsChildOf(r.ViewedBody)) return 0f;
            cam = r.transform;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.95f, r.Zoom));
        }

        // ---- first person, looking THROUGH the sight (the owner, 2026-10-20 night: "when zoomed in all the way to first person I should be looking out the red dot sight / scope / iron sights of whatever weapon it is"):
        // the gun is placed so that its sight (the little dot of the red dot sight, the rear sight of a pistol, the eyepiece of the sniper's scope) is on the line of sight, this far (m) in front of the camera
        // (a gun held in one hand is held at arm's length: its relief is worked out from the arm, see FindSight)
        public const float RedDotRelief = 0.22f, IronRelief = 0.26f, ArmRelief = 0.85f, ArmReliefMin = 0.24f, ArmReliefMax = 0.45f;

        /// <summary>How far the local player's view is through the red dot of the gun in hand (0 in third person, while reloading, with no gun or a gun without a red dot, up to 1): the crosshair gives way to the dot itself.</summary>
        public static float LocalDotShare { get; private set; }

        /// <summary>
        /// What the local player's view shows of the body in first person (CameraFollow asks every frame): 0 nothing, 1 the right arm, reaching out with the pistol in its hand (the owner, 2026-10-20 night: "for the pistol make an arm extended and a hand
        /// holding the gun"). A gun held in both hands shows no arms (the owner: "all the guns look good except the pistol and the sniper").
        /// </summary>
        public static int LocalArmMode { get; private set; }

        /// <summary>
        /// Is the right arm drawn in first person when a pistol is held out in one hand? No since 2026-10-21 (the owner: "the first person mode for the pistol has the character's hands somewhat visible and blocking the weapon model a bit, please make
        /// the hands not visible for the pistol similar to the rifle"): the gun alone is in the picture, as with every other gun (the arm still holds it, out of sight, and casts its shadow). Set it to true to bring the arm back.
        /// </summary>
        public static bool ShowPistolArm = false;

        // ---- the sniper rifle's scope (the owner, 2026-10-20 night: "for the sniper can you make it so we look down the scope and it zooms in a little with a normal crosshair in the scope"):
        // in first person with the rifle up the view narrows to 1/ScopeMagnification of its field, the rifle itself is not drawn (the scope fills the view), and HudWidgets.Scope draws the dark lens with a plain crosshair.
        /// <summary>How many times the scope magnifies (the field of view of the camera is narrowed by this much).</summary>
        public const float ScopeMagnification = 2.5f;

        /// <summary>How far the local player looks down the scope of a sniper rifle (0 with any other gun, in third person, while reloading, up to 1): the camera zooms and the lens is drawn by this much.</summary>
        public static float LocalScope { get; private set; }

        float scopeNow;                                         // the scope view coming in and going out (0..1, by time)
        readonly List<Renderer> scopeHidden = new List<Renderer>();          // the rifle's meshes switched off while looking down the scope

        /// <summary>The rifle is not drawn while the view is down its scope (it would fill the lens), and comes back the moment the view leaves it.</summary>
        void HideForScope(bool hide)
        {
            if (hide)
            {
                if (scopeHidden.Count > 0 || !Grip) return;
                foreach (var r in Grip.GetComponentsInChildren<MeshRenderer>(true)) if (r && r.enabled) { r.enabled = false; scopeHidden.Add(r); }
            }
            else
            {
                foreach (var r in scopeHidden) if (r) r.enabled = true;
                scopeHidden.Clear();
            }
        }

        // ---- a pistol arm that does not shake (the owner, 2026-10-20 night: "the arm is glitching while it moves if you could make it more stiff"): in first person the shoulder is held where the eyes are, not where the running animation
        // and the wobbling body carry it; the animation's own offset of the shoulder from the eyes is followed only slowly (this many times a second), so the whole arm rides the camera
        const float ShoulderFollow = 1.5f, ShoulderMaxMove = 0.15f;
        Vector3 fpShoulder;                  // the shoulder from the eyes, in the frame of the view's heading (metres), smoothed
        bool fpShoulderSet;

        Vector3 sightLocal;                  // the sight, in the grip's own space (+Z the barrel, +Y the top of the gun)
        float sightRelief;
        bool hasSight, sightIsDot, sightSearched;

        /// <summary>Where the sight is on the held model: the red dot sight's dot, else the gun's own sights worked out from the body of the model (the rear sight of a pistol, the eyepiece of the scope on the sniper rifle).</summary>
        void FindSight()
        {
            sightSearched = true; hasSight = false; sightIsDot = false;
            if (!Grip) return;
            var dot = Grip.Find("RedDot/Dot");
            if (dot) { sightLocal = Grip.InverseTransformPoint(dot.position); sightRelief = RedDotRelief; hasSight = true; sightIsDot = true; return; }
            Bounds b = default; bool any = false;
            foreach (Transform c in Grip)
            {
                if (!c.name.EndsWith("_Model")) continue;                      // (the gun's own body: not the silencer, not the muzzle and the other markers)
                foreach (var mf in c.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.sharedMesh) continue;
                    var mb = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                        var p = Grip.InverseTransformPoint(mf.transform.TransformPoint(corner));
                        if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                    }
                }
            }
            if (!any || b.size.z < 0.01f) return;
            // the iron sights (the owner, 2026-10-20 night, about the sniper rifle: "I just want iron sights for now"): the line of sight runs along the top of the gun from a point a sixth of the way along it; on the sniper rifle a little above
            // the top of its scope, so that the scope is below the line and does not fill the screen. The crosshair stays (there is no dot).
            bool sniper = Kind == RangedWeapon.Kind.Sniper;
            sightLocal = new Vector3(b.center.x, b.max.y + (sniper ? 0.06f * b.size.y : 0f), Mathf.Lerp(b.min.z, b.max.z, 0.18f));
            sightRelief = IronRelief;
            if (!twoHand) sightRelief = Mathf.Clamp(armLength * ArmRelief, ArmReliefMin, ArmReliefMax);          // (one hand: the gun is held out at arm's length, the arm in the picture)
            hasSight = true;
        }

        /// <summary>
        /// The grip's place that puts the sight on the line of sight (<paramref name="f"/>: the way the view looks) <see cref="sightRelief"/> in front of the camera; for a gun whose sight is not known, the shouldered hold of before.
        /// </summary>
        Vector3 FirstPersonGrip(Transform cam, Vector3 f, Vector3 right, Vector3 up, Quaternion gunRot, float oneHandForward = 1f)
        {
            if (hasSight) return cam.position + f * sightRelief - gunRot * Vector3.Scale(sightLocal, Grip.lossyScale);
            return cam.position + f * (FpForward * oneHandForward) + right * FpRight - up * FpDown;
        }

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
            HideForScope(false);
            LocalDotShare = 0f; LocalArmMode = 0; LocalScope = 0f;
            Destroy(this);
        }

        void LateUpdate()
        {
            if (!Grip || !hasArm) return;
            raiseNow = Mathf.MoveTowards(raiseNow, Mathf.Clamp01(Raise), Time.deltaTime * 6f);
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);
            if (!sightSearched) FindSight();
            // first person with the gun up (not while it is being reloaded, which tilts it): the barrel points where the view does, so that the sight is on the line of sight
            float fpShare = FirstPersonShare(out var viewCamera);
            float ads = viewCamera && hasSight ? fpShare * Mathf.InverseLerp(0.92f, 1f, raiseNow) : 0f;
            if (viewCamera) { LocalDotShare = sightIsDot ? ads : 0f; LocalArmMode = ShowPistolArm && !twoHand && fpShare > 0.5f ? 1 : 0; }
            // the scope: only the sniper rifle, only at the eye with the rifle fully up; it eases in and out (a quarter of a second)
            float scopeWant = viewCamera && Kind == RangedWeapon.Kind.Sniper && ads > 0.99f ? 1f : 0f;
            scopeNow = Mathf.MoveTowards(scopeNow, scopeWant, Time.deltaTime * 4f);
            if (viewCamera) LocalScope = Mathf.SmoothStep(0f, 1f, scopeNow);
            else if (scopeNow > 0f) { scopeNow = 0f; LocalScope = 0f; }           // (the view left this body)
            HideForScope(scopeNow > 0.5f);
            if (raiseNow < 0.001f) { fpShoulderSet = false; return; }

            var aim = AimDirection;
            if (aim.sqrMagnitude < 1e-4f) aim = transform.forward;
            aim.Normalize();
            if (ads > 0.001f) aim = Vector3.Slerp(aim, viewCamera.forward, ads).normalized;
            // never straight up or down: the gun's "up" is undefined there
            if (Mathf.Abs(aim.y) > 0.97f) aim = new Vector3(aim.x, Mathf.Sign(aim.y) * 0.97f, aim.z).normalized;
            if (kick > 0f)
            {
                var right = Vector3.Cross(Vector3.up, aim);
                if (right.sqrMagnitude > 1e-4f) aim = Quaternion.AngleAxis(-kick * RangedWeapon.Specs[(int)Kind].KickDegrees, right.normalized) * aim;
            }

            float b = raiseNow;
            if (twoHand) { TwoHandPose(aim, b); return; }
            float fp1 = FirstPersonShare(out var cam1);
            if (fp1 > 0.001f) { OneHandShouldered(aim, b, fp1, cam1); return; }
            fpShoulderSet = false;
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

        /// <summary>A pistol in first person: held out in the right hand in front of the eyes, a little low, the barrel along the line of sight.</summary>
        void OneHandShouldered(Vector3 aim, float b, float fp, Transform cam)
        {
            // the stiff arm: the shoulder rides the eyes (see ShoulderFollow) - measured in the view's heading, so looking up and down does not move it, then carried with the camera's full turn
            var yawRot = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
            var rawShoulder = Quaternion.Inverse(yawRot) * (upper.position - cam.position);
            if (!fpShoulderSet) { fpShoulder = rawShoulder; fpShoulderSet = true; }
            else fpShoulder = Vector3.Lerp(fpShoulder, rawShoulder, 1f - Mathf.Exp(-ShoulderFollow * Time.deltaTime));
            upper.position += Vector3.ClampMagnitude(cam.position + cam.rotation * fpShoulder - upper.position, ShoulderMaxMove) * fp;
            var right = Vector3.Cross(Vector3.up, aim);
            if (right.sqrMagnitude < 1e-4f) right = transform.right;
            right.Normalize();
            var up = Vector3.Cross(aim, right);
            var gunRot = Quaternion.LookRotation(aim, up);
            var carried = upper.position + aim * (0.9f * armLength);                                  // (the third-person hold: the arm straight out along the aim)
            var held = hasSight ? FirstPersonGrip(cam, aim, right, up, gunRot) : cam.position + aim * (FpForward * 1.2f) + right * (FpRight + 0.04f) - up * (FpDown - 0.02f);
            SolveArm(upper, fore, hand, Vector3.Lerp(carried, held, fp), -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
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
            float fp = FirstPersonShare(out var viewCam);
            if (fp > 0.001f) pr = Vector3.Lerp(pr, FirstPersonGrip(viewCam, f, right, up, gunRot), fp);          // (first person: the sight on the line of sight, in the middle of the screen)
            var tl = pr + toForegrip;
            if (fp > 0.001f) tl += (-right * 0.075f + up * 0.085f) * fp;                               // (first person: the left hand grips the side of the handguard, so that its forearm is seen reaching in from the left)
            SolveArm(upper, fore, hand, pr, -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
            SolveArm(lUpper, lFore, lHand, tl, -up * 0.8f - right * 0.6f, b);
            lHand.rotation = Quaternion.Slerp(lHand.rotation, gunRot * Quaternion.Euler(0f, 0f, LeftRoll + FpLeftRoll * fp) * MirrorX(Quaternion.Inverse(Grip.localRotation)), b);
            LeftHandGap = (lHand.position - Foregrip.position).magnitude;
        }

        /// <summary>The mirror image of a rotation across the plane the gun's right and left sides are on: the left hand is the right one seen in a mirror.</summary>
        static Quaternion MirrorX(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

        /// <summary>
        /// A two-bone arm: the wrist goes to <paramref name="target"/> (as near as the arm reaches) and the elbow to the side the pole points to. Works from the bones' own directions, so it does not care how a rig names or turns its
        /// bones; <paramref name="weight"/> blends from the pose the animation gave to the solved one.
        /// </summary>
        public static void SolveArm(Transform up, Transform fo, Transform ha, Vector3 target, Vector3 pole, float weight)
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
