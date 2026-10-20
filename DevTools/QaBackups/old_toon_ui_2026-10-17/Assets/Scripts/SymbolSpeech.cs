namespace BuildingEscape
{
    /// <summary>
    /// What guards "say" in their speech bubbles: a few random symbols like a comic-book curse, never letters or digits, so it can
    /// never spell a real word. Plain C# (no Unity types) so it can be tested outside Unity.
    /// </summary>
    public static class SymbolSpeech
    {
        /// <summary>The symbols a bubble is made of.</summary>
        public const string Symbols = "#@$%&*!?~^+=/|";
        public const int MinLength = 3, MaxLength = 6;

        /// <summary>3 to 6 symbols, never the same one twice in a row, so it reads like "#@%&!" and never like a lone "!".</summary>
        public static string Make(SynthRandom rng)
        {
            int n = MinLength + rng.Int(MaxLength - MinLength + 1);
            var chars = new char[n];
            char previous = '\0';
            for (int i = 0; i < n; i++)
            {
                char c;
                do { c = Symbols[rng.Int(Symbols.Length)]; } while (c == previous);
                chars[i] = previous = c;
            }
            if (rng.Chance(0.35f) && chars[n - 2] != '!') chars[n - 1] = '!';   // shouted
            return new string(chars);
        }
    }
}
