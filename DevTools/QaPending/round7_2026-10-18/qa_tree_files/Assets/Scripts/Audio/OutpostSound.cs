using System;

namespace BuildingEscape
{
    /// <summary>
    /// Placeholder sounds for the guard outpost and the safes, made from maths like the door and radio sounds (<see cref="ProceduralSound"/>):
    /// the keypad's blips, the lock opening or shorting out, the alarm siren, the safe's bolts and hinge, a crowbar groaning in a gap and
    /// snapping, a sheet of paper. Plain C# (no Unity types); the same variant always gives the same samples.
    /// </summary>
    public static class OutpostSound
    {
        const int SR = Dsp.SampleRate;

        public const int KeypadVariants = 3, PryVariants = 3;
        /// <summary>The siren is this long, in seconds; the alarm plays it again and again.</summary>
        public const float AlarmSeconds = 3.2f;

        // ---------------------------------------------------------------- the lock

        /// <summary>One key of the keypad: a short electronic blip, a little higher for each variant.</summary>
        public static float[] KeypadBeep(int variant)
        {
            float hz = 1500f * MathF.Pow(2f, (variant % KeypadVariants) * 2f / 12f);
            int n = Dsp.Samples(0.075f);
            var o = new float[n];
            float ph = 0f, dp = hz / SR;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float env = MathF.Min(1f, i / (0.003f * SR)) * MathF.Pow(1f - t, 1.4f);
                o[i] = (Dsp.Sine(ph) * 0.8f + Dsp.Sine(ph * 2f) * 0.2f) * env;
                ph += dp; ph -= (int)ph;
            }
            Dsp.FadeEnds(o, 0.002f);
            Dsp.Normalize(o, 0.8f);
            return o;
        }

        /// <summary>The lock opens: three rising blips and the clunk of the bolt.</summary>
        public static float[] HackWin()
        {
            var o = new float[Dsp.Samples(0.8f)];
            float[] notes = { 659.3f, 784.0f, 1046.5f };
            for (int k = 0; k < notes.Length; k++)
            {
                int at = Dsp.Samples(0.13f * k), len = Dsp.Samples(0.11f);
                float ph = 0f, dp = notes[k] / SR;
                for (int i = 0; i < len && at + i < o.Length; i++)
                {
                    float edge = MathF.Min(MathF.Min(1f, i / 64f), (len - i) / 128f);
                    o[at + i] += Dsp.Sine(ph) * 0.55f * edge;
                    ph += dp; ph -= (int)ph;
                }
            }
            Clunk(o, Dsp.Samples(0.46f), 0.9f, 95f, new SynthRandom(611));
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.85f);
            return o;
        }

        /// <summary>The lock shorts out: sparks, then a falling buzzer.</summary>
        public static float[] HackFail()
        {
            var r = new SynthRandom(612);
            int n = Dsp.Samples(0.75f);
            var o = new float[n];
            var lp = new Svf();
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float hz = 320f * MathF.Pow(0.32f, t);                       // falls from 320 Hz to about 100 Hz
                ph += hz / SR; ph -= (int)ph;
                if ((i & 15) == 0) lp.Set(1800f - 1200f * t, 0.8f);
                float tremolo = 0.65f + 0.35f * MathF.Sin(t * 24f * 0.75f * Dsp.TwoPi);
                o[i] = lp.Low(Dsp.Pulse(ph, hz / SR, 0.5f)) * tremolo * MathF.Min(1f, i / 96f) * MathF.Min(1f, (1f - t) / 0.2f);
            }
            // sparks in the first quarter second
            for (int p = 0; p < 22; p++)
            {
                int at = (int)(r.Value() * r.Value() * Dsp.Samples(0.28f));
                float g = r.Range(0.5f, 1.3f), dec = Dsp.DecayPerSample(0.0007f), env = 1f;
                for (int j = 0; j < 90 && at + j < n; j++) { o[at + j] += g * r.Signed() * env; env *= dec; }
            }
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.9f);
            return o;
        }

        /// <summary>The alarm: a wailing siren, twice up and down. It is played again and again for as long as the alarm lasts.</summary>
        public static float[] Alarm()
        {
            int n = Dsp.Samples(AlarmSeconds);
            var o = new float[n];
            var lp = new Svf();
            lp.Set(3200f, 0.7f);
            float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float sweep = 0.5f - 0.5f * MathF.Cos(t / 1.6f * Dsp.TwoPi);              // 0..1..0 every 1.6 s
                float hz = 640f + 460f * sweep;
                ph += hz / SR; ph -= (int)ph;
                float v = Dsp.SoftClip(2.0f * Dsp.Saw(ph, hz / SR)) + 0.5f * Dsp.Pulse(ph, hz / SR, 0.5f);
                o[i] = lp.Low(v);
            }
            Dsp.FadeEnds(o, 0.02f);
            Dsp.Normalize(o, 0.8f);
            return o;
        }

        /// <summary>The alarm panel's button: a heavy relay clack, then the siren winding down (a falling tone) and two calm blips.</summary>
        public static float[] AlarmOff()
        {
            var o = new float[Dsp.Samples(1.1f)];
            Clunk(o, 0, 0.9f, 110f, new SynthRandom(1033));

            // the siren dying away: a tone that slides down from 900 Hz to 150 Hz while it fades
            int from = Dsp.Samples(0.08f), len = Dsp.Samples(0.55f);
            float ph = 0f;
            var lp = new Svf();
            lp.Set(2400f, 0.7f);
            for (int i = 0; i < len && from + i < o.Length; i++)
            {
                float t = i / (float)len;
                float hz = 900f * MathF.Pow(150f / 900f, t);
                ph += hz / SR; ph -= (int)ph;
                float v = lp.Low(Dsp.Saw(ph, hz / SR) * 0.6f + Dsp.Pulse(ph, hz / SR, 0.5f) * 0.3f);
                o[from + i] += v * MathF.Pow(1f - t, 1.5f) * MathF.Min(1f, i / 96f) * 0.7f;
            }

            // two soft blips: "all clear"
            float[] notes = { 659.3f, 523.3f };
            for (int k = 0; k < notes.Length; k++)
            {
                int at = Dsp.Samples(0.62f + 0.17f * k), n = Dsp.Samples(0.14f);
                float bp = 0f, dp = notes[k] / SR;
                for (int i = 0; i < n && at + i < o.Length; i++)
                {
                    float edge = MathF.Min(MathF.Min(1f, i / 64f), (n - i) / 256f);
                    o[at + i] += Dsp.Sine(bp) * 0.45f * edge;
                    bp += dp; bp -= (int)bp;
                }
            }
            Dsp.FadeEnds(o, 0.006f);
            Dsp.Normalize(o, 0.85f);
            return o;
        }

        // ---------------------------------------------------------------- the safe

        /// <summary>The safe opens: two heavy bolts and the long groan of the hinge.</summary>
        public static float[] SafeOpen()
        {
            var r = new SynthRandom(731);
            var o = new float[Dsp.Samples(1.5f)];
            Clunk(o, 0, 1f, 75f, r);
            Clunk(o, Dsp.Samples(0.22f), 0.8f, 62f, r);

            // the hinge: a low saw gliding up through a resonant band that sweeps, with a slow swell
            int from = Dsp.Samples(0.42f), len = o.Length - from;
            var bp = new Svf();
            float ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)len;
                float hz = 120f + 70f * t + 8f * MathF.Sin(t * 37f);
                ph += hz / SR; ph -= (int)ph;
                if ((i & 7) == 0) bp.Set(520f + 700f * t, 5f);
                float swell = MathF.Sin(MathF.PI * MathF.Min(1f, t * 1.15f)) * (0.7f + 0.3f * MathF.Sin(t * 53f));
                o[from + i] += bp.Band(Dsp.Saw(ph, hz / SR) + 0.35f * r.Signed()) * swell * 0.55f;
            }
            Dsp.FadeEnds(o, 0.006f);
            Dsp.Normalize(o, 0.88f);
            return o;
        }

        /// <summary>A wrong code: two low, flat blips.</summary>
        public static float[] SafeWrong()
        {
            var o = new float[Dsp.Samples(0.5f)];
            for (int k = 0; k < 2; k++)
            {
                int at = Dsp.Samples(0.24f * k), len = Dsp.Samples(0.17f);
                float ph = 0f, dp = 165f / SR;
                for (int i = 0; i < len && at + i < o.Length; i++)
                {
                    float edge = MathF.Min(MathF.Min(1f, i / 48f), (len - i) / 96f);
                    o[at + i] += Dsp.Pulse(ph, dp, 0.5f) * 0.5f * edge;
                    ph += dp; ph -= (int)ph;
                }
            }
            var lp = new Svf();
            lp.Set(900f, 0.8f);
            for (int i = 0; i < o.Length; i++) o[i] = lp.Low(o[i]);
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.8f);
            return o;
        }

        /// <summary>A crowbar working in the gap: a metal groan that climbs and creaks, with small cracks in it.</summary>
        public static float[] Pry(int variant)
        {
            variant = Math.Abs(variant) % PryVariants;
            var r = new SynthRandom(820 + variant * 29);
            int n = Dsp.Samples(0.95f);
            var o = new float[n];
            var bp = new Svf();
            float ph = 0f, start = 85f + 14f * variant;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float hz = start * (1f + 0.55f * t * t) * (1f + 0.04f * MathF.Sin(t * 61f));
                ph += hz / SR; ph -= (int)ph;
                if ((i & 7) == 0) bp.Set(620f + 900f * t + 120f * MathF.Sin(t * 29f), 4f);
                float creak = 0.55f + 0.45f * MathF.Sin(t * (31f + 7f * variant) + 1.3f);
                o[i] = bp.Band(Dsp.Saw(ph, hz / SR) + 0.25f * r.Signed()) * creak * MathF.Sin(MathF.PI * t);
            }
            for (int c = 0; c < 5; c++)                                             // little cracks as the metal gives
            {
                int at = (int)(r.Range(0.1f, 0.9f) * n);
                float g = r.Range(0.4f, 0.9f), dec = Dsp.DecayPerSample(0.002f), env = 1f;
                for (int j = 0; j < 200 && at + j < n; j++) { o[at + j] += g * r.Signed() * env; env *= dec; }
            }
            Dsp.FadeEnds(o, 0.008f);
            Dsp.Normalize(o, 0.85f);
            return o;
        }

        /// <summary>The crowbar snaps: a hard crack and a short ring of steel.</summary>
        public static float[] CrowbarSnap()
        {
            var r = new SynthRandom(901);
            int n = Dsp.Samples(0.7f);
            var o = new float[n];
            var hp = new Svf();
            hp.Set(1500f, 0.7f);
            float dec = Dsp.DecayPerSample(0.004f), env = 1f;
            for (int i = 0; i < Dsp.Samples(0.03f) && i < n; i++) { o[i] += hp.High(r.Signed()) * env * 1.4f; env *= dec; }

            float[] ratio = { 1f, 2.76f, 5.40f, 8.93f }, amp = { 1f, 0.6f, 0.3f, 0.14f }, tau = { 0.16f, 0.09f, 0.05f, 0.025f };
            for (int p = 0; p < ratio.Length; p++)
            {
                float hz = 1250f * ratio[p];
                float pd = Dsp.DecayPerSample(tau[p]), pe = 1f, pph = 0f, dp = hz / SR;
                for (int i = 0; i < n && pe > 0.0005f; i++)
                {
                    o[i] += amp[p] * 0.7f * Dsp.Sine(pph) * pe;
                    pph += dp; pph -= (int)pph;
                    pe *= pd;
                }
            }
            Clunk(o, 0, 0.5f, 140f, r);
            Dsp.FadeEnds(o, 0.003f);
            Dsp.Normalize(o, 0.9f);
            return o;
        }

        /// <summary>The security computer locks a door: a hard click, a heavy bolt thrown and seated (two thunks) and a long ring of steel. Loud: played at full volume for an executive suite or the server room.</summary>
        public static float[] MetalLock()
        {
            var r = new SynthRandom(977);
            int n = Dsp.Samples(1.15f);
            var o = new float[n];
            var hp = new Svf();
            hp.Set(2200f, 0.7f);
            float dec = Dsp.DecayPerSample(0.003f), env = 1f;
            for (int i = 0; i < Dsp.Samples(0.025f) && i < n; i++) { o[i] += hp.High(r.Signed()) * env * 1.2f; env *= dec; }      // (the first sharp click)

            Clunk(o, 0, 0.9f, 95f, r);                                                  // the bolt is thrown...
            Clunk(o, Dsp.Samples(0.11f), 1.0f, 120f, r);                                // ...and seats against the frame

            float[] ratio = { 1f, 2.32f, 4.17f, 6.61f }, amp = { 1f, 0.65f, 0.35f, 0.18f }, tau = { 0.38f, 0.22f, 0.12f, 0.06f };
            int ringAt = Dsp.Samples(0.11f);
            for (int p = 0; p < ratio.Length; p++)
            {
                float hz = 640f * ratio[p];
                float pd = Dsp.DecayPerSample(tau[p]), pe = 1f, pph = 0f, dp = hz / SR;
                for (int i = ringAt; i < n && pe > 0.0005f; i++)
                {
                    o[i] += amp[p] * 0.55f * Dsp.Sine(pph) * pe;
                    pph += dp; pph -= (int)pph;
                    pe *= pd;
                }
            }
            Dsp.FadeEnds(o, 0.003f);
            Dsp.Normalize(o, 0.95f);
            return o;
        }

        /// <summary>A sheet of paper taken off a desk.</summary>
        public static float[] Paper()
        {
            var r = new SynthRandom(955);
            int n = Dsp.Samples(0.42f);
            var o = new float[n];
            var bp = new Svf();
            bp.Set(3800f, 0.9f);
            float level = 0f, target = 0f;
            int next = 0;
            for (int i = 0; i < n; i++)
            {
                if (i >= next)
                {
                    target = r.Chance(0.25f) ? 0.1f : r.Range(0.4f, 1f);
                    next = i + Dsp.Samples(r.Range(0.02f, 0.07f));
                }
                level += (target - level) * 0.02f;
                float t = i / (float)n;
                o[i] = bp.Band(r.Signed()) * level * MathF.Sin(MathF.PI * t);
            }
            Dsp.FadeEnds(o, 0.01f);
            Dsp.Normalize(o, 0.6f);
            return o;
        }

        // ---------------------------------------------------------------- pieces

        /// <summary>A heavy metal thunk: a falling sine, a dull click and a short ring.</summary>
        public const int PunchVariants = 3;

        /// <summary>A fist landing on a body: a low thump with a short dull slap on top; three variants, a little different in pitch.</summary>
        public static float[] Punch(int variant)
        {
            var r = new SynthRandom(2301 + variant * 17);
            var o = new float[Dsp.Samples(0.28f)];
            Clunk(o, 0, 0.95f, 72f + 14f * variant, r);
            var lp = new Svf();
            lp.Set(1800f - 250f * variant, 0.8f);
            float dec = Dsp.DecayPerSample(0.03f), env = 1f;
            for (int i = 0; i < Dsp.Samples(0.06f) && i < o.Length; i++) { o[i] += lp.Low(r.Signed()) * env * 0.8f; env *= dec; }
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.9f);
            return o;
        }

        static void Clunk(float[] o, int at, float gain, float hz, SynthRandom r)
        {
            float dec = Dsp.DecayPerSample(0.07f), env = 1f, ph = 0f;
            for (int i = 0; at + i < o.Length && env > 0.001f; i++)
            {
                float f = hz * (0.8f + 0.9f * env);
                ph += f / SR; ph -= (int)ph;
                o[at + i] += Dsp.Sine(ph) * env * gain;
                env *= dec;
            }
            var lp = new Svf();
            lp.Set(1400f, 0.9f);
            int click = Dsp.Samples(0.012f);
            for (int i = 0; i < click && at + i < o.Length; i++) o[at + i] += lp.Low(r.Signed()) * (1f - i / (float)click) * gain * 0.9f;
            float ring = 0f, rd = Dsp.DecayPerSample(0.05f), re = 1f, rdp = (hz * 5.1f) / SR;
            for (int i = 0; at + i < o.Length && re > 0.002f; i++)
            {
                o[at + i] += Dsp.Sine(ring) * re * gain * 0.18f;
                ring += rdp; ring -= (int)ring;
                re *= rd;
            }
        }
    }
}
