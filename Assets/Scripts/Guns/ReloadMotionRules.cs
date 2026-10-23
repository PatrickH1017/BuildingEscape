using System;

namespace BuildingEscape
{
    /// <summary>
    /// How a gun moves while it is being reloaded, seen from the eyes (the owner, 2026-10-23: "when you zoom in ... uses this asset pack for shooting aiming and reloading"). The Kinemation pack that was
    /// imported (Sights Aligner) brings NO reload animation, and neither the characters nor the project have one, so the reload motion is made here, in code: the gun dips from the sight line, turns and
    /// canted, takes a small tug when the old magazine is drawn, a bump when the new one is seated, a pull back when it is charged, and comes back to the sight line. Pure rules (no Unity types), so the whole
    /// curve is checked outside the editor (<c>NetDevSightsAimTest</c>).
    /// The motion is an OFFSET from the aimed pose, in the camera's own frame (x right, y up, z forward, in metres; the angles in degrees: pitch about the camera's right axis, positive = the muzzle goes down,
    /// yaw about up, positive = to the right, roll about the barrel, positive = counter-clockwise seen from behind). It is zero before the reload and after it (so the aimed pose is where it was), and it follows
    /// the reload's own sounds: the steps of the sound set (<see cref="StepFractions"/>), so a tug and a bump land on the click they belong to.
    /// </summary>
    public static class ReloadMotionRules
    {
        /// <summary>The offset of the gun from the aimed pose at one moment of a reload.</summary>
        public struct Pose
        {
            public float x, y, z, pitch, yaw, roll;
            /// <summary>0..1: how far the gun is down out of the sight line (the red dot is not the crosshair then, the scope is not up).</summary>
            public float lowered;
            public bool IsZero => x == 0f && y == 0f && z == 0f && pitch == 0f && yaw == 0f && roll == 0f && lowered == 0f;
        }

        /// <summary>The gun's place when it is fully lowered for the magazine (metres, degrees).</summary>
        public const float LowX = 0.04f, LowY = -0.09f, LowZ = -0.03f, LowPitch = -14f, LowYaw = -6f, LowRoll = -22f;
        /// <summary>The lowering is done by this much of the reload, and the raising begins at this much.</summary>
        public const float DownEnd = 0.16f, UpStart = 0.80f;
        /// <summary>The last step of a reload (the charge) can fall this late in it (the shotgun's pump is at 0.89 of its reload, while the gun is already coming back up) and its bump still ends before the reload does.</summary>
        public const float MaxStep = 0.93f;
        /// <summary>The recordings of a reload are cut for a reload of this many seconds (<c>GunSounds.ReloadStepTimes</c>); a longer or a shorter one stretches them, so their share of it stays the same.</summary>
        public const float SoundSeconds = 2.2f;

        /// <summary>A tug when the old magazine comes out (down), a bump when the new one is seated (up and forward), a pull back when it is charged (back, the muzzle up a little), a small dip for every step between.</summary>
        const float TugY = -0.022f, TugPitch = 3f, SeatY = 0.020f, SeatZ = 0.015f, SeatPitch = -3f, DipY = -0.008f, RackZ = -0.040f, RackPitch = -4f, BumpWidth = 0.055f, RackWidth = 0.05f;

        /// <summary>The steps of a reload when the sound set has none (the rifle's).</summary>
        public static readonly float[] DefaultSteps = { 0f, 0.205f, 0.455f, 0.727f };

        /// <summary>
        /// Where each sound of a reload falls as a share of it (0..1): the times of the sound set over <see cref="SoundSeconds"/>, put in order, kept clear of the end (<see cref="MaxStep">no later than this</see>) and apart (at least 0.05, as far as the end allows). At least two steps, whatever is given.
        /// </summary>
        public static float[] StepFractions(float[] soundTimes)
        {
            if (soundTimes == null || soundTimes.Length < 2) return (float[])DefaultSteps.Clone();
            var f = new float[soundTimes.Length];
            for (int i = 0; i < f.Length; i++)
            {
                float t = soundTimes[i] / SoundSeconds;
                if (float.IsNaN(t) || float.IsInfinity(t)) t = 0f;
                f[i] = Clamp(t, 0f, MaxStep);
            }
            Array.Sort(f);
            for (int i = 1; i < f.Length; i++) if (f[i] < f[i - 1] + 0.05f) f[i] = Math.Min(f[i - 1] + 0.05f, MaxStep);
            return f;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
        static float Smooth(float t) { t = Clamp(t, 0f, 1f); return t * t * (3f - 2f * t); }

        /// <summary>A bump 0 .. 1 .. 0 centred on <paramref name="c"/>, <paramref name="w"/> to each side (a raised cosine: no corner).</summary>
        static float Bump(float p, float c, float w)
        {
            float d = Math.Abs(p - c);
            return d >= w ? 0f : 0.5f * (1f + (float)Math.Cos(Math.PI * d / w));
        }

        /// <summary>A pose with all of it taken by <paramref name="w"/> (0 = none of it, 1 = all of it): how much of the reload motion shows (how far this is first person, how far the motion has come in).</summary>
        public static Pose Scale(Pose p, float w)
        {
            if (float.IsNaN(w) || w <= 0f) return default(Pose);
            if (w > 1f) w = 1f;
            return new Pose { x = p.x * w, y = p.y * w, z = p.z * w, pitch = p.pitch * w, yaw = p.yaw * w, roll = p.roll * w, lowered = p.lowered * w };
        }

        /// <summary>The lowering: 0 before, up to 1 in the first <see cref="DownEnd"/> of the reload, held, and back to 0 from <see cref="UpStart"/> to the end.</summary>
        public static float Lowered(float p)
        {
            if (!(p > 0f) || p >= 1f) return 0f;                  // (also a NaN)
            if (p < DownEnd) return Smooth(p / DownEnd);
            if (p > UpStart) return 1f - Smooth((p - UpStart) / (1f - UpStart));
            return 1f;
        }

        /// <summary>
        /// The gun's offset when the reload has got <paramref name="p"/> of the way (0..1). <paramref name="steps"/>: where the sounds fall (see <see cref="StepFractions"/>; null = the rifle's).
        /// Zero at 0 and at 1 (and for anything outside), and it has no jump anywhere between.
        /// </summary>
        public static Pose Evaluate(float p, float[] steps)
        {
            if (!(p > 0f) || p >= 1f) return default(Pose);
            if (steps == null || steps.Length < 2) steps = DefaultSteps;
            float env = Lowered(p);
            var o = new Pose { lowered = env, x = LowX * env, y = LowY * env, z = LowZ * env, pitch = LowPitch * env, yaw = LowYaw * env, roll = LowRoll * env };
            int n = steps.Length;
            for (int k = 1; k < n - 1; k++)
            {
                float b = Bump(p, steps[k], BumpWidth) * env;
                if (b <= 0f) continue;
                if (k == 1) { o.y += TugY * b; o.pitch += TugPitch * b; }                              // the old magazine comes out
                else if (k == n - 2) { o.y += SeatY * b; o.z += SeatZ * b; o.pitch += SeatPitch * b; }  // the new one goes in
                else o.y += DipY * b;                                                                  // (a step between: the pistol's drawn magazine, a shotgun's shells)
            }
            float rack = Bump(p, steps[n - 1], RackWidth) * env;                                       // the charge: the slide, the bolt or the pump
            o.z += RackZ * rack; o.pitch += RackPitch * rack;
            return o;
        }
    }
}
