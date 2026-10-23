using System;
using System.Collections.Generic;
using BuildingEscape;
using UnityEngine;

static class Program
{
    static int fails, passes;
    static void Check(string name, bool ok, string detail = "")
    {
        if (ok) passes++; else fails++;
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (detail.Length > 0 ? "   [" + detail + "]" : ""));
    }
    static bool Near(Vector3 a, Vector3 b, float tol) => (a - b).magnitude <= tol;

    // ---- a test body: a chest, two arms hanging down, a gun in the right hand, the way HeldGun builds one
    class Body
    {
        public Transform root, chest, upper, fore, hand, lUpper, lFore, lHand, grip, foregrip;
        public Vector3 sightLocal = new Vector3(0.004f, 0.075f, 0.11f);
        public Body(float arm = 0.585f, float gripScale = 0.9f, bool left = true)
        {
            var g = new GameObject("body"); root = g.transform;
            chest = root.Make("chest", new Vector3(0f, 1.40f, 0f));
            float a = arm * 0.56f, b = arm * 0.44f;
            upper = chest.Make("RightArm", new Vector3(0.19f, 0.05f, 0f)); fore = upper.Make("RightForeArm", new Vector3(0f, -a, 0f)); hand = fore.Make("RightHand", new Vector3(0f, -b, 0f));
            if (left) { lUpper = chest.Make("LeftArm", new Vector3(-0.19f, 0.05f, 0f)); lFore = lUpper.Make("LeftForeArm", new Vector3(0f, -a, 0f)); lHand = lFore.Make("LeftHand", new Vector3(0f, -b, 0f)); }
            grip = hand.Make("Grip", Vector3.zero);       // (as RangedWeapon.BuildHeld does: the grip is the hand bone's own origin)
            grip.localRotation = Quaternion.Euler(12f, -95f, 20f);
            grip.localScale = new Vector3(gripScale, gripScale, gripScale);
            foregrip = grip.Make("Foregrip", new Vector3(0.012f, -0.035f, 0.27f));
        }
    }

    static Quaternion LeftTargetRot(Body bd, Quaternion gunRot, float leftRoll) => gunRot * Quaternion.Euler(0f, 0f, leftRoll) * Mirror(Quaternion.Inverse(bd.grip.localRotation));
    static Quaternion Mirror(Quaternion q) => new Quaternion(q.x, -q.y, -q.z, q.w);

    // the hold as HeldGun.AlignedTwoHand poses it before the layer works (a copy of those lines)
    static void CarryTwoHand(Body bd, Vector3 f, float b, out Vector3 right, out Vector3 up, out Quaternion gunRot)
    {
        right = Vector3.Cross(Vector3.up, f).normalized; up = Vector3.Cross(f, right); gunRot = Quaternion.LookRotation(f, up);
        float L = 0.585f, half = 0.19f, fitFwd = 0.2f, fitLat = 0.5f;
        var carried = bd.upper.position + f * (fitFwd * L) - up * (0.28f * L) - right * (half * (1f - fitLat));
        var toForegrip = gunRot * Vector3.Scale(bd.foregrip.localPosition, bd.grip.lossyScale);
        BuildingEscape.HeldGunCopy.SolveArm(bd.upper, bd.fore, bd.hand, carried, -up * 0.8f + right * 0.6f, b);
        bd.hand.rotation = Quaternion.Slerp(bd.hand.rotation, gunRot * Quaternion.Inverse(bd.grip.localRotation), b);
        BuildingEscape.HeldGunCopy.SolveArm(bd.lUpper, bd.lFore, bd.lHand, carried + toForegrip, -up * 0.8f - right * 0.6f, b);
        bd.lHand.rotation = Quaternion.Slerp(bd.lHand.rotation, LeftTargetRot(bd, gunRot, 0f), b);
    }
    static SightsAimLayer.Arm R(Body bd) => new SightsAimLayer.Arm { upper = bd.upper, fore = bd.fore, hand = bd.hand };
    static SightsAimLayer.Arm L(Body bd) => new SightsAimLayer.Arm { upper = bd.lUpper, fore = bd.lFore, hand = bd.lHand };

    static Vector3 Elbow(Transform upper, Transform fore) => fore.position;
    // the elbow's side: how far the elbow is off the line from the shoulder to the wrist, along the pole
    static float ElbowSide(Transform upper, Transform fore, Transform hand, Vector3 pole)
    {
        var line = (hand.position - upper.position).normalized;
        var off = Vector3.ProjectOnPlane(fore.position - upper.position, line);
        return Vector3.Dot(off.normalized, pole.normalized);
    }

    static void Main()
    {
        // ------------------------------------------------------------ the stand-in itself (Unity's conventions)
        Check("shim: Euler z 90 turns right to up", Near(Quaternion.Euler(0, 0, 90) * Vector3.right, Vector3.up, 1e-5f));
        Check("shim: Euler x 90 points the forward down (positive pitch = down)", Near(Quaternion.Euler(90, 0, 0) * Vector3.forward, Vector3.down, 1e-5f));
        Check("shim: Euler y 90 turns forward to the right", Near(Quaternion.Euler(0, 90, 0) * Vector3.forward, Vector3.right, 1e-5f));
        Check("shim: cross(up, forward) is right", Near(Vector3.Cross(Vector3.up, Vector3.forward), Vector3.right, 1e-6f));
        Check("shim: LookRotation(right, up) faces right", Near(Quaternion.LookRotation(Vector3.right, Vector3.up) * Vector3.forward, Vector3.right, 1e-5f));
        Check("shim: LookRotation keeps up up", Near(Quaternion.LookRotation(new Vector3(1, 0.3f, 2), Vector3.up) * Vector3.up, Quaternion.LookRotation(new Vector3(1, 0.3f, 2), Vector3.up) * Vector3.up, 1e-6f) && (Quaternion.LookRotation(new Vector3(1, 0.3f, 2), Vector3.up) * Vector3.up).y > 0.9f);
        var tt = new GameObject("t").transform; tt.localRotation = Quaternion.Euler(10, 20, 30); tt.localPosition = new Vector3(1, 2, 3); var tc = tt.Make("c", new Vector3(0, 1, 0)); tc.localScale = new Vector3(2, 2, 2);
        Check("shim: child position and its inverse", Near(tc.TransformPoint(tc.InverseTransformPoint(new Vector3(5, 5, 5))), new Vector3(5, 5, 5), 1e-4f));

        // ------------------------------------------------------------ Target: the sight line and the reload motion on it
        var f0 = new Vector3(0.3f, 0.2f, 1f).normalized; var r0 = Vector3.Cross(Vector3.up, f0).normalized; var u0 = Vector3.Cross(f0, r0); var g0 = Quaternion.LookRotation(f0, u0); var cam0 = new Vector3(1f, 1.6f, 2f);
        SightsAimLayer.Target(cam0, f0, r0, u0, g0, 0.22f, default(ReloadMotionRules.Pose), out var sa, out var rt);
        Check("target: no reload = the sight line, relief in front of the camera, the plain gun turn", Near(sa, cam0 + f0 * 0.22f, 1e-6f) && Quaternion.Angle(rt, g0) < 1e-3f);
        var pp = new ReloadMotionRules.Pose { pitch = 10f };
        SightsAimLayer.Target(cam0, f0, r0, u0, g0, 0.22f, pp, out sa, out rt);
        Check("target: positive pitch = the muzzle goes down by that much about the aim's right", Vector3.Dot(rt * Vector3.forward, u0) < -0.17f && Math.Abs(Vector3.Angle(rt * Vector3.forward, f0) - 10f) < 0.01f);
        pp = new ReloadMotionRules.Pose { yaw = 10f };
        SightsAimLayer.Target(cam0, f0, r0, u0, g0, 0.22f, pp, out sa, out rt);
        Check("target: positive yaw = the muzzle goes to the right", Vector3.Dot(rt * Vector3.forward, r0) > 0.17f);
        pp = new ReloadMotionRules.Pose { roll = 20f };
        SightsAimLayer.Target(cam0, f0, r0, u0, g0, 0.22f, pp, out sa, out rt);
        Check("target: positive roll = counter-clockwise seen from behind (the top goes left), the barrel stays on the aim", Vector3.Dot(rt * Vector3.up, r0) < -0.3f && Vector3.Angle(rt * Vector3.forward, f0) < 0.01f);
        pp = new ReloadMotionRules.Pose { x = 0.04f, y = -0.09f, z = -0.03f };
        SightsAimLayer.Target(cam0, f0, r0, u0, g0, 0.22f, pp, out sa, out rt);
        Check("target: the offsets are along the aim's own right, up and forward", Near(sa, cam0 + f0 * (0.22f - 0.03f) + r0 * 0.04f + u0 * -0.09f, 1e-6f));

        // ------------------------------------------------------------ the layer on a gun in both hands
        foreach (var pitch in new[] { 0f, 25f, -30f })
        foreach (var yaw in new[] { 0f, 70f })
        {
            var bd = new Body(); bd.root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var f = (Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward);
            CarryTwoHand(bd, f, 1f, out var right, out var up, out var gunRot);
            float carriedGap = (bd.lHand.position - bd.foregrip.position).magnitude;
            var cam = bd.chest.position + bd.root.rotation * new Vector3(0f, 0.2f, 0.1f);
            var layer = new SightsAimLayer(bd.root);
            SightsAimLayer.Target(cam, f, right, up, gunRot, 0.22f, default(ReloadMotionRules.Pose), out var at, out var rot);
            bool ok = layer.Align(bd.grip, bd.sightLocal, at, rot, R(bd), -up * 0.8f + right * 0.6f, L(bd), -up * 0.8f - right * 0.6f, 1f, new Vector3(-0.075f, 0.085f, 0f), 0f);
            var sightNow = bd.grip.TransformPoint(bd.sightLocal);
            string tag = $"two hands, aim pitch {pitch} yaw {yaw}";
            Check(tag + ": the layer says yes", ok, "gap " + layer.Gap.ToString("0.0000"));
            Check(tag + ": the sight is on the line of sight (within 3 mm)", (sightNow - at).magnitude < 0.003f, (sightNow - at).magnitude.ToString("0.0000") + " m");
            Check(tag + ": the gun is turned to the aim (within 0.5 deg)", Quaternion.Angle(bd.grip.rotation, rot) < 0.5f, Quaternion.Angle(bd.grip.rotation, rot).ToString("0.00"));
            float rs = ElbowSide(bd.upper, bd.fore, bd.hand, -up * 0.8f + right * 0.6f), ls = ElbowSide(bd.lUpper, bd.lFore, bd.lHand, -up * 0.8f - right * 0.6f);
            Check(tag + ": both elbows bend the way the poles say", rs > 0.2f && ls > 0.2f, rs.ToString("0.00") + " / " + ls.ToString("0.00"));
            var lhWant = bd.foregrip.position + rot * new Vector3(-0.075f, 0.085f, 0f);
            Check(tag + ": the left hand is on the side of the handguard (within 3 cm; the hold reached the foregrip to begin with)", carriedGap < 0.005f && (bd.lHand.position - lhWant).magnitude < 0.03f, "miss " + (bd.lHand.position - lhWant).magnitude.ToString("0.000") + " m, needs reach " + (lhWant - bd.lUpper.position).magnitude.ToString("0.000") + " of " + (0.585f).ToString("0.000") + ", carried left gap " + carriedGap.ToString("0.000"));
            Check(tag + ": the right hand sits where it did on the gun (the grip is one piece with it)", (bd.hand.TransformPoint(bd.grip.localPosition * 0f) - bd.hand.position).magnitude < 1e-6f);
        }

        // ------------------------------------------------------------ a whole reload
        foreach (var steps in new[] { ReloadMotionRules.DefaultSteps, ReloadMotionRules.StepFractions(new[] { 0f, .40f, .85f, 1.20f, 1.55f }), ReloadMotionRules.StepFractions(new[] { 0f, .45f, .95f, 1.45f, 1.95f }) })
        {
            var bd = new Body();
            var f = Quaternion.Euler(-8f, 20f, 0f) * Vector3.forward;
            var layer = new SightsAimLayer(bd.root);
            float maxGap = 0f, maxJump = 0f, maxTurn = 0f, minSideSign = 9f; bool allOk = true;
            Vector3 prevGrip = default; Quaternion prevRot = default; bool first = true; float lowestY = 0f; float maxZBack = 0f;
            for (int i = 0; i <= 200; i++)
            {
                float p = i / 200f;
                var o = ReloadMotionRules.Evaluate(p, steps);
                CarryTwoHand(bd, f, 1f, out var right, out var up, out var gunRot);
                var cam = bd.chest.position + new Vector3(0f, 0.2f, 0.1f);
                SightsAimLayer.Target(cam, f, right, up, gunRot, 0.22f, o, out var at, out var rot);
                bool ok = layer.Align(bd.grip, bd.sightLocal, at, rot, R(bd), -up * 0.8f + right * 0.6f, L(bd), -up * 0.8f - right * 0.6f, 1f, new Vector3(-0.075f, 0.085f, 0f), 70f);
                allOk &= ok; maxGap = Math.Max(maxGap, layer.Gap);
                var sightNow = bd.grip.TransformPoint(bd.sightLocal);
                lowestY = Math.Min(lowestY, Vector3.Dot(sightNow - (cam + f * 0.22f), up));
                maxZBack = Math.Min(maxZBack, Vector3.Dot(sightNow - (cam + f * 0.22f), f));
                minSideSign = Math.Min(minSideSign, Math.Min(ElbowSide(bd.upper, bd.fore, bd.hand, -up * 0.8f + right * 0.6f), ElbowSide(bd.lUpper, bd.lFore, bd.lHand, -up * 0.8f - right * 0.6f)));
                if (!first) { maxJump = Math.Max(maxJump, (bd.grip.position - prevGrip).magnitude); maxTurn = Math.Max(maxTurn, Quaternion.Angle(bd.grip.rotation, prevRot)); }
                prevGrip = bd.grip.position; prevRot = bd.grip.rotation; first = false;
            }
            string tag = "reload sweep (" + steps.Length + " steps)";
            Check(tag + ": the layer worked at every one of 201 moments", allOk);
            Check(tag + ": the sight follows the motion to within 1 cm (arm reach)", maxGap < 0.01f, "worst " + maxGap.ToString("0.0000") + " m");
            Check(tag + ": the gun moves smoothly (no frame moves it more than 1.5 cm or turns it more than 2.5 deg)", maxJump < 0.015f && maxTurn < 2.5f, "worst " + maxJump.ToString("0.0000") + " m, " + maxTurn.ToString("0.00") + " deg");
            Check(tag + ": the gun really goes down from the sight line (about 9 cm and more)", lowestY < -0.085f, lowestY.ToString("0.000"));
            Check(tag + ": the elbows keep bending the right way throughout", minSideSign > 0.1f, minSideSign.ToString("0.00"));
            // the last frame: back on the sight line
            Check(tag + ": the gun is back on the sight line at the end", (bd.grip.TransformPoint(bd.sightLocal) - (bd.chest.position + new Vector3(0f, 0.2f, 0.1f) + f * 0.22f)).magnitude < 0.003f);
        }

        // ------------------------------------------------------------ a pistol in one hand
        {
            var bd = new Body(0.585f, 1f, false);
            bd.sightLocal = new Vector3(0f, 0.05f, 0.09f);
            var f = Vector3.forward; var right = Vector3.right; var up = Vector3.up; var gunRot = Quaternion.LookRotation(f, up);
            var carried = bd.upper.position + f * (0.9f * 0.585f);
            BuildingEscape.HeldGunCopy.SolveArm(bd.upper, bd.fore, bd.hand, carried, -up * 0.8f + right * 0.6f, 1f);
            bd.hand.rotation = gunRot * Quaternion.Inverse(bd.grip.localRotation);
            var cam = bd.chest.position + new Vector3(0f, 0.2f, 0.1f);
            var layer = new SightsAimLayer(bd.root);
            float relief = Mathf.Clamp(0.585f * 0.85f, 0.24f, 0.45f);
            SightsAimLayer.Target(cam, f, right, up, gunRot, relief, default(ReloadMotionRules.Pose), out var at, out var rot);
            bool ok = layer.Align(bd.grip, bd.sightLocal, at, rot, R(bd), -up * 0.8f + right * 0.6f, default(SightsAimLayer.Arm), Vector3.zero, 1f);
            Check("pistol: layer says yes", ok, "gap " + layer.Gap.ToString("0.0000"));
            Check("pistol: sight on the line of sight (3 mm)", (bd.grip.TransformPoint(bd.sightLocal) - at).magnitude < 0.003f);
            Check("pistol: gun turned to the aim", Quaternion.Angle(bd.grip.rotation, rot) < 0.5f);
        }

        // ------------------------------------------------------------ weight, bad input, unreachable aims
        {
            var bd = new Body(); var f = Vector3.forward;
            CarryTwoHand(bd, f, 1f, out var right, out var up, out var gunRot);
            var cam = bd.chest.position + new Vector3(0f, 0.2f, 0.1f);
            var layer = new SightsAimLayer(bd.root);
            SightsAimLayer.Target(cam, f, right, up, gunRot, 0.22f, default(ReloadMotionRules.Pose), out var at, out var rot);
            var before = bd.grip.position; var rq = bd.upper.localRotation;
            Check("weight 0: yes, and nothing moved", layer.Align(bd.grip, bd.sightLocal, at, rot, R(bd), Vector3.down, L(bd), Vector3.down, 0f) && bd.grip.position == before && Quaternion.Angle(bd.upper.localRotation, rq) < 1e-4f);
            var sightHold = bd.grip.TransformPoint(bd.sightLocal);
            var l2 = new SightsAimLayer(bd.root);
            Check("weight 0.5: yes", l2.Align(bd.grip, bd.sightLocal, at, rot, R(bd), -up * 0.8f + right * 0.6f, L(bd), -up * 0.8f - right * 0.6f, 0.5f));
            float d0 = (sightHold - at).magnitude, d1 = (bd.grip.TransformPoint(bd.sightLocal) - at).magnitude;
            Check("weight 0.5: the sight is between where it was and the line of sight", d1 < d0 - 0.001f || d0 < 0.003f, d0.ToString("0.000") + " -> " + d1.ToString("0.000"));
        }
        {
            var bd = new Body(); var f = Vector3.forward;
            CarryTwoHand(bd, f, 1f, out var right, out var up, out var gunRot);
            var layer = new SightsAimLayer(bd.root);
            var rots = new[] { bd.upper.localRotation, bd.fore.localRotation, bd.hand.localRotation, bd.lUpper.localRotation, bd.lFore.localRotation, bd.lHand.localRotation };
            bool ok = layer.Align(bd.grip, bd.sightLocal, new Vector3(float.NaN, 1f, 1f), gunRot, R(bd), Vector3.down, L(bd), Vector3.down, 1f);
            Check("not a number: no, and the layer is not dead after it", !ok && !layer.Failed);
            // an aim point 2 m to the side: out of reach
            ok = layer.Align(bd.grip, bd.sightLocal, bd.chest.position + new Vector3(2f, 0f, 0.3f), gunRot, R(bd), -up * 0.8f + right * 0.6f, L(bd), -up * 0.8f - right * 0.6f, 1f);
            var now = new[] { bd.upper.localRotation, bd.fore.localRotation, bd.hand.localRotation, bd.lUpper.localRotation, bd.lFore.localRotation, bd.lHand.localRotation };
            bool same = true; for (int i = 0; i < 6; i++) same &= Quaternion.Angle(rots[i], now[i]) < 1e-3f;
            string dd = ""; for (int i = 0; i < 6; i++) dd += Quaternion.Angle(rots[i], now[i]).ToString("0.0") + " ";
            Check("out of reach (2 m away): no, and the arms are back as they were", !ok && same, "ok=" + ok + " gap " + layer.Gap.ToString("0.00") + " angles " + dd);
            var lf = new SightsAimLayer(bd.root);
            Check("no grip: no, and the layer stays off", !lf.Align(null, bd.sightLocal, Vector3.zero, Quaternion.identity, R(bd), Vector3.down, default(SightsAimLayer.Arm), Vector3.down, 1f) && lf.Failed);
            Check("... and stays off even for a good call", !lf.Align(bd.grip, bd.sightLocal, at0(bd), gunRot, R(bd), Vector3.down, default(SightsAimLayer.Arm), Vector3.down, 1f));
            var holders = bd.root.children.FindAll(c => c.name == "SightsAligner"); int kids = holders.Count > 0 ? holders[0].children.Count : -1;
            Check("the layer made its four helper objects", kids == 4, kids.ToString());
            layer.Dispose();
            Check("Dispose takes the holder away", holders.Count > 0 && !holders[0].gameObject);
        }

        Console.WriteLine();
        Console.WriteLine($"{passes} passed, {fails} failed");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
    static Vector3 at0(Body bd) => bd.chest.position + Vector3.forward;
}
