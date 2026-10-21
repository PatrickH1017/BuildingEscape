namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The run-start cinematic's rules (owner, 2026-10-16 night: it plays at every new run, HOLD SPACE skips it, in multiplayer at least 50 % of the
        /// party members must hold): the vote for 1 to 5 players, the hold meter, the timeline, the typed text and the fades.
        /// </summary>
        public static void IntroRulesTest()
        {
            Check("intro: a lone player skips by holding, nobody holding never skips", IntroRules.SkipPassed(1, 1) && !IntroRules.SkipPassed(0, 1) && !IntroRules.SkipPassed(0, 5));
            Check("intro: half of the party or more skips (1 of 2, 2 of 3, 2 of 4, 3 of 5)", IntroRules.SkipPassed(1, 2) && IntroRules.SkipPassed(2, 3) && IntroRules.SkipPassed(2, 4) && IntroRules.SkipPassed(3, 5));
            Check("intro: less than half does not (1 of 3, 1 of 4, 2 of 5)", !IntroRules.SkipPassed(1, 3) && !IntroRules.SkipPassed(1, 4) && !IntroRules.SkipPassed(2, 5));
            bool needed = true;
            for (int party = 1; party <= 5; party++)
            {
                int n = IntroRules.NeededToSkip(party);
                needed &= IntroRules.SkipPassed(n, party) && !IntroRules.SkipPassed(n - 1, party);
            }
            Check("intro: the number needed is the smallest that passes (1, 1, 2, 2, 3 for 1 to 5 players)", needed
                && IntroRules.NeededToSkip(1) == 1 && IntroRules.NeededToSkip(2) == 1 && IntroRules.NeededToSkip(3) == 2 && IntroRules.NeededToSkip(4) == 2 && IntroRules.NeededToSkip(5) == 3);

            float held = 0f;
            for (int i = 0; i < 9; i++) held = IntroRules.Hold(held, true, 0.1f);
            bool notYet = !IntroRules.HoldDone(held);
            held = IntroRules.Hold(held, true, 0.1f);
            Check("intro: Space has to be held for a full second (not 0.9 s, yes 1.0 s)", notYet && IntroRules.HoldDone(held) && IntroRules.HoldSeconds == 1f);
            Check("intro: the meter never goes past full or below empty, and empties twice as fast as it fills",
                IntroRules.Hold(1f, true, 5f) == 1f && IntroRules.Hold(0f, false, 5f) == 0f && System.Math.Abs(IntroRules.Hold(1f, false, 0.25f) - 0.5f) < 1e-5f);

            var beats = IntroRules.Beats;
            bool contiguous = beats.Length > 0 && beats[0].start == 0f && beats[beats.Length - 1].end == IntroRules.Total;
            bool typed = true, longEnough = true;
            for (int i = 0; i < beats.Length; i++)
            {
                if (i > 0) contiguous &= beats[i].start == beats[i - 1].end;
                longEnough &= beats[i].Length > 0f;
                if (!string.IsNullOrEmpty(beats[i].line)) typed &= beats[i].line.Length / IntroRules.CharsPerSecond + 0.6f <= beats[i].Length;
            }
            Check("intro: the beats follow one another from 0 to the total (57.6 s) with no gap or overlap", contiguous && longEnough && IntroRules.Total == 57.6f && beats.Length == 15);
            Check("intro: every line is typed out with 0.6 s to read it before the next beat", typed);
            int last = beats.Length - 1, worker = -1;
            for (int i = 0; i < beats.Length; i++) if (beats[i].speaker == Speaker.Worker) worker = i;
            Check("intro: the opening - the TV is dark for 5.4 s while the five workers talk in a circle, then it comes on (the boss's office) before he says a word, and there are five workers",
                IntroRules.Workers == 5 && beats[IntroRules.OffBeat].shot == IntroShot.Off && beats[IntroRules.OffBeat].start == 0f && beats[IntroRules.OffBeat].end == IntroRules.TvOnAt && string.IsNullOrEmpty(beats[IntroRules.OffBeat].line)
                && beats[IntroRules.TvOnBeat].shot == IntroShot.Wide && beats[IntroRules.TvOnBeat].start == IntroRules.TvOnAt && string.IsNullOrEmpty(beats[IntroRules.TvOnBeat].line) && beats[IntroRules.TvOnBeat].Length > IntroRules.TvStaticSeconds + 1f);
            bool bossLines = true;
            for (int i = IntroRules.FirstBossBeat; i <= IntroRules.LastBossBeat; i++) bossLines &= beats[i].speaker == Speaker.Boss && !string.IsNullOrEmpty(beats[i].line) && beats[i].shot != IntroShot.None && beats[i].shot != IntroShot.Off;
            Check("intro: the TV shows the boss's monologue (six lines, each said by the boss) until 32.4 s, then static",
                bossLines && beats[IntroRules.LastBossBeat].end == IntroRules.ProgramEnd && beats[IntroRules.StaticBeat].shot == IntroShot.None && beats[IntroRules.WorkerBeat].shot == IntroShot.None && beats[last].shot == IntroShot.None && IntroRules.ProgramEnd == 32.4f
                && beats[IntroRules.FirstBossBeat].line == IntroContent.Line1 && beats[IntroRules.BonusBeat].line == IntroContent.Line3 && beats[IntroRules.LastBossBeat].line == IntroContent.Line6);
            Check("intro: after the broadcast one worker says the owner's new line (that's enough, time for revenge, meet up tonight, call the Vendor)",
                worker == IntroRules.WorkerBeat && beats[worker].start >= IntroRules.ProgramEnd && beats[worker].line == "That's enough, it's time for revenge. Let's meet back up tonight. Call that Vendor you talked about." && IntroContent.FinalLine == beats[worker].line);
            bool vendorLines = true;
            for (int i = IntroRules.FirstVendorBeat; i <= IntroRules.LastVendorBeat; i++) vendorLines &= beats[i].speaker == Speaker.Vendor && beats[i].dock == DockShot.Bay && beats[i].shot == IntroShot.None && !string.IsNullOrEmpty(beats[i].line);
            Check("intro: the vendor says the owner's four lines, one beat for each break, after the workers' line and his door (the last beat of the Intro)",
                vendorLines && IntroRules.LastVendorBeat == last && IntroRules.FirstVendorBeat == IntroRules.DoorBeat + 1 && beats[IntroRules.DoorBeat].dock == DockShot.Bay && string.IsNullOrEmpty(beats[IntroRules.DoorBeat].line)
                && beats[11].line == "I got your call, just remember I ain't cheap." && beats[12].line == "Every floor's got a quota, the longer I'm here the more it is."
                && beats[13].line == "Just get the loot back to the elevator, my boys will take care of the rest. When my clients hit the quota it makes my job easier, so don't undercut it."
                && beats[14].line == "Any cool weapons you find bring back to me, I might have a deal for ya. Good luck.");
            Check("intro: his door starts to roll up after the workers' line has ended and is all the way up before he speaks",
                IntroRules.BayOpensAt >= beats[IntroRules.WorkerBeat].end && IntroRules.BayOpensAt + DockBayDoor.RollSeconds < beats[IntroRules.FirstVendorBeat].start);
            Check("intro: the camera starts across the docks, ends on the vendor's bay, and is on the two who talk for the workers' line", beats[0].dock == DockShot.Establish && beats[worker].dock == DockShot.TwoShot && beats[last].dock == DockShot.Bay);
            bool docks = true; var used = new System.Collections.Generic.HashSet<DockShot>();
            foreach (var b in beats) used.Add(b.dock);
            Check("intro: the cutscene uses all six camera set-ups in the docks (across the room, behind the workers, close on the TV, their faces, the two who talk, the vendor's bay)", used.Count == 6 && docks);
            Check("intro: BeatAt finds the beat and says -1 outside the Intro", IntroRules.BeatAt(0f) == 0 && IntroRules.BeatAt(5.4f) == 1 && IntroRules.BeatAt(7.4f) == 2 && IntroRules.BeatAt(57.59f) == beats.Length - 1
                && IntroRules.BeatAt(-1f) == -1 && IntroRules.BeatAt(IntroRules.Total) == -1 && IntroRules.BeatAt(34.3f) == 9);
            Check("intro: letters appear at 30 a second and stop at the end of the line", IntroRules.VisibleChars("abcdef", 0f) == 0 && IntroRules.VisibleChars("abcdef", 0.1f) == 3
                && IntroRules.VisibleChars("abcdef", 9f) == 6 && IntroRules.VisibleChars(null, 1f) == 0);
            Check("intro: the picture fades in from black and out to black", IntroRules.Fade(0f) == 0f && IntroRules.Fade(IntroRules.Total * 0.5f) == 1f && IntroRules.Fade(IntroRules.Total) == 0f
                && IntroRules.Fade(IntroRules.FadeIn * 0.5f) > 0.4f && IntroRules.Fade(IntroRules.FadeIn * 0.5f) < 0.6f);
            int window = IntroRules.BeatAt(IntroRules.JetStart);
            Check("intro: the jet crosses the windows during the window shot (on the TV)", window >= 0 && beats[window].shot == IntroShot.Window
                && IntroRules.JetStart + IntroRules.JetSeconds <= beats[window].end + 0.5f);

            // who stands where and who speaks
            Check("intro: a lone player gets a coworker, and the coworker speaks to them", IntroRules.SpeakerIndex(2, true, 12345) == 0 && IntroRules.ListenerIndex(2, 0) == 1 && IntroRules.SpeakerIndex(1, false, 7) == 0);
            bool seedsOk = true; var speakers = new System.Collections.Generic.HashSet<int>();
            for (int cast = 2; cast <= 5; cast++)
                for (int seed = -50; seed < 400; seed += 7)
                {
                    int sp = IntroRules.SpeakerIndex(cast, false, seed), li = IntroRules.ListenerIndex(cast, sp);
                    seedsOk &= sp >= 0 && sp < cast && li >= 0 && li < cast && li != sp && sp == IntroRules.SpeakerIndex(cast, false, seed);
                    if (cast == 5) speakers.Add(sp);
                }
            Check("intro: in a party the speaker is picked from the run's seed (the same on every machine, anybody, never talking to themselves), and varies from run to run", seedsOk && speakers.Count >= 3);
            bool rows = true;
            for (int cast = 1; cast <= 5; cast++)
                for (int sp = 0; sp < cast; sp++)
                {
                    int li = IntroRules.ListenerIndex(cast, sp);
                    var row = IntroRules.RowOrder(cast, sp, li);
                    var seen = new System.Collections.Generic.HashSet<int>(row);
                    rows &= row.Length == cast && seen.Count == cast;                                          // everybody stands somewhere, nobody twice
                    if (cast >= 2)
                    {
                        int ps = System.Array.IndexOf(row, sp), pl = System.Array.IndexOf(row, li);
                        rows &= ps == pl + 1;                                                                  // the listener is just left of the speaker
                        rows &= System.Math.Abs((ps + pl) / 2.0 - (cast - 1) / 2.0) <= 1.0;                    // and the pair is in the middle of the row
                    }
                }
            Check("intro: everybody has a place in the row, and the speaker and the one they talk to stand next to each other in the middle", rows);
            float lineStart = beats[IntroRules.BonusBeat].start, lineSaid = lineStart + IntroRules.TypedSeconds(beats[IntroRules.BonusBeat].line);
            Check("intro: the workers hang out until the TV comes on, are as they are while the boss is friendly and while the bonus line is SAID (their heads do not drop at its first word), and drop their heads a moment after it has been heard (the owner, 2026-10-20), then are angry from the workers' line",
                IntroRules.MoodAt(0f) == -1 && IntroRules.MoodAt(IntroRules.TvOnAt - 0.01f) == -1 && IntroRules.MoodAt(IntroRules.TvOnAt + 0.01f) == 0 && IntroRules.MoodAt(lineStart + 0.1f) == 0 && IntroRules.MoodAt(lineStart + IntroRules.TypedSeconds(beats[IntroRules.BonusBeat].line) * 0.5f) == 0
                && IntroRules.MoodAt(lineSaid) == 0 && IntroRules.MoodAt(IntroRules.CrushedAt + 0.01f) == 1 && IntroRules.CrushedAt > lineSaid && IntroRules.CrushedAt < beats[IntroRules.BonusBeat].end && IntroRules.MoodAt(IntroRules.ProgramEnd - 0.1f) == 1
                && IntroRules.MoodAt(beats[IntroRules.WorkerBeat].start + 0.1f) == 2 && IntroRules.MoodAt(beats[beats.Length - 1].start) == 2, $"(the line starts at {lineStart:0.0} s and is said by {lineSaid:0.0} s, the heads drop at {IntroRules.CrushedAt:0.00} s)");
            Check("intro: the workers stand 1.2 m apart (up to three) or 1 m (four or five) so a party of five still fits in front of the TV", IntroRules.Spacing(1) == 1.2f && IntroRules.Spacing(3) == 1.2f && IntroRules.Spacing(4) == 1.0f && IntroRules.Spacing(5) == 1.0f
                && (5 - 1) * IntroRules.Spacing(5) <= 4.5f);

            // the opening: a circle talking, then a line in front of the TV (the owner, 2026-10-20)
            var talkers = new System.Collections.Generic.HashSet<int>();
            bool turnsChange = true;
            for (int turn = 0; turn < 10; turn++)
            {
                int tk = IntroRules.TalkerAt((turn + 0.5f) * IntroRules.TalkTurnSeconds, 5);
                talkers.Add(tk);
                if (turn > 0) turnsChange &= tk != IntroRules.TalkerAt((turn - 0.5f) * IntroRules.TalkTurnSeconds, 5);
            }
            Check("intro: in the circle everybody takes a turn to talk (a different one every 1.4 s, all five in five turns)",
                talkers.Count == 5 && turnsChange && IntroRules.TalkerAt(0f, 5) == IntroRules.TalkerAt(0.5f, 5) && IntroRules.TalkerAt(-3f, 5) == IntroRules.TalkerAt(0f, 5) && IntroRules.TalkerAt(7f, 1) == 0);
            bool walksOk = true; float lastArrive = 0f;
            for (int shorter = 0; shorter < 5; shorter++)
            {
                float metres = 0.5f + shorter * 0.6f;               // (0.5 to 2.9 m: the more walks are shorter than theirs, the longer theirs)
                float dep = IntroRules.DepartAt(metres, shorter), arrive = dep + IntroRules.WalkSeconds(metres), halfWay = IntroRules.WalkProgress((dep + arrive) * 0.5f, dep, metres);
                walksOk &= dep >= IntroRules.TvOnAt + IntroRules.NoticeSeconds - 1e-4f && IntroRules.WalkProgress(dep - 0.01f, dep, metres) == 0f && IntroRules.WalkProgress(arrive, dep, metres) > 0.999f && halfWay > 0.49f && halfWay < 0.51f;
                lastArrive = System.Math.Max(lastArrive, arrive);
            }
            Check("intro: when the TV comes on nobody sets off before they have looked round at it, a walk is steady from set-off to arrival, and all five are in the line within half a second of the boss's first word",
                walksOk && lastArrive < beats[IntroRules.FirstBossBeat].start + 0.5f, $"(the last one arrives at {lastArrive:0.00} s, the boss starts at {beats[IntroRules.FirstBossBeat].start:0.0} s)");
        }
    }
}
