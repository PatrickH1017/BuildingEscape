using System;
using System.Linq;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the first-person reload motion (<c>ReloadMotionRules.cs</c>, 2026-10-23: the gun leaves the sight line for a reload, takes a tug when the old magazine is drawn, a bump when the new one is seated,
    /// a pull back when it is charged, and comes back). They prove the motion is nothing before and after the reload (so the aimed pose is where it always was), that it has no jump anywhere, that it stays small, that its
    /// steps fall on the sounds of each gun's reload, and that the helpers (<c>StepFractions</c>, <c>Scale</c>) cope with bad input. What the Kinemation pack's solver does with the arms is checked by the stand-in run outside
    /// Unity (aimsim) and, in the editor, by the play-mode scenario <c>FpLook</c>. Called from <c>NetDevTest.PolicyTest</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void SightsAimTest()
        {
            var def = ReloadMotionRules.DefaultSteps;
            const float eps = 1e-5f;

            // the constants the motion is tuned with (pinned: a change is a decision, not an accident)
            Check("reload motion: the lowered pose is 4 cm right, 9 cm down, 3 cm back, 14 deg muzzle up, 6 deg left, 22 deg rolled to the left",
                Math.Abs(ReloadMotionRules.LowX - 0.04f) < eps && Math.Abs(ReloadMotionRules.LowY + 0.09f) < eps && Math.Abs(ReloadMotionRules.LowZ + 0.03f) < eps
                && Math.Abs(ReloadMotionRules.LowPitch + 14f) < eps && Math.Abs(ReloadMotionRules.LowYaw + 6f) < eps && Math.Abs(ReloadMotionRules.LowRoll + 22f) < eps);
            Check("reload motion: it is down by 0.16 of the reload and starts coming up at 0.80, the sounds are cut for 2.2 s, the last step is no later than 0.93",
                Math.Abs(ReloadMotionRules.DownEnd - 0.16f) < eps && Math.Abs(ReloadMotionRules.UpStart - 0.80f) < eps && Math.Abs(ReloadMotionRules.SoundSeconds - 2.2f) < eps && Math.Abs(ReloadMotionRules.MaxStep - 0.93f) < eps);
            Check("reload motion: the default steps are the rifle's (the sounds at 0, 0.45, 1.00 and 1.60 s of 2.2)", def.Length == 4 && Math.Abs(def[0]) < eps && Math.Abs(def[1] - 0.45f / 2.2f) < 0.002f && Math.Abs(def[2] - 1.00f / 2.2f) < 0.002f && Math.Abs(def[3] - 1.60f / 2.2f) < 0.002f);

            // nothing before, nothing after, and nothing for numbers that are not a reload's progress
            foreach (var bad in new[] { 0f, -0.5f, 1f, 1.5f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.0f })
            {
                var o = ReloadMotionRules.Evaluate(bad, def);
                Check("reload motion: progress " + bad + " is no motion at all", o.IsZero && ReloadMotionRules.Lowered(bad) == 0f);
            }

            // the envelope: in, held, out
            Check("reload motion: the lowering is 0 at the start, 1 from 0.16 to 0.80, 0 at the end",
                ReloadMotionRules.Lowered(0.0001f) < 0.001f && Math.Abs(ReloadMotionRules.Lowered(0.16f) - 1f) < eps && Math.Abs(ReloadMotionRules.Lowered(0.5f) - 1f) < eps && Math.Abs(ReloadMotionRules.Lowered(0.80f) - 1f) < eps
                && ReloadMotionRules.Lowered(0.9999f) < 0.001f);
            bool inUp = true, outDown = true, inRange = true;
            float prev = 0f;
            for (int i = 1; i <= 160; i++) { float v = ReloadMotionRules.Lowered(i / 1000f); inUp &= v >= prev - 1e-7f; prev = v; inRange &= v >= 0f && v <= 1f; }
            prev = 1f;
            for (int i = 800; i < 1000; i++) { float v = ReloadMotionRules.Lowered(i / 1000f); outDown &= v <= prev + 1e-7f; prev = v; inRange &= v >= 0f && v <= 1f; }
            Check("reload motion: it comes down without turning back, and goes up without turning back, always between 0 and 1", inUp && outDown && inRange);
            Check("reload motion: halfway down (0.08) it is half lowered, halfway up (0.90) as well", Math.Abs(ReloadMotionRules.Lowered(0.08f) - 0.5f) < 0.01f && Math.Abs(ReloadMotionRules.Lowered(0.90f) - 0.5f) < 0.01f);
            Check("reload motion: it is a smooth S, not a straight ramp: a quarter of the way down it is 0.156 lowered (not 0.25), a fifth of the way up 0.896 (not 0.8)",
                Math.Abs(ReloadMotionRules.Lowered(0.04f) - 0.15625f) < 0.002f && Math.Abs(ReloadMotionRules.Lowered(0.84f) - 0.896f) < 0.002f);

            // between the bumps the gun is simply in its lowered pose
            var mid = ReloadMotionRules.Evaluate(0.30f, def);
            Check("reload motion: between two steps the gun is in the lowered pose exactly",
                Math.Abs(mid.x - ReloadMotionRules.LowX) < eps && Math.Abs(mid.y - ReloadMotionRules.LowY) < eps && Math.Abs(mid.z - ReloadMotionRules.LowZ) < eps
                && Math.Abs(mid.pitch - ReloadMotionRules.LowPitch) < eps && Math.Abs(mid.yaw - ReloadMotionRules.LowYaw) < eps && Math.Abs(mid.roll - ReloadMotionRules.LowRoll) < eps && Math.Abs(mid.lowered - 1f) < eps);

            // no jump anywhere, and nothing large, for every sound set
            foreach (var kv in SightsAimSets())
            {
                var steps = kv.Value;
                string n = "reload motion (" + kv.Key + "): ";
                float maxStep = 0f, maxAng = 0f, maxPos = 0f; int samples = 4000;
                var last = ReloadMotionRules.Evaluate(1f / samples, steps);
                for (int i = 2; i < samples; i++)
                {
                    var o = ReloadMotionRules.Evaluate(i / (float)samples, steps);
                    maxStep = Math.Max(maxStep, Math.Max(Math.Max(Math.Abs(o.x - last.x), Math.Abs(o.y - last.y)), Math.Abs(o.z - last.z)));
                    maxStep = Math.Max(maxStep, Math.Max(Math.Max(Math.Abs(o.pitch - last.pitch), Math.Abs(o.yaw - last.yaw)), Math.Abs(o.roll - last.roll)) * 0.01f);       // (a degree counts as a centimetre)
                    maxPos = Math.Max(maxPos, Math.Max(Math.Abs(o.x), Math.Max(Math.Abs(o.y), Math.Abs(o.z))));
                    maxAng = Math.Max(maxAng, Math.Max(Math.Abs(o.pitch), Math.Max(Math.Abs(o.yaw), Math.Abs(o.roll))));
                    last = o;
                }
                Check(n + "no jump: from one 4000th of the reload to the next nothing moves more than 1 mm or turns 0.1 deg", maxStep < 0.0012f, maxStep.ToString("0.00000"));
                Check(n + "it stays small: at most 12 cm and 30 deg from the aimed pose", maxPos <= 0.12f && maxAng <= 30f, maxPos.ToString("0.000") + " m, " + maxAng.ToString("0.0") + " deg");
                // the steps: the tug (first step after the start) draws the gun down, the seating (the one before the last) pushes it up and forward, the charge pulls it back
                int c = steps.Length;
                var baseLow = ReloadMotionRules.Evaluate(0.5f, steps);
                var tug = ReloadMotionRules.Evaluate(steps[1], steps); var seat = ReloadMotionRules.Evaluate(steps[c - 2], steps); var rack = ReloadMotionRules.Evaluate(steps[c - 1], steps);
                float eTug = ReloadMotionRules.Lowered(steps[1]), eSeat = ReloadMotionRules.Lowered(steps[c - 2]), eRack = ReloadMotionRules.Lowered(steps[c - 1]);
                Check(n + "the old magazine's tug: at the second sound the gun is 2.2 cm lower than the lowered pose would have it", Math.Abs(tug.y - (ReloadMotionRules.LowY * eTug - 0.022f * eTug)) < 0.0007f, (tug.y - ReloadMotionRules.LowY * eTug).ToString("0.0000"));
                Check(n + "the new magazine's seating: at the last but one sound the gun is 2 cm higher and 1.5 cm further forward than the lowered pose would have it",
                    Math.Abs(seat.y - (ReloadMotionRules.LowY * eSeat + 0.020f * eSeat)) < 0.0007f && Math.Abs(seat.z - (ReloadMotionRules.LowZ * eSeat + 0.015f * eSeat)) < 0.0007f);
                Check(n + "the charge: at the last sound the gun is pulled 4 cm back and the muzzle 4 deg up more than the lowered pose would have it",
                    Math.Abs(rack.z - (ReloadMotionRules.LowZ * eRack - 0.040f * eRack)) < 0.0007f && Math.Abs(rack.pitch - (ReloadMotionRules.LowPitch * eRack - 4f * eRack)) < 0.02f);
                if (c >= 5)
                {
                    var dip = ReloadMotionRules.Evaluate(steps[2], steps); float eD = ReloadMotionRules.Lowered(steps[2]);
                    Check(n + "a step between the tug and the seating is a small dip: 8 mm below the lowered pose", Math.Abs(dip.y - (ReloadMotionRules.LowY * eD - 0.008f * eD)) < 0.0007f && Math.Abs(dip.z - ReloadMotionRules.LowZ * eD) < 1e-5f);
                }
                bool plain = true; int farSamples = 0;
                for (int i = 1; i < 1000; i++)
                {
                    float p = i / 1000f; if (steps.Skip(1).Any(st => Math.Abs(p - st) < 0.06f)) continue;
                    var o = ReloadMotionRules.Evaluate(p, steps); float env = ReloadMotionRules.Lowered(p); farSamples++;
                    plain &= Math.Abs(o.y - ReloadMotionRules.LowY * env) < 1e-6f && Math.Abs(o.z - ReloadMotionRules.LowZ * env) < 1e-6f && Math.Abs(o.pitch - ReloadMotionRules.LowPitch * env) < 1e-4f;
                }
                Check(n + "every step is a short bump: further than 0.06 of the reload from every step the pose is the plain lowered pose again", plain && farSamples > 300, farSamples + " samples");
                Check(n + "the gun is not lower at the start of its reload than at the end of the lowering (it does not dive and come back)", baseLow.lowered > 0.99f);
            }

            // StepFractions: the sounds' times as shares of the reload
            var pistol = ReloadMotionRules.StepFractions(new[] { 0f, 0.40f, 0.85f, 1.20f, 1.55f });
            Check("reload motion: the pistol's five sounds fall at 0, 0.18, 0.39, 0.55 and 0.70 of its reload", pistol.Length == 5 && Math.Abs(pistol[0]) < eps && Math.Abs(pistol[1] - 0.40f / 2.2f) < 1e-4f && Math.Abs(pistol[2] - 0.85f / 2.2f) < 1e-4f && Math.Abs(pistol[3] - 1.20f / 2.2f) < 1e-4f && Math.Abs(pistol[4] - 1.55f / 2.2f) < 1e-4f);
            var shot = ReloadMotionRules.StepFractions(new[] { 0f, 0.45f, 0.95f, 1.45f, 1.95f });
            Check("reload motion: the shotgun's pump (the last sound, at 0.89) is kept, not pushed back before the gun rises", shot.Length == 5 && Math.Abs(shot[4] - 1.95f / 2.2f) < 1e-4f && shot[4] <= ReloadMotionRules.MaxStep);
            var late = ReloadMotionRules.StepFractions(new[] { 0f, 1f, 2f, 3f, 5f });
            Check("reload motion: a sound later than the reload is kept inside it (at 0.93 at the latest) and the steps keep 0.05 apart where there is room", late.All(v => v >= 0f && v <= ReloadMotionRules.MaxStep) && late[4] <= ReloadMotionRules.MaxStep && late[1] >= late[0] + 0.05f - 1e-6f);
            var unsorted = ReloadMotionRules.StepFractions(new[] { 1.2f, 0.4f, 0f, 0.8f });
            Check("reload motion: steps given out of order are put in order (0, 0.4, 0.8 and 1.2 s of 2.2)", unsorted.Length == 4 && Math.Abs(unsorted[0]) < 1e-4f && Math.Abs(unsorted[1] - 0.4f / 2.2f) < 1e-4f && Math.Abs(unsorted[2] - 0.8f / 2.2f) < 1e-4f && Math.Abs(unsorted[3] - 1.2f / 2.2f) < 1e-4f);
            var crowded = ReloadMotionRules.StepFractions(new[] { 0f, 0.01f, 0.02f, 0.03f });
            Check("reload motion: steps crowded together are pulled 0.05 apart", crowded.Length == 4 && crowded[1] - crowded[0] >= 0.05f - 1e-6f && crowded[2] - crowded[1] >= 0.05f - 1e-6f && crowded[3] - crowded[2] >= 0.05f - 1e-6f);
            var crowded2 = ReloadMotionRules.StepFractions(new[] { 0f, 0.066f, 0.132f, 0.198f });
            Check("reload motion: steps only 0.03 apart are pulled 0.05 apart too", crowded2.Length == 4 && crowded2[1] - crowded2[0] >= 0.05f - 1e-6f && crowded2[2] - crowded2[1] >= 0.05f - 1e-6f && crowded2[3] - crowded2[2] >= 0.05f - 1e-6f);
            var nan = ReloadMotionRules.StepFractions(new[] { 0f, float.NaN, 0.9f, float.PositiveInfinity });
            Check("reload motion: a time that is not a number does not break the steps", nan.Length == 4 && nan.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f && v <= ReloadMotionRules.MaxStep));
            var none = ReloadMotionRules.StepFractions(null); var one = ReloadMotionRules.StepFractions(new[] { 0.5f });
            Check("reload motion: no sounds, or one, gives the rifle's steps", none.SequenceEqual(def) && one.SequenceEqual(def));
            none[0] = 9f;
            Check("reload motion: the answer is a copy: changing it does not change the defaults", Math.Abs(ReloadMotionRules.DefaultSteps[0]) < eps);
            Check("reload motion: evaluating with no steps (or one) uses the rifle's", SameOffset(ReloadMotionRules.Evaluate(0.46f, null), ReloadMotionRules.Evaluate(0.46f, def)) && SameOffset(ReloadMotionRules.Evaluate(0.46f, new[] { 0.3f }), ReloadMotionRules.Evaluate(0.46f, def)));

            // Scale: how much of the motion shows
            var full = ReloadMotionRules.Evaluate(0.30f, def);
            Check("reload motion: scaled by 0 there is none of it, by 1 all of it, by 0.5 half of it",
                ReloadMotionRules.Scale(full, 0f).IsZero && SameOffset(ReloadMotionRules.Scale(full, 1f), full) && Math.Abs(ReloadMotionRules.Scale(full, 0.5f).y - full.y * 0.5f) < eps && Math.Abs(ReloadMotionRules.Scale(full, 0.5f).roll - full.roll * 0.5f) < eps && Math.Abs(ReloadMotionRules.Scale(full, 0.5f).lowered - 0.5f) < eps);
            Check("reload motion: scaled by something not a number, below 0 or above 1 it is none of it, none of it, all of it",
                ReloadMotionRules.Scale(full, float.NaN).IsZero && ReloadMotionRules.Scale(full, -1f).IsZero && SameOffset(ReloadMotionRules.Scale(full, 7f), full));
            Check("reload motion: a zero pose scaled stays zero", ReloadMotionRules.Scale(default(ReloadMotionRules.Pose), 1f).IsZero);

#if UNITY_EDITOR
            // the real sound sets (GunSounds.cs): each step the motion uses is the time of a sound
            foreach (RangedWeapon.SoundSet set in Enum.GetValues(typeof(RangedWeapon.SoundSet)))
            {
                var times = GunSounds.ReloadTimes(set);
                var f = ReloadMotionRules.StepFractions(times);
                Check("reload motion: the " + set + " sound set has " + times.Length + " sounds and as many steps, each at its sound's share of the reload (when it is not pushed by the end)",
                    f.Length == times.Length && Enumerable.Range(0, f.Length).All(i => Math.Abs(f[i] - Math.Min(times[i] / ReloadMotionRules.SoundSeconds, ReloadMotionRules.MaxStep)) < 1e-4f));
            }
#endif
        }

        static bool SameOffset(ReloadMotionRules.Pose a, ReloadMotionRules.Pose b) =>
            Math.Abs(a.x - b.x) < 1e-6f && Math.Abs(a.y - b.y) < 1e-6f && Math.Abs(a.z - b.z) < 1e-6f && Math.Abs(a.pitch - b.pitch) < 1e-5f && Math.Abs(a.yaw - b.yaw) < 1e-5f && Math.Abs(a.roll - b.roll) < 1e-5f && Math.Abs(a.lowered - b.lowered) < 1e-6f;

        /// <summary>The step lists of the three sound sets (the pistol's and the SMG's, the rifle's, the shotgun's): the sound times of GunSounds.cs cut for a 2.2 s reload.</summary>
        static System.Collections.Generic.Dictionary<string, float[]> SightsAimSets() => new System.Collections.Generic.Dictionary<string, float[]>
        {
            { "pistol", ReloadMotionRules.StepFractions(new[] { 0f, 0.40f, 0.85f, 1.20f, 1.55f }) },
            { "rifle", ReloadMotionRules.StepFractions(new[] { 0f, 0.45f, 1.00f, 1.60f }) },
            { "shotgun", ReloadMotionRules.StepFractions(new[] { 0f, 0.45f, 0.95f, 1.45f, 1.95f }) },
        };
    }
}
