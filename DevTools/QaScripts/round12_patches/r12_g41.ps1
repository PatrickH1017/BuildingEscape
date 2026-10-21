# Round 12, batch G41 (the owner, 2026-10-20): the intro cinematic.
#  "In the beginning of the intro cinematic there should be 5 Loading Dock workers in the break room hanging out casually and then the tv should come on for the announcement and the characters will turn to look at the tv and listen to the
#   announcement" / "the plane sound is too loud" / "when the players are being told their bonuses are removed they should wait until after they hear it before they drop their heads (currently they drop their heads before being told)" /
#   "after watching the tape and before saying call that guy, the character should now say, 'That's enough, it's time for revenge. Let's meet back up tonight. Call that Vendor you talked about.'" / "after the Vendor arrives he should say,
#   'I got your call, just remember I ain't cheap. -Break- Every floor's got a quota, the longer I'm here the more it is. -Break- Just get the loot back to the elevator, my boys will take care of the rest. When my clients hit the quota it makes
#   my job easier, so don't undercut it. -Break- Any cool weapons you find bring back to me, I might have a deal for ya. Good luck.'"
# The timeline is 55.4 s now (it was 37.8): the TV is dark and five workers hang out for 3.2 s, it comes on (static, a click, then the boss's office) and they turn to it one after the other, the six lines of the boss (the same words, 3.2 s later),
# the workers' new line, the vendor's door rolls up and he says his four lines (a beat for each "-Break-"). The heads drop 0.35 s after the line that takes the bonuses has been said. BE_qa (= C:\bqm) only.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

function Write-Full([string]$path, [string]$text) {
    if ($env:R2_DRY) { Write-Output "dry run: would write $path"; return }
    $bytes = [IO.File]::ReadAllBytes($path)
    $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    [IO.File]::WriteAllText($path, $text.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding($bom)))
    Write-Output "wrote $path (bom=$bom)"
}

# ---------------------------------------------------------------- the words
$content = @'
namespace BuildingEscape
{
    /// <summary>
    /// The words of the run-start cinematic (the "Intro"). The boss's six lines are PLACEHOLDERS written to the owner's brief (the boss tells the loading dock workers, on the wall TV, that their bonuses are gone this year because he is
    /// buying a private jet); the workers' line and the vendor's four lines are the owner's own words (2026-10-20). The owner writes the game's text, so rewrite them here and nothing else has to move (the start and end seconds of a beat in
    /// <see cref="IntroRules.Beats"/> say how long a line stays up; about 30 letters a second are typed, so a line needs <c>length / 30 + 0.6</c> seconds - the pure test <c>IntroRulesTest</c> checks that).
    /// </summary>
    public static class IntroContent
    {
        /// <summary>Who the subtitle names while the boss talks (on the TV).</summary>
        public const string BossLabel = "THE BOSS";
        public const string SkipHint = "HOLD SPACE TO SKIP";
        /// <summary>Shown instead of the hint once this player's bar has filled (the vote is saved, they can let go).</summary>
        public const string SavedHint = "SKIP VOTE SAVED";

        public const string Line1 = "Team, gather round. This will be quick.";
        public const string Line2 = "It has been a record year for the company. Record profits. Record growth.";
        public const string Line3 = "Which is why, this year, I am taking away the Loading Dock bonuses.";
        public const string Line4 = "Every last cent. Gone.";
        public const string Line5 = "A man in my position needs a private jet. And jets are not cheap.";
        public const string Line6 = "So the bonus fund is now the jet fund. Now, back to work.";

        /// <summary>
        /// What one worker says to another when the broadcast is over (the owner's own words, 2026-10-20: "after watching the tape and before saying call that guy, the character should now say ..."; it was "Hey call that guy you were talking about.").
        /// </summary>
        public const string FinalLine = "That's enough, it's time for revenge. Let's meet back up tonight. Call that Vendor you talked about.";

        /// <summary>The name over the vendor's lines.</summary>
        public const string VendorLabel = "THE VENDOR";

        /// <summary>
        /// What the vendor says when his door has rolled up (the owner's own words, 2026-10-20: "this will give the players a good jist of the game"), one beat for each "-Break-": his price, the quota that grows with his stay, the loot to the
        /// elevator and not undercutting the quota, and the weapons he wants to see.
        /// </summary>
        public const string VendorLine1 = "I got your call, just remember I ain't cheap.";
        public const string VendorLine2 = "Every floor's got a quota, the longer I'm here the more it is.";
        public const string VendorLine3 = "Just get the loot back to the elevator, my boys will take care of the rest. When my clients hit the quota it makes my job easier, so don't undercut it.";
        public const string VendorLine4 = "Any cool weapons you find bring back to me, I might have a deal for ya. Good luck.";

        /// <summary>The name over the worker's line when it is the coworker who stands beside a lone player; a player's character is named by their number.</summary>
        public const string CoworkerLabel = "COWORKER";
        public static string PlayerLabel(int slot) => "PLAYER " + (slot + 1);

        /// <summary>A CharacterCatalog id (the Synty office pack's boss since 2026-10-17 night; before that Tiny Toon male_b); the Party character stands in when this machine has not got it.</summary>
        public const string BossModelId = "syn_boss_male_01";
    }
}
'@
Write-Full "$A\Intro\IntroContent.cs" $content

# ---------------------------------------------------------------- the timeline and the rules
$rules = @'
using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>What the TV shows (the boss's office camera): the camera set-ups of the monologue. <see cref="None"/>: the broadcast is over, the screen is static. <see cref="Off"/>: the TV is dark (the opening, before the announcement).</summary>
    public enum IntroShot { Wide, Medium, MediumClose, Desk, Window, Low, None, Off }

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
    /// The run-start cinematic ("the Intro"): five loading dock workers - the players' own characters and coworkers - hang out in the break room with the wall-mounted TV dark; it comes on for the announcement, they turn to it and watch the
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
        /// <summary>The length of the Intro: the workers hanging out (3.2 s), the TV coming on, the monologue on it (27 s), a moment of static, the workers' line, the vendor's bay door rolling up on him and his four lines.</summary>
        public const float Total = 55.4f;
        /// <summary>The TV comes on at this time (before it the workers hang out and it is dark), shows static for <see cref="TvStaticSeconds"/> and then the picture; the workers turn to it, one after the other.</summary>
        public const float TvOnAt = 3.2f;
        public const float TvStaticSeconds = 0.5f;
        /// <summary>The monologue ends and the TV goes to static at this time.</summary>
        public const float ProgramEnd = 30.2f;
        public const float FadeIn = 1f, FadeOut = 1.2f;
        public const float CharsPerSecond = 30f;
        /// <summary>The private jet flies across the windows during the window shot (on the TV).</summary>
        public const float JetStart = 21.6f, JetSeconds = 4.6f;
        /// <summary>The vendor's bay door starts to roll up (it takes <see cref="DockBayDoor.RollSeconds"/>) a little after the workers' line has ended (the owner, 2026-10-19 night: the vendor shows up after they say they are going to call).</summary>
        public const float BayOpensAt = 37.7f;
        /// <summary>How many workers hang out in the break room: the players' characters, a coworker beside a lone player, and coworkers to make five (the owner, 2026-10-20).</summary>
        public const int Workers = 5;
        /// <summary>The workers take in the line that takes their bonuses this long after they have heard it, and only then do their heads drop (the owner, 2026-10-20: "they should wait until after they hear it").</summary>
        public const float DropAfterLine = 0.35f;

        /// <summary>The beats that matter, by number: the dark TV, the TV coming on, the boss's first and last lines, the line that takes the bonuses, the static, the workers' line, the vendor's door and his four lines.</summary>
        public const int OffBeat = 0, TvOnBeat = 1, FirstBossBeat = 2, BonusBeat = 4, LastBossBeat = 7, StaticBeat = 8, WorkerBeat = 9, DoorBeat = 10, FirstVendorBeat = 11, LastVendorBeat = 14;

        /// <summary>The timeline: fifteen beats, 55.4 s. The words are in <see cref="IntroContent"/>.</summary>
        public static readonly IntroBeat[] Beats =
        {
            new IntroBeat(0.0f, 3.2f, IntroShot.Off, DockShot.Establish, null, Speaker.None),
            new IntroBeat(3.2f, 5.2f, IntroShot.Wide, DockShot.Faces, null, Speaker.None),
            new IntroBeat(5.2f, 8.7f, IntroShot.Medium, DockShot.Behind, IntroContent.Line1, Speaker.Boss),
            new IntroBeat(8.7f, 13.7f, IntroShot.MediumClose, DockShot.TvClose, IntroContent.Line2, Speaker.Boss),
            new IntroBeat(13.7f, 18.2f, IntroShot.Desk, DockShot.Faces, IntroContent.Line3, Speaker.Boss),
            new IntroBeat(18.2f, 21.2f, IntroShot.Medium, DockShot.TvClose, IntroContent.Line4, Speaker.Boss),
            new IntroBeat(21.2f, 26.2f, IntroShot.Window, DockShot.TvClose, IntroContent.Line5, Speaker.Boss),
            new IntroBeat(26.2f, 30.2f, IntroShot.Low, DockShot.Faces, IntroContent.Line6, Speaker.Boss),
            new IntroBeat(30.2f, 32.0f, IntroShot.None, DockShot.Behind, null, Speaker.None),
            new IntroBeat(32.0f, 37.2f, IntroShot.None, DockShot.TwoShot, IntroContent.FinalLine, Speaker.Worker),
            new IntroBeat(37.2f, 39.6f, IntroShot.None, DockShot.Bay, null, Speaker.None),
            new IntroBeat(39.6f, 42.1f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine1, Speaker.Vendor),
            new IntroBeat(42.1f, 45.2f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine2, Speaker.Vendor),
            new IntroBeat(45.2f, 51.4f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine3, Speaker.Vendor),
            new IntroBeat(51.4f, 55.4f, IntroShot.None, DockShot.Bay, IntroContent.VendorLine4, Speaker.Vendor),
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
        /// How the workers look at this time: -1 hanging out (the TV is not on yet), 0 as they are (the boss is friendly: until the line that takes the bonuses has been heard), 1 crushed (until the broadcast ends),
        /// 2 angry (from the workers' line: calling somebody). Only the Party characters have faces to change; the Tiny Toon people show it in their head and chest.
        /// </summary>
        public static int MoodAt(float t) => t < TvOnAt ? -1 : t < CrushedAt ? 0 : t < Beats[WorkerBeat].start ? 1 : 2;

        /// <summary>How far apart (m) the workers stand side by side: 1.2 m for up to three, 1.0 m for four or five.</summary>
        public static float Spacing(int cast) => cast <= 3 ? 1.2f : 1.0f;
    }
}
'@
Write-Full "$A\Intro\IntroRules.cs" $rules

# ---------------------------------------------------------------- the TV can be off
Open-Src "$A\Intro\DockTv.cs"
Rep 'enum Mode { Standby, Program, Static }' 'enum Mode { Standby, Program, Static, Off }'
Rep @'
        /// <summary>Colour bars, dimmed, and a faint glow: nobody is watching.</summary>
'@ @'
        /// <summary>Dark: the TV is off (the opening of the Intro, before the announcement); only its LED glows.</summary>
        public void ShowOff()
        {
            if (mode == Mode.Off) return;
            mode = Mode.Off;
            screenMat.mainTexture = Texture2D.blackTexture;
            screenMat.color = new Color(0.03f, 0.03f, 0.035f);
            glow.intensity = 0.02f;
        }

        /// <summary>Colour bars, dimmed, and a faint glow: nobody is watching.</summary>
'@
Rep 'if (mode != Mode.Standby)' 'if (mode != Mode.Standby && mode != Mode.Off)'
Save-Src

# ---------------------------------------------------------------- the cinematic
Open-Src "$A\Intro\IntroCinematic.cs"
Rep 'public float yaw;' 'public float yaw, casualYaw, turnDelay, phase;                  // (casualYaw: where he looks while the workers hang out; turnDelay: how long after the TV comes on he looks round at it; phase: his own rhythm of swaying)'
Rep 'bool voting, shown, whooshed;' 'bool voting, shown, whooshed, tvClicked;'
Rep @'
                tv.ShowProgram(tvRt);
                BuildActors();
'@ @'
                tv.ShowOff();                                          // (the TV is dark until the announcement: IntroRules.TvOnAt)
                BuildActors();
'@
Rep @'
            int speaker = IntroRules.SpeakerIndex(cast.Count, coworker, RunSeed());
            int listener = IntroRules.ListenerIndex(cast.Count, speaker);
            var row = IntroRules.RowOrder(cast.Count, speaker, listener);
'@ @'
            int speaker = IntroRules.SpeakerIndex(cast.Count, coworker, RunSeed());
            int listener = IntroRules.ListenerIndex(cast.Count, speaker);
            // five workers hang out in the break room (the owner, 2026-10-20): the players' characters, the coworker of a lone player, and more coworkers to make five (after the speaker and the listener are known: they are among the first)
            for (int k = 0; cast.Count < IntroRules.Workers; k++) cast.Add((FillerLook(k, cast), IntroContent.CoworkerLabel));
            var row = IntroRules.RowOrder(cast.Count, speaker, listener);
'@
Rep @'
                if (who == listener) listenerAt = actors.Count - 1;
            }
        }
'@ @'
                if (who == listener) listenerAt = actors.Count - 1;
            }
            // hanging out: the workers stand in pairs turned to each other (the last one joins the pair next to him), until the TV comes on and they look round at it one after the other
            for (int i = 0; i < actors.Count; i++)
            {
                int mate = i % 2 == 0 ? Mathf.Min(i + 1, actors.Count - 1) : i - 1;
                if (mate == i) mate = Mathf.Max(0, i - 1);
                var toMate = actors[mate].foot - actors[i].foot; toMate.y = 0f;
                var a = actors[i];
                a.casualYaw = toMate.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toMate, Vector3.up).eulerAngles.y + (i % 3 - 1) * 12f : faceTv.eulerAngles.y;
                a.turnDelay = 0.15f * ((i * 3) % actors.Count) + 0.05f * i;
                a.phase = i * 1.9f;
                a.yaw = a.casualYaw;
                a.root.rotation = Quaternion.Euler(0f, a.yaw, 0f);
            }
        }

        /// <summary>A coworker in a look of his own to make up the five workers: a Tiny Toon person that nobody in the cast is, if there is one (Party characters in other colours when this machine has no toon people).</summary>
        static CharacterLook FillerLook(int k, List<(CharacterLook look, string label)> cast)
        {
            var looks = PartyCharacterFactory.PlayerLooks;
            var used = new HashSet<string>();
            foreach (var c in cast) if (!string.IsNullOrEmpty(c.look.model)) used.Add(c.look.model);
            string pick = null;
            var cat = CharacterCatalog.Instance;
            if (cat != null)
            {
                var ids = new List<string>();
                foreach (var id in ToonCycle) if (cat.Find(id) != null) ids.Add(id);
                for (int j = 0; j < ids.Count && pick == null; j++)
                {
                    var id = ids[(k * 2 + 3 + j) % ids.Count];
                    if (!used.Contains(id)) pick = id;
                }
                if (pick == null && ids.Count > 0) pick = ids[(k * 2 + 3) % ids.Count];
            }
            var look = looks[(2 + k) % looks.Length];
            return pick != null ? look.WithModel(pick) : look;
        }
'@
RepRegion 'float shake = mood == 2 ? Mathf.Sin(Time.time * 19f + a.root.GetInstanceID() * 0.37f) * 1.6f : 0f;' 'if (a.head) a.head.rotation = Quaternion.AngleAxis(shake, Vector3.up) * Quaternion.AngleAxis(a.headPitch, right) * a.head.rotation;' @'
float shake = mood == 2 ? Mathf.Sin(Time.time * 19f + a.root.GetInstanceID() * 0.37f) * 1.6f : 0f;
            // hanging out (before the TV comes on): a slow sway, the head turning a little as they talk and now and then a nod
            float sway = mood == -1 ? Mathf.Sin(Time.time * 0.8f + a.phase) * 9f + Mathf.Sin(Time.time * 2.3f + a.phase * 1.7f) * 3f : 0f;
            float nod = mood == -1 ? Mathf.Max(0f, Mathf.Sin(Time.time * 1.3f + a.phase * 2.1f)) * 5f : 0f;
            float lean = mood == -1 ? Mathf.Sin(Time.time * 0.6f + a.phase) * 1.5f : 0f;
            var right = a.root.right;
            if (a.spine) a.spine.rotation = Quaternion.AngleAxis(a.spinePitch + lean, right) * a.spine.rotation;
            if (a.head) a.head.rotation = Quaternion.AngleAxis(shake + sway, Vector3.up) * Quaternion.AngleAxis(a.headPitch + nod, right) * a.head.rotation;
'@
Rep 'int beatNow = IntroRules.BeatAt(t);' '// (the TV is dark and they hang out, then they are as they are until the bonus line has been HEARD, then crushed, then angry: IntroRules.MoodAt)'
Rep 'int mood = beatNow < 0 ? 0 : IntroRules.FaceMood(beatNow);' 'int mood = IntroRules.MoodAt(t);'
Rep 'float target = faceTv;' @'
bool turned = t >= IntroRules.TvOnAt + a.turnDelay;             // (the TV comes on: they look round at it, one after the other)
                float target = turned ? faceTv : a.casualYaw;
                bool turning = talking && (i == speakerAt || i == listenerAt);
'@
Rep 'int face = mood == 0 ? a.ownFace : mood == 1 ? 3 : 2;' 'int face = mood <= 0 ? a.ownFace : mood == 1 ? 3 : 2;'
Rep 'a.yaw = Mathf.MoveTowardsAngle(a.yaw, target, 300f * dt);' 'a.yaw = Mathf.MoveTowardsAngle(a.yaw, target, (turning ? 300f : 150f) * dt);'
RepRegion '// ---- the TV: the boss''s office while the monologue runs, then static' 'if (tv != null) { tv.ShowStatic(); StartHiss(); }' @'
// ---- the TV: dark while the workers hang out, static (and a click) for a moment when it comes on, then the boss's office while the monologue runs, then static again
            if (beat.shot == IntroShot.Off)
            {
                office.SetJet(-1f);
                if (tv != null) { tv.ShowOff(); StopHiss(); }
            }
            else if (beat.shot != IntroShot.None)
            {
                var rig = Rigs[(int)beat.shot];
                var o = office.root.position;
                officeCam.transform.position = o + Vector3.Lerp(rig.posA, rig.posB, p);
                officeCam.transform.rotation = Quaternion.LookRotation(o + Vector3.Lerp(rig.lookA, rig.lookB, p) - officeCam.transform.position, Vector3.up);
                officeCam.fieldOfView = TvFov(Mathf.Lerp(rig.fovA, rig.fovB, p));
                float jet = (t - IntroRules.JetStart) / IntroRules.JetSeconds;
                office.SetJet(jet);
                if (jet >= 0f && !whooshed && voice) { whooshed = true; voice.pitch = 1f; voice.PlayOneShot(whoosh, 0.22f); }          // (the plane: it was 0.8, too loud; the owner, 2026-10-20)
                RenderOffice();
                if (tv != null)
                {
                    if (t < IntroRules.TvOnAt + IntroRules.TvStaticSeconds)
                    {
                        if (!tvClicked && voice && blips != null) { tvClicked = true; voice.pitch = 0.5f; voice.PlayOneShot(blips[0], 0.6f); }          // (the click of a TV coming on)
                        tv.ShowStatic(); StartHiss();
                    }
                    else { StopHiss(); if (!tv.OnProgram) tv.ShowProgram(tvRt); }
                }
            }
            else
            {
                office.SetJet(-1f);
                if (tv != null) { tv.ShowStatic(); StartHiss(); }
'@
Rep 'voice.pitch = beat.speaker == Speaker.Worker ? Random.Range(1.3f, 1.65f) : Random.Range(0.92f, 1.12f);' 'voice.pitch = beat.speaker == Speaker.Worker ? Random.Range(1.3f, 1.65f) : beat.speaker == Speaker.Vendor ? Random.Range(0.72f, 0.86f) : Random.Range(0.92f, 1.12f);'
Rep @'
                var e = dockGen.DockTrailerEntrance;
                pos = e + new Vector3(Mathf.Lerp(-0.9f, -0.3f, p), Mathf.Lerp(1.6f, 1.55f, p), Mathf.Lerp(-3.2f, -1.9f, p));
                look = e + new Vector3(0f, 1.35f, 9.8f);
                fov = Mathf.Lerp(rig.fovA, rig.fovB, p);
'@ @'
                var e = dockGen.DockTrailerEntrance;
                float pb = Mathf.Clamp01((t - IntroRules.Beats[IntroRules.DoorBeat].start) / (IntroRules.Total - IntroRules.Beats[IntroRules.DoorBeat].start));          // (one slow push-in over the whole bay scene: the door, then the vendor's four lines)
                pos = e + new Vector3(Mathf.Lerp(-0.9f, -0.3f, pb), Mathf.Lerp(1.6f, 1.55f, pb), Mathf.Lerp(-3.2f, -1.4f, pb));
                look = e + new Vector3(0f, 1.35f, 9.8f);
                fov = Mathf.Lerp(rig.fovA, rig.fovB, pb);
'@
Rep 'if (hiss) return;' 'if (hiss) { if (!hiss.isPlaying) hiss.UnPause(); return; }'
Rep '        void StartHiss()' @'
        void StopHiss() { if (hiss && hiss.isPlaying) hiss.Pause(); }

        void StartHiss()
'@
Rep 'string who = worker && speakerAt >= 0 && speakerAt < actors.Count ? actors[speakerAt].label : worker ? IntroContent.CoworkerLabel : IntroContent.BossLabel;' @'
bool vendor = beat.speaker == Speaker.Vendor;
                string who = vendor ? IntroContent.VendorLabel : worker && speakerAt >= 0 && speakerAt < actors.Count ? actors[speakerAt].label : worker ? IntroContent.CoworkerLabel : IntroContent.BossLabel;
'@
Rep 'worker ? HudTheme.Info : HudTheme.Quota' 'worker ? HudTheme.Info : vendor ? HudTheme.Cash : HudTheme.Quota'
Save-Src

# ---------------------------------------------------------------- the tests: the pure rules
Open-Src "$E\NetDevIntroTest.cs"
RepRegion 'Check("intro: the beats follow one another from 0 to the total (37.8 s) with no gap or overlap"' 'Check("intro: the camera starts across the docks and ends on the two who talk", beats[0].dock == DockShot.Establish && beats[worker].dock == DockShot.TwoShot);' @'
Check("intro: the beats follow one another from 0 to the total (55.4 s) with no gap or overlap", contiguous && longEnough && IntroRules.Total == 55.4f && beats.Length == 15);
            Check("intro: every line is typed out with 0.6 s to read it before the next beat", typed);
            int last = beats.Length - 1, worker = -1;
            for (int i = 0; i < beats.Length; i++) if (beats[i].speaker == Speaker.Worker) worker = i;
            Check("intro: the opening - the TV is dark for 3.2 s while the five workers hang out, then it comes on (the boss's office) before he says a word, and there are five workers",
                IntroRules.Workers == 5 && beats[IntroRules.OffBeat].shot == IntroShot.Off && beats[IntroRules.OffBeat].start == 0f && beats[IntroRules.OffBeat].end == IntroRules.TvOnAt && string.IsNullOrEmpty(beats[IntroRules.OffBeat].line)
                && beats[IntroRules.TvOnBeat].shot == IntroShot.Wide && beats[IntroRules.TvOnBeat].start == IntroRules.TvOnAt && string.IsNullOrEmpty(beats[IntroRules.TvOnBeat].line) && beats[IntroRules.TvOnBeat].Length > IntroRules.TvStaticSeconds + 1f);
            bool bossLines = true;
            for (int i = IntroRules.FirstBossBeat; i <= IntroRules.LastBossBeat; i++) bossLines &= beats[i].speaker == Speaker.Boss && !string.IsNullOrEmpty(beats[i].line) && beats[i].shot != IntroShot.None && beats[i].shot != IntroShot.Off;
            Check("intro: the TV shows the boss's monologue (six lines, each said by the boss) until 30.2 s, then static",
                bossLines && beats[IntroRules.LastBossBeat].end == IntroRules.ProgramEnd && beats[IntroRules.StaticBeat].shot == IntroShot.None && beats[IntroRules.WorkerBeat].shot == IntroShot.None && beats[last].shot == IntroShot.None && IntroRules.ProgramEnd == 30.2f
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
'@
Rep 'Check("intro: BeatAt finds the beat and says -1 outside the Intro", IntroRules.BeatAt(0f) == 0 && IntroRules.BeatAt(2.0f) == 1 && IntroRules.BeatAt(37.79f) == beats.Length - 1' 'Check("intro: BeatAt finds the beat and says -1 outside the Intro", IntroRules.BeatAt(0f) == 0 && IntroRules.BeatAt(3.2f) == 1 && IntroRules.BeatAt(5.2f) == 2 && IntroRules.BeatAt(55.39f) == beats.Length - 1'
Rep '&& IntroRules.BeatAt(-1f) == -1 && IntroRules.BeatAt(IntroRules.Total) == -1 && IntroRules.BeatAt(28.9f) == 8);' '&& IntroRules.BeatAt(-1f) == -1 && IntroRules.BeatAt(IntroRules.Total) == -1 && IntroRules.BeatAt(32.1f) == 9);'
RepRegion 'Check("intro: the workers look as they are for the first two lines, crushed when the bonuses go, angry for the last line"' '&& IntroRules.FaceMood(7) == 1 && IntroRules.FaceMood(8) == 2 && IntroRules.FaceMood(9) == 2);' @'
float lineStart = beats[IntroRules.BonusBeat].start, lineSaid = lineStart + IntroRules.TypedSeconds(beats[IntroRules.BonusBeat].line);
            Check("intro: the workers hang out until the TV comes on, are as they are while the boss is friendly and while the bonus line is SAID (their heads do not drop at its first word), and drop their heads a moment after it has been heard (the owner, 2026-10-20), then are angry from the workers' line",
                IntroRules.MoodAt(0f) == -1 && IntroRules.MoodAt(IntroRules.TvOnAt - 0.01f) == -1 && IntroRules.MoodAt(IntroRules.TvOnAt + 0.01f) == 0 && IntroRules.MoodAt(lineStart + 0.1f) == 0 && IntroRules.MoodAt(lineStart + IntroRules.TypedSeconds(beats[IntroRules.BonusBeat].line) * 0.5f) == 0
                && IntroRules.MoodAt(lineSaid) == 0 && IntroRules.MoodAt(IntroRules.CrushedAt + 0.01f) == 1 && IntroRules.CrushedAt > lineSaid && IntroRules.CrushedAt < beats[IntroRules.BonusBeat].end && IntroRules.MoodAt(IntroRules.ProgramEnd - 0.1f) == 1
                && IntroRules.MoodAt(beats[IntroRules.WorkerBeat].start + 0.1f) == 2 && IntroRules.MoodAt(beats[beats.Length - 1].start) == 2, $"(the line starts at {lineStart:0.0} s and is said by {lineSaid:0.0} s, the heads drop at {IntroRules.CrushedAt:0.00} s)");
'@
Save-Src

# ---------------------------------------------------------------- the tests: the round 12 intro check
Open-Src "$E\NetDevRound12.cs"
RepRegion '// ---- the intro: the vendor''s bay opens after the workers'' line, in the last beat, a roller door''s time before it ends' 'IntroRules.Total == beats[last].end);' @'
// ---- the intro: the vendor's bay opens after the workers' line and the vendor says his four lines (the owner, 2026-10-20)
            var beats = IntroRules.Beats; int last = beats.Length - 1;
            Check("intro: the vendor's bay door starts to roll up after the workers' line has ended and is all the way up before he speaks, and his four lines end the Intro",
                beats[IntroRules.DoorBeat].dock == DockShot.Bay && IntroRules.BayOpensAt >= beats[IntroRules.WorkerBeat].end && IntroRules.BayOpensAt + DockBayDoor.RollSeconds < beats[IntroRules.FirstVendorBeat].start && beats[last].speaker == Speaker.Vendor && IntroRules.Total == beats[last].end);
'@
RepRx '(?m)^[ \t]*cam\.transform\.position = new Vector3\(4\.75f, 11\.0f, 19\.0f\);\r?\n[ \t]*cam\.transform\.LookAt\(new Vector3\(4\.75f, 0f, 18\.5f\)\);\r?\n[ \t]*ShotCam\(cam, "round12_pvp_from_above"\);\r?\n' ''
Save-Src

# ---------------------------------------------------------------- the tests: the Etr scenario (the cutscene in a real Editor window)
Open-Src "$E\NetDevEtr.cs"
Rep @'
            EtrCheck("tv: two workers stand in the docks - the player's character and a coworker, who is the one that speaks", intro.WorkerCount == 2 && intro.WorkerLabel(intro.SpeakerAt) == IntroContent.CoworkerLabel && intro.WorkerLabel(intro.ListenerAt) == IntroContent.PlayerLabel(0));
'@ @'
            EtrCheck("tv: five workers hang out in the break room - the player's character and four coworkers, one of whom is the one that speaks to the player", intro.WorkerCount == IntroRules.Workers && intro.WorkerLabel(intro.SpeakerAt) == IntroContent.CoworkerLabel && intro.WorkerLabel(intro.ListenerAt) == IntroContent.PlayerLabel(0));
            // the opening (the owner, 2026-10-20): the TV is dark and they hang out, some turned to each other, not to the wall
            intro.TestSeek(1.8f);
            yield return new WaitForSeconds(0.5f);
            int notFacing = 0;
            for (int i = 0; i < intro.WorkerCount; i++) if (Mathf.Abs(Mathf.DeltaAngle(intro.WorkerYaw(i), 180f)) > 25f) notFacing++;
            EtrCheck("tv: before the announcement the TV is dark and the workers hang out: most of them are turned away from the wall, to each other", !tv.OnProgram && notFacing >= 3, $"({notFacing} of {intro.WorkerCount} not facing the TV)");
            intro.TestSeek(IntroRules.Beats[IntroRules.FirstBossBeat].start + 0.5f);
            yield return new WaitForSeconds(1.6f);
'@
Rep 'tvProgram &= beats[i].shot == IntroShot.None ? !tv.OnProgram : tv.OnProgram;' 'tvProgram &= beats[i].shot == IntroShot.None || beats[i].shot == IntroShot.Off ? !tv.OnProgram : tv.OnProgram;'
Rep 'if (beats[i].dock == DockShot.Bay) at = beats[i].start + 0.2f;' 'if (i == IntroRules.DoorBeat) at = beats[i].start + 0.2f;'
Rep 'if (beats[i].dock == DockShot.Bay)
                {
                    yield return new WaitForSeconds(IntroRules.BayOpensAt - at + DockBayDoor.RollSeconds + 0.3f);' 'if (beats[i].dock == DockShot.Bay)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, IntroRules.BayOpensAt - at + DockBayDoor.RollSeconds + 0.3f));'
Rep @'
            intro.TestSeek(31.5f);
            yield return new WaitForSeconds(0.8f);
'@ @'
            intro.TestSeek(IntroRules.Beats[IntroRules.WorkerBeat].start + 1.5f);
            yield return new WaitForSeconds(1.2f);
'@
Rep 'EtrCheck("tv: the TV shows the boss during the monologue and static after it", tvProgram);' 'EtrCheck("tv: the TV is dark at first, shows the boss during the monologue and static after it", tvProgram);'
Save-Src

# ---------------------------------------------------------------- the tests: the built-player autopilot and the intro play scenario
Open-Src "$A\QaAutopilot.cs"
Rep 'Check("the cutscene is in the docks: a TV on the wall, the player''s character and a coworker watching it", IntroCinematic.Current != null && IntroCinematic.Current.HasTv && IntroCinematic.Current.WorkerCount == 2 && IntroCinematic.Current.WorkerIsToon(0) && IntroCinematic.Current.WorkerIsToon(1), $"(Tiny Toon people: {(IntroCinematic.Current != null ? IntroCinematic.Current.WorkerCount : -1)} workers)");
            yield return Until(() => IntroCinematic.Clock >= 3.8f, 20f, "the first line");
            yield return Shot("i1_boss_first_line");' 'Check("the cutscene is in the docks: a TV on the wall and five workers (the player''s character and coworkers) hanging out", IntroCinematic.Current != null && IntroCinematic.Current.HasTv && IntroCinematic.Current.WorkerCount == IntroRules.Workers && IntroCinematic.Current.WorkerIsToon(0) && IntroCinematic.Current.WorkerIsToon(1), $"(Tiny Toon people: {(IntroCinematic.Current != null ? IntroCinematic.Current.WorkerCount : -1)} workers)");
            yield return Until(() => IntroCinematic.Clock >= 1.8f, 20f, "the workers hanging out");
            yield return Shot("i0_workers_hanging_out");
            yield return Until(() => IntroCinematic.Clock >= IntroRules.Beats[IntroRules.FirstBossBeat].start + 0.6f, 20f, "the first line");
            yield return Shot("i1_boss_first_line");'
Rep 'yield return Until(() => IntroCinematic.Clock >= 12.8f, 30f, "the desk shot");' 'yield return Until(() => IntroCinematic.Clock >= IntroRules.Beats[IntroRules.BonusBeat].start + 1.0f, 30f, "the desk shot");'
Rep 'yield return Until(() => IntroCinematic.Clock >= 20.8f, 30f, "the jet");
                yield return Shot("i3_window_jet");
                yield return Until(() => IntroCinematic.Clock >= 25.5f, 30f, "the last line of the boss");
                yield return Shot("i4_boss_last_line");
                yield return Until(() => IntroCinematic.Clock >= 31.0f, 30f, "the workers'' line");
                yield return Shot("i4b_worker_line");
                yield return Until(() => !IntroCinematic.Playing, 20f, "the Intro to end by itself");
                Check("the Intro ends by itself after its 37.8 s and was shown once", !IntroCinematic.Playing && IntroCinematic.TimesPlayed == 1);' 'yield return Until(() => IntroCinematic.Clock >= IntroRules.JetStart + 1.0f, 30f, "the jet");
                yield return Shot("i3_window_jet");
                yield return Until(() => IntroCinematic.Clock >= IntroRules.Beats[IntroRules.LastBossBeat].start + 1.5f, 30f, "the last line of the boss");
                yield return Shot("i4_boss_last_line");
                yield return Until(() => IntroCinematic.Clock >= IntroRules.Beats[IntroRules.WorkerBeat].start + 1.0f, 30f, "the workers'' line");
                yield return Shot("i4b_worker_line");
                yield return Until(() => IntroCinematic.Clock >= IntroRules.Beats[IntroRules.FirstVendorBeat + 2].start + 1.0f, 40f, "the vendor''s third line");
                yield return Shot("i4c_vendor_line");
                yield return Until(() => !IntroCinematic.Playing, 40f, "the Intro to end by itself");
                Check($"the Intro ends by itself after its {IntroRules.Total:0.#} s and was shown once", !IntroCinematic.Playing && IntroCinematic.TimesPlayed == 1);'
Rep 'Check("the cutscene is in the docks: a TV on the wall, the player''s character and a coworker watching it", IntroCinematic.Current != null && IntroCinematic.Current.HasTv && IntroCinematic.Current.WorkerCount == 2 && IntroCinematic.Current.WorkerIsToon(0) && IntroCinematic.Current.WorkerIsToon(1), $"(Tiny Toon people: {(IntroCinematic.Current != null ? IntroCinematic.Current.WorkerCount : -1)} workers)");
            yield return Until(() => IntroCinematic.Clock >= 3.8f, 20f, "the first line");
            yield return Shot("io1_boss_first_line");' 'Check("the cutscene is in the docks: a TV on the wall and five workers (the player''s character and coworkers) hanging out", IntroCinematic.Current != null && IntroCinematic.Current.HasTv && IntroCinematic.Current.WorkerCount == IntroRules.Workers && IntroCinematic.Current.WorkerIsToon(0) && IntroCinematic.Current.WorkerIsToon(1), $"(Tiny Toon people: {(IntroCinematic.Current != null ? IntroCinematic.Current.WorkerCount : -1)} workers)");
            yield return Until(() => IntroCinematic.Clock >= IntroRules.Beats[IntroRules.FirstBossBeat].start + 0.6f, 20f, "the first line");
            yield return Shot("io1_boss_first_line");'
Rep 'yield return Until(() => IntroCinematic.Clock >= 20.8f, 30f, "the jet");
                yield return Shot("io2_window_jet");
                yield return Until(() => IntroCinematic.Clock >= 31.0f, 30f, "the workers'' line");
                yield return Shot("io2b_worker_line");
                yield return Until(() => !IntroCinematic.Playing, 30f, "the Intro to end by itself");
                Check("the Intro ends by itself after its 37.8 s and was shown once", !IntroCinematic.Playing && IntroCinematic.TimesPlayed == 1);' 'yield return Until(() => IntroCinematic.Clock >= IntroRules.JetStart + 1.0f, 30f, "the jet");
                yield return Shot("io2_window_jet");
                yield return Until(() => IntroCinematic.Clock >= IntroRules.Beats[IntroRules.WorkerBeat].start + 1.0f, 30f, "the workers'' line");
                yield return Shot("io2b_worker_line");
                yield return Until(() => !IntroCinematic.Playing, 45f, "the Intro to end by itself");
                Check($"the Intro ends by itself after its {IntroRules.Total:0.#} s and was shown once", !IntroCinematic.Playing && IntroCinematic.TimesPlayed == 1);'
Save-Src

Open-Src "$E\NetDevIntroPlay.cs"
Rep 'IntroCinematic.Clock >= 3.8f' 'IntroCinematic.Clock >= IntroRules.Beats[IntroRules.FirstBossBeat].start + 0.6f'
Save-Src

# ---------------------------------------------------------------- the words in the READMEs
Open-Src 'C:\bqs\README_SEND.txt'
Rep 'you and a coworker watch the boss on the TV in the break room.' 'you and your coworkers watch the boss on the TV in the break room, and then the vendor shows up.'
Save-Src
Write-Output 'round 12 batch G41 patched'
