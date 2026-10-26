using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The audio devices of this machine, for the settings pages and the voice chat (the owner, 2026-10-24: "Please add Audio Options so players can choose the audio input/output devices they want to use in-game"): the microphones
    /// Windows lists (looked up again every couple of seconds, so a headset that is plugged in shows up), the player's pick (kept in PlayerPrefs by name) and the way to Windows' page for the speakers. The rules are in <see cref="AudioDeviceRules"/>.
    /// </summary>
    public static class AudioDevices
    {
        public const string MicKey = "OvertimeEscape.MicDevice";
        /// <summary>Windows' "App volume and device preferences" page: the output device of this game is picked there (Unity has no call to pick it).</summary>
        public const string SoundSettingsUri = "ms-settings:apps-volume";
        const float RefreshSeconds = 2f;

        static readonly string[] None = new string[0];
        static string[] inputs = None;
        static string[] labels = { AudioDeviceRules.DefaultLabel };
        static float refreshedAt = -100f;
        static string savedMic;
        /// <summary>Tests: these microphones instead of the machine's.</summary>
        public static string[] TestInputs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { inputs = None; labels = new[] { AudioDeviceRules.DefaultLabel }; refreshedAt = -100f; savedMic = null; TestInputs = null; }

        /// <summary>The names of the microphones that are plugged in (an empty list: none).</summary>
        public static string[] Inputs { get { Refresh(); return inputs; } }
        /// <summary>The buttons for them: the default first (<see cref="AudioDeviceRules.Labels"/>).</summary>
        public static string[] InputLabels { get { Refresh(); return labels; } }

        /// <summary>Look the devices up again at the next question (tests, after a change).</summary>
        public static void ForceRefresh() { refreshedAt = -100f; }

        static void Refresh()
        {
            string[] found;
            if (TestInputs != null) found = TestInputs;
            else
            {
                float now = Time.realtimeSinceStartup;
                if (now - refreshedAt < RefreshSeconds) return;
                refreshedAt = now;
                try { found = Microphone.devices; }
                catch (System.Exception) { found = None; }
            }
            if (SameList(found, inputs)) return;
            inputs = found; labels = AudioDeviceRules.Labels(found);
        }

        static bool SameList(string[] a, string[] b)
        {
            if (a == b) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>The microphone the player picked in the settings ("": none picked, the default); kept whether or not it is plugged in.</summary>
        public static string SavedMic
        {
            get { if (savedMic == null) savedMic = PlayerPrefs.GetString(MicKey, ""); return savedMic; }
            set { savedMic = value ?? ""; PlayerPrefs.SetString(MicKey, savedMic); }
        }

        /// <summary>The microphone to open: the pick while it is plugged in, otherwise "" (the default: Steam's voice when Steam is up).</summary>
        public static string ChosenMic => AudioDeviceRules.Chosen(SavedMic, Inputs);

        /// <summary>Windows' page for the speakers can be opened (only on Windows).</summary>
        public static bool CanOpenSoundSettings => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;

        /// <summary>Opens Windows' page where the speakers (the output device) of this game are picked.</summary>
        public static void OpenSoundSettings()
        {
            if (CanOpenSoundSettings) Application.OpenURL(SoundSettingsUri);
        }
    }
}
