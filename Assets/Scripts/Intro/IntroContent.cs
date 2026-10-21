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