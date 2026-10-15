using System;

namespace BuildingEscape
{
    /// <summary>
    /// Placeholder sound effects made from maths: sliding doors, the elevator "ding", officers' key rings and walkie-talkie
    /// static. They let the game ship door, key and radio sounds before real recordings exist; SoundBank uses a real clip
    /// instead whenever one is assigned in the AudioCatalog. Plain C# (no Unity types): builds a mono float[] at
    /// <see cref="Dsp.SampleRate"/>, the same for the same variant every time.
    /// </summary>
    public static class ProceduralSound
    {
        const int SR = Dsp.SampleRate;

        public const int DoorVariants = 3, KeyVariants = 4, RadioVariants = 5, CallVariants = 3;

        // ---------------------------------------------------------------- doors

        public static float[] DoorOpen(int variant) => DoorSlide(variant, true);
        public static float[] DoorClose(int variant) => DoorSlide(variant, false);

        /// <summary>An automatic sliding door: a relay click, a motor whine and an air whoosh; closing ends with a latch thud.</summary>
        static float[] DoorSlide(int variant, bool opening)
        {
            var r = new SynthRandom((opening ? 101 : 202) + variant * 17);
            int n = Dsp.Samples(opening ? 0.60f : 0.66f);
            var whoosh = new float[n]; var hum = new float[n]; var click = new float[n]; var thud = new float[n];

            // whoosh: noise through a band-pass whose centre sweeps up (opening) or down (closing)
            float lo = 450f + 60f * variant, hi = 2300f - 150f * variant;
            var bp = new Svf(); var rolloff = new Svf();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n, sweep = MathF.Min(1f, t / 0.8f);
                if ((i & 7) == 0)
                {
                    float centre = opening ? lo * MathF.Pow(hi / lo, sweep) : hi * MathF.Pow(lo / hi, sweep);
                    bp.Set(centre, 1.3f);
                    rolloff.Set(centre * 2.2f, 0.7f);
                }
                whoosh[i] = rolloff.Low(bp.Band(r.Signed())) * MathF.Sin(sweep * MathF.PI);
            }

            // motor: a low sawtooth gliding up (opening) or down (closing)
            float hz = 88f + 11f * variant, ph = 0f;
            var lp = new Svf();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n, f = hz * (opening ? 1f + 0.4f * t : 1.4f - 0.4f * t);
                ph += f / SR; ph -= (int)ph;
                if ((i & 15) == 0) lp.Set(650f, 0.8f);
                hum[i] = lp.Low(Dsp.Saw(ph, f / SR)) * MathF.Min(1f, t / 0.1f) * MathF.Min(1f, (1f - t) / 0.25f);
            }

            // relay click as it starts
            AddClick(click, 0, r, 0.004f, 1f);

            // closing: a latch thud near the end (a falling sine plus a dull click)
            if (!opening)
            {
                int at = n - Dsp.Samples(0.22f);
                float dec = Dsp.DecayPerSample(0.055f), env = 1f, tph = 0f;
                for (int i = 0; at + i < n; i++)
                {
                    float f = 52f + 70f * env;
                    tph += f / SR; tph -= (int)tph;
                    thud[at + i] = Dsp.Sine(tph) * env;
                    env *= dec;
                }
                AddClick(click, at, r, 0.006f, 0.8f);
            }

            var o = new float[n];
            Mix(o, whoosh, opening ? 0.50f : 0.45f);
            Mix(o, hum, 0.28f);
            Mix(o, click, 0.45f);
            Mix(o, thud, opening ? 0f : 0.95f);
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.9f);
            return o;
        }

        // ---------------------------------------------------------------- elevator

        /// <summary>The two-note "ding-dong" of an elevator arriving.</summary>
        public static float[] ElevatorDing()
        {
            var o = new float[Dsp.Samples(1.6f)];
            Bell(o, 0, 1318.5f, 0.85f);                       // E6
            Bell(o, Dsp.Samples(0.50f), 1046.5f, 1.0f);       // C6
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.9f);
            return o;
        }

        static readonly float[] BellRatio = { 1f, 2.01f, 3.0f, 4.2f };
        static readonly float[] BellAmp = { 1f, 0.35f, 0.18f, 0.08f };
        static readonly float[] BellTau = { 0.55f, 0.35f, 0.22f, 0.12f };

        static void Bell(float[] o, int at, float hz, float gain)
        {
            for (int p = 0; p < BellRatio.Length; p++)
            {
                float dec = Dsp.DecayPerSample(BellTau[p]), env = 1f, ph = 0f, dp = hz * BellRatio[p] / SR;
                for (int i = 0; at + i < o.Length && env > 0.0003f; i++)
                {
                    float attack = i < 32 ? i / 32f : 1f;
                    o[at + i] += gain * BellAmp[p] * Dsp.Sine(ph) * env * attack;
                    ph += dp; ph -= (int)ph;
                    env *= dec;
                }
            }
        }

        // ---------------------------------------------------------------- officers

        /// <summary>A ring of keys on a belt: a handful of small metallic clinks. Running makes more of them.</summary>
        public static float[] Keys(int variant, bool run)
        {
            var r = new SynthRandom(300 + variant * 31 + (run ? 7 : 0));
            int n = Dsp.Samples(run ? 0.55f : 0.42f);
            var o = new float[n];
            int clinks = run ? 9 + r.Int(5) : 5 + r.Int(3);
            // inharmonic partials, like a small bar of metal
            float[] ratio = { 1f, 2.76f, 5.40f, 8.93f }, amp = { 1f, 0.55f, 0.28f, 0.12f };
            for (int c = 0; c < clinks; c++)
            {
                int at = (int)(r.Value() * r.Value() * n * 0.55f);   // most of them close to the start
                float f0 = r.Range(2300f, 5400f), gain = r.Range(0.35f, 1f), tau = r.Range(0.035f, 0.085f);
                for (int p = 0; p < ratio.Length; p++)
                {
                    float hz = f0 * ratio[p];
                    if (hz > 14000f) break;
                    float dec = Dsp.DecayPerSample(tau / (1f + p * 0.7f)), env = 1f, ph = 0f, dp = hz / SR;
                    for (int i = 0; at + i < n && env > 0.001f; i++)
                    {
                        o[at + i] += gain * amp[p] * Dsp.Sine(ph) * env;
                        ph += dp; ph -= (int)ph;
                        env *= dec;
                    }
                }
                AddClick(o, at, r, 0.0012f, gain * 0.5f);
            }
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.85f);
            return o;
        }

        /// <summary>Walkie-talkie static: wandering hiss, a few pops and, on some variants, a short key-up beep.</summary>
        public static float[] RadioStatic(int variant)
        {
            var r = new SynthRandom(400 + variant * 53);
            int n = Dsp.Samples(0.55f + 0.18f * (variant % 5));
            var o = new float[n];
            Hiss(o, 0, n, r, 1f, 0.5f);
            AddPops(o, r, 3 + r.Int(6));
            if (variant % 2 == 0) AddBeep(o, n - Dsp.Samples(0.11f), 1180f, 0.06f, 0.5f);
            Envelope(o, 0.015f, 0.09f);
            Dsp.Normalize(o, 0.85f);
            return o;
        }

        /// <summary>An officer keys the radio to call for backup: squelch burst, garbled mumbling (no real words), closing squelch and a roger beep.</summary>
        public static float[] RadioCall(int variant)
        {
            var r = new SynthRandom(500 + variant * 71);
            int n = Dsp.Samples(1.05f + 0.15f * variant);
            var o = new float[n];
            int open = Dsp.Samples(0.10f), close = Dsp.Samples(0.09f), tail = Dsp.Samples(0.14f);
            int bodyStart = open, bodyEnd = n - close - tail;
            Hiss(o, 0, open, r, 1f, 0.0f);                                     // squelch opens
            AddBeep(o, open / 2, 1600f, 0.05f, 0.35f);

            var voice = new float[n];
            Mumble(voice, bodyStart, bodyEnd, r);
            Hiss(o, bodyStart, bodyEnd, r, 0.18f, 0.4f);                       // a little hiss under the voice
            for (int i = bodyStart; i < bodyEnd; i++) o[i] += voice[i] * 0.55f;

            Hiss(o, bodyEnd, n - tail, r, 0.9f, 0.0f);                         // squelch closes
            AddBeep(o, n - Dsp.Samples(0.13f), 1500f, 0.05f, 0.45f);
            AddBeep(o, n - Dsp.Samples(0.07f), 1000f, 0.05f, 0.45f);
            AddPops(o, r, 4);
            Envelope(o, 0.004f, 0.03f);
            Dsp.Normalize(o, 0.85f);
            return o;
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>Radio-band noise (about 420-3300 Hz, like a small speaker) whose level wanders every 15-60 ms. roughness 0 = steady.</summary>
        static void Hiss(float[] o, int from, int to, SynthRandom r, float gain, float roughness)
        {
            var hp = new Svf(); hp.Set(420f, 0.7f);
            var lp = new Svf(); lp.Set(3300f, 0.7f);
            float level = 0.6f, target = 0.6f;
            int next = from;
            for (int i = from; i < to && i < o.Length; i++)
            {
                if (i >= next)
                {
                    target = Dsp.Lerp(0.7f, r.Range(0.05f, 1f), roughness);
                    next = i + Dsp.Samples(r.Range(0.015f, 0.06f));
                }
                level += (target - level) * 0.01f;
                o[i] += lp.Low(hp.High(r.Signed())) * level * gain * 1.6f;
            }
        }

        static readonly float[] VowelF1 = { 730f, 530f, 270f, 570f, 300f, 660f };
        static readonly float[] VowelF2 = { 1090f, 1840f, 2290f, 840f, 870f, 1720f };

        /// <summary>
        /// Speech-like mumble that says nothing: a buzzing voice through two moving "vowel" filters, in syllable-sized swells with
        /// pauses, squashed and band-limited like a walkie-talkie. Written into <paramref name="o"/> between the two sample indexes.
        /// </summary>
        static void Mumble(float[] o, int from, int to, SynthRandom r)
        {
            var f1 = new Svf(); var f2 = new Svf();
            var hp = new Svf(); hp.Set(380f, 0.7f);
            var lp = new Svf(); lp.Set(3200f, 0.7f);
            float ph = 0f, pitch = r.Range(95f, 130f);
            float a1 = 700f, a2 = 1100f, b1 = 700f, b2 = 1100f;
            int syllStart = from, syllEnd = from;
            bool voiced = false;
            for (int i = from; i < to && i < o.Length; i++)
            {
                if (i >= syllEnd)
                {
                    syllStart = i;
                    syllEnd = i + Dsp.Samples(r.Range(0.09f, 0.2f));
                    a1 = b1; a2 = b2;
                    int v = r.Int(VowelF1.Length);
                    b1 = VowelF1[v]; b2 = VowelF2[v];
                    voiced = !r.Chance(0.2f);
                    pitch = Dsp.Clamp(pitch * r.Range(0.88f, 1.14f), 85f, 150f);
                }
                float t = (i - syllStart) / (float)(syllEnd - syllStart);
                if ((i & 15) == 0) { f1.Set(Dsp.Lerp(a1, b1, t), 5f); f2.Set(Dsp.Lerp(a2, b2, t), 6f); }
                ph += pitch / SR; ph -= (int)ph;
                float src = Dsp.Saw(ph, pitch / SR);
                float v2 = f1.Band(src) + 0.7f * f2.Band(src);
                float env = voiced ? MathF.Sin(MathF.PI * t) : 0f;
                o[i] = lp.Low(hp.High(Dsp.SoftClip(v2 * env * 1.5f)));
            }
            // level it so the caller can balance it against the hiss
            float peak = 0f;
            for (int i = from; i < to && i < o.Length; i++) peak = MathF.Max(peak, MathF.Abs(o[i]));
            if (peak > 1e-6f) for (int i = from; i < to && i < o.Length; i++) o[i] /= peak;
        }

        /// <summary>Short random clicks, as loud as the loudest part of what is already in the buffer.</summary>
        static void AddPops(float[] o, SynthRandom r, int count)
        {
            float peak = 0f;
            for (int i = 0; i < o.Length; i++) peak = MathF.Max(peak, MathF.Abs(o[i]));
            for (int p = 0; p < count; p++)
            {
                int at = r.Int(Math.Max(1, o.Length - 60));
                float g = peak * r.Range(0.8f, 1.8f);
                float dec = Dsp.DecayPerSample(0.0008f), env = 1f;
                for (int j = 0; j < 48 && at + j < o.Length; j++) { o[at + j] += g * r.Signed() * env; env *= dec; }
            }
        }

        static void AddBeep(float[] o, int at, float hz, float seconds, float gain)
        {
            if (at < 0) at = 0;
            int len = Dsp.Samples(seconds);
            float ph = 0f, dp = hz / SR;
            float peak = 0f;
            for (int i = 0; i < o.Length; i++) peak = MathF.Max(peak, MathF.Abs(o[i]));
            if (peak < 0.001f) peak = 0.3f;
            for (int i = 0; i < len && at + i < o.Length; i++)
            {
                float edge = MathF.Min(1f, MathF.Min(i, len - i) / 48f);
                o[at + i] += Dsp.Sine(ph) * edge * gain * peak;
                ph += dp; ph -= (int)ph;
            }
        }

        /// <summary>A very short decaying noise burst (a switch, a key hitting metal).</summary>
        static void AddClick(float[] o, int at, SynthRandom r, float seconds, float gain)
        {
            int len = Dsp.Samples(seconds);
            for (int i = 0; i < len && at + i < o.Length; i++) o[at + i] += gain * r.Signed() * (1f - i / (float)len);
        }

        static void Envelope(float[] o, float attack, float release)
        {
            int a = Dsp.Samples(attack), rl = Dsp.Samples(release);
            for (int i = 0; i < o.Length; i++)
            {
                float g = 1f;
                if (i < a) g = i / (float)a;
                int fromEnd = o.Length - 1 - i;
                if (fromEnd < rl) g = MathF.Min(g, fromEnd / (float)rl);
                o[i] *= g;
            }
        }

        /// <summary>Adds src into dst after scaling src to a peak of 1 and then by gain, so every part is balanced by gain alone.</summary>
        static void Mix(float[] dst, float[] src, float gain)
        {
            if (gain <= 0f) return;
            float peak = 0f;
            for (int i = 0; i < src.Length; i++) peak = MathF.Max(peak, MathF.Abs(src[i]));
            if (peak < 1e-6f) return;
            float g = gain / peak;
            int n = Math.Min(dst.Length, src.Length);
            for (int i = 0; i < n; i++) dst[i] += src[i] * g;
        }
    }
}
