using System;

namespace BuildingEscape
{
    /// <summary>
    /// The login of the computers in the guard outposts (the owner, 2026-10-17: "to get the room number and codes for safes should no longer be a note but instead a player should have to
    /// use the computer where it then gives them an interface to type in a username and password, the proper username and password should be on a post-it on the computer monitor").
    /// Every outpost's computer has its own user name and password, worked out from the run's seed and the floor alone (so every machine of a networked run - and the post-it that is
    /// built into the world - agrees), made of capital letters and digits only (the dot lettering of the post-it has no others). Free of Unity types, so it is tested outside Unity.
    /// </summary>
    public static class ComputerRules
    {
        public struct Login
        {
            public string user, pass;
        }

        /// <summary>The longest user name or password (the post-it and the login boxes are made for this).</summary>
        public const int MaxLength = 10;

        static readonly string[] UserWords = { "GUARD", "NIGHT", "PATROL", "DESK", "SECURITY", "WATCH", "LOBBY", "RADIO" };
        static readonly string[] PassWords = { "COFFEE", "STAPLER", "MONDAY", "INVOICE", "PRINTER", "LOCKER", "BADGE", "SHIFT", "CLOCK", "ALARM", "TOWER", "LUNCH", "FRIDAY", "WINDOW", "ELEVATOR", "OFFICE" };
        const int SaltUser = 61, SaltUserNumber = 62, SaltPass = 63, SaltPassNumber = 64;

        /// <summary>The user name and password of the computer in the guard outpost of this floor ("GUARD07" and "STAPLER42").</summary>
        public static Login CredentialsFor(int runSeed, int floor)
        {
            string user = UserWords[(int)(SafeRules.Hash(runSeed, floor, SaltUser) % (uint)UserWords.Length)] + (10 + (int)(SafeRules.Hash(runSeed, floor, SaltUserNumber) % 90U));
            string pass = PassWords[(int)(SafeRules.Hash(runSeed, floor, SaltPass) % (uint)PassWords.Length)] + (10 + (int)(SafeRules.Hash(runSeed, floor, SaltPassNumber) % 90U));
            return new Login { user = user, pass = pass };
        }

        /// <summary>Does what was typed open the computer? Capitals and small letters count the same and spaces round the words are ignored: it is typed from a scrap of paper.</summary>
        public static bool Accepts(Login right, string typedUser, string typedPass) => Same(right.user, typedUser) && Same(right.pass, typedPass);

        static bool Same(string a, string b) => a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>Is every character one the post-it's lettering has (capitals and digits)?</summary>
        public static bool IsPostItText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > MaxLength) return false;
            foreach (char c in text) if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            return true;
        }
    }
}
