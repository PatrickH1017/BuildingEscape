using System;
using System.Security.Cryptography;
using System.Text;

namespace BuildingEscape
{
    /// <summary>
    /// The pure rules of the developer terminal (the owner, 2026-10-23: "CTRL + F1 should open a terminal" and "To access the dev-menu you should need to type @Catdog813 into the terminal that is opened from clicking CTRL + F1. The dev-menu should no longer
    /// be accessible by just clicking F1"): what a typed line is, and whether it is the code that unlocks the dev menu. No Unity types, so the rules are tested outside the editor. The code itself is not in the game, only its SHA-256: a person who opens the
    /// files finds a hash, not the word (the game has no other secret in it: who may spectate a match is decided by Steam accounts, see <c>DevAccessRules</c>).
    /// </summary>
    public static class DevTerminalRules
    {
        /// <summary>The SHA-256 (lower case hex) of the unlock code.</summary>
        public const string CodeHash = "c0067d9f6224627320e835029e8b36ed27942d9e70b428a4bf6feac0b7db583c";

        /// <summary>The longest line the terminal takes, the lines it keeps, and the lines it remembers for the arrow keys.</summary>
        public const int MaxLine = 64, MaxLines = 200, MaxHistory = 30;

        /// <summary>The SHA-256 of a text as 64 lower case hex characters.</summary>
        public static string Hash(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var sb = new StringBuilder(64);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Is this line the unlock code? Exact (capitals count); spaces round it are ignored; compared in constant time.</summary>
        public static bool Accepts(string line)
        {
            var typed = Encoding.ASCII.GetBytes(Hash((line ?? "").Trim()));
            var wanted = Encoding.ASCII.GetBytes(CodeHash);
            return CryptographicOperations.FixedTimeEquals(typed, wanted);
        }

        /// <summary>A typed line cleaned up: spaces trimmed, cut at <see cref="MaxLine"/>, no control characters.</summary>
        public static string Clean(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            var sb = new StringBuilder(Math.Min(line.Length, MaxLine));
            foreach (var c in line) { if (!char.IsControl(c) && sb.Length < MaxLine) sb.Append(c); }
            return sb.ToString().Trim();
        }

        /// <summary>The first word of a line in lower case ("steamid", "spectate", ...), and the rest of it.</summary>
        public static string Verb(string line, out string rest)
        {
            line = Clean(line);
            int i = line.IndexOf(' ');
            if (i < 0) { rest = ""; return line.ToLowerInvariant(); }
            rest = line.Substring(i + 1).Trim();
            return line.Substring(0, i).ToLowerInvariant();
        }

        /// <summary>Does the F1 key open (or close) the dev menu: F1 alone (CTRL + F1 is the terminal's), once the code was typed in the terminal, and not while the terminal is up?</summary>
        public static bool MenuKeyOpens(bool f1Pressed, bool ctrlHeld, bool unlocked, bool terminalOpen) => f1Pressed && !ctrlHeld && unlocked && !terminalOpen;

        /// <summary>The line before / after the one shown, in a history of <paramref name="count"/> lines (the up and down arrows): -1 is "nothing, a blank line". <paramref name="at"/> is where the history is now, -1 at the blank line.</summary>
        public static int HistoryStep(int at, int count, int direction)
        {
            if (count <= 0) return -1;
            if (direction < 0) return at < 0 ? count - 1 : Math.Max(0, at - 1);
            return at < 0 ? -1 : at + 1 >= count ? -1 : at + 1;
        }
    }
}
