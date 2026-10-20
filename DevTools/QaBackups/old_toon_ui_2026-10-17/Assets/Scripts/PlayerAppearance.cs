using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What a player chose for their character: which character, and (for the Party character) body colour,
    /// face and hat, plus build. Saved to PlayerPrefs, shared with friends as a short string
    /// ("40E6B2-1-2-100-100", or "40E6B2-1-2-100-100-male_a" for a Tiny Toon person) and rebuilt into a CharacterLook.
    /// Strings from other players are validated and clamped; they are never trusted.
    /// </summary>
    public struct PlayerAppearance
    {
        public const string PrefKey = "OvertimeEscape.Appearance";
        public const float MinBuild = 0.9f, MaxBuild = 1.15f;
        /// <summary>Longest string accepted from another machine (the network field holds 61 bytes).</summary>
        public const int MaxLength = 56;

        /// <summary>Index 0 is "no hat"; the rest are the pack's hat prefab names.</summary>
        public static readonly string[] HatNames = { null, "party hat", "chef hat", "orange fedora" };
        public static readonly string[] HatLabels = { "None", "Party hat", "Chef hat", "Fedora" };
        public static readonly string[] FaceLabels = { "Grin", "Angry", "Sad" };

        public Color32 body;
        /// <summary>1 = big grin, 2 = angry, 3 = sad.</summary>
        public int face;
        public int hat;
        public float height, width;
        /// <summary>A CharacterCatalog id (a Tiny Toon person), or null / empty for the Party character.</summary>
        public string model;

        /// <summary>True when this look is a Tiny Toon person (colour, face and hat then do not apply).</summary>
        public bool IsToon => !string.IsNullOrEmpty(model);

        /// <summary>
        /// The dock workers (the Synty office people) a player is by default, in slot order: the four characters the owner showed (the man with the cap, the blonde woman in dark grey, the man with glasses
        /// and headphones, the man with the red beard and the vest) and the woman cleaner from his first picture (2026-10-17 night: "The Dock workers should be these character models"; before that
        /// "Make the toon the default character").
        /// </summary>
        public static readonly string[] DefaultToons = { "syn_cleaner_male_01", "syn_boss_female_01", "syn_developer_male_02", "syn_business_male_02", "syn_cleaner_female_01" };

        /// <summary>The dock worker a slot is by default, or null on a machine without the office pack (the slot's Party character stands in). An older catalog without the defaults offers whoever can be played.</summary>
        public static string DefaultToon(int slot)
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) return null;
            var ids = new List<string>();
            foreach (var id in DefaultToons) if (cat.Find(id) != null) ids.Add(id);
            if (ids.Count == 0) foreach (var c in cat.Players()) ids.Add(c.id);
            if (ids.Count == 0) return null;
            return ids[((slot % ids.Count) + ids.Count) % ids.Count];
        }

        /// <summary>
        /// A saved look whose character can no longer be played (the Party character, a Tiny Toon person: the office people are the playable characters now) becomes the first slot's dock worker; the rest
        /// of the look (build, colours) is kept. A machine without the office pack keeps what it has.
        /// </summary>
        public static PlayerAppearance Migrated(PlayerAppearance a)
        {
            var cat = CharacterCatalog.Instance;
            if (cat == null) return a;
            var players = cat.Players();
            if (players.Count == 0) return a;
            foreach (var c in players) if (c.id == a.model) return a;
            a.model = DefaultToon(0);
            return a;
        }

        /// <summary>
        /// The look this player slot has until the player chooses one: a Tiny Toon person (the body colour, face and hat are the slot's Party character's, kept for the
        /// roster colours and for a machine without the toon pack).
        /// </summary>
        public static PlayerAppearance ForSlot(int slot)
        {
            var looks = PartyCharacterFactory.PlayerLooks;
            var l = looks[((slot % looks.Length) + looks.Length) % looks.Length];
            int hat = System.Array.IndexOf(HatNames, l.hat);
            return new PlayerAppearance { body = l.body, face = l.face, hat = Mathf.Max(0, hat), height = 1f, width = 1f, model = DefaultToon(slot) };
        }

        public CharacterLook ToLook(string name = "Player") =>
            new CharacterLook(name, body, face, HatNames[hat], height, width, IsToon ? model : null);

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

        public string Serialize()
        {
            string s = string.Format(CultureInfo.InvariantCulture, "{0:X2}{1:X2}{2:X2}-{3}-{4}-{5}-{6}",
                body.r, body.g, body.b, face, hat, Mathf.RoundToInt(height * 100f), Mathf.RoundToInt(width * 100f));
            return IsToon && CharacterCatalog.ValidId(model) ? s + "-" + model : s;
        }

        public static bool TryParse(string s, out PlayerAppearance a)
        {
            a = ForSlot(0);
            if (string.IsNullOrEmpty(s) || s.Length > MaxLength) return false;
            var p = s.Split('-');
            if ((p.Length != 5 && p.Length != 6) || p[0].Length != 6) return false;
            if (!int.TryParse(p[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return false;
            if (!int.TryParse(p[1], NumberStyles.None, CultureInfo.InvariantCulture, out int face)) return false;
            if (!int.TryParse(p[2], NumberStyles.None, CultureInfo.InvariantCulture, out int hat)) return false;
            if (!int.TryParse(p[3], NumberStyles.None, CultureInfo.InvariantCulture, out int h)) return false;
            if (!int.TryParse(p[4], NumberStyles.None, CultureInfo.InvariantCulture, out int w)) return false;
            if (p.Length == 6 && !CharacterCatalog.ValidId(p[5])) return false;

            a.body = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
            a.face = Mathf.Clamp(face, 1, 3);
            a.hat = Mathf.Clamp(hat, 0, HatNames.Length - 1);
            a.height = Mathf.Clamp(h / 100f, MinBuild, MaxBuild);
            a.width = Mathf.Clamp(w / 100f, MinBuild, MaxBuild);
            // an id this machine's catalog does not know still parses (a friend on a newer build); the Party model is shown then
            a.model = p.Length == 6 ? p[5] : null;
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
            bool ok = TryParse(PlayerPrefs.GetString(PrefKey, ""), out a);
            if (ok) a = Migrated(a);                         // (a look saved before the office people came: the Party character or a Tiny Toon person)
            return ok;
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
