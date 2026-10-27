using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>What the TV shows (the boss's office camera): the camera set-ups of the monologue. <see cref="None"/>: the broadcast is over, the screen is static. <see cref="Off"/>: the TV is dark (the opening, before the announcement).</summary>
    public enum IntroShot { Wide, Medium, MediumClose, Desk, Window, Low, None, Off }

    /// <summary>Where the cutscene's camera is in the Loading Docks (the rigs are in <c>IntroCinematic</c>, measured from the TV on the wall).</summary>
    public enum DockShot
    {
        /// <summary>The break room from its north-west corner: the workers in a circle talking, the TV dark on the wall beyond them; the same shot goes on (one slow move) while the TV comes on and they walk to their places in the line in front of it (the owner, 2026-10-20).</summary>
        Establish,
        /// <summary>From behind the workers, over their shoulders, at the TV.</summary>
        Behind,
        /// <summary>Close on the TV: the boss fills the picture.</summary>
        TvClose,
        /// <summary>From beside the TV at the workers' faces, lit by its glow.</summary>
        Faces,
        /// <summary>The two who talk, side by side, turned to each other.</summary>
        TwoShot,
        /// <summary>The vendor's bay from inside the docks: after the workers' line its roller door rolls up on him (the owner, 2026-10-19 night: "the Vendor showing up after they say they are going to call"), then he says his four lines (2026-10-20).</summary>
        Bay,
    }

    /// <summary>Who says a line: the boss (on the TV), one of the workers (a player's character, or a coworker) or the vendor.</summary>
    public enum Speaker { None, Boss, Worker, Vendor }

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
    /// The run-start cinematic ("the Intro"): five loading dock workers - the players' own characters and coworkers - stand in a circle in the break room talking, with the wall-mounted TV dark; it comes on for the announcement, they look round at it, line up in front of it and watch the
    /// boss tell them that their bonuses are gone this year (a private jet is expensive); when the broadcast ends one of them says that it is time for revenge and to call the vendor, and the vendor's roller door rolls up on him: he tells
    /// them how the game works (the owner, 2026-10-17: "have the player characters be watching the boss's monologue on a tv mounted on the wall in the corner of the loading docks ..."; 2026-10-20: the five workers hanging out first, the new words
    /// of the worker and of the vendor). The owner's rules of 2026-10-16 night still stand: it plays every time a new run is started; HOLDING SPACE skips it; in multiplayer it is skipped, for everybody, as soon as at least 50 % of the party
    /// members have filled their bar (the vote is saved: they need not keep holding).
    /// Pure rules, no Unity types (they compile and test outside Unity): the vote, the hold meter, the timeline of beats, the typed-out text, the fades, how the workers look and who stands where and who says the worker's line.
    /// </summary>
    public static class IntroRules
    {
        /// <summary>How long Space has to be held before it counts as a vote to skip.</summary>
        public const float HoldSeconds = 1f;
        /// <summary>The host starts the Intro this long after every machine has its docks built, so nobody misses the first picture.</summary>
        public const float LeadIn = 2.5f;
        /// <summary>The length of the Intro: the workers talking in a circle (5.4 s), the TV coming on and the workers lining up in front of it, the monologue on it (27 s), a moment of static, the workers' line, a black "Later that night" card, the vendor's bay door rolling up on him and his four lines.</summary>
        public const float Total = 60.1f;
        /// <summary>The TV comes on at this time (before it the workers stand in a circle talking and it is dark), shows static for <see cref="TvStaticSeconds"/> and then the picture; the workers look round at it, one after the other, and walk to their places in the line in front of it (the owner, 2026-10-20).</summary>
        public const float TvOnAt = 5.4f;
        public const float TvStaticSeconds = 0.5f;
        /// <summary>The monologue ends and the TV goes to static at this time.</summary>
        public const float ProgramEnd = 32.4f;
        public const float FadeIn = 1f, FadeOut = 1.2f;
        public const float CharsPerSecond = 30f;
        /// <summary>The private jet flies across the windows during the window shot (on the TV).</summary>
        public const float JetStart = 23.8f, JetSeconds = 4.6f;
        /// <summary>The vendor's bay door starts to roll up (it takes <see cref="DockBayDoor.RollSeconds"/>) a little after the workers' line has ended (the owner, 2026-10-19 night: the vendor shows up after they say they are going to call).</summary>
        public const float BayOpensAt = 42.4f;
        /// <summary>How many workers hang out in the break room: the players' characters, a coworker beside a lone player, and coworkers to make five (the owner, 2026-10-20).</summary>
        public const int Workers = 5;
        /// <summary>The workers take in the line that takes their bonuses this long after they have heard it, and only then do their heads drop (the owner, 2026-10-20: "they should wait until after they hear it").</summary>
        public const float DropAfterLine = 0.35f;

        /// <summary>The beats that matter, by number: the dark TV, the TV coming on, the boss's first and last lines, the line that takes the bonuses, the static, the workers' line, the night card, the vendor's door and his four lines.</summary>
        public const int OffBeat = 0, TvOnBeat = 1, FirstBossBeat = 2, BonusBeat = 4, LastBossBeat = 7, StaticBeat = 8, WorkerBeat = 9, NightBeat = 10, DoorBeat = 11, FirstVendorBeat = 12, LastVendorBeat = 15;

        /// <summary>
        /// Between the workers' line and the vendor (the owner, 2026-10-27: "Add quick black screen that says "Later that night" right after the dock worker says "call the guys you know" but before the vendor scene"): the picture goes
        /// black and <see cref="IntroContent.NightCard"/> is shown, fading in and out in <see cref="CardFade"/> s. From the card on the TV is off (it is night: no static and no hiss under the vendor's lines; the owner, 2026-10-27).
        /// </summary>
        public const float CardFade = 0.35f;

        /// <summary>The timeline: sixteen beats, 60.1 s. The words are in <see cref="IntroContent"/>.</summary>
        public static readonly IntroBeat[] Beats =
        {
            new IntroBeat(0.0f, 5.4f, IntroShot.Off, DockShot.Establish, null, Speaker.None),
            new IntroBeat(5.4f, 7.4f, IntroShot.Wide, DockShot.Establish, null, Speaker.None),
            new IntroBeat(7.4f, 10.9f, IntroShot.Medium, DockShot.Behind, IntroContent.Line1, Speaker.Boss),
            new IntroBeat(10.9f, 15.9f, IntroShot.MediumClose, DockShot.TvClose, IntroContent.Line2, Speaker.Boss),
            new IntroBeat(15.9f, 20.4f, IntroShot.Desk, DockShot.Faces, IntroContent.Line3, Speaker.Boss),
            new IntroBeat(20.4f, 23.4f, IntroShot.Medium, DockShot.TvClose, IntroContent.Line4, Speaker.Boss),
            new IntroBeat(23.4f, 28.4f, IntroShot.Window, DockShot.TvClose, IntroContent.Line5, Speaker.Boss),
            new IntroBeat(28.4f, 32.4f, IntroShot.Low, DockShot.Faces, IntroContent.Line6, Speaker.Boss),
            new IntroBeat(32.4f, 34.2f, IntroShot.None, DockShot.Behind, null, Speaker.None),
            new IntroBeat(34.2f, 39.4f, IntroShot.None, DockShot.TwoShot, IntroContent.FinalLine, Speaker.Worker),
            new IntroBeat(39.4f, 41.9f, IntroShot.Off, DockShot.Bay, null, Speaker.None),
            new IntroBeat(41.9f, 44.3f, IntroShot.Off, DockShot.Bay, null, Speaker.None),
            new IntroBeat(44.3f, 46.8f, IntroShot.Off, DockShot.Bay, IntroContent.VendorLine1, Speaker.Vendor),
            new IntroBeat(46.8f, 49.9f, IntroShot.Off, DockShot.Bay, IntroContent.VendorLine2, Speaker.Vendor),
            new IntroBeat(49.9f, 56.1f, IntroShot.Off, DockShot.Bay, IntroContent.VendorLine3, Speaker.Vendor),
            new IntroBeat(56.1f, 60.1f, IntroShot.Off, DockShot.Bay, IntroContent.VendorLine4, Speaker.Vendor),
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

        /// <summary>How long a line takes to type out, which is how long it takes to say (s).</summary>
        public static float TypedSeconds(string line) => string.IsNullOrEmpty(line) ? 0f : line.Length / CharsPerSecond;

        /// <summary>How much of the night card shows at this time: 0 outside its beat, 1 for most of it, faded in and out over <see cref="CardFade"/> at its two ends.</summary>
        public static float NightCard(float t)
        {
            var b = Beats[NightBeat];
            if (t < b.start || t >= b.end) return 0f;
            return Math.Min(1f, Math.Min((t - b.start) / CardFade, (b.end - t) / CardFade));
        }

        /// <summary>0 = black, 1 = the picture: faded in at the start and out at the end.</summary>
        public static float Fade(float t) => Math.Max(0f, Math.Min(1f, Math.Min(t / FadeIn, (Total - t) / FadeOut)));

        /// <summary>How far through its beat the time is (0 to 1).</summary>
        public static float Progress(in IntroBeat beat, float t) => beat.Length <= 0f ? 1f : Math.Max(0f, Math.Min(1f, (t - beat.start) / beat.Length));

        public static bool Over(float t) => t >= Total;

        // ------------------------------------------------------------------ the cast

        /// <summary>
        /// Who says the worker's line, as an index into the cast: with a lone player the cast is the coworker (index 0) and the player's character, and the coworker speaks;
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

        /// <summary>The second at which the workers' heads drop: the line that takes the bonuses has been said (typed out, which is when it is heard) and they take it in for a moment.</summary>
        public static float CrushedAt => Beats[BonusBeat].start + TypedSeconds(Beats[BonusBeat].line) + DropAfterLine;

        /// <summary>
        /// How the workers look at this time: -1 talking in the circle (the TV is not on yet), 0 as they are (the boss is friendly: until the line that takes the bonuses has been heard), 1 crushed (until the broadcast ends),
        /// 2 angry (from the workers' line: calling somebody). Only the Party characters have faces to change; the Tiny Toon people show it in their head and chest.
        /// </summary>
        public static int MoodAt(float t) => t < TvOnAt ? -1 : t < CrushedAt ? 0 : t < Beats[WorkerBeat].start ? 1 : 2;

        /// <summary>How far apart (m) the workers stand side by side: 1.2 m for up to three, 1.0 m for four or five.</summary>
        public static float Spacing(int cast) => cast <= 3 ? 1.2f : 1.0f;

        // ------------------------------------------------------------------ the opening: a circle talking, then a line in front of the TV

        /// <summary>
        /// The opening (the owner, 2026-10-20: "have them all in the break room in a circle talking then the tv plays and then they all line up to watch it"): the workers stand in a RING of this radius (m), facing its middle, and talk - a
        /// different one every <see cref="TalkTurnSeconds"/>, the others looking at whoever talks. When the TV comes on they look round at it and set off for their places in the line in front of it.
        /// </summary>
        public const float RingRadius = 0.9f, TalkTurnSeconds = 1.4f;
        /// <summary>
        /// How fast they walk to the line (m/s), how long after the TV comes on the first of them sets off (they look round at it first), and how far apart the walks END (the shortest ends first, from <see cref="FirstArrival"/> on, so the
        /// longest set off first and they come into the line one after the other as the boss starts).
        /// </summary>
        public const float WalkSpeed = 1.7f, NoticeSeconds = 0.3f, ArrivalGap = 0.15f;
        public const float FirstArrival = TvOnAt + 1.5f;

        /// <summary>Who is talking in the circle at this time (an index into the workers): each takes a turn, in an order that goes round all of them before it comes back to the first.</summary>
        public static int TalkerAt(float t, int workers)
        {
            if (workers <= 1) return 0;
            int step = 2;
            while (Gcd(step, workers) != 1) step++;
            int turn = (int)Math.Floor(Math.Max(0f, t) / TalkTurnSeconds);
            return (int)(((long)turn * step) % workers);
        }

        static int Gcd(int a, int b) { while (b != 0) { int r = a % b; a = b; b = r; } return a; }

        /// <summary>How long a walk of <paramref name="metres"/> takes (s).</summary>
        public static float WalkSeconds(float metres) => metres / WalkSpeed;

        /// <summary>
        /// When a worker whose walk to the line is <paramref name="metres"/> long sets off, given how many of the others have a shorter walk than his (<paramref name="shorter"/>): the walks end one after the other from
        /// <see cref="FirstArrival"/> on, the shortest first; nobody sets off before he has had a moment (<see cref="NoticeSeconds"/>) to look round at the TV.
        /// </summary>
        public static float DepartAt(float metres, int shorter) => Math.Max(TvOnAt + NoticeSeconds, FirstArrival + shorter * ArrivalGap - WalkSeconds(metres));

        /// <summary>How far along his walk a worker is at this time: 0 = in the circle, 1 = in his place in the line (a steady walk).</summary>
        public static float WalkProgress(float t, float departAt, float metres) =>
            metres <= 0.001f ? (t >= departAt ? 1f : 0f) : Math.Max(0f, Math.Min(1f, (t - departAt) / WalkSeconds(metres)));
    }
}