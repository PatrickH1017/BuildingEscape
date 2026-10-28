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
    /// A PLAYER's pistol is held with the dock worker's Blender animation instead (the owner, 2026-10-27; <see cref="Animated"/>, <see cref="PistolAnimation"/>): the raise, the aim, the shot and the reload are clips on the upper body,
    /// the pistol sits in the hand where the Blender files hold it, and the code only turns the chest a little and the arm at the shoulder so that the barrel points at the crosshair (the shot's kick and the reload stay as they were made).
    /// The long guns and the officers' pistol have their Blender animations too since the same day (<see cref="WeaponAnimation"/>): a long gun is held in both hands as its file holds it (the low ready, the raise, the aim, the shot) and
    /// turned to the crosshair with both arms; first person keeps the sight picture of before.
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
        /// <summary>First person, the left hand grips the side of the handguard (not the foregrip itself): this far (m) to the left and up of it, in the gun's own frame.</summary>
        static readonly Vector3 FpLeftShift = new Vector3(-0.075f, 0.085f, 0f);

        // ---- the Kinemation "FPS Shooter Aiming" pack (the owner, 2026-10-23: "make it so the fps shooting in game when you zoom in uses this asset pack for shooting aiming and reloading"): see SightsAimLayer and ReloadMotionRules
        /// <summary>First person: put the gun's sight on the line of sight with the pack's solver (<see cref="SightsAimLayer"/>, the pack's two-bone IK in <c>AnimToolkitLib</c>). False: the direct arm solve of before. It also takes over by itself, frame by frame, when the layer cannot do it.</summary>
        public static bool UseSightsAligner = true;
        /// <summary>First person: show a reload by moving the gun out of the sight line and back (<see cref="ReloadMotionRules"/>; the pack has no reload animation, so this is made here). False: the gun is tilted up as in third person. Needs <see cref="UseSightsAligner"/>.</summary>
        public static bool UseReloadMotion = true;

        /// <summary>0 = the gun is not being reloaded, else how far the reload has got (0..1): the owner's controller sets it every frame; only first person uses it.</summary>
        public float ReloadProgress;
        /// <summary>Where the sounds of this gun's reload fall as shares of it (<see cref="ReloadMotionRules.StepFractions"/>), so the motion keeps time with them; null = the rifle's.</summary>
        public float[] ReloadSteps;
        /// <summary>The reload is shown by moving the gun from the sight line (first person, a gun with a sight, the layer working): the controller keeps the gun aimed then instead of tilting it up.</summary>
        public bool FirstPersonReload => !(animated && !twoHand) && UseReloadMotion && AlignerAllowed && hasSight && fpShareNow > 0.5f;
        /// <summary>The layer is switched on, has not failed for good, and is not resting after a frame it could not do (see <see cref="NoteFallback"/>).</summary>
        bool AlignerAllowed => UseSightsAligner && !(aimLayer != null && aimLayer.Failed) && Time.time >= alignerRetryAt;
        float alignerRetryAt;                    // after a frame the layer gave back with the gun fully up: no more tries until this time (the old solve and the old reload tilt run meanwhile)
        /// <summary>How far the gun is down out of the sight line for the reload this frame (0..1; for the tests).</summary>
        public float ReloadLowered => reloadNow.lowered;
        /// <summary>Was the last first-person frame aimed by the pack's layer (false: the direct solve did it, or it is not first person)? For the tests.</summary>
        public bool AlignerUsed { get; private set; }
        /// <summary>The aiming layer, once it has been needed (for the tests and the pictures).</summary>
        public SightsAimLayer AimLayer => aimLayer;
        SightsAimLayer aimLayer;
        bool alignerLogged, fallbackLogged;      // (one line in the console, once per gun, when the layer first aims and when it first gives a frame back)
        float fpShareNow, reloadWeight;
        ReloadMotionRules.Pose reloadPose;      // the reload's offset as the controller's progress gave it (kept while it eases out)
        ReloadMotionRules.Pose reloadNow;       // ... scaled by how far this is first person and how far the motion is in: what the gun does this frame

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

        /// <summary>
        /// Puts a gun in the hands of this character. Null if the right hand cannot be found. <paramref name="animated"/>: the gun is held with its Blender animation when the rig can play it (<see cref="Animated"/>; the
        /// pistol: the dock worker's clips, or the officers' when <paramref name="guard"/>; a long gun: its own clips). Otherwise it is posed by code as before 2026-10-27.
        /// </summary>
        public static HeldGun Attach(CharacterAnimator anim, RangedWeapon.Kind kind, bool silenced = false, bool animated = false, bool guard = false)
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
            if (animated) held.SetUpAnimated(anim, guard);
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

        /// <summary>A shot was fired: the barrel kicks up (a held pistol of the Blender animation plays its shot).</summary>
        public void Kick() { kick = 1f; lastShot = Time.time; }

        /// <summary>Take the gun away (the model is destroyed).</summary>
        public void Remove()
        {
            if (animated) EndAnimated();
            if (Grip) Destroy(Grip.gameObject);
            if (aimLayer != null) { aimLayer.Dispose(); aimLayer = null; }
            HideForScope(false);
            LocalDotShare = 0f; LocalArmMode = 0; LocalScope = 0f;
            Destroy(this);
        }

        /// <summary>
        /// The reload motion of this frame: the offset of <see cref="ReloadMotionRules.Evaluate"/> at the reload's progress, in first person only and by how far the view is at the eyes. It comes in at once with the reload and, when the reload
        /// is over or stopped, eases out in a sixth of a second (a stopped one: the gun is switched, the body is knocked down).
        /// </summary>
        void UpdateReload(float fpShare, bool viewed)
        {
            bool want = viewed && UseReloadMotion && AlignerAllowed && hasSight && ReloadProgress > 0f && ReloadProgress < 1f;
            if (want) { reloadPose = ReloadMotionRules.Evaluate(ReloadProgress, ReloadSteps); reloadWeight = 1f; }
            else reloadWeight = Mathf.MoveTowards(reloadWeight, 0f, Time.deltaTime * 6f);
            reloadNow = ReloadMotionRules.Scale(reloadPose, reloadWeight * fpShare);
            if (reloadWeight <= 0f) reloadPose = default(ReloadMotionRules.Pose);
        }

        void LateUpdate()
        {
            if (!Grip || !hasArm) return;
            if (animated && !twoHand) { AnimatedLateUpdate(); return; }
            if (animated) AnimatedTwoHand();                       // (a long gun with its animation: the clip's hold turned to the aim; first person goes on below from that pose)
            AlignerUsed = false;
            if (!animated) raiseNow = Mathf.MoveTowards(raiseNow, Mathf.Clamp01(Raise), Time.deltaTime * 6f);
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);
            if (!sightSearched) FindSight();
            // first person with the gun up (not while it is being reloaded, which tilts it): the barrel points where the view does, so that the sight is on the line of sight
            float fpShare = FirstPersonShare(out var viewCamera);
            fpShareNow = fpShare;
            UpdateReload(fpShare, viewCamera != null);
            float ads = viewCamera && hasSight ? fpShare * Mathf.InverseLerp(0.92f, 1f, raiseNow) : 0f;
            if (viewCamera) { LocalDotShare = sightIsDot ? ads * (1f - reloadNow.lowered) : 0f; LocalArmMode = ShowPistolArm && !twoHand && fpShare > 0.5f ? 1 : 0; }
            // the scope: only the sniper rifle, only at the eye with the rifle fully up; it eases in and out (a quarter of a second)
            float scopeWant = viewCamera && Kind == RangedWeapon.Kind.Sniper && ads > 0.99f && reloadNow.lowered < 0.05f ? 1f : 0f;          // (not while the rifle is down for its reload)
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
            if (animated) { b = (stances ? AimRules.AimShare(raiseNow) : b) * fpShare; if (b < 0.001f) return; }          // (an animated long gun: the hold below only in first person, by how far the view is at the eyes)
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
            AlignerUsed = false;
            if (hasSight && AlignedOneHand(cam, aim, right, up, gunRot, carried, b, fp)) return;
            var held = hasSight ? FirstPersonGrip(cam, aim, right, up, gunRot) : cam.position + aim * (FpForward * 1.2f) + right * (FpRight + 0.04f) - up * (FpDown - 0.02f);
            SolveArm(upper, fore, hand, Vector3.Lerp(carried, held, fp), -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
        }

        // ------------------------------------------------------------------ a player's pistol: the dock worker's Blender animation (the owner, 2026-10-27; see PistolAnimation)
        // The clips pose the upper body (the raise, the aim, the shot, the reload); this only turns the chest a little and the arm at the shoulder so that the barrel points at the crosshair. The turn is measured from the AIM
        // as the clips have it, not from the frame itself, so the shot's kick and the sway of the run stay in the picture. In first person the arm is in the picture (it holds the gun out, low and to the right of the
        // crosshair) and the view no longer goes through the sights: the owner, 2026-10-27, "you can remove that for now if it doesnt work with the animation".

        bool animated;
        bool stances, faceKnown;             // the gun is held with the stance loops (2026-10-27: the barrel is the loop's own, the kick is made here); the head's face direction is known (the stances read the barrel from the hand as it is)
        CharacterAnimator character;
        WeaponAnimation.GunClips gunClips;
        Transform clavicle, leftClavicle, chest, head, slide, magazine, pump;
        Vector3 upperRest, clavicleRest, leftClavicleRest, slideRest, magRestPos, pumpRest;
        Quaternion magRestRot;
        float slideLength, raiseSeconds = 0.4f, shotSeconds = 0.233f, lastShot = -100f;
        Quaternion sUpper, sFore, sHand;                     // the arm's bones and the way the face looks in the aim as the clips have it (see AnimatedLateUpdate)
        Vector3 faceLocal;
        bool steadyKnown, magDropped;

        /// <summary>This pistol is held with the dock worker's Blender animation (a player's pistol on a rig that can play it).</summary>
        public bool Animated => animated;
        /// <summary>The gun has no round left: its slide stays back (a held pistol of the Blender animation shows it) until a reload's slide release. The owner's controller sets it.</summary>
        public bool SlideLocked;

        /// <summary>How much of the turn to the crosshair the chest takes (the head goes with it; at most this many degrees), the arm at the shoulder the rest; how much of what is left the head looks along, in third person.</summary>
        const float ChestShare = 0.35f, ChestMaxDegrees = 30f, HeadLook = 0.6f;
        /// <summary>
        /// First person, while the magazine is changed: both shoulders are lifted this far up and forward (in units of the character's size), so that the hands and the gun come up into the picture. The reload was made for
        /// a character who looks down at his hands; the first-person view looks straight ahead, and without this the hands would be below the bottom of the screen. Only the arms are drawn in first person.
        /// </summary>
        static readonly Vector2 FpReloadLift = new Vector2(0.28f, 0.08f);
        /// <summary>First person: how fast (per second) the shoulder's place under the eyes follows the body (slower than the old hold's <see cref="ShoulderFollow"/>: the walk and the wobble of the body are averaged out, the pistol held out stays put in the picture).</summary>
        const float AnimatedShoulderFollow = 0.4f;
        // the reload's moments (the Blender file's frames 1-66 over the reload; see pistol_reload.py): the empty magazine leaves the grip (13), the new one is in the left hand (23) until it is seated (37), the left hand pulls
        // the slide (44-47) and lets it go (48). The slide is locked back by SlideLock of its length while the gun is empty and pulled a further SlidePull; a shot throws it back by SlideKick for a frame.
        const float ReloadFrames = 65f, MagOutFrame = 13f, MagInHandFrame = 23f, MagSeatedFrame = 37f, RackFrom = 44f, RackPulled = 47f, RackReleased = 48f;
        const float SlideLock = 0.26f, SlidePull = 0.05f, SlideKick = 0.2f;
        // the new magazine in the left hand (frames 23-36): its origin along the left hand's f, u and palm (in units of the character's size), and its long axis (+Y) and its +Z in the same frame
        static readonly Vector3 MagInHand = new Vector3(0.04521f, 0f, 0.08995f), MagInHandY = new Vector3(-0.1378f, 0f, 0.9905f), MagInHandZ = new Vector3(0.9905f, 0f, 0.1378f);
        /// <summary>The way the empty magazine leaves the grip, in the gun's frame (+Z the barrel, +Y the top): out of the bottom and a little back, along the grip.</summary>
        static readonly Vector3 MagEject = new Vector3(0f, -0.9905f, -0.1378f);
        float size = 1f;
        Animator animator;

        void SetUpAnimated(CharacterAnimator anim, bool guard)
        {
            if (!hasArm || !anim || !anim.CanPlayWeapons) return;
            var an = anim.GetComponent<Animator>();
            var model = Grip.Find(RangedWeapon.Specs[(int)Kind].name + "_Model");
            float s = armLength / PistolAnimation.ArmAtSizeOne;
            if (!an || !model) return;
            WeaponAnimation.GunClips clips;
            if (Kind == RangedWeapon.Kind.Pew)
            {
                clips = guard ? (WeaponAnimation.GunsEnabled ? WeaponAnimation.Guard() : null) : (PistolAnimation.Enabled ? PistolAnimation.Load() : null);
                if (clips == null || !PistolAnimation.PlaceInHand(an, Grip, model, s)) return;
            }
            else
            {
                clips = WeaponAnimation.GunsEnabled && twoHand ? WeaponAnimation.Gun(Kind) : null;           // (a long gun the body could not hold in both hands keeps the code's hold)
                var fitted = Grip.localScale;
                if (clips == null || !WeaponAnimation.PlaceGun(an, Grip, model, WeaponAnimation.GunHold(Kind), s)) return;
                gripScaleFitted = fitted; gripScaleBlender = Grip.localScale;
            }
            animated = true;
            gunClips = clips;
            character = anim;
            animator = an;
            size = s;
            raiseSeconds = clips.raise.length;
            stances = clips.Stances;
            if (stances && Kind == RangedWeapon.Kind.Pew) { FindLeftArm(anim); twoHand = false; }          // (the stances hold the pistol in both hands: the left arm follows the gun; for the rest of this class it is still a one-hand gun)
            shotSeconds = clips.fire.length;
            clavicle = upper.parent;
            leftClavicle = an.GetBoneTransform(HumanBodyBones.LeftShoulder);
            chest = an.GetBoneTransform(HumanBodyBones.UpperChest);
            if (!chest) chest = an.GetBoneTransform(HumanBodyBones.Chest);
            head = an.GetBoneTransform(HumanBodyBones.Head);
            upperRest = upper.localPosition;
            if (clavicle) clavicleRest = clavicle.localPosition;
            if (leftClavicle) leftClavicleRest = leftClavicle.localPosition;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (!slide && t.name.Contains(WeaponAnimation.BoltPart)) slide = t;
                if (!magazine && t.name.Contains("_Mag")) magazine = t;
                if (!pump && t.name.Contains(WeaponAnimation.PumpPart)) pump = t;
            }
            if (pump) pumpRest = pump.localPosition;
            if (slide)
            {
                slideRest = slide.localPosition;
                var mf = slide.GetComponent<MeshFilter>();
                slideLength = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size.z * slide.localScale.z : 0f;
            }
            if (magazine) { magRestPos = magazine.localPosition; magRestRot = magazine.localRotation; }
        }

        /// <summary>The gun goes: the shoulders are put back where the skeleton has them (a first-person frame moves them).</summary>
        void EndAnimated() => PutShouldersBack();

        /// <summary>The joints a first-person frame moves (the shoulder that holds the pistol out, both shoulders lifted for a reload) back where the skeleton has them: the animation turns them but does not move them back.</summary>
        void PutShouldersBack()
        {
            if (upper) upper.localPosition = upperRest;
            if (clavicle) clavicle.localPosition = clavicleRest;
            if (leftClavicle) leftClavicle.localPosition = leftClavicleRest;
        }

        /// <summary>Before the animation runs: the raise moves at the speed of the raise clip, and the character animator is told what to show.</summary>
        void Update()
        {
            if (!animated || !Grip) return;
            raiseNow = Mathf.MoveTowards(raiseNow, Mathf.Clamp01(Raise), Time.deltaTime / Mathf.Max(0.05f, raiseSeconds));
            if (character) character.GunPose(gunClips, raiseNow, Time.time - lastShot, twoHand ? 0f : ReloadProgress);
        }

        void AnimatedLateUpdate()
        {
            AlignerUsed = false;
            PutShouldersBack();                                                         // (the first-person shoulders of the frame before: nothing else puts them back)
            float fp = FirstPersonShare(out var cam);
            fpShareNow = fp;
            float layer = character ? character.GunWeight : 0f;
            float reloadW = character ? character.GunReloadWeight : 0f;
            bool both = stances && lHand && lFore && lUpper;                            // (the stances hold the pistol in both hands: the left hand stays on the gun)
            if (cam) { LocalDotShare = 0f; LocalScope = 0f; LocalArmMode = fp > 0.5f ? (reloadW > 0.01f || both ? 2 : 1) : 0; }          // (first person: the arm that holds the pistol out, the left one too while it changes the magazine or holds the gun)
            SlideAndMagazine();
            float kickNow = kick;
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 9f);
            var leftOnGun = both ? Grip.InverseTransformPoint(lHand.position) : Vector3.zero;                  // (the left hand's place on the gun as the clips have it, before anything turns)
            var leftRotOnGun = both ? Quaternion.Inverse(Grip.rotation) * lHand.rotation : Quaternion.identity;
            if (fp > 0.001f && reloadW > 0.001f)
            {
                var lift = (Vector3.up * FpReloadLift.x + Quaternion.Euler(0f, cam.eulerAngles.y, 0f) * Vector3.forward * FpReloadLift.y) * (size * fp * reloadW);
                if (clavicle) clavicle.position += lift;
                if (leftClavicle) leftClavicle.position += lift;
            }
            // the aim as the clips have it, whenever it is shown just so (raised, no shot, no reload); the stances have no kick of their own, so the barrel is read from the hand as it is
            if (!stances && raiseNow >= 0.999f && layer > 0.999f && reloadW < 0.001f && Time.time - lastShot >= shotSeconds)
            {
                sUpper = upper.localRotation; sFore = fore.localRotation; sHand = hand.localRotation;
                if (head) faceLocal = Quaternion.Inverse(head.rotation) * transform.forward;
                steadyKnown = true;
            }
            else if (stances && head && raiseNow >= 0.999f && layer > 0.999f && reloadW < 0.001f && character.RunShare < 0.05f)
            {
                faceLocal = Quaternion.Inverse(head.rotation) * transform.forward;
                faceKnown = true;
            }
            float w = (stances ? AimRules.HipShare(raiseNow) : raiseNow) * layer * (1f - reloadW);          // (the reload is shown as it was made: the gun is not aimed meanwhile)
            if (w < 0.001f) { fpShoulderSet = false; return; }
            var aim = AimDirection.sqrMagnitude > 1e-4f ? AimDirection.normalized : transform.forward;
            if (Mathf.Abs(aim.y) > 0.97f) aim = new Vector3(aim.x, Mathf.Sign(aim.y) * 0.97f, aim.z).normalized;
            if (stances && kickNow > 0f)
            {
                var kr = Vector3.Cross(Vector3.up, aim);
                if (kr.sqrMagnitude > 1e-4f) aim = Quaternion.AngleAxis(-kickNow * RangedWeapon.Specs[(int)Kind].KickDegrees, kr.normalized) * aim;          // (the shot's kick: the barrel jumps up and comes back)
            }
            if (chest)
            {
                Quaternion.FromToRotation(SteadyBarrel(), aim).ToAngleAxis(out float deg, out var axis);
                if (deg > 180f) deg -= 360f;
                if (axis.sqrMagnitude > 0.5f) chest.rotation = Quaternion.AngleAxis(Mathf.Clamp(deg * ChestShare, -ChestMaxDegrees, ChestMaxDegrees) * w, axis) * chest.rotation;
            }
            upper.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(SteadyBarrel(), aim), w) * upper.rotation;
            if (head && (stances ? faceKnown : steadyKnown) && fp < 0.5f)
                head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(head.rotation * faceLocal, aim), HeadLook * w * (stances ? AimRules.AimShare(raiseNow) : 1f)) * head.rotation;
            if (fp > 0.001f) StiffShoulder(cam, fp * w);
            if (both)
            {
                var lTarget = Grip.TransformPoint(leftOnGun);
                float lArm = (lFore.position - lUpper.position).magnitude + (lHand.position - lFore.position).magnitude - 0.004f;
                var toLeft = lTarget - lUpper.position;
                if (toLeft.magnitude - lArm > 0.002f)
                {
                    // the left arm cannot reach the gun where it is turned to now (running): the gun is brought towards the left shoulder by what the arm lacks, by the right arm, which has the slack
                    var keepRot = hand.rotation;
                    SolveArm(upper, fore, hand, hand.position - toLeft.normalized * Mathf.Min(toLeft.magnitude - lArm, 0.12f), fore.position - (upper.position + hand.position) * 0.5f, w);
                    hand.rotation = keepRot;
                    lTarget = Grip.TransformPoint(leftOnGun);
                }
                SolveArm(lUpper, lFore, lHand, lTarget, lFore.position - (lUpper.position + lHand.position) * 0.5f, w);
                lHand.rotation = Quaternion.Slerp(lHand.rotation, Grip.rotation * leftRotOnGun, w);
                LeftHandGap = (lHand.position - lTarget).magnitude;
            }
        }

        /// <summary>The barrel's direction in the aim as the clips have it, carried by the body as it is now (before the aim is known: the barrel as it is).</summary>
        Vector3 SteadyBarrel()
        {
            var handRot = steadyKnown && clavicle ? clavicle.rotation * sUpper * sFore * sHand : hand.rotation;
            return handRot * (Grip.localRotation * Vector3.forward);
        }

        /// <summary>First person: the shoulder rides the eyes (its offset from them followed slowly, see AnimatedShoulderFollow), so the pistol held out does not bob with the walk and the wobble of the body.</summary>
        void StiffShoulder(Transform cam, float weight)
        {
            var yawRot = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
            var raw = Quaternion.Inverse(yawRot) * (upper.position - cam.position);
            if (!fpShoulderSet) { fpShoulder = raw; fpShoulderSet = true; }
            else fpShoulder = Vector3.Lerp(fpShoulder, raw, 1f - Mathf.Exp(-AnimatedShoulderFollow * Time.deltaTime));
            upper.position += Vector3.ClampMagnitude(cam.position + cam.rotation * fpShoulder - upper.position, ShoulderMaxMove) * weight;
        }

        /// <summary>
        /// The pistol's moving parts as the Blender files have them: the slide thrown back by a shot, held back while the gun is empty and let go by the reload's slide release; the magazine that drops out of the grip, the new one
        /// in the left hand and then in the grip.
        /// </summary>
        void SlideAndMagazine()
        {
            bool reloading = ReloadProgress > 0f && ReloadProgress < 1f;
            float frame = 1f + Mathf.Clamp01(ReloadProgress) * ReloadFrames;
            if (slide && slideLength > 0f)
            {
                float back = 0f, t = Time.time - lastShot;
                if (t >= 0f && t < 1f / 30f) back = SlideKick * t * 30f;
                else if (t >= 1f / 30f && t < 5f / 30f) back = SlideKick * (5f / 30f - t) * 7.5f;
                if (reloading)
                {
                    float held = SlideLocked ? SlideLock : 0f;
                    if (frame < RackFrom) back = Mathf.Max(back, held);
                    else if (frame < RackPulled) back = Mathf.Lerp(held, SlideLock + SlidePull, (frame - RackFrom) / (RackPulled - RackFrom));
                    else if (frame < RackReleased) back = SlideLock + SlidePull;
                    else back = 0f;
                }
                else if (SlideLocked) back = Mathf.Max(back, SlideLock);
                slide.localPosition = slideRest + Vector3.back * (back * slideLength);
            }
            if (!magazine) return;
            if (reloading && frame >= MagOutFrame && frame < MagSeatedFrame)
            {
                if (!magDropped)
                {
                    magDropped = true;
                    if (frame < MagInHandFrame) DroppedMagazine.Drop(magazine, Grip.TransformDirection(MagEject).normalized, Grip.right);          // (a reload picked up again later, after a stumble, drops none)
                }
                Vector3 w = default, f = default, u = default, palm = default;
                bool inHand = frame >= MagInHandFrame && PistolAnimation.HandFrame(animator, out w, out f, out u, out palm, true);
                magazine.gameObject.SetActive(inHand);
                if (inHand)
                {
                    magazine.position = w + (f * MagInHand.x + u * MagInHand.y + palm * MagInHand.z) * size;
                    magazine.rotation = Quaternion.LookRotation(f * MagInHandZ.x + u * MagInHandZ.y + palm * MagInHandZ.z, f * MagInHandY.x + u * MagInHandY.y + palm * MagInHandY.z);
                }
            }
            else if (magDropped || !magazine.gameObject.activeSelf)
            {
                magDropped = reloading && frame >= MagOutFrame;                        // (a reload that is still on keeps its dropped magazine dropped; a new one drops again)
                magazine.gameObject.SetActive(true);
                magazine.localPosition = magRestPos; magazine.localRotation = magRestRot;
            }
        }

        // ------------------------------------------------------------------ a long gun with its Blender animation (the owner, 2026-10-27; see WeaponAnimation)
        // The clip holds the gun in both hands (the low ready, the raise, the aim, a shot). Third person: the chest takes a share of the turn to the crosshair, and the gun turns the rest about the middle of the shoulders as one piece with
        // both hands on it - the arms are solved to the hands' new places, the left hand where the clip has it on the gun. The turn is measured from the aim AS THE CLIP HAS IT (as the pistol's), so a shot's kick stays in the picture.
        // First person goes on to the hold of before from this pose (the sight on the line of sight, the reload motion, the scope), by how far the view is at the eyes.

        /// <summary>How much of the turn to the crosshair the chest takes when a long gun is held (at most this many degrees); the gun and both arms take the rest.</summary>
        const float TwoHandChestShare = 0.5f, TwoHandChestMaxDegrees = 35f;
        Vector3 steadyBarrelLocal;           // the barrel in the aim as the clip has it, in the chest's frame
        Vector3 gripScaleFitted, gripScaleBlender;          // the gun's size as the code's hold fits it to the arms (first person), and as the Blender file has it for this character (third person: both hands on it)

        void AnimatedTwoHand()
        {
            LongGunParts();
            float layer = character ? character.GunWeight : 0f;
            float fp = FirstPersonShare(out _);
            // first person shows the gun at the size the first-person view was made for (2026-10-20/23; the arms are not drawn there), third person at the file's size, which both hands fit: it grows about the grip as the view goes to the eyes
            Grip.localScale = Vector3.Lerp(gripScaleBlender, gripScaleFitted, fp);
            bool steadyAim = raiseNow >= 0.999f && layer > 0.999f && Time.time - lastShot >= shotSeconds;
            // the barrel as the clip has it: the stance loops have no kick of their own, so it is read from the gun as it is this frame; the raise / aim clips of before: as it is when aimed and still (so that a shot's kick stays in the picture)
            if (chest && (stances || steadyAim)) { steadyBarrelLocal = Quaternion.Inverse(chest.rotation) * Grip.forward; steadyKnown = true; }
            if (chest && head && steadyAim && (!stances || character.RunShare < 0.05f)) { faceLocal = Quaternion.Inverse(head.rotation) * transform.forward; faceKnown = true; }
            float w = (stances ? AimRules.HipShare(raiseNow) : raiseNow) * layer;
            if (w < 0.001f || !chest || !lHand) { LeftHandGap = 0f; return; }
            var aim = AimDirection.sqrMagnitude > 1e-4f ? AimDirection.normalized : transform.forward;
            if (Mathf.Abs(aim.y) > 0.97f) aim = new Vector3(aim.x, Mathf.Sign(aim.y) * 0.97f, aim.z).normalized;
            if (stances && kick > 0f)
            {
                var kr = Vector3.Cross(Vector3.up, aim);
                if (kr.sqrMagnitude > 1e-4f) aim = Quaternion.AngleAxis(-kick * RangedWeapon.Specs[(int)Kind].KickDegrees, kr.normalized) * aim;          // (the shot's kick: the barrel jumps up and comes back)
            }
            Quaternion.FromToRotation(SteadyLongBarrel(), aim).ToAngleAxis(out float deg, out var axis);
            if (deg > 180f) deg -= 360f;
            if (axis.sqrMagnitude > 0.5f) chest.rotation = Quaternion.AngleAxis(Mathf.Clamp(deg * TwoHandChestShare, -TwoHandChestMaxDegrees, TwoHandChestMaxDegrees) * w, axis) * chest.rotation;
            // the left hand's place on the gun as the clip has it, before anything turns
            var leftOnGun = Grip.InverseTransformPoint(lHand.position);
            var leftRotOnGun = Quaternion.Inverse(Grip.rotation) * lHand.rotation;
            var q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(SteadyLongBarrel(), aim), w);
            var pivot = (upper.position + lUpper.position) * 0.5f;
            var rPole = q * (fore.position - (upper.position + hand.position) * 0.5f);
            var lPole = q * (lFore.position - (lUpper.position + lHand.position) * 0.5f);
            var rTarget = pivot + q * (hand.position - pivot);
            var rRot = q * hand.rotation;
            if (stances)
            {
                // the left arm is nearly straight on a foregrip: turning the gun about the shoulders can put the foregrip beyond its reach (running, the body wobbles and the turn is larger): the gun is moved towards the left shoulder by what the arm lacks (the right arm has the slack)
                var lPred = pivot + q * (lHand.position - pivot);
                float lReach = (lFore.position - lUpper.position).magnitude + (lHand.position - lFore.position).magnitude - 0.004f;
                var toL = lPred - lUpper.position;
                float lack = toL.magnitude - lReach;
                if (lack > 0f) rTarget -= toL.normalized * Mathf.Min(lack, 0.12f);
            }
            SolveArm(upper, fore, hand, rTarget, rPole, 1f);
            hand.rotation = rRot;
            var lTarget = Grip.TransformPoint(leftOnGun);
            SolveArm(lUpper, lFore, lHand, lTarget, lPole, 1f);
            lHand.rotation = Grip.rotation * leftRotOnGun;
            LeftHandGap = (lHand.position - lTarget).magnitude;
            if (head && (stances ? faceKnown : steadyKnown) && fp < 0.5f)
                head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(head.rotation * faceLocal, aim), HeadLook * w * (stances ? AimRules.AimShare(raiseNow) : 1f)) * head.rotation;
        }

        /// <summary>The barrel's direction in the aim as the clip has it, carried by the chest as it is now (before the aim is known: the barrel as it is).</summary>
        Vector3 SteadyLongBarrel() => steadyKnown && chest ? chest.rotation * steadyBarrelLocal : Grip.forward;

        /// <summary>A long gun's moving parts as the Blender files move them: the bolt thrown back by a shot (the SMG, the rifle, the sniper rifle), the shotgun's pump and bolt worked after every shot.</summary>
        void LongGunParts()
        {
            float t = Time.time - lastShot;
            if (slide)
            {
                float back = Kind == RangedWeapon.Kind.Shotgun ? WeaponAnimation.PumpBoltTravel * WeaponAnimation.PumpOut(t) : WeaponAnimation.BoltKick[(int)Kind] * WeaponAnimation.BoltOut(t);
                slide.localPosition = slideRest + Vector3.back * back;
            }
            if (pump) pump.localPosition = pumpRest + Vector3.back * (WeaponAnimation.PumpTravel * WeaponAnimation.PumpOut(t));
        }

        /// <summary>How far the shotgun's pump is back this frame (0..1; for the tests).</summary>
        public float PumpBack => pump ? WeaponAnimation.PumpOut(Time.time - lastShot) : 0f;
        /// <summary>The clips this gun is held with (null: posed by code).</summary>
        public WeaponAnimation.GunClips Clips => animated ? gunClips : null;

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
            AlignerUsed = false;
            if (fp > 0.001f && hasSight && AlignedTwoHand(viewCam, f, right, up, gunRot, pr, toForegrip, b, fp)) return;          // (first person: the pack's layer puts the sight on the line of sight)
            if (fp > 0.001f) pr = Vector3.Lerp(pr, FirstPersonGrip(viewCam, f, right, up, gunRot), fp);          // (the layer could not, or is off: the sight on the line of sight, in the middle of the screen, as before)
            var tl = pr + toForegrip;
            if (fp > 0.001f) tl += (-right * 0.075f + up * 0.085f) * fp;                               // (first person: the left hand grips the side of the handguard, so that its forearm is seen reaching in from the left)
            var gripFromWrist = gunRot * Quaternion.Inverse(Grip.localRotation) * Vector3.Scale(Grip.localPosition, hand.lossyScale);          // (the code's hold has the grip on the wrist; a gun placed as a Blender file holds it sits a little along the hand)
            SolveArm(upper, fore, hand, pr - gripFromWrist, -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
            SolveArm(lUpper, lFore, lHand, tl, -up * 0.8f - right * 0.6f, b);
            lHand.rotation = Quaternion.Slerp(lHand.rotation, gunRot * Quaternion.Euler(0f, 0f, LeftRoll + FpLeftRoll * fp) * MirrorX(Quaternion.Inverse(Grip.localRotation)), b);
            LeftHandGap = (lHand.position - Foregrip.position).magnitude;
        }

        // ------------------------------------------------------------------ first person through the pack's layer

        SightsAimLayer.Arm RightArm() => new SightsAimLayer.Arm { upper = upper, fore = fore, hand = hand };
        SightsAimLayer.Arm LeftArm() => new SightsAimLayer.Arm { upper = lUpper, fore = lFore, hand = lHand };

        /// <summary>The local rotations of the arm bones, to put the arms back as they were when the layer cannot do its work.</summary>
        struct ArmsSaved
        {
            public Quaternion u, f, h, lu, lf, lh;
            public bool two;
        }

        ArmsSaved SaveArms() => new ArmsSaved
        {
            u = upper.localRotation, f = fore.localRotation, h = hand.localRotation, two = twoHand && lUpper && lFore && lHand,
            lu = twoHand && lUpper ? lUpper.localRotation : Quaternion.identity, lf = twoHand && lFore ? lFore.localRotation : Quaternion.identity, lh = twoHand && lHand ? lHand.localRotation : Quaternion.identity,
        };

        void RestoreArms(ArmsSaved a)
        {
            upper.localRotation = a.u; fore.localRotation = a.f; hand.localRotation = a.h;
            if (a.two) { lUpper.localRotation = a.lu; lFore.localRotation = a.lf; lHand.localRotation = a.lh; }
        }

        /// <summary>
        /// First person, a gun in both hands, aimed through the pack's layer: the hold as in third person (both hands on the gun, the gun level and along the aim, <paramref name="carried"/> the right hand's place), then the layer moves the gun and
        /// both hands as one piece so that the sight is on the line of sight (and the reload motion on top of that), and solves the arms to the hands. False (the arms are back as they were): the layer is off or could not do it.
        /// </summary>
        bool AlignedTwoHand(Transform cam, Vector3 f, Vector3 right, Vector3 up, Quaternion gunRot, Vector3 carried, Vector3 toForegrip, float b, float fp)
        {
            if (!AlignerAllowed) return false;
            var saved = SaveArms();
            SolveArm(upper, fore, hand, carried, -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
            SolveArm(lUpper, lFore, lHand, carried + toForegrip, -up * 0.8f - right * 0.6f, b);
            lHand.rotation = Quaternion.Slerp(lHand.rotation, gunRot * Quaternion.Euler(0f, 0f, LeftRoll) * MirrorX(Quaternion.Inverse(Grip.localRotation)), b);
            SightsAimLayer.Target(cam.position, f, right, up, gunRot, sightRelief, reloadNow, out var sightAt, out var rot);
            if (aimLayer == null) aimLayer = new SightsAimLayer(transform);
            bool ok = aimLayer.Align(Grip, sightLocal, sightAt, rot, RightArm(), -up * 0.8f + right * 0.6f, LeftArm(), -up * 0.8f - right * 0.6f, fp * b, FpLeftShift * fp, FpLeftRoll * fp);
            if (!ok) { RestoreArms(saved); NoteFallback(fp * b); return false; }
            LeftHandGap = (lHand.position - Foregrip.position).magnitude;
            AlignerUsed = true;
            NoteAligned(fp * b);
            return true;
        }

        /// <summary>The same for a pistol in the right hand alone (<paramref name="carried"/>: the wrist's place in the third-person hold).</summary>
        bool AlignedOneHand(Transform cam, Vector3 aim, Vector3 right, Vector3 up, Quaternion gunRot, Vector3 carried, float b, float fp)
        {
            if (!AlignerAllowed) return false;
            var saved = SaveArms();
            SolveArm(upper, fore, hand, carried, -up * 0.8f + right * 0.6f, b);
            hand.rotation = Quaternion.Slerp(hand.rotation, gunRot * Quaternion.Inverse(Grip.localRotation), b);
            SightsAimLayer.Target(cam.position, aim, right, up, gunRot, sightRelief, reloadNow, out var sightAt, out var rot);
            if (aimLayer == null) aimLayer = new SightsAimLayer(transform);
            bool ok = aimLayer.Align(Grip, sightLocal, sightAt, rot, RightArm(), -up * 0.8f + right * 0.6f, default(SightsAimLayer.Arm), Vector3.zero, fp * b);
            if (!ok) { RestoreArms(saved); NoteFallback(fp * b); return false; }
            AlignerUsed = true;
            NoteAligned(fp * b);
            return true;
        }

        void NoteAligned(float weight)
        {
            if (alignerLogged || weight < 0.9f) return;
            alignerLogged = true;
            Debug.Log($"[HeldGun] {Kind}: first person is aimed through the Kinemation pack's solver (SightsAimLayer); the sight is {aimLayer.Gap:0.000} m from the line of sight");
        }

        /// <summary>The layer gave a frame back. With the gun fully up that is a body the layer cannot aim (an arm too short to bring the sight within 0.35 m), so it rests for 2 s: the old solve and the old reload tilt run, and nothing flickers between the two.</summary>
        void NoteFallback(float weight)
        {
            if (weight >= 0.9f) alignerRetryAt = Time.time + 2f;
            if (fallbackLogged) return;
            fallbackLogged = true;
            Debug.LogWarning($"[HeldGun] {Kind}: the Kinemation layer could not aim a frame (sight {(aimLayer != null ? aimLayer.Gap : -1f):0.000} m from the line of sight, layer failed: {(aimLayer != null && aimLayer.Failed)}); the direct arm solve is used for it");
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
