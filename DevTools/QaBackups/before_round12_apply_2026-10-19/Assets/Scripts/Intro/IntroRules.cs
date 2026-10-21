using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>What the TV shows (the boss's office camera): the camera set-ups of the monologue. <see cref="None"/>: the broadcast is over, the screen is static.</summary>
    public enum IntroShot { Wide, Medium, MediumClose, Desk, Window, Low, None }

    /// <summary>Where the cutscene's camera is in the Loading Docks (the rigs are in <c>IntroCinematic</c>, measured from the TV on the wall).</summary>
    public enum DockShot
    {
        /// <summary>The whole docks from across the room: the workers in the corner, the TV on the wall.</summary>
        Establish,
        /// <summary>From behind the workers, over their shoulders, at the TV.</summary>
        Behind,
        /// <summary>Close on the TV: the boss fills the picture.</summary>
        TvClose,
        /// <summary>From beside the TV at the workers' faces, lit by its glow.</summary>
        Faces,
        /// <summary>The two who talk, side by side, turned to each other.</summary>
        TwoShot,
    }

    /// <summary>Who says a line: the boss (on the TV) or one of the workers (a player's character, or the coworker who stands with a lone player).</summary>
    public enum Speaker { None, Boss, Worker }

    /// <summary>One piece of the cinematic: what the TV shows, where the camera is, and what is said (null = nothing).</summary>
    public readonly struct IntroBeat
    {
        public readonly float start, end;
        public readonly IntroShot shot;
        public readonly DockShot dock;
        public readonly string line;
        public readonly Speaker speaker;
        public IntroBeat(float start, float end, IntroShot shot, DockShot dock, string line, Speaker speaker)
        { this.start = start; this.end = end; this.shot = shot; this.dock = dock; this.line = line; this.speaker = speaker; }
        public float Length => end - start;
    }

    /// <summary>
    /// The run-start cinematic ("the Intro"): the loading dock workers - the players' own characters - stand in a corner of the Loading Docks and watch the boss tell them on
    /// the wall-mounted TV that their bonuses are gone this year (a private jet is expensive); when the broadcast ends one of them says to another "Hey call that guy you
    /// were talking about." (the owner, 2026-10-17: "have the player characters be watching the boss's monologue on a tv mounted on the wall in the corner of the loading
    /// docks ... After watching the scene on the tv, one of the character's should say to another ..."). The owner's rules of 2026-10-16 night still stand: it plays every
    /// time a new run is started; HOLDING SPACE skips it; in multiplayer it is skipped, for everybody, as soon as at least 50 % of the party members have filled their bar (the vote is saved: they need not keep holding).
    /// Pure rules, no Unity types (they compile and test outside Unity): the vote, the hold meter, the timeline of beats, the typed-out text, the fades and who
    /// stands where and who says the last line.
    /// </summary>
    public static class IntroRules
    {
        /// <summary>How long Space has to be held before it counts as a vote to skip.</summary>
        public const float HoldSeconds = 1f;
        /// <summary>The host starts the Intro this long after every machine has its docks built, so nobody misses the first picture.</summary>
        public const float LeadIn = 2.5f;
        /// <summary>The length of the Intro: the monologue on the TV (27 s), a moment of static, the workers' line, and the way out.</summary>
        public const float Total = 35f;
        /// <summary>The monologue ends and the TV goes to static at this time.</summary>
        public const float ProgramEnd = 27f;
        public const float FadeIn = 1f, FadeOut = 1.2f;
        public const float CharsPerSecond = 30f;
        /// <summary>The private jet flies across the windows during the window shot (on the TV).</summary>
        public const float JetStart = 18.4f, JetSeconds = 4.6f;

        /// <summary>The timeline: ten beats, 35 s. The words are in <see cref="IntroContent"/>.</summary>
        public static readonly IntroBeat[] Beats =
        {
            new IntroBeat(0.0f, 2.0f, IntroShot.Wide, DockShot.Establish, null, Speaker.None),
            new IntroBeat(2.0f, 5.5f, IntroShot.Medium, DockShot.Behind, IntroContent.Line1, Speaker.Boss),
            new IntroBeat(5.5f, 10.5f, IntroShot.MediumClose, DockShot.TvClose, IntroContent.Line2, Speaker.Boss),
            new IntroBeat(10.5f, 15.0f, IntroShot.Desk, DockShot.Faces, IntroContent.Line3, Speaker.Boss),
            new IntroBeat(15.0f, 18.0f, IntroShot.Medium, DockShot.TvClose, IntroContent.Line4, Speaker.Boss),
            new IntroBeat(18.0f, 23.0f, IntroShot.Window, DockShot.TvClose, IntroContent.Line5, Speaker.Boss),
            new IntroBeat(23.0f, 27.0f, IntroShot.Low, DockShot.Faces, IntroContent.Line6, Speaker.Boss),
            new IntroBeat(27.0f, 28.8f, IntroShot.None, DockShot.Behind, null, Speaker.None),
            new IntroBeat(28.8f, 33.4f, IntroShot.None, DockShot.TwoShot, IntroContent.FinalLine, Speaker.Worker),
            new IntroBeat(33.4f, 35.0f, IntroShot.None, DockShot.Behind, null, Speaker.None),
        };

        /// <summary>Is the vote to skip won? At least half of the party must be holding (1 of 1, 1 of 2, 2 of 3, 2 of 4, 3 of 5).</summary>
        public static bool SkipPassed(int holders, int party) => party > 0 && holders > 0 && holders * 2 >= party;

        /// <summary>How many holders it takes (what the skip readout counts up to).</summary>
        public static int NeededToSkip(int party) => party <= 1 ? 1 : (party + 1) / 2;

        /// <summary>The hold meter: fills in <see cref="HoldSeconds"/> while Space is held, empties twice as fast once it is let go.</summary>
        public static float Hold(float held, bool holding, float dt) => holding ? Math.Min(HoldSeconds, held + dt) : Math.Max(0f, held - dt * 2f);

        public static bool HoldDone(float held) => held >= HoldSeconds - 0.0001f;

        /// <summary>
        /// Is this player's vote to skip in? Once the bar has filled the vote is SAVED: letting go of Space does not take it back (the owner, 2026-10-18: "if the bar fills up for a player it saves their skip vote, not keep
        /// holding for it to count"). <paramref name="saved"/> is whether it already was.
        /// </summary>
        public static bool VoteSaved(bool saved, float held) => saved || HoldDone(held);

        /// <summary>The beat that is on at this time (seconds since the Intro began), or -1 before it and after it.</summary>
        public static int BeatAt(float t)
        {
            for (int i = 0; i < Beats.Length; i++) if (t >= Beats[i].start && t < Beats[i].end) return i;
            return -1;
        }

        /// <summary>How many letters of the line are typed <paramref name="into"/> seconds into its beat.</summary>
        public static int VisibleChars(string line, float into) =>
            string.IsNullOrEmpty(line) ? 0 : Math.Max(0, Math.Min(line.Length, (int)(into * CharsPerSecond)));

        /// <summary>0 = black, 1 = the picture: faded in at the start and out at the end.</summary>
        public static float Fade(float t) => Math.Max(0f, Math.Min(1f, Math.Min(t / FadeIn, (Total - t) / FadeOut)));

        /// <summary>How far through its beat the time is (0 to 1).</summary>
        public static float Progress(in IntroBeat beat, float t) => beat.Length <= 0f ? 1f : Math.Max(0f, Math.Min(1f, (t - beat.start) / beat.Length));

        public static bool Over(float t) => t >= Total;

        // ------------------------------------------------------------------ the cast

        /// <summary>
        /// Who says the last line, as an index into the cast: with a lone player the cast is the coworker (index 0) and the player's character, and the coworker speaks;
        /// otherwise it is one of the players' characters, picked from the run's seed (so every machine picks the same one).
        /// </summary>
        public static int SpeakerIndex(int cast, bool hasCoworker, int seed) =>
            cast <= 1 || hasCoworker ? 0 : (int)((((uint)seed * 2654435761u) >> 8) % (uint)cast);

        /// <summary>Who the speaker talks to: the next one in the cast (the player's character, when the speaker is the coworker).</summary>
        public static int ListenerIndex(int cast, int speaker) => cast <= 1 ? 0 : (speaker + 1) % cast;

        /// <summary>
        /// Where the cast stands, left to right in front of the TV: <c>result[position]</c> is the index into the cast. The speaker and the one they talk to stand next to each other
        /// (the listener to the left of the speaker) in the middle of the row, the others on either side in cast order.
        /// </summary>
        public static int[] RowOrder(int cast, int speaker, int listener)
        {
            var others = new List<int>();
            for (int i = 0; i < cast; i++) if (i != speaker && i != listener) others.Add(i);
            var row = new List<int>();
            int left = others.Count / 2;
            for (int i = 0; i < left; i++) row.Add(others[i]);
            if (cast >= 2) { row.Add(listener); row.Add(speaker); } else row.Add(speaker);
            for (int i = left; i < others.Count; i++) row.Add(others[i]);
            return row.ToArray();
        }

        /// <summary>
        /// How the workers look at this beat: 0 as they are (the boss is friendly: the first two lines), 1 crushed (he takes the bonuses; until the broadcast ends), 2 angry (the last
        /// beats: the line about calling somebody). Only the Party characters have faces to change.
        /// </summary>
        public static int FaceMood(int beat) => beat < 3 ? 0 : beat < 8 ? 1 : 2;

        /// <summary>How far apart (m) the workers stand side by side: 1.2 m for up to three, 1.0 m for four or five.</summary>
        public static float Spacing(int cast) => cast <= 3 ? 1.2f : 1.0f;
    }
}
