using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The rules of the audio device choice (the owner, 2026-10-24: "Please add Audio Options so players can choose the audio input/output devices they want to use in-game"), without Unity types so they are tested outside Unity.
    /// The microphone is picked from the list of the devices Windows reports and the pick is kept by NAME, not by place (a device's place in the list changes when something is plugged in or out); a pick that is not plugged in
    /// any more counts as no pick (the default) until it is back. A microphone that was picked is opened by Unity itself (<see cref="Net.MicrophoneVoiceInput"/>): Steam's voice recording takes only the device Windows uses by default.
    /// The speakers cannot be picked inside the game (Unity plays through the device Windows uses by default and has no call to change it), so the settings page opens Windows' own page for it (<see cref="AudioDevices.OpenSoundSettings"/>).
    /// </summary>
    public static class AudioDeviceRules
    {
        /// <summary>The first choice of the list: no pick, the device Windows (and Steam) use by default.</summary>
        public const string DefaultLabel = "DEFAULT";
        /// <summary>The longest name a button shows (the device names of Windows can be long, "Microphone (Realtek High Definition Audio)").</summary>
        public const int LabelChars = 34;

        /// <summary>Where the saved pick <paramref name="saved"/> is in the list of the plugged-in devices; -1 when nothing was picked, or the pick is not plugged in.</summary>
        public static int Resolve(string saved, IList<string> devices)
        {
            if (string.IsNullOrEmpty(saved) || devices == null) return -1;
            for (int i = 0; i < devices.Count; i++) if (devices[i] == saved) return i;
            return -1;
        }

        /// <summary>The device to open: the saved pick when it is plugged in, otherwise "" (the default).</summary>
        public static string Chosen(string saved, IList<string> devices)
        {
            int i = Resolve(saved, devices);
            return i < 0 ? "" : devices[i];
        }

        /// <summary>A name as a button shows it: trimmed, and cut short with "..." when it is longer than <see cref="LabelChars"/>.</summary>
        public static string Shorten(string name)
        {
            name = (name ?? "").Trim();
            return name.Length <= LabelChars ? name : name.Substring(0, LabelChars - 3).TrimEnd() + "...";
        }

        /// <summary>The buttons of the list: the default first, then one for each device (two that read alike are numbered, "Name", "Name (2)").</summary>
        public static string[] Labels(IList<string> devices)
        {
            int n = devices == null ? 0 : devices.Count;
            var labels = new string[n + 1];
            labels[0] = DefaultLabel;
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                string t = Shorten(devices[i]);
                seen.TryGetValue(t, out int before);
                seen[t] = before + 1;
                labels[i + 1] = before == 0 ? t : t + " (" + (before + 1) + ")";
            }
            return labels;
        }

        /// <summary>Which button is lit: 0 for the default (nothing picked, or the pick is gone), otherwise the place of the device in the list plus one.</summary>
        public static int SelectedOption(string saved, IList<string> devices) => Resolve(saved, devices) + 1;

        /// <summary>The device name to save for a button: "" for the default (button 0), "" for a button that is not there.</summary>
        public static string NameForOption(int option, IList<string> devices) => devices == null || option <= 0 || option > devices.Count ? "" : devices[option - 1];

        /// <summary>Is Steam's own voice recording used (it takes the device Windows uses by default and cannot be pointed at another)? Only while no microphone was picked and Steam is up.</summary>
        public static bool SteamVoice(string chosen, bool steamReady) => steamReady && string.IsNullOrEmpty(chosen);
    }
}
