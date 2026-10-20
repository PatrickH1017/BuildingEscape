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
            Check("intro: the beats follow one another from 0 to the total (35 s) with no gap or overlap", contiguous && longEnough && IntroRules.Total == 35f && beats.Length == 10);
            Check("intro: every line is typed out with 0.6 s to read it before the next beat", typed);
            int last = beats.Length - 1, worker = -1;
            for (int i = 0; i < beats.Length; i++) if (beats[i].speaker == Speaker.Worker) worker = i;
            bool bossLines = true;
            for (int i = 1; i <= 6; i++) bossLines &= beats[i].speaker == Speaker.Boss && !string.IsNullOrEmpty(beats[i].line) && beats[i].shot != IntroShot.None;
            Check("intro: the TV shows the boss's monologue for 27 s (six lines, each said by the boss), then static", bossLines && beats[0].shot == IntroShot.Wide && string.IsNullOrEmpty(beats[0].line)
                && beats[6].end == IntroRules.ProgramEnd && beats[7].shot == IntroShot.None && beats[8].shot == IntroShot.None && beats[9].shot == IntroShot.None && IntroRules.ProgramEnd == 27f);
            Check("intro: after the broadcast one worker says the owner's line, the last words of the Intro", worker == 8 && beats[8].start >= IntroRules.ProgramEnd && beats[8].line == "Hey call that guy you were talking about."
                && IntroContent.FinalLine == "Hey call that guy you were talking about." && string.IsNullOrEmpty(beats[last].line) && beats[last].speaker == Speaker.None);
            Check("intro: the camera starts across the docks and ends on the two who talk", beats[0].dock == DockShot.Establish && beats[worker].dock == DockShot.TwoShot);
            bool docks = true; var used = new System.Collections.Generic.HashSet<DockShot>();
            foreach (var b in beats) used.Add(b.dock);
            Check("intro: the cutscene uses all five camera set-ups in the docks (across the room, behind the workers, close on the TV, their faces, the two who talk)", used.Count == 5 && docks);
            Check("intro: BeatAt finds the beat and says -1 outside the Intro", IntroRules.BeatAt(0f) == 0 && IntroRules.BeatAt(2.0f) == 1 && IntroRules.BeatAt(34.99f) == beats.Length - 1
                && IntroRules.BeatAt(-1f) == -1 && IntroRules.BeatAt(IntroRules.Total) == -1 && IntroRules.BeatAt(28.9f) == 8);
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
            Check("intro: the workers look as they are for the first two lines, crushed when the bonuses go, angry for the last line", IntroRules.FaceMood(0) == 0 && IntroRules.FaceMood(2) == 0 && IntroRules.FaceMood(3) == 1
                && IntroRules.FaceMood(7) == 1 && IntroRules.FaceMood(8) == 2 && IntroRules.FaceMood(9) == 2);
            Check("intro: the workers stand 1.2 m apart (up to three) or 1 m (four or five) so a party of five still fits in front of the TV", IntroRules.Spacing(1) == 1.2f && IntroRules.Spacing(3) == 1.2f && IntroRules.Spacing(4) == 1.0f && IntroRules.Spacing(5) == 1.0f
                && (5 - 1) * IntroRules.Spacing(5) <= 4.5f);
        }
    }
}
