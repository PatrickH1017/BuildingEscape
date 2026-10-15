using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// Turns a <see cref="TrackSpec"/> into a seamlessly looping mono clip (placeholder soundtrack, see MusicPlan).
    /// Plain C#, no Unity types: it takes a second or so, so MusicDirector runs it on a worker thread and only creates
    /// the AudioClip on the main thread. The same spec always gives the same samples.
    /// </summary>
    public static class ProceduralMusic
    {
        /// <summary>One loop of an intense track: <see cref="TrackSpec.Bars"/> bars at the track's tempo.</summary>
        public static float[] RenderIntense(TrackSpec spec) => new IntenseRenderer(spec).Render();

        /// <summary>One loop of the lounge ("elevator music") track.</summary>
        public static float[] RenderLounge() => new LoungeRenderer().Render();

        /// <summary>
        /// Adds a feedback echo to a looping buffer: out[i] = in[i] + feedback * out[i - delay], where the echo of the end of the loop
        /// wraps round onto the start so the loop stays seamless. Going round three times settles the circular recursion.
        /// </summary>
        internal static void LoopingEcho(float[] buf, int delay, float feedback)
        {
            int n = buf.Length;
            if (delay <= 0 || delay >= n) return;
            var dry = (float[])buf.Clone();
            for (int pass = 0; pass < 3; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = i - delay; if (j < 0) j += n;
                    buf[i] = dry[i] + feedback * buf[j];
                }
            }
        }

        /// <summary>Scales to a target RMS, soft-clips what still pokes out and finally sets the peak. Gives dense music a steady loudness.</summary>
        internal static void Master(float[] buf, float preClipRms, float peak)
        {
            double sum = 0;
            for (int i = 0; i < buf.Length; i++) sum += (double)buf[i] * buf[i];
            float rms = (float)Math.Sqrt(sum / Math.Max(1, buf.Length));
            if (rms < 1e-6f) return;
            float g = preClipRms / rms;
            for (int i = 0; i < buf.Length; i++) buf[i] = Dsp.SoftClip(buf[i] * g);
            Dsp.Normalize(buf, peak);
        }
    }

    // ------------------------------------------------------------------------------------------------ drum data

    /// <summary>One bar of each drum part, sixteen steps (sixteenth notes). x = hit, X = accent, g = ghost (quiet), o = open hat, . = rest.</summary>
    sealed class DrumPattern
    {
        public readonly string Kick, KickAlt, Snare, Hat;
        public DrumPattern(string kick, string kickAlt, string snare, string hat)
        {
            Kick = kick; KickAlt = kickAlt; Snare = snare; Hat = hat;
            if (kick.Length != 16 || kickAlt.Length != 16 || snare.Length != 16 || hat.Length != 16) throw new ArgumentException("drum patterns are 16 steps");
        }
    }

    /// <summary>How the drums of a kit sound.</summary>
    struct KitSound
    {
        public float KickHz0, KickHz1, KickSweep, KickTau, KickDrive, KickGain;   // pitch drops from Hz0 to Hz1
        public float SnareTone, SnareTau, SnareNoise, ClapMix, SnareGain;
        public float HatCut, HatGain;
        public float Duck;          // how far the kick pushes the other instruments down
        public bool FillToms;       // fills use toms instead of a snare roll
    }

    // ------------------------------------------------------------------------------------------------ the renderer

    /// <summary>
    /// A small step sequencer and software synth. Four stems (drums, bass, lead, pad) are written into float buffers the length
    /// of the loop; anything that rings past the end wraps round to the start so the loop has no seam. The kick ducks the other
    /// stems ("sidechain"), then everything is mixed, soft-clipped and levelled.
    /// </summary>
    sealed class IntenseRenderer
    {
        const int SR = Dsp.SampleRate;

        static readonly DrumPattern[] Patterns =
        {
            // FourFloor
            new DrumPattern("x...x...x...x...", "x...x...x...x.xx", "....X.......X...", "xgogxgogxgogxgog"),
            // Breakbeat
            new DrumPattern("x.....x...x.....", "x..x..x...x...x.", "....X..g....X..g", "x.x.x.x.x.x.x.xg"),
            // HalfTime
            new DrumPattern("x.......x.x.....", "x.......x.x..x..", "........X.......", "x.x.x.x.x.x.x.x."),
            // Gallop
            new DrumPattern("x.xx.xx.x.xx.xx.", "x.xx.xx.xxxxxxxx", "....X.......X...", "x.x.x.x.x.x.x.x."),
            // DrumAndBass
            new DrumPattern("x.........x.....", "x.........x.x...", "....X..g....X..g", "x.x.x.o.x.x.x.o."),
            // Industrial
            new DrumPattern("x..x..x...x.x...", "x..x..x...x.xx..", "....X..g....X...", "XgxgXgxgXgxgXgxg"),
            // Trap
            new DrumPattern("x..x......x.x...", "x..x......x.xx..", "........X.......", "x.x.x.xxx.x.x.xx"),
            // Punk
            new DrumPattern("x.x...x.x.x...x.", "x.x...x.x.x.x.x.", "....X.......X...", "x.x.x.x.x.x.x.o."),
        };

        static readonly KitSound[] Kits =
        {
            // FourFloor
            new KitSound { KickHz0 = 150, KickHz1 = 48, KickSweep = 0.030f, KickTau = 0.12f, KickDrive = 1.4f, KickGain = 1.0f,
                           SnareTone = 190, SnareTau = 0.10f, SnareNoise = 0.55f, ClapMix = 0.8f, SnareGain = 0.8f, HatCut = 7000, HatGain = 0.35f, Duck = 0.70f },
            // Breakbeat
            new KitSound { KickHz0 = 170, KickHz1 = 55, KickSweep = 0.025f, KickTau = 0.09f, KickDrive = 1.2f, KickGain = 0.95f,
                           SnareTone = 200, SnareTau = 0.13f, SnareNoise = 0.65f, ClapMix = 0.2f, SnareGain = 0.9f, HatCut = 6500, HatGain = 0.35f, Duck = 0.40f },
            // HalfTime
            new KitSound { KickHz0 = 130, KickHz1 = 42, KickSweep = 0.040f, KickTau = 0.26f, KickDrive = 1.3f, KickGain = 1.0f,
                           SnareTone = 180, SnareTau = 0.17f, SnareNoise = 0.60f, ClapMix = 0.3f, SnareGain = 1.0f, HatCut = 6500, HatGain = 0.30f, Duck = 0.30f, FillToms = true },
            // Gallop
            new KitSound { KickHz0 = 180, KickHz1 = 58, KickSweep = 0.020f, KickTau = 0.075f, KickDrive = 1.8f, KickGain = 0.9f,
                           SnareTone = 210, SnareTau = 0.12f, SnareNoise = 0.70f, ClapMix = 0.0f, SnareGain = 0.9f, HatCut = 7500, HatGain = 0.30f, Duck = 0.15f, FillToms = true },
            // DrumAndBass
            new KitSound { KickHz0 = 165, KickHz1 = 52, KickSweep = 0.022f, KickTau = 0.09f, KickDrive = 1.3f, KickGain = 0.95f,
                           SnareTone = 220, SnareTau = 0.12f, SnareNoise = 0.70f, ClapMix = 0.0f, SnareGain = 1.0f, HatCut = 8000, HatGain = 0.30f, Duck = 0.45f },
            // Industrial
            new KitSound { KickHz0 = 140, KickHz1 = 45, KickSweep = 0.030f, KickTau = 0.14f, KickDrive = 3.0f, KickGain = 1.0f,
                           SnareTone = 170, SnareTau = 0.16f, SnareNoise = 0.80f, ClapMix = 0.5f, SnareGain = 1.0f, HatCut = 5500, HatGain = 0.25f, Duck = 0.50f, FillToms = true },
            // Trap
            new KitSound { KickHz0 = 95, KickHz1 = 45, KickSweep = 0.050f, KickTau = 0.45f, KickDrive = 1.1f, KickGain = 1.0f,
                           SnareTone = 200, SnareTau = 0.15f, SnareNoise = 0.50f, ClapMix = 0.7f, SnareGain = 0.9f, HatCut = 7500, HatGain = 0.30f, Duck = 0.30f },
            // Punk
            new KitSound { KickHz0 = 170, KickHz1 = 60, KickSweep = 0.025f, KickTau = 0.09f, KickDrive = 1.5f, KickGain = 0.95f,
                           SnareTone = 200, SnareTau = 0.14f, SnareNoise = 0.75f, ClapMix = 0.0f, SnareGain = 1.0f, HatCut = 6500, HatGain = 0.30f, Duck = 0.25f },
        };

        // ---- bass voices: how each BassStyle sounds (the notes themselves come from PlayBass)
        struct BassVoice { public float Saw, Sine, Drive, CutBase, CutPeak, FilterTau, Q, LfoPerBeat, Gain; }

        static readonly BassVoice[] BassVoices =
        {
            /* Sub        */ new BassVoice { Saw = 0.0f, Sine = 1.0f, Drive = 1.8f, CutBase = 300,  CutPeak = 300,  FilterTau = 0.10f, Q = 0.7f, Gain = 1.00f },
            /* Pump       */ new BassVoice { Saw = 0.8f, Sine = 0.5f, Drive = 1.3f, CutBase = 260,  CutPeak = 1500, FilterTau = 0.07f, Q = 1.2f, Gain = 0.85f },
            /* Gallop     */ new BassVoice { Saw = 0.9f, Sine = 0.4f, Drive = 1.6f, CutBase = 320,  CutPeak = 1100, FilterTau = 0.05f, Q = 1.0f, Gain = 0.85f },
            /* Offbeat    */ new BassVoice { Saw = 0.8f, Sine = 0.5f, Drive = 1.3f, CutBase = 280,  CutPeak = 1800, FilterTau = 0.12f, Q = 1.4f, Gain = 0.90f },
            /* Roll       */ new BassVoice { Saw = 0.9f, Sine = 0.4f, Drive = 1.4f, CutBase = 300,  CutPeak = 1300, FilterTau = 0.06f, Q = 1.1f, Gain = 0.80f },
            /* Wobble     */ new BassVoice { Saw = 1.0f, Sine = 0.5f, Drive = 1.8f, CutBase = 140,  CutPeak = 1700, FilterTau = 1.00f, Q = 2.4f, LfoPerBeat = 2f, Gain = 0.80f },
            /* Syncopated */ new BassVoice { Saw = 0.8f, Sine = 0.6f, Drive = 1.4f, CutBase = 300,  CutPeak = 1600, FilterTau = 0.09f, Q = 1.3f, Gain = 0.90f },
        };

        // ---- lead voices
        enum Wave { Saw2, Pulse, Square }
        struct LeadVoice { public Wave Wave; public float Attack, Release, CutBase, CutPeak, FilterTau, Q, Vibrato, Detune, Gain; }

        static readonly LeadVoice VoiceArp      = new LeadVoice { Wave = Wave.Pulse, Attack = 0.002f, Release = 0.03f, CutBase = 1400, CutPeak = 5200, FilterTau = 0.08f, Q = 1.3f, Detune = 9,  Gain = 0.75f };
        static readonly LeadVoice VoiceRiff     = new LeadVoice { Wave = Wave.Saw2,  Attack = 0.004f, Release = 0.05f, CutBase = 1100, CutPeak = 4200, FilterTau = 0.18f, Q = 1.4f, Detune = 11, Gain = 0.90f };
        static readonly LeadVoice VoiceStab     = new LeadVoice { Wave = Wave.Saw2,  Attack = 0.003f, Release = 0.04f, CutBase = 900,  CutPeak = 6000, FilterTau = 0.10f, Q = 1.1f, Detune = 14, Gain = 0.40f };
        static readonly LeadVoice VoiceLong     = new LeadVoice { Wave = Wave.Saw2,  Attack = 0.030f, Release = 0.25f, CutBase = 1500, CutPeak = 2600, FilterTau = 0.60f, Q = 0.9f, Vibrato = 0.18f, Detune = 7, Gain = 0.85f };
        static readonly LeadVoice VoiceOstinato = new LeadVoice { Wave = Wave.Saw2,  Attack = 0.002f, Release = 0.02f, CutBase = 700,  CutPeak = 3000, FilterTau = 0.05f, Q = 1.2f, Detune = 6,  Gain = 0.70f };

        static readonly string[] RiffRhythms =
        {
            "x..x..x.x..x..x.", "x.x..x.x.x..x.x.", "xx.xx.xx.xx.xx.x", "x...x.x.x...x.xx",
            "x.xx.x.xx.x.x.xx", "x.....x.x.x...x.", "xxx.xxx.xxx.xxx.", "x.x.x.xx.x.x.x.x", "x..xx..xx..xx..x",
        };

        static readonly string[] StabRhythms =
        {
            "x..x..x...x.x...", "x...x...x..x..x.", "..x...x...x...x.", "x.x...x.x.x...x.", "x..x....x..x..x.",
        };

        static readonly string[] LongRhythms =
        {
            "x...............", "x.......x.......", "x.....x.x.......", "x.......x.....x.", "x.......x.x.....",
        };

        static readonly int[][] ArpOrders =
        {
            new[] { 0, 1, 2, 3 }, new[] { 0, 1, 2, 3, 2, 1 }, new[] { 0, 2, 1, 3 }, new[] { 0, 3, 1, 3, 2, 3, 1, 3 },
        };

        // ------------------------------------------------------------------------------------------------ state

        readonly TrackSpec t;
        readonly SynthRandom noise;        // drum noise
        readonly SynthRandom motif;        // melodies and rhythms of this track (separate so tuning one never changes the other)
        readonly DrumPattern pat;
        readonly KitSound kit;
        readonly BassVoice bassVoice;
        readonly int total;                // samples in the loop
        readonly double spStep;            // samples per sixteenth
        readonly float[] drums, bass, lead, pad, duck;
        readonly List<int> kicks = new List<int>();
        readonly int leadTonic;            // the tonic in the lead's octave (MIDI 57-68)

        public IntenseRenderer(TrackSpec spec)
        {
            t = spec;
            noise = new SynthRandom(spec.Seed * 3 + 1);
            motif = new SynthRandom(spec.Seed * 5 + 2);
            pat = Patterns[(int)spec.Drums];
            kit = Kits[(int)spec.Drums];
            bassVoice = BassVoices[(int)spec.Bass];
            spStep = SR * 60.0 / spec.Bpm / 4.0;
            total = (int)Math.Round(TrackSpec.Bars * 16 * spStep);
            drums = new float[total]; bass = new float[total]; lead = new float[total]; pad = new float[total]; duck = new float[total];
            leadTonic = Scales.TonicNear(spec.RootMidi, 63);
        }

        public float[] Render()
        {
            PlayDrums();
            PlayBass();
            PlayLead();
            PlayPad();
            BuildDuck();
            ProceduralMusic.LoopingEcho(lead, (int)Math.Round(spStep * 3), 0.38f);   // dotted-eighth echo on the lead
            return Mix();
        }

        // ------------------------------------------------------------------------------------------------ timing helpers

        int StepPos(int step) => (int)(step * spStep + (((step & 1) == 1) ? t.Swing * spStep : 0.0));
        int Steps(double n) => Math.Max(1, (int)(n * spStep));
        static float Vel(char c) => c == 'X' ? 1f : (c == 'g' ? 0.35f : (c == 'o' ? 0.8f : 0.85f));

        /// <summary>Adds into a stem; an index past the end wraps to the start of the loop.</summary>
        void Add(float[] buf, int i, float v) { if (i >= total) i -= total; buf[i] += v; }

        // ------------------------------------------------------------------------------------------------ drums

        void PlayDrums()
        {
            for (int bar = 0; bar < TrackSpec.Bars; bar++)
            {
                bool variation = (bar & 3) == 3;
                bool fillBar = (bar % 8) == 7;
                string kickPattern = variation ? pat.KickAlt : pat.Kick;
                for (int step = 0; step < 16; step++)
                {
                    if (fillBar && step >= 12) continue;      // the fill replaces the last beat
                    int at = StepPos(bar * 16 + step);
                    if (kickPattern[step] != '.') Kick(at, Vel(kickPattern[step]));
                    char s = pat.Snare[step];
                    if (s != '.') Backbeat(at, Vel(s));
                    char h = pat.Hat[step];
                    if (h == 'o') Hat(at, 0.8f, true);
                    else if (h != '.') Hat(at, Vel(h), false);
                }
                if (bar % 8 == 0) Crash(StepPos(bar * 16), 0.7f);
                if (fillBar) Fill(bar);
            }
        }

        void Kick(int at, float vel)
        {
            kicks.Add(at);
            float ampDecay = Dsp.DecayPerSample(kit.KickTau), sweepDecay = Dsp.DecayPerSample(kit.KickSweep);
            int len = Math.Min(Dsp.Samples(0.55f), Dsp.Samples(kit.KickTau * 5.5f));
            float amp = 1f, sweep = 1f, ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float hz = kit.KickHz1 + (kit.KickHz0 - kit.KickHz1) * sweep;
                ph += hz / SR; if (ph >= 1f) ph -= 1f;
                float s = Dsp.Sine(ph);
                if (kit.KickDrive > 1f) s = MathF.Tanh(s * kit.KickDrive);
                Add(drums, at + i, s * amp * Math.Min(1f, i / 24f) * vel * kit.KickGain);
                amp *= ampDecay; sweep *= sweepDecay;
            }
            for (int i = 0; i < 40; i++) Add(drums, at + i, noise.Signed() * (1f - i / 40f) * 0.25f * vel);   // beater click
        }

        /// <summary>The backbeat: a snare, a clap, or a mix of both depending on the kit.</summary>
        void Backbeat(int at, float vel)
        {
            if (kit.ClapMix < 1f) Snare(at, vel * (1f - 0.5f * kit.ClapMix));
            if (kit.ClapMix > 0f) Clap(at, vel * kit.ClapMix);
        }

        void Snare(int at, float vel)
        {
            int len = Dsp.Samples(kit.SnareTau * 5f);
            var hp = new Svf(); hp.Set(1400f, 0.7f);
            float noiseDecay = Dsp.DecayPerSample(kit.SnareTau), toneDecay = Dsp.DecayPerSample(0.055f);
            float noiseEnv = 1f, toneEnv = 1f, ph = 0f;
            for (int i = 0; i < len; i++)
            {
                ph += kit.SnareTone * (1f + 0.5f * toneEnv) / SR; if (ph >= 1f) ph -= 1f;
                float n = hp.High(noise.Signed()) * noiseEnv * kit.SnareNoise;
                float body = Dsp.Sine(ph) * toneEnv * (1f - 0.5f * kit.SnareNoise);
                Add(drums, at + i, (n + body) * vel * kit.SnareGain * Math.Min(1f, i / 12f));
                noiseEnv *= noiseDecay; toneEnv *= toneDecay;
            }
        }

        void Clap(int at, float vel)
        {
            var bp = new Svf(); bp.Set(1300f, 0.9f);
            int len = Dsp.Samples(0.26f);
            int[] burstStart = { 0, Dsp.Samples(0.011f), Dsp.Samples(0.022f) };
            int tailStart = Dsp.Samples(0.033f);
            float burstDecay = Dsp.DecayPerSample(0.004f), tailDecay = Dsp.DecayPerSample(0.07f);
            for (int i = 0; i < len; i++)
            {
                float env = 0f;
                for (int b = 0; b < burstStart.Length; b++)
                    if (i >= burstStart[b]) env = Math.Max(env, MathF.Pow(burstDecay, i - burstStart[b]));
                if (i >= tailStart) env = Math.Max(env, 0.8f * MathF.Pow(tailDecay, i - tailStart));
                Add(drums, at + i, bp.Band(noise.Signed()) * env * vel * kit.SnareGain * 0.7f);
            }
        }

        void Hat(int at, float vel, bool open)
        {
            int len = Dsp.Samples(open ? 0.30f : 0.07f);
            float decay = Dsp.DecayPerSample(open ? 0.07f : 0.012f), env = 1f;
            var hp = new Svf(); hp.Set(kit.HatCut, 0.8f);
            for (int i = 0; i < len; i++)
            {
                Add(drums, at + i, hp.High(noise.Signed()) * env * vel * kit.HatGain);
                env *= decay;
            }
        }

        void Crash(int at, float vel)
        {
            int len = Dsp.Samples(1.5f);
            var hp = new Svf(); hp.Set(3200f, 0.7f);
            float decay = Dsp.DecayPerSample(0.5f), env = 1f;
            for (int i = 0; i < len; i++)
            {
                Add(drums, at + i, hp.High(noise.Signed()) * env * vel * 0.45f * Math.Min(1f, i / 16f));
                env *= decay;
            }
        }

        void Tom(int at, float vel, float hz)
        {
            float ampDecay = Dsp.DecayPerSample(0.13f), sweepDecay = Dsp.DecayPerSample(0.05f);
            int len = Dsp.Samples(0.5f);
            float amp = 1f, sweep = 1f, ph = 0f;
            for (int i = 0; i < len; i++)
            {
                ph += hz * (0.65f + 0.35f * sweep) / SR; if (ph >= 1f) ph -= 1f;
                Add(drums, at + i, Dsp.Sine(ph) * amp * vel * 0.8f * Math.Min(1f, i / 24f));
                amp *= ampDecay; sweep *= sweepDecay;
            }
        }

        /// <summary>The last beat of every eighth bar: a snare roll or a tom run, and a rising noise sweep over the last half bar.</summary>
        void Fill(int bar)
        {
            float[] tomHz = { 260f, 205f, 160f, 120f };
            float[] rollVel = { 0.45f, 0.6f, 0.8f, 1.0f };
            for (int k = 0; k < 4; k++)
            {
                int at = StepPos(bar * 16 + 12 + k);
                if (k == 0) Kick(at, 1f);
                if (kit.FillToms) Tom(at, 0.9f, tomHz[k]); else Snare(at, rollVel[k]);
            }
            // riser over steps 8-15
            int from = StepPos(bar * 16 + 8), len = StepPos(bar * 16 + 15) + Steps(1) - from;
            var bp = new Svf();
            for (int i = 0; i < len; i++)
            {
                float p = i / (float)len;
                if ((i & 7) == 0) bp.Set(400f * MathF.Pow(18f, p), 2.2f);
                Add(drums, from + i, bp.Band(noise.Signed()) * p * p * 0.45f);
            }
        }

        // ------------------------------------------------------------------------------------------------ bass

        /// <summary>The note the bass plays for a chord degree, kept within about an octave of the tonic.</summary>
        int BassRoot(int degree)
        {
            int m = Scales.Midi(t.Mode, t.RootMidi, degree);
            return m > t.RootMidi + 7 ? m - 12 : m;
        }

        void PlayBass()
        {
            const string gallop = "x.xx.xx.x.xx.xx.";
            int[] rollOctave = { 0, 0, 12, 0, 0, 12, 0, 0 };
            int[] syncSteps = { 0, 3, 6, 10, 12, 15 };
            for (int bar = 0; bar < TrackSpec.Bars; bar++)
            {
                int root = BassRoot(t.DegreeAt(bar));
                switch (t.Bass)
                {
                    case BassStyle.Sub:
                        BassNote(bar, 0, 10f, root, 1f);
                        BassNote(bar, 10, 6f, root + ((bar & 1) == 1 ? 7 : 0), 0.9f);
                        break;
                    case BassStyle.Pump:
                        for (int s = 0; s < 16; s += 2) BassNote(bar, s, 1.6f, root + (s % 8 == 6 ? 12 : 0), s % 4 == 0 ? 1f : 0.8f);
                        break;
                    case BassStyle.Gallop:
                        for (int s = 0; s < 16; s++) if (gallop[s] == 'x') BassNote(bar, s, 0.7f, root, s % 4 == 0 ? 1f : 0.85f);
                        break;
                    case BassStyle.Offbeat:
                        BassNote(bar, 0, 1.2f, root, 0.9f);
                        for (int s = 2; s < 16; s += 4) BassNote(bar, s, 1.8f, root + (s == 14 && (bar & 1) == 1 ? 12 : 0), 1f);
                        break;
                    case BassStyle.Roll:
                        for (int s = 0; s < 16; s++) BassNote(bar, s, 0.85f, root + rollOctave[s % 8], s % 4 == 0 ? 1f : 0.8f);
                        break;
                    case BassStyle.Wobble:
                        BassNote(bar, 0, 6f, root, 1f);
                        BassNote(bar, 6, 2f, root, 0.9f);
                        BassNote(bar, 8, 8f, root + ((bar & 3) == 3 ? 7 : 0), 1f);
                        break;
                    case BassStyle.Syncopated:
                        for (int k = 0; k < syncSteps.Length; k++)
                        {
                            int s = syncSteps[k];
                            int next = k + 1 < syncSteps.Length ? syncSteps[k + 1] : 16;
                            BassNote(bar, s, (next - s) * 0.8f, root + ((s == 6 || s == 12) ? 7 : 0), s % 4 == 0 ? 1f : 0.85f);
                        }
                        break;
                }
            }
        }

        void BassNote(int bar, int step, float lengthSteps, int midi, float vel)
        {
            int at = StepPos(bar * 16 + step);
            int len = Steps(lengthSteps);
            int release = Dsp.Samples(0.025f);
            var v = bassVoice;
            float hz = Dsp.MidiToHz(midi), dt = hz / SR;
            float ph = 0f, filterEnv = 1f, filterDecay = Dsp.DecayPerSample(v.FilterTau);
            var lp = new Svf();
            double lfoRate = v.LfoPerBeat * t.Bpm / 60.0 / SR;       // cycles per sample, locked to the tempo so the wobble repeats with the loop
            for (int i = 0; i < len + release; i++)
            {
                if ((i & 7) == 0)
                {
                    float cut;
                    if (v.LfoPerBeat > 0f)
                    {
                        double x = (at + i) * lfoRate;
                        cut = v.CutBase + (v.CutPeak - v.CutBase) * (0.5f - 0.5f * MathF.Cos(Dsp.TwoPi * (float)(x - Math.Floor(x))));
                    }
                    else cut = v.CutBase + (v.CutPeak - v.CutBase) * filterEnv;
                    lp.Set(cut, v.Q);
                }
                filterEnv *= filterDecay;
                ph += dt; if (ph >= 1f) ph -= 1f;
                float s = v.Saw * lp.Low(Dsp.Saw(ph, dt)) + v.Sine * Dsp.Sine(ph);
                s = MathF.Tanh(s * v.Drive);
                float amp = i < len ? Math.Min(1f, i / 48f) : Math.Max(0f, 1f - (i - len) / (float)release);
                Add(bass, at + i, s * amp * vel * v.Gain);
            }
        }

        // ------------------------------------------------------------------------------------------------ lead

        void PlayLead()
        {
            // Each 8-bar section has its own riff/rhythm, derived from the track's seed.
            var motifs = new Motif[2];
            for (int section = 0; section < 2; section++) motifs[section] = new Motif(motif);

            for (int bar = 0; bar < TrackSpec.Bars; bar++)
            {
                int section = (bar / 8) & 1;
                var style = t.LeadAt(bar);
                int degree = t.DegreeAt(bar);
                switch (style)
                {
                    case LeadStyle.Arp: ArpBar(bar, degree, motifs[section]); break;
                    case LeadStyle.Riff: RiffBar(bar, degree, motifs[section], section == 1); break;
                    case LeadStyle.Stab: StabBar(bar, degree, motifs[section]); break;
                    case LeadStyle.Long: LongBar(bar, degree, motifs[section]); break;
                    case LeadStyle.Ostinato: OstinatoBar(bar, degree, motifs[section]); break;
                    case LeadStyle.Siren: if (bar % 8 == 0) Siren(bar, 8); break;
                }
            }
        }

        /// <summary>The random choices that give a section its own tune: rhythm picks and note offsets, drawn once per section.</summary>
        sealed class Motif
        {
            public readonly string Riff, Stab, Long;
            public readonly int[] RiffOffsets = new int[16];
            public readonly int[] ArpOrder;
            public readonly bool Gallop;
            public readonly int[] LongOffsets = new int[16];

            public Motif(SynthRandom r)
            {
                Riff = RiffRhythms[r.Int(RiffRhythms.Length)];
                Stab = StabRhythms[r.Int(StabRhythms.Length)];
                Long = LongRhythms[r.Int(LongRhythms.Length)];
                ArpOrder = ArpOrders[r.Int(ArpOrders.Length)];
                Gallop = r.Chance(0.5f);
                int[] chordTones = { 0, 0, 2, 4, 7 };
                int cur = 0;
                for (int s = 0; s < 16; s++)
                {
                    if (Riff[s] == '.') continue;
                    if (s % 4 == 0) cur = chordTones[r.Int(chordTones.Length)];
                    else cur = Math.Max(-2, Math.Min(9, cur + r.Int(5) - 2));
                    RiffOffsets[s] = cur;
                }
                int walk = 2;
                for (int s = 0; s < 16; s++)
                {
                    if (Long[s] == '.') continue;
                    walk = Math.Max(0, Math.Min(7, walk + r.Int(5) - 2));
                    LongOffsets[s] = walk;
                }
            }
        }

        int LeadMidi(int degree, int offset) => Scales.Midi(t.Mode, leadTonic, degree + offset);

        void ArpBar(int bar, int degree, Motif m)
        {
            int[] tones = { 0, 2, 4, 7 };
            for (int s = 0; s < 16; s++)
            {
                int tone = tones[m.ArpOrder[s % m.ArpOrder.Length]];
                float vel = s % 4 == 0 ? 1f : (s % 2 == 0 ? 0.8f : 0.65f);
                LeadNote(LeadMidi(degree, tone), StepPos(bar * 16 + s), Steps(0.8), VoiceArp, vel);
            }
        }

        void RiffBar(int bar, int degree, Motif m, bool octaveUp)
        {
            string rhythm = m.Riff;
            for (int s = 0; s < 16; s++)
            {
                if (rhythm[s] != 'x') continue;
                int next = s + 1;
                while (next < 16 && rhythm[next] == '.') next++;
                int gap = Math.Min(next - s, 2);               // a note rings for up to two steps, shorter if the next one comes first
                int midi = LeadMidi(degree, m.RiffOffsets[s]) + (octaveUp ? 12 : 0);
                LeadNote(midi, StepPos(bar * 16 + s), Steps(gap * 0.9), VoiceRiff, s % 4 == 0 ? 1f : 0.85f);
            }
        }

        void StabBar(int bar, int degree, Motif m)
        {
            int[] tones = { 0, 2, 4 };
            for (int s = 0; s < 16; s++)
            {
                if (m.Stab[s] != 'x') continue;
                for (int k = 0; k < tones.Length; k++)
                    LeadNote(LeadMidi(degree, tones[k] + 7), StepPos(bar * 16 + s), Steps(1.8), VoiceStab, s % 4 == 0 ? 1f : 0.85f);
            }
        }

        void LongBar(int bar, int degree, Motif m)
        {
            for (int s = 0; s < 16; s++)
            {
                if (m.Long[s] != 'x') continue;
                int next = s + 1;
                while (next < 16 && m.Long[next] == '.') next++;
                int steps = next - s;
                LeadNote(LeadMidi(degree, m.LongOffsets[s] + 7), StepPos(bar * 16 + s), Steps(steps - 0.5), VoiceLong, 1f);
            }
        }

        void OstinatoBar(int bar, int degree, Motif m)
        {
            int tonic = Scales.TonicNear(t.RootMidi, 52);
            for (int s = 0; s < 16; s++)
            {
                bool hit = m.Gallop ? "x.xxx.xxx.xxx.xx"[s] == 'x' : true;
                if (!hit) continue;
                int offset = (s % 8 == 6) ? 7 : 0;              // an octave jump now and then keeps it moving
                float vel = s % 4 == 0 ? 1f : 0.7f;
                LeadNote(Scales.Midi(t.Mode, tonic, degree + offset), StepPos(bar * 16 + s), Steps(0.7), VoiceOstinato, vel);
            }
        }

        /// <summary>A police-style two-tone siren: the tonic and the note a fourth above, swapping every beat with a quick glide.</summary>
        void Siren(int startBar, int bars)
        {
            int from = StepPos(startBar * 16);
            int to = Math.Min(total, StepPos((startBar + bars) * 16));
            int lowNote = Scales.TonicNear(t.RootMidi, 76);
            float lowHz = Dsp.MidiToHz(lowNote), highHz = Dsp.MidiToHz(Scales.Midi(t.Mode, lowNote, 3));
            int beat = Math.Max(1, (int)(spStep * 4));
            int fade = Dsp.Samples(0.015f);
            float hz = lowHz, ph = 0f;
            var lp = new Svf();
            for (int i = from; i < to; i++)
            {
                float target = (((i - from) / beat) & 1) == 0 ? lowHz : highHz;
                hz += (target - hz) * 0.0035f;
                if ((i & 15) == 0) lp.Set(2800f, 0.8f);
                ph += hz / SR; if (ph >= 1f) ph -= 1f;
                float s = lp.Low(0.6f * Dsp.Saw(ph, hz / SR) + 0.5f * Dsp.Sine(ph));
                float amp = Math.Min(1f, Math.Min(i - from, to - 1 - i) / (float)fade);
                lead[i] += s * amp * 0.55f;
            }
        }

        /// <summary>One synth note: one or two oscillators through a low-pass filter whose cutoff falls from CutPeak to CutBase.</summary>
        void LeadNote(int midi, int at, int len, LeadVoice v, float vel)
        {
            float hz = Dsp.MidiToHz(midi), dt = hz / SR;
            float detuneRatio = MathF.Pow(2f, v.Detune / 1200f);
            int attack = Math.Max(1, Dsp.Samples(v.Attack)), release = Math.Max(1, Dsp.Samples(v.Release));
            float ph = 0f, ph2 = 0.37f, filterEnv = 1f, filterDecay = Dsp.DecayPerSample(v.FilterTau);
            float rate = 1f;
            var lp = new Svf();
            int delayedVibrato = Dsp.Samples(0.2f);
            for (int i = 0; i < len + release; i++)
            {
                if ((i & 7) == 0)
                {
                    lp.Set(v.CutBase + (v.CutPeak - v.CutBase) * filterEnv, v.Q);
                    if (v.Vibrato > 0f)
                        rate = 1f + 0.0578f * v.Vibrato * Math.Min(1f, i / (float)delayedVibrato) * MathF.Sin(Dsp.TwoPi * 5.5f * i / SR);
                }
                filterEnv *= filterDecay;
                float s;
                switch (v.Wave)
                {
                    case Wave.Pulse:
                    {
                        float duty = 0.5f - 0.2f * (0.5f + 0.5f * MathF.Sin(Dsp.TwoPi * 2.5f * (at + i) / SR));
                        s = 0.6f * Dsp.Pulse(ph, dt * rate, duty) + 0.4f * Dsp.Pulse(ph2, dt * rate * detuneRatio, 0.5f);
                        break;
                    }
                    case Wave.Square:
                        s = Dsp.Pulse(ph, dt * rate, 0.5f);
                        break;
                    default:
                        s = 0.5f * (Dsp.Saw(ph, dt * rate) + Dsp.Saw(ph2, dt * rate * detuneRatio));
                        break;
                }
                ph += dt * rate; if (ph >= 1f) ph -= 1f;
                ph2 += dt * rate * detuneRatio; if (ph2 >= 1f) ph2 -= 1f;
                float amp = i < len ? Math.Min(1f, i / (float)attack) : Math.Max(0f, 1f - (i - len) / (float)release);
                Add(lead, at + i, lp.Low(s) * amp * vel * v.Gain);
            }
        }

        // ------------------------------------------------------------------------------------------------ pad

        /// <summary>A soft wall of detuned saws on the chord. Chords that repeat in following bars are held as one note.</summary>
        void PlayPad()
        {
            int padTonic = Scales.TonicNear(t.RootMidi, 54);
            int bar = 0;
            while (bar < TrackSpec.Bars)
            {
                int degree = t.DegreeAt(bar), run = 1;
                while (bar + run < TrackSpec.Bars && ((bar + run) % 8) != 0 && t.DegreeAt(bar + run) == degree) run++;
                bool sectionB = (bar / 8 & 1) == 1;
                PadChord(padTonic, degree, StepPos(bar * 16), Steps(run * 16), sectionB ? 1.0f : 0.7f, sectionB ? 1700f : 1000f);
                bar += run;
            }
        }

        void PadChord(int tonic, int degree, int at, int len, float gain, float cutoff)
        {
            int[] tones = { 0, 2, 4 };
            float[] detune = { 0.9948f, 1.0f, 1.0052f };                 // about -9 / 0 / +9 cents
            var phase = new float[tones.Length * detune.Length];
            var dts = new float[phase.Length];
            for (int k = 0; k < tones.Length; k++)
                for (int d = 0; d < detune.Length; d++)
                {
                    int idx = k * detune.Length + d;
                    dts[idx] = Dsp.MidiToHz(Scales.Midi(t.Mode, tonic, degree + tones[k])) * detune[d] / SR;
                    phase[idx] = (idx * 0.2718f) % 1f;
                }
            int attack = Dsp.Samples(0.08f), release = Dsp.Samples(0.12f);
            var lp = new Svf();
            for (int i = 0; i < len + release; i++)
            {
                if ((i & 15) == 0) lp.Set(cutoff, 0.8f);
                float s = 0f;
                for (int k = 0; k < phase.Length; k++)
                {
                    s += Dsp.Saw(phase[k], dts[k]);
                    phase[k] += dts[k]; if (phase[k] >= 1f) phase[k] -= 1f;
                }
                float amp = i < len ? Math.Min(1f, i / (float)attack) : Math.Max(0f, 1f - (i - len) / (float)release);
                Add(pad, at + i, lp.Low(s) * (1f / phase.Length) * amp * gain);
            }
        }

        // ------------------------------------------------------------------------------------------------ mix

        /// <summary>Sidechain: a curve that jumps to 1 at every kick and falls back to 0 over a fraction of a beat.</summary>
        void BuildDuck()
        {
            int len = Dsp.Samples(0.20f);
            float decay = Dsp.DecayPerSample(0.06f);
            foreach (int at in kicks)
            {
                float a = 1f;
                for (int i = 0; i < len; i++)
                {
                    int idx = at + i; if (idx >= total) idx -= total;
                    if (a > duck[idx]) duck[idx] = a;
                    a *= decay;
                }
            }
        }

        float[] Mix()
        {
            var o = new float[total];
            float d = kit.Duck;
            for (int i = 0; i < total; i++)
            {
                float a = duck[i];
                o[i] = drums[i] * 1.0f
                     + bass[i] * (1f - 0.8f * d * a) * 0.85f
                     + pad[i] * (1f - 1.0f * d * a) * 0.45f
                     + lead[i] * (1f - 0.5f * d * a) * 0.6f;
            }
            ProceduralMusic.Master(o, 0.30f, 0.92f);
            return o;
        }
    }
}
