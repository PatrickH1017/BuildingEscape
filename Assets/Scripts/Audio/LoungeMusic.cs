using System;

namespace BuildingEscape
{
    /// <summary>
    /// The "elevator music" placeholder: a slow, swung jazz-lounge loop (electric piano chords, walking bass, vibraphone tune,
    /// brushed drums), 8 bars at 92 bpm. Plain C#, called through <see cref="ProceduralMusic.RenderLounge"/>.
    /// </summary>
    sealed class LoungeRenderer
    {
        const int SR = Dsp.SampleRate;
        const double Bpm = 92.0;
        const int Bars = 8;
        const double SwingBeat = 1.0 / 6.0;     // triplet swing: the off-beat eighth lands two thirds of the way through the beat

        /// <summary>One chord of the progression: where it starts (in beats), how long it lasts, the bass note, the piano voicing,
        /// the note the walking bass steps to second, and the notes the vibraphone tune may use.</summary>
        sealed class Chord
        {
            public readonly double Start, Beats;
            public readonly int Bass, Third;
            public readonly int[] Keys, Pool;
            public Chord(double start, double beats, int bass, int third, int[] keys, int[] pool)
            { Start = start; Beats = beats; Bass = bass; Third = third; Keys = keys; Pool = pool; }
        }

        // Dm9 | G13 | Cmaj9 | Am7 | Dm9 | G13 | Em7 A7 | Cmaj9   (a ii-V-I turnaround in C, the standard lift-music progression)
        static readonly Chord[] Chords =
        {
            new Chord( 0, 4, 38, 3, new[] { 53, 57, 60, 64 }, new[] { 74, 76, 77, 81, 84 }),
            new Chord( 4, 4, 43, 4, new[] { 53, 59, 62, 64 }, new[] { 74, 76, 77, 79, 83 }),
            new Chord( 8, 4, 36, 4, new[] { 55, 59, 62, 64 }, new[] { 72, 74, 76, 79, 83 }),
            new Chord(12, 4, 45, 3, new[] { 55, 60, 64, 67 }, new[] { 72, 76, 79, 81 }),
            new Chord(16, 4, 38, 3, new[] { 53, 57, 60, 64 }, new[] { 74, 76, 77, 81, 84 }),
            new Chord(20, 4, 43, 4, new[] { 53, 59, 62, 64 }, new[] { 74, 76, 77, 79, 83 }),
            new Chord(24, 2, 40, 3, new[] { 55, 59, 62, 64 }, new[] { 71, 74, 76, 79 }),
            new Chord(26, 2, 45, 4, new[] { 55, 61, 64, 67 }, new[] { 73, 76, 79, 81 }),
            new Chord(28, 4, 36, 4, new[] { 55, 59, 62, 64 }, new[] { 72, 74, 76, 79, 83 }),
        };

        static readonly string[] TuneRhythms = { "x.x.x...", "x...x.x.", ".x.x..x.", "x.....x.", "x.xx....", "..x.x.x." };

        readonly int total;
        readonly double spBeat;
        readonly SynthRandom noise = new SynthRandom(901), tune = new SynthRandom(902);
        readonly float[] keys, bass, vibes, drums;

        public LoungeRenderer()
        {
            spBeat = SR * 60.0 / Bpm;
            total = (int)Math.Round(Bars * 4 * spBeat);
            keys = new float[total]; bass = new float[total]; vibes = new float[total]; drums = new float[total];
        }

        public float[] Render()
        {
            PlayKeys();
            PlayBass();
            PlayTune();
            PlayDrums();
            ProceduralMusic.LoopingEcho(vibes, (int)Math.Round(spBeat * 0.75), 0.30f);   // dotted-eighth echo on the vibes
            var o = new float[total];
            for (int i = 0; i < total; i++) o[i] = keys[i] * 0.55f + bass[i] * 0.85f + vibes[i] * 0.5f + drums[i] * 0.5f;
            ProceduralMusic.Master(o, 0.14f, 0.6f);
            return o;
        }

        // ---- timing: positions are in beats from the start of the loop; off-beat eighths are swung
        int Pos(double beats) => (int)Math.Round(beats * spBeat);
        static double Swung(double beats)
        {
            double frac = beats - Math.Floor(beats);
            return Math.Abs(frac - 0.5) < 1e-6 ? beats + SwingBeat : beats;
        }

        void Add(float[] buf, int i, float v) { if (i >= total) i -= total; buf[i] += v; }

        // ------------------------------------------------------------------------------------------------ piano

        void PlayKeys()
        {
            foreach (var c in Chords)
            {
                if (c.Beats >= 4)
                {
                    Comp(c, c.Start, 1.5, 1.0f);
                    Comp(c, Swung(c.Start + 2.5), 1.0, 0.6f);
                }
                else Comp(c, c.Start, c.Beats * 0.9, 1.0f);
            }
        }

        /// <summary>A chord hit: the notes are rolled slightly, bottom to top, the way a player's hand lands.</summary>
        void Comp(Chord c, double startBeat, double lengthBeats, float vel)
        {
            for (int k = 0; k < c.Keys.Length; k++)
                EPiano(Pos(startBeat) + k * Dsp.Samples(0.008f), Pos(lengthBeats), c.Keys[k], vel * (0.9f - 0.05f * k));
        }

        /// <summary>FM electric piano: a sine carrier modulated by a sine, bright on the attack and mellowing as it rings.</summary>
        void EPiano(int at, int len, int midi, float vel)
        {
            float hz = Dsp.MidiToHz(midi), dt = hz / SR;
            int release = Dsp.Samples(0.14f);
            float ampDecay = Dsp.DecayPerSample(1.5f - 0.015f * (midi - 48)), indexDecay = Dsp.DecayPerSample(0.28f), barkDecay = Dsp.DecayPerSample(0.12f);
            float amp = 1f, index = 1.9f, bark = 1f, ph = 0f, pm = 0f;
            for (int i = 0; i < len + release; i++)
            {
                float s = Dsp.Sine(ph + 0.55f * index * Dsp.Sine(pm) * 0.5f) + 0.12f * bark * Dsp.Sine(2f * ph);
                float tremolo = 1f - 0.08f + 0.08f * MathF.Sin(Dsp.TwoPi * 4.2f * (at + i) / SR);
                float gate = i < len ? Math.Min(1f, i / 96f) : Math.Max(0f, 1f - (i - len) / (float)release);
                Add(keys, at + i, s * amp * tremolo * gate * vel * 0.22f);
                ph += dt; if (ph >= 1f) ph -= 1f;
                pm += dt; if (pm >= 1f) pm -= 1f;
                amp *= ampDecay; index = 0.25f + (index - 0.25f) * indexDecay; bark *= barkDecay;
            }
        }

        // ------------------------------------------------------------------------------------------------ walking bass

        void PlayBass()
        {
            for (int k = 0; k < Chords.Length; k++)
            {
                var c = Chords[k];
                var next = Chords[(k + 1) % Chords.Length];
                int approach = next.Bass > c.Bass ? next.Bass - 1 : next.Bass + 1;    // a semitone below or above the next root
                if (c.Beats >= 4)
                {
                    BassNote(c.Start, c.Bass, 1f);
                    BassNote(c.Start + 1, c.Bass + c.Third, 0.8f);
                    BassNote(c.Start + 2, c.Bass + 7, 0.85f);
                    BassNote(c.Start + 3, approach, 0.8f);
                }
                else
                {
                    BassNote(c.Start, c.Bass, 1f);
                    BassNote(c.Start + 1, approach, 0.8f);
                }
            }
        }

        void BassNote(double beat, int midi, float vel)
        {
            int at = Pos(beat), len = Pos(0.92);
            float hz = Dsp.MidiToHz(midi), dt = hz / SR;
            int release = Dsp.Samples(0.05f);
            float decay = Dsp.DecayPerSample(0.55f), amp = 1f, ph = 0f;
            var lp = new Svf();
            for (int i = 0; i < len + release; i++)
            {
                if ((i & 7) == 0) lp.Set(260f + 700f * amp, 0.8f);
                float s = 0.85f * Dsp.Sine(ph) + 0.35f * lp.Low(Dsp.Triangle(ph));
                float gate = i < len ? Math.Min(1f, i / 80f) : Math.Max(0f, 1f - (i - len) / (float)release);
                Add(bass, at + i, s * amp * gate * vel * 0.8f);
                ph += dt; if (ph >= 1f) ph -= 1f;
                amp *= decay;
            }
        }

        // ------------------------------------------------------------------------------------------------ vibraphone tune

        void PlayTune()
        {
            int poolIndex = 2;
            for (int k = 0; k < Chords.Length; k++)
            {
                var c = Chords[k];
                bool last = k == Chords.Length - 1;
                if (last)
                {
                    // land on the home note and let it ring out
                    VibeNote(c.Start, 3.5, c.Pool[0], 1f);
                    continue;
                }
                string rhythm = TuneRhythms[tune.Int(TuneRhythms.Length)];
                double span = Math.Min(4.0, c.Beats);
                for (int e = 0; e < 8; e++)
                {
                    double beat = e * 0.5;
                    if (rhythm[e] != 'x' || beat >= span) continue;
                    poolIndex = Math.Max(0, Math.Min(c.Pool.Length - 1, poolIndex + tune.Int(5) - 2));
                    VibeNote(Swung(c.Start + beat), Math.Min(1.0, span - beat), c.Pool[poolIndex], e % 2 == 0 ? 1f : 0.8f);
                }
            }
        }

        void VibeNote(double beat, double lengthBeats, int midi, float vel)
        {
            int at = Pos(beat), len = Pos(lengthBeats);
            float hz = Dsp.MidiToHz(midi), dt = hz / SR;
            int release = Dsp.Samples(0.35f);
            float decay = Dsp.DecayPerSample(1.1f), partialDecay = Dsp.DecayPerSample(0.25f), ringDecay = Dsp.DecayPerSample(0.08f);
            float amp = 1f, partial = 1f, ring = 1f, ph = 0f;
            for (int i = 0; i < len + release; i++)
            {
                float s = Dsp.Sine(ph) + 0.22f * partial * Dsp.Sine(4f * ph) + 0.08f * ring * Dsp.Sine(10f * ph);
                float motor = 0.78f + 0.22f * MathF.Sin(Dsp.TwoPi * 5f * (at + i) / SR);      // the vibraphone's spinning-fan tremolo
                float gate = i < len ? Math.Min(1f, i / 96f) : Math.Max(0f, 1f - (i - len) / (float)release);
                Add(vibes, at + i, s * amp * motor * gate * vel * 0.4f);
                ph += dt; if (ph >= 1f) ph -= 1f;
                amp *= decay; partial *= partialDecay; ring *= ringDecay;
            }
        }

        // ------------------------------------------------------------------------------------------------ brushes

        void PlayDrums()
        {
            double[] ride = { 0, 1, 1 + 2.0 / 3.0, 2, 3, 3 + 2.0 / 3.0 };    // "ding, ding-a, ding, ding-a"
            for (int bar = 0; bar < Bars; bar++)
            {
                double b0 = bar * 4;
                for (int k = 0; k < ride.Length; k++) Brush(Pos(b0 + ride[k]), (k == 2 || k == 5) ? 0.55f : 0.9f, false);
                Brush(Pos(b0 + 1), 0.8f, true);       // swish on 2 and 4
                Brush(Pos(b0 + 3), 0.8f, true);
                SoftKick(Pos(b0));
                SoftKick(Pos(b0 + 2));
                if ((bar & 1) == 1) Rim(Pos(b0 + 3 + 2.0 / 3.0));
            }
        }

        void Brush(int at, float vel, bool swish)
        {
            var bp = new Svf(); bp.Set(swish ? 3200f : 6200f, 0.9f);
            int len = Dsp.Samples(swish ? 0.20f : 0.09f);
            float decay = Dsp.DecayPerSample(swish ? 0.07f : 0.03f), env = 1f;
            for (int i = 0; i < len; i++)
            {
                float rise = swish ? Math.Min(1f, i / (float)Dsp.Samples(0.04f)) : 1f;     // the swish blooms, the ride ticks
                Add(drums, at + i, bp.Band(noise.Signed()) * env * rise * vel * (swish ? 0.30f : 0.32f));
                env *= decay;
            }
        }

        void SoftKick(int at)
        {
            float ampDecay = Dsp.DecayPerSample(0.09f), sweepDecay = Dsp.DecayPerSample(0.03f);
            int len = Dsp.Samples(0.3f);
            float amp = 1f, sweep = 1f, ph = 0f;
            for (int i = 0; i < len; i++)
            {
                ph += (62f + 50f * sweep) / SR; if (ph >= 1f) ph -= 1f;
                Add(drums, at + i, Dsp.Sine(ph) * amp * 0.28f * Math.Min(1f, i / 48f));
                amp *= ampDecay; sweep *= sweepDecay;
            }
        }

        void Rim(int at)
        {
            var bp = new Svf(); bp.Set(1800f, 2.5f);
            int len = Dsp.Samples(0.04f);
            float decay = Dsp.DecayPerSample(0.012f), env = 1f;
            for (int i = 0; i < len; i++)
            {
                Add(drums, at + i, bp.Band(noise.Signed()) * env * 0.35f);
                env *= decay;
            }
        }
    }
}
