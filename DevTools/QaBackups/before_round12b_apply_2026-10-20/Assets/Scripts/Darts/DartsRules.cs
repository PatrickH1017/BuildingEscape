using System;

namespace BuildingEscape
{
    /// <summary>
    /// The rules of the darts game of the Loading Docks (the owner, 2026-10-19 night: "the darts game should work as point and click but there is drop off and how long you hold is the power of your throw"; the modes are
    /// 301 and Cricket, the owner's queue Q17). Pure rules, no Unity types: where a dart lands on the board and what it scores, the throw's power and speed, and the two games.
    /// </summary>
    public static class DartsRules
    {
        // ------------------------------------------------------------------ the board (a regulation board's proportions, the radius of the double ring's outer edge = 1)

        public const float BullRadius = 0.0374f, OuterBullRadius = 0.0935f, TripleInner = 0.5824f, TripleOuter = 0.6294f, DoubleInner = 0.9529f;
        /// <summary>The numbers round the board, clockwise from the top (20 is straight up).</summary>
        public static readonly int[] Ring = { 20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5 };

        public enum Zone { Miss, Single, Double, Triple, OuterBull, Bull }

        public readonly struct Hit
        {
            public readonly Zone zone;
            public readonly int number;
            public Hit(Zone zone, int number) { this.zone = zone; this.number = number; }
            public bool OnBoard => zone != Zone.Miss;
            public int Score => zone switch
            {
                Zone.Single => number, Zone.Double => number * 2, Zone.Triple => number * 3, Zone.OuterBull => 25, Zone.Bull => 50, _ => 0,
            };
            /// <summary>How many marks it is worth in Cricket (a single 1, a double or the bull 2, a triple 3, the outer bull 1).</summary>
            public int Marks => zone switch { Zone.Single => 1, Zone.Double => 2, Zone.Triple => 3, Zone.OuterBull => 1, Zone.Bull => 2, _ => 0 };
        }

        /// <summary>
        /// What a dart that lands at (<paramref name="x"/>, <paramref name="y"/>) scores: the position on the board's face as the thrower sees it, in board radii (x to his right, y up).
        /// </summary>
        public static Hit Score(float x, float y)
        {
            float r = (float)Math.Sqrt(x * x + y * y);
            if (r > 1f) return new Hit(Zone.Miss, 0);
            if (r <= BullRadius) return new Hit(Zone.Bull, 25);
            if (r <= OuterBullRadius) return new Hit(Zone.OuterBull, 25);
            float angle = (float)(Math.Atan2(x, y) * 180.0 / Math.PI);            // 0 = up, clockwise
            if (angle < 0f) angle += 360f;
            int sector = (int)((angle + 9f) / 18f) % 20;
            int n = Ring[sector];
            var zone = r >= DoubleInner ? Zone.Double : (r >= TripleInner && r <= TripleOuter) ? Zone.Triple : Zone.Single;
            return new Hit(zone, n);
        }

        // ------------------------------------------------------------------ the throw

        /// <summary>Seconds the button is held for a full-power throw; a longer hold changes nothing.</summary>
        public const float MaxChargeSeconds = 1.2f;
        /// <summary>The dart's speed (m/s) at no power and at full power: it flies in an arc, so a weak throw falls short and low (the drop-off).</summary>
        public const float MinSpeed = 5.5f, MaxSpeed = 17f;
        public const float Gravity = 9.81f;
        /// <summary>The cone (degrees) the dart goes out in: a hard throw is a little wilder than a soft one.</summary>
        public const float MinSpread = 0.2f, MaxSpread = 1.4f;

        public static float Power(float heldSeconds) => Math.Max(0f, Math.Min(1f, heldSeconds / MaxChargeSeconds));
        public static float Speed(float power) => MinSpeed + (MaxSpeed - MinSpeed) * Math.Max(0f, Math.Min(1f, power));
        public static float Spread(float power) => MinSpread + (MaxSpread - MinSpread) * Math.Max(0f, Math.Min(1f, power));
        /// <summary>How far a dart thrown level at this speed has fallen after flying this far along (the drop-off).</summary>
        public static float Drop(float speed, float distance) { float t = distance / Math.Max(0.01f, speed); return 0.5f * Gravity * t * t; }

        // ------------------------------------------------------------------ the two games, one player

        public enum Mode { Game301, Cricket }

        /// <summary>The Cricket numbers, then the bull (25).</summary>
        public static readonly int[] CricketTargets = { 20, 19, 18, 17, 16, 15, 25 };

        /// <summary>
        /// A game: three darts a turn. 301: every dart takes its score off 301; a dart that would take it below nothing is a bust (the score goes back to what it was at the start of the turn and the turn is over);
        /// the dart that makes exactly nothing wins. Cricket: 20 to 15 and the bull each have to be hit three times (a double is two marks, a triple three); marks beyond the third score the number's points; the game is
        /// won when all seven are closed.
        /// </summary>
        public sealed class Match
        {
            public const int Start = 301, DartsPerTurn = 3;
            public readonly Mode mode;
            public int Remaining { get; private set; } = Start;
            public readonly int[] marks = new int[7];
            public int Points { get; private set; }
            public int DartsThrown { get; private set; }
            public int DartsLeft { get; private set; } = DartsPerTurn;
            public int Turn { get; private set; } = 1;
            public bool Won { get; private set; }
            public bool LastBust { get; private set; }
            int turnStart = Start;

            public Match(Mode mode) { this.mode = mode; }

            public bool Closed(int target) => marks[target] >= 3;
            public bool AllClosed { get { foreach (int m in marks) if (m < 3) return false; return true; } }

            public void Throw(Hit hit)
            {
                if (Won) return;
                DartsThrown++; DartsLeft--; LastBust = false;
                if (mode == Mode.Game301)
                {
                    int s = hit.Score;
                    if (Remaining - s < 0) { Remaining = turnStart; LastBust = true; DartsLeft = 0; }
                    else Remaining -= s;
                    if (Remaining == 0) Won = true;
                }
                else
                {
                    int target = -1;
                    if (hit.zone == Zone.Bull || hit.zone == Zone.OuterBull) target = 6;
                    else if (hit.OnBoard && hit.number >= 15 && hit.number <= 20) target = 20 - hit.number;
                    if (target >= 0)
                        for (int k = 0; k < hit.Marks; k++)
                        {
                            if (marks[target] < 3) marks[target]++;
                            else Points += CricketTargets[target];
                        }
                    if (AllClosed) Won = true;
                }
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
