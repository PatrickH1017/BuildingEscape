using System.Text;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The name over a player's head (the owner, 2026-10-23, after the playtest with friends: "Players need nametags above their heads"). Pure rules, no Unity types: what a name is allowed to be (the owner of a body writes his own name, so every machine
    /// cleans what it reads: no control characters, no rich-text tags, no more than <see cref="MaxLength"/> characters), what is shown when there is none, and how the tag fades with the distance.
    /// </summary>
    public static class NameRules
    {
        public const int MaxLength = 16;          // (a name is written into a FixedString64Bytes: 16 characters are at most 48 bytes)
        /// <summary>Tags are shown up to this far (m) and fade out from <see cref="FadeFrom"/> on.</summary>
        public const float MaxDistance = 30f, FadeFrom = 18f;

        /// <summary>The name made safe to draw: tags (&lt;...&gt;) and control characters removed, white space turned into single spaces, trimmed, cut to <see cref="MaxLength"/> characters. Null gives "".</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            bool inTag = false, space = false;
            foreach (char c in raw)
            {
                if (inTag) { if (c == '>') inTag = false; continue; }
                if (c == '<') { inTag = true; continue; }
                if (c == '>' || char.IsControl(c) && !char.IsWhiteSpace(c)) continue;
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
            }
            if (sb.Length > MaxLength)
            {
                sb.Length = MaxLength;
                if (char.IsHighSurrogate(sb[sb.Length - 1])) sb.Length--;           // (never half of a character)
                while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
            }
            return sb.ToString();
        }

        /// <summary>The text over a head: his own name, or "Player n" (n = slot + 1, as the game calls him everywhere else) when he has none.</summary>
        public static string Tag(string cleanName, int slot) => string.IsNullOrEmpty(cleanName) ? "Player " + (slot + 1) : cleanName;

        /// <summary>1 up to <see cref="FadeFrom"/>, falling to 0 at <see cref="MaxDistance"/>.</summary>
        public static float Alpha(float distance)
        {
            if (float.IsNaN(distance) || distance >= MaxDistance) return 0f;
            if (distance <= FadeFrom) return 1f;
            return 1f - (distance - FadeFrom) / (MaxDistance - FadeFrom);
        }
    }
}
