namespace BuildingEscape
{
    /// <summary>
    /// The words of the run-start cinematic (the "Intro"). PLACEHOLDERS written to the owner's brief (the boss tells the loading dock workers, on the wall TV, that
    /// their bonuses are gone this year because he is buying a private jet; afterwards one worker tells another to call somebody - the owner's own line): the owner writes
    /// the game's text, so rewrite them here and nothing else has to move (the start and end seconds of a beat in <see cref="IntroRules.Beats"/> say how long a line
    /// stays up; about 30 letters a second are typed, so a line needs <c>length / 30 + 0.6</c> seconds - the pure test <c>IntroRulesTest</c> checks that).
    /// </summary>
    public static class IntroContent
    {
        /// <summary>Who the subtitle names while the boss talks (on the TV).</summary>
        public const string BossLabel = "THE BOSS";
        public const string SkipHint = "HOLD SPACE TO SKIP";

        public const string Line1 = "Team, gather round. This will be quick.";
        public const string Line2 = "It has been a record year for the company. Record profits. Record growth.";
        public const string Line3 = "Which is why, this year, I am taking away the Loading Dock bonuses.";
        public const string Line4 = "Every last cent. Gone.";
        public const string Line5 = "A man in my position needs a private jet. And jets are not cheap.";
        public const string Line6 = "So the bonus fund is now the jet fund. Now, back to work.";

        /// <summary>
        /// What one worker says to another when the broadcast is over. The owner's own words (2026-10-17), not a placeholder.
        /// </summary>
        public const string FinalLine = "Hey call that guy you were talking about.";

        /// <summary>The name over the worker's line when it is the coworker who stands beside a lone player; a player's character is named by their number.</summary>
        public const string CoworkerLabel = "COWORKER";
        public static string PlayerLabel(int slot) => "PLAYER " + (slot + 1);

        /// <summary>A CharacterCatalog id (the Synty office pack's boss since 2026-10-17 night; before that Tiny Toon male_b); the Party character stands in when this machine has not got it.</summary>
        public const string BossModelId = "syn_boss_male_01";
    }
}
