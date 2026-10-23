using Kinemation.SightsAligner;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// First person aiming through the Kinemation "FPS Shooter Aiming" pack (Sights Aligner, imported 2026-10-23; the owner: "make it so the fps shooting in game when you zoom in uses this asset pack for shooting aiming
    /// and reloading"). The pack's idea ("dynamic retargeting", its own documentation): the hands are copied onto empty objects that ride the gun's pivot, the gun is moved as ONE piece so that its sight is at the aim
    /// point, and the arms are solved to the hands - the base animation is never touched. This is that, in the pack's order, with the pack's own code doing the work (<see cref="AnimToolkitLib.SolveTwoBoneIK"/> for the arms).
    /// What it is NOT: the pack's <c>CoreAnimComponent</c> itself. That component runs in the default order (before the game's own arm code, <see cref="HeldGun"/>, which puts the hands on the gun's grip and foregrip) and needs
    /// its bones and aim target set by hand in the Inspector (private fields), a rig with animated gun poses and a <c>TargetAimData</c> per gun; these rigs are built at run time and have no gun animation. So the
    /// hold is posed first (by HeldGun, as for third person) and this layer then moves the gun and the hands to the sights, with the pack's solver.
    /// Plain class (no Update of its own): HeldGun calls <see cref="Align"/> every frame, after it has posed the hold, and takes the old direct solve back when this says no.
    /// </summary>
    public sealed class SightsAimLayer
    {
        /// <summary>A two-bone arm: the shoulder (upper arm), the elbow (forearm) and the wrist (hand) bones.</summary>
        public struct Arm
        {
            public Transform upper, fore, hand;
            public bool Ok => upper && fore && hand;
        }

        /// <summary>The sight may miss its place by this much (m) once the gun is fully up before the layer gives the frame back to the direct solve (an arm that cannot reach is short by a few centimetres, never by this).</summary>
        public const float MaxGap = 0.35f;

        readonly GameObject holder;
        readonly Transform rightIk, leftIk, rightHint, leftHint;

        /// <summary>Set when something went wrong (a bone missing, numbers that are not numbers): the layer stays off for the rest of this gun.</summary>
        public bool Failed { get; private set; }
        /// <summary>How far the sight is from its place after the last <see cref="Align"/> (m; for the tests and the pictures).</summary>
        public float Gap { get; private set; }
        /// <summary>Frames the layer has aimed (for the tests).</summary>
        public int Frames { get; private set; }

        public SightsAimLayer(Transform parent)
        {
            holder = new GameObject("SightsAligner");
            holder.transform.SetParent(parent, false);
            rightIk = Make("RightHandIK"); leftIk = Make("LeftHandIK");
            rightHint = Make("RightElbowHint"); leftHint = Make("LeftElbowHint");
        }

        Transform Make(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder.transform, false);
            return go.transform;
        }

        /// <summary>Takes the layer's helper objects away.</summary>
        public void Dispose()
        {
            if (holder) Object.Destroy(holder);
        }

        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        static bool Finite(Quaternion q) => !(float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w) || float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w))
                                            && q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 0.5f;

        struct Saved
        {
            public Quaternion up, fo, ha;
            public static Saved Of(Arm a) => new Saved { up = a.upper.localRotation, fo = a.fore.localRotation, ha = a.hand.localRotation };
            public void Restore(Arm a) { a.upper.localRotation = up; a.fore.localRotation = fo; a.hand.localRotation = ha; }
        }

        /// <summary>
        /// One frame of aiming through the sights. The hold has been posed (the gun carried: the hands on its grip and foregrip). The gun and the hands that hold it are moved as one piece so that the gun's
        /// sight (<paramref name="sightLocal"/>, in the grip's own space) is at <paramref name="aimPos"/> and the gun is turned to <paramref name="aimRot"/>, and each arm is solved to its hand by <paramref name="weight"/>
        /// (0 = as it is carried, 1 = at the sights). <paramref name="rightPole"/> and <paramref name="leftPole"/> say which way the elbows bend (world directions). <paramref name="left"/> may be empty (a gun in one hand).
        /// <paramref name="leftShift"/> (in the gun's own frame: x right, y up, z along the barrel; metres) and <paramref name="leftRoll"/> (degrees about the barrel) move the left hand on the gun after the move: it grips the side of the handguard, not the foregrip itself.
        /// False when it could not be done (the caller poses the arms the old way); the bones are put back as they were then.
        /// </summary>
        public bool Align(Transform grip, Vector3 sightLocal, Vector3 aimPos, Quaternion aimRot, Arm right, Vector3 rightPole, Arm left, Vector3 leftPole, float weight, Vector3 leftShift = default(Vector3), float leftRoll = 0f)
        {
            if (Failed) return false;
            if (!grip || !right.Ok) { Failed = true; return false; }
            if (weight <= 0.0005f) { Gap = 0f; return true; }
            if (!Finite(aimPos) || !Finite(aimRot) || !Finite(sightLocal) || !Finite(grip.position) || !Finite(grip.rotation) || !Finite(rightPole) || !Finite(leftPole)) return false;
            weight = Mathf.Clamp01(weight);

            // 1. retarget: where the gun is, where it must be (its sight at the aim point), and the turn that takes it there
            var gripPos = grip.position; var gripRot = grip.rotation;
            var wantPos = aimPos - aimRot * Vector3.Scale(sightLocal, grip.lossyScale);
            var move = aimRot * Quaternion.Inverse(gripRot);
            if (!Finite(wantPos) || !Finite(move)) return false;

            // 2. the hands ride the gun: each hand's place and turn, carried by the same move (the objects are the pack's "dynamic IK targets")
            Put(rightIk, right.hand, wantPos, move, gripPos);
            Hint(rightHint, right.upper, rightPole);
            bool two = left.Ok;
            if (two)
            {
                Put(leftIk, left.hand, wantPos, move, gripPos);
                if (leftShift != Vector3.zero) leftIk.position += aimRot * leftShift;
                if (leftRoll != 0f) leftIk.rotation = aimRot * Quaternion.Euler(0f, 0f, leftRoll) * Quaternion.Inverse(aimRot) * leftIk.rotation;
                Hint(leftHint, left.upper, leftPole);
            }

            // 3. the arms, by the pack's two-bone IK
            var sr = Saved.Of(right); var sl = two ? Saved.Of(left) : default(Saved);
            AnimToolkitLib.SolveTwoBoneIK(right.upper, right.fore, right.hand, rightIk, rightHint, weight, weight, 1f);
            if (two) AnimToolkitLib.SolveTwoBoneIK(left.upper, left.fore, left.hand, leftIk, leftHint, weight, weight, 1f);

            // 4. look at what came out: no bad numbers, and (once the gun is up) the sight near its place
            Gap = (grip.TransformPoint(sightLocal) - aimPos).magnitude;
            bool good = Finite(right.hand.position) && Finite(right.hand.rotation) && Finite(right.fore.rotation) && Finite(right.upper.rotation)
                        && (!two || (Finite(left.hand.position) && Finite(left.hand.rotation) && Finite(left.fore.rotation) && Finite(left.upper.rotation)))
                        && Finite(grip.position) && !float.IsNaN(Gap) && (weight < 0.9f || Gap < MaxGap);
            if (!good)
            {
                sr.Restore(right);
                if (two) sl.Restore(left);
                return false;
            }
            Frames++;
            return true;
        }

        /// <summary>
        /// Where the sight goes and how the gun is turned in first person: the sight on the line of sight (<paramref name="relief"/> in front of the camera at <paramref name="camPos"/>) with the gun level and along the aim
        /// (<paramref name="gunRot"/> = LookRotation(f, up)), and on top of that the reload motion <paramref name="o"/> (see <see cref="ReloadMotionRules"/>): moved along the aim's own right, up and forward, turned about
        /// the aim's right (pitch, the muzzle goes down), up (yaw, to the right) and barrel (roll, counter-clockwise seen from behind). With no reload motion it is the plain sight line, as before.
        /// </summary>
        public static void Target(Vector3 camPos, Vector3 f, Vector3 right, Vector3 up, Quaternion gunRot, float relief, ReloadMotionRules.Pose o, out Vector3 sightAt, out Quaternion rot)
        {
            sightAt = camPos + f * relief;
            rot = gunRot;
            if (o.IsZero) return;
            sightAt += right * o.x + up * o.y + f * o.z;
            if (o.pitch != 0f || o.yaw != 0f || o.roll != 0f)
                rot = Quaternion.AngleAxis(o.yaw, up) * Quaternion.AngleAxis(o.pitch, right) * Quaternion.AngleAxis(o.roll, f) * gunRot;
        }

        /// <summary>An IK target object on the hand's place after the move: its offset from the gun's grip is kept, the gun's turn is applied to it.</summary>
        static void Put(Transform ik, Transform hand, Vector3 wantGripPos, Quaternion move, Vector3 gripPos)
        {
            ik.position = wantGripPos + move * (hand.position - gripPos);
            ik.rotation = move * hand.rotation;
        }

        /// <summary>The elbow's hint: a point in the pole's direction from the shoulder, so the elbow keeps bending down and out as the hold has it.</summary>
        static void Hint(Transform hint, Transform shoulder, Vector3 pole)
        {
            hint.position = shoulder.position + (pole.sqrMagnitude > 1e-6f ? pole.normalized : Vector3.down) * 0.5f;
        }
    }
}
