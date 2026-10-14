using System.Globalization;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What a player chose for their character: body colour, face, hat and build. Saved to PlayerPrefs,
    /// shared with friends as a short string ("40E6B2-1-2-100-100") and rebuilt into a CharacterLook.
    /// Strings from other players are validated and clamped; they are never trusted.
    /// </summary>
    public struct PlayerAppearance
    {
        public const string PrefKey = "OvertimeEscape.Appearance";
        public const float MinBuild = 0.9f, MaxBuild = 1.15f;

        /// <summary>Index 0 is "no hat"; the rest are the pack's hat prefab names.</summary>
        public static readonly string[] HatNames = { null, "party hat", "chef hat", "orange fedora" };
        public static readonly string[] HatLabels = { "None", "Party hat", "Chef hat", "Fedora" };
        public static readonly string[] FaceLabels = { "Grin", "Angry", "Sad" };

        public Color32 body;
        /// <summary>1 = big grin, 2 = angry, 3 = sad.</summary>
        public int face;
        public int hat;
        public float height, width;

        /// <summary>The pack look this player slot would get by default.</summary>
        public static PlayerAppearance ForSlot(int slot)
        {
            var looks = PartyCharacterFactory.PlayerLooks;
            var l = looks[((slot % looks.Length) + looks.Length) % looks.Length];
            int hat = System.Array.IndexOf(HatNames, l.hat);
            return new PlayerAppearance { body = l.body, face = l.face, hat = Mathf.Max(0, hat), height = 1f, width = 1f };
        }

        public CharacterLook ToLook(string name = "Player") =>
            new CharacterLook(name, body, face, HatNames[hat], height, width);

        public static PlayerAppearance Random(System.Random rng)
        {
            float F() => (float)rng.NextDouble();
            return new PlayerAppearance
            {
                body = Color.HSVToRGB(F(), 0.55f + 0.45f * F(), 0.8f + 0.2f * F()),
                face = 1 + rng.Next(3),
                hat = rng.Next(HatNames.Length),
                height = Mathf.Lerp(MinBuild, MaxBuild, F()),
                width = Mathf.Lerp(MinBuild, MaxBuild, F()),
            };
        }

        // ------------------------------------------------------------------ string form

        public string Serialize() =>
            string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}-{3}-{4}-{5}-{6}",
                body.r, body.g, body.b, face, hat, Mathf.RoundToInt(height * 100f), Mathf.RoundToInt(width * 100f));

        public static bool TryParse(string s, out PlayerAppearance a)
        {
            a = ForSlot(0);
            if (string.IsNullOrEmpty(s) || s.Length > 40) return false;
            var p = s.Split('-');
            if (p.Length != 5 || p[0].Length != 6) return false;
            if (!int.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
            if (!int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int face)) return false;
            if (!int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out int hat)) return false;
            if (!int.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out int h)) return false;
            if (!int.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out int w)) return false;

            a.body = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            a.face = Mathf.Clamp(face, 1, 3);
            a.hat = Mathf.Clamp(hat, 0, HatNames.Length - 1);
            a.height = Mathf.Clamp(h / 100f, MinBuild, MaxBuild);
            a.width = Mathf.Clamp(w / 100f, MinBuild, MaxBuild);
            return true;
        }

        // ------------------------------------------------------------------ saved choice

        public static bool TryLoadSaved(out PlayerAppearance a)
        {
#if UNITY_EDITOR
            // Test hook: two Editors on one machine share PlayerPrefs, so a test gives each its own look here.
            var forced = System.Environment.GetEnvironmentVariable("BE_TEST_LOOK");
            if (!string.IsNullOrEmpty(forced)) return TryParse(forced, out a);
#endif
            return TryParse(PlayerPrefs.GetString(PrefKey, ""), out a);
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefKey, Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>The saved look, or the default for slot 0 if the player never customized.</summary>
        public static PlayerAppearance LoadOrDefault() => TryLoadSaved(out var a) ? a : ForSlot(0);
    }
}