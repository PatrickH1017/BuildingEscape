using System;

namespace BuildingEscape
{
    /// <summary>
    /// Placeholder sounds for the restraint, drag and rescue clips (the owner's text, 2026-10-28: "scuffs, a chain, grunts"), made from maths like the outpost's (<see cref="OutpostSound"/>): a body scuffing along the floor, the captive's strain,
    /// the officer's shove, a zip tie's ratchet. All of them are PLACEHOLDERS until the owner supplies sounds. Plain C# (no Unity types); the same variant always gives the same samples.
    /// </summary>
    public static class RestraintSound
    {
        const int SR = Dsp.SampleRate;
        public const int ScuffVariants = 3, StrainVariants = 3, ShoveVariants = 1, RatchetVariants = 1;

        /// <summary>A body dragged along the floor, one pull: filtered noise that swells and falls, with a little grit on it; three variants (a different grain and pitch).</summary>
        public static float[] Scuff(int variant)
        {
            var r = new SynthRandom(4101 + variant * 31);
            var o = new float[Dsp.Samples(0.4f)];
            var bp = new Svf(); bp.Set(520f + 260f * variant, 0.9f);
            var lp = new Svf(); lp.Set(2400f, 0.7f);
            float grit = 0f;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)o.Length;
                float swell = MathF.Sin(MathF.PI * t);
                if (r.Chance(0.004f)) grit = r.Range(0.5f, 1f);
                grit *= 0.985f;
                o[i] = (bp.Band(r.Signed()) * 0.8f + lp.Low(r.Signed()) * 0.25f + grit * r.Signed() * 0.5f) * swell * swell;
            }
            Dsp.FadeEnds(o, 0.01f);
            Dsp.Normalize(o, 0.55f);
            return o;
        }

        /// <summary>The captive straining against the officer: a rough falling hum (a sawtooth round 140 Hz, shaped by a vowel-like band at about 600 Hz) with breath on top, about 0.45 s; three variants, a little different in pitch.</summary>
        public static float[] Strain(int variant)
        {
            var r = new SynthRandom(5203 + variant * 17);
            var o = new float[Dsp.Samples(0.45f)];
            var vowel = new Svf(); vowel.Set(560f + 90f * variant, 2.2f);
            var breath = new Svf(); breath.Set(2600f, 0.8f);
            float ph = 0f, baseHz = 150f - 14f * variant;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)o.Length;
                float hz = baseHz * (1.15f - 0.35f * t) * (1f + 0.02f * MathF.Sin(t * 60f));
                float dt = hz / SR;
                ph += dt; ph -= (int)ph;
                float env = MathF.Min(1f, t * 12f) * MathF.Pow(1f - t, 0.8f);
                o[i] = (vowel.Band(Dsp.Saw(ph, dt)) * 1.2f + breath.High(r.Signed()) * 0.18f) * env;
            }
            Dsp.FadeEnds(o, 0.01f);
            Dsp.Normalize(o, 0.6f);
            return o;
        }

        /// <summary>The officer's shove: a low thump with a short slap of cloth on top, a quarter of a second.</summary>
        public static float[] Shove(int variant)
        {
            var r = new SynthRandom(6307 + variant);
            var o = new float[Dsp.Samples(0.26f)];
            float ph = 0f, env = 1f, dec = Dsp.DecayPerSample(0.07f);
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)o.Length;
                ph += Dsp.Lerp(120f, 52f, MathF.Min(1f, t * 4f)) / SR; ph -= (int)ph;
                o[i] = Dsp.Sine(ph) * env;
                env *= dec;
            }
            var lp = new Svf(); lp.Set(1500f, 0.8f);
            float ne = 1f, nd = Dsp.DecayPerSample(0.025f);
            for (int i = 0; i < Dsp.Samples(0.07f) && i < o.Length; i++) { o[i] += lp.Low(r.Signed()) * ne * 0.7f; ne *= nd; }
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.8f);
            return o;
        }

        /// <summary>A zip tie pulled tight (or cut): a quick run of small clicks that speeds up and rises, then one short snap, about 0.45 s.</summary>
        public static float[] Ratchet(int variant)
        {
            var r = new SynthRandom(7411 + variant);
            var o = new float[Dsp.Samples(0.45f)];
            float at = 0f, gap = 0.06f;
            while (at < 0.33f)
            {
                int s = Dsp.Samples(at);
                int len = Dsp.Samples(0.006f);
                float amp = 0.5f + 0.4f * (at / 0.33f);
                for (int i = 0; i < len && s + i < o.Length; i++) o[s + i] += (i % 2 == 0 ? 1f : -1f) * r.Range(0.6f, 1f) * amp * (1f - i / (float)len);
                at += gap; gap = MathF.Max(0.02f, gap * 0.82f);
            }
            int snap = Dsp.Samples(0.36f);
            float pe = 1f, pd = Dsp.DecayPerSample(0.02f), pph = 0f;
            for (int i = 0; snap + i < o.Length; i++) { pph += 2100f / SR; pph -= (int)pph; o[snap + i] += Dsp.Sine(pph) * pe * 0.5f; pe *= pd; }
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.6f);
            return o;
        }
    }
}
