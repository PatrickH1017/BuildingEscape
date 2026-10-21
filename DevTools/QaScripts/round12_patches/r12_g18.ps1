# Round 12, batch G18 (the owner, 2026-10-19 night): (1) "a little scoreboard on the wall above the dartboard that shows what score the player gets for each of their 3 darts thrown and everyone should see the results",
# (2) "a line on the ground that looks like tape showing where the player throwing the dart stands from (like it would be in the real world)", (3) "When a player stands at the line it should say throw darts and if they click E it should
# put them into the throwing screen". New files: DartScoreboard.cs, RunSync.Darts.cs (copied by r12_g18_files.ps1). BE_qa (= C:\bqm) only.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

# ---------------------------------------------------------------- the rules: the throw line and what the scoreboard says
Open-Src "$A\Darts\DartsRules.cs"
Rep @'
                if (!Won && DartsLeft <= 0) { DartsLeft = DartsPerTurn; Turn++; turnStart = Remaining; }
            }
        }
    }
}
'@ @'
                if (!Won && DartsLeft <= 0) { DartsLeft = DartsPerTurn; Turn++; turnStart = Remaining; }
            }
        }

        // ------------------------------------------------------------------ the throw line (the oche) and the scoreboard

        /// <summary>The regulation distance from the board's face to the throw line (the oche): 7 ft 9.25 in = 2.37 m (the owner, 2026-10-19 night: a line on the ground that looks like tape, where the player stands to throw).</summary>
        public const float ThrowDistance = 2.37f;
        /// <summary>The spot where "throw darts" is offered: this far past the line towards the board, this far behind it, and this far to each side of the line's middle (metres).</summary>
        public const float LineZoneAhead = 0.5f, LineZoneBehind = 1.3f, LineZoneHalfWidth = 0.9f;

        /// <summary>Is somebody standing at the line? <paramref name="behind"/> is how far he is behind the line (away from the board; negative past it), <paramref name="across"/> how far to the side of its middle.</summary>
        public static bool AtThrowLine(float behind, float across) => behind >= -LineZoneAhead && behind <= LineZoneBehind && Math.Abs(across) <= LineZoneHalfWidth;

        /// <summary>
        /// What the little scoreboard over the dart board shows (the owner, 2026-10-19 night: "a little scoreboard on the wall above the dartboard that shows what score the player gets for each of their 3 darts thrown and everyone
        /// should see the results"): who threw, the score of each of the three darts of the turn (a dash for one still to come) and, in 301, what is left (or BUST, or WON), in Cricket the points. It only keeps what it was told and
        /// turns it into three lines of text in <see cref="DotText"/>'s alphabet. The thrower's machine tells it at once, the host tells everybody else (<c>RunSync.RequestDartScore</c>). The words are placeholders: the owner writes the text.
        /// </summary>
        public sealed class Scoreboard
        {
            public const string Title = "DARTS", PlayerWord = "PLAYER", LeftWord = "LEFT", PointsWord = "PTS", BustWord = "BUST", WonWord = "WON";
            /// <summary>The thrower (0-based; the board says PLAYER slot + 1), or -1 while nobody has thrown.</summary>
            public int Slot { get; private set; } = -1;
            /// <summary>The score of each of the three darts of the turn, -1 for one that has not been thrown.</summary>
            public readonly int[] Scores = { -1, -1, -1 };
            public int Turn { get; private set; }
            public int Remaining { get; private set; } = Match.Start;
            public int Points { get; private set; }
            public bool Bust { get; private set; }
            public bool Won { get; private set; }
            public Mode GameMode { get; private set; }

            /// <summary>A dart landed. The first dart of a turn (or a dart of another thrower or turn) wipes the old darts. False when the numbers make no sense (a dart scores 0 to 60, three darts a turn).</summary>
            public bool Apply(int slot, int dartNo, int score, int turn, int remaining, int points, bool bust, bool won, Mode mode)
            {
                if (slot < 0 || dartNo < 0 || dartNo >= Match.DartsPerTurn || score < 0 || score > 60 || remaining < 0 || remaining > Match.Start || points < 0) return false;
                if (dartNo == 0 || slot != Slot || turn != Turn) for (int i = 0; i < Scores.Length; i++) Scores[i] = -1;
                Slot = slot; Turn = turn; Scores[dartNo] = score;
                Remaining = remaining; Points = points; Bust = bust; Won = won; GameMode = mode;
                return true;
            }

            /// <summary>The three lines of the board, each in the alphabet of <see cref="DotText"/> (letters, digits, dashes and spaces).</summary>
            public string[] Lines()
            {
                string who = Slot < 0 ? Title : PlayerWord + " " + (Slot + 1);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < Scores.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(Scores[i] < 0 ? "-" : Scores[i].ToString()); }
                string foot = Slot < 0 ? "" : Won ? WonWord : Bust ? BustWord : GameMode == Mode.Game301 ? LeftWord + " " + Remaining : PointsWord + " " + Points;
                return new[] { who, sb.ToString(), foot };
            }
        }
    }
}
'@
Save-Src

# ---------------------------------------------------------------- the board: the tape, the prompt at the line, the scoreboard, the announcements
Open-Src "$A\Darts\DartBoard.cs"
Rep 'PromptPlay = "[E] Darts"' 'PromptPlay = "[E] Throw darts"'
Rep 'Purely local and cosmetic: nothing is sent, so a team mate does not see the darts. The rules are <see cref="DartsRules"/>.' 'The darts themselves are local (a team mate does not see them fly), but every dart''s score goes on the little scoreboard over the board, which everybody sees (<see cref="DartScoreboard"/>, <c>RunSync.RequestDartScore</c>). A strip of tape on the floor marks the throw line, 2.37 m from the board; standing at it "[E] Throw darts" brings up the game''s screen. The rules are <see cref="DartsRules"/>.'
Rep @'
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; }
'@ @'
        /// <summary>The dart board of the docks (there is one), whether or not a game is on: the network tells it the scores of everybody's darts.</summary>
        public static DartBoard Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Instance = null; }
'@
Rep @'
        Transform holder;
'@ @'
        Transform holder;
        // the throw line, the E prompt at it and the little scoreboard on the wall (the owner, 2026-10-19 night)
        Vector3 lineCentre, flatNormal;
        Transform throwLine;
        DartScoreboard scoreboard;
        readonly DartsRules.Scoreboard scores = new DartsRules.Scoreboard();
        bool atLine;
        int lastToggleFrame = -1;
#if UNITY_EDITOR
        [System.NonSerialized] public bool testPressE;          // (a test presses E here: the Input System cannot be driven from a test)
#endif
        /// <summary>The tape's size (m): across the way to the board and along it. The scoreboard hangs this high above the board's middle and this far off the wall.</summary>
        public const float TapeLength = 1.0f, TapeWidth = 0.06f, ScoreboardHeight = 0.95f, ScoreboardOut = 0.03f;
'@
Rep @'
        public float Held => held;
'@ @'
        public float Held => held;
        public DartsRules.Scoreboard Scores => scores;
        public DartScoreboard ScoreboardView => scoreboard;
        public Transform ThrowLine => throwLine;
        public Vector3 LineCentre => lineCentre;
        public bool AtLine => atLine;
'@
Rep @'
            holder = new GameObject("Darts").transform;
            holder.SetParent(transform, true);
        }
'@ @'
            holder = new GameObject("Darts").transform;
            holder.SetParent(transform, true);
            BuildThrowLine();
            BuildScoreboard();
        }

        /// <summary>A strip of tape on the floor across the way to the board, 2.37 m from its face: where a player stands to throw (the owner, 2026-10-19 night: "a line on the ground that looks like tape ... like it would be in the real world").</summary>
        void BuildThrowLine()
        {
            flatNormal = new Vector3(normal.x, 0f, normal.z).normalized;
            lineCentre = new Vector3(faceCentre.x, 0f, faceCentre.z) + flatNormal * DartsRules.ThrowDistance;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DartThrowLine";
            var col = go.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
            var tr = go.transform;
            tr.SetParent(transform, true);
            tr.SetPositionAndRotation(lineCentre + Vector3.up * 0.007f, Quaternion.LookRotation(flatNormal, Vector3.up));       // (its z runs along the way to the board, its x across it)
            var ls = transform.lossyScale;
            tr.localScale = new Vector3(TapeLength / Mathf.Max(0.01f, ls.x), 0.014f / Mathf.Max(0.01f, ls.y), TapeWidth / Mathf.Max(0.01f, ls.z));
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(new Color(0.94f, 0.92f, 0.82f), 0.25f);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            throwLine = tr;
        }

        /// <summary>The little scoreboard on the wall above the board (the owner, 2026-10-19 night).</summary>
        void BuildScoreboard()
        {
            var centre = transform.TransformPoint(new Vector3(0f, ScoreboardHeight, ScoreboardOut));
            scoreboard = DartScoreboard.Build(transform, centre, normal);
            scoreboard.Show(scores.Lines(), scores.Bust, scores.Won);
        }
'@
Rep @'
        protected override void OnDisable()
        {
            base.OnDisable();
            if (Current == this) { Current = null; OutpostUi.Hide(OutpostUi.Kind.Darts); }
        }
'@ @'
        protected override void OnEnable() { base.OnEnable(); Instance = this; }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Instance == this) Instance = null;
            if (Current == this) { Current = null; OutpostUi.Hide(OutpostUi.Kind.Darts); }
        }
'@
Rep @'
        public override void Use(WobblyPlayer who)
        {
            if (state == State.Playing) { Leave(); return; }
'@ @'
        public override void Use(WobblyPlayer who)
        {
            lastToggleFrame = Time.frameCount;
            if (state == State.Playing) { Leave(); return; }
'@
Rep @'
        public void Leave()
        {
            state = State.Idle;
'@ @'
        public void Leave()
        {
            lastToggleFrame = Time.frameCount;
            state = State.Idle;
'@
Rep @'
            if (state == State.Playing) UpdatePlaying();
            StepDarts(Time.deltaTime);
        }
'@ @'
            if (state == State.Playing) UpdatePlaying();
            UpdateLine();
            StepDarts(Time.deltaTime);
        }

        /// <summary>
        /// Standing at the throw line (<see cref="DartsRules.AtThrowLine"/>) with no game on, "[E] Throw darts" shows and E brings up the game's screen (the owner, 2026-10-19 night: "When a player stands at the line it should say
        /// throw darts and if they click E it should put them into the throwing screen"). E on the board itself (the crosshair on it) works as before; the two never act in the same frame.
        /// </summary>
        void UpdateLine()
        {
            atLine = false;
            if (state != State.Idle || !isActiveAndEnabled) return;
            var body = Body();
            if (!body) return;
            var flat = body.position - lineCentre; flat.y = 0f;
            if (!DartsRules.AtThrowLine(Vector3.Dot(flat, flatNormal), Vector3.Dot(flat, viewerRight))) return;
            if (EscapeMenu.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || TestMode.PanelOpen || OutpostUi.Open) return;
            body.TryGetComponent(out WobblyPlayer wob);
            if (wob && wob.InCustody) return;
            atLine = true;
            bool e = Input.GetKeyDown(KeyCode.E);
#if UNITY_EDITOR
            if (testPressE) { e = true; testPressE = false; }
#endif
            if (e && lastToggleFrame != Time.frameCount) Use(wob);
        }

        // ------------------------------------------------------------------ the scoreboard on the wall

        static int ThrowerSlot() => Net.NetRole.Synced && Net.NetPlayer.Local != null ? Net.NetPlayer.Local.Slot : 0;

        /// <summary>
        /// A dart landed (this machine's thrower: at once; anybody else's: the host says so, see <c>RunSync.DartScoreRpc</c>): the scoreboard on the wall shows it. <paramref name="dartNo"/> is which of the three darts of the turn it
        /// was (0, 1 or 2).
        /// </summary>
        public void ShowScore(int slot, int dartNo, int score, int turn, int remaining, int points, bool bust, bool won, DartsRules.Mode mode)
        {
            if (!scores.Apply(slot, dartNo, score, turn, remaining, points, bust, won, mode)) return;
            if (scoreboard) scoreboard.Show(scores.Lines(), scores.Bust, scores.Won);
        }
'@
Rep @'
            LastHit = hit;
            if (match == null || state != State.Playing) return;
            match.Throw(hit);
            lastText = match.LastBust ? Bust : TextOf(hit);
            lastTextAt = Time.time;
        }
'@ @'
            LastHit = hit;
            if (match == null || state != State.Playing) return;
            int dartNo = DartsRules.Match.DartsPerTurn - match.DartsLeft, turn = match.Turn;       // (before the dart is counted: which of the three it is)
            match.Throw(hit);
            lastText = match.LastBust ? Bust : TextOf(hit);
            lastTextAt = Time.time;
            // the scoreboard on the wall: here at once, and the host tells the others (the owner, 2026-10-19 night: "everyone should see the results")
            ShowScore(ThrowerSlot(), dartNo, hit.Score, turn, match.Remaining, match.Points, match.LastBust, match.Won, match.mode);
            if (Net.NetRole.Synced && Net.RunSync.Instance != null)
                Net.RunSync.Instance.RequestDartScore(dartNo, hit.Score, turn, match.Remaining, match.Points, match.LastBust, match.Won, match.mode == DartsRules.Mode.Cricket);
        }
'@
Rep @'
            if (state == State.Choosing) DrawChooser();
            else if (state == State.Playing && Event.current.type == EventType.Repaint) DrawHud();
'@ @'
            if (state == State.Choosing) DrawChooser();
            else if (state == State.Playing && Event.current.type == EventType.Repaint) DrawHud();
            else if (atLine && state == State.Idle && Event.current.type == EventType.Repaint)
            {
                var aim = Crosshair.Aim;
                if (!(aim.Kind == AimKind.Interact && aim.Interactable == this)) HudWidgets.Prompt(PromptPlay, Screen.height * 0.62f);          // (aiming at the board, the crosshair shows the same words)
            }
'@
Save-Src

# ---------------------------------------------------------------- the protocol: new messages
Open-Src "$A\Net\NetConst.cs"
Rep 'ProtocolVersion = "23";   // 23: armour' 'ProtocolVersion = "24";   // 24: the dart board''s scoreboard (the thrower''s machine tells the host the score of each dart, the host tells everybody); 23: armour'
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src "$E\NetDevRound12.cs"
Rep @'
            // the throw: how long the button is held, the speed, the drop-off, the spread
'@ @'
            // ---- the scoreboard over the board and the throw line: what the wall says after each dart (the owner, 2026-10-19 night)
            var sbd = new DartsRules.Scoreboard();
            var blankLines = sbd.Lines();
            sbd.Apply(1, 0, 60, 1, 241, 0, false, false, DartsRules.Mode.Game301); var oneLines = sbd.Lines();
            sbd.Apply(1, 1, 19, 1, 222, 0, false, false, DartsRules.Mode.Game301); sbd.Apply(1, 2, 50, 1, 172, 0, false, false, DartsRules.Mode.Game301); var threeLines = sbd.Lines();
            sbd.Apply(1, 0, 0, 2, 172, 0, false, false, DartsRules.Mode.Game301); var nextTurnLines = sbd.Lines();
            sbd.Apply(2, 0, 20, 1, 281, 0, false, false, DartsRules.Mode.Game301); var otherLines = sbd.Lines();
            sbd.Apply(2, 1, 60, 1, 281, 0, true, false, DartsRules.Mode.Game301); var bustLines = sbd.Lines();
            sbd.Apply(2, 2, 25, 1, 0, 0, false, true, DartsRules.Mode.Game301); var wonLines = sbd.Lines();
            var cricketBoard = new DartsRules.Scoreboard(); cricketBoard.Apply(0, 0, 60, 1, 301, 40, false, false, DartsRules.Mode.Cricket); var cricketLines = cricketBoard.Lines();
            var badBoard = new DartsRules.Scoreboard();
            bool refusedNonsense = !badBoard.Apply(0, 3, 10, 1, 301, 0, false, false, DartsRules.Mode.Game301) && !badBoard.Apply(0, 0, 61, 1, 301, 0, false, false, DartsRules.Mode.Game301) && !badBoard.Apply(-1, 0, 10, 1, 301, 0, false, false, DartsRules.Mode.Game301) && badBoard.Slot < 0;
            bool drawable = true;
            foreach (var lineSet in new[] { blankLines, oneLines, threeLines, nextTurnLines, otherLines, bustLines, wonLines, cricketLines }) foreach (var line in lineSet) foreach (char ch in line) drawable &= DotText.Has(ch);
            Check("darts scoreboard: it starts blank (DARTS, three dashes), each dart shows its score in its place (a dash for the ones to come) with the thrower's name and what is left, a new turn or another player wipes the old darts",
                blankLines[0] == "DARTS" && blankLines[1] == "- - -" && blankLines[2] == "" && oneLines[0] == "PLAYER 2" && oneLines[1] == "60 - -" && oneLines[2] == "LEFT 241" && threeLines[1] == "60 19 50" && threeLines[2] == "LEFT 172"
                && nextTurnLines[1] == "0 - -" && otherLines[0] == "PLAYER 3" && otherLines[1] == "20 - -" && otherLines[2] == "LEFT 281",
                $"({string.Join(" | ", blankLines)} / {string.Join(" | ", oneLines)} / {string.Join(" | ", threeLines)} / {string.Join(" | ", nextTurnLines)} / {string.Join(" | ", otherLines)})");
            Check("darts scoreboard: a bust says BUST, a win says WON, Cricket shows the points, nonsense (a fourth dart, 61 points, nobody) is refused, and every letter on it can be drawn in dots",
                bustLines[2] == "BUST" && wonLines[2] == "WON" && cricketLines[2] == "PTS 40" && refusedNonsense && drawable,
                $"({string.Join(" | ", bustLines)} / {string.Join(" | ", wonLines)} / {string.Join(" | ", cricketLines)}, refused {refusedNonsense}, drawable {drawable})");
            Check("darts: the throw line is the regulation 2.37 m (7 ft 9 in) from the board; the throw-darts spot is round it: on the line, a step behind it and a step past it are in, too far back or too far to the side are out",
                Mathf.Approximately(DartsRules.ThrowDistance, 2.37f) && DartsRules.AtThrowLine(0f, 0f) && DartsRules.AtThrowLine(1.0f, 0f) && DartsRules.AtThrowLine(-0.4f, 0.5f) && !DartsRules.AtThrowLine(-0.7f, 0f) && !DartsRules.AtThrowLine(1.5f, 0f) && !DartsRules.AtThrowLine(0.5f, 1.0f));
            // the throw: how long the button is held, the speed, the drop-off, the spread
'@
Rep @'
                var first = board.StuckDarts[0];
'@ @'
                var first = board.StuckDarts[0];
                int hardScore = board.LastHit.Score;
'@
Rep @'
                if (gfx)
                {
                    var cam = new GameObject("R12Cam").AddComponent<Camera>();
'@ @'
                // the scoreboard on the wall (the owner, 2026-10-19 night: "a little scoreboard on the wall above the dartboard that shows what score the player gets for each of their 3 darts thrown and everyone should see the results")
                int softScore = board.LastHit.Score;
                var sbv = board.ScoreboardView;
                R12Check("a little scoreboard hangs on the wall above the dart board, facing the room, about 2.6 m up",
                    sbv != null && sbv.transform.position.y > 2.3f && sbv.transform.position.y < 2.95f && Mathf.Abs(sbv.transform.position.z - board.FaceCentre.z) < 0.1f && Vector3.Dot(sbv.transform.forward, board.Normal) < -0.99f && Mathf.Abs(sbv.transform.position.x - board.FaceCentre.x) < 0.2f,
                    $"(at {(sbv ? sbv.transform.position.ToString("0.00") : "none")})");
                R12Check("after two darts it says PLAYER 1, the score of each of them (the third a dash) and what is left",
                    board.Scores.Slot == 0 && sbv != null && sbv.Lines[0] == "PLAYER 1" && sbv.Lines[1] == hardScore + " " + softScore + " -" && sbv.Lines[2] == "LEFT " + board.Match.Remaining && sbv.LitRuns > 20,
                    $"({(sbv ? string.Join(" | ", sbv.Lines) : "none")}, hard {hardScore}, soft {softScore}, left {board.Match.Remaining})");
                board.Launch(from, aimAbove, 1f, false);
                yield return new WaitForSeconds(1.0f);
                int thirdScore = board.LastHit.Score;
                R12Check("the third dart completes the turn: all three scores are on the wall, and the next dart starts a new turn",
                    sbv != null && sbv.Lines[1] == hardScore + " " + softScore + " " + thirdScore && board.Scores.Scores[2] == thirdScore && board.Match.Turn == 2 && board.Match.DartsLeft == 3,
                    $"({(sbv ? string.Join(" | ", sbv.Lines) : "none")}, turn {board.Match.Turn}, darts left {board.Match.DartsLeft})");

                // the throw line: tape on the floor 2.37 m from the board (7 ft 9 in), across the way to it
                var tape = board.ThrowLine;
                var tapeRend = tape ? tape.GetComponent<Renderer>() : null;
                float fromBoard = tape ? Vector2.Distance(new Vector2(tape.position.x, tape.position.z), new Vector2(board.FaceCentre.x, board.FaceCentre.z)) : -1f;
                R12Check("a strip of tape lies on the floor 2.37 m in front of the board, across the way to it: thin, about a metre long and flat",
                    tapeRend != null && Mathf.Abs(fromBoard - DartsRules.ThrowDistance) < 0.03f && tape.position.y < 0.05f && Vector3.Dot(tape.position - board.FaceCentre, board.Normal) > 2.3f
                    && Mathf.Min(tapeRend.bounds.size.x, tapeRend.bounds.size.z) < 0.1f && Mathf.Max(tapeRend.bounds.size.x, tapeRend.bounds.size.z) > 0.8f && tapeRend.bounds.size.y < 0.05f,
                    $"({fromBoard:0.00} m from the board, size {(tapeRend ? tapeRend.bounds.size.ToString("0.00") : "none")})");

                // standing at the line it says "throw darts", and E brings up the game's screen
                board.Leave();
                var onLine = board.LineCentre + board.Normal * 0.4f;
                player.TeleportTo(new Vector3(onLine.x, 1.1f, onLine.z), Quaternion.LookRotation(-board.Normal));
                yield return new WaitForSeconds(0.5f);
                R12Check("standing at the line the board offers a game: the prompt is \"[E] Throw darts\"", board.AtLine && board.Phase == DartBoard.State.Idle && board.Prompt == "[E] Throw darts", $"(at the line {board.AtLine}, phase {board.Phase}, prompt {board.Prompt})");
                board.testPressE = true;
                yield return null; yield return null;
                R12Check("E at the line brings up the game's screen (the chooser: 301 or Cricket)", board.Phase == DartBoard.State.Choosing && OutpostUi.Current == OutpostUi.Kind.Darts, $"(phase {board.Phase})");
                board.Begin(DartsRules.Mode.Game301);
                R12Check("...and a game can be started from it, with the player at the line", DartBoard.Playing);
                board.Leave();
                player.TeleportTo(new Vector3(onLine.x + board.Normal.x * 4f, 1.1f, onLine.z + board.Normal.z * 4f), Quaternion.LookRotation(-board.Normal));
                yield return new WaitForSeconds(0.4f);
                R12Check("four metres further back the prompt is gone", !board.AtLine);
                if (gfx)
                {
                    var cam = new GameObject("R12Cam").AddComponent<Camera>();
'@
Rep @'
                    ShotCam(cam, "round12_board");
'@ @'
                    ShotCam(cam, "round12_board");
                    cam.transform.position = board.FaceCentre + board.Normal * 3.4f + Vector3.up * 0.9f; cam.fieldOfView = 55f;
                    cam.transform.LookAt(board.FaceCentre + Vector3.up * 0.55f);
                    ShotCam(cam, "round12_scoreboard");
                    cam.transform.position = board.LineCentre + board.Normal * 1.3f + Vector3.up * 1.6f; cam.fieldOfView = 60f;
                    cam.transform.LookAt(board.FaceCentre);
                    ShotCam(cam, "round12_throw_line");
'@
Save-Src

# ---------------------------------------------------------------- the built-player network test: the scoreboard on both machines
Open-Src "$A\QaAutopilot.cs"
Rep @'
        IEnumerator RunHost()
        {
'@ @'
        /// <summary>
        /// The dart board's scoreboard over the network (the owner, 2026-10-19 night: "everyone should see the results"), in the docks, run by the host and the client at the same moment. The client goes to the board, starts a game
        /// and throws three darts: its own wall shows them at once, and the HOST's wall must show PLAYER 2 and the same three scores. Then the host throws three: the client's wall must show PLAYER 1 and its three scores.
        /// </summary>
        IEnumerator DartsNet(bool host)
        {
            string who = host ? "host" : "client";
            var board = DartBoard.Instance;
            var wob = NetPlayer.Local != null ? NetPlayer.Local.GetComponent<WobblyPlayer>() : null;
            var rb = wob != null ? wob.GetComponent<Rigidbody>() : null;
            Check($"{who}: the docks have a dart board with its scoreboard and my body is here", board != null && board.ScoreboardView != null && wob != null && rb != null);
            if (board == null || board.ScoreboardView == null || wob == null || rb == null) yield break;
            int mySlot = NetPlayer.Local.Slot;
            var spot = new Vector3(board.FaceCentre.x + board.Normal.x * 2.8f, 1.1f, board.FaceCentre.z + board.Normal.z * 2.8f);
            if (!host)
            {
                wob.TeleportTo(spot, Quaternion.LookRotation(-board.Normal));
                rb.linearVelocity = Vector3.zero;
                yield return Wait(3f);                                                  // (the host's copy of me has to arrive near the board: the host checks that the thrower is there)
                board.Begin(DartsRules.Mode.Game301);
                yield return ThrowThree(board);
                Check("client: my three darts are on the wall here: PLAYER " + (mySlot + 1) + " and a score for each", board.Scores.Slot == mySlot && board.Scores.Scores[0] >= 0 && board.Scores.Scores[2] >= 0 && board.ScoreboardView.Lines[0] == "PLAYER " + (mySlot + 1), $"({string.Join(" | ", board.ScoreboardView.Lines)})");
                board.Leave();
                yield return Until(() => board.Scores.Slot >= 0 && board.Scores.Slot != mySlot && board.Scores.Scores[2] >= 0, 120f, "the host's three darts on my scoreboard");
                Check("client: the host's darts reached my wall: his name and a score for each of the three", board.ScoreboardView.Lines[0] == "PLAYER " + (board.Scores.Slot + 1) && board.Scores.Scores[0] >= 0 && board.Scores.Scores[1] >= 0 && board.Scores.Scores[2] >= 0, $"({string.Join(" | ", board.ScoreboardView.Lines)})");
            }
            else
            {
                yield return Until(() => board.Scores.Slot >= 0 && board.Scores.Slot != mySlot && board.Scores.Scores[2] >= 0, 150f, "the client's three darts on the host's scoreboard");
                yield return Wait(0.5f);
                var l = board.ScoreboardView.Lines;
                Check("host: the client's darts reached the wall here: his name, a score for each of the three and what is left", l[0] == "PLAYER " + (board.Scores.Slot + 1) && board.Scores.Scores[0] >= 0 && board.Scores.Scores[1] >= 0 && board.Scores.Scores[2] >= 0  && (l[2].StartsWith("LEFT ") || l[2] == "BUST" || l[2] == "WON"), $"({string.Join(" | ", l)})");
                wob.TeleportTo(spot, Quaternion.LookRotation(-board.Normal));
                rb.linearVelocity = Vector3.zero;
                yield return Wait(3f);
                board.Begin(DartsRules.Mode.Game301);
                yield return ThrowThree(board);
                Check("host: my three darts are on the wall: PLAYER " + (mySlot + 1) + " and a score for each", board.Scores.Slot == mySlot && board.Scores.Scores[0] >= 0 && board.Scores.Scores[2] >= 0 && board.ScoreboardView.Lines[0] == "PLAYER " + (mySlot + 1), $"({string.Join(" | ", board.ScoreboardView.Lines)})");
                board.Leave();
                yield return Wait(4f);                                                  // (the client looks at it)
            }
        }

        IEnumerator ThrowThree(DartBoard board)
        {
            var from = board.FaceCentre + board.Normal * 2.8f + Vector3.down * 0.3f;
            var aim = board.FaceCentre + Vector3.up * DartsRules.Drop(DartsRules.Speed(1f), 2.8f);
            for (int i = 0; i < 3; i++) { board.Launch(from, aim, 1f, true); yield return Wait(1.2f); }
        }

        IEnumerator RunHost()
        {
'@
Rep 'yield return ArmourNet(true);' 'yield return ArmourNet(true);
            yield return DartsNet(true);'
Rep 'yield return ArmourNet(false);' 'yield return ArmourNet(false);
            yield return DartsNet(false);'
Save-Src
Write-Output 'round 12 batch G18 patched'
