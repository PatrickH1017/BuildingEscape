using System;

namespace BuildingEscape
{
    /// <summary>
    /// Deterministic random numbers (xorshift32) for the synthesisers. UnityEngine.Random cannot be used off the main
    /// thread, and the same seed must always give the same sound, so the sound code has its own generator.
    /// </summary>
    public sealed class SynthRandom
    {
        uint s;

        public SynthRandom(int seed)
        {
            s = unchecked((uint)seed * 2654435761u + 0x9E3779B9u);
            if (s == 0) s = 1;
            Next(); Next();
        }

        uint Next()
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return s;
        }

        /// <summary>0 up to (not including) 1.</summary>
        public float Value() => (Next() >> 8) * (1f / 16777216f);
        /// <summary>-1 to 1.</summary>
        public float Signed() => Value() * 2f - 1f;
        public float Range(float a, float b) => a + (b - a) * Value();
        /// <summary>0 .. n-1.</summary>
        public int Int(int n) => n <= 1 ? 0 : (int)(Next() % (uint)n);
        public bool Chance(float p) => Value() < p;
    }

    /// <summary>
    /// Small DSP building blocks shared by the placeholder sounds and the music. Plain C# (no Unity types), so it also
    /// runs on a worker thread and in the tests outside Unity. All audio is mono, <see cref="SampleRate"/> Hz.
    /// </summary>
    public static class Dsp
    {
        public const int SampleRate = 32000;
        public const float TwoPi = 6.28318531f;

        public static float MidiToHz(float midi) => 440f * MathF.Pow(2f, (midi - 69f) / 12f);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static int Samples(float seconds) => Math.Max(1, (int)MathF.Round(seconds * SampleRate));

        /// <summary>Multiply an envelope by this every sample and it falls by 1/e every <paramref name="seconds"/>.</summary>
        public static float DecayPerSample(float seconds) => MathF.Exp(-1f / (Math.Max(0.0001f, seconds) * SampleRate));

        // ---- waveforms from a phase in [0, 1)
        public static float Sine(float ph) => MathF.Sin(ph * TwoPi);
        public static float Triangle(float ph) => 4f * MathF.Abs(ph - 0.5f) - 1f;

        /// <summary>Band-limited sawtooth (PolyBLEP): no harsh aliasing on high notes. dt = frequency / sample rate.</summary>
        public static float Saw(float ph, float dt) => 2f * ph - 1f - PolyBlep(ph, dt);

        /// <summary>Band-limited pulse wave; duty 0.5 is a square.</summary>
        public static float Pulse(float ph, float dt, float duty)
        {
            float v = ph < duty ? 1f : -1f;
            v += PolyBlep(ph, dt);
            float shifted = ph - duty;
            if (shifted < 0f) shifted += 1f;
            v -= PolyBlep(shifted, dt);
            return v;
        }

        static float PolyBlep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }

        /// <summary>Gentle saturation: never leaves -1..1.</summary>
        public static float SoftClip(float x) => MathF.Tanh(x);

        /// <summary>Adds a value into a looping buffer: an index past the end wraps to the start (the note keeps playing into the next loop).</summary>
        public static void MixWrap(float[] buf, int index, float v)
        {
            if (index >= buf.Length) index -= buf.Length;
            buf[index] += v;
        }

        /// <summary>Scales the buffer so its loudest sample is <paramref name="peak"/>.</summary>
        public static void Normalize(float[] buf, float peak)
        {
            float max = 0f;
            for (int i = 0; i < buf.Length; i++) { float a = MathF.Abs(buf[i]); if (a > max) max = a; }
            if (max < 1e-6f) return;
            float g = peak / max;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        /// <summary>Short linear fades at both ends so a one-shot never starts or stops with a click.</summary>
        public static void FadeEnds(float[] buf, float seconds)
        {
            int n = Math.Min(Samples(seconds), buf.Length / 2);
            for (int i = 0; i < n; i++)
            {
                float g = i / (float)n;
                buf[i] *= g;
                buf[buf.Length - 1 - i] *= g;
            }
        }
    }

    /// <summary>
    /// Zero-delay-feedback state variable filter: low-, band- and high-pass outputs at once. Stable for any cutoff and cheap.
    /// </summary>
    public struct Svf
    {
        float ic1, ic2, a1, a2, a3, k;

        /// <summary>q around 0.7 is flat; higher rings at the cutoff.</summary>
        public void Set(float cutoffHz, float q)
        {
            cutoffHz = Dsp.Clamp(cutoffHz, 20f, Dsp.SampleRate * 0.45f);
            float g = MathF.Tan(MathF.PI * cutoffHz / Dsp.SampleRate);
            k = 1f / Math.Max(0.05f, q);
            a1 = 1f / (1f + g * (g + k));
            a2 = g * a1;
            a3 = g * a2;
        }

        public void Reset() { ic1 = 0f; ic2 = 0f; }

        public void Process(float x, out float low, out float band, out float high)
        {
            float v3 = x - ic2;
            float v1 = a1 * ic1 + a2 * v3;
            float v2 = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2f * v1 - ic1;
            ic2 = 2f * v2 - ic2;
            low = v2; band = v1; high = x - k * v1 - v2;
        }

        public float Low(float x) { Process(x, out float l, out _, out _); return l; }
        public float Band(float x) { Process(x, out _, out float b, out _); return b; }
        public float High(float x) { Process(x, out _, out _, out float h); return h; }
    }
}
