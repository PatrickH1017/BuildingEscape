using System;

namespace BuildingEscape
{
    /// <summary>
    /// The dock radio's rules (the owner, 2026-10-27: "add a radio that plays random soundtracks in the dock for some music ... Right now there is only 3 stations so only 3 buttons should be clickable"). Pure arithmetic, no Unity types
    /// (tested with the other pure rules):
    ///  - the stations: <see cref="Names"/>, <see cref="Frequencies"/>; the radio's eight buttons, of which the first three (reading order) are the stations (<see cref="StationOfButton"/>);
    ///  - a station's playlist: its tracks in an order shuffled from a seed (<see cref="Order"/>), played one after the other and from the start again (a loop);
    ///  - where a station is at any moment (<see cref="Locate"/>): the track and the second in it, from the seconds since the station was put on. Every machine of a session works it out from the same shared clock, so the
    ///    whole team hears the same song at the same place without anything more being sent.
    /// </summary>
    public static class RadioRules
    {
        /// <summary>How many stations there are (the owner: "Right now there is only 3 stations").</summary>
        public const int StationCount = 3;
        /// <summary>How many buttons the radio's pad has (the Synty radio: two columns of four).</summary>
        public const int ButtonCount = 8;

        public static readonly string[] Names = { "MIDNIGHT MODE", "HARD ROCK", "URBAN JAZZ" };
        /// <summary>What the radio's display shows for each station.</summary>
        public static readonly string[] Frequencies = { "88.5", "97.1", "104.3" };

        /// <summary>The station a button tunes to (buttons in reading order: left to right, top to bottom), or -1 for a button that is not a station.</summary>
        public static int StationOfButton(int button) => button >= 0 && button < StationCount ? button : -1;

        /// <summary>What a button of the pad does (the owner, 2026-10-27: "add a volume control to the radio as well as an off button"): 1-3 the stations, 4 the power (off / on), 5 the volume down, 6 the volume up; 7 and 8 nothing.</summary>
        public enum Button { None, Station, Power, VolumeDown, VolumeUp }
        public const int PowerButton = 3, VolumeDownButton = 4, VolumeUpButton = 5;

        public static Button FunctionOf(int button) =>
            StationOfButton(button) >= 0 ? Button.Station : button == PowerButton ? Button.Power : button == VolumeDownButton ? Button.VolumeDown : button == VolumeUpButton ? Button.VolumeUp : Button.None;

        // ---- what a press asks for (the same number offline, on the host and in a client's request): 0-2 a station, then the power and the volume
        public const int ActionPower = 10, ActionVolumeDown = 11, ActionVolumeUp = 12;

        /// <summary>The action of a button (-1: none).</summary>
        public static int ActionOf(int button)
        {
            switch (FunctionOf(button))
            {
                case Button.Station: return StationOfButton(button);
                case Button.Power: return ActionPower;
                case Button.VolumeDown: return ActionVolumeDown;
                case Button.VolumeUp: return ActionVolumeUp;
                default: return -1;
            }
        }

        public static bool ValidAction(int action) => ValidStation(action) || action == ActionPower || action == ActionVolumeDown || action == ActionVolumeUp;

        // ---- the volume: 0 (silent) to 10 in steps; 8 is the loudness of before the knob (the music volume of the settings, heard as far as the elevator room's doorway), 10 a quarter louder
        public const int VolumeSteps = 10, DefaultVolume = 8;

        /// <summary>The loudness of a volume step against the default (8 = 1).</summary>
        public static float VolumeGain(int level) => Math.Max(0, Math.Min(VolumeSteps, level)) / (float)DefaultVolume;

        /// <summary>A step up or down, kept within 0..10.</summary>
        public static int StepVolume(int level, int step) => Math.Max(0, Math.Min(VolumeSteps, level + step));

        public static bool ValidStation(int station) => station >= 0 && station < StationCount;

        /// <summary>The order the tracks of a station play in: 0..count-1 shuffled by <paramref name="seed"/> (a Fisher-Yates shuffle on a small hash generator: the same on every machine).</summary>
        public static int[] Order(int count, int seed)
        {
            var o = new int[Math.Max(0, count)];
            for (int i = 0; i < o.Length; i++) o[i] = i;
            uint s = (uint)seed * 2654435761u + 0x9E3779B9u;
            for (int i = o.Length - 1; i > 0; i--)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                int j = (int)(s % (uint)(i + 1));
                int t = o[i]; o[i] = o[j]; o[j] = t;
            }
            return o;
        }

        /// <summary>
        /// Where a station is <paramref name="elapsed"/> seconds after it was put on: <paramref name="slot"/> is the place in <paramref name="order"/> (the track is <c>order[slot]</c>) and <paramref name="offset"/> the seconds into it. The
        /// playlist loops; a moment before the start counts as the start. False when there is nothing to play.
        /// </summary>
        public static bool Locate(float[] lengths, int[] order, double elapsed, out int slot, out float offset)
        {
            slot = 0; offset = 0f;
            if (lengths == null || order == null || order.Length == 0) return false;
            double total = 0;
            foreach (int t in order) total += t >= 0 && t < lengths.Length ? Math.Max(0f, lengths[t]) : 0f;
            if (total <= 0.01) return false;
            if (double.IsNaN(elapsed) || elapsed < 0) elapsed = 0;
            double at = elapsed % total;
            for (int i = 0; i < order.Length; i++)
            {
                float len = order[i] >= 0 && order[i] < lengths.Length ? Math.Max(0f, lengths[order[i]]) : 0f;
                if (at < len) { slot = i; offset = (float)at; return true; }
                at -= len;
            }
            slot = order.Length - 1;
            offset = 0f;
            return true;
        }

        /// <summary>Where in its loop a station starts on the radio's clock, in seconds (0 .. a day, from the seed and the station: the stations do not all begin with their first track at the same moment).</summary>
        public static double StartOffset(int seed, int station)
        {
            uint s = (uint)seed * 2246822519u + (uint)(station + 1) * 3266489917u;
            s ^= s >> 15; s *= 2654435761u; s ^= s >> 13;
            return s % 86400u;
        }

        /// <summary>A track's name for the display, from its file's name: "WAV_Sonar_Dreams_full" -> "Sonar Dreams", "Be Faster_demo" -> "Be Faster", "URBAN_JAZZ(STANDARD_WITH_SOLO)" -> "Urban Jazz (with solo)".</summary>
        public static string TrackTitle(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return "";
            string n = clipName;
            string extra = "";
            int paren = n.IndexOf('(');
            if (paren >= 0)
            {
                string inside = n.Substring(paren + 1).TrimEnd(')').ToUpperInvariant();
                extra = inside.Contains("WITHOUT_SOLO") ? " (without solo)" : inside.Contains("WITH_SOLO") ? " (with solo)" : "";
                n = n.Substring(0, paren);
            }
            if (n.StartsWith("WAV_", StringComparison.OrdinalIgnoreCase)) n = n.Substring(4);
            foreach (var tail in new[] { "_full", "_loop", "_demo" })
                if (n.EndsWith(tail, StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - tail.Length);
            n = n.Replace('_', ' ').Trim();
            // words in capitals become words with a capital ("URBAN JAZZ" -> "Urban Jazz"); mixed words stay as they are ("BIOSphere")
            var words = n.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                var w = words[i];
                if (w.Length > 1 && w.ToUpperInvariant() == w && w.ToLowerInvariant() != w) words[i] = w.Substring(0, 1) + w.Substring(1).ToLowerInvariant();
            }
            return string.Join(" ", words) + extra;
        }
    }
}
