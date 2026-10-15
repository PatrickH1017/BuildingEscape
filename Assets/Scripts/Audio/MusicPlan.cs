using System;

namespace BuildingEscape
{
    // The music is made from maths, so the game has a full soundtrack before any real music is bought or composed:
    // MusicPlan (this file) is the "score" - sixteen intense tracks described by a few numbers and styles - and
    // ProceduralMusic turns one of them into audio. Plain C# with no Unity types, so it runs on a worker thread and
    // in tests outside Unity. To change what a track sounds like, edit its line in MusicPlan.Tracks.

    /// <summary>The seven-note scale a track is written in.</summary>
    public enum Mode { Minor, Phrygian, Dorian, HarmonicMinor, PhrygianDominant, Locrian }

    /// <summary>Which drum pattern and drum sound a track uses.</summary>
    public enum DrumKit { FourFloor, Breakbeat, HalfTime, Gallop, DrumAndBass, Industrial, Trap, Punk }

    /// <summary>The bass line's rhythm and tone.</summary>
    public enum BassStyle { Sub, Pump, Gallop, Offbeat, Roll, Wobble, Syncopated }

    /// <summary>The main instrument line: arpeggio, riff, chord stabs, police-style two-tone siren, long cinematic notes, or a driving string ostinato.</summary>
    public enum LeadStyle { None, Arp, Riff, Stab, Siren, Long, Ostinato }

    /// <summary>
    /// One looping track: 16 bars of 4/4 in two 8-bar sections (A then B). Every bar has a chord given as a scale degree
    /// (0 = the tonic chord, 5 = the chord on the sixth note of the scale, ...), written as eight digits per section.
    /// </summary>
    public sealed class TrackSpec
    {
        public readonly string Name;
        public readonly int Seed;           // makes the riffs and arpeggio shapes of this track its own
        public readonly int Bpm;
        public readonly int RootMidi;       // tonic as a low MIDI note (28 = E1, 33 = A1, 38 = D2)
        public readonly Mode Mode;
        public readonly int[] ProgA, ProgB; // chord degree per bar for section A (bars 1-8) and section B (bars 9-16)
        public readonly DrumKit Drums;
        public readonly BassStyle Bass;
        public readonly LeadStyle LeadA, LeadB;
        public readonly float Swing;        // 0 = straight sixteenths, 0.1 = a light shuffle

        public const int Bars = 16;

        public TrackSpec(string name, int seed, int bpm, int rootMidi, Mode mode, string progA, string progB,
                         DrumKit drums, BassStyle bass, LeadStyle leadA, LeadStyle leadB, float swing = 0f)
        {
            Name = name; Seed = seed; Bpm = bpm; RootMidi = rootMidi; Mode = mode;
            ProgA = Degrees(progA); ProgB = Degrees(progB);
            Drums = drums; Bass = bass; LeadA = leadA; LeadB = leadB; Swing = swing;
        }

        static int[] Degrees(string digits)
        {
            if (digits == null || digits.Length != 8) throw new ArgumentException("a progression is eight digits, one chord per bar");
            var d = new int[8];
            for (int i = 0; i < 8; i++)
            {
                d[i] = digits[i] - '0';
                if (d[i] < 0 || d[i] > 6) throw new ArgumentException("chord degrees are 0-6");
            }
            return d;
        }

        /// <summary>The chord degree playing in the given bar (0-15).</summary>
        public int DegreeAt(int bar) => ((bar / 8) & 1) == 0 ? ProgA[bar % 8] : ProgB[bar % 8];

        public LeadStyle LeadAt(int bar) => ((bar / 8) & 1) == 0 ? LeadA : LeadB;

        /// <summary>Length of the loop in seconds.</summary>
        public float Seconds => Bars * 4 * 60f / Bpm;
    }

    /// <summary>The scales and the note arithmetic shared by the renderer and the tests.</summary>
    public static class Scales
    {
        static readonly int[][] Table =
        {
            new[] { 0, 2, 3, 5, 7, 8, 10 },   // Minor (natural minor / Aeolian)
            new[] { 0, 1, 3, 5, 7, 8, 10 },   // Phrygian
            new[] { 0, 2, 3, 5, 7, 9, 10 },   // Dorian
            new[] { 0, 2, 3, 5, 7, 8, 11 },   // Harmonic minor
            new[] { 0, 1, 4, 5, 7, 8, 10 },   // Phrygian dominant
            new[] { 0, 1, 3, 5, 6, 8, 10 },   // Locrian
        };

        /// <summary>MIDI note of a scale degree counted from the tonic; degrees past 6 (or below 0) wrap into the next (or previous) octave.</summary>
        public static int Midi(Mode mode, int tonicMidi, int degree)
        {
            var s = Table[(int)mode];
            int octave = FloorDiv(degree, 7);
            return tonicMidi + s[degree - octave * 7] + 12 * octave;
        }

        /// <summary>The tonic's pitch moved by octaves to the note in [center - 6, center + 6).</summary>
        public static int TonicNear(int tonicMidi, int center)
        {
            int low = center - 6;
            int pitchClass = ((tonicMidi % 12) + 12) % 12;
            return low + (((pitchClass - low) % 12) + 12) % 12;
        }

        public static int FloorDiv(int a, int b) { int q = a / b; return (a % b != 0 && ((a < 0) != (b < 0))) ? q - 1 : q; }
    }

    public static class MusicPlan
    {
        /// <summary>
        /// The sixteen intense tracks. Each combines a tempo, key, chord progression, drum kit, bass line and two lead
        /// lines (one per 8-bar section), so no two feel alike. Names are only for logs.
        /// </summary>
        public static readonly TrackSpec[] Tracks =
        {
            //          name                      seed  bpm  root  mode                       progA       progB       drums                 bass                    leadA              leadB
            new TrackSpec("Sirens At Midnight",     11, 150, 33, Mode.Minor,            "05260526", "03620362", DrumKit.FourFloor,    BassStyle.Pump,        LeadStyle.Siren,    LeadStyle.Arp),
            new TrackSpec("Red Handed",             12, 140, 28, Mode.Phrygian,         "01010106", "01061016", DrumKit.HalfTime,     BassStyle.Sub,         LeadStyle.Riff,     LeadStyle.Riff),
            new TrackSpec("Vault Run",              13, 174, 29, Mode.Minor,            "05050606", "03030662", DrumKit.DrumAndBass,  BassStyle.Wobble,      LeadStyle.Stab,     LeadStyle.Arp),
            new TrackSpec("Break Room Breakdown",   14, 132, 38, Mode.Dorian,           "03030630", "03630364", DrumKit.Breakbeat,    BassStyle.Syncopated,  LeadStyle.Riff,     LeadStyle.Stab, 0.10f),
            new TrackSpec("Alarm Bells",            15, 160, 37, Mode.Minor,            "00560056", "05620562", DrumKit.Gallop,       BassStyle.Gallop,      LeadStyle.Ostinato, LeadStyle.Siren),
            new TrackSpec("Overtime",               16, 138, 31, Mode.Minor,            "05620562", "03620345", DrumKit.Trap,         BassStyle.Sub,         LeadStyle.Long,     LeadStyle.Arp,  0.06f),
            new TrackSpec("Hostile Takeover",       17, 145, 35, Mode.Phrygian,         "00010006", "01016601", DrumKit.Industrial,   BassStyle.Roll,        LeadStyle.Stab,     LeadStyle.Ostinato),
            new TrackSpec("Paper Chase",            18, 156, 31, Mode.HarmonicMinor,    "05430543", "03450345", DrumKit.FourFloor,    BassStyle.Offbeat,     LeadStyle.Arp,      LeadStyle.Stab),
            new TrackSpec("Sub-Zero Security",      19, 170, 40, Mode.Locrian,          "00510051", "00630063", DrumKit.DrumAndBass,  BassStyle.Roll,        LeadStyle.Ostinato, LeadStyle.Long),
            new TrackSpec("Last Call",              20, 168, 38, Mode.Minor,            "06560656", "05630563", DrumKit.Punk,         BassStyle.Pump,        LeadStyle.Riff,     LeadStyle.Siren),
            new TrackSpec("Lockdown Protocol",      21, 152, 42, Mode.PhrygianDominant, "00110061", "01061106", DrumKit.Industrial,   BassStyle.Wobble,      LeadStyle.Stab,     LeadStyle.Arp),
            new TrackSpec("Pursuit",                22, 165, 33, Mode.HarmonicMinor,    "00500060", "05430654", DrumKit.Gallop,       BassStyle.Gallop,      LeadStyle.Riff,     LeadStyle.Ostinato),
            new TrackSpec("Cold Cuffs",             23, 136, 36, Mode.Minor,            "03630362", "05620350", DrumKit.HalfTime,     BassStyle.Sub,         LeadStyle.Long,     LeadStyle.Riff),
            new TrackSpec("Smash And Grab",         24, 148, 31, Mode.Minor,            "00650065", "06560345", DrumKit.Breakbeat,    BassStyle.Syncopated,  LeadStyle.Stab,     LeadStyle.Riff, 0.08f),
            new TrackSpec("Neon Getaway",           25, 158, 40, Mode.Minor,            "05260526", "05652652", DrumKit.FourFloor,    BassStyle.Roll,        LeadStyle.Arp,      LeadStyle.Stab),
            new TrackSpec("Dead Drop",              26, 143, 34, Mode.Dorian,           "00330063", "03450363", DrumKit.Trap,         BassStyle.Wobble,      LeadStyle.Riff,     LeadStyle.Long, 0.05f),
        };

        /// <summary>
        /// Which of <paramref name="count"/> tracks plays on the given floor of a run. The order is a shuffle that is different for every
        /// run seed, so within a stretch of <paramref name="count"/> floors no track repeats, the first track of the next stretch is never
        /// the last one of the previous (so no two floors in a row share a track), and the same seed gives every player the same track
        /// on the same floor. Floors count from 1.
        /// </summary>
        public static int IndexFor(int runSeed, int floor, int count)
        {
            if (count <= 1) return 0;
            int floorIndex = Math.Max(0, floor - 1);
            return Order(runSeed, floorIndex / count, count)[floorIndex % count];
        }

        /// <summary>The track played on the given floor, from the built-in sixteen.</summary>
        public static int IndexFor(int runSeed, int floor) => IndexFor(runSeed, floor, Tracks.Length);

        public static TrackSpec TrackFor(int runSeed, int floor) => Tracks[IndexFor(runSeed, floor)];

        /// <summary>The play order of one stretch of <paramref name="count"/> floors (0 = the first stretch).</summary>
        public static int[] Order(int runSeed, int stretch, int count)
        {
            var order = Shuffled(runSeed, stretch, count);
            // the stretch must not open with the track the previous stretch closed with
            if (stretch > 0 && count > 1)
            {
                int previousLast = Order(runSeed, stretch - 1, count)[count - 1];
                if (order[0] == previousLast) { int t = order[0]; order[0] = order[1]; order[1] = t; }
            }
            return order;
        }

        /// <summary>A shuffle of 0..count-1; the same (seed, stretch, count) always gives the same order.</summary>
        public static int[] Shuffled(int runSeed, int stretch, int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            var rnd = new SynthRandom(unchecked(runSeed * 7919 + stretch * 104729 + 13));
            for (int i = count - 1; i > 0; i--)
            {
                int j = rnd.Int(i + 1);
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }
            return order;
        }
    }
}
